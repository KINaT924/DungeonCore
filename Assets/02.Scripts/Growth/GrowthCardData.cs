using UnityEngine;

public enum GrowthEffectType
{
    /// <summary>
    /// 카드가 변경할 능력치 구분 enum
    /// 표시 이름 대신 이 값을 사용하여 실제 효과를 적용
    /// </summary>
    
    AttackDamage,
    FireInterval,
    MagazineSize,
    ReloadDuration,
    MaxHealth,
    DodgeCooldown,
    ExperienceGain
}

public enum GrowthWeaponTarget
{
    /// <summary>
    /// 강화 효과 적용 범위 enum
    /// 공격력은 무기별로, 나머지 공통 효과는 Common으로 설정합니다
    /// </summary>

    Common,
    Pistol,
    Rifle,
    Shotgun
}

public enum GrowthValueType
{
    /// <summary>
    /// Flat은 고저 수치, PlercentOfBase는 기본 능력치 기준의 배율을 구분짓는 enum
    /// 비율 값 0.1 = 10%
    /// </summary>

    Flat,
    PercentOfBase
}

[CreateAssetMenu(fileName = "GrowthCard_", menuName = "DungeonCore/Growth Card")]
public class GrowthCardData : ScriptableObject
{
    /// <summary>
    /// 성장 카드 한 종류의 설정 원본
    /// 이름,설명,효과, 중첩 제한을 에셋으로 저장
    /// </summary>

    [Header("카드 고유 식별")]
    [SerializeField] string cardId;

    [Header("카드 표시")]
    [SerializeField] string cardName;
    [TextArea(2, 5)] [SerializeField] string description;
    [SerializeField] Sprite icon;

    [Header("카드 효과")]
    [SerializeField] GrowthEffectType effectType;
    [SerializeField] GrowthWeaponTarget weaponTarget;
    [SerializeField] GrowthValueType valueType;
    [SerializeField, Min(0f)] float effectValue;

    [Header("카드당 최대 선택 횟수")]
    [SerializeField, Min(1)] int maxSelections = 5;

    public string CardId => cardId;
    public string CardName => cardName;
    public string Description => description;
    public Sprite Icon => icon;
    public GrowthEffectType EffectType => effectType;
    public GrowthWeaponTarget WeaponTarget => weaponTarget;
    public GrowthValueType ValueType => valueType;
    public float EffectValue => effectValue;
    public int MaxSelections => maxSelections;



    // Inspector 에서 값을 편집할 때 호출
    private void OnValidate()
    {
        effectValue = Mathf.Max(0f, effectValue);
        maxSelections = Mathf.Max(1, maxSelections);
    }
}
