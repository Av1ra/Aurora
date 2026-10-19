// The editor owns the tool state. This overlay never changes model geometry.
let gizmoState={visible:false,position:[0,0,0],rotation:[0,0,0],mode:'move',editing:false},gizmoDrag=null,gizmoSegments=[];
const gizmoCanvas=document.createElement?document.createElement('canvas'):null;
let gizmoContext=null;
if(gizmoCanvas){gizmoCanvas.style.cssText='position:fixed;inset:0;width:100%;height:100%;pointer-events:none;background:transparent';document.body.appendChild(gizmoCanvas);gizmoContext=gizmoCanvas.getContext('2d');}
const gv={add:(a,b)=>a.map((x,i)=>x+b[i]),sub:(a,b)=>a.map((x,i)=>x-b[i]),mul:(a,s)=>a.map(x=>x*s),dot:(a,b)=>a.reduce((s,x,i)=>s+x*b[i],0)};
function gizmoRotate(v,r){let p=[...v];for(let a=0;a<3;a++){let b=(a+1)%3,c=(a+2)%3,t=r[a]*Math.PI/180,u=p[b],w=p[c];p[b]=u*Math.cos(t)-w*Math.sin(t);p[c]=u*Math.sin(t)+w*Math.cos(t);}return p;}
function cameraRotate(p,inverse=false){let x=p[0],y=p[1],z=p[2];if(inverse){let y1=Math.cos(pitch)*y+Math.sin(pitch)*z,z1=-Math.sin(pitch)*y+Math.cos(pitch)*z;return[Math.cos(yaw)*x-Math.sin(yaw)*z1,y1,Math.sin(yaw)*x+Math.cos(yaw)*z1];}let x1=Math.cos(yaw)*x+Math.sin(yaw)*z,z1=-Math.sin(yaw)*x+Math.cos(yaw)*z;return[x1,Math.cos(pitch)*y-Math.sin(pitch)*z1,Math.sin(pitch)*y+Math.cos(pitch)*z1];}
function gizmoProject(point){let p=cameraRotate(gv.mul(gv.sub(point,scene.center),1/scene.extent));let depth=distance-p[2];if(depth<.05)return null;return[canvas.clientWidth*(.5+p[0]*1.9/(canvas.clientWidth/canvas.clientHeight)/depth/2),canvas.clientHeight*(.5-p[1]*1.9/depth/2)];}
function gizmo(value){gizmoState={...gizmoState,...value};drawGizmo();}
function drawGizmo(){
 if(!gizmoContext)return;const c=gizmoContext,scale=Math.min(2,window.devicePixelRatio||1);gizmoCanvas.width=Math.max(1,Math.round(canvas.clientWidth*scale));gizmoCanvas.height=Math.max(1,Math.round(canvas.clientHeight*scale));c.scale(scale,scale);gizmoSegments=[];
 if(!scene||!gizmoState.visible)return;const position=gizmoState.position,origin=gizmoProject(position);if(!origin)return;
 const size=scene.extent*.26,axes=[[1,0,0],[0,1,0],[0,0,1]],colours=['#e65151','#64c56c','#5292ee'];
 function line(a,b,axis,angle){if(!a||!b)return;c.beginPath();c.moveTo(...a);c.lineTo(...b);c.stroke();if(gizmoState.editing)gizmoSegments.push({a,b,axis,angle});}
 axes.forEach((axis,i)=>{
  c.strokeStyle=colours[i];c.fillStyle=colours[i];c.lineWidth=2;
  const dir=gizmoRotate(axis,gizmoState.rotation),end=gizmoProject(gv.add(position,gv.mul(dir,size)));line(origin,end,i,0);
  if(end){c.font='12px Segoe UI';c.fillText('XYZ'[i],end[0]+5,end[1]-5);if(gizmoState.editing&&gizmoState.mode==='move'){let angle=Math.atan2(end[1]-origin[1],end[0]-origin[0]);c.beginPath();c.moveTo(...end);c.lineTo(end[0]-10*Math.cos(angle-.4),end[1]-10*Math.sin(angle-.4));c.lineTo(end[0]-10*Math.cos(angle+.4),end[1]-10*Math.sin(angle+.4));c.closePath();c.fill();}}
  if(gizmoState.editing&&gizmoState.mode==='rotate'){
   const u=gizmoRotate(axes[(i+1)%3],gizmoState.rotation),v=gizmoRotate(axes[(i+2)%3],gizmoState.rotation);let previous=null;
   for(let j=0;j<=96;j++){let angle=j*Math.PI/48,p=gizmoProject(gv.add(position,gv.mul(gv.add(gv.mul(u,Math.cos(angle)),gv.mul(v,Math.sin(angle))),size)));if(previous)line(previous,p,i,angle);previous=p;}
  }
 });c.fillStyle='#ddd';c.fillRect(origin[0]-3,origin[1]-3,6,6);
}
function gizmoPointer(e){const r=canvas.getBoundingClientRect?canvas.getBoundingClientRect():{left:0,top:0};return[e.clientX-r.left,e.clientY-r.top];}
function gizmoRingAngle(mouse,axis){
 const ndc=[(mouse[0]/canvas.clientWidth*2-1)*(canvas.clientWidth/canvas.clientHeight)/1.9,(1-mouse[1]/canvas.clientHeight*2)/1.9,-1];
 const origin=gv.add(scene.center,gv.mul(cameraRotate([0,0,distance],true),scene.extent)),ray=cameraRotate(ndc,true),normal=gizmoRotate([[1,0,0],[0,1,0],[0,0,1]][axis],gizmoState.rotation);let denom=gv.dot(ray,normal);if(Math.abs(denom)<1e-7)return null;
 let t=gv.dot(gv.sub(gizmoState.position,origin),normal)/denom;if(t<0)return null;let p=gv.sub(gv.add(origin,gv.mul(ray,t)),gizmoState.position),axes=[[1,0,0],[0,1,0],[0,0,1]];return Math.atan2(gv.dot(p,gizmoRotate(axes[(axis+2)%3],gizmoState.rotation)),gv.dot(p,gizmoRotate(axes[(axis+1)%3],gizmoState.rotation)));
}
const orbitDown=canvas.onpointerdown,orbitMove=canvas.onpointermove,orbitUp=canvas.onpointerup,orbitCancel=canvas.onpointercancel;
canvas.onpointerdown=e=>{
 if(!scene||!gizmoState.visible||!gizmoState.editing){orbitDown(e);return;}
 const mouse=gizmoPointer(e);let best=null,min=9;
 for(const segment of gizmoSegments){let d=gv.sub(segment.b,segment.a),length=gv.dot(d,d),t=length?Math.max(0,Math.min(1,gv.dot(gv.sub(mouse,segment.a),d)/length)):0,p=gv.add(segment.a,gv.mul(d,t)),error=Math.hypot(...gv.sub(mouse,p));if(error<min){min=error;best=segment;}}
 if(!best){orbitDown(e);return;}e.preventDefault?.();canvas.setPointerCapture(e.pointerId);gizmoDrag={axis:best.axis,mouse,position:[...gizmoState.position],rotation:[...gizmoState.rotation],angle:gizmoRingAngle(mouse,best.axis)};
};
canvas.onpointermove=e=>{
 if(!gizmoDrag){orbitMove(e);drawGizmo();return;}const d=gizmoDrag,mouse=gizmoPointer(e);
 if(gizmoState.mode==='move'){
  let dir=gizmoRotate([[1,0,0],[0,1,0],[0,0,1]][d.axis],d.rotation),a=gizmoProject(d.position),b=gizmoProject(gv.add(d.position,dir));if(!a||!b)return;let delta=gv.sub(b,a),length=gv.dot(delta,delta);if(length>1e-5)gizmoState.position=gv.add(d.position,gv.mul(dir,gv.dot(gv.sub(mouse,d.mouse),delta)/length));
 }else{let angle=gizmoRingAngle(mouse,d.axis);if(angle!=null&&d.angle!=null){let diff=angle-d.angle;while(diff>Math.PI)diff-=2*Math.PI;while(diff< -Math.PI)diff+=2*Math.PI;gizmoState.rotation=[...d.rotation];gizmoState.rotation[d.axis]+=diff*180/Math.PI;}}
 drawGizmo();window.chrome.webview.postMessage({gizmo:{position:gizmoState.position,rotation:gizmoState.rotation}});
};
canvas.onpointerup=e=>{if(gizmoDrag){gizmoDrag=null;return;}orbitUp(e);drawGizmo();};canvas.onpointercancel=e=>{gizmoDrag=null;orbitCancel(e);};
const normalDraw=draw;draw=function(picking=false){normalDraw(picking);if(!picking)drawGizmo();};
