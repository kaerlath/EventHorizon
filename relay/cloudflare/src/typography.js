import {sharedFonts,customFontId} from './font-catalog.js';
// Data-only styling: names are allowlisted; custom fonts use opaque account-owned IDs.
const lowerKeys = value => Object.fromEntries(Object.entries(value).map(([k,v]) => [k[0].toLowerCase()+k.slice(1),v]));
const object = value => value && typeof value === 'object' && !Array.isArray(value);
export function textStyle(raw, title = false) {
  if (raw != null && !object(raw)) throw new Error('Text style must be an object.');
  const data=raw == null ? {} : lowerKeys(raw), result={};
  const choice=(key,values,fallback)=>{
    const value=data[key]??fallback;
    if(!values.includes(value))throw new Error(`Choose a supported ${key}.`);
    result[key]=value;
  };
  const number=(key,min,max,fallback)=>{
    const value=data[key]??fallback;
    if(typeof value!=='number'||!Number.isFinite(value)||value<min||value>max)throw new Error(`Invalid text ${key}.`);
    result[key]=value;
  };
  const font=data.font??'Sans';
  if(!['Sans','Cinzel','Serif','Mono',...Object.keys(sharedFonts)].includes(font)&&!customFontId(font))throw new Error('Choose a supported font.');
  result.font=font; choice('align',['left','center','right'],'left');
  number('size',16,64,title?38:22); number('outline',0,3,0); number('glow',0,24,0);
  number('letterSpacing',0,6,0); number('lineHeight',1,2,1.5);
  for(const key of ['bold','italic','underline','strike']){
    const value=data[key]??(key==='bold'&&title);
    if(typeof value!=='boolean')throw new Error(`Invalid text ${key}.`); result[key]=value;
  }
  for(const [key,fallback] of [['color','#EDF1FF'],['outlineColor','#060B19'],['glowColor','#A695FF']]){
    const value=data[key]??fallback;
    if(typeof value!=='string'||!/^#[a-f0-9]{6}$/i.test(value))throw new Error('Choose a valid text color.');
    result[key]=value;
  }
  return result;
}
export function announcementStyle(raw){
  if(raw!=null&&!object(raw))throw new Error('Announcement style must be an object.');
  const data=raw==null?{}:lowerKeys(raw),paragraphs={};
  if(data.paragraphs!=null){
    if(!object(data.paragraphs)||Object.keys(data.paragraphs).length>64)throw new Error('Use up to 64 paragraph overrides.');
    for(const [key,value] of Object.entries(data.paragraphs)){
      if(!/^(0|[1-9][0-9]{0,2})$/.test(key))throw new Error('Invalid paragraph index.');
      paragraphs[key]=textStyle(value);
    }
  }
  return {title:textStyle(data.title,true),body:textStyle(data.body),paragraphs};
}
export function styleCss(style){
  const fonts={Sans:'Arial,sans-serif',Cinzel:'Cinzel,serif',Serif:'Georgia,serif',Mono:'monospace'};
  if(sharedFonts[style.font]||customFontId(style.font))fonts[style.font]=`'${style.font}'`;
  const decorations=[style.underline?'underline':'',style.strike?'line-through':''].filter(Boolean).join(' ')||'none';
  return `font-family:${fonts[style.font]};font-size:${style.size}px;font-weight:${style.bold?700:400};font-style:${style.italic?'italic':'normal'};text-decoration:${decorations};color:${style.color};text-align:${style.align};line-height:${style.lineHeight};letter-spacing:${style.letterSpacing}px;-webkit-text-stroke:${style.outline}px ${style.outlineColor};paint-order:stroke fill;text-shadow:${style.glow?`0 0 ${style.glow}px ${style.glowColor}`:'none'}`;
}
