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
        ("Ui005","Check my GPU…","내 GPU 확인…"),
        ("Ui006","Choose an output size","얼마나 줄일까요?"),
        ("Ui007","Recommendation codec (changing this rebuilds the plans and resets manual settings)","추천 코덱 (변경 시 추천 플랜과 직접 설정 초기화)"),
        ("Ui008","Start with the middle option if unsure. File size is estimated; picture quality is not measured.","잘 모르겠다면 균형 설정부터 시작하세요. 용량은 예상값이며 화질은 측정하지 않았습니다."),
        ("Ui009","Detailed compression settings","세부 압축 설정"),
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
        ("Ui027","MP4 · source frame timing · audio copy · single pass","MP4 · 원본 프레임 시간 유지 · 오디오 복사 · 단일 패스"),
        ("Ui028","Compress with CPU and save…","CPU로 압축 및 저장…"),
        ("Ui029","Cancel","취소"),
        ("Ui030","Open result folder","결과 폴더 열기"),
        ("Ui031","Copy result","결과 복사"),
        ("Ui032","Open an 8-bit SDR video with supported audio or no audio.","지원되는 오디오가 있거나 무음인 8비트 SDR 영상을 열어 주세요."),
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
        ("Ui058","Start CPU batch compression","CPU 일괄 압축 시작"),
        ("Ui059","Save defaults","기본값 저장"),
        ("Ui060","Reset defaults","기본값 초기화"),
        ("Ui061","VANTA · Individual video settings","VANTA · 개별 영상 설정"),
        ("Ui062","Individual video settings","개별 영상 설정"),
        ("Ui063","Codec","코덱"),
        ("Ui064","Output height (even pixels, no upscaling)","출력 높이 (짝수 픽셀, 확대 금지)"),
        ("Ui065","Optional target size budget (MB)","목표 용량 예산 (MB, 선택)"),
        ("Ui066","Original audio and frame timing are preserved. Size and quality are not guaranteed. This override is kept when the shared profile or codec changes.","원본 오디오와 프레임 시간을 유지합니다. 용량과 화질은 보장되지 않습니다. 공통 플랜이나 코덱을 바꿔도 개별 설정이 유지됩니다."),
        ("Ui067","Use these settings","설정 적용"),
        ("Ui068","superfast is an explicit high-speed choice; veryfast / fast prioritize speed. Faster settings can reduce quality or increase size. medium balances speed and compression efficiency; slow may take longer.","초고속은 직접 선택하는 속도 우선 설정입니다. 빠른 설정은 화질을 낮추거나 용량을 늘릴 수 있습니다. 균형은 속도와 압축 효율의 절충이며 압축 효율 우선은 더 오래 걸릴 수 있습니다."),
        ("Ui069","Saves a separate MP4. Original remains unchanged.","별도 MP4 파일로 저장하며 원본은 변경하지 않습니다."),
        ("Ui070","Shared encoding speed","공통 압축 속도"),
        ("Ui072","Compression progress","압축 진행률"),
        ("Ui073","Video queue","영상 대기열"),
        ("Ui074","Play output","결과 재생"),
        ("Ui075","Speed first · H.264 veryfast","속도 우선 · H.264 매우 빠르게"),
        ("Ui076","Explicitly change codec and preset; keep height and video bitrate. Actual size and quality can change. Does not start compression.","코덱과 속도 설정만 변경하고 해상도와 비트레이트를 유지합니다. 실제 용량과 화질은 달라질 수 있으며 압축을 시작하지 않습니다."),
        ("Ui077","Queue order","처리 순서"),
        ("Ui078","First","맨 앞"),
        ("Ui079","Up","위로"),
        ("Ui080","Down","아래로"),
        ("Ui081","Change submission order before starting. Parallel jobs can finish out of order.","시작 전 대기열 제출 순서를 변경합니다. 병렬 작업의 완료 순서는 다를 수 있습니다."),
        ("Ui082","Need it faster?","시간이 더 중요하신가요?"),
        ("Ui083","These options keep your selected resolution and bitrate. H.264 is widely compatible; HEVC can compress more efficiently.","선택한 해상도와 비트레이트는 유지합니다. H.264는 호환성이 좋고 HEVC는 압축 효율이 높을 수 있습니다."),
        ("Ui084","Keep HEVC · veryfast","HEVC · 매우 빠르게"),
        ("Ui085","Explicit HEVC veryfast choice. Same bitrate and height; quality and actual size may differ. No encoding started.","HEVC 매우 빠르게 직접 선택. 해상도와 비트레이트 유지, 실제 화질·용량은 달라질 수 있음. 압축 시작 안 함."),
        ("Ui086","Faster CPU settings may change picture quality or file size. A GPU can be faster, but needs a device check.","빠른 CPU 설정은 화질이나 실제 용량에 영향을 줄 수 있습니다. GPU는 더 빠를 수 있지만 장치 확인이 필요합니다."),
        ("Ui087","Fast · H.264","빠른 일괄 · H.264"),
        ("Ui088","Fast · HEVC","빠른 일괄 · HEVC"),
        ("Ui089","Change shared codec and preset, recompute budget tiers. Individual overrides retained. No encoding started.","공통 코덱과 프리셋 변경, 용량 예산 재계산. 개별 설정 유지. 압축 시작 안 함."),
        ("Ui090","GPU / device encoding trial","GPU / 하드웨어 시험 압축"),
        ("Ui091","Select a matching H.264 or HEVC backend. Device defaults, current height and bitrate.","현재 코덱과 일치하는 장치를 선택하세요. 장치 기본 설정, 현재 해상도와 비트레이트를 사용합니다."),
        ("Ui092","Compress with GPU and save…","GPU로 압축 및 저장…"),
        ("Ui093","Requires automatic threads and split off. Tests the selected device first; no CPU fallback. Full validation before saving.","스레드 자동, 분할 해제 필요. 선택한 장치부터 검사하며 CPU로 자동 전환하지 않습니다. 전체 검증 후 저장합니다."),
        ("Ui094","Try GPU-resident pipeline…","GPU 연속 처리 시험…"),
        ("Ui095","Experimental CUDA / QSV / D3D11 paths. AVC/HEVC 8-bit input; AMD original resolution only. No automatic fallback; CPU output verification.","실험용 CUDA / QSV / D3D11 경로. 8비트 AVC/HEVC 입력, AMD는 원본 해상도 유지. 자동 전환 없이 CPU 출력 검증."),
        ("Ui096","Compare CPU / selected GPU…","CPU / 선택 GPU 성능 비교…"),
        ("Ui097","Synthetic 3-second benchmark. Compares full verified time, bytes and SSIM. Does not read your video or save a video; may take several minutes.","3초 합성 영상 비교. 검증 포함 전체 시간·용량·SSIM을 측정합니다. 사용자 영상 접근 및 영상 저장 없음. 수 분 소요될 수 있습니다."),
        ("Ui098","Fast device preset · NVENC P1 / QSV veryfast","장치 속도 우선 · NVENC P1 / QSV veryfast"),
        ("Ui099","Explicit NVENC P1 or Intel QSV veryfast. Quality and size may differ from device defaults.","NVENC P1 또는 Intel QSV veryfast 직접 선택. 장치 기본 설정과 화질·용량이 다를 수 있습니다."),
        ("Ui100","Graphics device","그래픽 장치"),
        ("Ui101","Resident","GPU 연속 처리"),
        ("Ui102","Fast preset","속도 우선"),
        ("Ui103","Encode next while checking","검증 중 다음 영상 압축"),
        ("Ui104","Try GPU batch","GPU 일괄 시험"),
        ("Ui105","GPU acceleration (optional)","GPU 가속 (선택)"),
        ("Ui106","Drop a video here, or open a file.","영상을 끌어 놓거나 파일을 여세요."),
        ("Ui107","Estimated size appears after opening a video.","영상을 열면 예상 용량이 표시됩니다."),
        ("Ui108","Add videos, choose a common size, then start compression.","영상 추가 → 공통 압축 설정 선택 → 압축 시작"),
        ("Ui109","Resource settings (optional)","자원 설정 (선택)"),
        ("Ui110","Compress with tested GPU…","검사 통과 GPU로 압축…"),
        ("Ui071","One MP4 per ready video; original files are preserved. Individual settings override the shared preset.","준비된 영상마다 별도 MP4를 저장합니다. 원본은 유지되며 개별 설정이 공통 프리셋보다 우선합니다."),
    ];
    public static string Text(bool korean,string en,string ko)=>korean?ko:en;
    internal static string Rate(bool korean,double rate)=>Text(korean,"Recent speed: ","최근 처리 속도: ")+(rate<.01?"<0.01":rate.ToString("0.00",CultureInfo.InvariantCulture))+"×";
    internal static string Duration(TimeSpan value)
    {
        long seconds=(long)Math.Ceiling(Math.Max(0,value.TotalSeconds));
        return seconds>=3600?$"{seconds/3600}:{seconds%3600/60:00}:{seconds%60:00}":$"{seconds/60:00}:{seconds%60:00}";
    }
    internal static string DeviceName(string id)=>id switch
    {
        "h264_nvenc"=>"NVIDIA · H.264","hevc_nvenc"=>"NVIDIA · HEVC",
        "h264_qsv"=>"Intel Quick Sync · H.264","hevc_qsv"=>"Intel Quick Sync · HEVC",
        "h264_amf"=>"AMD · H.264","hevc_amf"=>"AMD · HEVC",_=>id
    };
    internal static string PlanPurpose(bool korean,string name,bool resize)=>name switch
    {
        "Quality"=>Text(korean,"Larger file, gentler compression","덜 줄이고 화질 손실을 줄이는 설정"),
        "Balanced"=>Text(korean,"Start here if unsure","잘 모르겠다면 이 설정부터"),
        "Compact"=>resize?Text(korean,"Smaller file, lower resolution","더 작은 파일, 해상도도 낮춤"):Text(korean,"Smaller file, more compression","더 작은 파일, 압축 강도 높음"),
        _=>Text(korean,"Custom settings","직접 설정")
    };
    public static string Tier(bool korean,string tier)=>!korean?tier:tier switch {"Quality"=>"화질 우선","Balanced"=>"균형","Compact"=>"용량 우선","Custom"=>"직접 설정",_=>tier};
    public static string Preset(bool korean,string key)=>!korean?key:key switch {"superfast"=>"초고속","veryfast"=>"매우 빠르게","fast"=>"빠르게","medium"=>"균형","slow"=>"압축 효율 우선",_=>key};
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
        return stage switch {"Diagnostics"=>"하드웨어 진단 중","Qualifying hardware"=>"하드웨어 초기화 검사 중","Preparing"=>"준비 중","Waiting for validation"=>"검증 대기","Waiting for encoder"=>"인코더 대기","Analyzing"=>"분석 중","Ready"=>"준비","Queued"=>"대기","Encoding"=>"압축 중","Validating"=>"검증 중","Complete"=>"완료","Failed"=>"실패","Canceled"=>"취소됨","Skipped"=>"건너뜀","Experimental split"=>"실험적 분할 압축",_=>stage};
    }
    internal static string Verification(bool korean,Vanta.Core.VerificationPhase? phase)=>phase switch
    {
        Vanta.Core.VerificationPhase.Metadata=>Text(korean,"Checking output metadata","출력 정보 검사"),
        Vanta.Core.VerificationPhase.Timeline=>Text(korean,"Checking frame and audio timing","영상·오디오 시간축 검사"),
        Vanta.Core.VerificationPhase.AudioIdentity=>Text(korean,"Checking copied audio payload","복사된 오디오 검사"),
        Vanta.Core.VerificationPhase.FullDecode=>Text(korean,"Decoding output for integrity","출력 전체 디코딩 검사"),
        Vanta.Core.VerificationPhase.Combined=>Text(korean,"Combined timeline and decode checks","시간축·디코딩 통합 검사"),
        Vanta.Core.VerificationPhase.DecodedAudio=>Text(korean,"Checking decoded audio identity","디코딩된 오디오 검사"),
        Vanta.Core.VerificationPhase.Publishing=>Text(korean,"Final source check and saving","최종 원본 검사 및 저장"),
        _=>Text(korean,"Verifying output","출력 검증 중")
    };
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
