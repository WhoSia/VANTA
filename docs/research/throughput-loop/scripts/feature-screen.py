import hashlib,json,math,pathlib,statistics,subprocess,time,sys
W,H,R=64,36,4
N=W*H

def run(args):
    p=subprocess.run(args,capture_output=True,timeout=90,check=True)
    if p.stderr.strip():raise RuntimeError(p.stderr.decode(errors='replace')[-500:])
    return p.stdout

def digest(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()

def thumb(p,start=None):
    args=['ffmpeg','-v','error','-xerror','-nostdin']
    if start is not None:args+=['-ss',str(start)]
    args+=['-i',str(p),'-map','0:v:0','-an','-sn','-dn','-vf','fps=4,scale=64:36:flags=area,format=gray']
    if start is not None:args+=['-frames:v','4']
    out=run(args+['-f','rawvideo','-'])
    if len(out)%N:raise ValueError('partial frame')
    return [out[i:i+N] for i in range(0,len(out),N)]

def feature(frames):
    if len(frames)!=R:raise ValueError('window count')
    s=sum(sum(abs(f[i]-f[i-1]) for i in range(N) if i%W)+sum(abs(f[i]-f[i-W]) for i in range(W,N)) for f in frames)/(R*N*2*255)
    t=sum(sum(abs(a-b) for a,b in zip(x,y)) for x,y in zip(frames,frames[1:]))/((R-1)*N*255)
    return [s,t]

def measure(p):
    before=digest(p)
    meta=json.loads(run(['ffprobe','-v','error','-show_entries','format=duration','-of','json',str(p)]))
    secs=int(float(meta['format']['duration'])+1e-6)
    start=time.monotonic();pack=json.loads(run(['ffprobe','-v','error','-select_streams','v:0','-show_packets','-show_entries','packet=size','-of','json',str(p)]))['packets'];packet_s=time.monotonic()-start
    sizes=[int(x['size']) for x in pack];cv=statistics.pstdev(sizes)/statistics.mean(sizes)
    start=time.monotonic();frames=thumb(p);full=[feature(frames[i:i+R]) for i in range(0,secs*R,R)];full_s=time.monotonic()-start
    windows=[0,secs//2,secs-1];start=time.monotonic();samples=[thumb(p,n) for n in windows];sample_f=[feature(a) for a in samples];sample_s=time.monotonic()-start
    equal=[a==frames[n*R:(n+1)*R] for a,n in zip(samples,windows)]
    errs=[max(abs(x-y) for x,y in zip(f,full[n])) for f,n in zip(sample_f,windows)]
    assert digest(p)==before
    return dict(source=str(p.name),parent=p.parent.name,bytes=p.stat().st_size,sha=before,seconds=secs,packet_count=len(sizes),packet_cv=cv,packet_s=packet_s,full_s=full_s,sample_s=sample_s,windows=windows,pixel_equal=equal,feature_error=errs,full=full,sample=sample_f,full_max=max(x[1] for x in full),sample_max=max(x[1] for x in sample_f))

if __name__=='__main__':
    for arg in sys.argv[1:]:print(json.dumps(measure(pathlib.Path(arg))),flush=True)
