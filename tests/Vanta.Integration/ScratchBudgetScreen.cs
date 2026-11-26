using Vanta.Core;
internal static class ScratchBudgetScreen
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    internal static void Run(string directory)
    {
        const long mib=1024*1024;
        Check(ScratchBudget.Required(8*mib,false)==42*mib&&ScratchBudget.Required(8*mib,true)==56*mib,"Scratch estimate multipliers");
        string a=Path.Combine(directory,"disk-admission-a"),b=Path.Combine(directory,"disk-admission-b");
        Directory.CreateDirectory(a);Directory.CreateDirectory(b);
        long free=80*mib;
        var shared=new ScratchBudget(_=>("volume-a",free));
        var first=shared.Reserve(a,8*mib,false)!;
        try
        {
            bool rejected=false;
            try{using var second=shared.Reserve(b,8*mib,false);}catch(IOException){rejected=true;}
            Check(rejected,"Concurrent same-volume reservation exceeds available");
            rejected=false;
            try{using var split=shared.Reserve(a,8*mib,true);}catch(IOException){rejected=true;}
            Check(rejected,"Split scratch headroom must be counted");
        }
        finally{first.Dispose();first.Dispose();}
        using(var next=shared.Reserve(b,8*mib,false))Check(next is not null,"Reservation returned after release");
        var separate=new ScratchBudget(dir=>(dir,80*mib));
        using(var one=separate.Reserve(a,8*mib,false))
        using(var two=separate.Reserve(b,8*mib,true))Check(one is not null&&two is not null,"Independent volumes do not share reservations");
        var unknown=new ScratchBudget(_=>null);
        Check(unknown.Reserve(a,8*mib,false) is null,"Unknown volume bypasses estimate gate");
        foreach(double invalid in new[]{0,double.NaN,double.PositiveInfinity,double.MaxValue})
        {
            bool rejected=false;
            try{ScratchBudget.Required(invalid,false);}catch(ArgumentException){rejected=true;}
            Check(rejected,"Invalid scratch estimate");
        }
        Console.WriteLine("Scratch estimate and reservation tests passed.");
    }
}
