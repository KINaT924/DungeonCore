using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>
/// 타이틀 설정 패널의 음량 슬라이더 3개(전체·음악·효과음)를 AudioVolumeSettings에 연결하는 컴포넌트.
/// 붙는 곳: TitleScene의 SettingsPanel.
///
/// - 패널이 처음 열릴 때 저장된 값으로 슬라이더 위치를 맞춘다(이때는 이벤트를 발생시키지 않는다).
/// - 슬라이더를 움직이면 즉시 소리에 반영하고, 패널을 닫을 때 한 번 저장한다.
/// </summary>
public sealed class VolumeSettingsPanel : MonoBehaviour
{
    [SerializeField] AudioMixer mixer;          // Assets/04.Audio/Mixer/Master.mixer
    [SerializeField] Slider masterSlider;       // MasterVolumeRow/VolumeSlider
    [SerializeField] Slider musicSlider;        // MusicVolumeRow/VolumeSlider
    [SerializeField] Slider sfxSlider;          // SfxVolumeRow/VolumeSlider

    // 호출 시점: 패널이 처음 활성화될 때. 믹서를 등록하고 슬라이더를 저장값과 연결한다.
    void Awake()
    {
        AudioVolumeSettings.RegisterMixer(mixer);
        Bind(masterSlider, VolumeChannel.Master);
        Bind(musicSlider, VolumeChannel.Bgm);
        Bind(sfxSlider, VolumeChannel.Sfx);
    }

    // 호출 시점: 패널을 닫을 때(비활성화). 움직인 값을 한 번에 저장한다.
    void OnDisable()
    {
        AudioVolumeSettings.Save();
    }

    // 슬라이더를 저장값 위치로 옮긴 뒤, 값이 바뀌면 해당 채널 음량을 바꾸도록 연결한다.
    static void Bind(Slider slider, VolumeChannel channel)
    {
        if (slider == null)
            return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(AudioVolumeSettings.Get(channel));   // 처음 위치를 맞출 때는 저장·적용을 다시 하지 않는다
        slider.onValueChanged.AddListener(value => AudioVolumeSettings.Set(channel, value));
    }
}
