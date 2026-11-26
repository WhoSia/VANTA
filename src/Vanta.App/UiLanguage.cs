using System.Globalization;
using System.IO;
using System.Windows;
namespace Vanta.App;

internal static class UiLanguage
{
    private static readonly (string Key,string En,string Ko)[] Captions =
    [
        ("Ui001","VANTA — 0.4.0 preview","VANTA — 0.4.0 미리 보기"),
        ("Ui002","Video compression · 0.4.0 development preview","영상 압축 · 0.4.0 개발 미리 보기"),
        ("Ui003","Open videos… (one or many)","영상 열기… (여러 개 가능)"),
        ("Ui004","Batch queue…","일괄 압축…"),
        ("Ui005","Check hardware encoders…","하드웨어 인코더 검사…"),
        ("Ui006","Suggested plans","추천 압축 플랜"),
        ("Ui007","Recommendation codec (changing this rebuilds the plans and resets manual settings)","추천 코덱 (변경 시 추천 플랜과 직접 설정 초기화)"),
        ("Ui008","Profiles are bitrate-budget tiers, not measured quality guarantees.","플랜은 비트레이트 예산을 기준으로 하며 실측 화질을 보장하지 않습니다."),
        ("Ui009","Encoding settings","압축 설정"),
        ("Ui010","Video codec","영상 코덱"),
        ("Ui011","Encoding speed","압축 속도 설정"),
        ("Ui012","Height (pixels, even)","영상 높이 (짝수 픽셀)"),
        ("Ui013","Quick choice","빠른 선택"),
        ("Ui014","Suggested heights (pixels)","추천 영상 높이 (픽셀)"),
        ("Ui015","Custom height","직접 높이 입력"),
        ("Ui016","Video bitrate (kbps)","영상 비트레이트 (kbps)"),
        ("Ui017","Example bitrate budgets (kbps)","비트레이트 예시 (kbps)"),
        ("Ui018","Custom bitrate","직접 비트레이트 입력"),
        ("Ui019","More compression (−25% video bitrate)","더 압축하기 (영상 비트레이트 −25%)"),
        ("Ui020","Changes the current budget only; does not start encoding.","현재 예산만 낮추며 압축을 시작하지 않습니다."),
        ("Ui021","Target size (MB, approximate)","목표 용량 (MB, 추정)"),
        ("Ui022","Apply target","목표 적용"),
        ("Ui023","Encoder thread budget (optional)","인코더 스레드 예산 (선택)"),
        ("Ui024","Auto is the default. This is an encoder thread request, not a CPU usage limit; decoding and codec worker pools may use additional threads.","기본값은 자동입니다. 인코더 스레드 요청값이며 CPU 사용량 제한은 아닙니다. 디코딩과 코덱 작업 풀은 추가 스레드를 사용할 수 있습니다."),
        ("Ui025","Experimental: split video into two parallel parts","실험 기능: 영상을 두 부분으로 분할해 병렬 압축"),
        ("Ui026","Optional, H.264 MP4 CFR only, original dimensions. May be slower or change quality; strict validation, no output on failure.","H.264 MP4 고정 프레임률 및 원본 해상도에서만 사용 가능합니다. 더 느리거나 화질이 달라질 수 있으며 검증 실패 시 출력하지 않습니다."),
        ("Ui027","MP4 · source frame timing · AAC copy · single pass","MP4 · 원본 프레임 시간 유지 · AAC 복사 · 단일 패스"),
        ("Ui028","Save compressed video…","압축 파일 저장…"),
        ("Ui029","Cancel","취소"),
        ("Ui030","Open result folder","결과 폴더 열기"),
        ("Ui031","Copy result","결과 복사"),
        ("Ui032","Open an 8-bit SDR video with AAC audio or no audio.","AAC 오디오가 있거나 무음인 8비트 SDR 영상을 열어 주세요."),
        ("Ui033","VANTA · Batch compression","VANTA · 일괄 압축"),
        ("Ui034","Batch compression","일괄 압축"),
        ("Ui035","Each video is encoded continuously and independently. Choose a destination or save beside each source.","각 영상은 독립적으로 연속 압축됩니다. 저장 폴더를 지정하거나 원본 옆에 저장할 수 있습니다."),
        ("Ui036","Add videos…","영상 추가…"),
        ("Ui037","Clear","목록 비우기"),
        ("Ui038","Drop videos here or use Add videos. Select a row to edit its settings.","영상을 끌어 놓거나 추가하세요. 목록에서 영상을 선택해 설정을 수정할 수 있습니다."),
        ("Ui039","Shared profile","공통 플랜"),
        ("Ui040","Recommendation codec","추천 코덱"),
        ("Ui041","Parallel jobs","동시 작업 수"),
        ("Ui042","Total encoder threads","전체 인코더 스레드"),
        ("Ui043","Choose folder…","폴더 선택…"),
        ("Ui044","Beside sources","원본 옆에 저장"),
        ("Ui045","Output: beside each source","저장 위치: 각 원본 옆"),
        ("Ui046","Source","원본"),
        ("Ui047","Output","출력"),
        ("Ui048","Plan / budget","플랜 / 예산"),
        ("Ui049","Status","상태"),
        ("Ui050","Details","상세"),
        ("Ui051","No video selected","선택된 영상 없음"),
        ("Ui052","Edit settings…","설정 수정…"),
        ("Ui053","Use shared profile","공통 플랜 적용"),
        ("Ui054","Retry","재시도"),
        ("Ui055","Remove","목록에서 제거"),
        ("Ui056","Copy details","상세 정보 복사"),
        ("Ui057","Add or drop videos to review the queue.","영상을 추가하거나 끌어 놓아 대기열을 확인하세요."),
        ("Ui058","Start batch compression","일괄 압축 시작"),
        ("Ui059","Save defaults","기본값 저장"),
        ("Ui060","Reset defaults","기본값 초기화"),
        ("Ui061","VANTA · Individual video settings","VANTA · 개별 영상 설정"),
        ("Ui062","Individual video settings","개별 영상 설정"),
        ("Ui063","Codec","코덱"),
        ("Ui064","Output height (even pixels, no upscaling)","출력 높이 (짝수 픽셀, 확대 금지)"),
        ("Ui065","Optional target size budget (MB)","목표 용량 예산 (MB, 선택)"),
        ("Ui066","Original audio and frame timing are preserved. Size and quality are not guaranteed. This override is kept when the shared profile or codec changes.","원본 오디오와 프레임 시간을 유지합니다. 용량과 화질은 보장되지 않습니다. 공통 플랜이나 코덱을 바꿔도 개별 설정이 유지됩니다."),
        ("Ui067","Use these settings","설정 적용"),
        ("Ui068","veryfast / fast prioritize speed; medium balances speed and compression efficiency; slow may improve efficiency. Size and quality vary.","매우 빠르게·빠르게는 속도 우선, 균형은 속도와 압축 효율의 절충, 압축 효율 우선은 더 오래 걸릴 수 있습니다. 실제 용량과 화질은 달라질 수 있습니다."),
        ("Ui069","Saves a separate MP4. Original remains unchanged.","별도 MP4 파일로 저장하며 원본은 변경하지 않습니다."),
        ("Ui070","Shared encoding speed","공통 압축 속도"),
        ("Ui072","Compression progress","압축 진행률"),
        ("Ui073","Video queue","영상 대기열"),
        ("Ui074","Play output","결과 재생"),
        ("Ui075","Speed first · H.264 fast","속도 우선 · H.264 빠르게"),
        ("Ui076","Explicitly change codec and preset; keep height and video bitrate. Actual size and quality can change. Does not start compression.","코덱과 속도 설정만 변경하고 해상도와 비트레이트를 유지합니다. 실제 용량과 화질은 달라질 수 있으며 압축을 시작하지 않습니다."),
        ("Ui071","One MP4 per ready video; original files are preserved. Individual settings override the shared preset.","준비된 영상마다 별도 MP4를 저장합니다. 원본은 유지되며 개별 설정이 공통 프리셋보다 우선합니다."),
    ];
    public static string Text(bool korean,string en,string ko)=>korean?ko:en;
    public static string Tier(bool korean,string tier)=>!korean?tier:tier switch {"Quality"=>"화질 우선","Balanced"=>"균형","Compact"=>"용량 우선","Custom"=>"직접 설정",_=>tier};
    public static string Preset(bool korean,string key)=>!korean?key:key switch {"veryfast"=>"매우 빠르게","fast"=>"빠르게","medium"=>"균형","slow"=>"압축 효율 우선",_=>key};
    internal sealed record PresetChoice(string Key,string Label);
    public static void SetPresets(System.Windows.Controls.ComboBox box,bool korean)
    {
        var selected=box.SelectedValue as string;
        box.SelectedValuePath=nameof(PresetChoice.Key);box.DisplayMemberPath=nameof(PresetChoice.Label);
        box.ItemsSource=Vanta.Core.PlanRules.Presets.Select(key=>new PresetChoice(key,Preset(korean,key))).ToArray();
        box.SelectedValue=selected;
    }
    public static string Stage(bool korean,string stage)
    {
        if(!korean)return stage;
        if(stage.StartsWith("Encoding ",StringComparison.Ordinal))return "압축 중 "+stage["Encoding ".Length..];
        return stage switch {"Analyzing"=>"분석 중","Ready"=>"준비","Queued"=>"대기","Encoding"=>"압축 중","Validating"=>"검증 중","Complete"=>"완료","Failed"=>"실패","Canceled"=>"취소됨","Skipped"=>"건너뜀","Experimental split"=>"실험적 분할 압축",_=>stage};
    }
    public static void Apply(Window window,bool korean)
    {
        foreach(var (key,en,ko) in Captions)window.Resources[key]=Text(korean,en,ko);
    }
    private static string PreferencePath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VANTA","language.txt");
    public static bool Load()
    {
        try{if(File.Exists(PreferencePath)){var value=File.ReadAllText(PreferencePath).Trim();if(value is "ko" or "en")return value=="ko";}}
        catch(IOException){}catch(UnauthorizedAccessException){}
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="ko";
    }
    public static void Save(bool korean)
    {
        var path=PreferencePath;Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temp,korean?"ko":"en");File.Move(temp,path,true);}
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
}
