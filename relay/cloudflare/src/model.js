import {announcementStyle} from './typography.js';
import zones from './windows-zones.json' with { type: 'json' };

export class RelayError extends Error {
  constructor(message, status = 400) { super(message); this.status = status; }
}
export const MAX_IMAGE = 4 * 1024 * 1024;
export const token = () => Array.from(crypto.getRandomValues(new Uint8Array(32)), b => b.toString(16).padStart(2, '0')).join('');
export const encode = s => String(s ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
export const b64 = bytes => {
  let text = ''; for (let i = 0; i < bytes.length; i += 8192) text += String.fromCharCode(...bytes.subarray(i, i + 8192));
  return btoa(text);
};
export function imageData(value) {
  if (value == null || value === '') return null;
  if (typeof value !== 'string' || value.length > Math.ceil(MAX_IMAGE / 3) * 4 + 32) throw new RelayError('Banner must be a PNG or JPEG up to 4 MB.');
  const match = /^data:(image\/(?:png|jpeg));base64,([A-Za-z0-9+/]+={0,2})$/.exec(value);
  if (!match) throw new RelayError('Invalid banner data.');
  let bytes; try { bytes = Uint8Array.from(atob(match[2]), c => c.charCodeAt(0)); } catch { throw new RelayError('Invalid banner encoding.'); }
  const png = bytes.length >= 24 && [137,80,78,71,13,10,26,10].every((n,i) => bytes[i] === n);
  const jpeg = bytes.length >= 3 && bytes[0] === 255 && bytes[1] === 216 && bytes[2] === 255;
  if (bytes.length > MAX_IMAGE || !(match[1] === 'image/png' ? png : jpeg)) throw new RelayError('Banner content does not match its image type.');
  return { bytes, mime: match[1] };
}
export function localStamp(date, zone, formatter) {
  if(typeof date==='string')date=new Date(date);
  const parts = (formatter ?? new Intl.DateTimeFormat('en-CA', { timeZone: zone, year:'numeric', month:'2-digit', day:'2-digit', hour:'2-digit', minute:'2-digit', hourCycle:'h23' })).formatToParts(date);
  const p = Object.fromEntries(parts.map(x => [x.type,x.value]));
  return `${p.year}-${p.month}-${p.day} ${p.hour}:${p.minute}`;
}
function instant(item, zone) {
  if (!/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$/.test(item.startLocal)) throw new RelayError('Choose a valid date and time.');
  const guess = Date.parse(item.startLocal.replace(' ', 'T') + ':00Z');
  if (!Number.isFinite(guess)) throw new RelayError('Choose a valid date and time.');
  const matches = [];
  try {
    const formatter = new Intl.DateTimeFormat('en-CA', { timeZone: zone, year:'numeric', month:'2-digit', day:'2-digit', hour:'2-digit', minute:'2-digit', hourCycle:'h23' });
    // Modern civil timezone offsets are multiples of fifteen minutes. Match the
    // wall clock explicitly so skipped and repeated DST times cannot publish.
    for (let offset = -14 * 60; offset <= 14 * 60; offset += 15) {
      const candidate = guess + offset * 60000;
      if (localStamp(candidate, zone, formatter) === item.startLocal) matches.push(candidate);
    }
  } catch { throw new RelayError('Choose a supported time zone.'); }
  if (matches.length !== 1) throw new RelayError('That date/time is invalid or occurs twice during daylight saving time. Choose another time.');
  return matches[0];
}
export function schedule(item, now = Date.now()) {
  const zone = zones[item.timeZoneId] || item.timeZoneId;
  const mode=item.scheduleMode??'Single';
  const parse=value=>instant({startLocal:value},zone);
  let rows;
  if(mode==='Single')rows=[[parse(item.startLocal),parse(item.startLocal)+item.durationMinutes*60000]];
  else if(mode==='Continuous')rows=[[parse(item.startLocal),parse(item.endLocal)]];
  else if(mode==='Sessions' && Array.isArray(item.sessions) && item.sessions.length>=1 && item.sessions.length<=12)
    rows=item.sessions.map(s=>[parse(s?.startLocal??s?.StartLocal),parse(s?.endLocal??s?.EndLocal)]).sort((a,b)=>a[0]-b[0]);
  else throw new RelayError('Choose a schedule with 1–12 sessions.');
  if(rows.some(([a,b],i)=>!Number.isFinite(b)||b<=a||(i>0&&a<rows[i-1][1])))throw new RelayError('Each end must follow its start and sessions must not overlap.');
  if(rows.at(-1)[1]-rows[0][0]>366*86400000)throw new RelayError('An event can span at most 366 days.');
  if(rows[0][0]<=now)throw new RelayError('Publishing requires a future start time.');
  const sessions=rows.map(([a,b])=>({start:new Date(a).toISOString(),end:new Date(b).toISOString()}));
  return {start:sessions[0].start,end:sessions.at(-1).end,zone,sessions};
}
export function itinerary(item) {
  if(item.scheduleMode!=='Sessions')return '';
  const times=schedule(item,0);
  return `Session schedule (${times.zone}):\n`+times.sessions.map(s=>`${localStamp(s.start,times.zone)} – ${localStamp(s.end,times.zone)}`).join('\n');
}
export function discordDescription(item) {
  const plan=itinerary(item);
  if(!plan)return item.description;
  const suffix='\n\n'+plan;
  const available=1000-suffix.length;
  const description=item.description.length>available?item.description.slice(0,Math.max(0,available-48))+'… Full description in the announcement.':item.description;
  return description+suffix;
}
export function validate(raw, id, now = Date.now(), preview = false) {
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) throw new RelayError('An event is required.');
  const e = Object.fromEntries(Object.entries(raw).map(([k,v]) => [k[0].toLowerCase()+k.slice(1),v]));
  if(e.informationOnly)throw new RelayError('Official events are information only.');
  const limits = {id:36,title:100,description:1000,location:100,world:100,startLocal:16,timeZoneId:100,organizer:100,signupGroups:600,signupsClose:100,server:100,channel:100,guildId:20,channelId:20,recurrence:30};
  const item = {};
  for (const [key,max] of Object.entries(limits)) {
    const value = e[key] ?? (key === 'recurrence' ? 'None' : '');
    if (typeof value !== 'string' || value.length > max) throw new RelayError(`Invalid ${key}.`);
    item[key] = value;
  }
  try { item.announcementStyle = announcementStyle(e.announcementStyle); }
  catch(error) { throw new RelayError(error.message); }
  item.id = item.id.toLowerCase();
  if (item.id !== id || !/^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/.test(id)) throw new RelayError('Event ID mismatch.');
  if(preview) { item.title ||= 'Untitled gathering'; item.location ||= 'To be announced'; item.guildId ||= '0'; item.channelId = ''; item.recurrence = 'None'; }
  if (!item.title.trim() || !item.location.trim() || `${item.world} — ${item.location}`.length > 100) throw new RelayError('Enter a title and location within Discord limits.');
  if (!/^\d+$/.test(item.guildId) || (item.channelId && !/^\d+$/.test(item.channelId))) throw new RelayError('Choose a Discord server and channel.');
  if (item.recurrence !== 'None') throw new RelayError('Recurring publication is not available yet.');
  item.scheduleMode=e.scheduleMode??'Single';
  item.endLocal=e.endLocal??'';
  item.sessions=e.sessions??[];
  if(item.scheduleMode==='Single' && (!Number.isInteger(e.durationMinutes) || e.durationMinutes < 1 || e.durationMinutes > 527040)) throw new RelayError('Duration must be between 1 minute and 366 days.');
  if(e.googleCalendarSync != null && typeof e.googleCalendarSync !== 'boolean')throw new RelayError('Invalid Google Calendar sync option.');
  item.googleCalendarSync = e.googleCalendarSync ?? false;
  item.durationMinutes = e.durationMinutes;
  const times=schedule(item,now);
  item.startLocal=localStamp(times.start,times.zone);
  item.durationMinutes=(Date.parse(times.end)-Date.parse(times.start))/60000;
  item.sessions=item.scheduleMode==='Sessions'?times.sessions.map(s=>({startLocal:localStamp(s.start,times.zone),endLocal:localStamp(s.end,times.zone)})):[];
  item.endLocal=item.scheduleMode==='Continuous'?localStamp(times.end,times.zone):'';
  return { item, ...times };
}
export function permissions(guild, user, member, roles, channel) {
  const ids = new Set(member.roles);
  let p = roles.filter(r => r.id === guild || ids.has(r.id)).reduce((p,r) => p | BigInt(r.permissions),0n);
  if (p & 8n) return p;
  const apply = o => { p = (p & ~BigInt(o.deny)) | BigInt(o.allow); };
  const overwrites = channel.permission_overwrites ?? [];
  overwrites.filter(o => o.id === guild).forEach(apply);
  let deny = 0n, allow = 0n;
  for (const o of overwrites.filter(o => o.type === 0 && ids.has(o.id))) { deny |= BigInt(o.deny); allow |= BigInt(o.allow); }
  p = (p & ~deny) | allow;
  overwrites.filter(o => o.type === 1 && o.id === user).forEach(apply);
  return p;
}
export const canCreate = p => (BigInt(p) & (8n | (1n<<33n) | (1n<<44n))) !== 0n;
const postMask = (1n<<10n) | (1n<<11n) | (1n<<14n) | (1n<<15n);
export const canPost = p => !!(p & 8n) || (p & postMask) === postMask;
