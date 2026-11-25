using System.Text.Json;
namespace Vanta.Core;

// Explicitly saved defaults only. No media paths, history, or experimental switches.
public sealed record BatchPreferences(string Tier="Balanced",string? Codec=null,int ParallelJobs=2,int? EncoderThreads=null)
{
    public void Validate()
    {
        if(Tier is not ("Quality" or "Balanced" or "Compact")||
           (Codec is not null&&!PlanRules.Codecs.Contains(Codec)))
            throw new ArgumentException("Invalid saved profile or codec.");
        ThreadBudget.PerJob(EncoderThreads,ParallelJobs);
    }
    public static BatchPreferences Load(string path)
    {
        if(!File.Exists(path))return new();
        using var stream=File.OpenRead(path);
        if(stream.Length>16384)throw new InvalidDataException("Settings file is too large.");
        var value=JsonSerializer.Deserialize<BatchPreferences>(stream)??throw new InvalidDataException("Empty settings.");
        value.Validate();return value;
    }
    public void Save(string path)
    {
        Validate();path=Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {
                JsonSerializer.Serialize(stream,this);stream.Flush(true);
            }
            File.Move(temp,path,true);
        }
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
}