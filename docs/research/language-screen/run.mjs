// Research only. Same FFmpeg/ffprobe work, including interpreter startup.
import {spawnSync} from 'node:child_process';
import {mkdtempSync,rmSync,existsSync,readFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {fileURLToPath} from 'node:url';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
const dir=mkdtempSync(join(tmpdir(),'vanta-language-'));
const py=process.argv[2]??'python';
const systemPy=process.argv[3];
const commands={
  python:[py,fileURLToPath(new URL('./launch.py',import.meta.url))],
  'python-isolated':[py,'-S',fileURLToPath(new URL('./launch.py',import.meta.url))],
  node:[process.execPath,fileURLToPath(new URL('./launch.mjs',import.meta.url))]
};
if(systemPy)commands['system-python']=[systemPy,fileURLToPath(new URL('./launch.py',import.meta.url))];
function run(exe,args){
 const p=spawnSync(exe,args,{encoding:'utf8',timeout:30000,maxBuffer:16*1024*1024,cwd:dir});
 if(p.error||p.status!==0||p.stderr)throw Error(p.error??p.stderr??'child failed');
 return p.stdout.trim();
}
try{
 run('ffmpeg',['-v','error','-n','-f','lavfi','-i','testsrc2=s=640x360:r=30:d=3','-c:v','libx264','-threads','2','-preset','veryfast','-pix_fmt','yuv420p','src.mp4']);
 const rows=[];
 for(const mode of ['probe','encode']){
  const names=Object.keys(commands);
  for(let rep=0;rep<6;rep++)for(const lang of [...names.slice(rep%names.length),...names.slice(0,rep%names.length)]){
   const out=mode+'-'+rep+'-'+lang+'.mp4';
   const [exe,...args]=commands[lang],t=performance.now();
   const hash=run(exe,[...args,'src.mp4',out,mode]);
   rows.push({mode,rep,language:lang,seconds:(performance.now()-t)/1000,sha256:hash});
   if(existsSync(join(dir,out)))rmSync(join(dir,out));
  }
  assert.equal(new Set(rows.filter(r=>r.mode===mode).map(r=>r.sha256)).size,1);
 }
 console.log(JSON.stringify({source: createHash('sha256').update(readFileSync(join(dir,'src.mp4'))).digest('hex'),rows},null,2));
}finally{rmSync(dir,{recursive:true,force:true});}
