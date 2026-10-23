import fs from 'node:fs';
import assert from 'node:assert/strict';

// Exercise the actual viewport script with a deterministic GPU boundary.
// This covers async scene ownership and coordinate conversion, not visual quality.
const html = fs.readFileSync(new URL('../Aurora.Editor/Assets/viewport.html', import.meta.url), 'utf8');
const script = html.match(/<script>([\s\S]*?)<\/script>/)[1].replace('/*AURORA_GIZMO*/',fs.readFileSync(new URL('../Aurora.Editor/Assets/gizmo.js',import.meta.url),'utf8')).replace('/*AURORA_PREVIEWS*/',fs.readFileSync(new URL('../Aurora.Editor/Assets/previews.js',import.meta.url),'utf8')).replace('/*AURORA_SELECTION*/',fs.readFileSync(new URL('../Aurora.Editor/Assets/selection.js',import.meta.url),'utf8'));
let vertexUploads=0;const images = [], deleted = [], messages = [], reads = [],drawCalls=[];
const uniforms = new Map();
let created = 0, pickId = 1, viewportSize;const previewFrames=[],clearColours=[],eventHandlers=new Map(),cameraFrames=[],animationFrames=[],effectFrames=[];let frameTime=0,spriteCount=0;const wheelFrames=[];
class Texture {}
class ImageStub { constructor() { images.push(this); } }
const gpu = new Proxy({
  bufferData:()=>vertexUploads++,checkFramebufferStatus:()=>1,LINES:2,TRIANGLES:3,drawElements:(mode,count)=>drawCalls.push({mode,count}),
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
  script + '\nreturn {selectionOptions,regionSelection,clickSelection,meshPreview,playWheels,stopWheels,selectionState:()=>selectionState,wheelPlayback:()=>wheelPlayback,resetView:()=>{yaw=pitch=roll=0;distance=4;focal=2;},updateScene,effectLights,playAnimation,stopAnimation,poseWheels,replaceCargo,poseMechanism,previewAnimation,sampleAnimation,allNodes:()=>nodes,materialState:()=>materials,load,texture,requestPreview,wireframe,lighting,lampTexture,startPreviewCamera,stopPreviewCamera,previewCameraPose,gizmo,gizmoRotate,gizmoProject,camera:()=>({yaw,pitch,distance,focal}),tool:()=>gizmoState,segments:()=>gizmoSegments,selected:()=>selected,cargoNodes:()=>nodes.filter(n=>n.Cargo),bounds:()=>({center:scene.center,extent:scene.extent})};')(document, window, class { observe() {} }, ImageStub, Texture,cb=>{if(cb.name==='cameraFrameTick')cameraFrames.push(cb);else if(cb.name==='animationFrameTick')animationFrames.push(cb);else if(cb.name==='wheelFrameTick')wheelFrames.push(cb);else if(cb.name==='diagnosticEffectTick')effectFrames.push(cb);else cb(0);});
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
canvas.onpointerdown({altKey:true,clientX:10,clientY:10,pointerId:8});canvas.onpointermove({clientX:30,clientY:15});canvas.onpointerup({});assert.notEqual(api.previewCameraPose().Yaw,initialPose.Yaw);
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

// Toggle off must remove the point even when an earlier RAF tick remains queued.
api.lighting({night:false,lights:[],animate:false});assert.equal(api.effectLights().length,0);
const leftIndicator={position:[-1,1,2],direction:[0,0,1],kind:'blinker'};api.lighting({night:false,lights:[leftIndicator],animate:true});effectFrames.shift()(1000);assert.equal(api.effectLights().length,1);assert.deepEqual(api.effectLights()[0].position,[-1,1,2]);effectFrames.shift()(1750);assert.equal(api.effectLights().length,0);effectFrames.shift()(2000);assert.equal(api.effectLights().length,1);api.lighting({night:false,lights:[],animate:false});while(effectFrames.length)effectFrames.shift()(2250);assert.equal(api.effectLights().length,0);console.log('PASS indicators stay off before activation, blink after activation and stay off after deactivation despite queued frames');

const wheelPoints=[2,0,4,4,0,4,3,2,4,-4,0,4,-2,0,4,-3,2,4];api.load({Materials:['wheel'],Nodes:[{Name:'tire_leftShape',Positions:wheelPoints,Normals:Array(6).fill([0,0,1]).flat(),Uv:Array(12).fill(0),Indices:[0,1,2,3,4,5],Groups:[{Material:0,First:0,Count:6}]}]});const wheelParts=[{Mesh:'tire_leftShape',Component:0,Vertices:[0,1,2],Pivot:[3,1,4],Axle:[1,0,0],SteeringAxis:[0,1,0],Steering:true},{Mesh:'tire_leftShape',Component:1,Vertices:[3,4,5],Pivot:[-3,1,4],Axle:[1,0,0],SteeringAxis:[0,1,0],Steering:true}];const wheelVao=api.allNodes()[0].vao;api.poseWheels(wheelParts,90,0,'tire_leftShape/0');let posedWheels=api.allNodes()[0].Positions;assert.deepEqual(posedWheels.slice(9),wheelPoints.slice(9));assert.ok(Math.abs(posedWheels[0]-3)<1e-6&&Math.abs(posedWheels[2]-5)<1e-6);api.poseWheels(wheelParts,0,0);assert.deepEqual(api.allNodes()[0].Positions,wheelPoints);assert.equal(api.allNodes()[0].vao,wheelVao);console.log('PASS a selected wheel rotates about its hub while other components of the same mesh remain unchanged; reset restores every wheel without VAO recreation');

// Ordinary edits reuse the document GPU resources and current framing.
api.updateScene(bodyScene,'document-a');const cached=api.allNodes()[0],cachedVao=cached.vao,cachedBuffers=cached.buffers.slice();api.texture('body','cached-map');images.at(-1).onload();const cachedTexture=api.materialState().body.texture;
api.updateScene(structuredClone(bodyScene),'document-a');assert.equal(api.allNodes()[0],cached);assert.equal(cached.vao,cachedVao);assert.deepEqual(cached.buffers,cachedBuffers);assert.equal(api.materialState().body.texture,cachedTexture);
const edited=structuredClone(bodyScene);edited.Nodes[0].Positions[0]=.25;api.updateScene(edited,'document-a');assert.equal(api.allNodes()[0].vao,cachedVao);assert.equal(api.allNodes()[0].Positions[0],.25);
api.gizmo({visible:false});const beforePan=structuredClone(api.bounds()),beforeOrbit=api.camera();canvas.onpointerdown({button:1,clientX:10,clientY:10,pointerId:1});canvas.onpointermove({clientX:40,clientY:25});canvas.onpointerup({clientX:40,clientY:25});assert.notDeepEqual(api.bounds().center,beforePan.center);assert.deepEqual(api.camera(),beforeOrbit);
const beforeRight=structuredClone(api.bounds());canvas.onpointerdown({button:2,clientX:10,clientY:10,pointerId:2});canvas.onpointermove({clientX:70,clientY:45});canvas.onpointerup({clientX:70,clientY:45});assert.deepEqual(api.bounds(),beforeRight);assert.deepEqual(api.camera(),beforeOrbit);
api.startPreviewCamera({width:256,height:256,fov:20,yaw:-45,pitch:30});const poseBefore=api.previewCameraPose();canvas.onpointerdown({button:2,clientX:0,clientY:0,pointerId:3});canvas.onpointermove({clientX:50,clientY:40});canvas.onpointerup({});assert.deepEqual(api.previewCameraPose(),poseBefore);canvas.onpointerdown({button:1,clientX:0,clientY:0,pointerId:4});canvas.onpointermove({clientX:50,clientY:40});canvas.onpointerup({});const poseAfter=api.previewCameraPose();assert.equal(poseBefore.Yaw,poseAfter.Yaw);assert.equal(poseBefore.Pitch,poseAfter.Pitch);assert.notEqual(poseBefore.X,poseAfter.X);api.stopPreviewCamera();
console.log('PASS incremental geometry keeps VAOs and textures; middle mouse pans and right mouse is inert in viewport and camera editor');

api.load({Materials:['rubber'],Nodes:[{Name:'tireShape',Positions:wheelPoints,Normals:Array(6).fill([0,0,1]).flat(),Uv:Array(12).fill(0),Indices:[0,1,2,3,4,5],Groups:[{Material:0,First:0,Count:6}]}]});api.resetView();api.gizmo({visible:false});api.selectionOptions({enabled:true,elements:false,items:[]});
const projects=[0,1,2].map(v=>api.gizmoProject(wheelPoints.slice(v*3,v*3+3))),box=[Math.min(...projects.map(p=>p[0])),Math.min(...projects.map(p=>p[1])),Math.max(...projects.map(p=>p[0])),Math.max(...projects.map(p=>p[1]))];
assert.equal(api.regionSelection(box,false).length,1);assert.equal(api.regionSelection(box,true).length,0,'the other disconnected wheel prevents complete-object containment');
api.selectionOptions({elements:true,editNode:0,parts:[{Id:0,Triangles:[0],Vertices:[0,1,2]},{Id:1,Triangles:[1],Vertices:[3,4,5]}],items:[]});assert.deepEqual(api.regionSelection(box,true),[{node:0,part:0}]);const clipped=[box[0]+1,box[1],box[2],box[3]];assert.deepEqual(api.regionSelection(clipped,false),[{node:0,part:0}]);assert.equal(api.regionSelection(clipped,true).length,0,'even one excluded pixel prevents full containment');
const centre=projects.reduce((s,p)=>s.map((v,i)=>v+p[i]/3),[0,0]);assert.deepEqual(api.clickSelection(centre),[{node:0,part:0}]);assert.deepEqual(api.regionSelection([centre[0]-.1,centre[1]-.1,centre[0]+.1,centre[1]+.1],false),[{node:0,part:0}],'crossing detects a rectangle within a face without a vertex in the rectangle');
const oldView=api.camera();canvas.onpointerdown({button:0,clientX:box[0]-1,clientY:box[1]-1,pointerId:20});canvas.onpointermove({clientX:box[2]+1,clientY:box[3]+1});canvas.onpointerup({clientX:box[2]+1,clientY:box[3]+1});assert.deepEqual(api.camera(),oldView);assert.deepEqual(api.selectionState().items,[{node:0,part:0}]);canvas.onpointerdown({button:0,altKey:true,clientX:0,clientY:0,pointerId:21});canvas.onpointermove({clientX:20,clientY:10});canvas.onpointerup({clientX:20,clientY:10});assert.notEqual(api.camera().yaw,oldView.yaw);api.resetView();
const va=api.allNodes()[0].vao;api.selectionOptions({enabled:false});api.playWheels(wheelParts.map(w=>({...w,Radius:.5})),20);wheelFrames.shift()(1000);wheelFrames.shift()(2000);const distance=api.wheelPlayback().distance;assert.ok(Math.abs(distance-20/3.6)<1e-9);assert.equal(api.allNodes()[0].vao,va);api.stopWheels(false);const stopped=api.allNodes()[0].Positions.slice();while(wheelFrames.length)wheelFrames.shift()(3000);assert.deepEqual(api.allNodes()[0].Positions,stopped);
api.lighting({lights:[{kind:'beacon',position:[0,0,0],phase:0},{kind:'beacon',position:[1,0,0],phase:Math.PI}],animate:true});effectFrames.shift()(0);const beacon=api.effectLights();assert.equal(beacon.length,2);assert.ok(beacon[0].strength>beacon[1].strength);api.lighting({lights:[{kind:'beacon',position:[0,0,0],animate:false}],animate:false});assert.equal(api.effectLights()[0].strength,1);
console.log('PASS component picking, pixel-exact window/crossing regions, LMB selection/Alt orbit, continuous 20 km/h wheels and steady-placement/phased beacon preview');

api.playWheels(wheelParts.map(w=>({...w,Radius:.5})),20);wheelFrames.shift()(4000);wheelFrames.shift()(5000);assert.ok(Math.abs(api.wheelPlayback().distance-40/3.6)<1e-9,'wheel play resumes distance without resetting angle');api.stopWheels(true);while(wheelFrames.length)wheelFrames.shift()(6000);assert.deepEqual(api.allNodes()[0].Positions,wheelPoints);
api.gizmo({visible:false,position:[4,1,4],rotation:[0,0,0],scale:[1,1,1],cargo:-1});api.meshPreview({revision:1,pivot:[3,1,4],items:[{node:0,vertices:[0,1,2]}]});assert.deepEqual(api.allNodes()[0].Positions.slice(9),wheelPoints.slice(9));assert.equal(api.allNodes()[0].Positions[0],wheelPoints[0]+1);assert.equal(api.allNodes()[0].vao,va);api.meshPreview(null);
console.log('PASS wheel pause/resume/reset and mesh-element transforms retain untouched geometry and the existing VAO');

api.selectionOptions({enabled:true});const shortcutMessages=messages.length;eventHandlers.get('keydown')({code:'KeyO',shiftKey:true,preventDefault(){}});assert.equal(messages.length,shortcutMessages+1);assert.equal(messages.at(-1).selectionToggle,true);eventHandlers.get('keydown')({code:'KeyO',shiftKey:true,ctrlKey:true,preventDefault(){}});assert.equal(messages.length,shortcutMessages+1);console.log('PASS Shift+O routes window/crossing toggle from the focused viewport and leaves other shortcuts alone');

api.selectionOptions({enabled:false,items:[]});api.gizmo({visible:false,position:[0,0,0],rotation:[0,0,0],scale:[1,1,1],cargo:-1});const beforeIdentity=vertexUploads;api.meshPreview({revision:99,pivot:[0,0,0],items:[{node:0,vertices:[0,1,2]}]});api.meshPreview({revision:99,pivot:[0,0,0],items:[{node:0,vertices:[0,1,2]}]});assert.equal(vertexUploads,beforeIdentity,'choosing a component and repeated UI echoes must not upload geometry');console.log('PASS selection-only acknowledgements do not re-upload vertex buffers');
