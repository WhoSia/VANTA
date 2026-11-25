using System.Globalization;
namespace Vanta.Core;

// Opt-in video-only experiment. Production remains two-way; research may vary segments and workers.
internal static class ExperimentalSplit
{
    private static string Num(double x)=>x.ToString("0.000000000",CultureInfo.InvariantCulture);

    public static async Task EncodeVideoAndMuxAsync(string ffmpeg,string ffprobe,string input,string destination,MediaDetails details,Plan plan,CancellationToken ct,int? encoderThreads=null,int segmentCount=2,int maxWorkers=2,bool keyframeAligned=false,int? researchCrf=null)
    {
        if(segmentCount is <2 or >16||maxWorkers<1||maxWorkers>Math.Min(segmentCount,8)||encoderThreads is int requested&&requested<maxWorkers)
            throw new ArgumentException("Research split requires 2–16 segments, 1–8 workers, and at least one encoder thread per worker.");
        var m=details.Info;
        if(plan.Codec!="libx264"||plan.Height!=m.Height||m.Codec!="h264"||
           !string.Equals(Path.GetExtension(input),".mp4",StringComparison.OrdinalIgnoreCase)||
           !double.IsFinite(m.Fps)||m.Fps<1||m.Fps>120||Math.Abs(details.Timing[0].Start)>.000001)
            throw new NotSupportedException("Experimental split requires zero-start CFR H.264 MP4, H.264 output and original dimensions. Uncheck the experiment for regular encoding.");
        var keyframes=keyframeAligned?new List<long>():null;
        var frames=await ReadGridAsync(ffprobe,input,m.Fps,ct,keyframes);
        if(frames<60||frames>2_000_000||Math.Abs(frames/m.Fps-m.VideoDuration)>.002)
            throw new NotSupportedException("Experimental split requires a complete, regular frame grid and known video duration.");
        if(frames<2L*segmentCount)throw new NotSupportedException("Too few frames for the requested split.");
        var threadsPerWorker=encoderThreads is int budget?budget/maxWorkers:Math.Clamp(Environment.ProcessorCount/maxWorkers,1,4);
        var boundaries=keyframeAligned?SegmentPartition.Balanced(frames,segmentCount,(long)Math.Ceiling(m.Fps),keyframes!):Enumerable.Range(0,segmentCount+1).Select(index=>frames*index/segmentCount).ToArray();
        if(boundaries is null)throw new NotSupportedException("No feasible keyframe partition within the research limits.");
        long Boundary(int index)=>boundaries[index];
        var work=Path.Combine(Path.GetDirectoryName(destination)!, ".vanta-split-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            // Each worker owns a disjoint half-open frame interval. Seek early, then trim
            // using source timestamps (copyts), so a rounded seek cannot define the cut.
            async Task Part(int index,long first,long count,CancellationToken token)
            {
                double boundary=first==0?0:(first-.5)/m.Fps;
                double seek=Math.Max(0,first/m.Fps-1);
                var part=Path.Combine(work,$"part{index}.mp4");
                var args=new List<string>{"-hide_banner","-nostdin","-n","-xerror","-loglevel","error",
                    "-ss",Num(seek),"-copyts","-i",input,"-map","0:V:0","-an",
                    "-vf",$"trim=start={Num(boundary)},setpts=PTS-STARTPTS",
                    "-frames:v",count.ToString(CultureInfo.InvariantCulture),"-c:v","libx264","-preset",plan.Preset,
                    researchCrf.HasValue?"-crf":"-b:v",(researchCrf.HasValue?(long)researchCrf.Value:plan.VideoBitsPerSecond).ToString(CultureInfo.InvariantCulture),"-threads",threadsPerWorker.ToString(CultureInfo.InvariantCulture),
                    "-fps_mode","passthrough","-video_track_timescale","90000",part};
                await Checked(ffmpeg,args,token);
                if(await ReadGridAsync(ffprobe,part,m.Fps,token)!=count)
                    throw new InvalidDataException("Segment frame count changed.");
            }
            using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct);
            int next=-1;
            async Task Worker()
            {
                try
                {
                    while(true)
                    {
                        linked.Token.ThrowIfCancellationRequested();
                        int index=Interlocked.Increment(ref next);
                        if(index>=segmentCount)return;
                        long first=Boundary(index);
                        await Part(index,first,Boundary(index+1)-first,linked.Token);
                    }
                }
                catch{linked.Cancel();throw;}
            }
            await Task.WhenAll(Enumerable.Range(0,maxWorkers).Select(_=>Worker()));
            ct.ThrowIfCancellationRequested();
            // Only generated relative filenames are used; concat safe mode stays on.
            var manifest=Path.Combine(work,"parts.txt");
            var entries=Enumerable.Range(0,segmentCount).Select(index=>$"file 'part{index}.mp4'\nduration {Num((Boundary(index+1)-Boundary(index))/m.Fps)}\n");
            await File.WriteAllTextAsync(manifest,string.Concat(entries),ct);
            await Checked(ffmpeg,["-hide_banner","-nostdin","-n","-xerror","-loglevel","error","-f","concat","-safe","1","-i",manifest,"-i",input,
                "-map","0:v:0","-map","1:a?","-c","copy","-tag:v","avc1","-video_track_timescale","90000","-movflags","+faststart",destination],ct);
        }
        finally { Directory.Delete(work,true); }
    }

    private static async Task<long> ReadGridAsync(string exe,string path,double fps,CancellationToken ct,List<long>? keyframes=null)
    {
        long count=0;
        bool invalid=false;
        // Stream decoded presentation timestamps. No memory growth with frame count.
        var r=await ProcessRunner.RunAsync(exe,["-v","error","-select_streams","v:0","-show_frames","-show_entries",keyframes is null?"frame=best_effort_timestamp_time":"frame=key_frame,best_effort_timestamp_time","-of","compact=p=0:nk=0",path],ct,line=>
        {
            double? timestamp;
            try{timestamp=PacketTimeline.ParseTimestamp(line,"best_effort_timestamp_time");}
            catch(InvalidDataException){invalid=true;return;}
            if(timestamp is null)return;
            if(Math.Abs(timestamp.Value-count/fps)>.0001)invalid=true;
            if(keyframes is not null)
            {
                try
                {
                    var flag=PacketTimeline.ParseTimestamp(line,"key_frame");
                    if(flag==1)
                    {
                        if(keyframes.Count<=SegmentPartition.MaxKeyframes)keyframes.Add(count);
                        else invalid=true;
                    }
                    else if(flag!=0)invalid=true;
                }
                catch(InvalidDataException){invalid=true;}
            }
            count++;
        });
        if(r.ExitCode!=0||!string.IsNullOrWhiteSpace(r.Error)||invalid||count==0)
            throw new NotSupportedException("Experimental split rejected a non-CFR or unreadable frame grid. Use regular encoding.");
        return count;
    }

    private static async Task Checked(string exe,IEnumerable<string> args,CancellationToken ct)
    {
        var r=await ProcessRunner.RunAsync(exe,args,ct);
        if(r.ExitCode!=0||!string.IsNullOrWhiteSpace(r.Error))throw new InvalidDataException(r.Error);
    }
}