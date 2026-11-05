const db = require('./db');
const config = require('./gameConfig');

function experienceForLevel(level) {
  return config.levelExperienceBase + (level - 1) * config.levelExperienceGrowth;
}

function effectiveCp(baseCp, level, quantity) {
  return Math.floor(baseCp * (1 + Math.log2(level) * config.levelCpRate)) * quantity;
}

async function recalculateTotalCp(tx, userId) {
  const rows = await tx.all(
    `SELECT us.quantity, us.level, sd.base_cp FROM user_slimes us
     JOIN slime_dictionary sd ON sd.slime_id = us.slime_id
     WHERE us.user_id = ? AND us.quantity > 0`,
    [userId],
  );
  const total = rows.reduce((sum, row) => sum + effectiveCp(row.base_cp, row.level, row.quantity), 0);
  await tx.run('UPDATE users SET total_cp = ? WHERE id = ?', [total, userId]);
  return total;
}

async function grantSlime(tx, userId, slimeId, quantity = 1) {
  const timestamp = new Date().toISOString();
  await tx.run(
    `INSERT INTO user_slimes (user_id, slime_id, quantity, first_obtained_at) VALUES (?, ?, ?, ?)
     ON CONFLICT(user_id, slime_id) DO UPDATE SET quantity = quantity + excluded.quantity`,
    [userId, slimeId, quantity, timestamp],
  );
  await tx.run(
    `INSERT INTO user_inventory (user_id, slime_id, quantity) VALUES (?, ?, ?)
     ON CONFLICT(user_id, slime_id) DO UPDATE SET quantity = quantity + excluded.quantity`,
    [userId, slimeId, quantity],
  );
  await tx.run(
    'INSERT OR IGNORE INTO user_dictionary (user_id, slime_id, first_obtained_at) VALUES (?, ?, ?)',
    [userId, slimeId, timestamp],
  );
  return recalculateTotalCp(tx, userId);
}

async function removeSlime(tx, userId, slimeId, quantity) {
  const row = await tx.get('SELECT quantity FROM user_slimes WHERE user_id = ? AND slime_id = ?', [userId, slimeId]);
  if (!row || row.quantity < quantity) {
    const error = new Error(`史莱姆 ${slimeId} 库存不足`);
    error.status = 409;
    throw error;
  }
  await tx.run('UPDATE user_slimes SET quantity = quantity - ? WHERE user_id = ? AND slime_id = ?', [quantity, userId, slimeId]);
  await tx.run('UPDATE user_inventory SET quantity = MAX(0, quantity - ?) WHERE user_id = ? AND slime_id = ?', [quantity, userId, slimeId]);
  return recalculateTotalCp(tx, userId);
}

async function getInventory(userId) {
  const rows = await db.all(
    `SELECT sd.slime_id, sd.name, sd.rarity_name AS rarity, sd.rarity_key, sd.rarity_color,
      sd.base_cp, us.quantity, us.level, us.experience, us.first_obtained_at
     FROM user_slimes us JOIN slime_dictionary sd ON sd.slime_id = us.slime_id
     WHERE us.user_id = ? AND us.quantity > 0 ORDER BY sd.slime_id`,
    [userId],
  );
  return rows.map((row) => ({
    ...row,
    next_level_experience: experienceForLevel(row.level),
    effective_cp: effectiveCp(row.base_cp, row.level, row.quantity),
  }));
}

async function getDictionary(userId) {
  const entries = await db.all(
    `SELECT sd.slime_id, sd.name, sd.rarity_name AS rarity, sd.rarity_key, sd.rarity_color, sd.base_cp,
      CASE WHEN ud.slime_id IS NULL THEN 0 ELSE 1 END AS discovered, ud.first_obtained_at
     FROM slime_dictionary sd LEFT JOIN user_dictionary ud
       ON ud.slime_id = sd.slime_id AND ud.user_id = ? ORDER BY sd.slime_id`,
    [userId],
  );
  const discovered = entries.filter((entry) => entry.discovered === 1).length;
  return { entries, discovered, total: entries.length, completion: entries.length ? discovered / entries.length : 0 };
}

module.exports = { experienceForLevel, effectiveCp, recalculateTotalCp, grantSlime, removeSlime, getInventory, getDictionary };
