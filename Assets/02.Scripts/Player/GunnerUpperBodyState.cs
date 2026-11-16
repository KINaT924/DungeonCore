using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Gunner Animator의 Empty와 Pistol_Fire 상태에 부착합니다
/// 발사 상태에서만 상체 Override 레이어를 켜고, 그 외에는 Base Layer에서의
/// 조준 이동,회피,사망 모션이 상체까지 표시되도록 레이어를 끄도록 설정합니다
/// Motion이 없는 Empty는 마지막 자세를 지우는 역할을 보장하지 않으므로
/// 상태 전환과 함께 레이어 Weight를 명시적으로 관리합니다
/// </summary>
public sealed class GunnerUpperBodyState : StateMachineBehaviour
{
    [FormerlySerializedAs("isFireState")]
    [Tooltip("발사와 재장전 상태에서 켜고, Empty에서는 끕니다.")]
    [SerializeField] bool useUpperBody;

    static readonly int DeadHash = Animator.StringToHash("IsDead");
    static readonly int DodgeHash = Animator.StringToHash("IsDodging");
    static readonly int ShootHash = Animator.StringToHash("Shoot");
    static readonly int EmptyHash = Animator.StringToHash("UpperBody.Empty");

    /// <summary>
    /// 상태 진입 시 호출하는 이벤트를 설정
    /// Empty 진입은 상체 영향을 제거하고 발사 진입은 사망,회피 여부를 확인한 뒤 상체 모션을 표시하도록 제작
    /// layerIndex는 이 Behaviour가 연결된 UpperBody 레이어 번호 인덱스입니다
    /// </summary>
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        UpdateLayerWeight(animator, layerIndex);
    }

    /// <summary>
    /// 발사 재생 중 매 Animator 평가에서 호출됩니다.
    /// 도중에 회피하거나 사망하면 발사 클립 종료를 기다리지 않고
    /// 상체 영향을 제거하고 Empty로 돌아가 전신 모션에 제어를 넘깁니다.
    /// Empty에서는 발사로 전환 중인 레이어를 다시 끄지 않도록 처리하지 않습니다.
    /// </summary>
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (useUpperBody)
            UpdateLayerWeight(animator, layerIndex);
    }

    /// <summary>
    /// 현재 상태와 중단 조건으로 상체 가중치를 결정합니다.
    /// 정상 종료로 Empty에 전환 중일 때도 Weight를 0으로 유지하여
    /// 빈 상태가 발사 마지막 자세를 덮어쓰는 현상을 방지합니다.
    /// 발사 취소 시 Trigger를 제거하므로 회피 후 지연 발사가 남지 않습니다.
    /// </summary>
    void UpdateLayerWeight(Animator animator, int layerIndex)
    {
        if (layerIndex <= 0)
            return;

        bool interrupted = animator.GetBool(DeadHash) || animator.GetBool(DodgeHash);
        bool returningToEmpty = animator.IsInTransition(layerIndex)
            && animator.GetNextAnimatorStateInfo(layerIndex).fullPathHash == EmptyHash;

        float weight = useUpperBody && !interrupted && !returningToEmpty ? 1f : 0f;

        animator.SetLayerWeight(layerIndex, weight);

        if (useUpperBody && interrupted)
        {
            animator.ResetTrigger(ShootHash);
            animator.Play(EmptyHash, layerIndex, 0f);
        }
    }
}
