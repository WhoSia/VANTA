using System.Security.Cryptography;
using System.Text.Json;
using Vanta.Core;
internal static class TenBitSdrScreen
{
    private static void Check(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
    private static string Hash(string p){using var f=File.OpenRead(p);return Convert.ToHexString(SHA256.HashData(f));}
    private static async Task Run(Toolchain tools,CancellationToken ct,params string[] args)
    {
        var r=await ProcessRunner.RunAsync(tools.Ffmpeg,args,ct);
        Check(r.ExitCode==0&&string.IsNullOrWhiteSpace(r.Error),"Ten-bit fixture: "+r.Error);
    }
    private static async Task<int> LowBits(Toolchain tools,string src,string raw,CancellationToken ct)
    {
        await Run(tools,ct,"-v","error","-xerror","-nostdin","-n","-i",src,"-map","0:v:0","-frames:v","1","-pix_fmt","yuv420p10le","-f","rawvideo",raw);
        byte[] bytes=await File.ReadAllBytesAsync(raw,ct);
        Check(bytes.Length==320*180*3,"10-bit planar frame bytes");
        int count=0;
        for(int i=0;i<320*180*2;i+=2)
        {
            int v=bytes[i]|bytes[i+1]<<8;
            Check(v<=1023,"Sample depth");
            if((v&3)!=0)count++;
        }
        File.Delete(raw);
        return count;
    }
    internal static async Task RunAsync(CancellationToken ct)
    {
        var tools=await Toolchain.DiscoverAsync(ct);
        string root=Path.Combine(Path.GetTempPath(),"VANTA-tenbit-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var plan=new Plan("Ten-bit SDR","libx265",180,250000,0,"fast",false);
            foreach(string transfer in new[]{"bt709","smpte2084","arib-std-b67"})
            {
                string source=Path.Combine(root,transfer+".mp4"),output=Path.Combine(root,transfer+"-out.mp4");
                string prim=transfer=="bt709"?"bt709":"bt2020",matrix=transfer=="bt709"?"bt709":"bt2020nc";
                await Run(tools,ct,"-v","error","-xerror","-nostdin","-n","-f","lavfi","-i","nullsrc=s=320x180:r=30000/1001:d=2,format=yuv420p10le,geq=lum=64+mod(X+Y+N*3\\,877):cb=512:cr=512","-f","lavfi","-i","sine=frequency=440:sample_rate=48000:duration=2","-map","0:v","-map","1:a","-c:v","libx265","-preset","ultrafast","-threads","2","-x265-params",$"pools=2:log-level=error:lossless=1:colorprim={prim}:transfer={transfer}:colormatrix={matrix}:range=limited","-pix_fmt","yuv420p10le","-c:a","aac",source);
                var before=Hash(source);
                var ordinary=await Probe.InspectAsync(tools.Ffprobe,source,ct);
                Check(!ordinary.Supported&&ordinary.PixelFormat=="yuv420p10le"&&ordinary.Visual?.Transfer==transfer,"Real bit depth and transfer tag");
                var research=await Probe.InspectResearchAsync(tools.Ffprobe,source,ct,true);
                if(transfer!="bt709")
                {
                    Check(!research.Supported,"HDR tags cannot enter SDR lane");
                    bool rejected=false;
                    try{await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,source,output,plan,ct,tenBitSdr:true);}
                    catch(NotSupportedException){rejected=true;}
                    Check(rejected&&!File.Exists(output)&&Hash(source)==before,"HDR rejected without publication");
                    continue;
                }
                Check(research.Supported,"10-bit SDR fixture: "+research.Reason);
                int lowSource=await LowBits(tools,source,Path.Combine(root,"source.raw"),ct);
                Check(lowSource>0,"Source contains actual sub-8-bit-grid luma variation");
                foreach(var invalid in new[]{plan with {Codec="libx264"},plan with{Height=90}})
                {
                    bool rejected=false;
                    try{await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,source,output,invalid,ct,tenBitSdr:true);}
                    catch(NotSupportedException){rejected=true;}
                    Check(rejected&&!File.Exists(output),"Unvalidated ten-bit settings refused");
                }
                var result=await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,source,output,plan,ct,encoderThreads:2,tenBitSdr:true);
                var actual=await Probe.InspectResearchAsync(tools.Ffprobe,output,ct,true);
                Check(actual.Supported&&actual.PixelFormat=="yuv420p10le","10-bit output contract");
                await SingleDecodeScreen.ReferenceAsync(tools,source,output,1,ct);
                await Encoder.ValidateAudioAsync(tools.Ffmpeg,source,output,ct);
                int lowOutput=await LowBits(tools,output,Path.Combine(root,"output.raw"),ct);
                Check(lowOutput>0&&Hash(source)==before,"Output sample variation and source hash");
                var q=await PublicCorpusScreen.Quality(tools,source,output,research.Info.Fps,ct);
                Check(q.Frames==60,"Independent frame quality count");
                Console.WriteLine("TEN_BIT_SDR "+JsonSerializer.Serialize(new{frames=q.Frames,q.Mean,q.WorstSecond,lowSource,lowOutput,seconds=result.Total.TotalSeconds,bytes=result.OutputBytes,sourceSha=before,outputSha=Hash(output),actual.PixelFormat}));
            }
            Check(!Directory.EnumerateFileSystemEntries(root,".vanta-*").Any(),"No scratch leak");
            Console.WriteLine("Ten-bit SDR full encoder, explicit pixel format, independent timeline and HDR refusal passed.");
        }
        finally{Directory.Delete(root,true);}
    }
}
