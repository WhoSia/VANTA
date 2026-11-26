using Vanta.Core;
internal static class BoundedTextScreen
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    internal static async Task RunAsync()
    {
        using(var reader=new StringReader(new string('x',9000)))
        {
            var r=await BoundedProcessText.CaptureAsync(reader,4096);
            Check(r.Text.Length==4096&&r.Truncated&&reader.Peek()==-1,"Capture limit drains entire input");
        }
        using(var reader=new StringReader("0123456789"))
        {
            var r=await BoundedProcessText.CaptureAsync(reader,10);
            Check(r.Text=="0123456789"&&!r.Truncated,"Exact cap accepted");
        }
        using(var reader=new StringReader(""))
            Check(await BoundedProcessText.ReadDiagnosticAsync(reader)=="","Empty diagnostics remain empty");
        using(var reader=new StringReader(new string(' ',BoundedProcessText.DiagnosticLimit+1)))
        {
            string error=await BoundedProcessText.ReadDiagnosticAsync(reader);
            Check(error.EndsWith(BoundedProcessText.Truncation,StringComparison.Ordinal)&&!string.IsNullOrWhiteSpace(error)&&reader.Peek()==-1,"Truncated diagnostics cannot be silent success");
        }
        using(var reader=new StringReader(new string('x',BoundedProcessText.OutputLimit+1)))
        {
            bool rejected=false;try{await BoundedProcessText.ReadOutputAsync(reader);}catch(InvalidDataException){rejected=true;}
            Check(rejected&&reader.Peek()==-1,"Oversized stdout is rejected after drain");
        }
        Console.WriteLine("Bounded process capture, drain, exact-cap and overflow controls passed.");
    }
}
