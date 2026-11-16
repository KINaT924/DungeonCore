using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>음량을 조절할 채널. Master 믹서의 그룹과 1:1로 대응한다.</summary>
public enum VolumeChannel
{
    Master,
    Bgm,
    Sfx
}

/// <summary>
/// 전체·배경음·효과음 음량을 저장하고 Master 믹서에 적용하는 정적 클래스.
///
/// - 값은 0~1 슬라이더 값으로 PlayerPrefs에 저장한다. WebGL에서는 브라우저 저장소에 저장되어 다음 접속에도 유지된다.
/// - 씬이 바뀔 때마다 저장된 값을 믹서에 다시 적용한다. 씬 전환 때 믹서가 다시 로드되면 값이 초기화될 수 있기 때문이다.
/// - 믹서 참조는 씬 안의 AudioSource가 연결한 믹서 그룹에서 찾는다. 그래서 씬마다 따로 연결할 필요가 없다.
///   (타이틀·인게임·보스 씬 모두 BGM 오브젝트가 Master 믹서의 BGM 그룹을 사용한다.)
///
/// 실무 예시: 옵션 메뉴의 사운드 설정, 설정값 영구 저장, 데시벨 변환.
/// </summary>
public static class AudioVolumeSettings
{
    // Master.mixer에 공개(Exposed)한 파라미터 이름
    const string MasterParameter = "MasterVolume";
    const string BgmParameter = "BgmVolume";
    const string SfxParameter = "SfxVolume";

    const float DefaultValue = 0.8f;   // 처음 실행 시 슬라이더 값(타이틀 슬라이더 기본값과 같다)
    const float BgmBaseDb = -5f;       // 믹서에서 BGM 그룹에 원래 설정해 둔 기본 감쇠(-5dB)를 유지한다
    const float MinDb = -80f;          // 믹서가 표현하는 무음 수준

    static AudioMixer mixer;

    /// <summary>
    /// 호출 시점: 게임 시작 직후, 첫 씬이 로드되기 전 한 번.
    /// 씬이 로드될 때마다 음량을 다시 적용하도록 이벤트를 등록한다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>씬 로드 직후 믹서를 찾고(없거나 다시 로드된 경우) 저장된 음량을 적용한다.</summary>
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mixer == null)
            mixer = FindMixer(scene);
        ApplyAll();
    }

    /// <summary>설정 패널처럼 믹서를 직접 알고 있는 쪽에서 등록한다. 등록 즉시 저장값을 적용한다.</summary>
    public static void RegisterMixer(AudioMixer targetMixer)
    {
        if (targetMixer == null)
            return;
        mixer = targetMixer;
        ApplyAll();
    }

    /// <summary>저장된 슬라이더 값(0~1)을 돌려준다. 저장된 적이 없으면 기본값.</summary>
    public static float Get(VolumeChannel channel) => PlayerPrefs.GetFloat(Key(channel), DefaultValue);

    /// <summary>슬라이더 값을 저장하고 즉시 믹서에 반영한다. 디스크 기록은 Save()에서 한 번에 한다.</summary>
    public static void Set(VolumeChannel channel, float value)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(Key(channel), value);
        Apply(channel, value);
    }

    /// <summary>변경한 설정을 저장소에 기록한다. 슬라이더를 움직일 때마다 쓰지 않도록 패널을 닫을 때 호출한다.</summary>
    public static void Save() => PlayerPrefs.Save();

    /// <summary>세 채널 모두 저장값으로 적용한다.</summary>
    public static void ApplyAll()
    {
        Apply(VolumeChannel.Master, Get(VolumeChannel.Master));
        Apply(VolumeChannel.Bgm, Get(VolumeChannel.Bgm));
        Apply(VolumeChannel.Sfx, Get(VolumeChannel.Sfx));
    }

    /// <summary>
    /// 0~1 값을 데시벨로 바꿔 믹서 파라미터에 넣는다.
    /// 사람 귀는 소리 크기를 로그로 느끼므로 20·log10(값)을 쓰면 슬라이더를 반으로 내렸을 때 '절반 정도'로 들린다.
    /// </summary>
    static void Apply(VolumeChannel channel, float value)
    {
        if (mixer == null)
            return;

        float db = value <= 0.0001f ? MinDb : 20f * Mathf.Log10(value);
        if (channel == VolumeChannel.Bgm)
            db += BgmBaseDb;

        if (!mixer.SetFloat(Parameter(channel), Mathf.Max(MinDb, db)))
            Debug.LogWarning($"[AudioVolume] 믹서에 공개된 파라미터가 없습니다: {Parameter(channel)}");
    }

    /// <summary>씬의 AudioSource가 사용하는 믹서 그룹에서 믹서를 찾는다. 없으면 null.</summary>
    static AudioMixer FindMixer(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true))
                if (source.outputAudioMixerGroup != null)
                    return source.outputAudioMixerGroup.audioMixer;
        return null;
    }

    static string Key(VolumeChannel channel) => "Audio.Volume." + channel;

    static string Parameter(VolumeChannel channel) => channel switch
    {
        VolumeChannel.Bgm => BgmParameter,
        VolumeChannel.Sfx => SfxParameter,
        _ => MasterParameter
    };
}
