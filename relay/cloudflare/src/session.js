import {RelayError} from './model.js';

// A short renewable lease bounds abandoned sessions without limiting play time.
export async function session(core, key) {
  const login = await core.live(key);
  if (login.playSession && (login.accessExpires ?? login.expires) <= Date.now() + 120000) {
    if (!login.refreshToken) {
      await core.store.delete(key);
      throw new RelayError('Discord authorization expired. Link Discord again.', 401);
    }
    const response = await core.fetcher('https://discord.com/api/v10/oauth2/token', {
      method: 'POST', redirect: 'manual', signal: AbortSignal.timeout(20000),
      body: new URLSearchParams({client_id:core.env.DISCORD_CLIENT_ID, client_secret:core.env.DISCORD_CLIENT_SECRET,
        grant_type:'refresh_token', refresh_token:login.refreshToken})
    });
    if (response.status === 400 || response.status === 401) {
      await core.store.delete(key);
      throw new RelayError('Discord authorization was revoked or expired. Link Discord again.', 401);
    }
    if (!response.ok) throw new RelayError('Discord renewal is temporarily unavailable. It will retry.', 503);
    const data = await response.json();
    if (!data.access_token || typeof data.access_token !== 'string' || !Number.isFinite(data.expires_in) || data.expires_in <= 0)
      throw new RelayError('Discord returned an invalid renewal response.', 503);
    login.accessToken = data.access_token;
    login.accessExpires = Date.now() + data.expires_in * 1000;
    if (typeof data.refresh_token === 'string' && data.refresh_token) login.refreshToken = data.refresh_token;
    await core.store.put(key, login);
  }
  return login;
}

export async function heartbeat(core, key, login, enabled) {
  if (typeof enabled !== 'boolean') throw new RelayError('Choose whether to stay connected while playing.');
  login.accessExpires ??= login.expires;
  login.playSession = enabled;
  login.expires = enabled ? Date.now() + 15 * 60000 : Math.min(Date.now() + 3600000, login.accessExpires);
  await core.store.put(key, login);
  if (!await core.store.getAlarm()) await core.store.setAlarm(Date.now() + 60000);
  return {ok:true};
}
