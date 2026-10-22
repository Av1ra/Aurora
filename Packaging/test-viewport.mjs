import fs from 'node:fs';
import assert from 'node:assert/strict';

// Exercise the actual viewport script with a deterministic GPU boundary.
// This covers async scene ownership and coordinate conversion, not visual quality.
const html = fs.readFileSync(new URL('../Aurora.Editor/Assets/viewport.html', import.meta.url), 'utf8');
const script = html.match(/<script>([\s\S]*?)<\/script>/)[1].replace('/*AURORA_GIZMO*/',fs.readFileSync(new URL('../Aurora.Editor/Assets/gizmo.js',import.meta.url),'utf8')).replace('/*AURORA_PREVIEWS*/',fs.readFileSync(new URL('../Aurora.Editor/Assets/previews.js',import.meta.url),'utf8'));
const images = [], deleted = [], messages = [], reads = [],drawCalls=[];
const uniforms = new Map();
let created = 0, pickId = 1, viewportSize;const previewFrames=[],clearColours=[],eventHandlers=new Map(),cameraFrames=[],animationFrames=[],effectFrames=[];let frameTime=0,spriteCount=0;
class Texture {}
class ImageStub { constructor() { images.push(this); } }
const gpu = new Proxy({
  LINES:2,TRIANGLES:3,drawElements:(mode,count)=>drawCalls.push({mode,count}),
  drawArrays:()=>spriteCount++,
  getShaderParameter: () => true, getProgramParameter: () => true,
  createTexture: () => { created++; return new Texture(); },
  getUniformLocation: (_, name) => name,
  uniform1i: (name, value) => uniforms.set(name, value),
  uniform1f: (name,value)=>uniforms.set(name,value),
  clearColor:(...args)=>clearColours.push(args),
  deleteTexture: texture => deleted.push(texture),
  viewport: (...args) => viewportSize = args,
  readPixels: (...args) => { reads.push(args.slice(0, 4)); args[6].set([pickId, 0, 0, 255]); }
}, { get: (target, name) => name in target ? target[name] : /^[A-Z_0-9]+$/.test(name) ? 1 : () => ({}) });
const canvas = { clientWidth: 200, clientHeight: 100, width: 200, height: 100,
  getContext: () => gpu, setPointerCapture() {},toDataURL:()=>{previewFrames.push({size:viewportSize,focal:uniforms.get('focal')});return 'data:image/png;base64,TEST';} };
const window = {devicePixelRatio:2,addEventListener:(name,handler)=>{const previous=eventHandlers.get(name);eventHandlers.set(name,previous?e=>{previous(e);handler(e);}:handler);},chrome:{webview:{postMessage:message=>messages.push(message)}}};
const overlayContext=new Proxy({}, {get:(_,key)=>key==='createRadialGradient'?()=>({addColorStop(){}}):()=>{}});
const overlay={style:{},getContext:()=>overlayContext};
const document = { getElementById: () => canvas,createElement:()=>overlay,body:{appendChild(){}} };
const api = new Function('document', 'window', 'ResizeObserver', 'Image', 'WebGLTexture','requestAnimationFrame',
  script + '\nreturn {playAnimation,stopAnimation,poseWheels,replaceCargo,poseMechanism,previewAnimation,sampleAnimation,allNodes:()=>nodes,materialState:()=>materials,load,texture,requestPreview,wireframe,lighting,lampTexture,startPreviewCamera,stopPreviewCamera,previewCameraPose,gizmo,gizmoRotate,gizmoProject,camera:()=>({yaw,pitch,distance,focal}),tool:()=>gizmoState,segments:()=>gizmoSegments,selected:()=>selected,cargoNodes:()=>nodes.filter(n=>n.Cargo),bounds:()=>({center:scene.center,extent:scene.extent})};')(document, window, class { observe() {} }, ImageStub, Texture,cb=>{if(cb.name==='cameraFrameTick')cameraFrames.push(cb);else if(cb.name==='animationFrameTick')animationFrames.push(cb);else if(cb.name==='diagnosticEffectTick')effectFrames.push(cb);else cb(0);});
assert.match(overlay.style.cssText,/background\s*:\s*transparent(?:;|$)/,'pivot overlay must override the shared canvas background so loaded NMF stays visible');
const scene = () => ({ Materials: ['body'], Nodes: [{
  Positions: [0,0,0, 1,0,0, 0,1,0], Normals: [0,0,1, 0,0,1, 0,0,1],
  Uv: [0,0, 1,0, 0,1], Indices: [0,1,2], Groups: [{ Material: 0, First: 0, Count: 3 }]
}] });
api.load(scene());
assert.deepEqual(viewportSize, [0, 0, 400, 200]);
api.texture('body', 'old-scene');
api.load(scene());
images[0].onload();
assert.equal(created, 0, 'stale image must not bind a texture into a new scene');
api.texture('body', 'first-map'); images[1].onload();
api.texture('body', 'replacement-map'); images[2].onload();
assert.equal(created, 2);
assert.equal(deleted.length, 1, 'replaced GPU texture must be released');
api.texture('body', 'slow-map'); api.texture('body', 'newer-map'); images[4].onload(); images[3].onload();
assert.equal(created, 3, 'late map in the same scene must not replace a newer request');
canvas.onpointerdown({clientX:20, clientY:30, pointerId:1});
canvas.onpointerup({clientX:20, clientY:30, offsetX:20, offsetY:30});
assert.deepEqual(reads.at(-1), [40,139,1,1]);
assert.equal(api.selected(), 0);
pickId = 0;
canvas.onpointerdown({clientX:20, clientY:30, pointerId:1});
canvas.onpointerup({clientX:20, clientY:30, offsetX:20, offsetY:30});
assert.equal(api.selected(), -1, 'background click clears the object selection');
assert.deepEqual(messages.at(-1), {selected:-1});
api.load(scene());
assert.equal(deleted.length, 3, 'scene replacement releases remaining GPU textures');
api.texture('body', 'specular', 'specularMap'); images.at(-1).onload();
assert.equal(uniforms.get('hasSpecular'), 1, 'specular map enables its own shader channel');
assert.equal(uniforms.get('hasMetal'), 0, 'specular import must not masquerade as metallic');
assert.notEqual(uniforms.get('specularMap'), uniforms.get('metalMap'), 'specular and metallic use separate texture units');
api.load(scene());
assert.equal(deleted.length, 4, 'specular GPU texture is disposed with its scene');
console.log('PASS viewport scene ownership, texture lifetime, DPI picking and background deselection');
api.gizmo({visible:true,editing:true,mode:'move',position:[.5,.5,0],rotation:[0,0,0]});
let segment=api.segments().find(s=>s.axis===0);assert.ok(segment,'move tool creates a pickable X axis');
let start=segment.a.map((x,i)=>x*.3+segment.b[i]*.7),step=segment.b.map((x,i)=>(x-segment.a[i])*.25);
canvas.onpointerdown({clientX:start[0],clientY:start[1],pointerId:2});
canvas.onpointermove({clientX:start[0]+step[0],clientY:start[1]+step[1]});
canvas.onpointerup({clientX:start[0]+step[0],clientY:start[1]+step[1]});
assert.ok(api.tool().position[0]>.5,'arrow dragging changes the corresponding world position');
assert.equal(api.tool().position[1],.5);assert.equal(api.tool().position[2],0);
assert.ok(messages.some(m=>m.gizmo),'gizmo drag sends numeric state to the editor');
api.gizmo({mode:'rotate',position:[.5,.5,0],rotation:[0,0,0]});
assert.ok(api.segments().length>280,'rotation tool exposes three sampled rings');
let ring=api.segments().find(s=>s.axis===1&&s.angle>1&&s.angle<1.2);
start=ring.a.map((x,i)=>(x+ring.b[i])/2);
canvas.onpointerdown({clientX:start[0],clientY:start[1],pointerId:3});
canvas.onpointermove({clientX:start[0]+8,clientY:start[1]+5});
canvas.onpointerup({clientX:start[0]+8,clientY:start[1]+5});
assert.ok(api.tool().rotation.every(Number.isFinite)&&api.tool().rotation.some(x=>Math.abs(x)>.01),'ring dragging changes a finite Euler angle');
assert.deepEqual(api.gizmoRotate([0,1,0],[90,0,0]).map(x=>Math.round(x)),[0,0,1],'tool frame uses the same handedness as exported model rotation');
api.gizmo({visible:false});assert.equal(api.segments().length,0,'hidden tools do not intercept object selection');
console.log('PASS actual move arrows, rotation rings, editor messages and tool-frame handedness');
const cameraBefore=api.camera();
await api.requestPreview('main',{width:256,height:256,fov:20,yaw:-45,pitch:30});
await api.requestPreview('side',{width:256,height:128,fov:20,yaw:-90,pitch:0});
assert.deepEqual(previewFrames.map(p=>p.size),[[0,0,256,256],[0,0,256,128]]);
assert.ok(previewFrames.every(p=>Math.abs(p.focal-1/Math.tan(Math.PI/18))<1e-8),'preview uses requested 20 degree FOV');
assert.deepEqual(api.camera(),cameraBefore,'preview generation restores editor camera');
assert.ok(clearColours.some(c=>c[3]===0),'preview background is transparent');
assert.ok(messages.some(m=>m.preview==='main'&&m.png)&&messages.some(m=>m.preview==='side'&&m.png),'native editor receives both preview images');
assert.ok(messages.some(m=>m.gizmo?.phase==='begin')&&messages.some(m=>m.gizmo?.phase==='end'),'one drag has explicit undo transaction boundaries');
console.log('PASS native-size FOV previews, transparent capture, camera restoration and undo drag transactions');
let prevented=0;for(const code of ['KeyZ','KeyY'])eventHandlers.get('keydown')({ctrlKey:true,metaKey:false,altKey:false,code,preventDefault(){prevented++;}});
assert.equal(prevented,2);assert.ok(messages.some(m=>m.history==='undo')&&messages.some(m=>m.history==='redo'),'viewport focus forwards undo/redo to native document history');

// Actual scale handles and live cargo vertex updates, without rebuilding the scene.
const cargoScene=scene();cargoScene.Nodes.push({...cargoScene.Nodes[0],Cargo:true,CargoSlot:0,LocalPositions:[0,0,0,1,0,0,0,1,0],LocalNormals:[0,0,1,0,0,1,0,0,1]});api.load(cargoScene);
api.gizmo({visible:true,editing:true,mode:'scale',cargo:0,uniformScale:false,position:[0,0,0],rotation:[0,0,0],scale:[1,1,1]});
segment=api.segments().find(s=>s.axis===0);start=segment.a.map((x,i)=>x*.2+segment.b[i]*.8);step=segment.b.map((x,i)=>(x-segment.a[i])*.5);
canvas.onpointerdown({clientX:start[0],clientY:start[1],pointerId:4});canvas.onpointermove({clientX:start[0]+step[0],clientY:start[1]+step[1]});canvas.onpointerup({clientX:start[0]+step[0],clientY:start[1]+step[1]});
assert.ok(api.tool().scale[0]>1);assert.equal(api.tool().scale[1],1);assert.equal(api.tool().scale[2],1);assert.equal(api.cargoNodes()[0].Positions[3],api.tool().scale[0],'axis scaling updates visible cargo vertices immediately');
api.gizmo({mode:'scale',uniformScale:true,scale:[1,2,3]});segment=api.segments().find(s=>s.axis===3);assert.ok(segment,'uniform scaling exposes the centre handle');start=segment.a;
canvas.onpointerdown({clientX:start[0],clientY:start[1],pointerId:5});canvas.onpointermove({clientX:start[0],clientY:start[1]-20});canvas.onpointerup({clientX:start[0],clientY:start[1]-20});
assert.ok(api.tool().scale[0]>1);assert.equal(api.tool().scale[1]/api.tool().scale[0],2);assert.equal(api.tool().scale[2]/api.tool().scale[0],3,'uniform scaling preserves aspect ratios');
api.gizmo({mode:'move',position:[2,3,4],rotation:[0,0,90],scale:[2,3,4]});assert.deepEqual(api.cargoNodes()[0].Positions.slice(0,3),[2,3,4]);assert.ok(Math.abs(api.cargoNodes()[0].Positions[4]-5)<1e-6,'cargo rotation and nonuniform scale use the export coordinate convention');assert.ok(messages.some(m=>m.gizmo?.scale),'cargo scale is sent to native undo history');
console.log('PASS visual cargo axis/uniform scale, live vertices and native transform convention');

const ordinary=scene();api.load(ordinary);const vehicleBounds=api.bounds();const distantCargo=scene();distantCargo.Nodes.push({...distantCargo.Nodes[0],Cargo:true,Positions:[1000,1000,1000,1001,1000,1000,1000,1001,1000]});api.load(distantCargo);assert.deepEqual(api.bounds(),vehicleBounds,'switching cargo resources must not reframe or move the vehicle camera');
console.log('PASS cargo resource switching keeps vehicle framing stable');

api.load(scene());drawCalls.length=0;api.wireframe(true);assert.ok(drawCalls.some(c=>c.mode===2&&c.count===6),'wireframe draws the three edges of the actual triangle');drawCalls.length=0;api.wireframe(false);assert.ok(!drawCalls.some(c=>c.mode===2),'wireframe toggle off restores solid-only rendering');api.wireframe(true);drawCalls.length=0;await api.requestPreview('no-wire',{width:256,height:128,fov:20,yaw:0,pitch:0});const firstLines=drawCalls.findIndex(c=>c.mode===2);assert.ok(firstLines>0&&drawCalls[0].mode===3,'exported preview draws solid geometry before restoring editor wireframe');api.wireframe(false);
console.log('PASS wireframe edges, toggle restoration and solid export previews');

const originalCamera=api.camera(),originalBounds=api.bounds(),oldDown=canvas.onpointerdown;
api.startPreviewCamera({width:256,height:256,fov:20,yaw:-45,pitch:30});const initialPose=api.previewCameraPose();
const key=k=>eventHandlers.get('keydown')({key:k,ctrlKey:false,altKey:false,metaKey:false,preventDefault(){}});
const frames=count=>{for(let i=0;i<count;i++){frameTime+=1000/60;const next=cameraFrames.splice(0);next.forEach(cb=>cb(frameTime));}};
key('w');key('q');key('z');frames(30);for(const k of ['w','q','z'])eventHandlers.get('keyup')({key:k});frames(100);let pose=api.previewCameraPose();assert.notEqual(pose.Y,initialPose.Y);assert.ok(pose.Roll<0);
for(let i=0;i<150;i++)canvas.onwheel({deltaY:1,preventDefault(){}});assert.equal(api.previewCameraPose().Fov,120);
for(let i=0;i<150;i++)canvas.onwheel({deltaY:-1,preventDefault(){}});assert.equal(api.previewCameraPose().Fov,5);
canvas.onpointerdown({clientX:10,clientY:10,pointerId:8});canvas.onpointermove({clientX:30,clientY:15});canvas.onpointerup({});assert.notEqual(api.previewCameraPose().Yaw,initialPose.Yaw);
key('r');assert.deepEqual(api.previewCameraPose(),initialPose,'reset restores the standard camera');
key('d');frames(30);eventHandlers.get('keyup')({key:'d'});frames(100);pose=api.previewCameraPose();assert.ok(Object.values(pose).every(v=>Math.abs(v*10-Math.round(v*10))<1e-8),'stored camera settings have at most one decimal');
api.stopPreviewCamera();assert.deepEqual(api.camera(),originalCamera);assert.deepEqual(api.bounds(),originalBounds);assert.equal(canvas.onpointerdown,oldDown,'camera editor restores original picking/gizmo handlers');
await api.requestPreview('saved-camera',{width:256,height:256,fov:20,yaw:0,pitch:0,pose:{...pose,Fov:120}});assert.ok(Math.abs(previewFrames.at(-1).focal-1/Math.tan(Math.PI/3))<1e-8);
api.lampTexture('forward','data:image/png;base64,GAME');images.at(-1).onload();api.lighting({night:true,lights:[{position:[0,0,0],direction:[0,0,1],rear:false}]});assert.ok(spriteCount>0,'native artwork uses a GPU sprite pass');const beforeShot=spriteCount;await api.requestPreview('night-shot',{width:256,height:128,fov:20,yaw:0,pitch:0});assert.equal(spriteCount,beforeShot+1,'transparent shot excludes lamps, then restores editor lamps');api.lighting({night:false,lights:[]});const off=spriteCount;api.wireframe(false);assert.equal(spriteCount,off,'day mode excludes lamp sprites');
console.log('PASS smooth held-key camera, roll/reset/FOV limits, saved poses, handler restoration and native lamp sprite pass');
await api.requestPreview('editor-main-hq',{width:1024,height:1024,fov:20,yaw:-45,pitch:30});assert.deepEqual(previewFrames.at(-1).size,[0,0,1024,1024]);await api.requestPreview('editor-side-hq',{width:1024,height:512,fov:20,yaw:-90,pitch:0});assert.deepEqual(previewFrames.at(-1).size,[0,0,1024,512]);assert.deepEqual(api.camera(),originalCamera,'high-quality inspection restores viewport camera');
console.log('PASS high-resolution camera inspection retains native-size export behavior');

const bodyScene=scene();bodyScene.Nodes[0].Name='body';api.load(bodyScene);api.texture('body','data:BODY');images.at(-1).onload();const bodyNode=api.allNodes()[0],bodyVao=bodyNode.vao,bodyBuffers=bodyNode.buffers.slice(),bodyTexture=api.materialState().body.texture,framing=api.bounds();
const cargoNode={...bodyScene.Nodes[0],Name:'cargo',Cargo:true,CargoSlot:0,Groups:[{Material:1,First:0,Count:3}]};api.replaceCargo({Materials:['body','__cargo__grain'],Nodes:[bodyScene.Nodes[0],cargoNode]});assert.equal(api.allNodes()[0],bodyNode);assert.equal(bodyNode.vao,bodyVao);assert.deepEqual(bodyNode.buffers,bodyBuffers);assert.equal(api.materialState().body.texture,bodyTexture);assert.deepEqual(api.bounds(),framing);
api.poseMechanism({mesh:'body',start:[0,0,0],end:[1,0,0],angle:90,cargo:true});assert.ok(Math.abs(bodyNode.Positions[8]-1)<1e-6);assert.ok(Math.abs(api.cargoNodes()[0].Positions[8]-1)<1e-6);assert.equal(bodyNode.vao,bodyVao);api.poseMechanism(null);assert.deepEqual(bodyNode.Positions,bodyScene.Nodes[0].Positions);
const anim={Name:'Dumper',Fps:25,LastFrame:100,Tracks:[{Mesh:'body',Pivot:[0,0,0],CargoParent:'body',Interpolation:'Smooth',Keys:[{Frame:0,Pose:{Position:[0,0,0],Rotation:[0,0,0],Scale:[1,1,1]}},{Frame:100,Pose:{Position:[0,0,0],Rotation:[90,0,0],Scale:[1,1,1]}}]}]};assert.equal(api.sampleAnimation(anim.Tracks[0],50).Rotation[0],45);api.previewAnimation(anim,50);assert.ok(Math.abs(bodyNode.Positions[8]-Math.SQRT1_2)<1e-6);assert.ok(Math.abs(api.cargoNodes()[0].Positions[8]-Math.SQRT1_2)<1e-6);assert.equal(api.materialState().body.texture,bodyTexture);assert.deepEqual(api.bounds(),framing);assert.equal(bodyNode.vao,bodyVao);
console.log('PASS cargo-only replacement and keyframe poses retain vehicle VAOs, textures and camera; cargo follows the animated body');

api.playAnimation(anim,0);animationFrames.shift()(1000);animationFrames.shift()(3000);assert.ok(Math.abs(bodyNode.Positions[8]-Math.SQRT1_2)<1e-6);api.stopAnimation();const frozen=bodyNode.Positions.slice();animationFrames.shift()(5000);assert.deepEqual(bodyNode.Positions,frozen);anim.Tracks[0].AxisRotation=[0,90,0];api.previewAnimation(anim,100);assert.ok(Math.abs(bodyNode.Positions[6]-1)<1e-6&&Math.abs(bodyNode.Positions[7])<1e-6);assert.equal(api.materialState().body.texture,bodyTexture);console.log('PASS playback FPS advances intermediate frames, stop freezes pose, and oriented animation axes match native matrix convention');
