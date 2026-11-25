"""Research screen only. Python 3 + FFmpeg/ffprobe on PATH. No production defaults."""
import subprocess as sp, tempfile, pathlib, json, hashlib, time, concurrent.futures, statistics

def run(*args):
    p=sp.run(args,check=True,stdout=sp.PIPE,stderr=sp.PIPE,text=True)
    if p.stderr.strip(): raise RuntimeError(p.stderr)
    return p.stdout

def ff(*args): return run('ffmpeg','-hide_banner','-loglevel','error','-xerror','-nostdin','-n',*map(str,args))
def frames(path):
    return json.loads(run('ffprobe','-v','error','-select_streams','v:0','-show_frames','-show_entries','frame=key_frame,best_effort_timestamp_time','-of','json',str(path)))['frames']
def hashes(path): return [l.split(',')[-1].strip() for l in ff('-i',path,'-map','0:v:0','-an','-f','framemd5','-').splitlines() if not l.startswith('#')]
def hashfile(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def audiohash(p): return ff('-i',p,'-map','0:a','-c:a','copy','-f','streamhash','-hash','sha256','-').strip()
def audio_pts(p): return run('ffprobe','-v','error','-select_streams','a:0','-show_packets','-show_entries','packet=pts_time','-of','compact=p=0:nk=1',str(p))

with tempfile.TemporaryDirectory() as d:
    d=pathlib.Path(d)
    for gop in ('closed','open'):
        src=d/f'{gop}.mp4'; fps=30000/1001
        ff('-f','lavfi','-i','testsrc2=s=320x180:r=30000/1001:d=8.008','-f','lavfi','-i','sine=frequency=440:sample_rate=48000:duration=8.008','-c:v','libx264','-preset','fast','-crf','18','-g','60','-keyint_min','60','-sc_threshold','0','-x264-params',f'open-gop={int(gop=="open")}','-threads','2','-pix_fmt','yuv420p','-c:a','aac',src)
        stamp=hashfile(src); grid=frames(src); n=len(grid); bounds=[0]+[i for i,x in enumerate(grid) if i and x['key_frame']==1]+[n]; pts=[float(x['best_effort_timestamp_time']) for x in grid]
        if len(bounds)!=5: raise ValueError(bounds)
        ref=None; times={k:[] for k in ('early','indexed')}; print(gop,'frames',n,'boundaries',bounds,'source',stamp,flush=True)
        for j,mode in enumerate(('early','indexed','indexed','early')):
            work=d/f'{gop}-{j}';work.mkdir();t=time.perf_counter()
            def part(i):
                a,b=bounds[i:i+2]; start=pts[a]; cut=0 if a==0 else (pts[a-1]+start)/2
                seek=max(0,a/fps-1) if mode=='early' else start
                opts=['-noaccurate_seek'] if mode=='indexed' else []
                p=work/f'part{i}.mp4';ff('-ss',f'{seek:.9f}',*opts,'-copyts','-i',src,'-map','0:v:0','-an','-vf',f'trim=start={cut:.9f},setpts=PTS-STARTPTS','-frames:v',b-a,'-c:v','libx264','-preset','medium','-crf','23','-threads','2','-fps_mode','passthrough','-video_track_timescale','90000',p)
                got=frames(p);assert len(got)==b-a and all(abs(float(x['best_effort_timestamp_time'])-k/fps)<.0001 for k,x in enumerate(got))
            try:
                with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool: list(pool.map(part,range(4)))
                manifest=work/'parts.txt';manifest.write_text(''.join(f"file 'part{i}.mp4'\nduration {(bounds[i+1]-bounds[i])/fps:.9f}\n" for i in range(4)))
                out=work/'out.mp4';ff('-f','concat','-safe','1','-i',manifest,'-i',src,'-map','0:v:0','-map','1:a:0','-c','copy','-video_track_timescale','90000','-movflags','+faststart',out)
                actual=[float(x['best_effort_timestamp_time']) for x in frames(out)];assert len(actual)==n and max(abs(a-b) for a,b in zip(actual,pts))<=.002
                assert audiohash(src)==audiohash(out) and audio_pts(src)==audio_pts(out)
                ff('-i',out,'-map','0:v:0','-map','0:a:0','-f','null','-');elapsed=time.perf_counter()-t
                h=hashes(out); assert len(h)==n
                if ref is None: ref=h
                same=h==ref; times[mode].append(elapsed);print(mode,round(elapsed,6),'same decoded frames',same,'bytes',out.stat().st_size,'sha',hashfile(out),flush=True)
            except (AssertionError,RuntimeError,sp.CalledProcessError) as e: print(mode,'REJECTED',str(e)[:200],flush=True)
        assert hashfile(src)==stamp
        print('means', {k:statistics.mean(v) if v else None for k,v in times.items()},flush=True)
