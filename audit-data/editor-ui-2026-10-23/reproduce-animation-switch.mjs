import fs from 'node:fs';
import {fileURLToPath} from 'node:url';
import {spawnSync} from 'node:child_process';
const root=fileURLToPath(new URL('../../',import.meta.url));
const assets=new URL('../../Aurora.Editor/Assets/',import.meta.url).href;
let test=fs.readFileSync(new URL('../../Packaging/test-viewport.mjs',import.meta.url),'utf8').replaceAll("new URL('../Aurora.Editor/Assets/", "new URL('"+assets);
test+=`
api.meshPreview(null);api.load({Materials:['paint'],Nodes:[0,1].map(i=>({Name:'mesh'+i,Positions:[i*4,0,0,i*4+1,0,0,i*4,1,0],Normals:[0,0,1,0,0,1,0,0,1],Uv:[0,0,1,0,0,1],Indices:[0,1,2],Groups:[{First:0,Count:3,Material:0}]}))});
const makeClip=mesh=>({Tracks:[{Mesh:mesh,Pivot:[0,0,0],Interpolation:'Linear',Keys:[{Frame:0,Pose:{Position:[1,0,0],Rotation:[0,0,0],Scale:[1,1,1]}}]}]});api.previewAnimation(makeClip('mesh0'),0);const posed=api.allNodes()[0].Positions.slice();api.previewAnimation(makeClip('mesh1'),0);assert.equal(posed[0],1);assert.equal(api.allNodes()[0].Positions[0],0);console.log('PASS REGRESSION: previous non-track mesh returns to source pose after target change');
`;
const output=new URL('../../artifacts/deep-audit-repro.mjs',import.meta.url);fs.writeFileSync(output,test);const result=spawnSync(process.execPath,[fileURLToPath(output)],{cwd:root,stdio:'inherit'});process.exitCode=result.status??1;
