const crypto = require('node:crypto');
const db = require('./db');
const gameConfig = require('./gameConfig');
const progression = require('./progressionService');
const { boxRarities, adjustedSlimeWeight } = require('./boxConfig');

function weightedPick(slimes, randomInt = (maximum) => crypto.randomInt(maximum)) {
  const totalWeight = slimes.reduce((sum, slime) => sum + slime.drop_weight, 0);
  if (!Number.isSafeInteger(totalWeight) || totalWeight <= 0) {
    const error = new Error('史莱姆掉落权重配置无效');
    error.status = 503;
    throw error;
  }
  let roll = randomInt(totalWeight);
  for (const slime of slimes) {
    roll -= slime.drop_weight;
    if (roll < 0) return slime;
  }
  throw new Error('随机抽取失败');
}

function createGameService({ database = db, now = () => new Date(), randomInt, config = gameConfig } = {}) {
  const random = randomInt || ((maximum) => crypto.randomInt(maximum));

  function boxIntervalMs(user) {
    return Math.max(config.minBoxGenerationIntervalMs,
      config.boxGenerationIntervalMs - (user.magic_level || 0) * config.magicNodeIntervalReductionMs);
  }

  function settleBoxes(user, currentTime) {
    if (user.box_count >= user.box_capacity) {
      return { boxCount: user.box_count, lastBoxAt: currentTime };
    }
    const lastBoxAt = new Date(user.last_box_at);
    const interval = boxIntervalMs(user);
    const elapsedMs = Math.max(0, currentTime.getTime() - lastBoxAt.getTime());
    const generated = Math.floor(elapsedMs / interval);
    const accepted = Math.min(generated, user.box_capacity - user.box_count);
    return {
      boxCount: user.box_count + accepted,
      lastBoxAt: accepted > 0
        ? new Date(lastBoxAt.getTime() + accepted * interval)
        : lastBoxAt,
    };
  }

  function buildGameState(user, currentTime) {
    const interval = boxIntervalMs(user);
    return {
      box_count: user.box_count,
      box_capacity: user.box_capacity,
      box_interval_seconds: Math.round(interval / 1000),
      total_cp: user.total_cp,
      server_time: currentTime.toISOString(),
      next_box_at: user.box_count >= user.box_capacity
        ? null
        : new Date(new Date(user.last_box_at).getTime() + interval).toISOString(),
    };
  }

  async function settleUserState(tx, userId, currentTime) {
    const user = await tx.get(
      `SELECT u.id, u.total_cp, u.box_count, u.box_capacity, u.last_box_at,
        COALESCE(g.magic_level, 0) AS magic_level
       FROM users u LEFT JOIN guild_members gm ON gm.user_id = u.id
       LEFT JOIN guilds g ON g.id = gm.guild_id WHERE u.id = ?`,
      [userId],
    );
    if (!user) {
      const error = new Error('玩家不存在');
      error.status = 404;
      throw error;
    }
    const settled = settleBoxes(user, currentTime);
    const timestamp = settled.lastBoxAt.toISOString();
    await tx.run('UPDATE users SET box_count = ?, last_box_at = ? WHERE id = ?', [settled.boxCount, timestamp, userId]);
    return { ...user, box_count: settled.boxCount, last_box_at: timestamp };
  }

  async function getGameState(userId) {
    return database.transaction(async (tx) => {
      const currentTime = now();
      const state = buildGameState(await settleUserState(tx, userId, currentTime), currentTime);
      state.box_inventory = await getBoxInventory(tx, userId);
      return state;
    });
  }

  async function getBoxInventory(tx, userId) {
    const rows = await tx.all('SELECT box_rarity_key, quantity FROM user_box_inventory WHERE user_id = ?', [userId]);
    return boxRarities.map((box) => ({
      key: box.key,
      name: box.name,
      color: box.color,
      quantity: rows.find((row) => row.box_rarity_key === box.key)?.quantity || 0,
    }));
  }

  async function claimBoxes(userId) {
    return database.transaction(async (tx) => {
      const currentTime = now();
      const user = await settleUserState(tx, userId, currentTime);
      if (user.box_count <= 0) {
        const error = new Error('当前没有可领取的盲盒');
        error.status = 409;
        throw error;
      }
      const gained = {};
      for (let index = 0; index < user.box_count; index += 1) {
        const rarity = weightedPick(boxRarities.map((box) => ({ ...box, drop_weight: box.weight })), random);
        gained[rarity.key] = (gained[rarity.key] || 0) + 1;
      }
      for (const [key, quantity] of Object.entries(gained)) {
        await tx.run(
          `INSERT INTO user_box_inventory (user_id, box_rarity_key, quantity) VALUES (?, ?, ?)
           ON CONFLICT(user_id, box_rarity_key) DO UPDATE SET quantity = quantity + excluded.quantity`,
          [userId, key, quantity],
        );
      }
      await tx.run('UPDATE users SET box_count = 0, last_box_at = ? WHERE id = ?', [currentTime.toISOString(), userId]);
      const updatedUser = { ...user, box_count: 0, last_box_at: currentTime.toISOString() };
      return { claimed: user.box_count, gained, game_state: { ...buildGameState(updatedUser, currentTime), box_inventory: await getBoxInventory(tx, userId) } };
    });
  }

  async function openBox(userId, boxRarityKey = 'WHITE') {
    return database.transaction(async (tx) => {
      const currentTime = now();
      const user = await settleUserState(tx, userId, currentTime);
      const box = boxRarities.find((item) => item.key === boxRarityKey);
      if (!box) { const error = new Error('未知盲盒品质'); error.status = 400; throw error; }
      const stored = await tx.get('SELECT quantity FROM user_box_inventory WHERE user_id = ? AND box_rarity_key = ?', [userId, box.key]);
      if (!stored || stored.quantity <= 0) {
        const error = new Error(`仓库中没有${box.name}盲盒`);
        error.status = 409;
        throw error;
      }
      const slimes = await tx.all(
        `SELECT slime_id, name, rarity, rarity_key, rarity_name, rarity_color, base_cp, drop_weight
         FROM slime_dictionary WHERE drop_weight > 0 ORDER BY slime_id`,
      );
      const adjusted = slimes.map((slime) => ({ ...slime, drop_weight: adjustedSlimeWeight(slime.drop_weight, slime.rarity_key, box.key) }));
      const result = weightedPick(adjusted, random);
      const updatedUser = { ...user };
      await tx.run('UPDATE user_box_inventory SET quantity = quantity - 1 WHERE user_id = ? AND box_rarity_key = ?', [userId, box.key]);
      await progression.grantSlime(tx, userId, result.slime_id);
      const total = await tx.get('SELECT total_cp FROM users WHERE id = ?', [userId]);
      updatedUser.total_cp = total.total_cp;
      return { slime_id: result.slime_id, name: result.name, rarity: result.rarity_name, rarity_key: result.rarity_key, rarity_color: result.rarity_color, box_rarity_key: box.key, box_rarity: box.name, game_state: { ...buildGameState(updatedUser, currentTime), box_inventory: await getBoxInventory(tx, userId) } };
    });
  }

  return { getGameState, claimBoxes, openBox, settleBoxes };
}

const defaultService = createGameService();
module.exports = { weightedPick, createGameService, ...defaultService };
