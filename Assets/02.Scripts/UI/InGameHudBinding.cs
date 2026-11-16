using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 기존 플레이어,성장,무기,코어의 실제 상태를 HUD에 표시
/// 체력과 미니맵은 기존 전용 컴포넌트가 담당하며 이 컴포넌트는 게임 데이터를 변경하지 않습니다
/// </summary>

[DefaultExecutionOrder(300)]
public class InGameHudBinding : MonoBehaviour
{
    [Header("상태를 읽을 기존 컴포넌트")]
    public PlayerLevel level;
    public PlayerGunController gun;
    public GunnerWeaponAnimationDriver weapons;
    public Health health;
    public CoreController core;
    public DungeonRunController run;

    [Header("Hierarchy의 실제 UI 참조")]
    public TMP_Text levelText;
    public TMP_Text experienceText;
    public Image experienceFill;
    public TMP_Text timerText;
    public TMP_Text coreStatusText;
    public Image coreStatusBackground;
    public TMP_Text ammoText;
    public TMP_Text weaponNameText;
    public TMP_Text reloadText;
    public Image reloadFill;
    public GameObject reloadPanel;
    public WeaponSlot[] slots;

    [Header("후속 기능용 자리 — 현재 연결된 기능이 없으면 숨김")]
    public GameObject weaponSkillPlaceholder;
    public GameObject escapePlaceholder;

    [Header("선택/대기 색상")]
    public Color selectedColor = new Color(.42f,.91f,1f);
    public Color idleColor = new Color(.66f,.68f,.8f,.95f);

    // 초 단위 캐시로 시간이 바뀔 때만 문자를 갱신
    // 카드 선택의 timeScale = 0 동안 시간은 흐르지 않음
    int lastSeconds = -1;
    int lastAmmo = -1, lastMagazine = -1, lastWeapon = -1, lastReloadPercent = -1;
    bool? lastCoreActive;
    bool? lastReloading;

    public float ElapsedSeconds => run != null ? run.ElapsedSeconds : 0f;

    // 무기 슬롯의 기존 테두리,아이콘,선택 장식을 묶어놓음
    // 슬롯 번호는 기존 무기 enum과 일치
    [Serializable]
    public class WeaponSlot
    {
        public Image[] borders;
        public Image icon;
        public GameObject selectedGlow;
    }

    // HUD 활성화 시 성장 이벤트를 구독
    // 초기 값은 Start에서도 읽어 Awake 실행 순서 차이를 보완
    void OnEnable()
    {
        if (level != null)
            level.OnLevelProgressChaned += UpdateLevel;

        InvalidateCache();
        RefreshNow();
    }

    // 모든 플레이어 컴포넌트 초기화 후 실제 수치를 표시하고 미구현 기능의 시안 안내를 숨깁니다
    void Start()
    {
        if (weaponSkillPlaceholder != null)
            weaponSkillPlaceholder.SetActive(false);
        if (escapePlaceholder != null) 
            escapePlaceholder.SetActive(false);

        RefreshNow();
    }

    // 비활성화 시 성장 이벤트를 해제하여 HUD를 다시 켜도 중복 갱신되지 않게 설정
    void OnDisable()
    {
        if (level != null) 
            level.OnLevelProgressChaned -= UpdateLevel;
    }

    // 기존 무기 애니메이션 갱신 뒤 상태를 읽습니다
    // 실제 플레이 시간은 일시정지와 사망 시 정지
    void LateUpdate()
    {
        UpdateTimer();
        UpdateWeapon();
        UpdateAmmo();
        UpdateCore();
    }

    // 에디터 연결 직후와 재활성화/검증 시 실제 데이터를 한 번 동기화
    // 게임 수치는 변경하지 않습니다
    public void RefreshNow()
    {
        if (level != null)
            UpdateLevel(level.CurrentLevel, level.CurrentLevelExp, level.RequiredExp);

        UpdateTimer();
        UpdateWeapon();
        UpdateAmmo();
        UpdateCore();
    }

    // HUD를 재활성화하면 기존 표시 캐시를 비워 변경 전 수치가 남지 않게 합니다
    void InvalidateCache()
    {
        lastSeconds = lastAmmo = lastMagazine = lastWeapon = lastReloadPercent = -1;
        lastCoreActive = lastReloading = null;
    }

    // 레벨 진행 이벤트의 현재 레벨/레벨 내 경험치/다음 요구량을 표시
    // 요구량 0은 설정상 최고 레벨
    void UpdateLevel(int currentLevel, int experience, int required)
    {
        if (levelText != null) 
            levelText.SetText("Lv. {0}",currentLevel);

        bool atMax = level != null && level.IsAtConfiguredMaxLevel;
        if (experienceText != null)
        {
            if (atMax) experienceText.text = "EXP MAX";
            else experienceText.SetText("EXP {0} / {1}",experience,required);
        }
        if (experienceFill != null)
            experienceFill.fillAmount = atMax ? 1f : (required > 0 ? Mathf.Clamp01((float)experience/required) : 0f);
    }

    // 누적된 배율 시간을 분:초로 표시
    // 1시간이 지나도 총 분을 유지
    void UpdateTimer()
    {
        if (run == null)
        {
            if (timerText != null)
                timerText.text = "--:--";
            return;
        }

        int seconds = Mathf.CeilToInt(run.RemainingSeconds);

        if (seconds == lastSeconds)
            return;

        lastSeconds = seconds;

        if (timerText != null)
            timerText.text = $"{seconds / 60:00}:{seconds % 60:00}";
    }

    // 기존 무기 전환이 실제로 성공한 결과를 읽어 이름과 슬롯을 갱신
    // 키를 눌렀다는 이유만으로 바꾸지 않습니다
    void UpdateWeapon()
    {
        int selected = weapons != null ? (int)weapons.CurrentWeapon : 0;
        if (selected == lastWeapon)
            return;
        lastWeapon = selected;

        if (weaponNameText != null)
            weaponNameText.text = selected == 1 ? "RIFLE" : selected == 2 ? "SHOTGUN" : "PISTOL";
        if (slots == null) 
            return;
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot == null) 
                continue;

            bool active = i == selected;
            if (slot.borders != null)
                foreach (Image border in slot.borders)
                if (border != null)
                        border.color = active ? selectedColor : idleColor;

            if (slot.icon != null)
                slot.icon.color = active ? Color.white : new Color(.7f,.72f,.77f,1);

            if (slot.selectedGlow != null)
                slot.selectedGlow.SetActive(active);
        }
    }

    // 현재 총기 컴포넌트의 실제 남은 탄약/강화된 탄창을 읽고 재장전 진행 막대와 백분율을 표시
    void UpdateAmmo()
    {
        if (gun == null)
            return;

        if (lastAmmo != gun.CurrentAmmo || lastMagazine != gun.MagazineSize)
        {
            lastAmmo = gun.CurrentAmmo; lastMagazine = gun.MagazineSize;
            if (ammoText != null) ammoText.SetText("{0} / {1}",lastAmmo,lastMagazine);
        }

        bool reloading = gun.IsReloading;
        if (lastReloading != reloading)
        {
            lastReloading = reloading;
            if (reloadPanel != null)
                reloadPanel.SetActive(reloading);
            if (ammoText != null)
                ammoText.color = reloading ? selectedColor : Color.white;
        }

        if (!reloading) 
        { 
            lastReloadPercent = -1;
            return; 
        }

        float progress = gun.ReloadProgress;
        if (reloadFill != null)
            reloadFill.fillAmount = progress;

        int percent = Mathf.FloorToInt(progress*100f);
        if (percent == lastReloadPercent)
            return;
        lastReloadPercent = percent;

        if (reloadText != null)
            reloadText.SetText("RELOADING {0}%",percent);
    }

    // 코어의 실제 활성/파괴 상태를 읽어 상단 문구와 배경을 변경
    // 코어 생성 규칙은 수정하지 않습니다
    void UpdateCore()
    {
        bool active = core != null && core.gameObject.activeInHierarchy && !core.IsDestroyed;

        if (lastCoreActive == active)
            return;
        lastCoreActive = active;

        if (coreStatusText != null)
            coreStatusText.text = active ? "CORE ACTIVE" : "CORE INACTIVE";
        if (coreStatusBackground != null)
            coreStatusBackground.color = active ? new Color(.36f,.12f,.55f,.95f) : new Color(.09f,.1f,.14f,.9f);
    }
}
