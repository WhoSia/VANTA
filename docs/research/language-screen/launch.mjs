import {spawnSync} from 'node:child_process';
import {readFileSync} from 'node:fs';
import {createHash} from 'node:crypto';
const [source,out,mode]=process.argv.slice(2);
const args=mode==='probe'?['ffprobe','-v','error','-show_streams','-of','json',source]:['ffmpeg','-v','error','-xerror','-nostdin','-n','-i',source,'-c:v','libx264','-preset','fast','-crf','23','-threads','2','-an',out];
let p;for(let i=0;i<(mode==='probe'?5:1);i++){p=spawnSync(args[0],args.slice(1),{maxBuffer:16*1024*1024,timeout:30000});if(p.status!==0||p.stderr.length)throw Error(String(p.stderr));}
console.log(createHash('sha256').update(mode==='probe'?p.stdout:readFileSync(out)).digest('hex'));
