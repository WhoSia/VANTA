using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Vanta.Core;

internal static class PipelineScreen
{
    private static string Hash(string path){using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f));}
    private static void Check(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
    private sealed class Progress(Action<BatchUpdate> callback):IProgress<BatchUpdate>{public void Report(BatchUpdate value)=>callback(value);}
    internal static async Task RunAsync(string reportPath,CancellationToken ct)
    {
        var tools=await Toolchain.DiscoverAsync(ct);
        string root=Path.Combine(Path.GetTempPath(),"VANTA-pipeline-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var stream=new FileStream(reportPath,FileMode.CreateNew,FileAccess.Write);
            using var writer=new StreamWriter(stream){AutoFlush=true};
            void Row(object row){string json=JsonSerializer.Serialize(row);writer.WriteLine(json);Console.WriteLine("PIPELINE "+json);}
            string[] sources=[Path.Combine(root,"mixed.mp4"),Path.Combine(root,"motion.mp4")];
            for(int i=0;i<sources.Length;i++)
            {
                var args=new List<string>{"-v","error","-xerror","-nostdin","-n","-f","lavfi","-i","testsrc2=s=1440x810:r=30:d=6","-f","lavfi","-i","sine=frequency=440:sample_rate=48000:duration=6","-map","0:v","-map","1:a"};
                if(i==0)args.AddRange(["-vf","drawbox=x=0:y=0:w=iw:h=ih:color=gray:t=fill:enable=between(t\\,2\\,4)"]);
                args.AddRange(["-c:v","libx264","-preset","veryfast","-b:v","2238750","-threads","4","-pix_fmt","yuv420p","-c:a","aac","-b:a","128k",sources[i]]);
                var made=await ProcessRunner.RunAsync(tools.Ffmpeg,args,ct);
                Check(made.ExitCode==0&&string.IsNullOrWhiteSpace(made.Error),"Fixture failed: "+made.Error);
            }
            var before=sources.Select(Hash).ToArray();var stamps=sources.Select(SourceStamp.Capture).ToArray();
            var version=(await ProcessRunner.RunAsync(tools.Ffmpeg,["-version"],ct)).Output.Split('\n')[0];
            Row(new{kind="environment",version,logicalProcessors=Environment.ProcessorCount,sourceSha=before,threads=4,frames=180});
            var plan=new Plan("Pipeline screen","libx264",810,1791000,0,"veryfast",false);
            BatchJob[] Jobs(string tag)=>sources.Select((src,i)=>new BatchJob(src,Path.Combine(root,tag+"-"+i+".mp4"),plan,stamps[i])).ToArray();
            for(int rep=0;rep<3;rep++)
            {
                var observations=new Dictionary<bool,(double Seconds,string[] Hashes)>();
                foreach(bool overlap in rep%2==0?new[]{false,true}:new[]{true,false})
                {
                    var jobs=Jobs(rep+"-"+overlap);
                    var sw=Stopwatch.StartNew();
                    var events=new System.Collections.Concurrent.ConcurrentQueue<(int Index,string Stage,double Seconds)>();
                    var result=await BatchEncoder.RunPipelineResearchAsync(tools,jobs,new Progress(x=>events.Enqueue((x.Index,x.Stage,sw.Elapsed.TotalSeconds))),ct,overlap,4);
                    double seconds=sw.Elapsed.TotalSeconds;
                    Check(result.Count==jobs.Length&&result.All(x=>x.Stage=="Complete"&&x.Report?.RequestedEncoderThreads==4),"Pipeline completion and equal encoder request");
                    var hashes=jobs.Select(x=>Hash(x.Output)).ToArray();
                    for(int k=0;k<jobs.Length;k++)
                    {
                        await PacketTimeline.ValidateAsync(tools.Ffprobe,jobs[k].Input,jobs[k].Output,1,ct,compareAudioPayloads:true);
                        await Encoder.ValidateAudioAsync(tools.Ffmpeg,jobs[k].Input,jobs[k].Output,ct);
                        Row(new{kind="job",rep,overlap,index=k,sha=hashes[k],report=result[k].Report});
                        File.Delete(jobs[k].Output);
                    }
                    Check(sources.Select(Hash).SequenceEqual(before),"Source changed");
                    observations.Add(overlap,(seconds,hashes));
                    Row(new{kind="trial",rep,overlap,seconds,events=events.Select(x=>new{x.Index,x.Stage,x.Seconds})});
                }
                var a=observations[false];var b=observations[true];
                Check(a.Hashes.SequenceEqual(b.Hashes),"Stage scheduling changed output bytes");
                Row(new{kind="pair",rep,exact=true,reduction=1-b.Seconds/a.Seconds});
            }
            // Pre-cancel and per-item failure return all permits and terminal results.
            using(var stopped=new CancellationTokenSource())
            {
                stopped.Cancel();var jobs=Jobs("pre-cancel");
                var rows=await BatchEncoder.RunPipelineResearchAsync(tools,jobs,new Progress(_=>{}),stopped.Token,true,4);
                Check(rows.All(x=>x.Stage=="Canceled")&&jobs.All(x=>!File.Exists(x.Output)),"Pre-cancel");
            }
            var invalid=Jobs("failure");invalid[0]=invalid[0] with{Plan=plan with{Height=809}};
            var failed=await BatchEncoder.RunPipelineResearchAsync(tools,invalid,new Progress(_=>{}),ct,true,4);
            Check(failed[0].Stage=="Failed"&&!File.Exists(invalid[0].Output)&&failed[1].Stage=="Complete","Failed encoder releases lease");
            File.Delete(invalid[1].Output);
            using(var cancel=CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                var jobs=Jobs("active-cancel");
                var rows=await BatchEncoder.RunPipelineResearchAsync(tools,jobs,new Progress(x=>{if(x.Stage=="Waiting for validation")cancel.Cancel();}),cancel.Token,true,4);
                Check(rows.All(x=>x.Stage=="Canceled")&&jobs.All(x=>!File.Exists(x.Output)),"Cancel before verification publication");
            }
            var mismatch=Jobs("device-mismatch");
            var refused=await BatchEncoder.RunPipelineResearchAsync(tools,mismatch,new Progress(_=>{}),ct,true,backend:"hevc_qsv",resident:true,fast:true);
            Check(refused.All(x=>x.Stage=="Failed")&&mismatch.All(x=>!File.Exists(x.Output)),"Device family mismatch refuses without fallback");
            bool rejectedQueue=false;
            try{await BatchEncoder.RunHardwareTrialAsync(tools,mismatch,"hevc_qsv",new Progress(_=>{}),ct,overlap:true,resident:true,fast:true);}catch(ArgumentException){rejectedQueue=true;}
            Check(rejectedQueue&&mismatch.All(x=>!File.Exists(x.Output)),"Public GPU trial rejects entire mismatched queue before admission");
            Check(sources.Select(Hash).SequenceEqual(before)&&!Directory.EnumerateFileSystemEntries(root,".vanta-*").Any(),"Source identity and staging cleanup");
            Row(new{kind="controls",preCancel=true,activeCancel=true,failureRecovery=true,deviceMismatch=true,sourceUnchanged=true});
        }
        finally{Directory.Delete(root,true);}
    }
}
