const config = require('./config');

async function authenticateSteamTicket(steamTicket) {
  if (!steamTicket || typeof steamTicket !== 'string') {
    const error = new Error('steamTicket 不能为空');
    error.status = 400;
    throw error;
  }

  if (config.allowDevAuth && steamTicket.startsWith('dev:')) {
    const steamId = steamTicket.slice(4).trim();
    if (!/^[a-zA-Z0-9_-]{3,64}$/.test(steamId)) {
      const error = new Error('开发票据格式应为 dev:<玩家标识>');
      error.status = 400;
      throw error;
    }
    return { steamId: `dev_${steamId}`, nickname: steamId };
  }

  if (!config.steamAppId || !config.steamWebApiKey) {
    const error = new Error('服务器尚未配置 Steam 正式鉴权');
    error.status = 503;
    throw error;
  }

  const url = new URL('https://partner.steam-api.com/ISteamUserAuth/AuthenticateUserTicket/v1/');
  url.searchParams.set('key', config.steamWebApiKey);
  url.searchParams.set('appid', config.steamAppId);
  url.searchParams.set('ticket', steamTicket);

  const response = await fetch(url, { signal: AbortSignal.timeout(8000) });
  if (!response.ok) {
    const error = new Error('Steam 鉴权服务暂时不可用');
    error.status = 502;
    throw error;
  }

  const body = await response.json();
  const params = body?.response?.params;
  if (body?.response?.error || !params?.steamid || String(params.ownersteamid) !== String(params.steamid)) {
    const error = new Error('Steam Ticket 无效');
    error.status = 401;
    throw error;
  }

  return { steamId: String(params.steamid), nickname: `Steam_${String(params.steamid).slice(-6)}` };
}

module.exports = { authenticateSteamTicket };
