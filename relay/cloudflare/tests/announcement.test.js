import test from 'node:test';
import assert from 'node:assert/strict';
import {announcementText} from '../src/announcement.js';
import {chatCardHtml,renderer} from '../src/card.js';
const schedule={start:'2035-10-03T23:00:00Z',end:'2035-10-04T02:00:00Z',zone:'UTC'};
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
test('compact card is landscape, escapes content and keeps a large type floor',()=>{
  const html=chatCardHtml({title:'<script>bad</script>',description:'A short introduction',announcementStyle:{title:{size:16},body:{size:16}}},'Host','',schedule,'','');
  assert.match(html,/width:1200px;height:680px/);assert.match(html,/font-size:52px/);assert.match(html,/font-size:36px/);
  assert.match(html,/&lt;script&gt;/);assert.ok(!html.includes('<script>bad'));
  assert.match(html,/object-fit:contain/);
});
test('renderer captures a fixed landscape frame even for long styled content',async()=>{
  let options;const png=new Uint8Array(24);png.set([137,80,78,71,13,10,26,10]);
  const render=renderer({quickAction:async(name,args)=>{options=args;return new Response(png);}},new Uint8Array(),new Uint8Array());
  await render({title:'Long title '.repeat(9),description:'Large body '.repeat(90),announcementStyle:{body:{size:64}}},'Host','',schedule);
  assert.equal(options.viewport.height,680);assert.equal(options.screenshotOptions.fullPage,false);
});
