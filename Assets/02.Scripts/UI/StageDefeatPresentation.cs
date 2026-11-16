using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 공용 패배 프리팹의 표시와 사망 이벤트를 연결하는 스크립트
/// UI 배치는 프리팹 Hierarchy에 저장한 후
/// 사망 모션 대기→암전/패널 등장→전투 정지→새 게임 또는 타이틀 복귀를 담당합니다
/// </summary>
public sealed class StageDefeatPresentation : MonoBehaviour
{
    [Header("프리팹 Hierarchy 연결")]
    public GameObject visualRoot;
    public CanvasGroup shade;
    public CanvasGroup result;
    public RectTransform resultCard;
    public TMP_Text stageText;
    public TMP_Text levelText;
    public TMP_Text timeText;
    public TMP_Text messageText;
    public Button retryButton;
    public Button titleButton;

    [Header("사망 후 연출 — 비배율 시간")]
    [Min(0f)] public float deathMotionSeconds = 2f;
    [Min(.05f)] public float revealSeconds = .8f;

    [Header("씬 이동")]
    [SerializeField] string loadingScene = "Assets/01.Scenes/LoadingScene.unity";
    [SerializeField] string gameScene = "Assets/01.Scenes/InGameScene.unity";
    [SerializeField] string titleScene = "Assets/01.Scenes/TitleScene.unity";

    GameUISpawner context;
    Health playerHealth;
    bool presenting, changingScene, ownsLock;
    float previousTimeScale;
    CursorLockMode previousCursorLock;
    bool previousCursorVisible;
    Vector2 designedPosition;
    Vector3 designedScale;
    // 이 패널이 잠근 동작과 액션만 해제하여 다른 컴포넌트의 비활성 상태를 덮어쓰지 않습니다.
    readonly List<Behaviour> lockedBehaviours = new List<Behaviour>();
    readonly List<InputAction> lockedActions = new List<InputAction>();

    public bool IsPresenting => presenting;

    // 비활성 프리팹 생성 직후 호출하여 해당 씬의 실제 플레이어를 연결
    public void Bind(GameUISpawner source)
    {
        context = source;
        playerHealth = source.player.GetComponent<Health>();
    }

    // 편집한 패널 위치를 보관하고 표시만 숨깁니다
    // 버튼은 실행 시 한 번 연결합니다
    void Awake()
    {
        designedPosition = resultCard.anchoredPosition;
        designedScale = resultCard.localScale;
        visualRoot.SetActive(false);
        shade.alpha = result.alpha = 0f;
        result.interactable = result.blocksRaycasts = false;
        retryButton.onClick.AddListener(Retry);
        titleButton.onClick.AddListener(ReturnToTitle);
    }

    // 전투 씬이 활성화될 때 플레이어 사망을 구독
    void OnEnable()
    {
        if (playerHealth != null) 
            playerHealth.OnDied += OnPlayerDied;
    }

    // HUD가 뒤늦게 생성되어 사망 이벤트를 놓친 경우에도 현재 상태를 확인
    void Start()
    {
        if (playerHealth != null && playerHealth.IsDead)
            OnPlayerDied();
    }

    // 사망 시 전투 결과를 한 번 확정하고, 종료 시점의 기록을 표시한 뒤 연출을 시작
    void OnPlayerDied()
    {
        if (presenting || context == null ||
            !context.TryFinishBattle(GameUISpawner.BattleResult.Defeated))
            return;
        presenting = true;

        bool bossBattle = context.mode == GameUISpawner.BattleMode.Boss;
        stageText.text = bossBattle ? $"BOSS BATTLE  /  {context.bossName}" : "DUNGEON  /  일반전";
        
        var level = context.player.GetComponent<PlayerLevel>();
        levelText.text = $"Lv. {(level != null ? level.CurrentLevel : 1)}";
        
        var boss = context.bossHealth != null ? context.bossHealth.GetComponent<BossQueenController>() : null;
        float elapsed = bossBattle ? (boss != null ? boss.CombatElapsedSeconds : 0f)
            : (context.run != null ? context.run.ElapsedSeconds : 0f);
        
        int seconds = Mathf.FloorToInt(elapsed);
        timeText.text = $"{seconds / 60:00}:{seconds % 60:00}";

        // 카드/포탈의 기존 일시정지 소유권을 먼저 정리한 뒤 패배 패널이 입력과 시간을 관리합니다.
        if (context.CardsInstance != null)
            context.CardsInstance.enabled = false;
        if (context.portalPresentation != null)
            context.portalPresentation.enabled = false;

        AcquireLock();
        StartCoroutine(Present());
    }

    // 게임 액션과 조작만 잠급니다
    // Animator와 별도의 UI 액션은 유지해 사망 모션/버튼이 작동
    void AcquireLock()
    {
        if (ownsLock)
            return;
        ownsLock = true;

        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;

        var playerInput = context.player.GetComponent<PlayerInput>();
        if (playerInput != null && playerInput.currentActionMap != null)
        {
            foreach (var action in playerInput.currentActionMap.actions)
                if (action.enabled)
                    lockedActions.Add(action);
            playerInput.currentActionMap.Disable();
        }

        foreach (var behaviour in context.player.GetComponents<Behaviour>())
        {
            if (!(behaviour is PlayerInputReader || behaviour is PlayerMoveController ||
                behaviour is PlayerGunController || behaviour is PlayerDodgeController ||
                behaviour is PlayerCameraController || behaviour is GunnerAimController ||
                behaviour is GunnerWeaponSkillController || behaviour is PlayerCombat ||
                behaviour is PlayerSkillController)) continue;
            if (behaviour.enabled)
            { 
                lockedBehaviours.Add(behaviour);
                behaviour.enabled = false;
            }
        }

        Time.timeScale = previousTimeScale;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // 사망 모션을 보여준 뒤 어두운 배경과 패널을 나타냅니다
    // 정지 후에도 비배율 시간으로 연출합니다
    IEnumerator Present()
    {
        visualRoot.SetActive(true);

        float elapsed = 0f;
        while (elapsed < deathMotionSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            shade.alpha = Mathf.Lerp(0f, .35f, Mathf.Clamp01(elapsed / Mathf.Max(.01f, deathMotionSeconds)));
            yield return null;
        }
        Time.timeScale = 0f;
        elapsed = 0f;

        float duration = Mathf.Max(.05f, revealSeconds);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            shade.alpha = Mathf.Lerp(.35f, .86f, t);
            result.alpha = t;
            resultCard.anchoredPosition = designedPosition + new Vector2(0f, Mathf.Lerp(-22f, 0f, t));
            resultCard.localScale = designedScale * Mathf.Lerp(.97f, 1f, t);
            yield return null;
        }

        shade.alpha = .86f;
        result.alpha = 1f;
        resultCard.anchoredPosition = designedPosition;
        resultCard.localScale = designedScale;
        result.interactable = result.blocksRaycasts = true;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(retryButton.gameObject);
    }

    // 재시작 버튼에서 호출하여 로딩을 거쳐 새 일반전을 시작합니다
    // 성장과 추가 도전 배율도 초기화합니다
    public void Retry()
    {
        if (!presenting || changingScene || !result.interactable)
            return;
        if (!CanLoad(loadingScene) || !CanLoad(gameScene))
            return;

        ChangeScene(loadingScene, true);
    }

    // 타이틀 버튼에서 호출하여 현재 도전을 종료하고 메뉴로 복귀
    public void ReturnToTitle()
    {
        if (!presenting || changingScene || !result.interactable || !CanLoad(titleScene))
            return;
        ChangeScene(titleScene, false);
    }

    // 이동 전에 등록 여부를 확인
    // 실패하면 패널에 남아 다른 버튼으로 복귀할 수 있습니다
    bool CanLoad(string path)
    {
        if (Application.CanStreamedLevelBeLoaded(path))
            return true;

        messageText.text = "이동할 씬을 불러올 수 없습니다. 씬 등록을 확인해 주세요.";
        Debug.LogError($"[패배 UI] Build Profiles에 등록되지 않은 씬: {path}", this);
        return false;
    }

    // 중복 클릭을 차단하고 시간을 정상화한 뒤 이동
    // 새 일반전 요청일 때만 로딩 목적지를 지정
    void ChangeScene(string path, bool throughLoading)
    {
        changingScene = true;
        result.interactable = false;

        PlayerRunState.Clear();
        ChallengeRunState.Reset();

        if (throughLoading)
            SceneTransitionData.SetTarget(gameScene);

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        try
        {
            if (SceneManager.LoadSceneAsync(path, LoadSceneMode.Single) != null)
                return;
        }
        catch (System.Exception error)
        {
            Debug.LogException(error, this);
        }
        changingScene = false;
        Time.timeScale = 0f;
        result.interactable = true;
        messageText.text = "이동하지 못했습니다. 버튼을 다시 선택해 주세요.";
    }

    // 씬 종료 또는 UI 비활성화 시 구독과 자신이 소유한 잠금만 정리합니다
    // 시간 0을 다음 씬에 남기지 않습니다
    void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnDied -= OnPlayerDied;
        StopAllCoroutines();

        if (!ownsLock) 
            return;
        ownsLock = false;

        Time.timeScale = changingScene ? 1f : previousTimeScale;

        // 씬을 떠나거나 죽은 플레이어를 정리할 때에는 파괴 중인 카메라/입력을 다시 켜지 않습니다
        // 같은 씬에서 실제로 살아 있는 플레이어의 UI만 해제할 때에만 기존 동작을 복구합니다
        bool restoreGameplay = !changingScene && Application.isPlaying && playerHealth != null && !playerHealth.IsDead;
        if (restoreGameplay)
            foreach (var action in lockedActions)
                action.Enable();
        lockedActions.Clear();

        if (restoreGameplay)
            foreach (var behaviour in lockedBehaviours)
                if (behaviour != null)
                    behaviour.enabled = true;
        lockedBehaviours.Clear();

        if (!changingScene)
        {
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }
    }
}
