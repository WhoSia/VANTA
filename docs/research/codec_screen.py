"""Local codec exploration: fixed inputs, nonmatched quality settings, not production acceptance."""
import subprocess as sp, tempfile, pathlib, time, re, json, hashlib

def run(args, timeout=60):
    p=sp.run(args,stdout=sp.PIPE,stderr=sp.PIPE,text=True,timeout=timeout)
    if p.returncode: raise RuntimeError(p.stderr[-3000:])
    return p

def ff(*args): return run(['ffmpeg','-hide_banner','-nostdin','-v','error','-xerror','-n',*map(str,args)])
def probe(p): return json.loads(run(['ffprobe','-v','error','-select_streams','v:0','-show_frames','-show_entries','frame=best_effort_timestamp_time','-of','json',str(p)]).stdout)['frames']
configs={'x264':['-c:v','libx264','-preset','medium','-crf','23'],'x265':['-c:v','libx265','-preset','medium','-crf','28','-x265-params','pools=4:frame-threads=2:log-level=error'],'rav1e':['-c:v','librav1e','-speed','8','-qp','100'],'svtav1':['-c:v','libsvtav1','-preset','8','-crf','32']}
with tempfile.TemporaryDirectory() as d:
    d=pathlib.Path(d)
    for fixture,filter in [('motion','testsrc2=s=320x180:r=30:d=3'),('flat','color=c=navy:s=320x180:r=30:d=3')]:
        src=d/f'{fixture}.mp4';ff('-f','lavfi','-i',filter,'-c:v','libx264','-preset','fast','-crf','18','-threads','4','-pix_fmt','yuv420p',src)
        pts=[float(x['best_effort_timestamp_time']) for x in probe(src)];stamp=hashlib.sha256(src.read_bytes()).hexdigest()
        for rep in range(2):
            for name in (list(configs) if rep==0 else list(reversed(configs))):
                dst=d/f'{fixture}-{rep}-{name}.mp4'; start=time.perf_counter()
                try:
                    ff('-i',src,'-map','0:v:0','-an',*configs[name],'-threads','4','-pix_fmt','yuv420p','-fps_mode','passthrough','-video_track_timescale','90000',dst)
                    encode=time.perf_counter()-start
                    out=[float(x['best_effort_timestamp_time']) for x in probe(dst)]
                    if len(out)!=len(pts) or max(abs(x-y) for x,y in zip(pts,out))>.002: raise ValueError('PTS mismatch')
                    ff('-i',dst,'-map','0:v:0','-f','null','-'); verified=time.perf_counter()-start
                    q=run(['ffmpeg','-hide_banner','-nostdin','-i',str(src),'-i',str(dst),'-filter_complex_threads','2','-filter_complex','[0:v][1:v]ssim=shortest=1:repeatlast=0','-an','-f','null','-']).stderr
                    s=float(re.findall(r'All:([0-9.]+)',q)[-1]);print(json.dumps(dict(fixture=fixture,rep=rep,codec=name,encode=encode,verified=verified,bytes=dst.stat().st_size,ssim=s,frames=len(out))),flush=True)
                except Exception as exc: print(json.dumps(dict(fixture=fixture,rep=rep,codec=name,error=str(exc))),flush=True)
        assert hashlib.sha256(src.read_bytes()).hexdigest()==stamp
