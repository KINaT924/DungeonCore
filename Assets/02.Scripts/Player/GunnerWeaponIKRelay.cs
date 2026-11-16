using UnityEngine;

/// <summary>
/// 자식 Animator에 전달되는 IK 콜백을 부모의 거너 애니메이션 드라이버로 전달
/// </summary>
public sealed class GunnerWeaponIKRelay : MonoBehaviour
{
    GunnerWeaponAnimationDriver driver;

    // 드라이버가 준비한 무기 손잡이 정보를 사용할 수 있도록 참조를 연결
    public void Bind(GunnerWeaponAnimationDriver target) => driver = target;

    // Animator 상체 레이어의 IK Pass에서만 현재 무기 왼손 고정을 적용함
    void OnAnimatorIK(int layerIndex)
    {
        if (driver != null && driver.isActiveAndEnabled)
            driver.ApplyLeftHandIK(layerIndex);
    }
}
