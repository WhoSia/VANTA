using System.Security.Cryptography;
using System.Text.Json;
using Vanta.Core;

internal static class MotionSearchScreen
{
    private sealed record Trial(double Seconds,double Encoding,long Bytes,double Mean,double Worst,int Frames,string Sha);
    private static string Hash(string path){using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f));}
    private static void Check(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
    internal static async Task RunAsync(string reportPath,CancellationToken ct)
    {
        var tools=await Toolchain.DiscoverAsync(ct);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        using var report=new StreamWriter(reportPath,false){AutoFlush=true};
        string root=Path.Combine(Path.GetTempPath(),"VANTA-motion-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        void Row(object x){string line=JsonSerializer.Serialize(x);report.WriteLine(line);Console.WriteLine("MOTION_SEARCH "+line);}
        try
        {
            var ff=await ProcessRunner.RunAsync(tools.Ffmpeg,["-version"],ct);
            Check(ff.ExitCode==0,"Tool version");
            Row(new{kind="environment",commit=Environment.GetEnvironmentVariable("GITHUB_SHA"),ffmpeg=ff.Output.Split('\n')[0],ffmpegSha=Hash(tools.Ffmpeg),ffprobeSha=Hash(tools.Ffprobe),cpuCount=Environment.ProcessorCount});
            (string Id,int Fps,int Seconds)[] fixtures=[("motion",60,6),("mixed",30,8),("texture",60,6)];
            (string Id,string Preset,MotionSearchMode Search)[] modes=[("base","veryfast",MotionSearchMode.Default),("diamond","veryfast",MotionSearchMode.Diamond),("subme1","veryfast",MotionSearchMode.DiamondSubme1),("superfast","superfast",MotionSearchMode.Default)];
            foreach(var fixture in fixtures)
            {
                var (id,fps,duration)=fixture;
                string source=Path.Combine(root,id+".mp4");
                var args=new List<string>{"-v","error","-xerror","-nostdin","-n","-f","lavfi","-i",$"testsrc2=s=1920x1080:r={fps}:d={duration}","-f","lavfi","-i",$"sine=frequency=440:sample_rate=48000:duration={duration}","-map","0:v","-map","1:a"};
                if(id=="mixed")args.AddRange(["-vf",$"drawbox=x=0:y=0:w=iw:h=ih:color=gray:t=fill:enable=lt(t\\,{duration/2})"]);
                if(id=="texture")args.AddRange(["-vf","noise=alls=8:allf=t+u:all_seed=73"]);
                args.AddRange(["-c:v","libx264","-preset","ultrafast","-crf","18","-threads","4","-pix_fmt","yuv420p","-c:a","aac","-b:a","128k",source]);
                var made=await ProcessRunner.RunAsync(tools.Ffmpeg,args,ct);
                Check(made.ExitCode==0&&string.IsNullOrWhiteSpace(made.Error),"Fixture: "+made.Error);
                string before=Hash(source);
                var d=await Probe.InspectAsync(tools.Ffprobe,source,ct);Check(d.Supported,d.Reason);
                var m=d.Info;
                var clock=await PacketTimeline.CaptureSourceClockAsync(tools.Ffprobe,source,ct);
                Check(clock.Count==fps*duration,"Unexpected source count");
                var baseline=new Plan("Motion research","libx264",m.Height,8_000_000,0,"veryfast",false);
                string invalid=Path.Combine(root,id+"-invalid.mp4");
                async Task Reject(Plan plan,MotionSearchMode mode,bool split=false,int? crf=null)
                {
                    bool rejected=false;
                    try{await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,source,invalid,plan,ct,experimentalSplit:split,researchCrf:crf,motionSearch:mode);}
                    catch(ArgumentException){rejected=true;}
                    Check(rejected&&!File.Exists(invalid)&&!Directory.EnumerateFileSystemEntries(root,".vanta-*").Any(),"Invalid research setting must not stage or publish");
                }
                await Reject(baseline,(MotionSearchMode)99);
                await Reject(baseline with {Codec="libx265"},MotionSearchMode.Diamond);
                await Reject(baseline with {Preset="superfast"},MotionSearchMode.Diamond);
                await Reject(baseline,MotionSearchMode.Diamond,true);
                await Reject(baseline,MotionSearchMode.Diamond,crf:23);
                Row(new{kind="fixture",id,fps,duration,sourceSha=before,frames=clock.Count,rate=8_000_000,threads=4});
                for(int rep=0;rep<2;rep++)
                {
                    var trials=new Dictionary<string,Trial>();
                    foreach(var mode in rep==0?modes:modes.Reverse().ToArray())
                    {
                        string dest=Path.Combine(root,$"{id}-{rep}-{mode.Id}.mp4");
                        try
                        {
                            var result=await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,source,dest,baseline with {Preset=mode.Preset},ct,encoderThreads:4,motionSearch:mode.Search);
                            await PacketTimeline.ValidateAsync(tools.Ffprobe,source,dest,m.AudioTracks,ct,compareAudioPayloads:true);
                            var q=await PublicCorpusScreen.Quality(tools,source,dest,m.Fps,ct);
                            Check(q.Frames==clock.Count&&Hash(source)==before,"Frame/source changed");
                            var trial=new Trial(result.Total.TotalSeconds,result.Encoding.TotalSeconds,result.OutputBytes,q.Mean,q.WorstSecond,q.Frames,Hash(dest));trials.Add(mode.Id,trial);
                            Row(new{kind="trial",id,rep,mode=mode.Id,trial});
                        }
                        catch(Exception ex){Row(new{kind="failure",id,rep,mode=mode.Id,error=ex.GetType().Name,message=ex.Message});throw;}
                        finally{if(File.Exists(dest))File.Delete(dest);}
                    }
                    foreach(string anchor in new[]{"base","superfast"})
                    foreach(string candidate in new[]{"diamond","subme1"})
                    {
                        var a=trials[anchor];var b=trials[candidate];double size=(double)b.Bytes/a.Bytes-1,mean=b.Mean-a.Mean,worst=b.Worst-a.Worst;
                        bool matched=a.Frames==b.Frames&&Math.Abs(size)<=.05&&Math.Abs(mean)<=.001&&Math.Abs(worst)<=.005;
                        Row(new{kind="pair",id,rep,anchor,candidate,matched,size,mean,worst,encodingReduction=1-b.Encoding/a.Encoding,timeReduction=1-b.Seconds/a.Seconds});
                    }
                }
                Check(Hash(source)==before,"Source changed");File.Delete(source);
            }
            Row(new{kind="summary",fixtures=fixtures.Length,trials=fixtures.Length*modes.Length*2,pairs=fixtures.Length*2*2*2,interpretation="Research only, no default change. Synthetic fixtures, two reversed repetitions."});
        }
        finally{Directory.Delete(root,true);}
    }
}
