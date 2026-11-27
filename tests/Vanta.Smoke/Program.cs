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
var manual=avcPlans[1] with {Name="Custom",Preset="veryfast",Height=720};
Check(PlanRules.Presets.Contains("superfast")&&!PlanRules.Presets.Contains("ultrafast"),"Explicit superfast option, ultrafast remains unavailable");
PlanRules.Validate(manual with {Preset="superfast"},source);
Check(BatchPlanSelection.Resolve(source,["libx264","libx265"],"Compact","libx265",manual)==manual,"Individual settings override shared policy");
Check(BatchPlanSelection.Resolve(source,["libx264","libx265"],"Quality","libx265",null)==plans[0],"Reset individual settings to shared policy");
Check(BatchPlanSelection.Resolve(source,["libx264","libx265"],"Quality","libx265",null,"veryfast")==plans[0] with {Preset="veryfast"},"Shared speed preset only changes preset");
Check(BatchPlanSelection.Resolve(source,["libx264","libx265"],"Compact","libx265",manual,"veryfast")==manual,"Individual preset overrides shared speed");
Check(BatchPlanSelection.Resolve(source,["libx264"],"Balanced","libx265",null) is null,"Unavailable shared tier stays unavailable");
bool overrideRejected=false;try{BatchPlanSelection.Resolve(source,["libx265"],"Quality",null,manual);}catch(ArgumentException){overrideRejected=true;}
Check(overrideRejected,"Unavailable manual codec rejected");
overrideRejected=false;try{BatchPlanSelection.Resolve(source,["libx264"],"Quality",null,manual with {Height=1081});}catch(ArgumentException){overrideRejected=true;}
Check(overrideRejected,"Invalid manual plan rejected");
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
Check(new EncodeProgress("Encoding",.999).DisplayPercent==99&&new EncodeProgress("Complete",1).DisplayPercent==100&&new EncodeProgress("Validating",null).DisplayPercent is null,"Progress display never rounds encoding to 100%");
var eta=new EncodingEta();
TimeSpan Sec(double n)=>TimeSpan.FromSeconds(n);
eta.Observe(new("Encoding",0),Sec(0));
eta.Observe(new("Encoding",.01),Sec(1));
Check(eta.Remaining(Sec(1)) is null,"ETA warmup");
eta.Observe(new("Encoding",.1),Sec(5));
Check(eta.Remaining(Sec(5)) is TimeSpan er&&Math.Abs(er.TotalSeconds-45)<.001,"ETA constant rate");
eta.Observe(new("Encoding",.2),Sec(10));
Check(eta.Remaining(Sec(10)) is TimeSpan er2&&Math.Abs(er2.TotalSeconds-40)<.001,"ETA updated");
eta.Observe(new("Encoding",.1),Sec(11));
Check(eta.Remaining(Sec(11)) is TimeSpan er3&&Math.Abs(er3.TotalSeconds-40)<.001,"ETA rejects regressive fraction");
Check(eta.Remaining(Sec(21)) is null,"ETA suppressed during stall");
eta.Observe(new("Validating",null),Sec(22));
Check(eta.Remaining(Sec(22)) is null,"ETA does not forecast verification");
eta.Observe(new("Encoding",double.NaN),Sec(23));
Check(eta.Remaining(Sec(23)) is null,"ETA invalid sample");

Check(CompletionReference.TotalRemaining(Sec(30),Sec(12))==Sec(42),"Whole-job reference adds previous validation tail");
Check(CompletionReference.TotalRemaining(Sec(30),null) is null,"No invented verification time");
Check(CompletionReference.TotalRemaining(TimeSpan.FromDays(30),Sec(1)) is null,"Bounded total ETA");
Check(CompletionReference.ValidationRemaining(Sec(12),Sec(5))==Sec(7),"Historical validation countdown");
Check(CompletionReference.ValidationRemaining(Sec(12),Sec(12)) is null&&CompletionReference.ValidationRemaining(Sec(12),Sec(13)) is null,"Overrun never reports zero or negative ETA");
Check(CompletionReference.ValidationRemaining(Sec(12),Sec(-1)) is null&&CompletionReference.ValidationRemaining(null,Sec(1)) is null,"Invalid or absent reference withheld");
Check(new EncodeProgress("Validating",null){Verification=VerificationPhase.FullDecode}.DisplayPercent is null,"Verification phase never claims percent");
DurationRange DR(double low,double high)=>new(Sec(low),Sec(high));
Check(QueueTiming.Estimate(1,[DR(3,4)],[DR(5,6),DR(7,8)])==DR(15,18),"Serial queue sums active and queued references");
Check(QueueTiming.Estimate(2,[DR(4,4),DR(8,8)],[])==DR(8,8),"Parallel active-only queue equals maximum");
Check(QueueTiming.Estimate(2,[DR(2,3)],[DR(6,8),DR(4,5)])==DR(6,12),"Parallel queue work-conserving scenario");
Check(QueueTiming.Estimate(2,[DR(1,1)],[null]) is null,"Queue with unknown job remains unknown");
Check(QueueTiming.Estimate(2,[DR(1,1),DR(1,1),DR(1,1)],[]) is null,"Queue rejects impossible active count");
Check(QueueTiming.Estimate(2,[],[]) is null&&QueueTiming.Estimate(0,[],[DR(1,1)]) is null,"Empty or invalid queue does not claim ETA");
Check(QueueTiming.Remaining(DR(10,20),Sec(12))==DR(0,8),"Active total reference subtracts elapsed");
Check(QueueTiming.Remaining(DR(10,20),Sec(20)) is null,"Overrun invalidates queue ETA");
var history=new TimingHistory();
var historyMedia=new MediaInfo(100_000_000,60,1920,1080,30,"h264",0,0,60,0,12_000_000);
var historyPlan=new Plan("Balanced","libx264",1080,8_000_000,0,"veryfast",false);
for(int i=0;i<2;i++)history.Record(new("sample"+i,historyMedia,historyPlan,4,"tools",Sec(20+i*5)));
Check(history.Estimate("query",historyMedia,historyPlan,4,"tools") is null,"Timing reference requires three distinct sources");
history.Record(new("sample2",historyMedia,historyPlan,4,"tools",Sec(30)));
var range=history.Estimate("query",historyMedia with{Seconds=90,VideoDurationSeconds=90},historyPlan,4,"tools");
Check(range is not null&&range.Sources==3&&range.Low==Sec(30)&&range.High==Sec(45),"Timing reference scales nearby durations");
Check(history.Estimate("sample0",historyMedia,historyPlan,4,"tools") is null,"Timing reference excludes queried source");
Check(history.Estimate("query",historyMedia,historyPlan with{Preset="slow"},4,"tools") is null,"Timing reference rejects other presets");
Check(history.Estimate("query",historyMedia with{Fps=60},historyPlan,4,"tools") is null,"Timing reference rejects other frame rates");
Check(history.Estimate("query",historyMedia,historyPlan,null,"tools") is null&&history.Estimate("query",historyMedia,historyPlan,4,"other") is null,"Timing reference isolates thread and tool settings");
history.Record(new("SAMPLE2",historyMedia,historyPlan with{Name="Custom"},4,"tools",Sec(25)));
Check(history.Count==3,"Timing reference deduplicates source and settings");
history.Record(new("bad",historyMedia with{Fps=double.NaN},historyPlan,4,"tools",Sec(10)));
Check(history.Count==3,"Timing reference rejects invalid frame rate");
history.Record(new("outlier",historyMedia,historyPlan,4,"tools",Sec(100)));
Check(history.Estimate("query",historyMedia,historyPlan,4,"tools") is null,"Timing reference refuses dispersed samples");

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
    var settings=Path.Combine(namingRoot,"prefs.json");
    Check(BatchPreferences.Load(settings)==new BatchPreferences(),"Missing preferences default");
    var prefs=new BatchPreferences("Compact","libx264",1,2,"veryfast");prefs.Save(settings);
    Check(BatchPreferences.Load(settings)==prefs,"Preferences roundtrip");
    File.WriteAllText(settings,"{\"Tier\":\"Balanced\",\"ParallelJobs\":2}");
    Check(BatchPreferences.Load(settings).Preset=="medium","Older saved defaults preserve medium preset");
    new BatchPreferences().Save(settings);Check(BatchPreferences.Load(settings)==new BatchPreferences(),"Preferences atomic replacement");
    rejected=false;try{new BatchPreferences("invalid").Save(settings);}catch(ArgumentException){rejected=true;}
    Check(rejected&&BatchPreferences.Load(settings)==new BatchPreferences(),"Invalid settings do not overwrite defaults");
    File.WriteAllText(settings,"{broken");
    rejected=false;try{BatchPreferences.Load(settings);}catch(System.Text.Json.JsonException){rejected=true;}
    Check(rejected,"Corrupt settings detected");
    Check(!Directory.EnumerateFiles(namingRoot,"*.tmp").Any(),"Settings temporary cleanup");
    File.WriteAllText(one,"existing");
    var stamp=SourceStamp.Capture(one);stamp.RequireUnchanged(one);
    File.AppendAllText(one," changed");
    rejected=false;try{stamp.RequireUnchanged(one);}catch(IOException){rejected=true;}
    Check(rejected,"Stale source stamp rejected");
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
Check(BatchRecovery.CanEdit("Failed",true)&&BatchRecovery.CanEdit("Canceled",true)&&!BatchRecovery.CanEdit("Complete",true)&&!BatchRecovery.CanEdit("Failed",false),"Batch edit recovery policy");
var zero=TimeSpan.Zero;
var reports=new[]{new EncodeReport(zero,zero,zero,zero,zero,zero,zero,TimeSpan.FromSeconds(2),50,100),new EncodeReport(zero,zero,zero,zero,zero,zero,zero,TimeSpan.FromSeconds(3),300,200)};
var totals=BatchTotals.From(reports);
Check(totals.Completed==2&&totals.InputBytes==300&&totals.OutputBytes==350&&Math.Abs(totals.Savings!.Value+1d/6)<1e-12&&totals.JobTime.TotalSeconds==5,"Actual weighted batch savings including growth");
Check(BatchTotals.From([]).Savings is null,"Empty result is not zero percent");
var visual=new VisualMetadata(16d/9,"tv","bt709","bt709","bt709",false);
visual.ValidateOutput(visual);
foreach(var changedVisual in new[]{visual with {DisplayRatio=4d/3},visual with {Range="pc"},visual with {Primaries=""},visual with {HasTransform=true}})
{
    rejected=false;try{visual.ValidateOutput(changedVisual);}catch(InvalidDataException){rejected=true;}
    Check(rejected,"Visual metadata mismatch rejected");
}
Check(PlanRules.ParseVideoKbps("64")==64000&&PlanRules.ParseVideoKbps("64.001")==64001&&PlanRules.ParseVideoKbps("100000")==100000000,"Exact bitrate parsing");
foreach(var invalidRate in new[]{"63.999","100000.001","64.0001","NaN","1,000","1e3",""})
{
    rejected=false;try{PlanRules.ParseVideoKbps(invalidRate);}catch(ArgumentException){rejected=true;}
    Check(rejected,"Invalid or fractional-bit rate rejected");
}
var quickHeights=QuickSettings.Heights(source);
Check(quickHeights[0]==1080&&quickHeights.Contains(720)&&!quickHeights.Contains(2160)&&quickHeights.All(h=>h%2==0),"Source-bounded quick heights");
var quickRates=QuickSettings.Rates(source);
Check(quickRates.Count>0&&quickRates.Distinct().Count()==quickRates.Count&&quickRates.All(r=>r>=PlanRules.MinimumVideoRate&&r<=PlanRules.MaximumVideoRate),"Bounded quick rates");
var more=QuickSettings.MoreCompression(plans[0],source)!;
Check(more.VideoBitsPerSecond<plans[0].VideoBitsPerSecond&&more.Height==plans[0].Height&&more.Codec==plans[0].Codec&&more.EstimateBytes(source)<plans[0].EstimateBytes(source),"More compression reduces video budget only");
Check(QuickSettings.MoreCompression(plans[0] with {VideoBitsPerSecond=64001},source)!.VideoBitsPerSecond==64000&&QuickSettings.MoreCompression(plans[0] with {VideoBitsPerSecond=64000},source) is null,"More compression stops at bitrate floor");
Console.WriteLine("Smoke checks passed");
sealed class ImmediateProgress(Action<EncodeProgress> report):IProgress<EncodeProgress>{public void Report(EncodeProgress value)=>report(value);}