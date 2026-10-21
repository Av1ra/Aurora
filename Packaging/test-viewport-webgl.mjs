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
 for(const filename of process.argv.slice(2)){
  const fixture=JSON.parse(fs.readFileSync(filename,'utf8'));await page.evaluate(s=>{load(s);if(s.Axis)gizmo({visible:true,editing:true,mode:'move',cargo:-1,position:s.Axis[0],axis:s.Axis});},fixture);
  const result=await page.evaluate(()=>{draw();const pixels=new Uint8Array(canvas.width*canvas.height*4);gl.readPixels(0,0,canvas.width,canvas.height,gl.RGBA,gl.UNSIGNED_BYTE,pixels);let visible=0;for(let i=0;i<pixels.length;i+=4)if(pixels[i]!==32||pixels[i+1]!==32||pixels[i+2]!==32)visible++;return{error:gl.getError(),visible};});
  assert.equal(result.error,0);assert(result.visible>100,'model produced no visible geometry: '+filename);assert.deepEqual(errors,[]);await page.screenshot({path:filename+'.png'});console.log('PASS native scene, axis overlay and real GPU draw: '+path.basename(filename));
 }
}finally{await browser.close();}
