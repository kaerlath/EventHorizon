import {itinerary} from './model.js';
import {announcementStyle,styleCss} from './typography.js';
import {fontCss} from './fonts.js';
import { encode as E, imageData, b64, MAX_IMAGE, RelayError } from './model.js';

export function scheduleText(schedule) {
  const format = (date,opts) => new Intl.DateTimeFormat('en-US',{timeZone:schedule.zone,...opts}).format(new Date(date));
  const sameDay = format(schedule.start,{dateStyle:'short'}) === format(schedule.end,{dateStyle:'short'});
  const end = format(schedule.end,sameDay ? {timeStyle:'short'} : {dateStyle:'medium',timeStyle:'short'});
  const zone = new Intl.DateTimeFormat('en-US',{timeZone:schedule.zone,timeZoneName:'short'}).formatToParts(new Date(schedule.start)).find(x=>x.type==='timeZoneName').value;
  return {date:format(schedule.start,{dateStyle:'full'}),period:`${format(schedule.start,{timeStyle:'short'})} – ${end} · ${zone}`};
}

export function cardHtml(item, organizer, banner, schedule, font, icon, extraFonts = '') {
  imageData(banner);
  const {date,period}=scheduleText(schedule);
  const design=announcementStyle(item.announcementStyle);
  const description=(item.description||'Join us for a gathering in Eorzea.').replace(/\r\n/g,'\n').split(/\n\s*\n/)
    .map((text,index)=>`<p style="${styleCss(design.paragraphs[index]??design.body)}">${E(text)}</p>`).join('');
  const groups = (item.signupGroups || '').split(/\r?\n/).filter(x=>x.trim()).map(line=>{
    const colon=line.indexOf(':'),name=(colon<0?line:line.slice(0,colon)).trim(),capacity=(colon<0?'':line.slice(colon+1)).trim();
    const label = !capacity ? 'Open group' : capacity.toLowerCase()==='unlimited' ? 'Unlimited' : `${capacity} places`;
    const role = /tank/i.test(name)?'tank':/heal/i.test(name)?'healer':/damage|dps/i.test(name)?'damage':'guests';
    return `<div class="group ${role}"><span class="group-dot"></span><div><strong>${E(name)}</strong><div class="capacity">${E(label)}</div></div></div>`;
  }).join('');
  return `<!doctype html><html><head><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data:; font-src data:; style-src 'unsafe-inline'; script-src 'nonce-eh-font-ready'; base-uri 'none'">
  <style>
  @font-face{font-family:Cinzel;src:url(data:font/ttf;base64,${font})}
  ${extraFonts}
  *{box-sizing:border-box}body{margin:0;width:1200px;padding:32px;background:radial-gradient(ellipse at 0 0,#1b2e50,transparent 60%),#050a15;color:#edf1ff;font:22px Arial,sans-serif}
  .card{border:1px solid #7185ad;border-radius:20px;overflow:hidden;background:linear-gradient(135deg,#101c33,#070e1b 70%);box-shadow:0 0 35px #4168ae22}
  header{display:flex;align-items:center;gap:24px;padding:22px 28px;border-bottom:1px solid #465676;background:linear-gradient(100deg,#192945,#0b1427 75%)}
  .logo{width:76px;height:76px;border-radius:50%;box-shadow:0 0 24px #759dff55}.brand{font:32px Cinzel,Georgia,serif;letter-spacing:5px;color:#e1eaff;text-shadow:0 0 18px #97bcff66}
  .eyebrow{font-size:12px;letter-spacing:3px;color:#9eb5d8;margin-top:9px;text-transform:uppercase}main{padding:30px}
  h1{font-size:38px;line-height:1.2;margin:0 0 18px;overflow-wrap:anywhere}.date{color:#c0d0ec;line-height:1.6;margin-bottom:24px}.time{color:#9fafd0}
  .hero{display:block;width:100%;height:auto;max-height:520px;object-fit:contain;background:#050a13;border:1px solid #445b81;border-radius:12px}
  .empty{height:300px;display:flex;align-items:center;justify-content:center;background:radial-gradient(ellipse,#303b67,#0b1429);font:30px Cinzel,Georgia,serif}
  .columns{display:grid;grid-template-columns:minmax(0,1fr) 285px;gap:20px;margin-top:24px;align-items:start}
  .panel{background:linear-gradient(120deg,#16243d,#101b2e);border:1px solid #334865;border-radius:12px;padding:22px;overflow-wrap:anywhere}
  .description p{white-space:pre-wrap;margin:0 0 1em}.description p:last-child{margin-bottom:0}.description{line-height:1.55;border-left:3px solid #b5a2f2}.label{font-size:12px;text-transform:uppercase;letter-spacing:2px;color:#9eb3d4;margin-bottom:8px}
  .value{line-height:1.45;font-size:20px;padding-bottom:18px;margin-bottom:18px;border-bottom:1px solid #30425e}.value:last-child{padding:0;margin:0;border:0}
  h2{font-size:22px;font-weight:500;margin:26px 0 14px}.groups{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px}
  .group{display:flex;gap:12px;padding:18px;border:1px solid #344b70;border-radius:10px;background:linear-gradient(140deg,#182b48,#101b2d);overflow-wrap:anywhere}
  .group-dot{flex:0 0 10px;height:10px;border-radius:50%;margin-top:6px;background:#b2a3f4;box-shadow:0 0 10px #9c95ea55}.tank .group-dot{background:#57a9ff}.healer .group-dot{background:#58dca2}.damage .group-dot{background:#f17c90}
  .group strong{font-size:19px;font-weight:500}.capacity{font-size:16px;color:#a5b8d9;margin-top:7px}.muted{font-size:16px;line-height:1.5;color:#95aaca;margin:14px 0 0}
  footer{display:flex;justify-content:space-between;padding:20px 30px;border-top:1px solid #334968;font-size:15px;color:#93aacd;letter-spacing:1px}
  </style></head><body><article class="card">
  <header><img class="logo" src="data:image/png;base64,${icon}" alt=""><div><div class="brand">Event Horizon</div><div class="eyebrow">From Eorzea to Discord</div></div></header>
  <main><h1 style="${styleCss(design.title)}">${E(item.title)}</h1><div class="date">${E(date)}<br><span class="time">${E(period)}</span></div>
  ${item.scheduleMode==='Sessions'?`<div class="panel" style="white-space:pre-wrap;margin-bottom:24px">${E(itinerary(item))}</div>`:''}
  ${banner?`<img class="hero" src="${E(banner)}" alt="Event banner">`:'<div class="hero empty">Your next gathering awaits</div>'}
  <div class="columns"><section><div class="panel description">${description}</div>
  ${groups?`<h2>Planned signup groups</h2><div class="groups">${groups}</div>`:''}
  <p class="muted">Mark your interest using the Discord event below. Live role signups are not available yet.</p></section>
  <aside class="panel"><div class="label">Organizer</div><div class="value">${E(item.organizer||organizer)}</div><div class="label">Location</div><div class="value">${E(item.location)}<br>${E(item.world)}</div><div class="label">Community</div><div class="value">${E(item.server)}</div></aside></div>
  </main><footer><span>Some events are worth falling into.</span><span>EVENT HORIZON</span></footer></article>
  <script nonce="eh-font-ready">Promise.all(Array.from(document.fonts,f=>f.load())).then(()=>document.fonts.ready).then(()=>{document.documentElement.dataset.fontsReady='true';}).catch(()=>{});</script></body></html>`;
}
export function renderer(browser, fontBytes, iconBytes, assets, store) {
  const font=b64(new Uint8Array(fontBytes)),icon=b64(new Uint8Array(iconBytes));
  let lastKey='',lastImage;
  return async (item,organizer,banner,schedule) => {
    const extraFonts=await fontCss(announcementStyle(item.announcementStyle),assets,store);
    const html=cardHtml(item,organizer,banner,schedule,font,icon,extraFonts);
    const key=b64(new Uint8Array(await crypto.subtle.digest('SHA-256',new TextEncoder().encode(html))));
    if(key===lastKey && lastImage)return lastImage;
    const response=await browser.quickAction('screenshot',{html,waitForSelector:{selector:'html[data-fonts-ready="true"]',timeout:15000},viewport:{width:1200,height:900,deviceScaleFactor:1},screenshotOptions:{type:'png',fullPage:true},gotoOptions:{waitUntil:'networkidle0',timeout:30000}});
    if(!response.ok)throw new RelayError('Announcement rendering failed. Try Render preview with another font; a selected font may be invalid or Browser Run may be unavailable.',502);
    const reader=response.body.getReader(),chunks=[];let size=0;
    for(;;){const {value,done}=await reader.read();if(done)break;size+=value.length;if(size>MAX_IMAGE){await reader.cancel();throw new Error('Card exceeds 4 MB');}chunks.push(value);}
    const bytes=new Uint8Array(await new Blob(chunks).arrayBuffer());
    if(bytes.length<24 || ![137,80,78,71,13,10,26,10].every((v,i)=>bytes[i]===v))throw new Error('Browser Run did not return PNG');
    lastKey=key;lastImage=bytes;return bytes;
  };
}
