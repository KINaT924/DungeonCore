using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 플레이어의 성장 상태를 저장하는 스크립트
/// </summary>
public sealed class PlayerGrowthSnapshot
{
    // 카드별 선택 횟수를 보스전에서도 이어받을 수 있게 보관하기 위함 
    public readonly Dictionary<string, int> SelectionCounts = new Dictionary<string, int>();

    // 무기별 추가 공격력 카드 저장
    public float PistolDamageBonus;
    public float RifleDamageBonus;
    public float ShotgunDamageBonus;

    // 공통 강화 카드 스탯 저장
    public float FireIntervalReduction;
    public int MagazineSizeBonus;
    public float ReloadDurationReduction;
    public float DodgeCooldownReduction;
    public float ExperienceGainBonus;

    // 누적 경험치와 아직 정수 경험치가 되지 않은 소수점 잔여량
    // 레벨은 누적 경험치로 다시 계산하므로 별도로 저장 x
    public int TotalExperience;
    public double ExperienceFraction;

    // 현재 체력과 카드로 증가한 최대 체력 추가량
    // 기본 최대 체력은 기존 플레이어 프리팹의 설정을 사용
    public float CurrentHealth;
    public float MaxHealthBonus;
}
