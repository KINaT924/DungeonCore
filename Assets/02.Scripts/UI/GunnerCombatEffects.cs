using System;
using DungeonCoreFx;
using UnityEngine;

/// <summary>
/// 실제 총기의 발사
/// Ray 결과와 스킬 상태를 읽어 무기별 카툰 연출만 담당하는 스크립트
/// </summary>

public sealed class GunnerCombatEffects : MonoBehaviour
{
    [Serializable]
    public class WeaponLook
    {
        public GameObject muzzle;
        public Color tracerColor = Color.white;
        public float tracerWidth = .035f;
    }

    [Header("실제 상태")]
    public GunnerWeaponSkillController skills;
    public Health health;

    [Header("권총 / 라이플 / 샷건 전용 연출")]
    public WeaponLook pistol = new WeaponLook();
    public WeaponLook rifle = new WeaponLook();
    public WeaponLook shotgun = new WeaponLook();
    public GameObject coreBreakShot;
    public GameObject rifleAura;
    public GameObject shotgunWave;

    [Header("실제 명중 표면별 연출")]
    public GameObject coreImpact;
    public GameObject surfaceImpact;
    public GameObject enemyImpact;
    public CombatTracerVisual tracerPrefab;
    GameObject activeAura;
    public GameObject ActiveAura => activeAura;

    // 누락된 같은 플레이어의 참조만 자동으로 찾습니다
    void Awake()
    {
        if (skills == null)
            skills = GetComponent<GunnerWeaponSkillController>();
        if (health == null)
            health = GetComponent<Health>();
    }

    // 실제 4초 효과가 유지되는 동안만 오라를 부착하고 종료,전환,사망 시 방출을 중지
    void LateUpdate()
    {
        bool active = skills != null && skills.IsRifleInfiniteAmmoActive && health != null && !health.IsDead;

        if (active && activeAura == null && rifleAura != null)
        {
            activeAura = Instantiate(rifleAura, transform);
            activeAura.transform.localPosition = new Vector3(0, .08f, 0);
            activeAura.transform.localRotation = Quaternion.identity;
        }
        else if (!active && activeAura != null) StopAura();
    }

    // 실제 Fire 검사가 성공한 시점에만 무기별 섬광과 스킬 충격파를 재생
    public void PlayMuzzle(GunnerWeaponType weapon, Vector3 origin, Vector3 direction, bool coreSkill, bool burstSkill)
    {
        var look = Look(weapon);
        GameObject prefab = coreSkill && weapon == GunnerWeaponType.Pistol ? coreBreakShot : look.muzzle;

        if (prefab != null) 
            Instantiate(prefab, origin, Quaternion.LookRotation(direction));
        if (burstSkill && weapon == GunnerWeaponType.Shotgun && shotgunWave != null)
            Instantiate(shotgunWave, transform.position + Vector3.up * .07f, Quaternion.identity);
    }

    // 즉시 판정된 Ray 결과를 시각 탄도에 전달
    // 체력,명중을 지연하거나 다시 계산하지 않음
    public void PlayPath(GunnerWeaponType weapon, Vector3 origin, Vector3 end, bool hasImpact, Vector3 normal,
        bool isCore, bool isEnemy, bool coreSkill)
    {
        GameObject impact = hasImpact ? (isCore ? coreImpact : isEnemy ? enemyImpact : surfaceImpact) : null;
        var look = Look(weapon);

        if (tracerPrefab != null)
        {
            var tracer = Instantiate(tracerPrefab);
            tracer.Initialize(origin, end,
                coreSkill ? new Color(1f, .88f, 1f) : look.tracerColor,
                coreSkill ? .075f : look.tracerWidth, impact, normal);
        }
        else if (impact != null)
        {
            Vector3 outward = normal.sqrMagnitude > .0001f ? normal.normalized : Vector3.up;
            Instantiate(impact, end + outward * .025f, Quaternion.LookRotation(outward));
        }
    }

    // 무기 enum에 대응하는 외형 설정을 반환
    WeaponLook Look(GunnerWeaponType weapon)
    {
        return weapon == GunnerWeaponType.Rifle ? rifle : weapon == GunnerWeaponType.Shotgun ? shotgun : pistol;
    }

    // 남아 있는 입자가 자연스럽게 사라지도록 월드로 분리한 후 짧게 정리
    void StopAura()
    {
        if (activeAura == null)
            return;
        activeAura.transform.SetParent(null, true);

        foreach (var ps in activeAura.GetComponentsInChildren<ParticleSystem>()) 
            ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        Destroy(activeAura, .6f); 
        activeAura = null;
    }

    // 플레이어가 꺼질 때도 오라가 남지 않게 정리
    void OnDisable()
    {
        StopAura(); 
    }
}
