using UnityEngine;

/// <summary>
/// 피격, 회피, 사망 때 무기 상체 레이어를 끄고, 이동 복귀 때 무기 자세를 다시 표시
/// </summary>

public sealed class GunnerFullBodyState : StateMachineBehaviour
{
    [SerializeField] bool fullBody;

    /// <summary>
    /// 전신 상태 진입 즉시 상체 가중치를 변경
    /// 상체 레이어가 나중에 평가되어도 같은 프레임에 발사 자세를 비우도록 플래그를 전달하고,
    /// 취소한 발사 Trigger가 회피 후 재생되지 않게 지워둠
    /// </summary>

    public override void OnStateEnter(Animator animator, AnimatorStateInfo info, int layerIndex)
    {
        bool blocked = fullBody || animator.GetBool("IsDead") || animator.GetBool("IsDodging");
        animator.SetBool("FullBodyAction", blocked);

        if (animator.layerCount > 1)
            animator.SetLayerWeight(1, blocked ? 0f : 1f);

        if (blocked)
            animator.ResetTrigger("Shoot");
    }
}
