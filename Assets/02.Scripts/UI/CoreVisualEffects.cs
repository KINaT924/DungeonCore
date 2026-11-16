using DungeonCoreFx;
using UnityEngine;

/// <summary>
/// 기존 코어 Health의 피해를 새 외형의 흰색 반응,눌림에 연결하는 스크립트
/// </summary>
public sealed class CoreVisualEffects : MonoBehaviour
{
    public Health health;
    public CoreIdleMotion motion;

    // 필요한 참조가 비어 있으면 같은 코어 루트에서 찾습니다
    void Awake()
    {
        if (health == null) 
            health = GetComponent<Health>();
        if (motion == null) 
            motion = GetComponentInChildren<CoreIdleMotion>(true);
    }

    // 재생성마다 피해 알림을 한 번 구독
    void OnEnable()
    {
        if (motion != null)
            motion.PlayHitAt(-1000f);
        if (health != null)
            health.OnDamaged += PlayHit;
    }

    // 치명타는 기존 파괴 흐름이 담당하고 살아 있는 코어만 짧게 반응
    void PlayHit(float damage)
    {
        if (damage > 0f && health != null && !health.IsDead && motion != null)
            motion.PlayHit();
    }

    // 코어 비활성화 시 구독을 해제하여 재생성 때 반응이 중복되지 않게 설정
    void OnDisable()
    {
        if (health != null) 
            health.OnDamaged -= PlayHit;
    }
}
