using UnityEngine;
using UnityEngine.Audio;

public class DeathSound : MonoBehaviour
{
    /// <summary>
    /// 오브젝트의 사망 이벤트를 받아 사망 사운드를 재생하는 스크립트
    /// 소리는 독립된 오브젝트에서 재생하여 원래 대상이 사라져도 유지
    /// 재활성화되는 오브젝트의 경우 재생기록을 초기화하므로 재사용가능하게 설정
    /// </summary>


    [Header("사망 사운드")]
    [SerializeField] AudioClip deathClip;
    [SerializeField] AudioMixerGroup sfxGroup;               // 전체 효과음 음량을 SFX 믹서에서 함께 조절합니다.
    [SerializeField, Range(0f, 1f)] float volume = 0.7f;

    Health health;
    bool played;                                             // 이번 활성화에서 이미 사망음을 요청했는지 기록합니다.


    // 최초 생성 시 사망 이벤트를 제공할 Health를 찾습니다
    void Awake()
    {
        health = GetComponent<Health>();
    }


    // 생성 또는 풀 재사용 시 재생 기록을 초기화하고 이벤트를 구독합니다
    void OnEnable()
    {
        played = false;
        health.OnDied += PlayDeathSound;
    }

    // 비활성화 시 구독을 해제하여 다음 활성화 때 중복 연결을 막습니다
    void OnDisable()
    {
        health.OnDied -= PlayDeathSound;
    }

    // Health.OnDied가 발생할 때 호출
    // 사망 위치에 독립적인 AudioSource를 만들고 재생 종료 후 제거
    void PlayDeathSound()
    {
        if (played)
            return;

        played = true;

        // 음원이 없으면 사망 처리를 방해하지 않고 소리만 생략
        if (deathClip == null)
            return;

        // 부모를 지정하지 않아 몬스터 비활성화와 독립적으로 유지
        GameObject soundObject = new GameObject("DeathSound_OneShot");
        soundObject.transform.position = transform.position;

        AudioSource source = soundObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.clip = deathClip;
        source.outputAudioMixerGroup = sfxGroup;
        source.volume = volume;

        // 사망 위치에서 거리감 있게 재생하고 이동에 따른 음높이 변화는 X
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 5f;
        source.maxDistance = 30f;

        source.Play();

        // Pitch 1 기준 음원 길이만큼 재생하고 약간의 여유 뒤 정리
        Destroy(soundObject, deathClip.length + 0.1f);
    }
}
