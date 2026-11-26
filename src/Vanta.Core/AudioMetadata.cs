using System.Globalization;
using System.Text.Json;
namespace Vanta.Core;

// Audio stream metadata is part of the copy contract, in addition to packet
// payloads and relative timestamps. Never infer an unknown surround layout.
internal sealed record AudioMetadata(string Codec,int Channels,int SampleRate,string Layout)
{
    internal bool Supported=>Codec=="aac"&&SampleRate>0&&(Channels switch
    {
        1=>Layout is "" or "mono",
        2=>Layout is "" or "stereo",
        6=>Layout=="5.1",
        8=>Layout=="7.1",
        _=>false
    });
    internal static AudioMetadata Read(JsonElement stream)
    {
        string Field(string name)=>stream.TryGetProperty(name,out var v)?v.ToString():"";
        int Number(string name)=>int.TryParse(Field(name),NumberStyles.None,CultureInfo.InvariantCulture,out int n)?n:0;
        string layout=Field("channel_layout");
        return new(Field("codec_name"),Number("channels"),Number("sample_rate"),layout is "unknown" or "unspecified" or "N/A"?"":layout);
    }
    internal static void ValidateOutput(IReadOnlyList<AudioMetadata> source,IReadOnlyList<AudioMetadata> output)
    {
        if(source.Count!=output.Count)throw new InvalidDataException("Audio stream layout changed");
        for(int i=0;i<source.Count;i++)
        {
            var a=source[i];var b=output[i];
            if(!a.Supported||!b.Supported||a.Codec!=b.Codec||a.Channels!=b.Channels||a.SampleRate!=b.SampleRate||
                (a.Layout.Length>0&&a.Layout!=b.Layout))
                throw new InvalidDataException($"Audio codec, channels, sample rate or layout changed: track {i+1}");
        }
    }
}
