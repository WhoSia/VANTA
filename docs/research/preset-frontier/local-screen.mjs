import {execFileSync} from 'node:child_process';
import {readFileSync,statSync,mkdtempSync,rmSync,unlinkSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
const ff=['-v','error','-xerror','-nostdin'];const run=(exe,args)=>execFileSync(exe,args,{encoding:'utf8',timeout:30000,maxBuffer:8*1024*1024});const sha=p=>createHash('sha256').update(readFileSync(p)).digest('hex');
const dir=mkdtempSync(join(tmpdir(),'vanta-speed-'));
try {for(const scene of ['motion','mixed']){
const src=join(dir,scene+'.mp4');const filter=scene==='mixed'?['-vf','drawbox=x=0:y=0:w=iw:h=ih:color=gray:t=fill:enable=lt(t\\,1)']:[];
run('ffmpeg',[...ff,'-y','-f','lavfi','-i','testsrc2=s=640x360:r=30:d=3',...filter,'-c:v','libx264','-preset','ultrafast','-crf','18','-threads','4','-pix_fmt','yuv420p',src]);const sourceSha=sha(src);
function pts(p){return run('ffprobe',['-v','error','-select_streams','v:0','-show_frames','-show_entries','frame=best_effort_timestamp_time','-of','compact=p=0:nk=0',p]).split('\n').flatMap(x=>{let m=x.match(/best_effort_timestamp_time=([^|]+)/);return m?[Number(m[1])]:[]})}const reference=pts(src);assert.equal(reference.length,90);
const modes=[['libx265','medium'],['libx264','fast'],['libx265','veryfast'],['libx264','medium'],['libx265','fast'],['libx264','veryfast']];
for(const [codec,preset] of scene==='mixed'?modes.reverse():modes){const dest=join(dir,scene+'-'+codec+'-'+preset+'.mp4');let t=performance.now();const x=codec==='libx265'?['-x265-params','pools=4:log-level=error']:[];run('ffmpeg',[...ff,'-y','-i',src,'-an','-c:v',codec,'-preset',preset,'-threads','4',...x,'-b:v','700k','-fps_mode','passthrough','-movflags','+faststart',dest]);const encoding=(performance.now()-t)/1000;const p=pts(dest);assert.equal(p.length,90);assert.ok(p.every((v,i)=>Math.abs(v-reference[i])<=.002));run('ffmpeg',[...ff,'-i',dest,'-map','0:v','-f','null','-']);const verified=(performance.now()-t)/1000;const metric=run('ffmpeg',[...ff,'-i',src,'-i',dest,'-filter_complex_threads','2','-filter_complex','[0:v][1:v]ssim=stats_file=-:shortest=1:repeatlast=0','-an','-f','null','-']);const scores=metric.trim().split('\n').map(x=>Number(x.match(/All:([0-9.]+)/)?.[1]));assert.equal(scores.length,90);assert.ok(scores.every(Number.isFinite));console.log(JSON.stringify({scene,codec,preset,encoding,verified,bytes:statSync(dest).size,mean:scores.reduce((a,b)=>a+b,0)/90,worst:Math.min(...[0,30,60].map(i=>scores.slice(i,i+30).reduce((a,b)=>a+b,0)/30)),frames:p.length,sourceSha,outputSha:sha(dest)}));unlinkSync(dest);}assert.equal(sha(src),sourceSha);
}}finally{rmSync(dir,{recursive:true,force:true});}
