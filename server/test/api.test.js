const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const request = require('supertest');

const testDatabase = path.join(__dirname, 'slime-test.db');
process.env.NODE_ENV = 'test';
process.env.DATABASE_PATH = testDatabase;
process.env.JWT_SECRET = 'test-secret-that-is-long-enough';
process.env.ALLOW_DEV_AUTH = 'true';

const db = require('../src/db');
const { createApp } = require('../src/app');
const { createGameService } = require('../src/gameService');
const { rarities, totalWeight } = require('../src/rarityConfig');
const { boxRarities, adjustedSlimeWeight } = require('../src/boxConfig');

let app;
let token;

test.before(async () => {
  for (const suffix of ['', '-shm', '-wal']) {
    if (fs.existsSync(testDatabase + suffix)) fs.unlinkSync(testDatabase + suffix);
  }
  await db.initializeDatabase();
  app = createApp();
  const login = await request(app).post('/api/auth/login').send({ steamTicket: 'dev:tester' });
  assert.equal(login.status, 200);
  token = login.body.token;
});

test.after(async () => {
  await db.closeDatabase();
  for (const suffix of ['', '-shm', '-wal']) {
    if (fs.existsSync(testDatabase + suffix)) fs.unlinkSync(testDatabase + suffix);
  }
});

test('缺少 JWT 时拒绝开箱', async () => {
  const response = await request(app).post('/api/game/open-box');
  assert.equal(response.status, 401);
});

test('新账号获得一个初始盲盒并返回权威时间', async () => {
  const response = await request(app)
    .get('/api/game/state')
    .set('Authorization', `Bearer ${token}`);
  assert.equal(response.status, 200);
  assert.equal(response.body.box_count, 1);
  assert.equal(response.body.box_capacity, 8);
  assert.equal(response.body.box_interval_seconds, 3 * 60 * 60);
  assert.equal(response.body.residue, undefined);
  assert.ok(response.body.server_time);
  assert.ok(response.body.next_box_at);
});

test('登录、开箱并读取库存', async () => {
  const claimed = await request(app)
    .post('/api/game/claim-boxes')
    .set('Authorization', `Bearer ${token}`);
  assert.equal(claimed.status, 200);
  const storedBox = claimed.body.game_state.box_inventory.find((box) => box.quantity > 0);
  const opened = await request(app)
    .post('/api/game/open-box')
    .set('Authorization', `Bearer ${token}`)
    .send({ box_rarity_key: storedBox.key });
  assert.equal(opened.status, 200);
  assert.ok(Number.isInteger(opened.body.slime_id));

  const inventory = await request(app)
    .get('/api/user/inventory')
    .set('Authorization', `Bearer ${token}`);
  assert.equal(inventory.status, 200);
  assert.equal(inventory.body.inventory.length, 1);
  assert.equal(inventory.body.inventory[0].quantity, 1);
});

test('无盲盒时拒绝开箱且库存不变', async () => {
  const user = await db.get("SELECT id FROM users WHERE steam_id = 'dev_tester'");
  await db.run('UPDATE users SET box_count = 0, last_box_at = ? WHERE id = ?', [new Date().toISOString(), user.id]);
  const before = await request(app).get('/api/user/inventory').set('Authorization', `Bearer ${token}`);
  const response = await request(app).post('/api/game/open-box').set('Authorization', `Bearer ${token}`);
  assert.equal(response.status, 409);
  const after = await request(app).get('/api/user/inventory').set('Authorization', `Bearer ${token}`);
  assert.deepEqual(after.body, before.body);
});

test('离线盲盒补发并封顶', async () => {
  const user = await db.get("SELECT id FROM users WHERE steam_id = 'dev_tester'");
  await db.run("UPDATE users SET box_count = 0, last_box_at = '2026-01-01T00:00:00.000Z' WHERE id = ?", [user.id]);
  const service = createGameService({ now: () => new Date('2026-01-03T00:00:00.000Z') });
  const state = await service.getGameState(user.id);
  assert.equal(state.box_count, 8);
  assert.equal(state.next_box_at, null);
});

test('已移除的融合与排行榜接口返回 404', async () => {
  const merged = await request(app).post('/api/game/merge').set('Authorization', `Bearer ${token}`)
    .send({ consume_slimes: [{ slime_id: 1, count: 5 }] });
  assert.equal(merged.status, 404);
  const leaderboard = await request(app).get('/api/social/leaderboard').set('Authorization', `Bearer ${token}`);
  assert.equal(leaderboard.status, 404);
});

test('图鉴会记录已获得的史莱姆', async () => {
  const response = await request(app).get('/api/user/dictionary').set('Authorization', `Bearer ${token}`);
  assert.equal(response.status, 200);
  assert.ok(response.body.total >= 7);
  assert.ok(response.body.discovered >= 1);
});

test('GM 可填满、清空并增加盲盒', async () => {
  let response = await request(app).post('/api/dev/gm/boxes').set('Authorization', `Bearer ${token}`).send({ action: 'fill', amount: 0 });
  assert.equal(response.status, 200);
  assert.equal(response.body.box_count, response.body.box_capacity);
  response = await request(app).post('/api/dev/gm/boxes').set('Authorization', `Bearer ${token}`).send({ action: 'clear', amount: 0 });
  assert.equal(response.body.box_count, 0);
  response = await request(app).post('/api/dev/gm/boxes').set('Authorization', `Bearer ${token}`).send({ action: 'add', amount: 1 });
  assert.equal(response.body.box_count, 1);
});

test('可创建个人公会并读取本周 Boss', async () => {
  const created = await request(app).post('/api/guild/create').set('Authorization', `Bearer ${token}`).send({ name: '测试公会' });
  assert.equal(created.status, 200);
  const state = await request(app).get('/api/guild/state').set('Authorization', `Bearer ${token}`);
  assert.equal(state.status, 200);
  assert.equal(state.body.has_guild, true);
  assert.equal(state.body.boss.max_hunger, 1000);
});

test('公会捐献增加资金并可升级扩容科技', async () => {
  const user = await db.get("SELECT id FROM users WHERE steam_id = 'dev_tester'");
  await db.run(
    `INSERT INTO user_slimes (user_id, slime_id, quantity) VALUES (?, 2, 10)
     ON CONFLICT(user_id, slime_id) DO UPDATE SET quantity = 10`, [user.id]);
  await db.run(
    `INSERT INTO user_inventory (user_id, slime_id, quantity) VALUES (?, 2, 10)
     ON CONFLICT(user_id, slime_id) DO UPDATE SET quantity = 10`, [user.id]);
  const donated = await request(app).post('/api/guild/donate').set('Authorization', `Bearer ${token}`)
    .send({ slime_id: 2, count: 10 });
  assert.equal(donated.status, 200);
  assert.equal(donated.body.guild_funds_gained, 100);
  const upgraded = await request(app).post('/api/guild/upgrade').set('Authorization', `Bearer ${token}`)
    .send({ type: 'capacity' });
  assert.equal(upgraded.status, 200);
  const state = await request(app).get('/api/game/state').set('Authorization', `Bearer ${token}`);
  assert.equal(state.body.box_capacity, 10);
});

test('魔力节点缩短产出间隔并同步返回给客户端', async () => {
  const user = await db.get("SELECT id FROM users WHERE steam_id = 'dev_tester'");
  await db.run('UPDATE guilds SET magic_level = 2 WHERE id = (SELECT guild_id FROM guild_members WHERE user_id = ?)', [user.id]);
  await db.run("UPDATE users SET box_count = 0, last_box_at = '2026-01-01T00:00:00.000Z' WHERE id = ?", [user.id]);
  const service = createGameService({ now: () => new Date('2026-01-01T00:30:00.000Z') });
  const state = await service.getGameState(user.id);
  assert.equal(state.box_interval_seconds, 2 * 60 * 60);
  assert.equal(state.box_count, 0);
  assert.equal(state.next_box_at, '2026-01-01T02:00:00.000Z');
  await db.run('UPDATE guilds SET magic_level = 0 WHERE id = (SELECT guild_id FROM guild_members WHERE user_id = ?)', [user.id]);
});

test('公会 Boss 每天只能投喂一次', async () => {
  const first = await request(app).post('/api/guild/boss/feed').set('Authorization', `Bearer ${token}`);
  assert.equal(first.status, 200);
  const second = await request(app).post('/api/guild/boss/feed').set('Authorization', `Bearer ${token}`);
  assert.equal(second.status, 409);
});

test('十二档品质权重完整且总概率为 100%', () => {
  assert.equal(rarities.length, 12);
  assert.equal(totalWeight, 1_000_000);
  assert.deepEqual(rarities.map((item) => item.name),
    ['劣质', '普通', '优秀', '精良', '极品', '史诗', '传说', '神器', '魔王', '战神', '至尊', '魔神']);
});

test('五档盲盒品质概率总和为 100%', () => {
  assert.deepEqual(boxRarities.map((box) => box.weight), [4500, 3000, 1500, 800, 200]);
  assert.equal(boxRarities.reduce((sum, box) => sum + box.weight, 0), 10000);
});

test('稀有盲盒提高高品质史莱姆相对权重', () => {
  const base = 1000;
  assert.ok(adjustedSlimeWeight(base, 'LEGENDARY', 'RED') > adjustedSlimeWeight(base, 'LEGENDARY', 'WHITE'));
  assert.ok(adjustedSlimeWeight(base, 'POOR', 'RED') < adjustedSlimeWeight(base, 'POOR', 'WHITE'));
});
