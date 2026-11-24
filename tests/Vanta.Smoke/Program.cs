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
Check(plans.All(p=>!p.TwoPass && p.CopyAudio),"Executable pass and audio copy mode");
Check(plans.All(p=>p.EstimateBytes(source)>source.Seconds*source.AudioBitsPerSecond/8),"Audio budget included");
StreamTiming[] a=[new("video",0,10),new("audio",.2,9.8)];
Timeline.Validate(a,[new("video",1,10),new("audio",1.2,9.8)],30);
bool rejected=false;
try {Timeline.Validate(a,[new("video",0,10),new("audio",.4,9.6)],30);} catch(InvalidDataException){rejected=true;}
Check(rejected,"Relative start shift rejection");
rejected=false;
try {Timeline.Validate(a,[new("video",0,10),new("audio",.2,9.5)],30);} catch(InvalidDataException){rejected=true;}
Check(rejected,"Relative end drift rejection");
var target=900_000_000d;
var solved=plans[0] with {VideoBitsPerSecond=PlanRules.TargetRate(source,target)};
Check(solved.EstimateBytes(source)<=target && target-solved.EstimateBytes(source)<300,"Target size rounding");
foreach(var p in plans)PlanRules.Validate(p,source);
Check(Planner.Propose(source with {Codec="hevc"})[0].Savings(source)<plans[0].Savings(source),"Conservative efficient source");
Check(Planner.Propose(new MediaInfo(100_000,10,320,180,30,"hevc",1,70000)).Count==0,"Audio-dominated source shortage");
foreach(var bad in new[]{plans[0] with {Codec="invalid"},plans[0] with {Height=1081},plans[0] with {TwoPass=true},plans[0] with {Preset="invalid"}})
{
    rejected=false;try{PlanRules.Validate(bad,source);}catch(ArgumentException){rejected=true;}Check(rejected,"Unsupported plan rejected");
}
rejected=false;try{PlanRules.TargetRate(source,100);}catch(ArgumentException){rejected=true;}Check(rejected,"Impossible target rejected");
Console.WriteLine("Smoke checks passed");