const jwt = require('jsonwebtoken');
const config = require('./config');
const db = require('./db');

function signToken(user) {
  return jwt.sign({ sub: String(user.id), steam_id: user.steam_id }, config.jwtSecret, {
    expiresIn: config.jwtExpiresIn,
  });
}

async function requireAuth(req, res, next) {
  try {
    const header = req.get('authorization') || '';
    const match = /^Bearer\s+(.+)$/i.exec(header);
    if (!match) return res.status(401).json({ error: '缺少登录凭证' });

    const payload = jwt.verify(match[1], config.jwtSecret);
    const user = await db.get(
      'SELECT id, steam_id, nickname, total_cp FROM users WHERE id = ?',
      [Number(payload.sub)],
    );
    if (!user) return res.status(401).json({ error: '登录凭证对应的用户不存在' });

    req.user = user;
    next();
  } catch (error) {
    if (error.name === 'JsonWebTokenError' || error.name === 'TokenExpiredError') {
      return res.status(401).json({ error: '登录凭证无效或已过期' });
    }
    next(error);
  }
}

module.exports = { signToken, requireAuth };
