using Vanta.Core;
static void Check(bool condition,string name){if(!condition)throw new Exception(name);}
var source=new MediaInfo(2400000000,1800,1920,1080,59.94,"h264",1,128000);
var plans=Planner.Propose(source);
Check(plans.Count==3,"Three candidates");
Check(plans.All(p=>p.Savings(source)>0),"Positive savings");
Check(plans.Zip(plans.Skip(1)).All(x=>x.First.EstimateBytes(source)>x.Second.EstimateBytes(source)),"Ordered sizes");
Check(Decisions.Dominates([.8,.9],[.7,.9]),"Dominance");
Check(!Decisions.Dominates([.8,.7],[.7,.9]),"Trade-off");
Check(Math.Abs(Decisions.GeometricUtility([.25,1],[.5,.5])-.5)<1e-9,"Geometric utility");
Check(Decisions.GeometricUtility([0,1],[.5,.5])==0,"Zero score");
Check(plans.All(p=>!p.TwoPass),"Executable pass mode");
StreamTiming[] a=[new("video",0,10),new("audio",.2,9.8)];
Timeline.Validate(a,[new("video",1,10),new("audio",1.2,9.8)],30);
bool rejected=false;
try {Timeline.Validate(a,[new("video",0,10),new("audio",.4,9.6)],30);} catch(InvalidDataException){rejected=true;}
Check(rejected,"Relative start shift rejection");
rejected=false;
try {Timeline.Validate(a,[new("video",0,10),new("audio",.2,9.5)],30);} catch(InvalidDataException){rejected=true;}
Check(rejected,"Relative end drift rejection");
Console.WriteLine("Smoke checks passed");