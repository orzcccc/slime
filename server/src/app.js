const express = require('express');
const cors = require('cors');
const helmet = require('helmet');
const rateLimit = require('express-rate-limit');
const db = require('./db');
const { authenticateSteamTicket } = require('./steamAuth');
const { signToken, requireAuth } = require('./auth');
const defaultGameService = require('./gameService');
const gameConfig = require('./gameConfig');
const progression = require('./progressionService');
const guildService = require('./guildService');
const config = require('./config');

function createApp({ gameService = defaultGameService } = {}) {
  const app = express();
  app.use(helmet());
  app.use(cors());
  app.use(express.json({ limit: '32kb' }));

  app.get('/health', (_req, res) => res.json({ status: 'ok' }));

  app.post(
    '/api/auth/login',
    rateLimit({ windowMs: 60_000, limit: 10, standardHeaders: true, legacyHeaders: false }),
    async (req, res, next) => {
      try {
        const identity = await authenticateSteamTicket(req.body?.steamTicket);
        const loginTime = new Date().toISOString();
        await db.run(
          `INSERT INTO users (steam_id, nickname, box_count, box_capacity, created_at, last_login_at, last_box_at)
           VALUES (?, ?, ?, ?, ?, ?, ?)
           ON CONFLICT(steam_id) DO UPDATE SET
             nickname = excluded.nickname,
             last_login_at = excluded.last_login_at`,
          [identity.steamId, identity.nickname, gameConfig.initialBoxCount, gameConfig.defaultBoxCapacity, loginTime, loginTime, loginTime],
        );
        const user = await db.get(
          'SELECT id, steam_id, nickname, total_cp, residue FROM users WHERE steam_id = ?',
          [identity.steamId],
        );
        res.json({
          token: signToken(user),
          user: { steam_id: user.steam_id, nickname: user.nickname, total_cp: user.total_cp, residue: user.residue },
        });
      } catch (error) {
        next(error);
      }
    },
  );

  app.post('/api/game/open-box', requireAuth, async (req, res, next) => {
    try {
      res.json(await gameService.openBox(req.user.id, req.body?.box_rarity_key));
    } catch (error) {
      next(error);
    }
  });

  app.post('/api/game/claim-boxes', requireAuth, async (req, res, next) => {
    try { res.json(await gameService.claimBoxes(req.user.id)); } catch (error) { next(error); }
  });

  app.post('/api/game/merge', requireAuth, async (req, res, next) => {
    try {
      res.json(await gameService.mergeSlimes(req.user.id, req.body?.consume_slimes));
    } catch (error) {
      next(error);
    }
  });

  app.get('/api/game/state', requireAuth, async (req, res, next) => {
    try {
      res.json(await gameService.getGameState(req.user.id));
    } catch (error) {
      next(error);
    }
  });

  app.get('/api/user/inventory', requireAuth, async (req, res, next) => {
    try {
      const inventory = await progression.getInventory(req.user.id);
      res.json({ inventory });
    } catch (error) {
      next(error);
    }
  });

  app.get('/api/user/dictionary', requireAuth, async (req, res, next) => {
    try {
      res.json(await progression.getDictionary(req.user.id));
    } catch (error) {
      next(error);
    }
  });

  app.get('/api/guild/state', requireAuth, async (req, res, next) => {
    try { res.json(await guildService.getState(req.user.id)); } catch (error) { next(error); }
  });
  app.post('/api/guild/create', requireAuth, async (req, res, next) => {
    try { res.json(await guildService.createGuild(req.user.id, req.body?.name)); } catch (error) { next(error); }
  });
  app.post('/api/guild/donate', requireAuth, async (req, res, next) => {
    try { res.json(await guildService.donate(req.user.id, req.body?.slime_id, req.body?.count)); } catch (error) { next(error); }
  });
  app.post('/api/guild/upgrade', requireAuth, async (req, res, next) => {
    try { res.json(await guildService.upgrade(req.user.id, req.body?.type)); } catch (error) { next(error); }
  });
  app.post('/api/guild/boss/feed', requireAuth, async (req, res, next) => {
    try { res.json(await guildService.feedBoss(req.user.id)); } catch (error) { next(error); }
  });

  if (config.allowDevAuth) {
    app.post('/api/dev/gm/boxes', requireAuth, async (req, res, next) => {
      try {
        if (!String(req.user.steam_id).startsWith('dev_')) return res.status(403).json({ error: '仅开发账号可使用 GM' });
        const action = req.body?.action;
        const amount = Number(req.body?.amount || 0);
        if (!['add', 'clear', 'fill', 'advance'].includes(action)) return res.status(400).json({ error: '未知 GM 操作' });
        await db.transaction(async (tx) => {
          if (action === 'add') {
            if (!Number.isInteger(amount) || amount < 1 || amount > 100) { const error = new Error('增加数量范围为 1-100'); error.status = 400; throw error; }
            await tx.run('UPDATE users SET box_count = MIN(box_capacity, box_count + ?) WHERE id = ?', [amount, req.user.id]);
          } else if (action === 'clear') {
            await tx.run('UPDATE users SET box_count = 0, last_box_at = ? WHERE id = ?', [new Date().toISOString(), req.user.id]);
          } else if (action === 'fill') {
            await tx.run('UPDATE users SET box_count = box_capacity WHERE id = ?', [req.user.id]);
          } else {
            if (!Number.isInteger(amount) || amount < 1 || amount > 168) { const error = new Error('推进小时范围为 1-168'); error.status = 400; throw error; }
            await tx.run("UPDATE users SET last_box_at = datetime(last_box_at, '-' || ? || ' hours') WHERE id = ?", [amount, req.user.id]);
          }
        });
        res.json(await gameService.getGameState(req.user.id));
      } catch (error) { next(error); }
    });
  }

  app.get('/api/social/leaderboard', requireAuth, async (_req, res, next) => {
    try {
      const leaderboard = await db.all(
        `SELECT steam_id, nickname, total_cp FROM users
         ORDER BY total_cp DESC, id ASC LIMIT 50`,
      );
      res.json({ leaderboard });
    } catch (error) {
      next(error);
    }
  });

  app.use((_req, res) => res.status(404).json({ error: '接口不存在' }));
  app.use((error, _req, res, _next) => {
    const status = Number.isInteger(error.status) ? error.status : 500;
    if (status >= 500) console.error(error);
    res.status(status).json({ error: status >= 500 ? '服务器内部错误' : error.message });
  });

  return app;
}

module.exports = { createApp };
