using TMPro;
using UnityEngine;

/// <summary>
/// 공용 HUD 내부 참조와 씬별 표시 모드를 연결하는 스크립트
/// </summary>
/// 
public class GameHudView : MonoBehaviour
{
    [SerializeField] InGameHudBinding hud;
    [SerializeField] PlayerHealthUI playerHealth;
    [SerializeField] DodgeCooldownUI dodge;
    [SerializeField] WeaponSkillCooldownUI skills;
    [SerializeField] RoomMinimap minimap;
    [SerializeField] GameObject survivalStatus;
    [SerializeField] CanvasGroup portalNotice;
    [SerializeField] BossHealthUI bossBar;
    [SerializeField] StageClearPresentation stageClear;
    [SerializeField] ThreatWarningUI threatWarning;
    public CanvasGroup PortalNotice => portalNotice;

    /// <summary>생성 직후 비활성 상태에서 현재 플레이어와 씬 고유 데이터를 UI에 연결합니다.</summary>
    public void Bind(GameUISpawner context)
    {
        var player = context.player;
        var health = player.GetComponent<Health>();

        hud.level = player.GetComponent<PlayerLevel>();
        hud.gun = player.GetComponent<PlayerGunController>();
        hud.weapons = player.GetComponent<GunnerWeaponAnimationDriver>();
        hud.health = health;
        hud.core = context.core;
        hud.run = context.run;

        playerHealth.BindHealth(health);
        dodge.health = health;
        dodge.dodge = player.GetComponent<PlayerDodgeController>();

        skills.health = health;
        skills.gun = hud.gun;
        skills.skills = player.GetComponent<GunnerWeaponSkillController>();

        minimap.player = player.transform;
        minimap.core = context.core;
        minimap.SetLayout(context.minimapLayout);

        foreach (var canvas in GetComponentsInChildren<Canvas>(true))
            canvas.worldCamera = context.uiCamera;

        bool boss = context.mode == GameUISpawner.BattleMode.Boss;
        survivalStatus.SetActive(!boss);
        portalNotice.gameObject.SetActive(!boss);
        bossBar.gameObject.SetActive(boss);
        bossBar.Bind(context.bossHealth, context.bossName);

        if (stageClear != null)
            stageClear.Bind(context);
        if (threatWarning != null)
            threatWarning.Bind(context);
    }
}
