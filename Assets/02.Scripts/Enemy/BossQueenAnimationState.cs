using UnityEngine;

/// <summary>
/// 클립의 정규화 진행도에서 부유와 발동을 전달하는 스크립트
/// 원본 FBX에 Animation Event를 심지 않음
/// </summary>

public class BossQueenAnimationState : StateMachineBehaviour
{
    public BossQueenAnimationDriver.Action action;

    [Range(-1, 1)] public float releaseAt = -1;
    BossQueenAnimationDriver driver;
    bool released;

    // 각 상태에 들어올 때 발동 여부를 초기화하고 해당 Animator의 보스 연결을 찾는 용도
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        driver = animator.GetComponentInParent<BossQueenAnimationDriver>();
        released = false;

        if (driver != null)
            driver.NotifyStateEntered(action);
    }

    // 실제 재생 속도에 맞는 진행도를 사용하여 부유와 1회 발동을 처리
    // 일시정지 시 함께 정지
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (driver == null || driver.IsDead)
            return;
        if (action == BossQueenAnimationDriver.Action.AreaCast)
            driver.UpdateHover(stateInfo.normalizedTime);

        if (!released && releaseAt >= 0 && stateInfo.normalizedTime >= releaseAt)
        {
            released = true; driver.NotifyReleased(action);
        }
    }

    // 마지막 프레임을 건너뛴 경우 정상 완료된 시전만 1회 발동을 보완한 뒤 행동 잠금을 해제
    // 사망 중단은 Driver에서 차단
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (driver == null)
            return;

        if (!released && releaseAt >= 0 && stateInfo.normalizedTime >= releaseAt)
        {
            released = true; driver.NotifyReleased(action);
        }
        driver.NotifyStateExited(action);
    }
}
