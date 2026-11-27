"""Local serial/parallel batch proxy; use an explicitly new lab directory."""
import concurrent.futures as cf,hashlib,json,pathlib,subprocess as sp,time,sys,math,os
root=pathlib.Path(os.environ["VANTA_LAB_DIR"]).resolve()
ff=['ffmpeg','-v','error','-xerror','-nostdin','-n'];fp=['ffprobe','-v','error']
def run(a):
 p=sp.run(list(map(str,a)),capture_output=True,timeout=120,check=True)
 if p.stderr.strip():raise RuntimeError(p.stderr.decode()[-1000:])
 return p.stdout.decode()
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def pts(p):return [float(x['best_effort_timestamp_time']) for x in json.loads(run(fp+['-select_streams','v:0','-show_frames','-show_entries','frame=best_effort_timestamp_time','-of','json',p]))['frames']]
def record(row):
 print(json.dumps(row),flush=True)
 with (root/'results.jsonl').open('a') as f:f.write(json.dumps(row)+'\n')
filters={'motion':'null','inverted':'negate','mixed':'drawbox=x=0:y=0:w=iw:h=ih:color=gray:t=fill:enable=between(t\\,4\\,7.999)','late':'drawbox=x=0:y=0:w=iw:h=ih:color=gray:t=fill:enable=lt(t\\,7)','grain':'noise=alls=12:allf=t+u:all_seed=73','detail':'hue=s=0,unsharp=5:5:1.2:5:5:0.0'}
if sys.argv[1]=='make':
 root.mkdir(parents=True,exist_ok=True)
 if any(root.iterdir()):raise FileExistsError('use an empty lab directory')
 record(dict(kind='environment',ffmpeg=sp.check_output(['ffmpeg','-version'],text=True).splitlines()[0],cpu_quota=pathlib.Path('/sys/fs/cgroup/cpu.max').read_text().strip()))
 for name,vf in filters.items():
  p=root/(name+'.mp4');run(ff+['-f','lavfi','-i','testsrc2=s=1280x720:r=30:d=12','-vf',vf,'-an','-c:v','libx264','-preset','ultrafast','-threads','2','-b:v','4M','-minrate','4M','-maxrate','4M','-bufsize','4M','-x264-params','nal-hrd=cbr:filler=1',p]);m=json.loads(run(fp+['-show_streams','-show_format','-of','json',p]));assert len(pts(p))==360
  record(dict(kind='source',id=name,bytes=p.stat().st_size,sha=sha(p),rate=int(m['streams'][0]['bit_rate']),seconds=12,frames=360))
else:
 group=sys.argv[1];workers=int(sys.argv[2])
 if group not in ('train','test') or workers not in (1,2):raise ValueError('unsupported group/workers')
 names=list(filters)[:3] if group=='train' else list(filters)[3:];source={r['id']:r for r in map(json.loads,(root/'results.jsonl').read_text().splitlines()) if r['kind']=='source'}
 prior=[r for r in map(json.loads,(root/'results.jsonl').read_text().splitlines()) if r['kind']=='job' and r['group']=='train' and r['workers']==workers]
 if group=='test':
  assert len(prior)==3 and all(.8<=source[a['id']]['rate']/source[n]['rate']<=1.25 for a in prior for n in names)
  low,high= min(r['seconds'] for r in prior),max(r['seconds'] for r in prior)
  assert high/low<=3
  predict=(math.floor(max(low,3*low/workers)),math.ceil(3*high/workers+(1-1/workers)*high))
 else:predict=None
 start=time.monotonic()
 def job(n):
  p=root/(n+'.mp4');out=root/f'{group}-{workers}-{n}.mp4';before=source[n]['sha'];assert sha(p)==before
  admitted=time.monotonic()-start;enc=time.monotonic();run(ff+['-i',p,'-map','0:v','-c:v','libx264','-preset','veryfast','-threads','2','-b:v','2M','-fps_mode','passthrough','-movflags','+faststart',out]);enc=time.monotonic()-enc
  a=pts(p);b=pts(out);assert len(a)==len(b)==360;origin=b[0]-a[0];drift=max(abs(y-x-origin) for x,y in zip(a,b));assert drift<=.002;run(ff+['-i',out,'-map','0:v','-f','null','-']);finish=time.monotonic()-start;assert sha(p)==before
  row=dict(kind='job',group=group,workers=workers,id=n,admitted=admitted,finished=finish,seconds=finish-admitted,encoding_s=enc,output_bytes=out.stat().st_size,output_sha=sha(out),source_sha=before,frames=360,drift=drift,strict_decode=True);out.unlink();return row
 with cf.ThreadPoolExecutor(max_workers=workers) as pool:
  futures=[pool.submit(job,n) for n in names];rows=[f.result() for f in cf.as_completed(futures)]
 wall=time.monotonic()-start
 for row in sorted(rows,key=lambda r:r['finished']):record(row)
 record(dict(kind='batch',group=group,workers=workers,wall_seconds=wall,job_sum=sum(r['seconds'] for r in rows),predicted=predict,covered=(predict[0]<=wall<=predict[1]) if predict else None))
