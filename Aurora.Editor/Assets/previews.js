async function requestPreview(id,options){
 const saved={yaw,pitch,distance,focal,selected,renderSize,transparentPreview,center:scene?.center,extent:scene?.extent,gizmo:gizmoState.visible};
 try{
  if(!scene)throw Error('No model loaded');
  if(options.width!==256||![128,256].includes(options.height)||!Number.isFinite(options.fov)||options.fov<5||options.fov>100)throw Error('Invalid preview settings');
  await Promise.all([...pendingImages]);await new Promise(resolve=>requestAnimationFrame(resolve));
  renderSize={width:options.width,height:options.height};transparentPreview=true;selected=-1;gizmoState.visible=false;yaw=options.yaw*Math.PI/180;pitch=options.pitch*Math.PI/180;focal=1/Math.tan(options.fov*Math.PI/360);
  const lo=[Infinity,Infinity,Infinity],hi=[-Infinity,-Infinity,-Infinity];for(const n of nodes)if(!n.Cargo)for(let i=0;i<n.Positions.length;i++){const axis=i%3;lo[axis]=Math.min(lo[axis],n.Positions[i]);hi[axis]=Math.max(hi[axis],n.Positions[i]);}
  const points=[];for(const x of [lo[0],hi[0]])for(const y of [lo[1],hi[1]])for(const z of [lo[2],hi[2]])points.push([x,y,z]);scene.center=lo.map((v,i)=>(v+hi[i])/2);scene.extent=Math.max(...hi.map((v,i)=>v-lo[i]))/2||1;
  const aspect=options.width/options.height;distance=.1;
  for(const point of points){const p=cameraRotate(gv.mul(gv.sub(point,scene.center),1/scene.extent));distance=Math.max(distance,p[2]+Math.abs(p[0])*focal/aspect*1.12,p[2]+Math.abs(p[1])*focal*1.12,p[2]+.1);}
  draw();window.chrome.webview.postMessage({preview:id,png:canvas.toDataURL('image/png')});
 }catch(error){window.chrome.webview.postMessage({preview:id,error:String(error)});}
 finally{yaw=saved.yaw;pitch=saved.pitch;distance=saved.distance;focal=saved.focal;selected=saved.selected;renderSize=saved.renderSize;transparentPreview=saved.transparentPreview;if(scene){scene.center=saved.center;scene.extent=saved.extent;}gizmoState.visible=saved.gizmo;draw();}
}
