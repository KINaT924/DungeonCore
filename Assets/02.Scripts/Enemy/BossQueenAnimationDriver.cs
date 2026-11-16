using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 몽마퀸에 대한 Animator의 요청, 행동 잠금, 시전 알림, 시각적 부유를 관리
/// </summary>

public class BossQueenAnimationDriver : MonoBehaviour
{
    // Animator 상태와 외부 보스 로직이 공유하는 동작 이름을 관리하는 ENUM
    public enum Action
    { 
        Idle,
        Walk,
        PrimaryCast,
        AreaCast,
        Hit,
        Death
    }

    [Header("씬 연결")]
    [SerializeField] Animator animator;
    [SerializeField] Transform motionRoot;
    [SerializeField] Transform primaryCastOrigin;
    [SerializeField] Transform areaCastOrigin;

    [Header("광역 부유 — 월드 미터")]
    [Min(0)] [SerializeField] float hoverHeight = 1.6f;

    [Header("실제 공격 로직을 나중에 연결할 발동 시점")]
    [SerializeField] UnityEvent onPrimaryReleased = new UnityEvent();
    [SerializeField] UnityEvent onAreaReleased = new UnityEvent();

    
    Action currentAction;                           // 시전 요청 즉시 잠가 같은 프레임의 중복 공격이나 피격 취소를 방지
    Action? requestedAction;                        // Trigger 요청과 실제 상태 진입 사이에는 이전 Idle/Walk 알림이 와도 시전 잠금을 유지
    bool dead;
    Vector3 restingLocalPosition;

    static readonly int SpeedId = Animator.StringToHash("MoveSpeed");
    static readonly int DeadId = Animator.StringToHash("IsDead");

    public Action CurrentAction => currentAction;
    public bool IsDead => dead;
    public bool IsBusy => currentAction != Action.Idle && currentAction != Action.Walk;
    public Animator Animator => animator;
    public Transform MotionRoot => motionRoot;
    public Transform PrimaryCastOrigin => primaryCastOrigin;
    public Transform AreaCastOrigin => areaCastOrigin;
    public UnityEvent PrimaryReleased => onPrimaryReleased;
    public UnityEvent AreaReleased => onAreaReleased;

    // 실제 PrimaryCast 상태에 진입한 프레임
    // 요청 시점과 시각 효과 시작 시점을 구분하는데 사용
    public event System.Action PrimaryStarted;

    // Play 시작 시 부유 기준 위치를 보관하고, 위치 이동을 Animator가 덮어쓰지 않게 하기위한 참조관리
    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (motionRoot != null)
            restingLocalPosition = motionRoot.localPosition;
        if (animator != null)
            animator.applyRootMotion = false;
    }

    // 도중에 씬을 끄거나 오브젝트를 비활성화하면 시각 루트의 부유 높이를 원래상태로 복원
    void OnDisable()
    { 
        RestoreHeight();
    }

    // 이동 AI가 호출할 정규화 속도
    // 0은 대기, 0.1 이상은 걷기이며 실제 좌표를 이동하지 않게 설정
    public void SetMoveSpeed(float normalizedSpeed)
    {
        if (animator != null && !dead)
            animator.SetFloat(SpeedId, Mathf.Clamp01(normalizedSpeed));
    }

    // 중립/걷기 중 기본 시전을 요청하는 메소드
    // 이미 행동 중이거나 사망했다면 false를 반환
    public bool TryPrimaryCast()
    {
        return Request(Action.PrimaryCast, "PrimaryCast");
    }

    //중립/걷기 중 광역 시전을 요청하는 메소드
    //부유는 광역 상태의 정규화 진행도와 함께 시작
    public bool TryAreaCast() 
    { 
        return Request(Action.AreaCast, "AreaCast");
    }

    // 대기/걷기 중 짧은 피격을 요청하는 메소드
    // 시전 중에는 피격 동작으로 공격을 끊지 못함
    public bool TryHit()
    { 
        return Request(Action.Hit, "Hit");
    }

    // 공통 잠금 검사 후 Trigger를 한 번 보내는 용도의 메소드
    // 반환값은 요청 수락 여부를 검사
    bool Request(Action action, string trigger)
    {
        if (!Application.isPlaying || animator == null || dead || IsBusy || animator.IsInTransition(0)) 
            return false;

        requestedAction = action;
        currentAction = action;
        animator.SetTrigger(trigger);

        return true;
    }

    // 모든 동작보다 사망을 우선
    // 발동 전 시전 알림을 차단하고 부유를 해제하며 마지막 자세를 유지
    public void Die()
    {
        if (!Application.isPlaying || animator == null || dead) 
            return;

        dead = true; 
        requestedAction = null;
        currentAction = Action.Death;
        RestoreHeight();

        animator.ResetTrigger("PrimaryCast");
        animator.ResetTrigger("AreaCast");
        animator.ResetTrigger("Hit");
        animator.SetFloat(SpeedId, 0);
        animator.SetBool(DeadId, true);
    }

    // Animator의 실제 상태 진입 알려주는 메소드
    // 블렌딩 중 이전 상태가 사망/새 시전 잠금을 해제하지 못하게 막음
    public void NotifyStateEntered(Action action)
    {
        if (dead && action != Action.Death)
            return;
        // 이전 중립 상태의 진입 알림이 다음 시전 요청을 취소하지 못하게 설정
        if (requestedAction.HasValue && action != requestedAction.Value)
            return;

        // Idle은 시전 후 Idle 블렌딩 시작 때 먼저 진입
        // 이전 시전의 마지막 발동/종료 알림까지 잠금을 유지
        if (!requestedAction.HasValue && IsBusy && (action == Action.Idle || action == Action.Walk))
            return;

        requestedAction = null;
        currentAction = action;

        if (action != Action.AreaCast)
            RestoreHeight();
        if (action == Action.PrimaryCast)
            PrimaryStarted?.Invoke();
    }

    // 시전/피격 상태가 완전히 빠져나갈 때 잠금을 푸는용도의 메소드
    // Idle 진입만으로 마법진이 사라지지 않음
    public void NotifyStateExited(Action action)
    {
        if (dead || requestedAction.HasValue || currentAction != action)
            return;
        if (action == Action.PrimaryCast || action == Action.AreaCast || action == Action.Hit)
            currentAction = Action.Idle;
    }

    // 상태의 발동 프레임에서 한 번 호출
    // 인자는 기본/광역 종류이며 사망 이후에는 공격 알림을 전달하지 않음
    public void NotifyReleased(Action action)
    {
        // 블렌딩 중 남은 이전 상태의 업데이트가 새 시전의 이펙트를 지우거나 공격을 만들지 않습니다
        if (dead || requestedAction.HasValue || action != currentAction)
            return;
        if (action == Action.PrimaryCast)
            onPrimaryReleased.Invoke();
        if (action == Action.AreaCast)
            onAreaReleased.Invoke();
    }

    // 광역 클립 진행도(0~1)에 맞춰 32까지 상승, 68까지 유지, 94까지 하강하고 착지 시간을 일부 남김
    public void UpdateHover(float normalizedTime)
    {
        if (motionRoot == null || dead || currentAction != Action.AreaCast)
            return;

        float t = Mathf.Clamp01(normalizedTime);
        float height = t < .32f ? Mathf.SmoothStep(0, 1, t / .32f) : t > .68f ? 1 - Mathf.SmoothStep(0, 1, (t - .68f) / .26f) : 1;
        float scale = motionRoot.parent != null ? Mathf.Abs(motionRoot.parent.lossyScale.y) : 1;
        motionRoot.localPosition = restingLocalPosition + Vector3.up * (height * hoverHeight / Mathf.Max(.001f, scale));
    }

    // 시전 완료/중단 시 보스 배치 루트를 건드리지 않고 시각 오프셋만 원래 높이로 복원
    void RestoreHeight()
    { 
        if (motionRoot != null)
            motionRoot.localPosition = restingLocalPosition;
    }

}
