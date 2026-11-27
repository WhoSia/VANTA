"""Reproduce the short-fixture source clock overlap experiment.
Requires exact retained inputs and reference outputs from owner-810p-speed-local.json.
No production path; no output publication. Python 3.11+, FFmpeg/FFprobe.
Example: python source-overlap-screen.py FIXTURES results.jsonl mixed libx264 0 ordinary
Run all 2 fixtures x 2 codecs x 2 reps x 2 modes, alternating order.
"""
import concurrent.futures as cf, hashlib, json, pathlib, subprocess as sp, time, sys, tempfile
F=['ffmpeg','-hide_banner','-v','error','-xerror','-nostdin','-n']; P=['ffprobe','-v','error']
def run(a):
    r=sp.run(list(map(str,a)),capture_output=True,timeout=40,check=True)
    if r.stderr.strip():raise RuntimeError(r.stderr.decode(errors='replace')[-1000:])
    return r.stdout.decode()
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def pts(p):return [float(x['best_effort_timestamp_time']) for x in json.loads(run(P+['-select_streams','v:0','-show_frames','-show_entries','frame=best_effort_timestamp_time','-of','json',p]))['frames']]
def audio(p):return json.loads(run(P+['-select_streams','a:0','-show_packets','-show_data_hash','sha256','-show_entries','packet=pts_time,duration_time,data_hash','-of','json',p]))['packets']
def strict(p):
    v=[]
    for line in run(F+['-copyts','-i',p,'-map','0:V:0','-map','0:a?','-fps_mode','passthrough','-stats_enc_pre:v','pipe:1','-stats_enc_pre_fmt:v','{n} {ptsi} {tbi}','-f','null','-']).splitlines():
        n,pt,tb=line.split();a,b=map(int,tb.split('/'));assert int(n)==len(v);v.append(int(pt)*a/b)
    return v
def hash_audio(p):return run(F+['-i',p,'-map','0:a','-c:a','copy','-f','streamhash','-hash','sha256','-']).strip()
if not __debug__:raise RuntimeError('Assertions required')
directory,report,id,codec,rep,mode=sys.argv[1:];rep=int(rep);I=pathlib.Path(directory).resolve();report=pathlib.Path(report).resolve()
assert mode in ['ordinary','overlap'] and codec in ['libx264','libx265'] and id in ['mixed','motion'] and rep in [0,1]
expected={
'mixed':('f74cf65bfae76f1c6fad22181350ac7854757ecbfee8335b74a8b9da985f0578','ae97455e3c4c48949442020f0f2e1c1d0951d3cc25e34f496a73d7b67c865382','5ee6d19f957010a760cd6f6c150180bc4bbb99cb396d5c06147f36aa320330d2'),
'motion':('26bc545c9d87790ed13c2c6f902395e335083c3a83155b8c0b334ecccbe53cb6','c7c88f8306ec896da00efcdbb30cdd85cde31d187427c304a62998f4aacd569b','7ac276e806daeff52a77505b3e6460cf6a58f629a52c47cd825b30e9f6add604')}
p=I/(id+'.mp4');refout=I/(id+'-'+codec+'-veryfast-0.mp4')
assert report.suffix=='.jsonl' and report.parent!=I and report!=p and report!=refout
srcsha,outsha=sha(p),sha(refout);assert srcsha==expected[id][0] and outsha==expected[id][1 if codec=='libx264' else 2]
a0,b0=pts(p),pts(refout);ar,ao=audio(p),audio(refout)
assert len(a0)==len(b0)==270 and len(ar)==len(ao)==423 and hash_audio(p)==hash_audio(refout)
decoded=strict(refout);assert len(decoded)==270 and max(abs(a-b) for a,b in zip(decoded,b0))<=1e-6
with tempfile.TemporaryDirectory(prefix='vanta-source-overlap-') as tmp:
    q=pathlib.Path(tmp)/'out.mp4';started=time.monotonic()
    def timed(fn):
        s=time.monotonic();v=fn();return v,s-started,time.monotonic()-started
    def enc():
        args=F+['-i',p,'-map','0:v','-map','0:a','-c:v',codec,'-preset','veryfast','-b:v','1791000','-threads','4']
        if codec=='libx265':args+=['-x265-params','pools=4:log-level=error']
        return run(args+['-fps_mode','passthrough','-c:a','copy','-movflags','+faststart',q])
    def source():return pts(p)
    with cf.ThreadPoolExecutor(max_workers=2) as pool:
        if mode=='overlap':
            f=pool.submit(timed,source);_,eb,ee=timed(enc);a,ab,ae=f.result();b,bb,be=timed(lambda:strict(q))
        else:
            _,eb,ee=timed(enc);f=pool.submit(timed,source);b,bb,be=timed(lambda:strict(q));a,ab,ae=f.result()
        f,g=pool.submit(audio,p),pool.submit(audio,q);ap,bp=f.result(),g.result()
    assert a==a0 and len(b)==270 and max(abs(x-y) for x,y in zip(b,b0))<=1e-6 and ap==ar and bp==ao
    origin=b[0]-a[0];drift=max(abs(y-x-origin) for x,y in zip(a,b));assert drift<=.002
    assert all(x['data_hash']==y['data_hash'] and abs(float(y['pts_time'])-float(x['pts_time'])-origin)<=.002 and abs(float(x['duration_time'])-float(y['duration_time']))<=.002 for x,y in zip(ap,bp))
    assert sha(p)==srcsha and sha(q)==outsha and sha(refout)==outsha
    row=dict(id=id,codec=codec,rep=rep,mode=mode,total_s=time.monotonic()-started,encode_begin=eb,encode_end=ee,source_begin=ab,source_end=ae,output_begin=bb,output_end=be,frames=270,audio_packets=423,drift=drift,source_sha=srcsha,output_sha=outsha)
    with report.open('a',encoding='utf-8') as f:f.write(json.dumps(row)+'\n')
    print(json.dumps(row),flush=True)
