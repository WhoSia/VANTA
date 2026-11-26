import {spawnSync} from 'node:child_process';
import {mkdtempSync,rmSync,statSync,readFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {createHash} from 'node:crypto';
const hash=p=>createHash('sha256').update(readFileSync(p)).digest('hex');
function run(exe,args){const p=spawnSync(exe,args,{encoding:'utf8',timeout:20000,maxBuffer:4*1024*1024});if(p.error||p.status!==0)throw Error(p.error??p.stderr);return p;}
const d=mkdtempSync(join(tmpdir(),'vanta-rd-'));const results=[];
try {for(const [id,filter] of [['motion','testsrc2=s=320x180:r=24:d=2'],['mixed','testsrc2=s=320x180:r=24:d=2,drawbox=x=0:y=0:w=iw:h=ih:color=gray:t=fill:enable=lt(t\\,0.7)']]){
 const src=join(d,id+'.mp4');run('ffmpeg',['-v','error','-xerror','-n','-f','lavfi','-i',filter,'-an','-c:v','libx264','-preset','veryfast','-crf','18','-threads','2','-pix_fmt','yuv420p',src]);const sha=hash(src);const grid=[];
 for(let q=18;q<=34;q++){const dst=join(d,id+'-'+q+'.mp4');const t=performance.now();const enc=run('ffmpeg',['-v','error','-xerror','-nostdin','-n','-i',src,'-an','-c:v','libx264','-preset','fast','-crf',String(q),'-threads','2',dst]);const encSec=(performance.now()-t)/1000;
 const p=run('ffmpeg',['-v','error','-xerror','-nostdin','-i',src,'-i',dst,'-filter_complex_threads','1','-filter_complex','[0:v][1:v]ssim=stats_file=-:shortest=1:repeatlast=0','-an','-f','null','-']);if(p.stderr.trim()||enc.stderr.trim())throw Error('unexpected stderr');const scores=p.stdout.split('\n').filter(Boolean).map(x=>Number(x.match(/All:([0-9.]+)/)?.[1]));if(scores.length!==48||scores.some(x=>!Number.isFinite(x)))throw Error('frame mismatch');grid.push({q,mean:scores.reduce((a,b)=>a+b,0)/scores.length,bytes:statSync(dst).size,encodeSeconds:encSec,probeSeconds:(performance.now()-t)/1000,frames:scores.length});rmSync(dst);}
 if(hash(src)!==sha)throw Error('source changed');results.push({id,sourceSha256:sha,grid});}
 console.log(JSON.stringify({environment:run('ffmpeg',['-version']).stdout.split('\n')[0],results},null,2));
}finally{rmSync(d,{recursive:true,force:true});}
