import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {mkdtempSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import assert from 'node:assert/strict';
const exec=promisify(execFile);
async function run(exe,args){const r=await exec(exe,args,{timeout:15000,maxBuffer:16*1024*1024});if(r.stderr.trim())throw Error(r.stderr);return r.stdout;}
const d=mkdtempSync(join(tmpdir(),'vanta-audio-'));
try{
 const rows=[];
 for(const tracks of [2,4]){
  const src=join(d,'src'+tracks+'.mp4'),dst=join(d,'dst'+tracks+'.mp4');
  const args=['-v','error','-xerror','-nostdin','-n','-f','lavfi','-i','testsrc2=s=320x180:r=24:d=8'];
  for(let i=0;i<tracks;i++)args.push('-f','lavfi','-i','sine=frequency='+(440+110*i)+':sample_rate=48000:duration=8');
  args.push('-map','0:v');for(let i=0;i<tracks;i++)args.push('-map',i+1+':a');
  await run('ffmpeg',[...args,'-c:v','libx264','-preset','ultrafast','-threads','2','-c:a','aac','-b:a','96k',src]);
  await run('ffmpeg',['-v','error','-xerror','-nostdin','-n','-i',src,'-map','0:v','-map','0:a','-c:v','libx264','-preset','veryfast','-threads','2','-c:a','copy',dst]);
  const read=async(p,sel)=>{const out=await run('ffprobe',['-v','error','-select_streams',sel,'-show_packets','-show_entries','packet=stream_index,pts_time,data_hash','-show_data_hash','sha256','-of','compact=p=1:nk=0',p]);return out.split('\n').filter(Boolean).map(l=>{const f=Object.fromEntries(l.split('|').slice(1).filter(x=>x.includes('=')).map(x=>x.split('=')));assert.ok(Number.isFinite(+f.pts_time)&&/^SHA256:[0-9a-f]{64}$/i.test(f.data_hash));return {i:+f.stream_index,pts:+f.pts_time,hash:f.data_hash};});};
  const cmp=(a,b)=>{assert.equal(a.length,b.length);for(let j=0;j<a.length;j++){assert.equal(a[j].hash,b[j].hash);assert.ok(Math.abs(a[j].pts-b[j].pts)<=.002);}return a.length;};
  for(let rep=0;rep<3;rep++)for(const mode of (rep%2?['all','per-track']:['per-track','all'])){const t=performance.now();let n=0;if(mode==='per-track'){for(let i=0;i<tracks;i++){const [a,b]=await Promise.all([read(src,'a:'+i),read(dst,'a:'+i)]);n+=cmp(a,b);}}else{const [a,b]=await Promise.all([read(src,'a'),read(dst,'a')]);for(let i=1;i<=tracks;i++)n+=cmp(a.filter(x=>x.i===i),b.filter(x=>x.i===i));assert.equal(n,a.length);assert.equal(n,b.length);}const row={tracks,rep,mode,seconds:(performance.now()-t)/1000,packets:n};rows.push(row);console.log(JSON.stringify(row));}
 }
 console.log(JSON.stringify({summary:rows.length}));
}finally{rmSync(d,{recursive:true,force:true});}
