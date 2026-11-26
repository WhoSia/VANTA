using System.Security.Cryptography;
using Vanta.Core;
internal static class ExtendedAudioScreen
{
    private static void Check(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
    private static string Hash(string path){using var s=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(s));}
    private static bool KnownMismatch(InvalidDataException ex)
        =>new[]{"Decoded audio samples changed:","Audio packet payload changed in a:","Timestamp count mismatch: a:","Timestamp drift in a:","Copied audio payload changed","Audio codec, channels, sample rate or layout changed:","Relative stream timing changed: audio"}.Any(prefix=>ex.Message.StartsWith(prefix,StringComparison.Ordinal));
    private static async Task<bool> Trial(Toolchain tools,string root,string source,string output,Plan plan,string codec,CancellationToken ct)
    {
        string before=Hash(source);
        var ordinary=await Probe.InspectAsync(tools.Ffprobe,source,ct);
        bool researchOnly=codec!="mp3";
        Check(ordinary.Supported==!researchOnly,"Ordinary codec eligibility: "+ordinary.Reason);
        if(researchOnly)
        {
            bool refused=false;
            try{await Encoder.EncodeAsync(tools.Ffmpeg,tools.Ffprobe,source,output,plan,ct);}
            catch(NotSupportedException){refused=true;}
            Check(refused&&!File.Exists(output),"Research-only codec cannot publish in ordinary mode");
        }
        var original=await Probe.InspectResearchAsync(tools.Ffprobe,source,ct,false,researchOnly);
        Check(original.Supported&&original.Audio.Single().Codec==codec,"Extended source: "+original.Reason);
        EncodeReport result;
        try
        {
            result=await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,source,output,plan,ct,encoderThreads:2,extendedAudioResearch:researchOnly);
        }
        catch(InvalidDataException ex) when(researchOnly&&KnownMismatch(ex)&&!File.Exists(output))
        {
            Check(Hash(source)==before&&!Directory.EnumerateFileSystemEntries(root,".vanta-*").Any(),"Rejected audio cleanup and source identity");
            Console.WriteLine($"EXTENDED_AUDIO_REFUSED {codec} {ex.Message}");
            return false;
        }
        Check(result.AudioHashCheck>TimeSpan.Zero&&File.Exists(output),"Decoded audio check accounted");
        await SingleDecodeScreen.ReferenceAsync(tools,source,output,1,ct);
        await DecodedAudioIdentity.ValidateAsync(tools.Ffmpeg,source,output,1,ct);
        var actual=await Probe.InspectResearchAsync(tools.Ffprobe,output,ct,false,researchOnly);
        Check(actual.Supported&&actual.Audio.Single()==original.Audio.Single()&&Hash(source)==before,"Extended metadata and source identity");
        Console.WriteLine($"EXTENDED_AUDIO_VERIFIED {codec} researchOnly={researchOnly}");
        return true;
    }
    internal static async Task RunAsync(Toolchain tools,string root,CancellationToken ct)
    {
        var plan=new Plan("Extended audio","libx264",180,250000,0,"fast",false);
        foreach(var (encoder,codec,extension) in new[]{("libmp3lame","mp3","mp4"),("ac3","ac3","mp4"),("eac3","eac3","mp4"),("alac","alac","mp4"),("alac","alac","mov")})
        {
            string source=Path.Combine(root,$"extended-{codec}-{extension}.{extension}"),output=Path.Combine(root,$"extended-{codec}-{extension}-out.mp4");
            var generated=await ProcessRunner.RunAsync(tools.Ffmpeg,["-v","error","-xerror","-nostdin","-n","-f","lavfi","-i","testsrc2=s=320x180:r=25:d=2","-f","lavfi","-i","sine=frequency=440:sample_rate=48000:duration=2","-map","0:v","-map","1:a","-c:v","libx264","-preset","ultrafast","-threads","2","-pix_fmt","yuv420p","-c:a",encoder,source],ct);
            Check(generated.ExitCode==0&&string.IsNullOrWhiteSpace(generated.Error),"Extended fixture creation: "+generated.Error);
            await Trial(tools,root,source,output,plan,codec,ct);
        }
        // Distinct stereo channels with delayed audio at fractional frame rate.
        // ALAC edit-list trimming can vary across muxer versions: require
        // either verified publication or a clean refusal, never weaken checks.
        foreach(var (encoder,codec) in new[]{("libmp3lame","mp3"),("ac3","ac3"),("eac3","eac3"),("alac","alac")})
        {
            string source=Path.Combine(root,$"extended-delayed-{codec}.mp4"),output=Path.Combine(root,$"extended-delayed-{codec}-out.mp4");
            var made=await ProcessRunner.RunAsync(tools.Ffmpeg,["-v","error","-xerror","-nostdin","-n","-f","lavfi","-i","testsrc2=s=320x180:r=30000/1001:d=3","-itsoffset","0.15","-f","lavfi","-i","aevalsrc=0.04*sin(2*PI*440*t)|0.04*sin(2*PI*660*t):s=48000:d=2.85:c=stereo","-map","0:v","-map","1:a","-c:v","libx264","-preset","ultrafast","-threads","2","-pix_fmt","yuv420p","-c:a",encoder,source],ct);
            Check(made.ExitCode==0&&string.IsNullOrWhiteSpace(made.Error),"Delayed stereo fixture");
            await Trial(tools,root,source,output,plan,codec,ct);
        }
        // Compare different valid decoded audio; no fixture-specific assumption
        // about a particular muxer's edit-list behavior.
        string a=Path.Combine(root,"extended-mp3-mp4.mp4");
        string changed=Path.Combine(root,"extended-changed.mp4");
        var changedResult=await ProcessRunner.RunAsync(tools.Ffmpeg,["-v","error","-xerror","-nostdin","-n","-i",a,"-f","lavfi","-i","sine=frequency=880:sample_rate=48000:duration=2","-map","0:v","-map","1:a","-c:v","copy","-c:a","libmp3lame",changed],ct);
        Check(changedResult.ExitCode==0&&string.IsNullOrWhiteSpace(changedResult.Error),"Changed audio fixture");
        bool rejected=false;
        try{await DecodedAudioIdentity.ValidateAsync(tools.Ffmpeg,a,changed,1,ct);}catch(InvalidDataException){rejected=true;}
        Check(rejected,"Decoded audio guard rejected substituted samples");
        string split=Path.Combine(root,"extended-split.mp4");
        rejected=false;try{await Encoder.EncodeAsync(tools.Ffmpeg,tools.Ffprobe,a,split,plan,ct,experimentalSplit:true);}catch(NotSupportedException){rejected=true;}
        Check(rejected&&!File.Exists(split),"Extended split cannot publish");
        Check(!new AudioMetadata("opus",2,48000,"stereo").Supported&&!new AudioMetadata("ac3",6,48000,"5.1").Supported,"Unproven audio layouts/codecs remain blocked");
        Console.WriteLine("Extended audio copy, independent PCM, substitution and unsupported controls passed.");
    }
}
