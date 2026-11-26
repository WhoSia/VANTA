using System.Text.Json;
namespace Vanta.Core;

// Narrow research contract, not a general HDR detector. Explicit BT.709
// 10-bit 4:2:0 only. Unrecognized HDR or other side data is not admitted.
internal static class TenBitSdrPolicy
{
    internal static bool Eligible(JsonElement video,VisualMetadata visual,bool research)
    {
        string Field(string key)=>video.TryGetProperty(key,out var v)?v.ToString():"";
        string pixel=Field("pix_fmt");
        if(pixel=="yuv420p")return visual.Transfer is not ("smpte2084" or "arib-std-b67");
        if(!research||pixel!="yuv420p10le"||visual.HasTransform||
            visual.Range!="tv"||visual.Space!="bt709"||visual.Transfer!="bt709"||visual.Primaries!="bt709")return false;
        // Deliberately reject any stream side data for this narrow lane.
        if(video.TryGetProperty("side_data_list",out var side)&&(side.ValueKind!=JsonValueKind.Array||side.GetArrayLength()!=0))return false;
        return true;
    }
}
