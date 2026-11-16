using RPGCharacterAnims.Actions;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

#if UNITY_EDITOR
using static UnityEditor.ShaderGraph.Internal.KeywordDependentCollection;
#endif
/// <summary>
/// 스폰 후 거리 제한 없이 플레이어를 추적하는 enum 기반 FSM을 구현
/// 몬스터 자신의 수평 위치가 활성 코어 반경 안이면 이동 속도가 강화됩니다.
/// 랜덤 위치 선정은 스포너, 경로 이동은 NavMeshAgent가 담당
/// 현재 Attack은 거리 내 정지만 처리하며 실제 타격은 이후 연결합니다.
/// </summary>
/// 


public class EnemyController : MonoBehaviour
{
    // 풀에서 활성화된 적만 HUD에 공개합니다. 매 프레임 씬 전체를 검색하지 않습니다.
    static readonly List<EnemyController> activeThreats = new List<EnemyController>();
    public static IReadOnlyList<EnemyController> ActiveThreats => activeThreats;

    // 새 Play 시작 시 도메인 재로드가 꺼져 있어도 이전 실행의 적 참조를 비워둠
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetThreats()
    {
        activeThreats.Clear();
    }

    // HUD가 호출되어 실제 AI의 공격 거리 안에 있는 살아 있는 대상에게만 사전 경고를 허용
    public bool IsAttackThreatTo(Health victim)
    {
        return isActiveAndEnabled && health != null && !health.IsDead && victim != null && !victim.IsDead
            && targetHealth == victim && attackArea != null && CanNavigate() && !isKnockBack
            && curState != eEnemyState.Hit && curState != eEnemyState.Dead && GetTargetDistance() <= attackRange;
    }

    // 행동 상태를 관리하기위한 enum 정의
    public enum eEnemyState
    { 
        Idle,
        Chase,
        Attack,
        Hit,
        Dead
    }


    [Header("레퍼런스")]
    [SerializeField] NavMeshAgent agent;        // 이동과 경로 계산을 담당하는 NavMeshAgent
    [SerializeField] Animator anim;             // 이동과 공격 애니메이션을 담당하는 Animator
    [SerializeField] Health health;             // 사망 시 이동을 중단하고 상태를 Idle로 전환하기 위해 구독하는 Health
    [SerializeField] Health targetHealth;       // 추적할 대상의 Health. 스폰 직후 Initialize로 전달하거나 Inspector로 연결합니다.
    [SerializeField] AttackArea attackArea;     // 몬스터의 실제 타격 범위


    [Header("몬스터 설정")]
    [SerializeField, Min(0.1f)] float walkSpeed = 2.5f;                 // 몬스터의 일반 이동 속도
    [SerializeField, Min(0.1f)] float runSpeed = 5f;                    // 몬스터의 강화 이동 속도 ( 코어 안에서 적용 )
    [SerializeField, Min(0f)] float attackDamage = 10f;                 // 몬스터의 공격력
    [SerializeField, Min(0.2f)] float attackRange = 1.8f;               // 몬스터의 공격 범위
    [SerializeField, Min(0.1f)] float attackCoolTime = 1f;              // 공격 후 다음 공격까지 대기 시간   
    [SerializeField, Min(0.01f)] float attackExitBuffer = 0.3f;         // 공격 범위 경계를 넘어서면 추적상태로 변경하기위한 값
    [SerializeField, Min(0.05f)] float repathInterval = 0.2f;           // 추적 중 경로를 갱신하는 간격

    [Header("넉백 설정")]
    [SerializeField] LayerMask knockBackObs;
    Vector3 knockBackDir;
    float knockBackDis, knockBackDur, knockBackElapsed;
    bool isKnockBack;

    [Header("현재 상태 - 실행 중 확인")]
    [SerializeField] eEnemyState curState;              // 현재 상태를 표시
    [SerializeField] bool isCoreBoosted;                // 현재 코어 강화 상태를 표시

    float repathTimer;                      // 추적 중 경로 갱신을 위한 타이머
    float attackCoolTimer;                  // 공격 후 남은 대기시간
    int moveSpeedHash;                      // Animator MoveSpeed 파라미터 해시
    int coreBoostedHash;                    // Animator IsCoreBoosted 파라미터 해시
    int attackHash;                         // Animator Attack 파라미터 해시
    int hitHash;                                // Animator Hit 파라미터 해시
    int isDeadHash;                         // Animator Dead 파라미터 해시
    bool hasCoreParameter;                  // Animator에 IsCoreBoosted 파라미터가 연결되어 있는지 확인
    bool hasOpenedAttackArea;               // 중복 이벤트로 같은 공격판정을 막는 해시
    bool isAttacking;                       // 공격 중인지 확인
    bool hitMotionEnded;                    // 피격 애니메이션의 종료 지점
    

    // 외부에서 현재 상태와 코어 강화 여부를 확인할 수 있는 프로퍼티
    public eEnemyState CurrentState { get { return curState; } }
    public bool IsCoreBoosted { get { return isCoreBoosted; } }


    // 참조와 Animator 파라미터를 연결
    // 이동은 Agent만 담당하도록 Root Motion을 비활성화 처리
    void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (anim == null) anim = GetComponentInChildren<Animator>();
        if (health == null) health = GetComponent<Health>();

        if (agent == null || anim == null || health == null)
        {
            enabled = false;
            return;
        }

        // Animator 파라미터 해시를 미리 계산하여 LateUpdate에서 성능을 향상시키도록 설정
        moveSpeedHash = Animator.StringToHash("MoveSpeed");
        coreBoostedHash = Animator.StringToHash("IsCoreBoosted");
        attackHash = Animator.StringToHash("Attack");
        hitHash = Animator.StringToHash("Hit");
        isDeadHash = Animator.StringToHash("IsDead");

        // Animator에 IsCoreBoosted 파라미터가 연결되어 있는지 확인
        foreach (AnimatorControllerParameter parameter in anim.parameters)
        {
            if (parameter.nameHash == coreBoostedHash &&
                parameter.type == AnimatorControllerParameterType.Bool)
                hasCoreParameter = true;
        }

        if (!hasCoreParameter)
            Debug.LogWarning("Animator에 Bool IsCoreBoosted와 Run연결확인 ", this);

        anim.applyRootMotion = false;
        agent.stoppingDistance = Mathf.Max(0f, attackRange - 0.1f);
        agent.speed = walkSpeed;
    }

    // 활성화될 때 상태를 초기화하고 사망 알림을 구독
    // 스폰 직후 첫 Update에서 유효한 대상이 있으면 추적을 시작
    void OnEnable()
    {
        if (!activeThreats.Contains(this))
            activeThreats.Add(this);

        isKnockBack = false;
        knockBackElapsed = 0f;
        hitMotionEnded = false;
        curState = eEnemyState.Idle;
        repathTimer = 0f;

        if (health != null)
        {
            health.OnDamaged += HandleDamaged;
            health.OnDied += HandleDeath;
        }

        if(anim != null)
        {
            anim.ResetTrigger(hitHash);
            anim.SetBool(isDeadHash, false);
        }

        // Health 재설정 등 전체 오브젝트 풀 초기화는 이후 예정
    }


    // 초기 NavMesh 배치 오류를 체크
    // 이동 가능 지점에 배치되지 않으면 경고를 출력
    void Start()
    {
        if (!CanNavigate())
            Debug.LogWarning("몬스터를 해당 Agent Type의 NavMesh 위에 배치하세요.", this);
        if (targetHealth == null)
            Debug.LogWarning("Target Health를 연결하거나 Initialize로 전달하세요.", this);
    }


    // 초기화 진입점에 미래 스포너가 생성 직후 호출하는 역할의 메소드
    // playerHealth는 추적할 플레이어, activeCore와 radius는 현재 코어 정보입니다
    // 코어가 없으면 null을 전달하며 추적 자체에는 영향이 없습니다
    public void Initialize(Health playerHealth, bool coreBoosted)
    {
        // 추적할 대상
        targetHealth = playerHealth;
        repathTimer = 0f;
        attackCoolTimer = 0f;

        isAttacking = false;
        hasOpenedAttackArea = false;

        DisableAttackArea();
        SetCoreBoosted(coreBoosted);
    }

    // 현재 맵에 활성화 되어 있는 코어가 존재한다면
    // 몬스터에 상태오 스탯을 변경
    public void SetCoreBoosted(bool boosted)
    {
        isCoreBoosted = boosted;

        if (agent != null)
            agent.speed = isCoreBoosted ? runSpeed : walkSpeed;
        if (anim != null && hasCoreParameter)
            anim.SetBool(coreBoostedHash, isCoreBoosted);
    }

    // 코어와 대상을 확인 후 현재 상태의 행동 실행
    // 대상이 없거나 본인이 사망, 혹은 NavMesh가 비활성화 된다면 행동 중지
    void Update()
    {
        // 사망 시 애니메이션 변경과 행동 중지
        if(health.IsDead)
        {
            ChangeState(eEnemyState.Dead);
            StopMovement();
            return;
        }
        // 넉백 중에는 추적, 공격 대신 넉백되는 이동만 처리
        if(isKnockBack)
        {
            UpdateKnockBack();
            return;
        }

        // 추적 중 공격 대기시간을 감소시켜 다시 접근했을 때 공격이 가능하도록 설정
        attackCoolTimer = Mathf.Max(0f, attackCoolTimer - Time.deltaTime);

        if (!CanNavigate() || targetHealth == null || !targetHealth.gameObject.activeInHierarchy || targetHealth.IsDead)
        {
            ChangeState(eEnemyState.Idle);
            StopMovement();
            return;
        }

        // 현재 상태에 따라 행동을 실행
        switch (curState)
        {
            case eEnemyState.Idle:
                ChangeState(eEnemyState.Chase);
                UpdateChase();
                break;
            case eEnemyState.Chase:
                UpdateChase();
                break;
            case eEnemyState.Attack:
                UpdateAttack();
                break;
            case eEnemyState.Hit:
                TryFinishHit();
                break;
            case eEnemyState.Dead:
                break;
        }
    }


    // 근접 거리에서는 정지하고, 그 외에는 목적지를 일정 간격으로 갱신하도록 설정
    void UpdateChase()
    {
        if (GetTargetDistance() <= attackRange)
        {
            ChangeState(eEnemyState.Attack);
            return;
        }

        repathTimer -= Time.deltaTime;
        if (repathTimer > 0f || agent.pathPending)
            return;

        repathTimer = repathInterval;
        agent.SetDestination(targetHealth.transform.position);
    }


    // 공격 상태 업데이트, 아직 애니메이션 연결 x
    // 이후 추적을 재개할 시 공격상태를 종료
    void UpdateAttack()
    {
        // 공격중이면 취소
        if (isAttacking)
            return;
        
        // 거리
        float distance = GetTargetDistance();

        if (distance > attackRange + attackExitBuffer)
        {
            ChangeState(eEnemyState.Chase);
            return;
        }

        // 거리가 벌어졋다하더라도 정지는 하되 공격은 시작하지않게 설정
        if (distance > attackRange)
            return;
        if (attackCoolTimer > 0f)
            return;

        StartAttack();
    }

    // 공격 한번을 담은 메소드
    // 이동을 정지시키고 대상 방향으로 몸을 돌린 뒤 애니메이션을 재생시킵니다
    void StartAttack()
    {
        if (attackArea == null)
            return;

        DisableAttackArea();
        hasOpenedAttackArea = false;

        // 이동 정지
        StopMovement();

        // 방향 계산
        Vector3 direction = targetHealth.transform.position - transform.position;
        direction.y = 0f;

        // 바라보는 방향
        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(direction);

        isAttacking = true;

        anim.ResetTrigger(attackHash);
        anim.SetTrigger(attackHash);
    }

    // Animation Event로 전달할 공격이 끝나는걸 알릴 메소드
    public void EndAttack()
    {
        if (!isActiveAndEnabled || !isAttacking)
            return;

        DisableAttackArea();
        isAttacking = false;
        attackCoolTimer = attackCoolTime;
    }

    // 공격을 중단할 때 내부 상태를 정리하는 메소드
    // 미사용된 트리거를 제거하고 모션을 Idle로 전환
    void CancelAttack()
    {
        isAttacking = false;
        DisableAttackArea();

        if (anim == null)
            return;

        anim.ResetTrigger(attackHash);
    }
    // 상태 변경을 한 곳에 모아 이전 행동 종료 후 새 행동을 시작하는 메소드
    // 코어 강화 여부는 Chase 상태 변경 x
    void ChangeState(eEnemyState nextState)
    {
        if (curState == nextState)
            return;
        ExitState(curState);
        curState = nextState;
        EnterState(curState);
        Debug.Log($"{name} 상태: {curState}", this);
    }

    // 추적에 진입하면 Agent를 재개하고 즉시 경로를 요청
    void EnterState(eEnemyState state)
    {
        if (state == eEnemyState.Chase && CanNavigate())
        {
            agent.isStopped = false;
            repathTimer = 0f;
        }
        else 
            StopMovement();
    }

    // 추적을 종료할 시 경로 제거, 공격 요청 취소 
    void ExitState(eEnemyState state)
    {
        if (state == eEnemyState.Chase)
            StopMovement();

        if (state == eEnemyState.Attack)
            CancelAttack();
    }

    // 모델의 실제 위치 간 직선거리를 반환
    float GetTargetDistance()
    {
        return Vector3.Distance(transform.position, targetHealth.transform.position);
    }

    // Agent가 활성화되고 NavMesh에 연결된 경우에만 이동 API를 허용
    bool CanNavigate()
    {
        return agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
    }

    // 이동과 기존 경로를 정리합니다. 초기화 실패 시에도 안전하게 호출
    void StopMovement()
    {
        if (CanNavigate())
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
        if (anim != null) anim.SetFloat("MoveSpeed", 0f);
    }

    // 총기가 산탄 피해 처리를 완료 했을 시 호출되는 메소드
    // dir = 넉백되는 월드 방향, dis = 넉백되는 최대 이동거리, dur = 넉백되는 이동 시간
    public void ApplyKnockback(Vector3 dir, float dis, float dur)
    {
        if (!isActiveAndEnabled || health == null || health.IsDead)
            return;
        if (!CanNavigate() || agent.isOnOffMeshLink || dis <= 0f)
            return;

        dir.y = 0f;     // 공중으로 밀기 x
        if (dir.sqrMagnitude < 0.0001f)
            return;

        // 공격 판정을 닫고 기존 추적 경로를 정리합니다.
        CancelAttack();
        ChangeState(eEnemyState.Hit);
        StopMovement();

        knockBackDir = dir.normalized;
        knockBackDis = dis;
        knockBackDur = Mathf.Max(0.01f, dur);
        knockBackElapsed = 0f;
        isKnockBack = true;
    }

    // Update에서 넉백 중 매 프레임 호출
    // 처음 빠르게 밀고 점차 감속하며, 장애물과 NavMesh 경계를 못넘도록 설정
    // deltaTime을 사용하므로 카드 선택 일시정지 중에는 이동도 멈춥
    void UpdateKnockBack()
    {
        if (!CanNavigate() || agent.isOnOffMeshLink)
        {
            FinishKnockback();
            return;
        }

        if (Time.deltaTime <= 0f)
            return;

        float previousT = Mathf.Clamp01(
            knockBackElapsed / knockBackDur);

        knockBackElapsed = Mathf.Min(
            knockBackElapsed + Time.deltaTime, knockBackDur);

        float currentT = knockBackElapsed / knockBackDur;

        // 누적 이동률의 차이로 이번 프레임 이동량을 계산
        // 장애물이 없으면 전체 이동량의 합이 설정 거리와 같아지도록
        float previousProgress = 1f - (1f - previousT) * (1f - previousT);
        float currentProgress = 1f - (1f - currentT) * (1f - currentT);
        float requestedStep =
            knockBackDis * (currentProgress - previousProgress);

        if (requestedStep <= 0f)
        {
            FinishKnockback();
            return;
        }

        float allowedStep = requestedStep;

        // 몸통 크기의 캡슐로 벽,기둥을 검사
        // 바닥과 닿지 않도록 아래쪽에 작은 틈을 둠
        float radius = Mathf.Max(0.05f, agent.radius * 0.9f);
        float height = Mathf.Max(agent.height, radius * 2f + 0.1f);
        Vector3 bottom = transform.position + Vector3.up * (radius + 0.05f);
        Vector3 top = transform.position + Vector3.up * (height - radius + 0.05f);

        if (Physics.CapsuleCast(bottom, top, radius, knockBackDir,out RaycastHit obstacleHit, requestedStep + 0.02f, 
                                                                            knockBackObs, QueryTriggerInteraction.Ignore))
        {
            allowedStep = Mathf.Min(allowedStep, Mathf.Max(0f, obstacleHit.distance - 0.02f));
        }

        // NavMesh 밖이나 걸어서 직진할 수 없는 경계도 못넘음
        Vector3 destination = agent.nextPosition + knockBackDir * requestedStep;

        if (agent.Raycast(destination, out NavMeshHit navHit))
        {
            allowedStep = Mathf.Min(allowedStep, Mathf.Max(0f, navHit.distance - 0.02f));
        }

        if (allowedStep > 0f)
            agent.Move(knockBackDir * allowedStep);

        // 벽에 막혔으면 남은 거리를 나중에 몰아서 이동하지 않도록
        if (allowedStep < requestedStep || knockBackElapsed >= knockBackDur)
        {
            FinishKnockback();
        }
    }

    // 이동 완료 또는 장애물로 중단되었을 때 호출되는 메소드
    // 넉백 잠금을 해제하고 기존 피격 종료 로직으로 다음 행동을 결정
    void FinishKnockback()
    {
        isKnockBack = false;
        knockBackElapsed = 0f;

        TryFinishHit();
    }

    // 살아있는 상태에서 피해를 받으면 Hit 상태로 전환
    void HandleDamaged(float damage)
    {
        if (health == null || health.IsDead)
            return;
        hitMotionEnded = false;     // 피격이 끝났더라도 새로운 피격은 기다려야함

        ChangeState(eEnemyState.Hit);

        // 이미 Hit 상태에서 다시 맞으면 상태를 관리하는 ChangeState가 반환하므로 이동 정지 명령을 추가
        StopMovement();

        anim.ResetTrigger(hitHash);
        anim.SetTrigger(hitHash);
    }

    // Hit 애니메이션이 끝날때 event로 호출
    public void EndHit()
    {
        if (curState != eEnemyState.Hit)
            return;
        if (health == null || health.IsDead)
            return;

        hitMotionEnded = true;

        TryFinishHit();

    }


    void TryFinishHit()
    {
        if (curState != eEnemyState.Hit)
            return;
        if (health == null || health.IsDead)
            return;
        if (!hitMotionEnded || isKnockBack)
            return;
        if (anim == null)
            return;

        // EndHit 이벤트 메소드가 클립 끝보다 먼저 호출된다 하여도 화면내에서 Hit 모션이 재생되는 동안엔 추적 x
        AnimatorStateInfo current = anim.GetCurrentAnimatorStateInfo(0);

        if (current.IsName("Hit"))
            return;
        if(anim.IsInTransition(0))      // 다른 상태에서 Hit로 들어가는 블렌딩 이동을 막음
        {
            AnimatorStateInfo next = anim.GetNextAnimatorStateInfo(0);

            if (next.IsName("Hit"))
                return;
        }

        hitMotionEnded = false;

        // 대상이 존재하지않는다면 대기 상태로 전환
        if (targetHealth == null || targetHealth.IsDead || !targetHealth.gameObject.activeInHierarchy)
        {
            ChangeState(eEnemyState.Idle);
            return;
        }

        // 피격 종료 시점에서 다시 거리를 계산
        if (GetTargetDistance() <= attackRange)
            ChangeState(eEnemyState.Attack);
        else
            ChangeState(eEnemyState.Chase);
    }


    // 사망 시 즉시 상태변경
    void HandleDeath()
    {
        isKnockBack = false;
        knockBackElapsed = 0f;
        ChangeState(eEnemyState.Dead);
        StopMovement();

        anim.ResetTrigger(hitHash);
        anim.SetBool(isDeadHash, true);
    }

    // 실제 수평 속도와 코어 강화 조건을 별도로 Animator에 전달하는 메소드
    // 코어 안이어도 정지한 상태라면 Idle을 표시하도록 전환 조건을 구성
    void LateUpdate()
    {
        if (anim == null)
            return;

        Vector3 velocity = CanNavigate() && !agent.isStopped ?
             agent.velocity : Vector3.zero;
        velocity.y = 0f;
        anim.SetFloat(moveSpeedHash, velocity.magnitude);

        if (hasCoreParameter)
            anim.SetBool(coreBoostedHash, isCoreBoosted);
    }

    // 비활성화 시 사망 구독을 해제하고 Agent 단독 이동을 방지하는 메소드
    void OnDisable()
    {
        activeThreats.Remove(this);
        isKnockBack = false;
        knockBackElapsed = 0f;

        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDied -= HandleDeath;
        }

        CancelAttack();
        StopMovement();
    }

    // 강화 속도가 일반 속도보다 낮아지지 않도록 Inspector 값을 보정
    void OnValidate()
    {
        runSpeed = Mathf.Max(runSpeed, walkSpeed);
    }

    // 몬스터의 타격 시작 이벤트가 시작되면 공격 판정을 여는 과정
    public void EnableAttackArea()
    {
        if (!isActiveAndEnabled || !isAttacking)
            return;
        if (curState != eEnemyState.Attack || hasOpenedAttackArea)
            return;
        if (health == null || health.IsDead || attackArea == null)
            return;
        if (targetHealth == null || targetHealth.IsDead || !targetHealth.gameObject.activeInHierarchy)
            return;

        hasOpenedAttackArea = true;
        // 프리팹 공격력은 보존하고 실제 타격을 열 때 현재 도전 배율을 적용합니다.
        attackArea.BeginAttack(attackDamage * ChallengeRunState.EnemyMultiplier);
    }

    // 몬스터의 타격 종료 또는 공격 취소 시 공격 판정을 닫는 과정
    public void DisableAttackArea()
    {
        if (attackArea != null)
            attackArea.EndAttack();
    }
}
