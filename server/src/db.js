const fs = require('node:fs');
const path = require('node:path');
const sqlite3 = require('sqlite3');
const config = require('./config');
const { rarities } = require('./rarityConfig');
const { boxRarities } = require('./boxConfig');

let database;
let transactionQueue = Promise.resolve();

function run(sql, params = []) {
  return new Promise((resolve, reject) => {
    database.run(sql, params, function onRun(error) {
      if (error) reject(error);
      else resolve({ id: this.lastID, changes: this.changes });
    });
  });
}

function get(sql, params = []) {
  return new Promise((resolve, reject) => {
    database.get(sql, params, (error, row) => (error ? reject(error) : resolve(row)));
  });
}

function all(sql, params = []) {
  return new Promise((resolve, reject) => {
    database.all(sql, params, (error, rows) => (error ? reject(error) : resolve(rows)));
  });
}

async function executeTransaction(work) {
  await run('BEGIN IMMEDIATE');
  try {
    const result = await work({ run, get, all });
    await run('COMMIT');
    return result;
  } catch (error) {
    await run('ROLLBACK');
    throw error;
  }
}

function transaction(work) {
  // sqlite3 使用单连接；串行化事务可防止并发请求在同一连接中相互穿插。
  const pending = transactionQueue.then(() => executeTransaction(work));
  transactionQueue = pending.catch(() => undefined);
  return pending;
}

async function initializeDatabase() {
  fs.mkdirSync(path.dirname(config.databasePath), { recursive: true });
  database = new sqlite3.Database(config.databasePath);
  await run('PRAGMA foreign_keys = ON');
  await run('PRAGMA journal_mode = WAL');

  await run(`CREATE TABLE IF NOT EXISTS schema_migrations (
    version INTEGER PRIMARY KEY,
    applied_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
  )`);

  await run(`CREATE TABLE IF NOT EXISTS users (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    steam_id TEXT NOT NULL UNIQUE,
    nickname TEXT NOT NULL,
    total_cp INTEGER NOT NULL DEFAULT 0 CHECK(total_cp >= 0),
    box_count INTEGER NOT NULL DEFAULT 1 CHECK(box_count >= 0),
    box_capacity INTEGER NOT NULL DEFAULT 8 CHECK(box_capacity > 0),
    last_box_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    residue INTEGER NOT NULL DEFAULT 0 CHECK(residue >= 0),
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_login_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
  )`);

  await migrateUsersTable();

  await run(`CREATE TABLE IF NOT EXISTS slime_dictionary (
    slime_id INTEGER PRIMARY KEY,
    name TEXT NOT NULL,
    rarity TEXT NOT NULL CHECK(rarity IN ('N', 'R', 'SR', 'SSR', 'UR')),
    base_cp INTEGER NOT NULL CHECK(base_cp >= 0),
    drop_weight INTEGER NOT NULL DEFAULT 0 CHECK(drop_weight >= 0)
  )`);
  await addColumnIfMissing('slime_dictionary', 'rarity_key', "TEXT NOT NULL DEFAULT 'COMMON'");
  await addColumnIfMissing('slime_dictionary', 'rarity_name', "TEXT NOT NULL DEFAULT '普通'");
  await addColumnIfMissing('slime_dictionary', 'rarity_color', "TEXT NOT NULL DEFAULT '#FFFFFF'");

  await run(`CREATE TABLE IF NOT EXISTS user_inventory (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    slime_id INTEGER NOT NULL REFERENCES slime_dictionary(slime_id),
    quantity INTEGER NOT NULL DEFAULT 0 CHECK(quantity >= 0),
    UNIQUE(user_id, slime_id)
  )`);

  await run(`CREATE TABLE IF NOT EXISTS user_slimes (
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    slime_id INTEGER NOT NULL REFERENCES slime_dictionary(slime_id),
    quantity INTEGER NOT NULL DEFAULT 0 CHECK(quantity >= 0),
    level INTEGER NOT NULL DEFAULT 1 CHECK(level >= 1),
    experience INTEGER NOT NULL DEFAULT 0 CHECK(experience >= 0),
    first_obtained_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY(user_id, slime_id)
  )`);

  await run(`CREATE TABLE IF NOT EXISTS user_dictionary (
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    slime_id INTEGER NOT NULL REFERENCES slime_dictionary(slime_id),
    first_obtained_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY(user_id, slime_id)
  )`);

  await run(`CREATE TABLE IF NOT EXISTS guilds (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    owner_user_id INTEGER NOT NULL UNIQUE REFERENCES users(id) ON DELETE CASCADE,
    funds INTEGER NOT NULL DEFAULT 0 CHECK(funds >= 0),
    magic_level INTEGER NOT NULL DEFAULT 0 CHECK(magic_level >= 0),
    capacity_level INTEGER NOT NULL DEFAULT 0 CHECK(capacity_level >= 0),
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
  )`);

  await run(`CREATE TABLE IF NOT EXISTS guild_members (
    guild_id INTEGER NOT NULL REFERENCES guilds(id) ON DELETE CASCADE,
    user_id INTEGER NOT NULL UNIQUE REFERENCES users(id) ON DELETE CASCADE,
    contribution INTEGER NOT NULL DEFAULT 0 CHECK(contribution >= 0),
    joined_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY(guild_id, user_id)
  )`);

  await run(`CREATE TABLE IF NOT EXISTS guild_bosses (
    guild_id INTEGER NOT NULL REFERENCES guilds(id) ON DELETE CASCADE,
    week_key TEXT NOT NULL,
    max_hunger INTEGER NOT NULL,
    current_hunger INTEGER NOT NULL CHECK(current_hunger >= 0),
    completed INTEGER NOT NULL DEFAULT 0,
    last_fed_date TEXT,
    reward_claimed INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY(guild_id, week_key)
  )`);

  await run(`CREATE TABLE IF NOT EXISTS user_box_inventory (
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    box_rarity_key TEXT NOT NULL,
    quantity INTEGER NOT NULL DEFAULT 0 CHECK(quantity >= 0),
    PRIMARY KEY(user_id, box_rarity_key)
  )`);
  for (const box of boxRarities) {
    await run('INSERT OR IGNORE INTO user_box_inventory (user_id, box_rarity_key, quantity) SELECT id, ?, 0 FROM users', [box.key]);
  }

  const seedSlimes = [
    [1, '泥巴史莱姆', 'N', 10, 4000],
    [2, '草史莱姆', 'N', 12, 2500],
    [101, '水史莱姆', 'R', 25, 1500],
    [102, '火史莱姆', 'SR', 80, 700],
    [103, '黄金史莱姆', 'SSR', 300, 290],
    [104, '量子史莱姆', 'UR', 1000, 5],
    [105, '克苏鲁史莱姆', 'UR', 1200, 5],
  ];

  for (const slime of seedSlimes) {
    await run(
      `INSERT INTO slime_dictionary (slime_id, name, rarity, base_cp, drop_weight)
       VALUES (?, ?, ?, ?, ?)
       ON CONFLICT(slime_id) DO UPDATE SET
         name = excluded.name,
         rarity = excluded.rarity,
         base_cp = excluded.base_cp,
         drop_weight = excluded.drop_weight`,
      slime,
    );
  }

  const raritySlimes = [
    [201, '干瘪史莱姆', 5, 'POOR'], [202, '基础史莱姆', 10, 'COMMON'],
    [203, '嫩芽史莱姆', 20, 'UNCOMMON'], [204, '潮汐史莱姆', 40, 'RARE'],
    [205, '紫晶史莱姆', 90, 'EPIC'], [206, '烈阳史莱姆', 180, 'MYTHIC'],
    [207, '赤龙史莱姆', 400, 'LEGENDARY'], [208, '星辉神器史莱姆', 900, 'ARTIFACT'],
    [209, '深渊魔王史莱姆', 1800, 'DEMON_KING'], [210, '炎狱战神史莱姆', 3500, 'WAR_GOD'],
    [211, '万象至尊史莱姆', 7000, 'SUPREME'], [212, '混沌魔神史莱姆', 15000, 'DEMON_GOD'],
  ];
  for (let index = 0; index < raritySlimes.length; index += 1) {
    const [slimeId, name, baseCp, rarityKey] = raritySlimes[index];
    const rarity = rarities.find((item) => item.key === rarityKey);
    const legacyRarity = index < 2 ? 'N' : index < 4 ? 'R' : index < 6 ? 'SR' : index < 8 ? 'SSR' : 'UR';
    await run(
      `INSERT INTO slime_dictionary (slime_id, name, rarity, base_cp, drop_weight, rarity_key, rarity_name, rarity_color)
       VALUES (?, ?, ?, ?, ?, ?, ?, ?)
       ON CONFLICT(slime_id) DO UPDATE SET name=excluded.name, base_cp=excluded.base_cp,
       drop_weight=excluded.drop_weight, rarity_key=excluded.rarity_key,
       rarity_name=excluded.rarity_name, rarity_color=excluded.rarity_color`,
      [slimeId, name, legacyRarity, baseCp, rarity.weight, rarity.key, rarity.name, rarity.color],
    );
  }
  await run('UPDATE slime_dictionary SET drop_weight = 0 WHERE slime_id < 201');
  await run('INSERT OR IGNORE INTO schema_migrations (version) VALUES (3)');
  await run('INSERT OR IGNORE INTO schema_migrations (version) VALUES (4)');

  await run(`INSERT OR IGNORE INTO user_slimes (user_id, slime_id, quantity, first_obtained_at)
    SELECT ui.user_id, ui.slime_id, ui.quantity, CURRENT_TIMESTAMP FROM user_inventory ui`);
  await run(`INSERT OR IGNORE INTO user_dictionary (user_id, slime_id, first_obtained_at)
    SELECT user_id, slime_id, first_obtained_at FROM user_slimes WHERE quantity > 0`);
  await addColumnIfMissing('users', 'guild_coins', 'INTEGER NOT NULL DEFAULT 0 CHECK(guild_coins >= 0)');
  await run('INSERT OR IGNORE INTO schema_migrations (version) VALUES (2)');
}

async function columnExists(tableName, columnName) {
  const columns = await all(`PRAGMA table_info(${tableName})`);
  return columns.some((column) => column.name === columnName);
}

async function addColumnIfMissing(tableName, columnName, definition) {
  if (!(await columnExists(tableName, columnName))) {
    await run(`ALTER TABLE ${tableName} ADD COLUMN ${columnName} ${definition}`);
  }
}

async function migrateUsersTable() {
  await addColumnIfMissing('users', 'box_count', 'INTEGER NOT NULL DEFAULT 1 CHECK(box_count >= 0)');
  await addColumnIfMissing('users', 'box_capacity', 'INTEGER NOT NULL DEFAULT 8 CHECK(box_capacity > 0)');
  await addColumnIfMissing('users', 'last_box_at', "TEXT NOT NULL DEFAULT '1970-01-01 00:00:00'");
  await addColumnIfMissing('users', 'residue', 'INTEGER NOT NULL DEFAULT 0 CHECK(residue >= 0)');
  await run("UPDATE users SET last_box_at = CURRENT_TIMESTAMP WHERE last_box_at = '1970-01-01 00:00:00'");
  await run('INSERT OR IGNORE INTO schema_migrations (version) VALUES (1)');
}

function closeDatabase() {
  return new Promise((resolve, reject) => {
    if (!database) return resolve();
    database.close((error) => (error ? reject(error) : resolve()));
  });
}

module.exports = { initializeDatabase, closeDatabase, run, get, all, transaction };
