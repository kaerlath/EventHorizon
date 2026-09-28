// Keep essential information readable as Discord text, independent of image scaling.
const plain=value=>String(value??'').replace(/([\\`*_~|>\[\]])/g,'\\$1');
export function announcementText(item,organizer,schedule){
  const start=Math.floor(Date.parse(schedule.start)/1000),end=Math.floor(Date.parse(schedule.end)/1000);
  const where=[item.world,item.location].filter(Boolean).join(' — ');
  const lines=[`**${plain(item.title)}**`,`<t:${start}:F> – <t:${end}:F>`, `Starts <t:${start}:R>`];
  if(where)lines.push(`**Location:** ${plain(where)}`);
  if(item.description)lines.push('',item.description);
  lines.push('',`**Organizer:** ${plain(item.organizer||organizer)}`);
  return lines.join('\n');
}
