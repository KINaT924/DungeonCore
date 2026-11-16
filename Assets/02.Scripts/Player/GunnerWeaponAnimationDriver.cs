using UnityEngine;

/// <summary>
/// 기존 이동·권총 판정을 유지하면서 세 무기의 자세, 번호 키 전환, 피격 반응을 연결합니다.
/// PlayerAnimationController가 공통 파라미터를 갱신한 뒤 추가 값을 전달합니다.
/// </summary>
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public sealed class GunnerWeaponAnimationDriver : MonoBehaviour
{
    [SerializeField] GunnerWeaponAnimationProfile profile;
    [SerializeField] Animator animator;
    [SerializeField] PlayerInputReader inputReader;
    [SerializeField] PlayerGunController gun;
    [SerializeField] PlayerDodgeController dodge;
    [SerializeField] Health health;
    [SerializeField] GunnerAimController aimCtr;
    [SerializeField] Camera aimCamera;
    [SerializeField] Transform weaponRoot;
    [SerializeField] Transform sharedMuzzle;
    [SerializeField] GunnerWeaponType initialWeapon = GunnerWeaponType.Pistol;

    [SerializeField, Min(.01f)] float dodgeDuration = .4f;
    [SerializeField, Range(0f, 1f)] float aimPitchWeight = .65f;

    readonly GameObject[] visuals = new GameObject[3];
    GunnerWeaponType currentWeapon;
    float  gunBlockedUntil, dodgeStartedAt;
    bool wasDodging, pausedGun;
    Transform chest;
    Transform leftGrip;
    // 카메라와 판정용 루트는 유지하고, 비조준 달리기에서 외형만 이동 방향으로 돌립니다.
    Quaternion visualBaseRotation;
    float freeRunYaw;
    [SerializeField, Min(1f)] float visualTurnSpeed = 900f;
    public GunnerWeaponType CurrentWeapon => currentWeapon;

    /// <summary>참조와 전용 Avatar·컨트롤러를 연결하고 대기 자세 및 무기 모델을 초기화합니다.</summary>
    void Awake()
    {
        if (inputReader == null) inputReader = GetComponent<PlayerInputReader>();
        if (gun == null) gun = GetComponent<PlayerGunController>();
        if (dodge == null) dodge = GetComponent<PlayerDodgeController>();
        if (health == null) health = GetComponent<Health>();
        if (aimCamera == null) aimCamera = Camera.main;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (aimCtr == null) aimCtr = GetComponent<GunnerAimController>();
        if (profile == null || animator == null || profile.controller == null || profile.avatar == null)
        {
            Debug.LogError("거너 애니메이션 Profile과 Animator 연결을 확인하세요.", this); enabled = false; return;
        }

        animator.avatar = profile.avatar; animator.runtimeAnimatorController = profile.controller;
        animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.Rebind();

        var relay = animator.GetComponent<GunnerWeaponIKRelay>() ?? animator.gameObject.AddComponent<GunnerWeaponIKRelay>();
        relay.Bind(this);
        visualBaseRotation = animator.transform.localRotation;
        chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest);

        if (weaponRoot == null)
        {
            weaponRoot = new GameObject("GunnerWeapons").transform;
            weaponRoot.SetParent(animator.GetBoneTransform(HumanBodyBones.RightHand), false);
        }

        foreach (var entry in profile.weapons)
        {
            if (entry == null || entry.visualPrefab == null) continue;
            var existing = weaponRoot.Find(entry.visualPrefab.name);
            var visual = existing != null ? existing.gameObject : Instantiate(entry.visualPrefab, weaponRoot, false);
            visual.name = entry.visualPrefab.name;
            visuals[(int)entry.weapon] = visual; visual.SetActive(false);
        }

        // 무기 자식 계층이 완성된 뒤 재바인딩하여 첫 프레임에 이전 골격 캐시가 남지 않게 합니다.
        animator.Rebind();

        if (gun == null || !gun.TrySetWeapon(initialWeapon))
        {
            Debug.LogError("시작 무기의 전투 데이터 연결을 확인하세요", this);
            enabled = false;
            return;
        }

        currentWeapon = initialWeapon; ShowWeapon(currentWeapon);
        animator.SetInteger("WeaponType", (int)currentWeapon);
        animator.SetFloat("WeaponBlend", (int)currentWeapon);
        animator.SetFloat("AimWeight", 0); animator.SetBool("IsAiming", false);
    }

    /// <summary>거너에서만 기존 1·2·3번 입력 이벤트를 무기 선택에 연결하고, 생존 피격 이벤트를 구독합니다.</summary>
    void OnEnable()
    {
        if (inputReader != null)
        {
            inputReader.Skill1Pressed += EquipPistol; inputReader.Skill2Pressed += EquipRifle; inputReader.Skill3Pressed += EquipShotgun;
        }
        if (health != null) health.OnDamaged += OnDamaged;
    }

    /// <summary>비활성화 시 이벤트를 해제하고 이 컴포넌트가 일시 중지했던 발사 컴포넌트만 복구합니다.</summary>
    void OnDisable()
    {
        if (inputReader != null)
        {
            inputReader.Skill1Pressed -= EquipPistol; inputReader.Skill2Pressed -= EquipRifle; inputReader.Skill3Pressed -= EquipShotgun;
        }
        if (health != null) health.OnDamaged -= OnDamaged;
        RestoreGun();
    }

    /// <summary>
    /// 우클릭 조준 상태에 맞춰 비조준 전신 달리기와 조준 8방향 걷기를 전환합니다.
    /// 비조준 Pose의 상체 덮어쓰기를 해제해 원본 달리기의 팔·몸통 움직임을 보존합니다.
    /// 회피 시작 방향을 저장하여 도중에 키를 놓거나 바꿔도 회피 모션이 다른 방향으로 섞이지 않게 합니다.
    /// </summary>
    void Update()
    {
        if (animator == null || profile == null) return;
        if (health != null && health.IsDead)
        {
            animator.SetBool("IsDead", true); animator.ResetTrigger("Shoot"); RestoreGun(); return;
        }
      
        bool dodging = dodge != null && dodge.IsDodging;
        if (dodging && !wasDodging)
        {
            var direction = inputReader != null ? inputReader.MoveInput : Vector2.zero;
            direction = direction.sqrMagnitude > .01f ? direction.normalized : Vector2.up;
            animator.SetFloat("DodgeX", direction.x); animator.SetFloat("DodgeY", direction.y);
            dodgeStartedAt = Time.time;
        }
        animator.SetFloat("DodgeProgress", dodging ? Mathf.Clamp01((Time.time - dodgeStartedAt) / dodgeDuration) : 0);
        wasDodging = dodging;
        bool blocked = animator.GetBool("FullBodyAction") || dodging || Time.time < gunBlockedUntil || (gun != null && gun.IsReloadAnimActive);
        bool aim = !blocked && aimCtr != null && aimCtr.IsAiming;
        animator.SetBool("IsAiming", aim);
        animator.SetFloat("AimWeight", Mathf.MoveTowards(
            animator.GetFloat("AimWeight"), aim ? 1f : 0f, Time.deltaTime / .12f));
        UpdateUpperBodyWeight();
        UpdateVisualFacing(aim, dodging);
        if (Time.time >= gunBlockedUntil) RestoreGun();
    }

    /// <summary>
    /// 일반 보유/이동에서는 기본 레이어의 전신 모션을 사용합니다. 조준과 발사·재장전·장착에는
    /// 상체 레이어를 켜되, 피격·회피·사망은 항상 전신 모션이 우선합니다.
    /// 재장전/장착은 조준을 차단하지만 해당 상체 연출은 유지합니다.
    /// </summary>
    void UpdateUpperBodyWeight()
    {
        if (animator.layerCount < 2) return;
        if (animator.GetBool("FullBodyAction") || (dodge != null && dodge.IsDodging))
        {
            animator.SetLayerWeight(1, 0f);
            return;
        }
        var state = animator.GetCurrentAnimatorStateInfo(1);
        var next = animator.GetNextAnimatorStateInfo(1);
        bool action = state.IsTag("Fire") || state.IsTag("Reload") || state.IsTag("Equip");
        if (animator.IsInTransition(1))
            action |= next.IsTag("Fire") || next.IsTag("Reload") || next.IsTag("Equip");
        animator.SetLayerWeight(1, action ? 1f : animator.GetFloat("AimWeight"));
    }

    /// <summary>
    /// 비조준 이동은 전방 달리기 원본을 사용하고 자식 외형을 입력 방향으로 회전합니다.
    /// 조준 시 외형은 카메라 기준 정면으로 돌아와 8방향 걷기와 총구 방향을 일치시킵니다.
    /// dodging이 참이면 기존 4방향 회피 좌표가 바뀌지 않도록 외형 회전을 즉시 복구합니다.
    /// </summary>
    void UpdateVisualFacing(bool aiming, bool dodging)
    {
        if (animator.transform == transform || Time.deltaTime <= 0f) return;
        if (dodging)
        {
            animator.transform.localRotation = visualBaseRotation;
            freeRunYaw = 0f;
            return;
        }
        if (animator.GetBool("FullBodyAction")) return;
        Vector2 movement = inputReader != null ? inputReader.MoveInput : Vector2.zero;
        if (aiming) freeRunYaw = 0f;
        else if (movement.sqrMagnitude > 0.01f)
            freeRunYaw = Mathf.Atan2(movement.x, movement.y) * Mathf.Rad2Deg;
        Quaternion target = Quaternion.AngleAxis(freeRunYaw, Vector3.up) * visualBaseRotation;
        animator.transform.localRotation = Quaternion.RotateTowards(
            animator.transform.localRotation, target, visualTurnSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 조준 중에만 흉곽을 카메라 상하 방향으로 완만하게 돌립니다. 월드 좌우 축으로만 회전하므로
    /// 발사 클립이 머리에 롤 회전을 더하지 않습니다. 전신 피격·회피·사망에서는 적용하지 않습니다.
    /// </summary>
    void LateUpdate()
    {
        if (Time.deltaTime <= 0 || chest == null || aimCamera == null || animator == null || (health != null && health.IsDead) || animator.GetBool("FullBodyAction")) return;
        float weight = animator.GetFloat("AimWeight") * aimPitchWeight;
        if (weight < .001f) return;
        float pitch = -Mathf.Asin(Mathf.Clamp(aimCamera.transform.forward.y, -1, 1)) * Mathf.Rad2Deg;
        chest.rotation = Quaternion.AngleAxis(Mathf.Clamp(pitch, -40, 45) * weight, animator.transform.right) * chest.rotation;
    }

    /// <summary>1번 키는 권총을 선택합니다.</summary>
    void EquipPistol() => TryEquip(GunnerWeaponType.Pistol);
    /// <summary>2번 키는 라이플을 선택합니다.</summary>
    void EquipRifle() => TryEquip(GunnerWeaponType.Rifle);
    /// <summary>3번 키는 샷건을 선택합니다.</summary>
    void EquipShotgun() => TryEquip(GunnerWeaponType.Shotgun);

    /// <summary>
    /// 생존, 일반 조작 상태에서만 무기를 전환합니다. 재장전·회피·피격·장착 중에는 전환을 받지 않습니다.
    /// 장착 중 입력된 발사가 남지 않도록 Trigger를 지우고 모델과 총구를 선택한 무기에 맞춥니다.
    /// </summary>
    public bool TryEquip(GunnerWeaponType weapon)
    {
        if (profile == null || animator == null || weapon == currentWeapon || (int)weapon < 0 || (int)weapon > 2 || profile.Find(weapon) == null) return false;
        if ((health != null && health.IsDead) || (dodge != null && dodge.IsDodging) || (gun != null && gun.IsReloading)) return false;
        if (Time.timeScale <= 0 || Time.time < gunBlockedUntil || animator.GetBool("FullBodyAction")) return false;
        if (gun == null || !gun.TrySetWeapon(weapon)) return false;
                

        currentWeapon = weapon; ShowWeapon(weapon);
        animator.ResetTrigger("Shoot"); animator.SetInteger("WeaponType", (int)weapon); animator.SetFloat("WeaponBlend", (int)weapon); animator.SetTrigger("Equip");
        var entry = profile.Find(weapon); PauseGun(entry.draw != null ? entry.draw.length : .4f);
        return true;
    }

    /// <summary>현재 선택한 모델 하나만 표시하고, 기존 공격 컴포넌트가 참조하는 공통 총구를 같은 위치로 옮깁니다.</summary>
    void ShowWeapon(GunnerWeaponType weapon)
    {
        for (int i = 0; i < visuals.Length; i++) if (visuals[i] != null) visuals[i].SetActive(i == (int)weapon);
        var selected = visuals[(int)weapon];
        leftGrip = selected != null ? selected.transform.Find("LeftGrip") : null;
        if (selected == null || sharedMuzzle == null) return;
        var muzzle = selected.transform.Find("Muzzle");
        if (muzzle != null)
        {
            sharedMuzzle.localPosition = weaponRoot.InverseTransformPoint(muzzle.position);
            sharedMuzzle.localRotation = Quaternion.Inverse(weaponRoot.rotation) * muzzle.rotation;
        }
    }

    /// <summary>
    /// 조준·발사 중 왼손을 무기의 보조 손잡이에 맞춥니다. 재장전·장착에서는 손을 놓아 원본 모션을
    /// 그대로 재생하고, 전신 피격·회피·사망에도 손 고정을 해제합니다. 상체 레이어의 IK Pass가 필요합니다.
    /// </summary>
    public void ApplyLeftHandIK(int layerIndex)
    {
        if (layerIndex != 1 || animator == null) return;
        var state = animator.GetCurrentAnimatorStateInfo(1);
        bool allowed = leftGrip != null && !animator.GetBool("FullBodyAction") && (state.IsTag("Pose") || state.IsTag("Fire"));
        float weight = allowed ? animator.GetFloat("AimWeight") : 0;
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, weight);
        if (weight > .001f)
        {
            animator.SetIKPosition(AvatarIKGoal.LeftHand, leftGrip.position);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, leftGrip.rotation);
        }
    }

    /// <summary>사망하지 않은 유효 피해만 Hit에 연결하고, 피격 모션 동안 발사를 잠시 중단합니다.</summary>
    void OnDamaged(float damage)
    {
        if (damage <= 0 || animator == null || (health != null && health.IsDead) || (dodge != null && dodge.IsDodging)) return;
        animator.ResetTrigger("Shoot"); animator.SetTrigger("Hit");
        var entry = profile.Find(currentWeapon); PauseGun(entry != null && entry.hit != null ? entry.hit.length : .4f);
    }

    /// <summary>장착·피격 중에만 발사 입력 구독을 잠시 끕니다. 탄약과 재장전 수치는 변경하지 않습니다.</summary>
    void PauseGun(float seconds)
    {
        gunBlockedUntil = Mathf.Max(gunBlockedUntil, Time.time + Mathf.Max(.01f, seconds));
        if (gun != null && gun.enabled) { gun.enabled = false; pausedGun = true; }
    }

    /// <summary>이 컴포넌트가 일시 중지한 경우에만 원래 발사 입력을 복구합니다.</summary>
    void RestoreGun()
    {
        if (pausedGun && gun != null) gun.enabled = true;
        pausedGun = false;
    }
}
