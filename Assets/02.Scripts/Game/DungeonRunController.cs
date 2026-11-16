using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 1라운드의 제한시간과 보스전으로 가는 포탈을 준비하는 스크립트
/// </summary>

[DefaultExecutionOrder(500)]
public class DungeonRunController : MonoBehaviour
{
    public enum RunPhase
    {
        Survival,
        PortalDelay,
        AwaitingPortalPresentation,
        Failed,
        PortalReady,
        PortalEntered
    }

    [Header("레퍼런스")]
    [SerializeField] Health playerHealth;
    [SerializeField] EnemySpawner enemySpawner;
    [SerializeField] CoreManager coreManager;

    [Header("진행 시간")]
    [SerializeField, Min(1f)] float survivalDuration = 100f;
    [SerializeField, Min(0f)] float portalDelay = 3f;
    [SerializeField] RunPhase phase = RunPhase.Survival;

    [Header("보스전 관련")]
    [SerializeField] string bossSceneName = "BossScene";                // 보스씬으로 이동하기 위한 참조
    [SerializeField] string loadingSceneName = "LoadingScene";       // 보스씬을 주비하는 동안 표시할 공동 로딩씬

    float elapsedSeconds;
    float delayRemaining;

    public RunPhase Phase => phase;
    public float ElapsedSeconds => elapsedSeconds;
    public float RemainingSeconds => Mathf.Max(0f, survivalDuration - elapsedSeconds);

    // 전투 정리 후의 대기시간이 종료됬을 시점에 발생하는 이벤트
    public event Action OnPortalRequested;

    private void Start()
    {
        if(playerHealth == null || enemySpawner ==null ||  coreManager == null)
        {
            Debug.LogError("던전런컨트롤러으 참조연결을 확인", this);
            enabled = false;
            return;
        }

        PlayerRunState.Clear();
        elapsedSeconds = 0f;
        delayRemaining = 0f;
        phase = RunPhase.Survival;
    }

    // 1라운드 전투 중에는 제한시간을 계산하고 종료 후엔 포탈 대기시간을 계산
    // 일시정지 중에는 시간이 흐르지 않도록 설정
    private void Update()
    {
        if (playerHealth == null)
            return;
        if(playerHealth.IsDead)
        {
            phase = RunPhase.Failed;
            return;
        }
        if (Time.timeScale <= 0f)
            return;

        switch (phase)
        {
            case RunPhase.Survival:
                elapsedSeconds = Mathf.Min(survivalDuration, elapsedSeconds + Time.deltaTime);
                if (elapsedSeconds >= survivalDuration)
                    FinishSurvival();
                break;
            case RunPhase.PortalDelay:
                delayRemaining = Mathf.Max(0f, delayRemaining - Time.deltaTime);
                if (delayRemaining <= 0f)
                    RequestPortal();
                break;
        }
    }

    // 제한시간이 끝나면 전투 정리를 시작하며 몬스터와 코어를 보상없이 비활성화
    void FinishSurvival()
    {
        if (phase != RunPhase.Survival)
            return;

        phase = RunPhase.PortalDelay;
        delayRemaining = portalDelay;

        coreManager.StopForBossTransition();
        enemySpawner.StopForBossTransition();

        Debug.Log("1라운드 전투 종료. 모든 몬스터, 코어 비활성화 시작 및 포탈 등장 대기 시간 시작", this);
    }

    // 전투 정리 후 대기 시간이 지나면 포탈 생성 요청을 한번 실행
    void RequestPortal()
    {
        if (phase != RunPhase.PortalDelay)
            return;

        phase = RunPhase.AwaitingPortalPresentation;

        Debug.Log("포탈 등장", this);

        OnPortalRequested?.Invoke();
    }

    // 포탈의 등장 연출을 마치고 플레이어가 포탈에 진입할 수 있도록 됬을 시점에 호출
    public void NotifyPortalReady()
    {
        if (phase != RunPhase.AwaitingPortalPresentation)
            return;

        phase = RunPhase.PortalReady;
    }

    // 포탈에 플레이어가 진입 했을 시 호출
    // 등록된 씬을 확인하고 상태를 변경해 중복 이동되는것을 방지
    public void NotifyPortaEntered()
    {
        // 준비되지 않았으면 진입요청 무시
        if (phase != RunPhase.PortalReady)
            return;
        // 플레이어가 사망하면 보스전으로 이동하면 안 됨
        if (playerHealth == null || playerHealth.IsDead)
            return;

        // 등록된 씬 이름을 이동 전에 확인
        if(!Application.CanStreamedLevelBeLoaded(bossSceneName))
        {
            Debug.LogError($"보스 씬을 불러오는 데 실패, 씬 이름과 설정을 확인 : {bossSceneName}", this);
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(loadingSceneName))
        {
            Debug.LogError($"로딩 씬을 불러오는 데 실패, 씬 이름과 설정을 확인 : {loadingSceneName}", this);
            return;
        }

        // 저장에 실패했을 경우 현재 씬에 남겨둬 성장 데이터 유실하는것을 방지
        if (!PlayerRunState.TrySave(playerHealth))
            return;

        phase = RunPhase.PortalEntered;
        SceneTransitionData.SetTarget(bossSceneName);
        Debug.Log($"[SceneFow] 포탈 -> {loadingSceneName} -> {bossSceneName}", this);

        // 로딩씬 진입
        SceneManager.LoadScene(loadingSceneName, LoadSceneMode.Single);
    }
}
