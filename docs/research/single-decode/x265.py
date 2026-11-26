import subprocess as s,tempfile,pathlib,time,json,hashlib,statistics,re
def run(a):
    p=s.run(a,capture_output=True,text=True,timeout=30)
    if p.returncode:raise RuntimeError(p.stderr[-1000:])
    return p
ff=['ffmpeg','-v','error','-xerror','-nostdin','-n']
with tempfile.TemporaryDirectory() as d:
    d=pathlib.Path(d);src=d/'source.mp4'
    run(ff+['-f','lavfi','-i','testsrc2=s=640x360:r=30:d=2','-c:v','libx264','-preset','veryfast','-threads','4','-pix_fmt','yuv420p',str(src)])
    before=hashlib.sha256(src.read_bytes()).hexdigest();modes={'auto':'pools=4:log-level=error','frame1':'pools=4:frame-threads=1:log-level=error','frame2':'pools=4:frame-threads=2:log-level=error','frame4':'pools=4:frame-threads=4:log-level=error'}
    rows=[]
    for rep in range(3):
        names=list(modes);names=names[rep:]+names[:rep]
        for mode in names:
            dest=d/f'{mode}-{rep}.mp4';t=time.perf_counter();run(ff+['-i',str(src),'-an','-c:v','libx265','-preset','fast','-crf','25','-x265-params',modes[mode],str(dest)]);elapsed=time.perf_counter()-t
            r=run(['ffmpeg','-hide_banner','-nostdin','-i',str(src),'-i',str(dest),'-filter_complex_threads','2','-filter_complex','[0:v][1:v]ssim=shortest=1:repeatlast=0','-an','-f','null','-']);score=float(re.findall(r'All:([0-9.]+)',r.stderr)[-1]);run(ff+['-i',str(dest),'-f','null','-']);probe=run(['ffprobe','-v','error','-select_streams','v:0','-count_frames','-show_entries','stream=nb_read_frames','-of','csv=p=0',str(dest)]);assert probe.stdout.strip()=='60';row=dict(rep=rep,mode=mode,seconds=elapsed,bytes=dest.stat().st_size,ssim=score,frames=60);rows.append(row);print(json.dumps(row),flush=True)
    assert hashlib.sha256(src.read_bytes()).hexdigest()==before
    print(json.dumps(dict(source_sha256=before,medians={m:statistics.median(r['seconds'] for r in rows if r['mode']==m) for m in modes})),flush=True)
