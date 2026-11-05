const path = require('node:path');

require('dotenv').config();

const isProduction = process.env.NODE_ENV === 'production';
const jwtSecret = process.env.JWT_SECRET || (isProduction ? '' : 'development-only-change-me');

if (!jwtSecret) {
  throw new Error('生产环境必须配置 JWT_SECRET');
}

module.exports = {
  port: Number(process.env.PORT || 3000),
  isProduction,
  databasePath: path.resolve(process.cwd(), process.env.DATABASE_PATH || './data/slime.db'),
  jwtSecret,
  jwtExpiresIn: process.env.JWT_EXPIRES_IN || '7d',
  allowDevAuth: !isProduction && process.env.ALLOW_DEV_AUTH === 'true',
  steamAppId: process.env.STEAM_APP_ID || '',
  steamWebApiKey: process.env.STEAM_WEB_API_KEY || '',
};
