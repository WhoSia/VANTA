import subprocess,sys
from pathlib import Path
from hashlib import sha256
source,out,mode=sys.argv[1:]
args=['ffprobe','-v','error','-show_streams','-of','json',source] if mode=='probe' else ['ffmpeg','-v','error','-xerror','-nostdin','-n','-i',source,'-c:v','libx264','-preset','fast','-crf','23','-threads','2','-an',out]
for _ in range(5 if mode=='probe' else 1):
    p=subprocess.run(args,capture_output=True,check=True,timeout=30)
    if p.stderr: raise RuntimeError(p.stderr)
print(sha256(p.stdout if mode=='probe' else Path(out).read_bytes()).hexdigest())
