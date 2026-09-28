import test from 'node:test';
import assert from 'node:assert/strict';
import {RelayCore} from '../src/core.js';
import {validate,schedule,imageData,permissions,canPost} from '../src/model.js';
import {cardHtml,renderer,scheduleText} from '../src/card.js';

class Storage {
  data=new Map(); alarm=null;
  async get(k){return structuredClone(this.data.get(k));}
  async put(k,v){if(typeof k === 'object'){for(const [key,value] of Object.entries(k))this.data.set(key,structuredClone(value));}else this.data.set(k,structuredClone(v));}
  async delete(k){for(const key of Array.isArray(k)?k:[k])this.data.delete(key);}
  async list({prefix='',limit=Infinity}={}){return new Map([...this.data].filter(([k])=>k.startsWith(prefix)).slice(0,limit).map(([k,v])=>[k,structuredClone(v)]));}
  async transaction(fn){const old=structuredClone(this.data);try{return await fn(this);}catch(e){this.data=old;throw e;}}
  async getAlarm(){return this.alarm;}
  async setAlarm(v){this.alarm=v;}
}
const env={PUBLIC_ORIGIN:'https://relay.example',DISCORD_CLIENT_ID:'1234',DISCORD_CLIENT_SECRET:'test-secret',DISCORD_BOT_TOKEN:'test-bot'};
const login={userId:'42',name:'Tester',accessToken:'test-user',expires:Date.now()+3600000};
const png=Uint8Array.from(Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jT1kAAAAASUVORK5CYII=','base64'));
const banner='data:image/png;base64,'+Buffer.from(png).toString('base64');
const event=()=>({id:crypto.randomUUID(),title:'Moonlit gathering',description:'Meet at the garden.',location:'Lavender Beds',world:'Balmung',startLocal:'2035-06-12 18:00',timeZoneId:'Mountain Standard Time',durationMinutes:120,guildId:'123',channelId:'456',organizer:'Kaerl',server:'Community',signupGroups:'Tank:2\nHealer:2\nDamage:4',recurrence:'None'});
function setup(){
  const store=new Storage(),state={writes:[],deny:false,denyChannel:false,failEvent:false,failMessage:false,rejectMessage:false,failRender:false,renders:[]};
  const fetcher=async(url,init={})=>{
    assert.equal(init.redirect,'manual','Discord requests must use a Workers-supported no-follow mode');
    const p=new URL(url).pathname,method=init.method??'GET';let data;
    if(p.endsWith('/oauth2/token'))data={access_token:'test-user',expires_in:3600};
    else if(p.endsWith('/users/@me/guilds'))data=[{id:'123',name:'Community',owner:false,permissions:state.deny?'0':'17592186060800'}];
    else if(p.endsWith('/users/@me'))data={id:init.headers?.Authorization?.startsWith('Bot')?'99':'42',username:'Tester'};
    else if(p.endsWith('/guilds/123/roles'))data=[{id:'123',permissions:'17592186096640'}];
    else if(p.includes('/members/'))data={roles:[]};
    else if(p.endsWith('/guilds/123/channels'))data=[{id:'456',name:'events',type:0,permission_overwrites:state.denyChannel?[{id:'42',type:1,deny:'1024',allow:'0'}]:[]}];
    else if(p.endsWith('/guilds/123'))data={owner_id:'7'};
    else if(p.includes('/scheduled-events')){
      state.writes.push({p,method,body:JSON.parse(init.body)}); if(state.failEvent)throw new Error('lost response');data={id:'777'};
    } else if(p.includes('/messages')){
      state.writes.push({p,method,body:JSON.parse(init.body.get('payload_json')),image:new Uint8Array(await init.body.get('files[0]').arrayBuffer())});
      if(state.failMessage)throw new Error('lost response');if(state.rejectMessage)return new Response('{}',{status:403});data={id:'888'};
    } else throw new Error('Unexpected test route '+p);
    return Response.json(data);
  };
  const render=async(item,name,image,schedule)=>{state.renders.push({item,image,schedule});if(state.failRender)throw new Error('quota');return png;};
  return {store,state,render,fetcher,core:new RelayCore(store,env,render,fetcher)};
}
test('valid Windows timezone converts to UTC',()=>{const e=event();assert.equal(validate(e,e.id).start,'2035-06-13T00:00:00.000Z');});
test('skipped and ambiguous DST wall clocks are rejected',()=>{
  const e=event();e.startLocal='2026-03-08 02:30';assert.throws(()=>schedule(e,0),/daylight/);
  e.startLocal='2026-11-01 01:30';assert.throws(()=>schedule(e,0),/daylight/);
});
test('invalid dates, past dates, recurrence and oversized fields rejected',()=>{
  const e=event();e.startLocal='2035-02-30 12:00';assert.throws(()=>validate(e,e.id));e.startLocal='2000-01-01 12:00';assert.throws(()=>validate(e,e.id));
  const r=event();r.recurrence='Weekly';assert.throws(()=>validate(r,r.id));r.recurrence='None';r.signupGroups='x'.repeat(601);assert.throws(()=>validate(r,r.id));
});
test('PascalCase legacy payload accepts without local paths',()=>{
  const e=event(),raw=Object.fromEntries(Object.entries(e).map(([k,v])=>[k[0].toUpperCase()+k.slice(1),v]));raw.BannerPath='C:/private/image.png';
  const result=validate(raw,e.id);assert.equal(result.item.title,e.title);assert.ok(!('bannerPath'in result.item));
});
test('image signatures and URL restrictions',()=>{assert.deepEqual(imageData(banner).bytes,png);assert.throws(()=>imageData('https://example.com/banner.png'));assert.throws(()=>imageData(banner.replace('image/png','image/jpeg')));});
test('permission member deny wins over roles',()=>{const p=permissions('123','42',{roles:[]},[{id:'123',permissions:'50176'}],{permission_overwrites:[{id:'42',type:1,deny:'1024',allow:'0'}]});assert.equal(canPost(p),false);});
test('publishes one card and edits existing objects',async()=>{
  const {core,state}=setup(),e=event();const result=await core.publish(login,e.id,{event:e,bannerDataUrl:banner});assert.equal(result.eventId,'777');assert.equal(result.messageId,'888');
  assert.deepEqual(state.writes[1].image,png);assert.equal(state.writes[1].body.flags,32768);assert.deepEqual(state.writes[1].body.allowed_mentions.parse,[]);
  assert.equal(state.writes[1].body.attachments[0].filename,'event-card.png');
  assert.deepEqual(state.writes[1].body.components.map(c=>c.type),[12,10,1]);
  assert.ok(state.writes[1].body.components[1].content.includes(e.description));
  assert.equal((await core.store.get('art:'+e.id)).published,true);
  assert.equal(await core.store.get('art:'+e.id+':0'),Buffer.from(png).toString('base64'));
  assert.equal(state.writes[0].body.image,banner);await core.publish(login,e.id,{event:e});assert.deepEqual(state.writes.map(x=>x.method),['POST','POST','PATCH','PATCH']);
  assert.deepEqual(state.writes.at(-1).body.embeds,[], 'updates must explicitly clear the old image embed');
  assert.equal(state.writes.at(-1).body.content,null);
  assert.ok(state.writes.at(-1).body.components[1].content.includes(e.description));
  assert.equal(state.writes.at(-1).body.components[2].components[0].label,'View event / Interested');
});
test('source image survives restart and can be removed',async()=>{
  const {core,store,state,render,fetcher}=setup(),e=event();await core.publish(login,e.id,{event:e,bannerDataUrl:banner});const restarted=new RelayCore(store,env,render,fetcher);
  await restarted.publish(login,e.id,{event:e});assert.equal(state.renders.at(-1).image,banner);assert.ok(!('image'in state.writes.at(-2).body));
  await restarted.publish(login,e.id,{event:e,bannerDataUrl:''});assert.equal(state.renders.at(-1).image,'');assert.equal(state.writes.at(-2).body.image,null);
});
test('uncertain create cannot duplicate after restart',async()=>{
  const {core,store,state,render,fetcher}=setup(),e=event();state.failEvent=true;await assert.rejects(core.publish(login,e.id,{event:e}));state.failEvent=false;
  await assert.rejects(new RelayCore(store,env,render,fetcher).publish(login,e.id,{event:e}),/uncertain/);assert.equal(state.writes.length,1);
});
test('uncertain announcement is not duplicated',async()=>{
  const {core,state}=setup(),e=event();state.failMessage=true;assert.match((await core.publish(login,e.id,{event:e})).warning,/confirmed/);state.failMessage=false;
  assert.match((await core.publish(login,e.id,{event:e})).warning,/uncertain/);assert.equal(state.writes.filter(x=>x.p.includes('messages')).length,1);
});
test('definitively rejected announcement can retry',async()=>{
  const {core,state}=setup(),e=event();state.rejectMessage=true;assert.match((await core.publish(login,e.id,{event:e})).warning,/rejected/);state.rejectMessage=false;
  assert.equal((await core.publish(login,e.id,{event:e})).messageId,'888');
});
test('render failure makes no Discord mutations',async()=>{const {core,state}=setup(),e=event();state.failRender=true;await assert.rejects(core.publish(login,e.id,{event:e}));assert.equal(state.writes.length,0);});
test('ownership and destination cannot change',async()=>{
  const {core}=setup(),e=event();await core.publish(login,e.id,{event:e});await assert.rejects(core.publish({...login,userId:'43'},e.id,{event:e}),/another account/);
  e.channelId='';await assert.rejects(core.publish(login,e.id,{event:e}),/destination/);
});
test('revoked guild or channel access blocks writes',async()=>{
  const {core,state}=setup(),e=event();state.deny=true;await assert.rejects(core.publish(login,e.id,{event:e}));state.deny=false;state.denyChannel=true;await assert.rejects(core.publish(login,e.id,{event:e}));assert.equal(state.writes.length,0);
});
test('OAuth pairing and state are single use and survive restart',async()=>{
  const {core,store,render,fetcher}=setup();
  const call=(path,method='GET',body)=>core.handle(new Request(env.PUBLIC_ORIGIN+path,{method,body:body?JSON.stringify(body):undefined}));
  const link=await(await call('/auth/link','POST')).json();const authPath=new URL(link.verificationUrl).pathname;
  assert.match(await(await call(authPath)).text(),new RegExp(link.code));
  const redirect=await call(authPath,'POST'),state=new URL(redirect.headers.get('Location')).searchParams.get('state');
  const restarted=new RelayCore(store,env,render,fetcher);
  assert.equal((await restarted.handle(new Request(env.PUBLIC_ORIGIN+`/auth/callback?state=${state}&code=test`))).status,200);
  assert.equal((await call(`/auth/callback?state=${state}&code=test`)).status,401);
  const connected=await(await call('/auth/poll','POST',{pollToken:link.pollToken})).json();assert.equal(connected.status,'connected');assert.equal(connected.token.length,64);
  assert.equal((await call('/auth/poll','POST',{pollToken:link.pollToken})).status,401);
});
test('authorization form policy permits the Discord redirect and preserves isolation',async()=>{
  const {core}=setup();
  const link=await(await core.handle(new Request(env.PUBLIC_ORIGIN+'/auth/link',{method:'POST'}))).json();
  const page=await core.handle(new Request(link.verificationUrl));
  const policy=Object.fromEntries(page.headers.get('Content-Security-Policy').split(';').map(x=>x.trim().split(/\s+/)).filter(x=>x[0]).map(([name,...values])=>[name,values]));
  const redirect=await core.handle(new Request(link.verificationUrl,{method:'POST'}));
  const destination=new URL(redirect.headers.get('Location'));
  assert.equal(redirect.status,303);
  assert.equal(destination.origin,'https://discord.com');
  assert.equal(destination.pathname,'/oauth2/authorize');
  assert.deepEqual(policy['form-action'],["'self'",destination.origin]);
  assert.deepEqual(policy['default-src'],["'none'"]);
  assert.deepEqual(policy['frame-ancestors'],["'none'"]);
  assert.equal(destination.searchParams.get('redirect_uri'),env.PUBLIC_ORIGIN+'/auth/callback');
  assert.equal(destination.searchParams.get('scope'),'identify guilds');
});
test('history is scoped to owner and permission; logout expires session',async()=>{
  const {core,store,state}=setup(),e=event();await core.publish(login,e.id,{event:e});await store.put('session:test',login);
  const request=(path,method='GET')=>new Request(env.PUBLIC_ORIGIN+path,{method,headers:{Authorization:'Bearer test'}});
  assert.equal((await(await core.handle(request('/events'))).json()).length,1);state.deny=true;
  assert.equal((await(await core.handle(request('/events'))).json()).length,0);await core.handle(request('/auth/logout','POST'));assert.equal((await core.handle(request('/events'))).status,401);
});
test('health stays unconfigured without secrets',async()=>{const core=new RelayCore(new Storage(),{},()=>{});assert.equal((await(await core.handle(new Request('https://relay.example/health'))).json()).configured,false);assert.equal((await core.handle(new Request('https://relay.example/auth/link',{method:'POST'}))).status,503);});
test('native fetch retains the global receiver in the production constructor',async()=>{
  const original=globalThis.fetch;
  try {
    globalThis.fetch=async function(){assert.equal(this,globalThis,'native Workers fetch requires its global receiver');return Response.json({id:'42',username:'Tester'});};
    const core=new RelayCore(new Storage(),env,()=>{});
    assert.equal((await core.discord('GET','users/@me',undefined,'test-user',false)).id,'42');
  } finally {globalThis.fetch=original;}
});
test('callback failures identify their stage without leaking response or credentials',async()=>{
  const store=new Storage();
  await store.put('state:test',{pollToken:'poll',expires:Date.now()+60000});
  await store.put('link:poll',{key:'key',expires:Date.now()+60000});
  const core=new RelayCore(store,env,()=>{},async()=>{throw new Error('SECRET OAuth code or provider response');});
  const response=await core.handle(new Request(env.PUBLIC_ORIGIN+'/auth/callback?state=test&code=private-code'));
  const body=await response.text();assert.equal(response.status,503);assert.match(body,/Discord token exchange/);assert.ok(!body.includes('SECRET'));assert.ok(!body.includes('private-code'));
  assert.equal(await store.get('state:test'),undefined);
});
test('token exchange refuses redirects without forwarding credentials',async()=>{
  const store=new Storage();let calls=0;
  await store.put('state:test',{pollToken:'poll',expires:Date.now()+60000});
  await store.put('link:poll',{key:'key',expires:Date.now()+60000});
  const core=new RelayCore(store,env,()=>{},async(url,init)=>{
    calls++;assert.equal(url,'https://discord.com/api/v10/oauth2/token');assert.equal(init.redirect,'manual');
    return new Response(null,{status:302,headers:{Location:'https://untrusted.example/private-data'}});
  });
  const response=await core.handle(new Request(env.PUBLIC_ORIGIN+'/auth/callback?state=test&code=private-code'));
  const body=await response.text();assert.equal(response.status,502);assert.match(body,/unexpected redirect/);assert.equal(calls,1);assert.ok(!body.includes('private-data'));assert.ok(!body.includes('test-secret'));
});
test('Discord API redirects remain uncertain without sending a second request',async()=>{
  let calls=0;const core=new RelayCore(new Storage(),env,()=>{},async(url,init)=>{
    calls++;assert.equal(init.redirect,'manual');return new Response(null,{status:307,headers:{Location:'https://untrusted.example'}});
  });
  await assert.rejects(core.discord('POST','guilds/123/scheduled-events',{name:'Test'}),/Unexpected Discord redirect/);assert.equal(calls,1);
});
test('template encodes text and blocks active content',()=>{
  const e=event();e.title='<script>alert(1)</script>';const html=cardHtml(e,'Tester',banner,validate(e,e.id),'','');assert.ok(html.includes('&lt;script&gt;'));assert.ok(!html.includes('<script>'));assert.ok(html.includes("default-src 'none'"));
});
test('Browser Run caches consecutive identical cards and validates PNG output',async()=>{
  let calls=0;const render=renderer({quickAction:async()=>{calls++;return new Response(png);}},new Uint8Array(),new Uint8Array()),e=event(),s=validate(e,e.id);
  await render(e,'Tester',banner,s);await render(e,'Tester',banner,s);assert.equal(calls,1);
  const bad=renderer({quickAction:async()=>new Response('not PNG')},new Uint8Array(),new Uint8Array());await assert.rejects(bad(e,'Tester','',s),/PNG/);
});


test('schedule uses daylight abbreviation and omits repeated date for same-day end',()=>{
  const e=event(),s=validate(e,e.id),text=scheduleText(s);
  assert.equal(text.period,'6:00 PM – 8:00 PM · MDT');
  e.startLocal='2035-06-12 23:00';
  assert.match(scheduleText(validate(e,e.id)).period,/Jun 13, 2035/);
});
test('organizer display-name changes do not transfer publication ownership',async()=>{
  const {core,state}=setup(),e=event();await core.publish(login,e.id,{event:e});
  e.organizer='My in-game name';await core.publish(login,e.id,{event:e});
  assert.equal(state.renders.at(-1).item.organizer,'My in-game name');
  const writes=state.writes.length;
  await assert.rejects(core.publish({...login,userId:'43'},e.id,{event:e}));
  assert.equal(state.writes.length,writes);
});


test('typography survives validation, rendering, edits and recovery',async()=>{
  const e=event();e.description='First paragraph.\n\nSecond paragraph.';
  e.announcementStyle={Title:{Font:'Cinzel',Size:52,Bold:true,Italic:true,Underline:true,Strike:true,Color:'#F0C080',Outline:2,OutlineColor:'#102030',Glow:12,GlowColor:'#7788FF',Align:'center',LetterSpacing:2,LineHeight:1.2},Body:{Size:24},Paragraphs:{1:{Font:'Serif',Size:30,Italic:true}}};
  const v=validate(e,e.id),html=cardHtml(v.item,'Tester',banner,v,'','');
  assert.match(html,/font-family:Cinzel,serif;font-size:52px/);
  assert.match(html,/text-decoration:underline line-through/);
  assert.match(html,/-webkit-text-stroke:2px #102030/);
  assert.match(html,/text-shadow:0 0 12px #7788FF/);
  assert.match(html,/font-family:Georgia,serif;font-size:30px/);
  const {core,store}=setup();await core.publish(login,e.id,{event:e});
  const saved=await store.get('event:'+e.id);assert.equal(saved.record.announcementStyle.title.size,52);
  assert.equal(saved.record.announcementStyle.paragraphs[1].italic,true);
});
test('typography rejects CSS injection, unsupported fonts and expensive ranges',()=>{
  for(const style of [{Color:'red;display:none'},{Font:'url(https://example.com)'},{Size:500},{Glow:999},{Bold:'true'},{Outline:-1}]){
    const e=event();e.announcementStyle={Title:style};assert.throws(()=>validate(e,e.id));
  }
  const e=event();e.announcementStyle={Paragraphs:{bad:{}}};assert.throws(()=>validate(e,e.id));
});
test('styled preview requires login, returns image and creates no publications',async()=>{
  const {core,store,state}=setup(),e=event();e.guildId='';e.channelId='';e.location='';
  e.announcementStyle={Title:{Font:'Cinzel',Outline:1}};
  const request=()=>new Request(env.PUBLIC_ORIGIN+'/preview',{method:'POST',headers:{Authorization:'Bearer preview-session','Content-Type':'application/json'},body:JSON.stringify({event:e,bannerDataUrl:banner})});
  assert.equal((await core.handle(request())).status,401);
  await store.put('session:preview-session',login);
  const response=await core.handle(request());assert.equal(response.status,200);
  assert.deepEqual(Buffer.from((await response.json()).imageBase64,'base64'),Buffer.from(png));
  assert.equal(state.writes.length,0);assert.equal((await store.list({prefix:'event:'})).size,0);
});
test('preview cannot read another publisher banner',async()=>{
  const {core,store,state}=setup(),e=event();await core.publish(login,e.id,{event:e,bannerDataUrl:banner});
  await store.put('session:other',{...login,userId:'43'});const count=state.renders.length;
  const response=await core.handle(new Request(env.PUBLIC_ORIGIN+'/preview',{method:'POST',headers:{Authorization:'Bearer other'},body:JSON.stringify({event:e})}));
  assert.equal(response.status,403);assert.equal(state.renders.length,count);
});
test('text style changes invalidate the screenshot cache',async()=>{
  let calls=0;const render=renderer({quickAction:async()=>{calls++;return new Response(png);}},new Uint8Array(),new Uint8Array()),e=event(),s=validate(e,e.id);
  await render(e,'Tester','',s);e.announcementStyle={Title:{Outline:2}};await render(e,'Tester','',s);assert.equal(calls,2);
});


test('Google unavailable never rolls back successful Discord publication',async()=>{
  const {core,state,store}=setup(),e=event();e.googleCalendarSync=true;
  const result=await core.publish(login,e.id,{event:e});
  assert.equal(result.eventId,'777');assert.equal(result.messageId,'888');assert.match(result.warning,/Connect Google/);
  assert.equal(state.writes.length,2);assert.equal((await store.get('event:'+e.id)).record.googleCalendarSync,true);
});
