using Vanta.Core;
static void Check(bool condition,string name){if(!condition)throw new Exception(name);}
var source=new MediaInfo(2400000000,1800,1920,1080,59.94,"h264",1,128000);
var plans=Planner.Propose(source);
Check(plans.Count==3,"Three candidates");
Check(plans.All(p=>p.Savings(source)>0),"Positive savings");
var avcPlans=Planner.Propose(source,["libx264"]);
Check(avcPlans.Count==3&&avcPlans.All(p=>p.Codec=="libx264"&&p.Savings(source)>0),"Capability aware AVC planning");
Check(avcPlans[0].VideoBitsPerSecond>=plans[0].VideoBitsPerSecond,"Conservative AVC-only budget");
Check(Planner.Propose(source,["libx265"]).All(p=>p.Codec=="libx265"),"Capability aware HEVC planning");
Check(Planner.Propose(source,[]).Count==0&&Planner.Propose(source,["unsupported"]).Count==0,"No executable codec means no recommendation");
Check(Planner.Propose(source,["libx264","libx265"],"libx264").All(p=>p.Codec=="libx264")&&Planner.Propose(source,["libx264","libx265"],"libx264").Count==3,"Explicit AVC recommendation");
Check(Planner.Propose(source,["libx264"],"libx265").Count==0,"No fallback for unavailable preferred codec");
bool badPreference=false;try{Planner.Propose(source,null,"invalid");}catch(ArgumentException){badPreference=true;}
Check(badPreference,"Invalid recommendation preference rejected");
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
var offset=source with {Seconds=100,VideoDurationSeconds=90,AudioPayloadBytes=640000,VideoStreamRate=1_000_000};
var offsetPlan=plans[0] with {VideoBitsPerSecond=800000};
Check(Math.Abs(offsetPlan.EstimateBytes(offset)-(90*800000d/8+640000)*PlanRules.Overhead)<.001,"Per-stream size budget");
var offsetTarget=10_000_000d;
var offsetSolved=offsetPlan with {VideoBitsPerSecond=PlanRules.TargetRate(offset,offsetTarget)};
Check(offsetSolved.EstimateBytes(offset)<=offsetTarget && offsetTarget-offsetSolved.EstimateBytes(offset)<12,"Per-stream inverse budget");
Check(Planner.Propose(offset)[0].VideoBitsPerSecond==950000,"Measured source video rate used");
Check(Planner.Propose(source with {Fps=double.NaN})[0].VideoBitsPerSecond>=plans[0].VideoBitsPerSecond,"Unknown FPS is conservative");
var observed=new List<EncodeProgress>();
var parser=new ProgressParser(10,new ImmediateProgress(observed.Add));
foreach(var line in new[]{"out_time_us=N/A","out_time_us=-5","out_time_us=3000000","out_time_us=2000000","out_time_us=10000000"})parser.Read(line);
Check(observed.Count==2 && observed[0].Fraction==.3 && observed[1].Fraction==.999,"Monotonic encoding progress capped before validation");
var namingRoot=Path.Combine(Path.GetTempPath(),"VANTA-names-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(namingRoot);
try
{
    var first=Path.Combine(namingRoot,"clip.mp4");
    var other=Path.Combine(namingRoot,"clip.mov");
    var reserved=new HashSet<string>(StringComparer.OrdinalIgnoreCase){first,other};
    var one=OutputNames.Suggest(first,reserved);
    var two=OutputNames.Suggest(other,reserved);
    Check(Path.GetFileName(one)=="clip_vanta.mp4"&&Path.GetFileName(two)=="clip_vanta_2.mp4","Batch unique names");
    var shared=Path.Combine(namingRoot,"shared");Directory.CreateDirectory(shared);
    var destReserved=new HashSet<string>(StringComparer.OrdinalIgnoreCase){Path.Combine(shared,"clip_vanta.mp4")};
    var sharedName=OutputNames.Suggest(first,destReserved,shared);
    Check(sharedName==Path.Combine(shared,"clip_vanta_2.mp4"),"Shared destination reservation");
    File.WriteAllText(one,"existing");
    Check(OutputNames.Suggest(first).EndsWith("clip_vanta_2.mp4"),"Existing file naming");
}
finally{Directory.Delete(namingRoot,true);}
Check(ThreadBudget.PerJob(null,2) is null,"Auto leaves encoder settings unchanged");
Check(ThreadBudget.PerJob(8,2)==4&&ThreadBudget.PerJob(5,2)==2&&ThreadBudget.PerJob(1,1)==1,"Shared thread allocation");
Check(ThreadBudget.Choices(1).SequenceEqual(new[]{1})&&ThreadBudget.Choices(9).Last()==9&&ThreadBudget.Choices(128).Last()==64,"Bounded thread choices");
ThreadBudget.ValidateEncoder(1,false);ThreadBudget.ValidateEncoder(4,true);
foreach(int badThreads in new[]{0,1,3,65})
{
    rejected=false;try{ThreadBudget.ValidateEncoder(badThreads,true);}catch(ArgumentException){rejected=true;}
    Check(rejected,"Invalid split thread budget");
}
rejected=false;try{ThreadBudget.PerJob(1,2);}catch(ArgumentException){rejected=true;}
Check(rejected,"Insufficient batch thread budget");
Console.WriteLine("Thread budget allocation passed.");
Console.WriteLine("Smoke checks passed");
sealed class ImmediateProgress(Action<EncodeProgress> report):IProgress<EncodeProgress>{public void Report(EncodeProgress value)=>report(value);}