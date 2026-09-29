import {itinerary} from './model.js';
// Keep essential information readable as Discord text, independent of image scaling.
const plain=value=>String(value??'').replace(/([\\`*_~|>\[\]])/g,'\\$1');
export function announcementText(item,organizer,schedule){
  const start=Math.floor(Date.parse(schedule.start)/1000),end=Math.floor(Date.parse(schedule.end)/1000);
  const where=[item.world,item.location].filter(Boolean).join(' — ');
  const lines=[`**${plain(item.title)}**`,`<t:${start}:F> – <t:${end}:F>`, `Starts <t:${start}:R>`];
  if(where)lines.push(`**Location:** ${plain(where)}`);
  if(item.description)lines.push('',item.description);
  if(item.scheduleMode==='Sessions')lines.push('',plain(itinerary(item)));
  lines.push('',`**Organizer:** ${plain(item.organizer||organizer)}`);
  return lines.join('\n');
}

export function announcementMessage(item,organizer,schedule,url,editing=false){
  const description=`${item.title} · ${item.startLocal} · ${item.location}`;
  return {
    flags:32768, // Components V2 preserves image -> text -> button ordering.
    ...(editing?{content:null,embeds:[]}:{}), // Clear legacy fields on existing posts.
    allowed_mentions:{parse:[]},
    attachments:[{id:0,filename:'event-card.png',description}],
    components:[
      {type:12,items:[{media:{url:'attachment://event-card.png'},description}]},
      {type:10,content:announcementText(item,organizer,schedule)},
      {type:1,components:[{type:2,style:5,label:'View event / Interested',url}]}
    ]
  };
}
