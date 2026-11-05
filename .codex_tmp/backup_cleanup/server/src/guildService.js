const db = require('./db');
const config = require('./gameConfig');
const progression = require('./progressionService');

function weekKey(date = new Date()) {
  const copy = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate()));
  const day = copy.getUTCDay() || 7;
  copy.setUTCDate(copy.getUTCDate() + 4 - day);
  const yearStart = new Date(Date.UTC(copy.getUTCFullYear(), 0, 1));
  const week = Math.ceil((((copy - yearStart) / 86400000) + 1) / 7);
  return `${copy.getUTCFullYear()}-W${String(week).padStart(2, '0')}`;
}

async function getMembership(tx, userId) {
  return tx.get(
    `SELECT g.*, gm.contribution FROM guild_members gm JOIN guilds g ON g.id = gm.guild_id WHERE gm.user_id = ?`,
    [userId],
  );
}

async function ensureBoss(tx, guildId, now = new Date()) {
  const key = weekKey(now);
  await tx.run(
    `INSERT OR IGNORE INTO guild_bosses (guild_id, week_key, max_hunger, current_hunger)
     VALUES (?, ?, ?, ?)`,
    [guildId, key, config.guildBossMaxHunger, config.guildBossMaxHunger],
  );
  return tx.get('SELECT * FROM guild_bosses WHERE guild_id = ? AND week_key = ?', [guildId, key]);
}

async function getState(userId) {
  return db.transaction(async (tx) => {
    const guild = await getMembership(tx, userId);
    if (!guild) return { has_guild: false };
    const boss = await ensureBoss(tx, guild.id);
    const user = await tx.get('SELECT guild_coins FROM users WHERE id = ?', [userId]);
    return { has_guild: true, guild, boss, guild_coins: user.guild_coins };
  });
}

async function createGuild(userId, name) {
  const cleanName = typeof name === 'string' ? name.trim().slice(0, 24) : '';
  if (cleanName.length < 2) {
    const error = new Error('公会名称至少需要 2 个字符');
    error.status = 400;
    throw error;
  }
  return db.transaction(async (tx) => {
    if (await getMembership(tx, userId)) {
      const error = new Error('当前已加入公会');
      error.status = 409;
      throw error;
    }
    const result = await tx.run('INSERT INTO guilds (name, owner_user_id) VALUES (?, ?)', [cleanName, userId]);
    await tx.run('INSERT INTO guild_members (guild_id, user_id) VALUES (?, ?)', [result.id, userId]);
    await ensureBoss(tx, result.id);
    return { guild_id: result.id, name: cleanName };
  });
}

async function donate(userId, slimeId, count) {
  if (!Number.isInteger(count) || count <= 0) {
    const error = new Error('捐献数量必须为正整数');
    error.status = 400;
    throw error;
  }
  return db.transaction(async (tx) => {
    const guild = await getMembership(tx, userId);
    if (!guild) { const error = new Error('尚未加入公会'); error.status = 409; throw error; }
    const totalCp = await progression.removeSlime(tx, userId, slimeId, count);
    const coins = count * config.guildCoinsPerSlime;
    const funds = count * config.guildFundsPerSlime;
    await tx.run('UPDATE users SET guild_coins = guild_coins + ? WHERE id = ?', [coins, userId]);
    await tx.run('UPDATE guilds SET funds = funds + ? WHERE id = ?', [funds, guild.id]);
    await tx.run('UPDATE guild_members SET contribution = contribution + ? WHERE guild_id = ? AND user_id = ?', [funds, guild.id, userId]);
    return { donated: count, guild_coins_gained: coins, guild_funds_gained: funds, total_cp: totalCp };
  });
}

async function upgrade(userId, type) {
  if (!['magic', 'capacity'].includes(type)) { const error = new Error('未知科技类型'); error.status = 400; throw error; }
  return db.transaction(async (tx) => {
    const guild = await getMembership(tx, userId);
    if (!guild) { const error = new Error('尚未加入公会'); error.status = 409; throw error; }
    const levelField = type === 'magic' ? 'magic_level' : 'capacity_level';
    const costs = type === 'magic' ? config.magicNodeCosts : config.capacityNodeCosts;
    const level = guild[levelField];
    if (level >= costs.length) { const error = new Error('科技已满级'); error.status = 409; throw error; }
    const cost = costs[level];
    if (guild.funds < cost) { const error = new Error('公会资金不足'); error.status = 409; throw error; }
    await tx.run(`UPDATE guilds SET funds = funds - ?, ${levelField} = ${levelField} + 1 WHERE id = ?`, [cost, guild.id]);
    if (type === 'capacity') {
      await tx.run('UPDATE users SET box_capacity = 8 + ? WHERE id IN (SELECT user_id FROM guild_members WHERE guild_id = ?)', [(level + 1) * config.capacityPerLevel, guild.id]);
    }
    return { type, level: level + 1, cost };
  });
}

async function feedBoss(userId, now = new Date()) {
  return db.transaction(async (tx) => {
    const guild = await getMembership(tx, userId);
    if (!guild) { const error = new Error('尚未加入公会'); error.status = 409; throw error; }
    const boss = await ensureBoss(tx, guild.id, now);
    const today = now.toISOString().slice(0, 10);
    if (boss.last_fed_date === today) { const error = new Error('今天已经投喂过 Boss'); error.status = 409; throw error; }
    const user = await tx.get('SELECT total_cp, box_count, box_capacity FROM users WHERE id = ?', [userId]);
    const damage = Math.max(1, Math.floor(user.total_cp / config.guildBossDamageDivisor));
    const hunger = Math.max(0, boss.current_hunger - damage);
    const completed = hunger === 0;
    await tx.run('UPDATE guild_bosses SET current_hunger = ?, completed = ?, last_fed_date = ? WHERE guild_id = ? AND week_key = ?', [hunger, completed ? 1 : 0, today, guild.id, boss.week_key]);
    let reward = 0;
    if (completed && !boss.reward_claimed) {
      reward = config.guildBossRewardBoxes;
      await tx.run('UPDATE users SET box_count = MIN(box_capacity, box_count + ?) WHERE id = ?', [reward, userId]);
      await tx.run('UPDATE guild_bosses SET reward_claimed = 1 WHERE guild_id = ? AND week_key = ?', [guild.id, boss.week_key]);
    }
    return { damage, current_hunger: hunger, completed, reward_boxes: reward };
  });
}

module.exports = { weekKey, getState, createGuild, donate, upgrade, feedBoss };
