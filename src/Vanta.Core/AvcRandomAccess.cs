using System.Globalization;
using System.Text.Json;
namespace Vanta.Core;

// Research-only MP4 AVC sample inspection. IDR presence is a conservative
// anchor candidate, not a standalone guarantee of seek correctness.
internal static class AvcRandomAccess
{
    internal sealed record Candidate(double Pts,bool IsIdr);
    internal static bool IsIdrSample(ReadOnlySpan<byte> sample,int width)
    {
        if(width is not (1 or 2 or 4)||sample.IsEmpty)throw new InvalidDataException("Invalid AVC sample framing");
        bool idr=false,other=false;
        while(!sample.IsEmpty)
        {
            if(sample.Length<width)throw new InvalidDataException("Truncated AVC NAL length");
            uint length=0;
            for(int i=0;i<width;i++)length=(length<<8)|sample[i];
            sample=sample[width..];
            if(length==0||length>(uint)sample.Length||(sample[0]&128)!=0)
                throw new InvalidDataException("Malformed AVC NAL");
            int type=sample[0]&31;
            if(type==5)idr=true;
            else if(type is not (6 or 7 or 8 or 9 or 10 or 11 or 12))other=true;
            sample=sample[(int)length..];
        }
        return idr&&!other;
    }

    internal static async Task<Candidate[]> ReadAsync(string ffprobe,string path,CancellationToken ct)
    {
        if(!string.Equals(Path.GetExtension(path),".mp4",StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("AVC anchor research requires MP4");
        using var source=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        var info=await ProcessRunner.RunAsync(ffprobe,["-v","error","-select_streams","v:0","-show_entries","stream=codec_name,is_avc,nal_length_size","-of","json",path],ct);
        if(info.ExitCode!=0||!string.IsNullOrWhiteSpace(info.Error))throw new InvalidDataException("AVC stream probe failed");
        using var doc=JsonDocument.Parse(info.Output);
        var stream=doc.RootElement.GetProperty("streams").EnumerateArray().Single();
        if(stream.GetProperty("codec_name").GetString()!="h264"||
            !string.Equals(stream.GetProperty("is_avc").ToString(),"true",StringComparison.OrdinalIgnoreCase)||
            !int.TryParse(stream.GetProperty("nal_length_size").ToString(),NumberStyles.None,CultureInfo.InvariantCulture,out int width)||
            width is not (1 or 2 or 4))throw new NotSupportedException("Unsupported AVC framing");
        var result=new List<Candidate>();
        long packets=0,totalBytes=0;
        var scan=await ProcessRunner.RunAsync(ffprobe,["-v","error","-select_streams","v:0","-show_packets","-show_entries","packet=pts_time,pos,size,flags","-of","compact=p=1:nk=0",path],ct,line=>
        {
            if(string.IsNullOrWhiteSpace(line))return;
            if(!line.StartsWith("packet|",StringComparison.Ordinal)||++packets>2_000_000)
                throw new InvalidDataException("Invalid or oversized AVC packet index");
            var fields=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(string field in line.Split('|').Skip(1))
            {
                int separator=field.IndexOf('=');
                if(separator<=0||!fields.TryAdd(field[..separator],field[(separator+1)..]))
                    throw new InvalidDataException("Malformed AVC packet index");
            }
            if(!fields.TryGetValue("flags",out string? flags))throw new InvalidDataException("Missing packet flags");
            if(!flags.Contains('K')||flags.Contains('D'))return;
            if(result.Count>=SegmentPartition.MaxKeyframes||
                !fields.TryGetValue("pos",out string? posText)||!long.TryParse(posText,NumberStyles.None,CultureInfo.InvariantCulture,out long pos)||
                !fields.TryGetValue("size",out string? sizeText)||!int.TryParse(sizeText,NumberStyles.None,CultureInfo.InvariantCulture,out int size)||
                !fields.TryGetValue("pts_time",out string? ptsText)||!double.TryParse(ptsText,NumberStyles.Float,CultureInfo.InvariantCulture,out double pts)||!double.IsFinite(pts)||
                pos<0||size<1||size>16*1024*1024||pos>source.Length-size||
                (totalBytes+=size)>128L*1024*1024)
                throw new InvalidDataException("Invalid or oversized AVC key sample");
            ct.ThrowIfCancellationRequested();
            source.Position=pos;
            byte[] sample=new byte[size];
            source.ReadExactly(sample);
            result.Add(new(pts,IsIdrSample(sample,width)));
        });
        if(scan.ExitCode!=0||!string.IsNullOrWhiteSpace(scan.Error)||result.Count==0)
            throw new InvalidDataException("AVC packet index unavailable");
        var sorted=result.OrderBy(x=>x.Pts).ToArray();
        for(int i=1;i<sorted.Length;i++)
            if(sorted[i].Pts<=sorted[i-1].Pts)throw new InvalidDataException("Duplicate AVC key sample PTS");
        return sorted;
    }

    internal static double? PreviousIdr(IReadOnlyList<Candidate> candidates,double target)
    {
        if(!double.IsFinite(target))throw new ArgumentException("Nonfinite target");
        double? result=null;
        foreach(var candidate in candidates)
            if(candidate.IsIdr&&double.IsFinite(candidate.Pts)&&candidate.Pts<=target&&
                (!result.HasValue||candidate.Pts>result.Value))result=candidate.Pts;
        return result;
    }
}
