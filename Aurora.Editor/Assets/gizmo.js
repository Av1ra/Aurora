// The editor owns the tool state. This overlay never changes model geometry.
let gizmoState={visible:false,position:[0,0,0],rotation:[0,0,0],mode:'move',editing:false,scale:[1,1,1],cargo:-1,uniformScale:true},gizmoDrag=null,gizmoSegments=[];
const gizmoCanvas=document.createElement?document.createElement('canvas'):null;
let gizmoContext=null;
if(gizmoCanvas){gizmoCanvas.style.cssText='position:fixed;inset:0;width:100%;height:100%;pointer-events:none;background:transparent';document.body.appendChild(gizmoCanvas);gizmoContext=gizmoCanvas.getContext('2d');}
const gv={add:(a,b)=>a.map((x,i)=>x+b[i]),sub:(a,b)=>a.map((x,i)=>x-b[i]),mul:(a,s)=>a.map(x=>x*s),dot:(a,b)=>a.reduce((s,x,i)=>s+x*b[i],0)};
function gizmoRotate(v,r){let p=[...v];for(let a=0;a<3;a++){let b=(a+1)%3,c=(a+2)%3,t=r[a]*Math.PI/180,u=p[b],w=p[c];p[b]=u*Math.cos(t)-w*Math.sin(t);p[c]=u*Math.sin(t)+w*Math.cos(t);}return p;}
function cameraRotate(p,inverse=false){let x=p[0],y=p[1],z=p[2];if(inverse){let y1=Math.cos(pitch)*y+Math.sin(pitch)*z,z1=-Math.sin(pitch)*y+Math.cos(pitch)*z;return[Math.cos(yaw)*x-Math.sin(yaw)*z1,y1,Math.sin(yaw)*x+Math.cos(yaw)*z1];}let x1=Math.cos(yaw)*x+Math.sin(yaw)*z,z1=-Math.sin(yaw)*x+Math.cos(yaw)*z;return[x1,Math.cos(pitch)*y-Math.sin(pitch)*z1,Math.sin(pitch)*y+Math.cos(pitch)*z1];}
function gizmoProject(point){let p=cameraRotate(gv.mul(gv.sub(point,scene.center),1/scene.extent));let depth=distance-p[2];if(depth<.05)return null;return[canvas.clientWidth*(.5+p[0]*focal/(canvas.clientWidth/canvas.clientHeight)/depth/2),canvas.clientHeight*(.5-p[1]*focal/depth/2)];}
function transformCargo(){
 if(gizmoState.cargo<0)return;const {position,rotation,scale}=gizmoState;
 for(const n of nodes)if(n.Cargo&&n.CargoSlot===gizmoState.cargo&&n.LocalPositions){
  const positions=[],normals=[];for(let i=0;i<n.LocalPositions.length;i+=3){const p=gizmoRotate(n.LocalPositions.slice(i,i+3).map((v,a)=>v*scale[a]),rotation),normal=gizmoRotate(n.LocalNormals.slice(i,i+3).map((v,a)=>v/Math.max(scale[a],1e-8)),rotation),length=Math.hypot(...normal)||1;positions.push(...gv.add(p,position));normals.push(...normal.map(v=>v/length));}n.Positions=positions;n.Normals=normals;
  for(const [index,values]of [[0,positions],[1,normals]]){gl.bindBuffer(gl.ARRAY_BUFFER,n.buffers[index]);gl.bufferData(gl.ARRAY_BUFFER,new Float32Array(values),gl.STATIC_DRAW);}
 }
}
function gizmo(value){gizmoState={...gizmoState,...value};transformCargo();draw();}
function drawGizmo(){
 if(!gizmoContext)return;const c=gizmoContext,scale=Math.min(2,window.devicePixelRatio||1);gizmoCanvas.width=Math.max(1,Math.round(canvas.clientWidth*scale));gizmoCanvas.height=Math.max(1,Math.round(canvas.clientHeight*scale));c.scale(scale,scale);gizmoSegments=[];
 if(!scene||!gizmoState.visible)return;const position=gizmoState.position,origin=gizmoProject(position);if(!origin)return;
 const size=scene.extent*.26,axes=[[1,0,0],[0,1,0],[0,0,1]],colours=['#e65151','#64c56c','#5292ee'];
 function line(a,b,axis,angle){if(!a||!b)return;c.beginPath();c.moveTo(...a);c.lineTo(...b);c.stroke();if(gizmoState.editing)gizmoSegments.push({a,b,axis,angle});}
 axes.forEach((axis,i)=>{
  c.strokeStyle=colours[i];c.fillStyle=colours[i];c.lineWidth=2;
  const dir=gizmoRotate(axis,gizmoState.rotation),end=gizmoProject(gv.add(position,gv.mul(dir,size)));line(origin,end,i,0);
  if(end){if(gizmoState.editing&&gizmoState.mode==='scale')c.fillRect(end[0]-4,end[1]-4,8,8);c.font='12px Segoe UI';c.fillText('XYZ'[i],end[0]+5,end[1]-5);if(gizmoState.editing&&gizmoState.mode==='move'){let angle=Math.atan2(end[1]-origin[1],end[0]-origin[0]);c.beginPath();c.moveTo(...end);c.lineTo(end[0]-10*Math.cos(angle-.4),end[1]-10*Math.sin(angle-.4));c.lineTo(end[0]-10*Math.cos(angle+.4),end[1]-10*Math.sin(angle+.4));c.closePath();c.fill();}}
  if(gizmoState.editing&&gizmoState.mode==='rotate'){
   const u=gizmoRotate(axes[(i+1)%3],gizmoState.rotation),v=gizmoRotate(axes[(i+2)%3],gizmoState.rotation);let previous=null;
   for(let j=0;j<=96;j++){let angle=j*Math.PI/48,p=gizmoProject(gv.add(position,gv.mul(gv.add(gv.mul(u,Math.cos(angle)),gv.mul(v,Math.sin(angle))),size)));if(previous)line(previous,p,i,angle);previous=p;}
  }
 });c.fillStyle='#ddd';c.fillRect(origin[0]-4,origin[1]-4,8,8);if(gizmoState.editing&&gizmoState.mode==='scale'&&gizmoState.uniformScale)gizmoSegments.push({a:origin,b:origin,axis:3,angle:0});
}
function gizmoPointer(e){const r=canvas.getBoundingClientRect?canvas.getBoundingClientRect():{left:0,top:0};return[e.clientX-r.left,e.clientY-r.top];}
function gizmoRingAngle(mouse,axis){
 const ndc=[(mouse[0]/canvas.clientWidth*2-1)*(canvas.clientWidth/canvas.clientHeight)/focal,(1-mouse[1]/canvas.clientHeight*2)/focal,-1];
 const origin=gv.add(scene.center,gv.mul(cameraRotate([0,0,distance],true),scene.extent)),ray=cameraRotate(ndc,true),normal=gizmoRotate([[1,0,0],[0,1,0],[0,0,1]][axis],gizmoState.rotation);let denom=gv.dot(ray,normal);if(Math.abs(denom)<1e-7)return null;
 let t=gv.dot(gv.sub(gizmoState.position,origin),normal)/denom;if(t<0)return null;let p=gv.sub(gv.add(origin,gv.mul(ray,t)),gizmoState.position),axes=[[1,0,0],[0,1,0],[0,0,1]];return Math.atan2(gv.dot(p,gizmoRotate(axes[(axis+2)%3],gizmoState.rotation)),gv.dot(p,gizmoRotate(axes[(axis+1)%3],gizmoState.rotation)));
}
const orbitDown=canvas.onpointerdown,orbitMove=canvas.onpointermove,orbitUp=canvas.onpointerup,orbitCancel=canvas.onpointercancel;
canvas.onpointerdown=e=>{
 if(!scene||!gizmoState.visible||!gizmoState.editing){orbitDown(e);return;}
 const mouse=gizmoPointer(e);let best=null,min=9;
 for(const segment of gizmoSegments){let d=gv.sub(segment.b,segment.a),length=gv.dot(d,d),t=length?Math.max(0,Math.min(1,gv.dot(gv.sub(mouse,segment.a),d)/length)):0,p=gv.add(segment.a,gv.mul(d,t)),error=Math.hypot(...gv.sub(mouse,p));if(error<min){min=error;best=segment;}}
 if(!best){orbitDown(e);return;}e.preventDefault?.();canvas.setPointerCapture(e.pointerId);gizmoDrag={axis:best.axis,mouse,position:[...gizmoState.position],rotation:[...gizmoState.rotation],scale:[...gizmoState.scale],angle:gizmoState.mode==='rotate'?gizmoRingAngle(mouse,best.axis):null};window.chrome.webview.postMessage({gizmo:{position:gizmoState.position,rotation:gizmoState.rotation,scale:gizmoState.scale,phase:"begin"}});
};
canvas.onpointermove=e=>{
 if(!gizmoDrag){orbitMove(e);drawGizmo();return;}const d=gizmoDrag,mouse=gizmoPointer(e);
 if(gizmoState.mode==='move'){
  let dir=gizmoRotate([[1,0,0],[0,1,0],[0,0,1]][d.axis],d.rotation),a=gizmoProject(d.position),b=gizmoProject(gv.add(d.position,dir));if(!a||!b)return;let delta=gv.sub(b,a),length=gv.dot(delta,delta);if(length>1e-5)gizmoState.position=gv.add(d.position,gv.mul(dir,gv.dot(gv.sub(mouse,d.mouse),delta)/length));
 }else if(gizmoState.mode==='scale'){
  let amount=(d.mouse[1]-mouse[1])/80;if(d.axis<3){const dir=gizmoRotate([[1,0,0],[0,1,0],[0,0,1]][d.axis],d.rotation),a=gizmoProject(d.position),b=gizmoProject(gv.add(d.position,dir));if(!a||!b)return;const delta=gv.sub(b,a),length=Math.hypot(...delta);if(length<1e-5)return;amount=gv.dot(gv.sub(mouse,d.mouse),delta)/length/80;}
  const factor=Math.exp(Math.max(-12,Math.min(12,amount)));gizmoState.scale=d.scale.map((v,i)=>gizmoState.uniformScale||i===d.axis?Math.min(1e6,Math.max(1e-5,v>0?v*factor:factor-1)):v);
 }else{let angle=gizmoRingAngle(mouse,d.axis);if(angle!=null&&d.angle!=null){let diff=angle-d.angle;while(diff>Math.PI)diff-=2*Math.PI;while(diff< -Math.PI)diff+=2*Math.PI;gizmoState.rotation=[...d.rotation];gizmoState.rotation[d.axis]+=diff*180/Math.PI;}}
 transformCargo();draw();window.chrome.webview.postMessage({gizmo:{position:gizmoState.position,rotation:gizmoState.rotation,scale:gizmoState.scale,phase:"update"}});
};
canvas.onpointerup=e=>{if(gizmoDrag){window.chrome.webview.postMessage({gizmo:{position:gizmoState.position,rotation:gizmoState.rotation,scale:gizmoState.scale,phase:"end"}});gizmoDrag=null;return;}orbitUp(e);drawGizmo();};canvas.onpointercancel=e=>{if(gizmoDrag)window.chrome.webview.postMessage({gizmo:{position:gizmoState.position,rotation:gizmoState.rotation,scale:gizmoState.scale,phase:"end"}});gizmoDrag=null;orbitCancel(e);};
const normalDraw=draw;draw=function(picking=false){normalDraw(picking);if(!picking)drawGizmo();};
window.addEventListener?.('keydown',e=>{if(!(e.ctrlKey||e.metaKey)||e.altKey||!['KeyZ','KeyY'].includes(e.code))return;e.preventDefault();window.chrome.webview.postMessage({history:e.code==='KeyY'||e.shiftKey?'redo':'undo'});});
