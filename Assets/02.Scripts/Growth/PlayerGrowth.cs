using System.Collections.Generic;
using UnityEngine;

public class PlayerGrowth : MonoBehaviour
{
    /// <summary>
    /// 플레이 중 선택한 카드 횟수와 누적 강화량을 관리하는 스크립트
    /// </summary>

    // 같은 ID를 기준으로 선택한 카드의 횟수를 저장 및 횟수 누적 저장
    readonly Dictionary<string, int> selectionCounts = new Dictionary<string, int>();

    // 이번 플레이에서 누적된 권총데미지 추가량
    float pistolDamageBonus;
    public float PistolDamageBonus => pistolDamageBonus;

    // 이번 플레이에서 누적된 라이플데미지 추가량
    float rifleDamageBonus;

    // 이번 플레이에서 누적된 샷건데미지 추가량
    float shotgunDamageBonus;

    // 모든 총기에 공통으로 적용될 기본 발사 간격 감소 비율
    float fireIntervalReduction;
    public float FireIntervalReduction => fireIntervalReduction;

    // 모든 총기에 공통으로 적용될 탄창 추가량
    int magazineSizeBonus;
    public int MagazineSizeBonus => magazineSizeBonus;

    // 모든 총기에 공통으로 적용할 기본 재장전 시간 감소 비율
    float reloadDurationReduction;
    public float ReloadDurationReduction => reloadDurationReduction;

    // 기본 회피 쿨타임에서 감소시킬 비율
    float dodgeCooldownReduction;
    public float DodgeCooldownReduction => dodgeCooldownReduction;

    // 이후 획득하는 경험치에 적용할 추가 비율
    float experienceGainBonus;
    public float ExperienceGainBonus => experienceGainBonus;


    [Header("테스트용")]
    [SerializeField] GrowthCardData testCard;

    [Header("레퍼런스")]
    [SerializeField] Health health;

    private void Awake()
    {
        if (health == null)
            health = GetComponent<Health>();
    }

    // 카드 선택 검사나 UI의 선택 횟수 표시에 사용, 선택된 적이 없다면 0을반환
    public int GetSelectionCount(string cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId))
            return 0;

        return selectionCounts.TryGetValue(cardId, out int count) ? count : 0;
    }

    // 카드 선택 진적에 검사하는 메소드
    public bool CanSelectCard(GrowthCardData card)
    {
        if (card == null || string.IsNullOrWhiteSpace(card.CardId))
            return false;

        // 무기 공격력은 세 무기 각각의 고정값 증가만 지원
        bool isWeaponDamage = card.EffectType == GrowthEffectType.AttackDamage && card.ValueType == GrowthValueType.Flat &&
            (card.WeaponTarget == GrowthWeaponTarget.Pistol ||
             card.WeaponTarget == GrowthWeaponTarget.Rifle ||
             card.WeaponTarget == GrowthWeaponTarget.Shotgun);

        // 발사 간격 감소는 모든 총기에 적용하며 기본값 기준 비율을 사용
        bool isFireInterval =
            card.EffectType == GrowthEffectType.FireInterval &&
            card.WeaponTarget == GrowthWeaponTarget.Common &&
            card.ValueType == GrowthValueType.PercentOfBase;

        // 탄창 증가는 공통 고정값 효과만 지원
        bool isMagazineSize =
            card.EffectType == GrowthEffectType.MagazineSize &&
            card.WeaponTarget == GrowthWeaponTarget.Common &&
            card.ValueType == GrowthValueType.Flat &&
            card.EffectValue == Mathf.Floor(card.EffectValue);

        // 재장전 시간 감소는 공통 효과이며 기본값 기준 비율만 지원합니다.
        bool isReloadDuration =
            card.EffectType == GrowthEffectType.ReloadDuration &&
            card.WeaponTarget == GrowthWeaponTarget.Common &&
            card.ValueType == GrowthValueType.PercentOfBase;

        // 최대 체력 증가는 공통 고정값 효과만 지원
        bool isMaxHealth =
            card.EffectType == GrowthEffectType.MaxHealth &&
            card.WeaponTarget == GrowthWeaponTarget.Common &&
            card.ValueType == GrowthValueType.Flat &&
            health != null &&
            !health.IsDead;

        // 회피 쿨타임 감소는 공통 효과이며 기본값 기준 비율을 사용
        bool isDodgeCooldown =
            card.EffectType == GrowthEffectType.DodgeCooldown &&
            card.WeaponTarget == GrowthWeaponTarget.Common &&
            card.ValueType == GrowthValueType.PercentOfBase;

        // 경험치 증가는 공통 효과이며 기본 획득량 기준 비율을 사용합니다.
        bool isExperienceGain =
            card.EffectType == GrowthEffectType.ExperienceGain &&
            card.WeaponTarget == GrowthWeaponTarget.Common &&
            card.ValueType == GrowthValueType.PercentOfBase;

        // 미구현 기능인 효과가 선택 횟수를 소비하지 않도록 차단
        if (!isWeaponDamage && !isFireInterval && !isMagazineSize && !isReloadDuration && !isMaxHealth && !isDodgeCooldown && !isExperienceGain)
            return false;

        if (card.EffectValue <= 0f)
            return false;

        return GetSelectionCount(card.CardId) < card.MaxSelections;
    }

    // 총기가 피해량을 계산할 때 호출되는 메소드
    public float GetWeaponDamageBonus(GunnerWeaponType weapon)
    {
        switch (weapon)
        {
            case GunnerWeaponType.Pistol:
                return pistolDamageBonus;
            case GunnerWeaponType.Rifle:
                return rifleDamageBonus;
            case GunnerWeaponType.Shotgun:
                return shotgunDamageBonus;
            default:
                return 0f;
        }
    }

    // 포탈 이동 전에 호출해서 현재 캐릭터의 성장 상태를 복사하고 보스전까지 이어가기 위한 메소드
    public PlayerGrowthSnapshot CaptureSnapshot()
    {
        // 성장된 강화 스탯을 저장
        PlayerGrowthSnapshot snapshot = new PlayerGrowthSnapshot
        {
            PistolDamageBonus = pistolDamageBonus,
            RifleDamageBonus = rifleDamageBonus,
            ShotgunDamageBonus = shotgunDamageBonus,
            FireIntervalReduction = fireIntervalReduction,
            MagazineSizeBonus = magazineSizeBonus,
            ReloadDurationReduction = reloadDurationReduction,
            DodgeCooldownReduction = dodgeCooldownReduction,
            ExperienceGainBonus = experienceGainBonus
        };

        // Dictionary를 복사하는게 아닌 항목을 복사
        // 이후 원본의 선택 횟수가 바뀐다하여도 복사된 기록이 바뀌진 않습니다
        foreach (KeyValuePair<string, int> entry in selectionCounts)
        {
            snapshot.SelectionCounts.Add(entry.Key, entry.Value);
        }

        return snapshot;
    }


    // 보스전 진입시 플레이어가 저장된 성장 상태를 가져오기 위한 메소드
    public bool RestoreSnapshot(PlayerGrowthSnapshot snapshot)
    {
        if (snapshot == null)
            return false;

        // 저장된 상태를 불러옴
        pistolDamageBonus = snapshot.PistolDamageBonus;
        rifleDamageBonus = snapshot.RifleDamageBonus;
        shotgunDamageBonus = snapshot.ShotgunDamageBonus;

        fireIntervalReduction = snapshot.FireIntervalReduction;
        magazineSizeBonus = snapshot.MagazineSizeBonus;
        reloadDurationReduction = snapshot.ReloadDurationReduction;
        dodgeCooldownReduction = snapshot.DodgeCooldownReduction;
        experienceGainBonus = snapshot.ExperienceGainBonus;

        // 기존 기록을 비워두고 저장된 상태로 교체
        selectionCounts.Clear();

        foreach (KeyValuePair<string, int> entry in snapshot.SelectionCounts)
        {
            selectionCounts.Add(entry.Key, entry.Value);
        }

        return true;
    }

    // 카드 선택이 완료된 시점에 호출되는 메소드
    // 카드의 효과와 선택 횟수를 함께 누적하며 기록
    public bool TryApplyCard(GrowthCardData card)
    {
        if (!CanSelectCard(card))
            return false;

        // 검사에서 허용한 효과만 각자의 강화 상태에 누적
        switch(card.EffectType)
        {
            case GrowthEffectType.AttackDamage:
                switch(card.WeaponTarget)
                {
                    case GrowthWeaponTarget.Pistol:
                        pistolDamageBonus += card.EffectValue;
                        break;
                    case GrowthWeaponTarget.Rifle:
                        rifleDamageBonus += card.EffectValue;
                        break;
                    case GrowthWeaponTarget.Shotgun:
                        shotgunDamageBonus += card.EffectValue;
                        break;
                    default:
                        return false;
                }
                break;
            case GrowthEffectType.FireInterval:
                fireIntervalReduction += card.EffectValue;
                break;
            case GrowthEffectType.MagazineSize:
                magazineSizeBonus += Mathf.RoundToInt(card.EffectValue);
                break;
            case GrowthEffectType.ReloadDuration:
                reloadDurationReduction += card.EffectValue;
                break;
            case GrowthEffectType.MaxHealth:
                if (health == null || !health.TryIncreaseMaxHealth(card.EffectValue))
                    return false;
                break;
            case GrowthEffectType.DodgeCooldown:
                dodgeCooldownReduction += card.EffectValue;
                break;
            case GrowthEffectType.ExperienceGain:
                experienceGainBonus += card.EffectValue;
                break;
            default:
                // 추후 지원 효과를 확장할 때 적용 코드 누락을 방지
                return false;
        }

        // 효과가 실제로 적용된 후에만 선택 횟수를 증가
        int nextCount = GetSelectionCount(card.CardId) + 1;
        selectionCounts[card.CardId] = nextCount;

        Debug.Log($"[성장] {card.CardName} : {nextCount}/{card.MaxSelections}회, " +
                    $"권총 추가 공격력: {pistolDamageBonus}, " +
                    $"발사 간격 감소: {fireIntervalReduction:P0}, " +
                    $"탄창 추가량: {magazineSizeBonus}, " +
                    $"재장전 시간 감소 : {reloadDurationReduction:P0}, " +
                    $"회피 쿨타임 감소: {dodgeCooldownReduction:P0}, " +
                    $"경험치 획득 증가: {experienceGainBonus:P0}, " +
                    $"라이플 추가 공격력: {rifleDamageBonus}, " +
                    $"샷건 산탄당 추가 공격력: {shotgunDamageBonus}", this);

        return true;
    }

    // 테스트용
    [ContextMenu("테스트/카드 1회적용")]
    void TestApplyCard()
    {
        if (!Application.isPlaying)
        {
            return;
        }
        if (!TryApplyCard(testCard))
        {
            Debug.LogWarning(
                "[성장] 적용 실패: 카드 연결, 지원 효과, 최대 선택 횟수를 확인하세요.",
                this);
        }

    }
}