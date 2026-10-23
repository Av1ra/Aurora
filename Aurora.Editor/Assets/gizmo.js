// The editor owns the tool state. This overlay never changes model geometry.
let gizmoState={visible:false,position:[0,0,0],rotation:[0,0,0],mode:'move',editing:false,scale:[1,1,1],cargo:-1,uniformScale:true},gizmoDrag=null,gizmoSegments=[];
const gizmoCanvas=document.createElement?document.createElement('canvas'):null;
let gizmoContext=null;
if(gizmoCanvas){gizmoCanvas.style.cssText='position:fixed;inset:0;width:100%;height:100%;pointer-events:none;background:transparent';document.body.appendChild(gizmoCanvas);gizmoContext=gizmoCanvas.getContext('2d');}
const gv={add:(a,b)=>a.map((x,i)=>x+b[i]),sub:(a,b)=>a.map((x,i)=>x-b[i]),mul:(a,s)=>a.map(x=>x*s),dot:(a,b)=>a.reduce((s,x,i)=>s+x*b[i],0)};
function gizmoRotate(v,r){let p=[...v];for(let a=0;a<3;a++){let b=(a+1)%3,c=(a+2)%3,t=r[a]*Math.PI/180,u=p[b],w=p[c];p[b]=u*Math.cos(t)-w*Math.sin(t);p[c]=u*Math.sin(t)+w*Math.cos(t);}return p;}
function gizmoOrientation(v){return gizmoRotate(gizmoRotate(v,gizmoState.rotation),gizmoState.basis||[0,0,0]);}
function animationUnrotate(v,r){let p=v.slice();for(let a=2;a>=0;a--){let b=(a+1)%3,c=(a+2)%3,t=-r[a]*Math.PI/180,u=p[b],w=p[c];p[b]=u*Math.cos(t)-w*Math.sin(t);p[c]=u*Math.sin(t)+w*Math.cos(t);}return p;}
function cameraRotate(p,inverse=false){let x=p[0],y=p[1],z=p[2];if(inverse){let x0=Math.cos(roll)*x+Math.sin(roll)*y,y0=-Math.sin(roll)*x+Math.cos(roll)*y;x=x0;y=y0;let y1=Math.cos(pitch)*y+Math.sin(pitch)*z,z1=-Math.sin(pitch)*y+Math.cos(pitch)*z;return[Math.cos(yaw)*x-Math.sin(yaw)*z1,y1,Math.sin(yaw)*x+Math.cos(yaw)*z1];}let x1=Math.cos(yaw)*x+Math.sin(yaw)*z,z1=-Math.sin(yaw)*x+Math.cos(yaw)*z,y1=Math.cos(pitch)*y-Math.sin(pitch)*z1;return[Math.cos(roll)*x1-Math.sin(roll)*y1,Math.sin(roll)*x1+Math.cos(roll)*y1,Math.sin(pitch)*y+Math.cos(pitch)*z1];}
function gizmoProject(point){let p=cameraRotate(gv.mul(gv.sub(point,scene.center),1/scene.extent));let depth=distance-p[2];if(depth<.05)return null;return[canvas.clientWidth*(.5+p[0]*focal/(canvas.clientWidth/canvas.clientHeight)/depth/2),canvas.clientHeight*(.5-p[1]*focal/depth/2)];}
function transformCargo(){
 if(gizmoState.cargo<0)return;const {position,rotation,scale}=gizmoState;
 for(const n of nodes)if(n.Cargo&&n.CargoSlot===gizmoState.cargo&&n.LocalPositions){
  const positions=[],normals=[];for(let i=0;i<n.LocalPositions.length;i+=3){const p=gizmoRotate(n.LocalPositions.slice(i,i+3).map((v,a)=>v*scale[a]),rotation),normal=gizmoRotate(n.LocalNormals.slice(i,i+3).map((v,a)=>v/Math.max(scale[a],1e-8)),rotation),length=Math.hypot(...normal)||1;positions.push(...gv.add(p,position));normals.push(...normal.map(v=>v/length));}n.Positions=positions;n.Normals=normals;n.poseBase={p:positions.slice(),n:normals.slice()};
  for(const [index,values]of [[0,positions],[1,normals]]){gl.bindBuffer(gl.ARRAY_BUFFER,n.buffers[index]);gl.bufferData(gl.ARRAY_BUFFER,new Float32Array(values),gl.STATIC_DRAW);}
 }
}
function gizmo(value){
 // C# may echo a previous pose while pointer events are still in flight.
 // Keep the local drag transform authoritative until pointerup.
 if(gizmoDrag){gizmoState={...gizmoState,axis:value.axis,rotationStep:value.rotationStep};return;}
 gizmoState={...gizmoState,...value};transformCargo();draw();
}
function drawGizmo(){
 if(!gizmoContext)return;const c=gizmoContext,scale=Math.min(2,window.devicePixelRatio||1);gizmoCanvas.width=Math.max(1,Math.round(canvas.clientWidth*scale));gizmoCanvas.height=Math.max(1,Math.round(canvas.clientHeight*scale));c.scale(scale,scale);gizmoSegments=[];
 drawPreviewCameraFrame(c);
 if(!scene||!gizmoState.visible)return;const position=gizmoState.position,origin=gizmoProject(position);if(!origin)return;
 if(gizmoState.axis){const a=gizmoProject(gizmoState.axis[0]),b=gizmoProject(gizmoState.axis[1]);if(a&&b){c.save();c.strokeStyle='#e5c967';c.fillStyle='#e5c967';c.lineWidth=2;c.beginPath();c.moveTo(...a);c.lineTo(...b);c.stroke();for(const [label,p]of [['A',a],['B',b]]){c.beginPath();c.arc(...p,3,0,Math.PI*2);c.fill();c.fillText(label,p[0]+6,p[1]-6);}c.restore();}}
 const size=scene.extent*.26,axes=[[1,0,0],[0,1,0],[0,0,1]],colours=['#e65151','#64c56c','#5292ee'];
 function line(a,b,axis,angle){if(!a||!b)return;c.beginPath();c.moveTo(...a);c.lineTo(...b);c.stroke();if(gizmoState.editing)gizmoSegments.push({a,b,axis,angle});}
 axes.forEach((axis,i)=>{
  c.strokeStyle=colours[i];c.fillStyle=colours[i];c.lineWidth=2;
  const dir=gizmoOrientation(axis),end=gizmoProject(gv.add(position,gv.mul(dir,size)));line(origin,end,i,0);
  if(end){if(gizmoState.editing&&gizmoState.mode==='scale')c.fillRect(end[0]-4,end[1]-4,8,8);c.font='12px Segoe UI';c.fillText('XYZ'[i],end[0]+5,end[1]-5);if(gizmoState.editing&&gizmoState.mode==='move'){let angle=Math.atan2(end[1]-origin[1],end[0]-origin[0]);c.beginPath();c.moveTo(...end);c.lineTo(end[0]-10*Math.cos(angle-.4),end[1]-10*Math.sin(angle-.4));c.lineTo(end[0]-10*Math.cos(angle+.4),end[1]-10*Math.sin(angle+.4));c.closePath();c.fill();}}
  if(gizmoState.editing&&gizmoState.mode==='rotate'){
   const u=gizmoOrientation(axes[(i+1)%3]),v=gizmoOrientation(axes[(i+2)%3]);let previous=null;
   for(let j=0;j<=96;j++){let angle=j*Math.PI/48,p=gizmoProject(gv.add(position,gv.mul(gv.add(gv.mul(u,Math.cos(angle)),gv.mul(v,Math.sin(angle))),size)));if(previous)line(previous,p,i,angle);previous=p;}
  }
 });c.fillStyle='#ddd';c.fillRect(origin[0]-4,origin[1]-4,8,8);if(gizmoState.editing&&gizmoState.mode==='scale'&&gizmoState.uniformScale)gizmoSegments.push({a:origin,b:origin,axis:3,angle:0});
}
function gizmoPointer(e){const r=canvas.getBoundingClientRect?canvas.getBoundingClientRect():{left:0,top:0};return[e.clientX-r.left,e.clientY-r.top];}
function gizmoRingAngle(mouse,axis){
 const ndc=[(mouse[0]/canvas.clientWidth*2-1)*(canvas.clientWidth/canvas.clientHeight)/focal,(1-mouse[1]/canvas.clientHeight*2)/focal,-1];
 const origin=gv.add(scene.center,gv.mul(cameraRotate([0,0,distance],true),scene.extent)),ray=cameraRotate(ndc,true),normal=gizmoOrientation([[1,0,0],[0,1,0],[0,0,1]][axis]);let denom=gv.dot(ray,normal);if(Math.abs(denom)<1e-7)return null;
 let t=gv.dot(gv.sub(gizmoState.position,origin),normal)/denom;if(t<0)return null;let p=gv.sub(gv.add(origin,gv.mul(ray,t)),gizmoState.position),axes=[[1,0,0],[0,1,0],[0,0,1]];return Math.atan2(gv.dot(p,gizmoOrientation(axes[(axis+2)%3])),gv.dot(p,gizmoOrientation(axes[(axis+1)%3])));
}
const orbitDown=canvas.onpointerdown,orbitMove=canvas.onpointermove,orbitUp=canvas.onpointerup,orbitCancel=canvas.onpointercancel;
canvas.onpointerdown=e=>{
 if((e.button??0)!==0){orbitDown(e);return;}
 if(!scene||!gizmoState.visible||!gizmoState.editing){orbitDown(e);return;}
 const mouse=gizmoPointer(e);let best=null,min=9;
 for(const segment of gizmoSegments){let d=gv.sub(segment.b,segment.a),length=gv.dot(d,d),t=length?Math.max(0,Math.min(1,gv.dot(gv.sub(mouse,segment.a),d)/length)):0,p=gv.add(segment.a,gv.mul(d,t)),error=Math.hypot(...gv.sub(mouse,p));if(error<min){min=error;best=segment;}}
 if(!best){orbitDown(e);return;}stopAnimation();e.preventDefault?.();canvas.setPointerCapture(e.pointerId);gizmoDrag={axis:best.axis,mouse,position:[...gizmoState.position],rotation:[...gizmoState.rotation],scale:[...gizmoState.scale],angle:gizmoState.mode==='rotate'?gizmoRingAngle(mouse,best.axis):null,rotationAccum:0,lastAngle:null};gizmoDrag.lastAngle=gizmoDrag.angle;window.chrome.webview.postMessage({gizmo:{position:gizmoState.position,rotation:gizmoState.rotation,scale:gizmoState.scale,phase:"begin"}});
};
canvas.onpointermove=e=>{
 if(!gizmoDrag){orbitMove(e);drawGizmo();return;}const d=gizmoDrag,mouse=gizmoPointer(e);
 if(gizmoState.mode==='move'){
  let dir=gizmoRotate([[1,0,0],[0,1,0],[0,0,1]][d.axis],d.rotation),a=gizmoProject(d.position),b=gizmoProject(gv.add(d.position,dir));if(!a||!b)return;let delta=gv.sub(b,a),length=gv.dot(delta,delta);if(length>1e-5)gizmoState.position=gv.add(d.position,gv.mul(dir,gv.dot(gv.sub(mouse,d.mouse),delta)/length));
 }else if(gizmoState.mode==='scale'){
  let amount=(d.mouse[1]-mouse[1])/80;if(d.axis<3){const dir=gizmoRotate([[1,0,0],[0,1,0],[0,0,1]][d.axis],d.rotation),a=gizmoProject(d.position),b=gizmoProject(gv.add(d.position,dir));if(!a||!b)return;const delta=gv.sub(b,a),length=Math.hypot(...delta);if(length<1e-5)return;amount=gv.dot(gv.sub(mouse,d.mouse),delta)/length/80;}
  const factor=Math.exp(Math.max(-12,Math.min(12,amount)));gizmoState.scale=d.scale.map((v,i)=>gizmoState.uniformScale||i===d.axis?Math.min(1e6,Math.max(1e-5,v>0?v*factor:factor-1)):v);
 }else{let angle=gizmoRingAngle(mouse,d.axis);if(angle!=null&&d.angle!=null){let diff=angle-(d.lastAngle??angle);while(diff>Math.PI)diff-=2*Math.PI;while(diff< -Math.PI)diff+=2*Math.PI;d.rotationAccum+=diff;d.lastAngle=angle;gizmoState.rotation=[...d.rotation];gizmoState.rotation[d.axis]+=d.rotationAccum*180/Math.PI;const step=gizmoState.rotationStep||.1;gizmoState.rotation[d.axis]=Math.round(gizmoState.rotation[d.axis]/step)*step;}}
 transformCargo();transformMeshPreview();draw();window.chrome.webview.postMessage({gizmo:{position:gizmoState.position,rotation:gizmoState.rotation,scale:gizmoState.scale,phase:"update"}});
};
canvas.onpointerup=e=>{if(gizmoDrag){window.chrome.webview.postMessage({gizmo:{position:gizmoState.position,rotation:gizmoState.rotation,scale:gizmoState.scale,phase:"end"}});gizmoDrag=null;return;}orbitUp(e);drawGizmo();};canvas.onpointercancel=e=>{if(gizmoDrag)window.chrome.webview.postMessage({gizmo:{position:gizmoState.position,rotation:gizmoState.rotation,scale:gizmoState.scale,phase:"end"}});gizmoDrag=null;orbitCancel(e);};
const normalDraw=draw;draw=function(picking=false){normalDraw(picking);if(!picking){drawLampSprites();drawGizmo();}};
window.addEventListener?.('keydown',e=>{if(!(e.ctrlKey||e.metaKey)||e.altKey||!['KeyZ','KeyY'].includes(e.code))return;e.preventDefault();window.chrome.webview.postMessage({history:e.code==='KeyY'||e.shiftKey?'redo':'undo'});});

// Geometry-only updates retain the vehicle buffers, camera, materials and textures.
function uploadNode(n){for(const [index,values]of [[0,n.Positions],[1,n.Normals]]){gl.bindBuffer(gl.ARRAY_BUFFER,n.buffers[index]);gl.bufferData(gl.ARRAY_BUFFER,new Float32Array(values),gl.DYNAMIC_DRAW||gl.STATIC_DRAW);}}
function replaceCargo(s){
 for(const n of nodes.filter(n=>n.Cargo)){gl.deleteVertexArray(n.vao);for(const b of n.buffers)gl.deleteBuffer(b);}
 nodes=nodes.filter(n=>!n.Cargo);for(const key of textureRequests.keys())if(key.includes('__cargo__'))textureRequests.set(key,textureRequests.get(key)+1);
 for(const key of Object.keys(materials))if(key.startsWith('__cargo__')){for(const v of Object.values(materials[key]))if(v instanceof WebGLTexture)gl.deleteTexture(v);delete materials[key];}
 if(s.CargoOnly){scene.Materials=scene.Materials.filter(m=>!m.startsWith("__cargo__"));const first=scene.Materials.length;scene.Materials.push(...s.Materials);s={...s,Nodes:s.Nodes.map(n=>({...n,Groups:n.Groups.map(g=>({...g,Material:g.Material+first}))}))};}else scene.Materials=s.Materials;for(const node of s.Nodes.filter(n=>n.Cargo)){const vao=gl.createVertexArray();gl.bindVertexArray(vao);const buffers=[buffer(node.Positions,3,'position'),buffer(node.Normals,3,'normal'),buffer(node.Uv,2,'uv')],ib=gl.createBuffer();buffers.push(ib);gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,ib);gl.bufferData(gl.ELEMENT_ARRAY_BUFFER,new Uint16Array(node.Indices),gl.STATIC_DRAW);nodes.push({...node,vao,buffers,index:nodes.length});}draw();
}
function poseMechanism(value){
 for(const n of nodes){if(!n.poseBase)n.poseBase={p:n.Positions.slice(),n:n.Normals.slice()};if(!value||n.Name!==value.mesh&&!(value.cargo&&n.Cargo)){if(n.poseChanged){n.Positions=n.poseBase.p.slice();n.Normals=n.poseBase.n.slice();n.poseChanged=false;uploadNode(n);}continue;}
 const axis=gv.sub(value.end,value.start),len=Math.hypot(...axis);if(len<1e-8)continue;const u=gv.mul(axis,1/len),angle=value.angle*Math.PI/180,c=Math.cos(angle),s=Math.sin(angle);
 const rotate=v=>{const cross=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]],d=gv.dot(u,v);return v.map((x,i)=>x*c+cross[i]*s+u[i]*d*(1-c));};n.Positions=[];n.Normals=[];
 for(let i=0;i<n.poseBase.p.length;i+=3){n.Positions.push(...gv.add(rotate(gv.sub(n.poseBase.p.slice(i,i+3),value.start)),value.start));n.Normals.push(...rotate(n.poseBase.n.slice(i,i+3)));}n.poseChanged=true;uploadNode(n);
 }draw();
}

let animationPlayback=null;
function sampleAnimation(track,frame){const keys=track._sortedKeys||(track._sortedKeys=track.Keys.slice().sort((a,b)=>a.Frame-b.Frame));if(frame<=keys[0].Frame)return keys[0].Pose;if(frame>=keys.at(-1).Frame)return keys.at(-1).Pose;const index=keys.findIndex(k=>k.Frame>=frame),a=keys[index-1],b=keys[index];let t=(frame-a.Frame)/(b.Frame-a.Frame);if(track.Interpolation==='Smooth')t=t*t*(3-2*t);const pose={};for(const key of ['Position','Rotation','Scale'])pose[key]=a.Pose[key].map((v,i)=>v+(b.Pose[key][i]-v)*t);return pose;}
function previewAnimation(clip,frame,overrideTrack=-1,overridePose=null){
 for(const n of nodes){if(!n.poseBase)n.poseBase={p:n.Positions.slice(),n:n.Normals.slice()};const index=clip.Tracks.findIndex(t=>t.Mesh===n.Name||n.Cargo&&t.CargoParent===t.Mesh);if(index<0)continue;const track=clip.Tracks[index],pose=index===overrideTrack&&overridePose?overridePose:sampleAnimation(track,frame),p=[],normals=[];
 for(let i=0;i<n.poseBase.p.length;i+=3){const axis=track.AxisRotation||[0,0,0],v=gizmoRotate(gizmoRotate(animationUnrotate(gv.sub(n.poseBase.p.slice(i,i+3),track.Pivot),axis).map((x,a)=>x*pose.Scale[a]),pose.Rotation),axis),normal=gizmoRotate(gizmoRotate(animationUnrotate(n.poseBase.n.slice(i,i+3),axis).map((x,a)=>x/pose.Scale[a]),pose.Rotation),axis),len=Math.hypot(...normal)||1;p.push(...gv.add(gv.add(v,track.Pivot),pose.Position));normals.push(...normal.map(x=>x/len));}n.Positions=p;n.Normals=normals;n.poseChanged=true;uploadNode(n);
 }draw();
}
function stopAnimation(){animationPlayback=null;}
function playAnimation(clip,startFrame=0){stopAnimation();const state={clip,time:null,lastPost:-Infinity};animationPlayback=state;requestAnimationFrame(function animationFrameTick(t){if(animationPlayback!==state)return;if(state.time===null)state.time=t;const frame=(startFrame+(t-state.time)/1000*clip.Fps)%(clip.LastFrame+1);previewAnimation(clip,frame);if(t-state.lastPost>=100){state.lastPost=t;window.chrome.webview.postMessage({animationFrame:frame});}requestAnimationFrame(animationFrameTick);});}

function rotateDiagnostic(v,axis,degrees){const length=Math.hypot(...axis);if(length<1e-8)return v;const u=gv.mul(axis,1/length),a=degrees*Math.PI/180,c=Math.cos(a),s=Math.sin(a),dot=gv.dot(u,v),cross=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]];return v.map((x,i)=>x*c+cross[i]*s+u[i]*dot*(1-c));}
function poseWheels(wheels,steering,spin,selectedWheel=null){for(const n of nodes.filter(n=>!n.Part&&!n.Cargo&&wheels.some(w=>w.Mesh===n.Name))){if(!n.poseBase)n.poseBase={p:n.Positions.slice(),n:n.Normals.slice()};n.Positions=n.poseBase.p.slice();n.Normals=n.poseBase.n.slice();for(const wheel of wheels.filter(w=>w.Mesh===n.Name&&(selectedWheel===null||w.Mesh+'/'+(w.Component||0)===selectedWheel))){const transform=v=>rotateDiagnostic(rotateDiagnostic(v,wheel.Axle,wheel.Spin??spin),wheel.SteeringAxis,wheel.Steering?steering:0);for(const vertex of wheel.Vertices||Array.from({length:n.poseBase.p.length/3},(_,i)=>i)){const i=vertex*3;const p=gv.add(transform(gv.sub(n.poseBase.p.slice(i,i+3),wheel.Pivot)),wheel.Pivot),normal=transform(n.poseBase.n.slice(i,i+3));for(let k=0;k<3;k++){n.Positions[i+k]=p[k];n.Normals[i+k]=normal[k];}}}uploadNode(n);}draw();}

// Reconcile edits in place: retain camera, textures and unchanged VAOs.
let sceneDocument=null;
function updateScene(next,document){
 if(!scene||sceneDocument!==document){load(next);sceneDocument=document;return;}
 const equal=(a,b)=>a.length===b.length&&a.every((v,i)=>v===b[i]);
 const remaining=new Set(nodes),result=[];
 for(let i=0;i<next.Nodes.length;i++){
  const data=next.Nodes[i];let node=nodes.find(n=>remaining.has(n)&&n.Name===data.Name&&!!n.Cargo===!!data.Cargo&&!!n.Part===!!data.Part);
  if(node&&equal(node.Indices,data.Indices)&&equal(node.Uv,data.Uv)){
   remaining.delete(node);const moved=!equal(node.Positions,data.Positions)||!equal(node.Normals,data.Normals);
   Object.assign(node,data,{index:i,glass:false});if(moved){delete node.poseBase;node.poseChanged=false;uploadNode(node);}result.push(node);continue;
  }
  const vao=gl.createVertexArray();gl.bindVertexArray(vao);
  const buffers=[buffer(data.Positions,3,'position'),buffer(data.Normals,3,'normal'),buffer(data.Uv,2,'uv')],ib=gl.createBuffer();buffers.push(ib);
  gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,ib);gl.bufferData(gl.ELEMENT_ARRAY_BUFFER,new Uint16Array(data.Indices),gl.STATIC_DRAW);result.push({...data,index:i,vao,buffers});
 }
 for(const node of remaining){gl.deleteVertexArray(node.vao);for(const b of node.buffers)gl.deleteBuffer(b);}
 next.center=scene.center;next.extent=scene.extent;scene=next;nodes=result;if(selected>=nodes.length)selected=-1;draw();
}

let wheelPlayback=null,wheelPlaybackGeneration=0;
let wheelTravelDistance=0,lastWheelSet=null,wheelSteeringAngle=0,wheelSteeringSelection=null;
function currentWheelPose(wheels,distance){return wheels.map(w=>({...w,Spin:distance/Math.max(.001,w.Radius||.5)*180/Math.PI%360,Steering:w.Steering&&(wheelSteeringSelection===null||w.Mesh+'/'+(w.Component||0)===wheelSteeringSelection)}));}
function setWheelSteering(wheels,angle,selected=null){wheelSteeringAngle=Math.max(-45,Math.min(45,angle));wheelSteeringSelection=selected;lastWheelSet=wheels;poseWheels(currentWheelPose(wheels,wheelPlayback?.distance??wheelTravelDistance),wheelSteeringAngle,0);}
function playWheels(wheels,kph=20){stopWheels(false);lastWheelSet=wheels;const generation=++wheelPlaybackGeneration;wheelPlayback={wheels,start:null,generation,baseDistance:wheelTravelDistance};requestAnimationFrame(function wheelFrameTick(t){const state=wheelPlayback;if(!state||state.generation!==generation)return;if(state.start===null)state.start=t;const distance=state.baseDistance+(t-state.start)/1000*kph/3.6;state.distance=distance;poseWheels(currentWheelPose(state.wheels,distance),wheelSteeringAngle,0);requestAnimationFrame(wheelFrameTick);});}
function stopWheels(reset=false){const old=wheelPlayback;wheelPlayback=null;wheelPlaybackGeneration++;if(old)wheelTravelDistance=old.distance??old.baseDistance??0;if(reset){wheelTravelDistance=0;wheelSteeringAngle=0;wheelSteeringSelection=null;}if(reset&&lastWheelSet){poseWheels(lastWheelSet,0,0);lastWheelSet=null;}}
