using System;
using UnityEngine;
using UnityEngine.AI;

// 몽마퀸의 전장 전체 탐지,거리 유지, 공격 선택, 체력 구간 반응,사망을 관리하는 스크립트
[RequireComponent(typeof(Health), typeof(NavMeshAgent), typeof(BossQueenAnimationDriver))]
public class BossQueenController : MonoBehaviour
{
    public enum Phase { Waiting, Moving, PrimaryCast, AreaCast, Hit, Dying, Cleared, PlayerDefeated }
    [Header("씬 연결 — 범위 제한 없이 이 플레이어를 탐지")]
    public Health player;
    public Health health;
    public BossQueenAnimationDriver animationDriver;
    public NavMeshAgent agent;


    [Header("행동 수치")]
    [Min(0)] public float initialWait = 3;
    [Min(.05f)] public float moveSpeed = .4f;
    public Vector2 preferredDistance = new Vector2(11, 16);
    [Min(1)] public float arenaRadius = 24;
    public Vector3 arenaCenter;
    [Min(.1f)] public float primaryCooldown = 4;
    [Min(.1f)] public float areaCooldown = 15;


    [Header("공격 프리팹과 수치")]
    public BossSpellVisual primaryCircle;
    public BossSpellVisual areaCircle;
    public BossAttackProjectile fireball;
    public BossAttackProjectile fallingRock;
    public BossSpellVisual fireImpact;
    public BossSpellVisual rockImpact;
    public BossSpellVisual dangerCircle;
    public BossSpellVisual deathEffect;
    public float fireDamage = 15;
    public float rockDamage = 25;
    public float fireRadius = 2;
    public float rockRadius = 1.6f;
    public float rockWarningSeconds = 1.2f;

    [Header("보스 공격 관련")]
    [Min(0.1f)] public float fireballSpeed = 20f;                           // 파이어볼 투사체 속도
    [Min(0.1f)] public float frieballMinTraveTime = 0.35f;           
    [Min(0.1f)] public float rockSizeMultiplier = 1.5f;

    [Header("마법진 위치 관련")]
    [Min(0f)] public float primaryCircleForward = 2f;
    [Tooltip("보스 발밑 기준 월드 높이입니다. 현재 2.5배 모델의 팔 높이는 약 5.5m입니다.")]
    public float primaryCircleHeight = 5.5f;
    [Tooltip("오른팔 방향의 월드 오프셋입니다. 시전 시작 위치에 고정하며 손을 계속 따라가지 않습니다.")]
    public float primaryCircleSide = 1f;


    [Header("현재 진행 상태")]
    [SerializeField] Phase phase;
    [SerializeField] int pendingHits;
    int nextHealthQuarter = 3;
    float readyAt, nextPrimary, nextArea, nextDestination, orbitChange;
    int orbitDirection = 1;
    Transform attacksRoot;
    BossSpellVisual chargeCircle;
    bool primaryReleased;                   // 한 번의 실제 시전 진입당 투사체를 한 번만 발사


    // 첫 대기부터 처치까지의 배율 시간
    // 카드 선택 등 Time.timeScale=0 동안은 증가하지 않음
    public float CombatElapsedSeconds { get; private set; }

    public Phase CurrentPhase => phase;
    public bool IsFinished => phase == Phase.Dying || phase == Phase.Cleared || phase == Phase.PlayerDefeated;


    void Awake()
    {
        if (health == null)
            health = GetComponent<Health>();
        if (agent == null) 
            agent = GetComponent<NavMeshAgent>();
        if (animationDriver == null)
            animationDriver = GetComponent<BossQueenAnimationDriver>();
    }

    // 동일 대상에 이벤트를 중복 등록하지 않도록 활성 수명에 맞춰 구독
    void OnEnable()
    {
        health.OnHealthChanged += OnHealthChanged;
        health.OnDied += OnDied;
        animationDriver.PrimaryReleased.AddListener(ReleasePrimary);
        animationDriver.AreaReleased.AddListener(ReleaseArea);
        animationDriver.PrimaryStarted += BeginPrimaryCharge;
    }

    // 전장 전체 탐지 후 첫 3초 대기와 최초 공격 예약을 시작
    // 이 시간에는 이동/공격하지 못함
    void Start()
    {
        phase = Phase.Waiting;

        readyAt = Time.time + initialWait;
        nextPrimary = readyAt + 1.2f;
        nextArea = readyAt + 7;

        agent.updateRotation = false; agent.speed = moveSpeed;
        agent.acceleration = 1;
        attacksRoot = new GameObject("RuntimeBossAttacks").transform;

        FacePlayer(360);
        StopMoving();

        if (player == null)
            Debug.LogError("[몽마퀸] 씬의 플레이어 Health를 연결 확인 필요", this);
    }

    // 사망, 플레이어 사망을 먼저 처리하고, 애니메이션 종료를 기다린 뒤 피격 예약/공격/거리 유지 순으로 결정
    void Update()
    {
        if (!IsFinished && !health.IsDead && player != null && !player.IsDead)
            CombatElapsedSeconds += Time.deltaTime;
        if (phase == Phase.Dying)
        {
            var info = animationDriver.Animator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName("Death") && info.normalizedTime >= .99f)
                phase = Phase.Cleared;
            return;
        }

        if (IsFinished || player == null || Time.timeScale <= 0) 
            return;

        if (player.IsDead)
        { 
            phase = Phase.PlayerDefeated;
            StopMoving();
            CancelAttacks();
            return; 
        }

        if (phase == Phase.Waiting)
        { 
            if (Time.time < readyAt) 
                return; 
            phase = Phase.Moving;
        }

        if (animationDriver.IsBusy)
        { 
            StopMoving();
            return; 
        }
        if (phase == Phase.PrimaryCast || phase == Phase.AreaCast || phase == Phase.Hit)
        { 
            phase = Phase.Moving; ClearCharge();
        }

        if (pendingHits > 0 && animationDriver.TryHit())
        { 
            pendingHits--; phase = Phase.Hit; StopMoving(); 
            return; 
        }

        FacePlayer(35);

        if (Time.time >= nextArea && animationDriver.TryAreaCast())
        {
            phase = Phase.AreaCast;
            nextArea = Time.time + areaCooldown;
            StopMoving();
            return;
        }

        if (Time.time >= nextPrimary && animationDriver.TryPrimaryCast())
        {
            phase = Phase.PrimaryCast;
            nextPrimary = Time.time + primaryCooldown;
            StopMoving();
            return;
        }

        KeepDistance();
    }

    // 기본 시전 시작부터 4초를 계산하며, 공격하지 않는 동안에만 거리를 유지하며 걷습니다
    void KeepDistance()
    {
        if (!agent.enabled || !agent.isOnNavMesh)
        {
            animationDriver.SetMoveSpeed(0);
            return;
        }
        agent.speed = moveSpeed;

        if (Time.time >= nextDestination)
        {
            nextDestination = Time.time + .4f;
            Vector3 away = transform.position - player.transform.position; 
            away.y = 0;

            if (away.sqrMagnitude < .01f)
                away = -transform.forward;

            float distance = away.magnitude;
            away.Normalize();

            if (Time.time > orbitChange)
            { 
                orbitChange = Time.time + 7;
                orbitDirection *= -1; 
            }

            Vector3 destination;
            if (distance < preferredDistance.x || distance > preferredDistance.y)
                destination = player.transform.position + away * ((preferredDistance.x + preferredDistance.y) * .5f);
            else
                destination = transform.position + Vector3.Cross(Vector3.up, away) * orbitDirection * 2;
            destination = ClampToArena(destination, 3);

            if (NavMesh.SamplePosition(destination, out var point, 3, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.SetDestination(point.position);
            }
        }
        animationDriver.SetMoveSpeed(agent.velocity.sqrMagnitude > .002f ? 1 : 0);
    }

    // 사거리 유지 중 벽 밖의 목적지를 만들지 않도록 전투 방 내부로 한정
    Vector3 ClampToArena(Vector3 point, float margin)
    {
        Vector3 delta = point - arenaCenter; delta.y = 0;
        return arenaCenter + Vector3.ClampMagnitude(delta, arenaRadius - margin);
    }

    // 보스가 플레이어를 천천히 바라보도록 설정, 시전 중에는 발사 방향을 계속 꺾지 않고 마지막 방향을 유지
    void FacePlayer(float degrees)
    {
        if (player == null) 
            return;
        Vector3 direction = player.transform.position - transform.position;
        direction.y = 0;

        if (direction.sqrMagnitude > .001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction),
                degrees >= 360 ? 360 : degrees * Time.deltaTime);
    }

    // Animator가 실제 기본 시전에 진입할 때 호출
    // 팔 앞의 월드 위치에 마법진을 고정하고 발사까지 유지
    void BeginPrimaryCharge()
    {
        if (IsFinished || player == null || player.IsDead)
            return;
        ClearCharge();

        phase = Phase.PrimaryCast;
        primaryReleased = false;

        if (primaryCircle == null)
            return;
        chargeCircle = Instantiate(primaryCircle, GetPrimaryCirclePosition(),
            Quaternion.LookRotation(transform.forward), attacksRoot);

        // 충전 마법진은 자동 수명 대신 발사/사망/전투 종료가 정리
        chargeCircle.lifetime = 0;
    }

    // 발사 순간 위치를 한 번만 저장
    // 이후 플레이어가 움직여도 파이어볼의 도착점은 첫 지점에 떨어짐
    void ReleasePrimary()
    {
        if (phase != Phase.PrimaryCast || primaryReleased || IsFinished || player == null || player.IsDead || fireball == null)
            return;
        primaryReleased = true;

        Vector3 start = chargeCircle != null ? chargeCircle.transform.position : GetPrimaryCirclePosition();
        Vector3 target = GroundPoint(player.transform.position);

        var projectile = Instantiate(fireball, start, Quaternion.identity, attacksRoot);
        projectile.Launch(this, player, target, Mathf.Max(frieballMinTraveTime,
            Vector3.Distance(start, target) / fireballSpeed), 0, fireDamage * ChallengeRunState.EnemyMultiplier, fireRadius, fireImpact, null);
        ClearCharge();
    }

    // 기본 공격 시전 시작 시 호출하여 보스 정면의 마법진 위치를 계산
    // 손 애니메이션과 보스 스케일의 영향을 받지 않는 월드 좌표를 반환
    Vector3 GetPrimaryCirclePosition()
    {
        return transform.position
            + transform.forward * primaryCircleForward
            + transform.right * primaryCircleSide
            + Vector3.up * primaryCircleHeight;
    }

    // 최대 부유 높이에서 뒤쪽 마법진 4개를 열고 당시 플레이어 주변 4곳에 예고 후 낙석을 예약
    void ReleaseArea()
    {
        if (IsFinished || player == null || player.IsDead || fallingRock == null) 
            return;

        Vector3 center = GroundPoint(player.transform.position);
        float bodyHeight = player.TryGetComponent<CharacterController>(out var body) ? body.bounds.size.y : 2;
        // 낙석 프리팹은 가장 긴 지름이 1이 되도록 제작되어 있어 생성 배율의 절반이 외형 반경
        // 경고 원도 같은 배율을 사용하되 실제 피해 반경보다 작게 표시하지 않아 위험 구역을 빠뜨리지 않습니다.
        float rockScale = Mathf.Clamp(bodyHeight * .8f, 1.2f, 2f) * rockSizeMultiplier;
        float warningRadius = Mathf.Max(rockRadius, rockScale * .5f);

        for (int i = 0; i < 4; i++)
        {
            Vector3 behind = animationDriver.AreaCastOrigin.position - transform.forward * 1.6f + transform.right * ((i - 1.5f) * 1.5f) + Vector3.up * (1 + (i % 2) * .6f);
            if (areaCircle != null) 
            { 
                var circle = Instantiate(areaCircle, behind, Quaternion.LookRotation(transform.forward), attacksRoot);
                circle.lifetime = 2.2f; 
            }

            float angle = 45 + i * 90;
            Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0, Mathf.Sin(angle * Mathf.Deg2Rad)) * 2.2f;
            Vector3 target = GroundPoint(ClampToArena(center + offset, 2.5f));

            BossSpellVisual warning = null;
            if (dangerCircle != null) 
            { 
                warning = Instantiate(dangerCircle, target + Vector3.up * .04f, Quaternion.Euler(90, 0, 0), attacksRoot);
                warning.lifetime = rockWarningSeconds + 1.25f + i * .13f;
                warning.transform.localScale = Vector3.one * warningRadius;
            }
            var rock = Instantiate(fallingRock, target + Vector3.up * 10, UnityEngine.Random.rotation, attacksRoot);
            rock.transform.localScale = Vector3.one * rockScale;
            rock.Launch(this, player, target, .85f, rockWarningSeconds + i * .13f, rockDamage * ChallengeRunState.EnemyMultiplier, rockRadius, rockImpact, warning);
        }
    }

    // 바닥 위치를 Ground 레이어에서 구해 공중/경사에서도 경고와 도착점이 바닥에 놓이게 좌표값을 설정
    Vector3 GroundPoint(Vector3 point)
    {
        if (Physics.Raycast(point + Vector3.up * 3, Vector3.down,
            out var hit, 8, 1 << LayerMask.NameToLayer("Ground"), QueryTriggerInteraction.Ignore))
            return hit.point;
        point.y = arenaCenter.y;

        return point;
    }

    // 보스의 체력을 75/50/25%를 각각 한 번 계산
    // 해당 구간을 넘으면 피격을 쌓고 0%에서는 사망만 처리
    void OnHealthChanged(float current, float maximum)
    {
        if (current <= 0 || maximum <= 0 || IsFinished)
            return;

        while (nextHealthQuarter > 0 && current <= maximum * nextHealthQuarter * .25f)
        { 
            pendingHits++;
            nextHealthQuarter--; 
        }
    }

    //체력 0에서 모든 행동을 즉시 비활성화하고 사망 애니메이션을 최우선으로 실행
    void OnDied()
    {
        if (IsFinished)
            return;

        phase = Phase.Dying;
        pendingHits = 0;
        StopMoving();
        CancelAttacks();
        animationDriver.Die();

        if (deathEffect != null && attacksRoot != null)
        { 
            var fx = Instantiate(deathEffect, transform.position + Vector3.up * 3, Quaternion.Euler(90, 0, 0), attacksRoot);
            fx.lifetime = 3; 
        }
    }

    // 이동만 정지하고 애니메이션 속도를 0으로 전달
    // NavMesh 밖에서는 API를 호출하지 않습니다
    void StopMoving()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        { 
            agent.isStopped = true;
            agent.ResetPath();
        }
        animationDriver.SetMoveSpeed(0);
    }

    // 손 앞 충전 마법진만 제거
    // 발사된 투사체의 목표는 별도로 유지
    void ClearCharge()
    { 
        if (chargeCircle != null)
            Destroy(chargeCircle.gameObject);

        chargeCircle = null; 
    }

    // Destroy 지연 중에도 피해가 발생하지 않도록 먼저 비활성화
    void CancelAttacks()
    {
        if (attacksRoot == null) 
            return;

        foreach (Transform child in attacksRoot)
        { 
            child.gameObject.SetActive(false);
            Destroy(child.gameObject); 
        }
        chargeCircle = null;
    }

    // 씬 종료/비활성화 때 이벤트와 남은 위험물을 정리
    void OnDisable()
    {
        health.OnHealthChanged -= OnHealthChanged;
        health.OnDied -= OnDied;

        animationDriver.PrimaryReleased.RemoveListener(ReleasePrimary);
        animationDriver.AreaReleased.RemoveListener(ReleaseArea);
        animationDriver.PrimaryStarted -= BeginPrimaryCharge;

        StopMoving();
        CancelAttacks();
    }

    // 씬에 따로 둔 공격 오브젝트 루트가 다음 씬에 남지 않게 설정
    void OnDestroy()
    { 
        if (attacksRoot != null)
            Destroy(attacksRoot.gameObject);
    }
}
