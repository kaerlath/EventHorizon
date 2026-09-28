export const sharedFonts = {
  'Orbitron':'Futuristic', 'Rajdhani':'Futuristic', 'Exo 2':'Futuristic',
  'Chakra Petch':'Futuristic', 'Oxanium':'Futuristic', 'Space Grotesk':'Modern',
  'Inter':'Readable', 'Nunito Sans':'Readable', 'Lora':'Literary',
  'Cormorant Garamond':'Fantasy', 'Playfair Display':'Elegant',
  'Uncial Antiqua':'Fantasy', 'Grenze Gotisch':'Gothic', 'Great Vibes':'Script',
  'Dancing Script':'Script', 'Caveat':'Handwritten', 'JetBrains Mono':'Monospace',
  'Special Elite':'Typewriter',
};
export const customFontId = value => typeof value === 'string' && /^custom-[a-f0-9]{64}$/.test(value);
export function usedFonts(style) {
  return [...new Set([style.title,style.body,...Object.values(style.paragraphs)].map(s=>s.font))];
}
