import subprocess as s, tempfile, pathlib, json, time, hashlib, statistics
from fractions import Fraction

def run(a):
    p=s.run(a,capture_output=True,text=True,timeout=60)
    if p.returncode or p.stderr.strip(): raise RuntimeError((p.returncode,p.stderr[-1500:]))
    return p.stdout
ff=['ffmpeg','-v','error','-xerror','-nostdin']; fp=['ffprobe','-v','error']
def probe(p):
    out=run(fp+['-select_streams','v:0','-show_frames','-show_entries','frame=best_effort_timestamp_time','-of','compact=p=0:nk=0',str(p)])
    return [float(l.split('|')[0].split('=')[1]) for l in out.splitlines() if l.startswith('best_effort_timestamp_time=')]
def strict(p,stats=False):
    a=ff+(['-copyts'] if stats else [])+['-i',str(p),'-map','0:V:0','-map','0:a?']
    if stats:a+=['-fps_mode','passthrough','-stats_enc_pre:v','pipe:1','-stats_enc_pre_fmt:v','{n} {ptsi} {tbi}']
    out=run(a+['-f','null','-'])
    if stats:
        result=[]
        for l in out.splitlines():
            n,pts,tb=l.split();assert int(n)==len(result);assert int(pts) not in (-2**63,2**63-1);assert Fraction(tb)>0;result.append(float(int(pts)*Fraction(tb)))
        return result
with tempfile.TemporaryDirectory() as d:
    d=pathlib.Path(d)
    for case,sz,rate,flt in [('ntsc','640x360','30000/1001','null'),('vfr','640x360','30','select=not(mod(n\\,2))+not(mod(n\\,5))'),('1080p','1920x1080','30','null')]:
        p=d/(case+'.mp4');run(ff+['-n','-f','lavfi','-i',f'testsrc2=s={sz}:r={rate}:d=3','-f','lavfi','-i','sine=frequency=440:duration=3','-map','0:v','-map','1:a','-vf',flt,'-fps_mode','passthrough','-c:v','libx264','-preset','veryfast','-threads','4','-c:a','aac',str(p)])
        reference=probe(p);before=hashlib.sha256(p.read_bytes()).hexdigest();rows=[]
        for rep in range(5):
            for mode in (['serial','one'] if rep%2==0 else ['one','serial']):
                t=time.perf_counter()
                if mode=='serial': values=probe(p);strict(p)
                else:values=strict(p,True)
                dur=time.perf_counter()-t;assert len(values)==len(reference);err=max(abs(a-b) for a,b in zip(values,reference));assert err<=.000001
                rows.append(dict(rep=rep,mode=mode,seconds=dur,count=len(values),max_error=err))
        assert hashlib.sha256(p.read_bytes()).hexdigest()==before
        print(json.dumps(dict(case=case,source_sha256=before,rows=rows,median={m:statistics.median(x['seconds'] for x in rows if x['mode']==m) for m in ['serial','one']})),flush=True)
