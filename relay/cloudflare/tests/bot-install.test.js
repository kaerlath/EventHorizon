import test from 'node:test';
import assert from 'node:assert/strict';
import {botInstallUrl} from '../src/bot-install.js';
import {RelayCore} from '../src/core.js';

test('bot invitation uses server install and only required event/post permissions',()=>{
  const url=new URL(botInstallUrl('1553917198378999818'));
  assert.equal(url.origin,'https://discord.com');assert.equal(url.pathname,'/oauth2/authorize');
  assert.equal(url.searchParams.get('client_id'),'1553917198378999818');
  assert.equal(url.searchParams.get('scope'),'bot');assert.equal(url.searchParams.get('integration_type'),'0');
  const p=BigInt(url.searchParams.get('permissions'));
  assert.equal(p,(1n<<44n)|(1n<<10n)|(1n<<11n)|(1n<<14n)|(1n<<15n));
  assert.equal(p&8n,0n);assert.equal(url.searchParams.has('guild_id'),false);
  assert.throws(()=>botInstallUrl('1&permissions=8'));
});
test('new users can get the installation link without login or exposing credentials',async()=>{
  const env={PUBLIC_ORIGIN:'https://relay.example',DISCORD_CLIENT_ID:'123',DISCORD_CLIENT_SECRET:'private-secret',DISCORD_BOT_TOKEN:'private-token'};
  const core=new RelayCore({},env,()=>{},()=>{throw Error('Must not contact Discord to get a public invitation');});
  const response=await core.handle(new Request('https://relay.example/discord/install'));
  assert.equal(response.status,200);
  const data=await response.json();assert.deepEqual(Object.keys(data),['url']);
  assert.ok(!data.url.includes('private'));assert.equal(new URL(data.url).searchParams.get('client_id'),'123');
  assert.equal((await core.handle(new Request('https://other.example/discord/install'))).status,400);
});
