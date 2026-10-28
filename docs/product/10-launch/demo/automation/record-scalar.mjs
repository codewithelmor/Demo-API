import { chromium } from 'playwright';
import fs from 'fs';
const sc=JSON.parse(fs.readFileSync('narration.json'));
const dur=Object.fromEntries(sc.map(s=>[s.id,s.dur]));
const pw=JSON.parse(fs.readFileSync('C:/Users/elmorc/source/repos/Demo-API/api-tests/http/http-client.private.env.json')).local.adminPassword;
fs.mkdirSync('shots',{recursive:true});
const b=await chromium.launch();
const T0=Date.now();
const ctx=await b.newContext({viewport:{width:1600,height:900},recordVideo:{dir:'vid2',size:{width:1600,height:900}}});
const page=await ctx.newPage(); const now=()=>(Date.now()-T0)/1000;
const sleep=ms=>new Promise(r=>setTimeout(r,ms));
let token='';
page.on('response',async r=>{if(r.url().endsWith('/api/v1/auth/login')&&r.status()===200){try{token=(await r.json()).accessToken}catch{}}});
await page.goto('http://localhost:8080/scalar/',{waitUntil:'networkidle'});
await page.addStyleTag({content:'html{scroll-behavior:auto!important}'});
const marks={};
async function scene(id,fn){marks[id]=now();const t=Date.now();await fn();await page.screenshot({path:`shots/${id}.png`});const rest=dur[id]*1000+700-(Date.now()-t);if(rest>0)await sleep(rest)}
async function open(id){
  await page.evaluate(h=>{location.hash=h},id.replace(/^v1\//,''));
  await sleep(1300);
  await page.locator(`[id="${id}"]`).getByRole('button',{name:/Test Request/}).first().click();
  await sleep(1200);
}
const modal=()=>page.locator('.scalar-modal, [role=dialog]').last();
async function send(match){
  const w=page.waitForResponse(r=>r.url().includes(match),{timeout:8000});
  w.catch(()=>{});
  try{
    await page.locator('button.scalar-button:has-text("Send"):visible').first().click({timeout:5000});
    await w;
  }catch(e){await page.screenshot({path:'shots/FAIL.png'});throw e}
  await sleep(1500);
}
async function close(){await page.keyboard.press('Escape');await sleep(700)}
await scene('intro',async()=>{});
await scene('ready',async()=>{await open('v1/tag/health/GET/health/ready');await send('/health/ready')});
await close();
await scene('live',async()=>{await open('v1/tag/health/GET/health/live');await send('/health/live')});
await close();
await scene('login',async()=>{
  await open('v1/tag/auth/POST/api/v1/auth/login');
  await page.evaluate(()=>setInterval(()=>document.querySelectorAll('.cm-line').forEach(l=>{if(/password|refreshToken"/.test(l.textContent))l.style.filter='blur(7px)'}),30));
  await page.locator('.cm-content').last().click();
  await page.keyboard.press('Control+A');
  await page.keyboard.insertText(JSON.stringify({email:'admin@demoapi.local',password:pw},null,2));
  await sleep(1800);
  await send('/auth/login');
  await sleep(1500);
});
await close();
await scene('products',async()=>{
  await open('v1/tag/products/GET/api/v1/products');
  const f=page.locator('[placeholder="Token"], [aria-label="Token"]').first();
  await f.click();await sleep(400);
  await page.keyboard.insertText(token);await sleep(1800);
  await send('/api/v1/products');
});
await close();
await scene('customers',async()=>{await open('v1/tag/customers/GET/api/v1/customers');await send('/api/v1/customers')});
await close();
await scene('orders',async()=>{await open('v1/tag/orders/GET/api/v1/orders');await send('/api/v1/orders')});
await close();
await scene('outro',async()=>{});
const total=now();
await page.close();await ctx.close();await b.close();
fs.writeFileSync('marks.json',JSON.stringify({marks,total},null,1));console.log(marks,total,'token',token.length);
