import test from 'node:test';
import assert from 'node:assert/strict';
import {validate,schedule,itinerary,discordDescription} from '../src/model.js';
import {announcementText} from '../src/announcement.js';
import {cardHtml} from '../src/card.js';
import {RelayCore} from '../src/core.js';
const make=()=>({id:crypto.randomUUID(),title:'Festival',description:'Full description',location:'Garden',guildId:'123',startLocal:'2035-06-12 18:00',timeZoneId:'UTC',durationMinutes:120,scheduleMode:'Sessions',sessions:[{StartLocal:'2035-06-14 19:00',EndLocal:'2035-06-14 21:00'},{StartLocal:'2035-06-12 18:00',EndLocal:'2035-06-12 20:00'}]});
test('category and tags survive relay validation without granting private or official access',()=>{
  const e=make();e.EventType='DM';e.Tags='RP, Investigation';
  const r=validate(e,e.id).item;assert.equal(r.eventType,'DM');assert.equal(r.tags,e.Tags);assert.equal(r.informationOnly,undefined);
  e.EventType='PE';assert.equal(validate(e,e.id).item.eventType,'CE');
  e.personalOnly=true;assert.equal(validate(e,e.id).item.eventType,'PE');
  e.Tags='x'.repeat(251);assert.throws(()=>validate(e,e.id),/tags/);
});
test('sessions roundtrip PascalCase client fields, sorted dates, and full itinerary',()=>{
  const e=make(),t=validate(e,e.id),r=t.item;
  assert.equal(t.start,'2035-06-12T18:00:00.000Z');assert.equal(t.end,'2035-06-14T21:00:00.000Z');
  assert.equal(r.sessions[0].startLocal,'2035-06-12 18:00');assert.deepEqual(schedule(r),schedule(r));
  assert.match(announcementText(r,'Organizer',t),/2035-06-14 19:00/);
  assert.match(cardHtml(r,'Organizer','',t,'',''),/2035-06-14 19:00/);
  const google=new RelayCore({},{}).google.eventPayload({record:r,eventId:'456'});
  assert.equal(google.transparency,'transparent');assert.equal(google.end.dateTime,t.end);
  assert.match(google.description,/2035-06-14 19:00/);
});
test('continuous range and DST offset changes retain actual end instant',()=>{
  const e={...make(),scheduleMode:'Continuous',startLocal:'2035-03-10 18:00',endLocal:'2035-03-12 18:00',timeZoneId:'America/Denver'};
  const t=validate(e,e.id);assert.equal((Date.parse(t.end)-Date.parse(t.start))/3600000,47);
  e.endLocal='2035-03-11 02:30';assert.throws(()=>validate(e,e.id),/daylight/);
});
test('reject overlaps, reversed ranges, excess sessions, huge span, official publication',()=>{
  const e=make();e.sessions[1].EndLocal='2035-06-14 20:00';assert.throws(()=>validate(e,e.id),/overlap/);
  e.sessions=Array(13).fill(e.sessions[0]);assert.throws(()=>validate(e,e.id),/1–12/);
  e.scheduleMode='Continuous';e.endLocal='2035-06-12 17:00';assert.throws(()=>validate(e,e.id),/end/);
  e.endLocal='2037-06-12 17:00';assert.throws(()=>validate(e,e.id),/366/);
  e.informationOnly=true;assert.throws(()=>validate(e,e.id),/information only/);
});
test('maximum itinerary preserves native event limit and full announcement description',()=>{
  const e=make();e.description='x'.repeat(1000);e.sessions=Array.from({length:12},(_,i)=>({startLocal:`2035-06-${String(i+12).padStart(2,'0')} 18:00`,endLocal:`2035-06-${String(i+12).padStart(2,'0')} 20:00`}));
  const t=validate(e,e.id);assert.ok(discordDescription(t.item).length<=1000);
  assert.match(discordDescription(t.item),/2035-06-23/);assert.ok(announcementText(t.item,'Organizer',t).includes(e.description));
  assert.ok(announcementText(t.item,'Organizer',t).length<4000);
});
