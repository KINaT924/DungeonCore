using UnityEngine;
using UnityEngineInternal;
using System.Collections.Generic;

public class PlayerGunController : MonoBehaviour
{
    /// <summary>
    /// 거너의 기본 사격을 담당하는 스크립트입니다.
    /// 기존 입력 이벤트를 받아 회피 , 사망 , 발사 간격을 검사한 후
    /// 카메라 중앙의 Raycast를 통해 확인한 대상에게 피해를 전달합니다
    /// </summary>

    [Header("레퍼런스")]
    [SerializeField] PlayerInputReader inputReader;
    [SerializeField] PlayerDodgeController dodgeCtr;
    [SerializeField] Health health;
    [SerializeField] Camera aimCamera;                  // 에임의 중앙점이 될 화면의 카메라 ( 출력 카메라 )
    [SerializeField] Transform muzzlePoint;             // 총구 위치 ( 총알이 발사되는 위치 )
    [SerializeField] PlayerAnimationController animCtr;
    [SerializeField] PlayerGrowth playerGrowth;         // 강화 상태 참조
    [SerializeField] GunnerAimController aimCtr;
    [SerializeField] GunnerWeaponSkillController weaponSkill;
    [SerializeField] GunnerCombatEffects combatEffects; // 실제 발사 결과의 시각 연출만 전달

    [Header("무기 전투 데이터 ")]          // 전투에 사용될 기본 설정
    [SerializeField] GunnerWeaponData weaponData;
    [SerializeField] GunnerWeaponData[] weaponDatas = new GunnerWeaponData[3];
    readonly int[] ammoByWeapon = new int[3];
    readonly float[] nextFireTimeByWeapon = new float[3];

    [Header("사운드")]
    [SerializeField] AudioSource gunAudio;              // 총기 발포 사운드
    [SerializeField] AudioClip fireClip;                // 발사 성공시 재생할 사운드 클립
    [SerializeField, Range(0f, 1f)] float fireVolume = 0.7f;          // 발사 사운드 볼륨

    [Header("발사, 명중 이펙트 관련")]
    [SerializeField] GameObject flashPrefab;                // 총구 섬광 fx
    [SerializeField] GameObject surfaceHitPrefab;       // 배경 명중 fx
    [SerializeField] GameObject enemyHitPrefab;        // 몬스터 명중 fx
    [SerializeField, Min(0.6f)] float effectLifetime = 1.2f;        // 가장 긴 입자 수명보다 여유 있게 유지한 후 이펙트를 제거
    [SerializeField, Min(0f)] float hitEffectOffset = 0.01f;        // 명중 이펙트가 표면 안에 묻히거나 겹쳐 깜짝이지 않도록하기위한 일정 거리

    [Header("권총 설정")]
    [SerializeField, Min(0f)] float damage = 20f;
    [SerializeField, Min(0.01f)] float fireInterval = 0.3f;         // 발사 후 최소한의 발사간격 시간
    [SerializeField, Min(0.1f)] float range = 50f;                  // 사거리
    [SerializeField] LayerMask hitMask;                             // 총알이 도달하는 레이어 ( 적, 벽, 바닥 )
    [SerializeField, Min(0.001f)] float muzzleCheckRadius = 0.01f;  // 총구 위치가 벽에 박혀있을 때 발사되는 문제를 방지하기 위한 최소한의 반지름

    [Header("샷건 넉백 설정")]
    [SerializeField, Min(0f)] float shotgunKnockBackDis = 2f;
    [SerializeField, Min(0.01f)] float shotgunKnockBackDur = 0.25f;
    readonly HashSet<EnemyController> shotgunHitEnemies = new HashSet<EnemyController>();

    [Header("탄창 설정")]
    [SerializeField, Min(1)] int magazineSize = 12;                 // 탄창 발수 크기 
    [SerializeField, Min(0.01f)] float reloadDur = 1.5f;            // 재장전 시간
    [SerializeField, Min(0f)] float reloadStartDelay = 0.15f;   // 마지막 발사 모션이 재장전 모션에 즉시 잘리지않도록 확보하는 시간
    [SerializeField] int currentAmmo;                               // 현재 탄창 발수
    [SerializeField] bool isReloading;                              // 현재 장전중인가
    float reloadEndTime;                                            // 재장전이 끝나는 게임시간
    float reloadStartTime;
    float activeReloadDuration;                 
    bool reloadPending;

    public int CurrentAmmo => currentAmmo;
    public int MagazineSize => Mathf.Max(1, (weaponData != null ? weaponData.MagazineSize : magazineSize) 
                                + (playerGrowth != null ? playerGrowth.MagazineSizeBonus : 0));
    public bool IsReloading => isReloading;
    public bool IsReloadAnimActive => isReloading && !reloadPending;
    public float CurrentDamage => (weaponData != null ? weaponData.Damage : damage)
                                + (playerGrowth != null ? playerGrowth.GetWeaponDamageBonus(CurrentWeaponType) : 0f);
    public float CurrentFireInterval => Mathf.Max(0.01f, (weaponData != null ? weaponData.FireInterval : fireInterval)
                                * (1f - (playerGrowth != null ? playerGrowth.FireIntervalReduction : 0f)));
    public float CurrentReloadDuration => Mathf.Max(0.01f, (weaponData != null ? weaponData.ReloadDuration : reloadDur)
                                * (1f - (playerGrowth != null ? playerGrowth.ReloadDurationReduction : 0f)));
    public float CurrentRange => Mathf.Max(0.1f, weaponData != null ? weaponData.Range : range);
    public float ReloadProgress
    {
        get 
        {
            if (!IsReloadAnimActive) 
                return 0f;

            return Mathf.Clamp01(1f - (reloadEndTime - Time.time) / activeReloadDuration);
        }
    }
    public GunnerWeaponType CurrentWeaponType => weaponData != null ? weaponData.WeaponType : GunnerWeaponType.Pistol;
    bool IsAutomaticWeapon => weaponData != null && weaponData.Automatic;


    [SerializeField] bool showDebug = true;     // 디버그 채킹

    float nextFireTime;     // 설정된 시간 이후에 다음 발사가 가능합니다


    private void Awake()
    {
        if (combatEffects == null)
            combatEffects = GetComponent<GunnerCombatEffects>();
        if (inputReader == null)
            inputReader = GetComponent<PlayerInputReader>();
        if (dodgeCtr == null)
            dodgeCtr = GetComponent<PlayerDodgeController>();
        if (health == null)
            health = GetComponent<Health>();
        if (aimCamera == null)
            aimCamera = Camera.main;
        if (muzzlePoint == null)
            Debug.LogError("연결참조 확인. MuzzlePoint", this);
        if (animCtr == null)
            animCtr = GetComponent<PlayerAnimationController>();
        if (playerGrowth == null)
            playerGrowth = GetComponent<PlayerGrowth>();
        if(aimCtr == null)
            aimCtr = GetComponent<GunnerAimController>();
        if (weaponSkill == null)
            weaponSkill = GetComponent<GunnerWeaponSkillController>();

        // 게임시작 시 풀 탄창
        magazineSize = Mathf.Max(1, magazineSize);
        reloadDur = Mathf.Max(0.01f, reloadDur);
        currentAmmo = MagazineSize;
        isReloading = false;
        reloadPending = false;
        reloadStartDelay = Mathf.Max(0f, reloadStartDelay);
        InitializeWeaponAmmo();
    }

    // 활성화 시 좌클릭을 통한 동작 실행
    private void OnEnable()
    {
        if (inputReader != null)
            inputReader.AttackPressed += HandleAttackPressed;
    }

    // 비활성화 시 동작 해제
    private void OnDisable()
    {
        if (inputReader != null)
            inputReader.AttackPressed -= HandleAttackPressed;
    }


    private void Update()
    {
        if (health == null)
            return;
        // 사망 시에 재장전을 취소하고 탄창을 채우지않습니다.
        if(health.IsDead)
        {
            isReloading = false;
            reloadPending = false;
            return;
        }

        UpdateReload();

        if (IsAutomaticWeapon && inputReader != null && inputReader.isActiveAndEnabled && inputReader.AttackHeld)
        {
            TryFireSingle();
        }
    }



    // 발사를 시도, 기존 조준, 행동 상태,발사 간격을 검사한 뒤 실제 발사에 성공했다면
    // 탄약, 사운드, 애니메이션, 다음 발사 시각을 갱신
    bool TryFireSingle(float coreDamageMultiplier = 1f, bool isShotgunSkill = false, 
        Vector3 fixedDirection = default, float spreadOverride = -1f)
    {
        // 집중 포화 중 좌클릭으로 추가 사격이 끼어들지 못하게 합니다.
        if (!isShotgunSkill && weaponSkill != null && weaponSkill.IsShotgunBursting)
            return false;
        if (!CanFire(isShotgunSkill))
            return false;

        UpdateReload();

        if (isReloading)
            return false;

        if (currentAmmo <= 0)
        {
            StartReload();
            return false;
        }

        // 발사에 필요한 방향·총구 검사를 통과하지 못하면 소비하지 않습니다.
        if (!Fire(coreDamageMultiplier, isShotgunSkill ? (Vector3?)fixedDirection : null, spreadOverride))
            return false;

        bool preserveAmmo = weaponSkill != null &&
                            weaponSkill.IsRifleInfiniteAmmoActive;

        if (!preserveAmmo)
            currentAmmo--;

        PlayFireSound();

        if (animCtr != null)
            animCtr.PlayGunFireAnimation();

        nextFireTime = Time.time + CurrentFireInterval;

        if (showDebug)
        {
            Debug.Log(
                $"[{CurrentWeaponType}] 탄창: {currentAmmo}/{MagazineSize}",
                this);
        }

        if (currentAmmo == 0)
            StartReload();

        return true;
    }

    /// <summary>
    /// 스킬 컨트롤러가 권총 R 입력을 처리할 때 호출합니다.
    /// 빈 탄창·재장전·비활성 상태에서는 스킬 발사를 거절합니다.
    /// 반환값은 스킬 쿨타임을 시작해도 되는지 판단하는 데 사용합니다.
    /// </summary>
    public bool TryFireCoreBreak(float coreDamageMultiplier)
    {
        if (!isActiveAndEnabled ||
            CurrentWeaponType != GunnerWeaponType.Pistol ||
            isReloading || currentAmmo <= 0)
            return false;

        return TryFireSingle(Mathf.Max(1f, coreDamageMultiplier));
    }

    /// <summary>
    /// 집중 포화 시작 시 호출합니다.
    /// 현재 총구에서 조준점을 향하는 방향을 반환하여 세 발에 재사용합니다.
    /// </summary>
    public Vector3 GetShotgunSkillDirection()
    {
        if (muzzlePoint == null || aimCamera == null)
            return Vector3.zero;

        Vector3 direction = GetAimPoint() - muzzlePoint.position;
        if (direction.sqrMagnitude < 0.000001f)
            return Vector3.zero;

        direction.Normalize();

        return Vector3.Dot(direction, aimCamera.transform.forward) > 0f
            ? direction
            : Vector3.zero;
    }

    /// <summary>
    /// 스킬 컨트롤러가 연속 발사할 때마다 호출합니다.
    /// 첫 발은 기존 발사 대기시간을 지키고,
    /// 후속 발사는 스킬 컨트롤러가 지정한 간격을 사용합니다.
    /// 실제 발사 성공 여부를 반환합니다.
    /// </summary>
    public bool TryFireShotgunSkill(
        Vector3 direction, float spreadAngle, bool firstShot)
    {
        if (!isActiveAndEnabled ||
            CurrentWeaponType != GunnerWeaponType.Shotgun ||
            isReloading || currentAmmo <= 0 ||
            direction.sqrMagnitude < 0.000001f)
            return false;

        if (firstShot && Time.time < nextFireTime)
            return false;

        return TryFireSingle(
            1f, true, direction.normalized,
            Mathf.Clamp(spreadAngle, 0f, 45f));
    }

    // 공격 버튼을 누른 순간 호출하며 권총,샷건은 한 번 발사하고 라이플은 유지 입력 처리를 사용하여 중복 발사를 방지합니다
    void HandleAttackPressed()
    {
        if (IsAutomaticWeapon)
            return;

        TryFireSingle();
    }

    // 탄환을 차감한 직후 PlayOneShot을 사용해이전 발포음의 잔향을 끊지않고 재생
    void PlayFireSound()
    {
        if(gunAudio == null || fireClip == null)
            return;

        gunAudio.PlayOneShot(fireClip, fireVolume);
    }


    // 현재 발사가 가능한지 검사
    // 반환값이 true이면 발사, false일 시 입력을 무시합니다
    bool CanFire(bool ignoreFireInterval = false)
    {
        // 필수 참조 검사
        if (inputReader == null || aimCamera == null || dodgeCtr == null || muzzlePoint == null || health == null) 
            return false;
        // 사격 불가한 상태 검사
        if (health.IsDead || dodgeCtr.IsDodging)
            return false;
        // 우클릭 조준을 하고있을 때 좌클릭 공격 허용 검사
        if(aimCtr == null || !aimCtr.IsAiming)
            return false;
        // 일시정지 상태이거나 커서가 풀린 상태에서 사격이 불가능하게 설정
        if (Time.timeScale <= 0f || Cursor.lockState != CursorLockMode.Locked)
            return false;
        // 일반 사격은 무기별 발사 간격을 지킵니다.
        // 집중 포화는 스킬 컨트롤러에서 0.2초 간격을 관리합니다.
        if (!ignoreFireInterval && Time.time < nextFireTime)
            return false;

        return true;
    }


    // 탄을 전부 소진하여 재장전 종료 시각을 정하는 호출 메소드
    void StartReload()
    {
        if(health == null ||  health.IsDead)
            return;
        if(isReloading || currentAmmo >= MagazineSize)
            return;

        // 재장전 요청이 승인된 시점에서 강화 상태로 소요 시간을 확정
        activeReloadDuration = CurrentReloadDuration;

        isReloading = true;
        reloadPending = true;
        reloadStartTime = Time.time + reloadStartDelay;
    }
    // 재장전이 끝났는지 검사하고 끝났다면 탄창을 채웁니다
    void UpdateReload()
    {
        if (!isReloading)
            return;
        // 사망 이후에는 이 메소드가 호출돼도 탄약을 지급하지않게 설정
        if (health == null || health.IsDead)
        {
            isReloading = false;
            reloadPending = false;
            return;
        }

        if (reloadPending)
        {
            if (Time.time < reloadStartTime)
                return;

            reloadPending = false;
            reloadEndTime = reloadStartTime + activeReloadDuration;

            if (showDebug)
                Debug.Log($"[권총] 재장전 시작: {activeReloadDuration:F3}초", this);
        }

        if (Time.time < reloadEndTime)
            return;
        currentAmmo = MagazineSize;
        isReloading = false;

        if (showDebug)
            Debug.Log($"[권총] 재장전 완료: {currentAmmo}/{MagazineSize}", this);
    }

    // 사격할 때 화면 중앙의 조준 위치를 계산, 첫 충돌을 검사하고 해당 충돌에 대한 반환값을 저장합니다
    // 해당 메소드에서 피해를 주는 계산을 하진않습니다
    public Vector3 GetAimPoint()
    {
        Ray aimRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        float cameraToMuzzle = Vector3.Distance(aimRay.origin, muzzlePoint.position);
        float aimDis = CurrentRange + cameraToMuzzle;

        if (Physics.Raycast(aimRay, out RaycastHit hit, aimDis, hitMask, QueryTriggerInteraction.Ignore))
            return hit.point;

        return aimRay.GetPoint(aimDis);
    }


    // 화면 중앙 방향으로 한번 검사 후 첫 충돌 상대에게 피해를 줍니다
    // 벽이나 바닥같은 물체도 총을 막지만 피해는 받지않습니다
    bool Fire(float coreDamageMultiplier = 1f, Vector3? fixedDirection = null, float spreadOverride = -1f)
    {
        Vector3 origin = muzzlePoint.position;

        // 레이캐스트 시작점이 장애물 내부에 있으면 그 장애물을 놓칠 수 있으므로 작은 구체를 만들어 충돌을 검사합니다
        if(Physics.CheckSphere(origin, muzzleCheckRadius, hitMask, QueryTriggerInteraction.Ignore))
        {
            if (showDebug)
                Debug.LogWarning($"총구 위치가 장애물 내부에 있습니다. 발사하지 않습니다.", this);
            return false;
        }

        // 집중 포화는 시작할 때 저장한 월드 방향을 사용합니다.
        Vector3 aimPoint = fixedDirection.HasValue ? origin + fixedDirection.Value.normalized * CurrentRange : GetAimPoint();
        Vector3 offset = aimPoint - origin;
        float aimDis = offset.magnitude;

        // 총구 위치가 너무 가까우면 발사 x
        if (aimDis < 0.001f)
            return false;

        Vector3 dir = offset / aimDis;

        // 뒤쪽으로 발사되는 문제를 방지하기 위해 카메라와 총구 위치의 방향을 검사
        if (!fixedDirection.HasValue && Vector3.Dot(dir, aimCamera.transform.forward) <= 0f)
        {
            if(showDebug)
                Debug.LogWarning($"총구 위치가 카메라 뒤쪽에 있습니다. 발사하지 않습니다.", this);
            return false;
        }

        bool isShotgun = CurrentWeaponType == GunnerWeaponType.Shotgun;

        // 샷건만 데이터에 설정된 개수와 퍼짐을 사용
        // 권총과 라이플은 기존처럼 중앙 방향 Ray 하나만 검사하도록 설정
        int pelletCount = isShotgun && weaponData != null ? Mathf.Max(1, weaponData.PelletCount) : 1;
        float spreadAngle = isShotgun && weaponData != null ? Mathf.Clamp(weaponData.SpreadAngle, 0f, 45f) : 0f;

        // 원본 무기 데이터의 퍼짐은 변경하지 않습니다
        if (isShotgun && spreadOverride >= 0f)
            spreadAngle = Mathf.Clamp(spreadOverride, 0f, 45f);

        // 산탄은 각 방향에서 사거리 끝까지 검사
        // 중앙 조준점까지로 제한하면 옆으로 퍼진 산탄도 중앙에 있는 가까운 물체의 거리에서 잘릴 수 있습니다
        float shotDis = isShotgun ? CurrentRange : Mathf.Min(CurrentRange, aimDis + 0.05f);

        // 총구 섬광은 산탄 개수가 아니라 발사 횟수에 맞춰 한 번 생성합니다.
        // 실제 발사가 성립한 경우에만 새 무기별 섬광을 재생합니다.
        if (combatEffects != null)
            combatEffects.PlayMuzzle(CurrentWeaponType, origin, dir, coreDamageMultiplier > 1f, fixedDirection.HasValue);
        else
            PlayShotEffect(flashPrefab, origin, dir);

        shotgunHitEnemies.Clear();
        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 pelletDirection = GetPelletDirection(dir, spreadAngle);
            FirePellet(origin, pelletDirection, shotDis, coreDamageMultiplier);
        }

        // 모든 산탄 피해 처리가 끝난 뒤 적을 넉백
        // 검사 도중 적을 옮겨 나머지 산탄 판정이 달라지는 것을 방지
        foreach ( EnemyController enemy in shotgunHitEnemies)
        {
            if (enemy == null)
                continue;

            Vector3 pushDir = enemy.transform.position - origin;

            enemy.ApplyKnockback(pushDir,shotgunKnockBackDis,shotgunKnockBackDur);
        }
        shotgunHitEnemies.Clear();

        if (showDebug)
        {
            Debug.DrawLine(aimCamera.transform.position, aimPoint, Color.cyan, 0.5f);

            Debug.Log($"[{CurrentWeaponType}] 발사: 산탄 {pelletCount}개", this);
        }
        return true;
    }

    // 산탄 하나를 검사하기 직전에 호출
    // 중심 방향을 기준으로 최대 spreadAngle도 이내의 방향을 반환
    // 퍼짐이 0이면 중심 방향을 그대로 반환
    Vector3 GetPelletDirection(Vector3 forward, float spreadAngle)
    {
        if (spreadAngle <= 0f)
             return forward;

        // 원판 안의 임의 위치를 원뿔 단면으로 변환
        // 사각형 범위로 뽑을 때 모서리에서 최대 각도를 넘는 일을 방지
        Vector2 spread = Random.insideUnitCircle;
        float radius = Mathf.Tan(spreadAngle * Mathf.Deg2Rad);

        Vector3 localDirection = new Vector3(spread.x * radius, spread.y * radius, 1f).normalized;

        // 로컬 정면 기준으로 만든 방향을 실제 총구 조준 방향에 맞추도록 설정
        return Quaternion.LookRotation(forward) * localDirection;
    }


    // Fire에서 산탄마다 호출하여 첫 충돌 지점과 피해를 처리하는 메소드
    // origin은 총구 위치, direction은 산탄 방향, distance는 검사할 최대 거리를 계산
    void FirePellet(Vector3 origin, Vector3 direction, float distance, float coreDamageMultiplier)
    {
        // 첫 충돌만 사용하므로 벽 뒤의 적까지 관통하지 않습니다.
        bool hasHit = Physics.Raycast(origin, direction, out RaycastHit hit, 
                                        distance, hitMask, QueryTriggerInteraction.Ignore);

        Vector3 endPoint = hasHit ? hit.point : origin + direction * distance;
        // 피해는 기존 Ray에서 즉시 계산하고, 이동 탄도와 표면별 착탄은 시각 효과에만 전달합니다.
        if (combatEffects != null)
        {
            bool selfHit = hasHit && hit.collider.transform.IsChildOf(transform);
            combatEffects.PlayPath(CurrentWeaponType, origin, endPoint, hasHit && !selfHit,
                hasHit ? hit.normal : Vector3.zero,
                hasHit && hit.collider.GetComponentInParent<CoreController>() != null,
                hasHit && (hit.collider.GetComponentInParent<EnemyController>() != null || hit.collider.GetComponentInParent<BossQueenController>() != null),
                coreDamageMultiplier > 1f);
        }

        if (showDebug)
        {
            Debug.DrawLine(origin, endPoint, hasHit ? Color.red : Color.yellow, 0.5f);
        }

        if (!hasHit || hit.collider.transform.IsChildOf(transform))
            return;

        // 자식 콜라이더에 맞아도 부모의 피해 대상과 적 컨트롤러를 찾습니다.
        IDamageable target = hit.collider.GetComponentInParent<IDamageable>();

        EnemyController enemy = hit.collider.GetComponentInParent<EnemyController>();

        GameObject hitPrefab = enemy != null && enemyHitPrefab != null ? enemyHitPrefab : surfaceHitPrefab;

        Vector3 effectPosition = hit.point + hit.normal * hitEffectOffset;
        if (combatEffects == null) PlayShotEffect(hitPrefab, effectPosition, hit.normal);

        // CurrentDamage는 산탄 한 개의 피해량
        // 넉백은 다음 단계에서 명중한 적을 모아 한 번씩 적용
        if (target != null)
        {
            float hitDamage = CurrentDamage;

            CoreController core =
                hit.collider.GetComponentInParent<CoreController>();

            // 코어 주변의 적이 아니라 코어 자신의 체력에 맞았는지 확인
            // 권총 R(코어 브레이크) 발사일 때만 배율이 1보다 크게 전달되며 일반 사격은 1배
            if (CurrentWeaponType == GunnerWeaponType.Pistol &&
                core != null &&
                target is Health targetHealth &&
                targetHealth == core.GetComponent<Health>())
            {
                hitDamage *= coreDamageMultiplier;

                // 코어 브레이크는 코어 체력이나 추가 도전 배율과 관계없이 한 방에 파괴
                // 남은 체력 이상을 피해로 넣어 Health의 기존 사망 처리(보상, 연출)를 그대로 사용
                if (coreDamageMultiplier > 1f)
                    hitDamage = Mathf.Max(hitDamage, targetHealth.CurrentHealth);
            }

            target.TakeDamage(hitDamage);
        }

        // 피해는 산탄의 개별로 적용되지만 넉백 대상은 적당 하나만 적용되게 설정
        // 사망한 적은 넉백 x
        if (CurrentWeaponType == GunnerWeaponType.Shotgun && enemy != null && target != null)
        {
            shotgunHitEnemies.Add(enemy);
        }
    }

    // 발사 또는 명중됬을 시 이펙트 생성을 위한 메소드
    void PlayShotEffect(GameObject prefab, Vector3 pos, Vector3 dir)
    {
        if (prefab == null)
            return;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.forward;

        Quaternion rot = Quaternion.LookRotation(dir.normalized);
        GameObject effect = Instantiate(prefab, pos, rot);

        Destroy(effect, effectLifetime);
    }

    // 요청한 무기 종류와 일치하는 무기 데이터를 찾습니다
    GunnerWeaponData FindWeaponData(GunnerWeaponType weapon)
    {
        if (weaponDatas == null)
            return null;

        foreach ( GunnerWeaponData data in weaponDatas )
        {
            if(data != null && data.WeaponType == weapon)
                return data;
        }
        return null;
    }

    // 게임 시작 시 각 무기를 무기별 최대 탄창으로 초기화
    // 강화를 반영하고 무기 전환 시에 탄창이 초기화되지않게 막습니다
    void InitializeWeaponAmmo()
    {
        int magazineBonus = playerGrowth != null ? playerGrowth.MagazineSizeBonus : 0;

        for ( int i = 0; i  < ammoByWeapon.Length; i++)
        {
            GunnerWeaponData data = FindWeaponData((GunnerWeaponType)i);
            ammoByWeapon[i] = data != null ? Mathf.Max(1, data.MagazineSize + magazineBonus) : 0;

            nextFireTimeByWeapon[i] = 0f;
        }

        int currentIndex = (int)CurrentWeaponType;
        ammoByWeapon[currentIndex] = currentAmmo;
        nextFireTimeByWeapon[currentIndex] = nextFireTime;
    }

    // 보스전 시작 시 성장 상태를 복원받은 직후 호출되는 메소드
    // 무기의 탄약양을 강화된 상태로 최대치로 채움
    public void RefillAmmoAfterGrowthRestore()
    {
        isReloading = false;
        reloadPending = false;
        nextFireTime = 0f;

        // 강화상태로 복원된 최대 탄창 크기
        currentAmmo = MagazineSize;

        // 기존 초기화 메소드로 나머지 무기 탄약도 함께 갱신
        InitializeWeaponAmmo();
    }    

    // 애니메이션드라이버 스크립트에서 무기전환을 승인 한 이후 호출되는 메소드
    // 기존 무기의 탄약 및 발사의 대기시간을 저장하고 새 무기상태를 복원
    public bool TrySetWeapon(GunnerWeaponType weapon)
    {
        if (weaponSkill != null && weaponSkill.IsShotgunBursting)
            return false;

        int nextIndex = (int)weapon;

        if(nextIndex < 0 || nextIndex >= weaponDatas.Length)
            return false;
        if (!enabled || (health != null && health.IsDead) || (dodgeCtr != null && dodgeCtr.IsDodging) || isReloading)
            return false;

        GunnerWeaponData nextData = FindWeaponData(weapon);

        if(nextData == null)
        {
            Debug.LogError($"무기 연결을 확인하세요 {weapon}", this);
            return false;
        }

        // 같은 무기를 다시 선택할 시 탄야이나 타이머를 초기화 x
        if (weaponData == nextData)
            return true;

        int previousIndex = (int)CurrentWeaponType;
        ammoByWeapon[previousIndex] = currentAmmo;
        nextFireTimeByWeapon[previousIndex] = nextFireTime;

        weaponData = nextData;
        if (weaponSkill != null)
            weaponSkill.CancelTemporaryEffect();
        currentAmmo = Mathf.Clamp(ammoByWeapon[nextIndex], 0, MagazineSize);
        nextFireTime = nextFireTimeByWeapon[nextIndex];
        return true;
    }
}
