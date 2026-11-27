"""Local staged long-media screen. Run in an explicitly new, disposable directory."""
import concurrent.futures as cf
import hashlib,json,pathlib,subprocess as sp,time,statistics,sys,os
ROOT=pathlib.Path(os.environ["VANTA_LAB_DIR"]).resolve()
FF=['ffmpeg','-v','error','-xerror','-nostdin','-n']; FP=['ffprobe','-v','error']; N=900

def call(args):
 r=sp.run(list(map(str,args)),capture_output=True,timeout=180,check=True)
 if r.stderr.strip(): raise RuntimeError(r.stderr.decode(errors='replace')[-1500:])
 return r.stdout.decode()
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def pts(p):return [float(x['best_effort_timestamp_time']) for x in json.loads(call(FP+['-select_streams','v:0','-show_frames','-show_entries','frame=best_effort_timestamp_time','-of','json',p]))['frames']]
def packets(p,selector='a'):
 d=json.loads(call(FP+['-select_streams',selector,'-show_packets','-show_entries','packet=stream_index,pts_time,duration_time,data_hash','-show_data_hash','sha256','-of','json',p]))['packets']; g={}
 for x in d:g.setdefault(x['stream_index'],[]).append((float(x['pts_time']),float(x['duration_time']),x['data_hash']))
 return list(g.values())
def strict(p,clock=False):
 a=FF+['-copyts','-i',p,'-map','0:V:0','-map','0:a?','-fps_mode','passthrough']
 if clock:a+=['-stats_enc_pre:v','pipe:1','-stats_enc_pre_fmt:v','{n} {ptsi} {tbi}']
 o=call(a+['-f','null','-'])
 if clock:
  v=[]
  for line in o.splitlines():
   n,pt,tb=line.split(); num,den=map(int,tb.split('/')); assert int(n)==len(v);v.append(int(pt)*num/den)
  return v
def streamhash(p):return call(FF+['-i',p,'-map','0:a','-c:a','copy','-f','streamhash','-hash','sha256','-']).strip()
def parallel(a,b):
 with cf.ThreadPoolExecutor(max_workers=2) as pool:
  x,y=pool.submit(a),pool.submit(b);return x.result(),y.result()
def compare(a,b):
 assert len(a)==len(b)==N
 origin=b[0]-a[0];drift=max(abs(y-x-origin) for x,y in zip(a,b));assert drift<=.002;return origin,drift
def audio_compare(a,b,origin):
 assert len(a)==len(b)>0
 for x,y in zip(a,b):
  assert len(x)==len(y)>0
  for (p,d,h),(q,e,k) in zip(x,y):assert abs(q-p-origin)<=.002 and abs(e-d)<=.002 and h==k
 return sum(map(len,a))
def record(row):
 with (ROOT/'results.jsonl').open('a') as f:f.write(json.dumps(row)+'\n')
 print(json.dumps(row),flush=True)
mode=sys.argv[1];src=ROOT/'source.mp4'
if mode=='make':
 ROOT.mkdir(parents=True,exist_ok=True)
 if any(ROOT.iterdir()):raise FileExistsError('use an empty lab directory')
 record(dict(kind='environment',ffmpeg=sp.check_output(['ffmpeg','-version'],text=True).splitlines()[0],cpu_quota=pathlib.Path('/sys/fs/cgroup/cpu.max').read_text().strip(),scope='local FFmpeg proxy; validation-only repeated on fixed output, independent oracle outside timer'))
 call(FF+['-f','lavfi','-i','testsrc2=s=1920x1080:r=30:d=30','-f','lavfi','-i','sine=frequency=440:sample_rate=48000:duration=30','-f','lavfi','-i','sine=frequency=660:sample_rate=48000:duration=30','-map','0:v','-map','1:a','-map','2:a','-vf','drawbox=x=0:y=0:w=iw:h=ih:color=gray:t=fill:enable=between(t\\,10\\,19.999)','-c:v','libx264','-preset','ultrafast','-crf','18','-threads','4','-c:a','aac','-b:a','128k',src])
 record(dict(kind='source',bytes=src.stat().st_size,sha=sha(src),frames=len(pts(src)),audio_packets=sum(map(len,packets(src)))))
else:
 rows=[json.loads(l) for l in (ROOT/'results.jsonl').read_text().splitlines()];source=next(r for r in rows if r['kind']=='source');sh=source['sha'];assert sh==sha(src)
 codec=sys.argv[2]
 if codec not in ('libx264','libx265'):raise ValueError(codec)
 out=ROOT/(codec+'.mp4')
 if mode=='encode':
  preset='veryfast' if codec=='libx264' else 'medium';start=time.monotonic()
  args=FF+['-i',src,'-map','0:v','-map','0:a','-c:v',codec,'-preset',preset,'-b:v','8M','-threads','4','-fps_mode','passthrough','-c:a','copy','-movflags','+faststart']
  if codec=='libx265':args+=['-x265-params','log-level=error:pools=4']
  call(args+[out]);enc=time.monotonic()-start
  record(dict(kind='encode',codec=codec,preset=preset,encoding_s=enc,bytes=out.stat().st_size,sha=sha(out)))
 else:
  before=next(r['sha'] for r in rows if r['kind']=='encode' and r['codec']==codec);assert sha(out)==before
  if mode=='quality':
   ref=pts(src);origin,drift=compare(ref,pts(out));n=audio_compare(packets(src),packets(out),origin);strict(out);assert streamhash(src)==streamhash(out)
   log=ROOT/(codec+'.ssim');assert not log.exists();call(FF+['-i',src,'-i',out,'-lavfi',f'ssim=stats_file={log}','-an','-f','null','-'])
   values=[float(next(v[4:] for v in line.split() if v.startswith('All:'))) for line in log.read_text().splitlines()];assert len(values)==N and sha(out)==before and sha(src)==sh
   record(dict(kind='quality',codec=codec,mean_ssim=statistics.mean(values),worst_second=min(statistics.mean(values[i:i+30]) for i in range(0,len(values),30)),frames=len(values),audio_packets=n,oracle_drift=drift,output_sha=before,source_unchanged=True,strict_decode=True));log.unlink()
  elif mode=='validate':
   rep=int(sys.argv[3]);v=sys.argv[4];start=time.monotonic()
   if v=='baseline':
    a,b=parallel(lambda:pts(src),lambda:pts(out));origin,drift=compare(a,b)
    for idx in range(2):
     a,b=parallel(lambda:packets(src,f'a:{idx}'),lambda:packets(out,f'a:{idx}'));assert len(a)==len(b)==1;audio_compare(a,b,origin)
    assert streamhash(src)==streamhash(out);strict(out)
   elif v=='combined':
    a,b=parallel(lambda:pts(src),lambda:strict(out,True));origin,drift=compare(a,b)
    a,b=parallel(lambda:packets(src),lambda:packets(out));assert len(a)==len(b)==2;audio_compare(a,b,origin)
   else:raise ValueError(v)
   elapsed=time.monotonic()-start;assert sha(src)==sh and sha(out)==before
   record(dict(kind='validation',codec=codec,rep=rep,mode=v,seconds=elapsed,drift=drift,source_sha=sh,output_sha=before))
  else:raise ValueError(mode)
