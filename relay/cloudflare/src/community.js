import {RelayError,permissions} from './model.js';

// Only information visible to the authenticated Discord member may leave a server.
export async function requireViewer(core,userId,saved,cache=new Map()) {
  if(!saved?.eventId||!saved.record)throw new RelayError('Event not found.',404);
  const guild=saved.guildId;
  async function get(path){
    if(!cache.has(path))cache.set(path,core.discord('GET',path));
    return cache.get(path);
  }
  const member=await get(`guilds/${guild}/members/${userId}`);
  const info=await get(`guilds/${guild}`);
  if(saved.channelId && info.owner_id!==userId){
    const channels=await get(`guilds/${guild}/channels`),channel=channels.find(c=>c.id===saved.channelId);
    if(!channel)throw new RelayError('This event is no longer visible to you.',403);
    const roles=await get(`guilds/${guild}/roles`),p=permissions(guild,userId,member,roles,channel);
    if(!(p&8n)&&!(p&(1n<<10n)))throw new RelayError('This event is not visible to you in Discord.',403);
  }
}

export async function communityEvents(core,login){
  const guilds=new Set((await core.allGuilds(login.accessToken,false)).map(g=>g.id)),cache=new Map(),result=[];
  for(const [,saved] of await core.store.list({prefix:'event:'})){
    if(!saved.eventId||!saved.record||!guilds.has(saved.guildId))continue;
    try { await requireViewer(core,login.userId,saved,cache); }
    catch(e){if(e instanceof RelayError && [403,404].includes(e.status))continue;throw e;}
    result.push({...saved.record,googleCalendarSync:false,readOnly:saved.userId!==login.userId,
      discordEventId:saved.eventId,discordMessageId:saved.messageId,relayOrigin:core.origin+'/',status:'Published'});
  }
  return result;
}
