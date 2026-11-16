using System;
using UnityEngine;

public enum GunnerWeaponType 
{ 
    Pistol = 0,
    Rifle = 1, 
    Shotgun = 2 
}

/// <summary>
/// 무기별 자세,장착 모션과 시각 모델을 저장하는 스크립트
/// </summary>
[Serializable]
public sealed class GunnerWeaponAnimationEntry
{
    public GunnerWeaponType weapon;
    public GameObject visualPrefab;
    public AnimationClip hold;
    public AnimationClip aim;
    public AnimationClip fire;
    public AnimationClip reload;
    public AnimationClip draw;
    public AnimationClip hit;
}

// 거너 전용 Avatar와 컨트롤러, 권총·라이플·샷건의 애니메이션 연결을 한곳에서 관리
[CreateAssetMenu(menuName = "DungeonCore/Gunner Animation Profile")]
public sealed class GunnerWeaponAnimationProfile : ScriptableObject
{
    public RuntimeAnimatorController controller;
    public Avatar avatar;
    public GunnerWeaponAnimationEntry[] weapons = new GunnerWeaponAnimationEntry[3];

    // 배열 순서에 의존하지 않고 무기 종류에 맞는 연결을 직접 찾습니다
    public GunnerWeaponAnimationEntry Find(GunnerWeaponType weapon)
    {
        if (weapons == null)
            return null;
        foreach (var entry in weapons)
            if (entry != null && entry.weapon == weapon)
                return entry;

        return null;
    }
}
