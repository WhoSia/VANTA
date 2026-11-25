using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Vanta.Core;

internal static class NativeKernelScreen
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate long NativeCount(IntPtr data,nuint length,uint width);
    private static long Managed(byte[] data,int width)
    {
        if(data.Length==0||data.Length>128*1024*1024||width is not (1 or 2 or 4))return -1;
        var remaining=data.AsSpan();
        long total=0;
        try
        {
            while(!remaining.IsEmpty)
            {
                if(remaining.Length<4)return -1;
                uint size=BinaryPrimitives.ReadUInt32LittleEndian(remaining);
                remaining=remaining[4..];
                if(size==0||size>(uint)remaining.Length)return -1;
                if(AvcRandomAccess.IsIdrSample(remaining[..(int)size],width))total++;
                remaining=remaining[(int)size..];
            }
        }
        catch(InvalidDataException){return -1;}
        return total;
    }
    private static byte[] Pack(IEnumerable<byte[]> samples)
    {
        using var stream=new MemoryStream();
        var length=new byte[4];
        foreach(var sample in samples)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(length,(uint)sample.Length);
            stream.Write(length);stream.Write(sample);
        }
        return stream.ToArray();
    }
    private static long Invoke(NativeCount call,byte[] data,int width)
    {
        var pin=GCHandle.Alloc(data,GCHandleType.Pinned);
        try{return call(pin.AddrOfPinnedObject(),(nuint)data.Length,(uint)width);}
        finally{pin.Free();}
    }
    private static void Check(byte[] data,int width,NativeCount c,NativeCount rust)
    {
        long expected=Managed(data,width);
        if(Invoke(c,data,width)!=expected||Invoke(rust,data,width)!=expected)
            throw new InvalidDataException("Native differential mismatch");
    }
    private static async Task<byte[][]> Samples(Toolchain tools,string file,CancellationToken ct)
    {
        var r=await ProcessRunner.RunAsync(tools.Ffprobe,["-v","error","-select_streams","v:0","-show_entries","stream=nal_length_size:packet=pos,size,flags","-show_packets","-show_streams","-of","json",file],ct);
        if(r.ExitCode!=0||!string.IsNullOrWhiteSpace(r.Error))throw new InvalidDataException("Packet probe failed");
        using var doc=JsonDocument.Parse(r.Output);
        var width=doc.RootElement.GetProperty("streams").EnumerateArray().Single().GetProperty("nal_length_size").ToString();
        if(width!="4")throw new InvalidDataException("Unexpected fixture NAL width");
        using var source=File.OpenRead(file);
        var samples=new List<byte[]>();
        foreach(var packet in doc.RootElement.GetProperty("packets").EnumerateArray())
        {
            if(!(packet.GetProperty("flags").GetString()??"").Contains('K'))continue;
            long offset=long.Parse(packet.GetProperty("pos").ToString(),CultureInfo.InvariantCulture);
            int size=int.Parse(packet.GetProperty("size").ToString(),CultureInfo.InvariantCulture);
            if(offset<0||size<1||size>16*1024*1024||offset>source.Length-size||samples.Count>=512)throw new InvalidDataException("Invalid key sample bounds");
            source.Position=offset;var bytes=new byte[size];source.ReadExactly(bytes);samples.Add(bytes);
        }
        if(samples.Count<2)throw new InvalidDataException("Too few key samples");
        return samples.ToArray();
    }
    internal static async Task RunAsync(string cPath,string rustPath,CancellationToken ct)
    {
        IntPtr cLib=NativeLibrary.Load(Path.GetFullPath(cPath));
        IntPtr rLib=IntPtr.Zero;
        string root=Path.Combine(Path.GetTempPath(),"vanta-native-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            rLib=NativeLibrary.Load(Path.GetFullPath(rustPath));
            var c=Marshal.GetDelegateForFunctionPointer<NativeCount>(NativeLibrary.GetExport(cLib,"vanta_count_idr"));
            var rust=Marshal.GetDelegateForFunctionPointer<NativeCount>(NativeLibrary.GetExport(rLib,"vanta_count_idr"));
            var random=new Random(20261126);
            int cases=0;
            foreach(int width in new[]{1,2,4,3})
            for(int i=0;i<1000;i++)
            {
                ct.ThrowIfCancellationRequested();
                byte[] data;
                if(i%2==0){data=new byte[random.Next(60)];random.NextBytes(data);}
                else
                {
                    using var sample=new MemoryStream();
                    for(int k=0;k<random.Next(1,8);k++)
                    {
                        var nal=new byte[random.Next(1,11)];random.NextBytes(nal);nal[0]&=127;
                        for(int j=width-1;j>=0;j--)sample.WriteByte((byte)(nal.Length>>(8*j)));
                        sample.Write(nal);
                    }
                    data=Pack([sample.ToArray()]);
                }
                Check(data,width,c,rust);cases++;
            }
            Console.WriteLine("NATIVE_DIFFERENTIAL cases="+cases);
            var tools=await Toolchain.DiscoverAsync(ct);
            foreach(int open in new[]{0,1})
            {
                string file=Path.Combine(root,$"sample-{open}.mp4");
                var fixture=await ProcessRunner.RunAsync(tools.Ffmpeg,["-v","error","-xerror","-nostdin","-n","-f","lavfi","-i","testsrc2=s=320x180:r=30:d=4","-an","-c:v","libx264","-preset","fast","-crf","18","-g","30","-keyint_min","30","-sc_threshold","0","-x264-params",$"open-gop={open}","-threads","4","-pix_fmt","yuv420p",file],ct);
                if(fixture.ExitCode!=0||!string.IsNullOrWhiteSpace(fixture.Error))throw new InvalidDataException("Fixture failed");
                var samples=await Samples(tools,file,ct);
                var packed=Pack(Enumerable.Range(0,128).SelectMany(_=>samples));
                Check(packed,4,c,rust);
                long expected=Managed(packed,4);
                if(expected!=(open==0?samples.Length*128:128))throw new InvalidDataException("Unexpected IDR count");
                var pin=GCHandle.Alloc(packed,GCHandleType.Pinned);
                try
                {
                    IntPtr pointer=pin.AddrOfPinnedObject();
                    (string Name,Func<long> Call)[] modes=[("csharp",()=>Managed(packed,4)),("c",()=>c(pointer,(nuint)packed.Length,4)),("rust",()=>rust(pointer,(nuint)packed.Length,4))];
                    foreach(var mode in modes)for(int i=0;i<100;i++)if(mode.Call()!=expected)throw new InvalidDataException("Warmup mismatch");
                    const int loops=500;
                    for(int rep=0;rep<7;rep++)
                    foreach(var mode in rep%2==0?modes:modes.Reverse().ToArray())
                    {
                        ct.ThrowIfCancellationRequested();
                        long sum=0;var sw=Stopwatch.StartNew();
                        for(int i=0;i<loops;i++)sum+=mode.Call();
                        sw.Stop();
                        if(sum!=expected*loops)throw new InvalidDataException("Timed mismatch");
                        Console.WriteLine("NATIVE_BENCH "+JsonSerializer.Serialize(new{open,rep,mode=mode.Name,loops,bytes=packed.Length,samples=samples.Length*128,seconds=sw.Elapsed.TotalSeconds,checksum=sum,sha256=Convert.ToHexString(SHA256.HashData(packed)),runtime=RuntimeInformation.FrameworkDescription,os=RuntimeInformation.OSDescription}));
                    }
                }
                finally{pin.Free();}
            }
        }
        finally
        {
            if(rLib!=IntPtr.Zero)NativeLibrary.Free(rLib);
            NativeLibrary.Free(cLib);Directory.Delete(root,true);
        }
    }
}
