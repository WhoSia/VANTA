"""Small deterministic FFmpeg copy-compatibility proxy; no media downloaded."""
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile

def run(args):
    r = subprocess.run(args, capture_output=True, text=True, timeout=30)
    if r.returncode or r.stderr.strip():
        raise RuntimeError(r.stderr[:400])
    return r.stdout

def ff(*args):
    return run(["ffmpeg", "-v", "error", "-xerror", "-nostdin", "-n", *args])

def pts(path, video):
    key = "best_effort_timestamp_time" if video else "pts_time"
    section = "frame" if video else "packet"
    r = json.loads(run(["ffprobe", "-v", "error", "-select_streams", "v:0" if video else "a:0",
                        "-show_" + section + "s", "-show_entries", section + "=" + key + ("" if video else ",data_hash"),
                        *([] if video else ["-show_data_hash", "sha256"]), "-of", "json", str(path)]))
    return [(float(x[key]), x.get("data_hash")) for x in r[section + "s"]]

def pcm(path):
    return ff("-i", str(path), "-map", "0:a:0", "-c:a", "pcm_s32le", "-f", "hash", "-hash", "sha256", "-").strip()

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def compare(src, dst):
    v, w = pts(src, True), pts(dst, True)
    a, b = pts(src, False), pts(dst, False)
    origin = w[0][0] - v[0][0]
    ff("-i", str(dst), "-map", "0:v", "-map", "0:a", "-f", "null", "-")
    return dict(video_count=[len(v), len(w)], audio_count=[len(a), len(b)],
                video_error=max(abs(y[0]-x[0]-origin) for x,y in zip(v,w)),
                audio_error=max(abs(y[0]-x[0]-origin) for x,y in zip(a,b)),
                packet_hash_match=[x[1] for x in a] == [y[1] for y in b],
                pcm_match=pcm(src)==pcm(dst))

def encode(src,dst,pre=(),post=()):
    ff(*pre,"-i",str(src),"-map","0:v","-map","0:a","-c:v","libx264","-preset","fast",
       "-b:v","250k","-threads","2","-fps_mode","passthrough","-c:a","copy",*post,str(dst))

def source(path,codec,delayed=False):
    args = ["-f","lavfi","-i","testsrc2=s=320x180:r=30000/1001:d=3" if delayed else "testsrc2=s=320x180:r=25:d=2"]
    if delayed:
        args += ["-itsoffset","0.15"]
    args += ["-f","lavfi","-i","aevalsrc=0.04*sin(2*PI*440*t)|0.04*sin(2*PI*660*t):s=48000:d=2.85:c=stereo" if delayed else "sine=frequency=440:sample_rate=48000:duration=2",
             "-map","0:v","-map","1:a","-c:v","libx264","-preset","ultrafast","-threads","2","-c:a",codec,str(path)]
    ff(*args)

with tempfile.TemporaryDirectory(prefix="vanta-compat-") as folder:
    p=Path(folder)
    for ext,codec in [("mp4","aac"),("mp4","libmp3lame"),("mp4","ac3"),("mp4","eac3"),("mp4","alac"),
                      ("mov","alac"),("mov","pcm_s16le"),("mkv","aac"),("mkv","libopus"),("mkv","flac"),("mp4","libopus")]:
        src=p/f"{codec}-{ext}.{ext}"
        source(src,codec)
        before=digest(src)
        dst=p/f"{codec}-{ext}-out.mp4"
        encode(src,dst,post=("-movflags","+faststart"))
        print(json.dumps(dict(container=ext,encoder=codec,**compare(src,dst))),flush=True)
        if ext=="mkv":
            for mode,out_ext,pre,post in [("copyts-mp4","mp4",("-copyts",),("-avoid_negative_ts","disabled")),("same-mkv","mkv",(),())]:
                dest=p/f"{codec}-{mode}.{out_ext}"
                encode(src,dest,pre,post)
                print(json.dumps(dict(encoder=codec,mode=mode,**compare(src,dest))),flush=True)
        assert digest(src)==before
    for codec in ("libmp3lame","ac3","eac3","alac"):
        src=p/f"{codec}-stereo.mp4"
        dst=p/f"{codec}-stereo-out.mp4"
        source(src,codec,True)
        before=digest(src)
        encode(src,dst,post=("-movflags","+faststart"))
        print(json.dumps(dict(encoder=codec,mode="delayed-stereo",**compare(src,dst))),flush=True)
        assert digest(src)==before
