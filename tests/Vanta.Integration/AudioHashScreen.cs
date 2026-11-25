using System.Diagnostics;
using Vanta.Core;
internal static class AudioHashScreen
{
    public static async Task RunAsync(string ffmpeg,string source,string output,string multiSource,string multiOutput,string original,string changed,CancellationToken ct)
    {
        foreach(bool parallel in new[]{false,true})
        {
            await Encoder.ValidateAudioAsync(ffmpeg,multiSource,multiOutput,ct,parallel);
            bool rejected=false;
            try{await Encoder.ValidateAudioAsync(ffmpeg,original,changed,ct,parallel);}
            catch(InvalidDataException ex) when(ex.Message.Contains("Copied audio payload changed")){rejected=true;}
            if(!rejected)throw new Exception("Audio hash screen accepted substituted audio.");
            using var canceled=new CancellationTokenSource();canceled.Cancel();
            rejected=false;
            try{await Encoder.ValidateAudioAsync(ffmpeg,source,output,canceled.Token,parallel);}
            catch(OperationCanceledException){rejected=true;}
            if(!rejected)throw new Exception("Audio hash screen ignored pre-cancellation.");
        }
        foreach(int jobs in new[]{1,2})
        for(int rep=0;rep<4;rep++)
        foreach(bool parallel in rep%2==0?new[]{false,true}:new[]{true,false})
        {
            ct.ThrowIfCancellationRequested();
            var watch=Stopwatch.StartNew();
            var first=Encoder.ValidateAudioAsync(ffmpeg,source,output,ct,parallel);
            if(jobs==2)await Task.WhenAll(first,Encoder.ValidateAudioAsync(ffmpeg,multiSource,multiOutput,ct,parallel));
            else await first;
            Console.WriteLine(FormattableString.Invariant($"AUDIO_HASH_SHADOW jobs={jobs} rep={rep} parallel={parallel} seconds={watch.Elapsed.TotalSeconds:F6}"));
        }
        Console.WriteLine("Audio hash serial/parallel differential, substitution and pre-cancel controls passed.");
    }
}
