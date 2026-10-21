// Optional integration check: an isolated headless browser compiles the real GLSL.
// No user browser profile, network, or desktop input is used.
import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import assert from 'node:assert/strict';
const {chromium}=createRequire(import.meta.url)('playwright');
const folder=path.resolve('Aurora.Editor/Assets');
let html=fs.readFileSync(path.join(folder,'viewport.html'),'utf8');
for(const [marker,file] of [['GIZMO','gizmo.js'],['PREVIEWS','previews.js']])html=html.replace(`/*AURORA_${marker}*/`,fs.readFileSync(path.join(folder,file),'utf8'));
const browser=await chromium.launch({channel:'msedge',headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
try{
 const page=await browser.newPage({viewport:{width:800,height:600}});await page.route('**/*',route=>route.abort());const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.addInitScript(()=>{window.auroraMessages=[];window.chrome??={};window.chrome.webview={postMessage:value=>window.auroraMessages.push(value)};});
 await page.goto('about:blank');await page.setContent(html);await page.waitForTimeout(200);
 const messages=await page.evaluate(()=>window.auroraMessages);assert.deepEqual(errors,[]);assert.deepEqual(messages.filter(m=>m.error),[]);assert(messages.some(m=>m.ready),'real WebGL viewport did not become ready');
 await page.evaluate(()=>{load({Materials:['Paint'],Nodes:[{Positions:[-1,-1,0,1,-1,0,0,1,0],Normals:[0,0,1,0,0,1,0,0,1],Uv:[0,0,1,0,.5,1],Indices:[0,1,2],Groups:[{First:0,Count:3,Material:0}]}]});material('Paint',{diffuseColour:[.2,.4,.6,1],ambientColour:[1,1,1],specularColour:[.5,.5,.5]});});
 const glError=await page.evaluate(()=>gl.getError());assert.equal(glError,0,'real rendering reported a WebGL error');assert.deepEqual(errors,[]);console.log('PASS real WebGL2 shader compilation and viewport readiness in isolated headless Edge');
}finally{await browser.close();}
