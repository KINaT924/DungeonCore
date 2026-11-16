using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 현재 무기의 실제 스킬 상태를 원형 아이콘,어두운 덮개, 남은 초로 표시
/// UI에서 시간을 계산하거나 전투 데이터를 변경하지 않으며 배치는 씬에 저장
/// </summary>
[DefaultExecutionOrder(320)]
public sealed class WeaponSkillCooldownUI : MonoBehaviour
{
    [Header("실제 플레이어 상태")]
    public PlayerGunController gun;
    public GunnerWeaponSkillController skills;
    public Health health;

    [Header("Hierarchy UI 참조")]
    public Image abilityIcon;
    public Image dimOverlay;
    public Image border;
    public TMP_Text countdownText;
    public TMP_Text skillNameText;
    public TMP_Text activeText;
    public TMP_Text keyText;

    [Header("무기별 스킬 이미지")]
    public Sprite pistolIcon;
    public Sprite rifleIcon;
    public Sprite shotgunIcon;

    [Header("표시 색상")]
    public Color readyColor = new Color(.4f, .85f, .9f, 1f);
    public Color cooldownColor = new Color(.57f, .52f, .73f, 1f);
    [Range(0f, 1f)] public float cooldownDarkness = .66f;

    // 숫자, 무기가 바뀔 때만 TMP 문자열과 스프라이트를 갱신
    int previousWeapon = -1;
    int previousSeconds = -2;
    int previousActive = -1;
    bool? previousUnavailable;

    // HUD 재활성화 시 현재 무기와 실제 쿨타임을 즉시 재동기화
    void OnEnable()
    {
        previousWeapon = previousActive = -1;
        previousSeconds = -2;
        previousUnavailable = null;
        RefreshNow();
    }

    // 총기, 스킬 Update가 끝난 뒤 선택 무기와 남은 시간을 읽습니다
    void LateUpdate()
    { 
        RefreshNow();
    }

    // 현재 무기에 대응하는 실제 쿨타임을 반환
    // 독립 타이머는 없습니다
    public float ReadCooldown()
    {
        if (skills == null || gun == null) 
            return 0f;

        switch (gun.CurrentWeaponType)
        {
            case GunnerWeaponType.Pistol:
                return skills.PistolCooldownRemaining;
            case GunnerWeaponType.Rifle:
                return skills.RifleCooldownRemaining;
            case GunnerWeaponType.Shotgun:
                return skills.ShotgunCooldownRemaining;
            default:
                return 0f;
        }
    }

    // 무기 전환 시 이미지와 이름을 바꾸고, 쿨타임 중 이미지를 어둡게 덮습니다
    // 남은 초는 올림하여 시간이 남았는데 0으로 표시하지 않습니다
    public void RefreshNow()
    {
        int weapon = gun != null ? (int)gun.CurrentWeaponType : 0;

        if (weapon != previousWeapon)
        {
            if (abilityIcon != null)
                abilityIcon.sprite = weapon == 1 ? rifleIcon : weapon == 2 ? shotgunIcon : pistolIcon;
            if (skillNameText != null)
                skillNameText.text = weapon == 1 ? "OVERDRIVE" : weapon == 2 ? "FOCUS BURST" : "CORE BREAK";
            previousWeapon = weapon;
        }

        bool unavailable = gun == null || skills == null || !skills.isActiveAndEnabled ||
                           (health != null && health.IsDead);
        float remaining = ReadCooldown();
        int seconds = Mathf.CeilToInt(remaining);
        bool cooling = remaining > 0f;

        if (dimOverlay != null)
        {
            dimOverlay.enabled = unavailable || cooling;
            dimOverlay.color = new Color(.005f, .01f, .025f, unavailable ? .82f : cooldownDarkness);
        }
        if (border != null) 
            border.color = unavailable ? Color.gray : cooling ? cooldownColor : readyColor;
        if (keyText != null)
            keyText.color = unavailable || cooling ? new Color(.65f, .69f, .76f) : readyColor;
        if (countdownText != null && (seconds != previousSeconds || previousUnavailable != unavailable))
        {
            countdownText.text = unavailable ? "—" : cooling ? seconds.ToString() : "";
            previousSeconds = seconds;
        }
        previousUnavailable = unavailable;

        int active = !unavailable && weapon == 1 && skills.IsRifleInfiniteAmmoActive ? 1 :
                     !unavailable && weapon == 2 && skills.IsShotgunBursting ? 2 : 0;
        if (activeText != null && active != previousActive)
        {
            activeText.text = active == 1 ? "ACTIVE" : active == 2 ? "BURST" : "";
            previousActive = active;
        }
    }
}
