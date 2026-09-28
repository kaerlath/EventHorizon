import {googleConfigured} from './google.js';
import {RelayCore,json,configured} from './core.js';
import {renderer} from './card.js';
import font from '../../../assets/fonts/Cinzel.ttf';
import icon from '../../../assets/icon.png';

export default {
  async fetch(request,env) {
    const url=new URL(request.url);
    if(url.pathname==='/health')return json({service:'Event Horizon',version:'0.14.1',configured:configured(env),composition:'browser-run',googleCalendar:googleConfigured(env),fontLibrary:true});
    if(!/^\/(auth\/|google\/|discord\/|fonts$|preview$|events(?:\/|$))/.test(url.pathname))return json({error:'Unknown endpoint.'},404);
    if(Number(request.headers.get('Content-Length')??0)>6*1024*1024)return json({error:'Request exceeds 6 MB.'},413);
    return env.RELAY.get(env.RELAY.idFromName('event-horizon-beta-v1')).fetch(request);
  }
};

export class EventRelay {
  constructor(ctx,env) {
    if(env.PUBLIC_ORIGIN) {
      const origin=new URL(env.PUBLIC_ORIGIN);
      if(origin.protocol!=='https:'||origin.pathname!=='/'||origin.username||origin.password||origin.search||origin.hash)throw new Error('PUBLIC_ORIGIN must be an HTTPS origin');
    }
    this.core=new RelayCore(ctx.storage,env,renderer(env.BROWSER,font,icon,env.FONTS,ctx.storage));
    this.tail=Promise.resolve();this.pending=0;this.rates=new Map();
  }
  async fetch(request) {
    const now=Date.now(),ip=request.headers.get('CF-Connecting-IP')??'local';
    for(const [k,v] of this.rates)if(v.until<now)this.rates.delete(k);
    const rate=this.rates.get(ip)??{until:now+60000,count:0};rate.count++;this.rates.set(ip,rate);
    if(rate.count>60||this.rates.size>10000||this.pending>=20)return json({error:'Relay busy. Try again shortly.'},429);
    this.pending++;
    const run=this.tail.then(()=>this.core.handle(request));
    this.tail=run.catch(()=>{});
    try{return await run;}finally{this.pending--;}
  }
  async alarm() {
    const run=this.tail.then(()=>this.core.cleanup());this.tail=run.catch(()=>{});await run;
  }
}
