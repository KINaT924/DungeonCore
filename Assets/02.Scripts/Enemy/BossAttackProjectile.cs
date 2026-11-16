using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 고정된 도착점으로 날아가거나 떨어진 뒤 한 번 폭발하는 스크립트
/// 매 프레임 플레이어 위치를 읽어 유도하지 않도록 설정
/// </summary>

public class BossAttackProjectile : MonoBehaviour
{
    // 예약된 낙석을 추적, 시각 경고 리스트 
    static readonly List<BossAttackProjectile> pendingRocks = new List<BossAttackProjectile>();
    public static IReadOnlyList<BossAttackProjectile> PendingRocks => pendingRocks;

    // 플레이마다 낙석 목록을 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetWarnings() { pendingRocks.Clear(); }

    // HUD가 매 프레임 호출, 마법진 생성 시 보관한 경고 반경과 플레이어 몸의 겹침으로 미리 경고 호출
    public bool Threatens(Health victim)
    {
        if (!armed || !falling || !isActiveAndEnabled || player != victim 
            || victim == null || victim.IsDead
            || owner == null || owner.IsFinished) 
            return false;

        Vector3 delta = victim.transform.position - target;
        float vertical = Mathf.Abs(delta.y);
        delta.y = 0;
        float bodyRadius = 0f;

        if (warningBody != null && warningBody.enabled)
        {
            // 발 중심이 원 바깥이어도 몸 일부가 경고 구역에 닿으면 진입 전에 회피할 여유시간 존재
            Vector3 scale = warningBody.transform.lossyScale;
            bodyRadius = warningBody.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));

            Vector3 bodyCenter = warningBody.bounds.center;
            delta.x = bodyCenter.x - target.x;
            delta.z = bodyCenter.z - target.z;
        }
        float alertRadius = previewRadius + bodyRadius;
        return delta.sqrMagnitude <= alertRadius * alertRadius && vertical < 3f;
    }

    public bool falling;
    public Transform visual;
    public float arcHeight = 1.2f;

    BossQueenController owner;
    Health player;
    Vector3 start, target, previous;
    float duration, delay, age, damage, radius;

    // 시각 마법진의 생성 직후 원래 크기를 보관
    // 등장 연출로 .3배 축소되어도 경고 범위는 그대로 설정
    float previewRadius;
    CharacterController warningBody;
    BossSpellVisual impact, warning;
    bool armed;

    // 발사 시 한 번 받은 목표 좌표와 피해 수치를 저장시켜두는 메소드
    // delay는 낙석의 바닥 예고 시간
    public void Launch(BossQueenController source, Health victim, Vector3 destination,
        float seconds, float warningSeconds, float amount, float blastRadius, BossSpellVisual impactPrefab, BossSpellVisual marker)
    {
        owner = source;
        player = victim;
        start = previous = transform.position;
        target = destination;

        duration = Mathf.Max(.1f, seconds);
        delay = warningSeconds;
        damage = amount;
        radius = blastRadius;
        impact = impactPrefab;
        warning = marker;

        // Launch는 보스가 마법진을 생성한 같은 프레임에 호출
        // 낙하 대기 중에도 곧바로 HUD가 검사할 수 있도록 설정
        previewRadius = marker != null ? Mathf.Max(blastRadius, Mathf.Abs(marker.transform.lossyScale.x)) : blastRadius;
        warningBody = victim != null ? victim.GetComponent<CharacterController>() : null;
        age = 0;
        armed = true;

        if (visual != null)
            visual.gameObject.SetActive(delay <= 0);
        if (falling && !pendingRocks.Contains(this))
            pendingRocks.Add(this);
    }

    void Update()
    {
        if (!armed)
            return;
        if (owner == null || owner.IsFinished || player == null || player.IsDead)
        {
            Destroy(gameObject);
            return;
        }

        age += Time.deltaTime;
        if (age < delay)
            return;
        if (visual != null)
            visual.gameObject.SetActive(true);

        float t = Mathf.Clamp01((age - delay) / duration);
        float progress = falling ? t * t : t;
        Vector3 next = Vector3.Lerp(start, target, progress);

        if (!falling)
            next += Vector3.up * Mathf.Sin(t * Mathf.PI) * arcHeight;
        if (!falling && Physics.Linecast(previous, next, out var hit, 1 << LayerMask.NameToLayer("Ground"), QueryTriggerInteraction.Ignore))
        {
            transform.position = hit.point; Detonate(hit.point);
            return; 
        }
        transform.position = next; previous = next;

        if (falling && visual != null)
            visual.Rotate(new Vector3(85, 35, 60) * Time.deltaTime, Space.Self);
        if (t >= 1)
            Detonate(target);
    }

    // 도착점에서 한 번만 플레이어의 수평 거리를 검사하고 Health에 피해를 전달
    // 회피 무적은 기존 Health가 판정하고있기때문에 여기서 관리x
    void Detonate(Vector3 point)
    {
        if (!armed)
            return;
        armed = false;

        pendingRocks.Remove(this);

        if (warning != null)
            Destroy(warning.gameObject);
        if (impact != null)
        {
            var fx = Instantiate(impact, point + Vector3.up * .08f, Quaternion.Euler(90, 0, 0), transform.parent);
            fx.transform.localScale = Vector3.one * radius;
        }

        if (player != null && !player.IsDead && owner != null && !owner.IsFinished)
        {
            Vector3 delta = player.transform.position - point; float vertical = Mathf.Abs(delta.y); delta.y = 0;
            if (delta.sqrMagnitude <= radius * radius && vertical < 3) player.TakeDamage(damage);
        }
        Destroy(gameObject);
    }

    // 중단되어 남은 경고 표식도 같이 제거
    void OnDestroy() 
    {
        pendingRocks.Remove(this);
        if (warning != null)
            Destroy(warning.gameObject);
    }

    // 전투 종료나 비활성화로 사라진 위험물은 경고 목록에서도 즉시 제외
    void OnDisable() 
    { 
        pendingRocks.Remove(this); 
    }
}
