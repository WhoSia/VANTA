"""Opt-in synthetic device screen. No user media, no published video. Python 3.11+."""
import argparse, hashlib, json, pathlib, shutil, statistics, subprocess, tempfile, time

def sha(p):
    with open(p,'rb') as f: return hashlib.file_digest(f,'sha256').hexdigest()

def run(args,timeout=90):
    r=subprocess.run([str(v) for v in args],capture_output=True,timeout=timeout)
    if r.returncode or r.stderr.strip(): raise RuntimeError(f'exit={r.returncode}: {r.stderr.decode(errors="replace")[-1200:]}')
    return r.stdout.decode(errors='replace')

def main():
    if not __debug__:raise RuntimeError('Run without Python optimization; assertions are validation gates')
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--report',required=True,type=pathlib.Path);ap.add_argument('--seconds',type=int,choices=[3,9,30],default=9);ap.add_argument('--reps',type=int,choices=[1,2,3],default=2);ap.add_argument('--family',choices=['hevc','h264'],default='hevc');ap.add_argument('--ffmpeg',default='ffmpeg');ap.add_argument('--ffprobe',default='ffprobe');a=ap.parse_args()
    ff=shutil.which(a.ffmpeg);fp=shutil.which(a.ffprobe)
    if not ff or not fp:raise RuntimeError('Matching FFmpeg and FFprobe required')
    fv=run([ff,'-version']).splitlines()[0];pv=run([fp,'-version']).splitlines()[0]
    if fv.split()[2]!=pv.split()[2]:raise RuntimeError('Tool versions differ')
    F=[ff,'-hide_banner','-v','error','-xerror','-nostdin','-n'];P=[fp,'-v','error']
    def pts(p):return [float(x['best_effort_timestamp_time']) for x in json.loads(run(P+['-select_streams','v:0','-show_frames','-show_entries','frame=best_effort_timestamp_time','-of','json',p]))['frames']]
    def audio(p):return json.loads(run(P+['-select_streams','a:0','-show_packets','-show_data_hash','sha256','-show_entries','packet=pts_time,duration_time,data_hash','-of','json',p]))['packets']
    def metadata(p):return json.loads(run(P+['-select_streams','v:0','-show_entries','stream=codec_name,width,height,pix_fmt','-of','json',p]))['streams'][0]
    family='libx265' if a.family=='hevc' else 'libx264';backend=a.family+'_nvenc'
    modes=['cpu-medium','cpu-veryfast','gpu-encode','gpu-resident']
    with a.report.open('x',encoding='utf-8') as report, tempfile.TemporaryDirectory(prefix='vanta-gpu-bench-') as tmp:
        def emit(d):
            line=json.dumps(d,ensure_ascii=False);report.write(line+'\n');report.flush();print(line,flush=True)
        root=pathlib.Path(tmp);src=root/'source.mp4';emit(dict(kind='environment',ffmpeg=fv,ffprobe=pv,ffmpeg_sha=sha(ff),ffprobe_sha=sha(fp),family=a.family,seconds=a.seconds,reps=a.reps,scope='synthetic only, no product timing or device guarantee; CPU automatic threads; identical ABR budget; quality outside timed encode+validation; output retained only in temporary directory'))
        run(F+['-f','lavfi','-i',f'testsrc2=s=1440x810:r=30:d={a.seconds}','-f','lavfi','-i',f'sine=frequency=440:sample_rate=48000:duration={a.seconds}','-map','0:v','-map','1:a','-c:v','libx264','-preset','veryfast','-b:v','2238750','-threads','4','-pix_fmt','yuv420p','-c:a','aac','-b:a','128k',src]);before=sha(src);ref=pts(src);ar=audio(src);assert len(ref)==a.seconds*30 and ar
        emit(dict(kind='source',sha=before,bytes=src.stat().st_size,frames=len(ref),audio_packets=len(ar)))
        for rep in range(a.reps):
            for mode in (modes if rep%2==0 else modes[::-1]):
                dest=root/f'{rep}-{mode}.mp4';log=root/'ssim.log';started=time.monotonic();phase='encoding'
                try:
                    gpu=mode.startswith('gpu');resident=mode=='gpu-resident';args=F+(['-hwaccel','cuda','-hwaccel_output_format','cuda'] if resident else [])+['-i',src,'-map','0:V:0','-c:v',backend if gpu else family]
                    if not gpu:
                        args+=['-preset',mode.split('-')[1]]
                        if family=='libx265':args+=['-x265-params','log-level=error']
                    args+=['-b:v','1791000','-fps_mode','passthrough']
                    args+=['-vf','scale_cuda=-2:810:format=nv12','-pix_fmt','+cuda'] if resident else ['-pix_fmt','yuv420p']
                    args+=['-map','0:a?','-c:a','copy','-tag:v','hvc1' if a.family=='hevc' else 'avc1','-movflags','+faststart',dest]
                    run(args);enc=time.monotonic()-started;phase='validation';t=time.monotonic();v=pts(dest);ao=audio(dest);assert len(v)==len(ref) and len(ao)==len(ar);origin=v[0]-ref[0];drift=max(abs(y-x-origin) for x,y in zip(ref,v));assert drift<=.002
                    assert all(x['data_hash']==y['data_hash'] and abs(float(y['pts_time'])-float(x['pts_time'])-origin)<=.002 and abs(float(x['duration_time'])-float(y['duration_time']))<=.002 for x,y in zip(ar,ao))
                    assert metadata(dest)==dict(codec_name=a.family,width=1440,height=810,pix_fmt='yuv420p')
                    run(F+['-i',dest,'-map','0:V:0','-map','0:a?','-f','null','-']);val=time.monotonic()-t;phase='quality'
                    # Relative path avoids Windows drive-letter filter escaping.
                    rr=subprocess.run(F+['-i',str(src),'-i',str(dest),'-lavfi','ssim=stats_file=ssim.log','-an','-f','null','-'],cwd=root,capture_output=True,timeout=90)
                    if rr.returncode or rr.stderr.strip():raise RuntimeError(rr.stderr.decode(errors='replace')[-1000:])
                    values=[float(next(w[4:] for w in line.split() if w.startswith('All:'))) for line in log.read_text().splitlines()];assert len(values)==len(ref)
                    assert sha(src)==before
                    emit(dict(kind='trial',rep=rep,mode=mode,status='verified',encode_s=enc,validation_s=val,total_s=enc+val,bytes=dest.stat().st_size,sha=sha(dest),frames=len(v),audio_packets=len(ao),drift=drift,mean_ssim=statistics.mean(values),worst_second=min(statistics.mean(values[k:k+30]) for k in range(0,len(values),30))))
                except (RuntimeError,AssertionError,subprocess.TimeoutExpired) as ex:
                    emit(dict(kind='trial',rep=rep,mode=mode,status='rejected',phase=phase,elapsed_s=time.monotonic()-started,error=str(ex)[:1400]))
                finally:
                    dest.unlink(missing_ok=True);log.unlink(missing_ok=True)
                    if sha(src)!=before:raise RuntimeError('Source changed')
        emit(dict(kind='summary',source_unchanged=True,planned_trials=a.reps*len(modes),note='Rejected trials are not successful speed measurements. Compare verified rows and quality/size; no automatic selection.'))
if __name__=='__main__':main()
