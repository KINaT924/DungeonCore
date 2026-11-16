using UnityEngine;

/// <summary>
/// 거너의 현재 무기 고유 스킬을 처리하는 스크립트
/// 권총 - 코어브레이크 - 코어에만 추가데미지를 입히는 스킬
/// 라이플 - 오버드라이브 - 지속시간동안 탄약 감소 x
/// 샷건 - 집중포화 - 3발 연속 발사하며 집탄율이 높게 발사 
/// </summary>


[DisallowMultipleComponent]
public class GunnerWeaponSkillController : MonoBehaviour
{
    [Header("레퍼런스")]
    [SerializeField] PlayerInputReader inputReader;
    [SerializeField] PlayerGunController gun;
    [SerializeField] GunnerAimController aim;
    [SerializeField] Health health;

    [Header("라이플 - 오버드라이브")]
    [SerializeField, Min(0.01f)] float rifleDuration = 4f;
    [SerializeField, Min(0.01f)] float rifleCooldown = 14f;

    [Header("권총 - 코어 브레이크")]
    [SerializeField, Min(0.01f)] float pistolCooldown = 8f;
    [SerializeField, Min(1f)] float pistolCoreDamageMultiplier = 4f;

    [Header("샷건 - 집중 포화")]
    [SerializeField, Min(0.01f)] float shotgunCooldown = 12f;
    [SerializeField, Min(0.01f)] float shotgunInterval = 0.2f;
    [SerializeField, Range(0f, 45f)] float shotgunSpread = 1f;



    // 종료 시각을 보관하므로 Update 실행 순서와 관계없이 만료를 판정합니다.
    // Time.time은 일시정지 중 멈추므로 효과와 쿨타임도 함께 멈춥니다.
    float rifleEffectEndTime;
    float rifleReadyTime;
    float pistolReadyTime;
    int shotgunShotsRemaining;
    float nextShotgunShotTime;
    float shotgunReadyTime;
    Vector3 shotgunDirection;
    bool isShotgunBursting;


    // 총기가 탄약을 차감하기 직전에 읽는 실제 효과 상태
    public bool IsRifleInfiniteAmmoActive => isActiveAndEnabled && health != null && !health.IsDead &&
        gun != null && gun.CurrentWeaponType == GunnerWeaponType.Rifle && Time.time < rifleEffectEndTime;

    // 이후 HUD에서 효과 지속시간과 재사용 대기시간을 표시할 수 있도록 설정
    public float RifleEffectRemaining => IsRifleInfiniteAmmoActive ? Mathf.Max(0f, rifleEffectEndTime - Time.time) : 0f;

    public float RifleCooldownRemaining =>
        Mathf.Max(0f, rifleReadyTime - Time.time);
    public float PistolCooldownRemaining =>
    Mathf.Max(0f, pistolReadyTime - Time.time);
    public bool IsShotgunBursting =>
        isActiveAndEnabled && isShotgunBursting;
    public float ShotgunCooldownRemaining =>
      Mathf.Max(0f, shotgunReadyTime - Time.time);



    void Awake()
    {
        if (inputReader == null)
            inputReader = GetComponent<PlayerInputReader>();
        if (gun == null)
            gun = GetComponent<PlayerGunController>();
        if (aim == null)
            aim = GetComponent<GunnerAimController>();
        if (health == null)
            health = GetComponent<Health>();
    }

    // R키를 눌러 활성화 될 때 구독
    void OnEnable()
    {
        if (inputReader != null)
            inputReader.WeaponSkillPressed += HandleWeaponSkillPressed;
        if (health != null)
            health.OnDamaged += HandleSkillDamaged;
    }

    // 비활성화 시 입력 구독과 임시 효과를 정리
    // 다시 활성화해도 기존 쿨타임은 초기화하지 않도록 설정
    void OnDisable()
    {
        if (inputReader != null)
            inputReader.WeaponSkillPressed -= HandleWeaponSkillPressed;
        if (health != null)
            health.OnDamaged -= HandleSkillDamaged;

        CancelTemporaryEffect();
    }

    // 사망했을 때 진행 중인 임시 효과를 종료
    void Update()
    {
        if (health == null || health.IsDead)
        {
            CancelTemporaryEffect();
            return;
        }

        if (!isShotgunBursting || Time.timeScale <= 0f)
            return;

        if (gun == null || !gun.isActiveAndEnabled ||
            gun.CurrentWeaponType != GunnerWeaponType.Shotgun ||
            inputReader == null || !inputReader.isActiveAndEnabled ||
            aim == null || !aim.IsAiming)
        {
            CancelShotgunBurst();
            return;
        }

        if (Time.time < nextShotgunShotTime)
            return;

        // 후속 발사가 막혔으면 남은 발사를 취소
        if (!gun.TryFireShotgunSkill(
            shotgunDirection, shotgunSpread, false))
        {
            CancelShotgunBurst();
            return;
        }

        shotgunShotsRemaining--;

        if (shotgunShotsRemaining <= 0)
            CancelShotgunBurst();
        else
            nextShotgunShotTime = Time.time + shotgunInterval;
    }

    // R 입력 시 현재 무기의 사용 조건을 검사
    // 성공했을 때만 쿨타임을 시작
    void HandleWeaponSkillPressed()
    {
        if (isShotgunBursting)
            return;
        if (inputReader == null || !inputReader.isActiveAndEnabled ||
            gun == null || !gun.isActiveAndEnabled ||
            health == null || health.IsDead ||
            aim == null || !aim.IsAiming)
            return;

        if (Time.timeScale <= 0f ||
            Cursor.lockState != CursorLockMode.Locked)
            return;

        // 공통 사용 조건을 통과한 뒤 현재 무기의 스킬로 분기합니다.
        if (gun.CurrentWeaponType == GunnerWeaponType.Pistol)
        {
            if (PistolCooldownRemaining > 0f)
                return;

            // 총기가 실제 발사를 승인했을 때만 쿨타임을 소비합니다.
            if (gun.TryFireCoreBreak(pistolCoreDamageMultiplier))
            {
                pistolReadyTime = Time.time + pistolCooldown;
                Debug.Log("[권총 스킬] 코어 브레이크 발사", this);
            }

            return;
        }

        if (gun.CurrentWeaponType == GunnerWeaponType.Shotgun)
        {
            StartShotgunBurst();
            return;
        }
        if (gun.CurrentWeaponType != GunnerWeaponType.Rifle)
            return;

        // 빈 탄창과 재장전 중에는 사용하지 않습니다.
        if (gun.IsReloading || gun.CurrentAmmo <= 0 ||
            RifleCooldownRemaining > 0f)
            return;

        rifleEffectEndTime = Time.time + rifleDuration;
        rifleReadyTime = Time.time + rifleCooldown;

        Debug.Log("[라이플 스킬] 오버드라이브 시작", this);
    }

    // 실제 무기 전환이나 컴포넌트 비활성화 시 호출
    // 지속 효과만 종료하고 재사용 대기시간은 유지
    public void CancelTemporaryEffect()
    {
        rifleEffectEndTime = 0f;
        CancelShotgunBurst();
    }

    // 샷건 R 입력 시 첫 발을 발사 
    // 첫 발 성공 후에만 남은 두 발 발사와 동시에 쿨타임 작동
    void StartShotgunBurst()
    {
        if (ShotgunCooldownRemaining > 0f ||
            gun.IsReloading || gun.CurrentAmmo < 3)
            return;

        shotgunDirection = gun.GetShotgunSkillDirection();
        if (shotgunDirection.sqrMagnitude < 0.000001f)
            return;

        // 첫 발이 처리되는 동안에도 일반 입력을 차단합니다.
        isShotgunBursting = true;

        if (!gun.TryFireShotgunSkill(
            shotgunDirection, shotgunSpread, true))
        {
            CancelShotgunBurst();
            return;
        }

        shotgunShotsRemaining = 2;
        nextShotgunShotTime = Time.time + shotgunInterval;
        shotgunReadyTime = Time.time + shotgunCooldown;

        Debug.Log("[샷건 스킬] 집중 포화 시작", this);
    }

    // 연사 완료,실패,취소 시 남은 발사와 행동 잠금을 해제
    // 이미 시작한 쿨타임과 소비한 탄약은 그대로 소모된채로
    void CancelShotgunBurst()
    {
        isShotgunBursting = false;
        shotgunShotsRemaining = 0;
    }

    // 생존 피격 이벤트에서 호출합니다.
    // 피격 모션과 스킬 사격이 겹치지 않도록 집중 포화만 취소
    void HandleSkillDamaged(float damage)
    {
        CancelShotgunBurst();
    }
}
