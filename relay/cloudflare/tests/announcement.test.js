import test from 'node:test';
import assert from 'node:assert/strict';
import {announcementText,announcementMessage} from '../src/announcement.js';
import {cardHtml,renderer} from '../src/card.js';
const schedule={start:'2035-10-03T23:00:00Z',end:'2035-10-04T02:00:00Z',zone:'UTC'};
test('new and existing announcements keep the complete card before text and button',()=>{
  for(const editing of [false,true]){
    const item={title:'Event',description:'Full details\n\nFinal paragraph',startLocal:'2035-10-03 23:00',location:'Garden'};
    const message=announcementMessage(item,'Host',schedule,'https://discord.com/events/1/2',editing);
    assert.equal(message.flags,32768);
    assert.deepEqual(message.components.map(c=>c.type),[12,10,1]);
    assert.equal(message.components[0].items[0].media.url,'attachment://'+message.attachments[0].filename);
    assert.ok(message.components[1].content.includes(item.description));
    assert.equal(message.components[2].components[0].url,'https://discord.com/events/1/2');
    assert.deepEqual(message.allowed_mentions,{parse:[]});
    if(editing){assert.equal(message.content,null);assert.deepEqual(message.embeds,[]);}
    else {assert.ok(!('content' in message));assert.ok(!('embeds' in message));}
  }
});
test('complete event details remain readable text, including overnight end date',()=>{
  const item={title:'Autumnal Masquerade',description:'First paragraph.\n\nSecond paragraph with https://example.com',world:'Mateus',location:'Lavender Beds',organizer:'Lynnaes'};
  const text=announcementText(item,'Fallback',schedule);
  for(const value of Object.values(item))assert.ok(text.includes(value));
  assert.match(text,/<t:\d+:F> – <t:\d+:F>/);
});
test('maximum valid text stays within Discord message limit without truncating description',()=>{
  const description='D'.repeat(1000);
  const text=announcementText({title:'*'.repeat(100),description,location:'*'.repeat(95),organizer:'*'.repeat(100)},'',schedule);
  assert.ok(text.length<=2000);assert.ok(text.includes(description));assert.match(text,/\\\*/);
});
test('full card preserves all paragraphs and their chosen styles without clipping',()=>{
  const html=cardHtml({title:'<script>bad</script>',description:'First paragraph\n\nComplete final paragraph',location:'Garden',server:'Community',signupGroups:'Guests:unlimited',announcementStyle:{title:{size:48},body:{size:28},paragraphs:{0:{size:32}}}},'Host','',schedule,'','');
  assert.match(html,/font-size:48px/);assert.match(html,/font-size:32px/);assert.match(html,/font-size:28px/);
  for(const value of ['First paragraph','Complete final paragraph','Garden','Community','Guests','Host'])assert.ok(html.includes(value));
  assert.ok(!html.includes('line-clamp'));
  assert.match(html,/&lt;script&gt;/);assert.ok(!html.includes('<script>bad'));
  assert.match(html,/object-fit:contain/);
});
test('renderer captures the complete styled poster including long content',async()=>{
  let options;const png=new Uint8Array(24);png.set([137,80,78,71,13,10,26,10]);
  const render=renderer({quickAction:async(name,args)=>{options=args;return new Response(png);}},new Uint8Array(),new Uint8Array());
  await render({title:'Long title '.repeat(9),description:'Large body '.repeat(90),announcementStyle:{body:{size:64}}},'Host','',schedule);
  assert.equal(options.viewport.width,1200);assert.equal(options.screenshotOptions.fullPage,true);
  assert.ok(options.html.includes('Large body '.repeat(90)));
});
