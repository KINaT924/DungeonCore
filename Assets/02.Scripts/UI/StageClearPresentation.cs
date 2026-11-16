using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 보스 사망 순간의 짧은 슬로 모션, 사망 모션 대기, 암전/클리어 패널/UI 입력 복귀를 순서대로 처리합니다
/// </summary>
/// 
public class StageClearPresentation : MonoBehaviour
{
    [Header("프리팹 내부 연결")]
    public GameObject visualRoot;
    public CanvasGroup shade;
    public CanvasGroup result;
    public RectTransform resultCard;
    public TMP_Text title;
    public TMP_Text battleTimeText;
    public Button retryButton;
    public Button titleButton;
    public AudioSource audioSource;
    public AudioClip clearSting;
    public GameObject bossStatus;

    [Header("연출 시간")]
    public float slowMotionSeconds = .9f;
    public float revealSeconds = 1.1f;
    GameUISpawner context;
    Health boss;
    bool presenting, ownsTime, changingScene;
    float previousTime;
    // Hierarchy에서 편집한 최종 위치와 크기를 등장 연출의 기준으로 보관합니다.
    Vector2 designedPosition;
    Vector3 designedScale;
    readonly System.Collections.Generic.List<Behaviour> locked = new System.Collections.Generic.List<Behaviour>();

    // 공용 HUD 생성 직후 보스전의 체력 이벤트만 연결할 준비
    public void Bind(GameUISpawner source)
    {
        context = source; boss = source.bossHealth;
        enabled = source.mode == GameUISpawner.BattleMode.Boss && boss != null;
    }

    // 비활성 패널을 초기화하되 이벤트 수신용 컴포넌트는 HUD 루트에 남겨둠
    void Awake()
    {
        designedPosition = resultCard.anchoredPosition;
        designedScale = resultCard.localScale;
        visualRoot.SetActive(false);
        shade.alpha = result.alpha = 0;
        // 평소 비상호작용 HUD의 CanvasGroup 설정과 독립적으로 결과 버튼을 받습니다
        result.ignoreParentGroups = true;
        retryButton.onClick.AddListener(Retry); titleButton.onClick.AddListener(GoToTitle);
    }

    // 보스전 HUD가 활성화되어 있을 때만 사망을 구독
    void OnEnable() 
    { 
        if (boss != null) 
            boss.OnDied += OnBossDied; 
    }

    // 동시에 플레이어가 이미 사망했다면 클리어를 표시하지 않습니다
    // 사망 알림 중복도 차단합니다
    void OnBossDied()
    {
        if (presenting || context == null || context.player.GetComponent<Health>().IsDead)
            return;
        if (!context.TryFinishBattle(GameUISpawner.BattleResult.Cleared))
            return;

        // 사망 연출의 슬로 모션과 패널 대기 시간은 보스전 기록에 포함하지 않습니다.
        var controller = boss.GetComponent<BossQueenController>();
        int seconds = Mathf.FloorToInt(controller != null ? controller.CombatElapsedSeconds : 0);
        if (battleTimeText != null) 
            battleTimeText.text = $"보스전 소요 시간  {seconds / 60:00}:{seconds % 60:00}";
        presenting = true; 
        StartCoroutine(Present());
    }

    // 전투 입력을 잠근 뒤 사망 모션을 보여주고, 보라빛 암전과 금빛 제목을 천천히 드러냅니다
    IEnumerator Present()
    {
        if (context.CardsInstance != null)
            context.CardsInstance.enabled = false;
        LockPlayer();

        previousTime = Time.timeScale > 0 ? Time.timeScale : 1;
        ownsTime = true;
        Time.timeScale = .35f;
        visualRoot.SetActive(true);
        result.interactable = result.blocksRaycasts = false;

        float elapsed = 0;
        while (elapsed < slowMotionSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            shade.alpha = Mathf.Lerp(0, .2f, elapsed / slowMotionSeconds);
            yield return null;
        }

        Time.timeScale = previousTime;
        var animation = boss.GetComponent<BossQueenAnimationDriver>();
        // 실제 사망 클립 끝을 기다리므로 클립 길이를 수정해도 패널이 모션을 덮지 않습니다
        while (animation != null)
        {
            var state = animation.Animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("Death") && state.normalizedTime >= .99f)
                break;
            yield return null;
        }
        yield return new WaitForSecondsRealtime(.35f);

        if (bossStatus != null)
            bossStatus.SetActive(false);
        if (audioSource != null && clearSting != null)
            audioSource.PlayOneShot(clearSting);

        elapsed = 0;
        while (elapsed < revealSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / revealSeconds);
            shade.alpha = Mathf.Lerp(.2f, .82f, t);
            result.alpha = t;
            resultCard.anchoredPosition = designedPosition + new Vector2(0, Mathf.Lerp(-26, 0, t));
            resultCard.localScale = designedScale * Mathf.Lerp(.94f, 1, t);
            yield return null;
        }
        shade.alpha = .82f; 
        result.alpha = 1;
        resultCard.localScale = designedScale;
        resultCard.anchoredPosition = designedPosition;

        Time.timeScale = 0;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        result.interactable = result.blocksRaycasts = true;
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(retryButton.gameObject);
    }

    // 전투 동작과 시점만 끄고 EventSystem의 UI 액션은 유지
    void LockPlayer()
    {
        foreach (var behaviour in context.player.GetComponents<Behaviour>())
        {
            if (!(behaviour is PlayerInputReader || behaviour is PlayerMoveController || behaviour is PlayerGunController ||
                  behaviour is PlayerDodgeController || behaviour is PlayerCameraController || behaviour is GunnerAimController ||
                  behaviour is GunnerWeaponSkillController))
                continue;

            if (behaviour.enabled)
            {
                locked.Add(behaviour);
                behaviour.enabled = false; 
            }
        }
    }

    // 기존 버튼 연결을 유지하며 추가 도전을 시작
    // 체력/공격력만 10% 누적 상승하고 일반전부터 새로 시작합니다
    public void Retry()
    {
        if (!presenting || changingScene) 
            return;

        // 등록 오류 시 난이도만 올라가고 패널에 남는 일을 방지
        if (!Application.CanStreamedLevelBeLoaded("InGameScene"))
        {
            Debug.LogError("[추가 도전] InGameScene을 Build Profiles에 등록하세요.", this);
            return;
        }
        changingScene = true;
        ChallengeRunState.Advance();
        RestoreTime();
        SceneManager.LoadScene("InGameScene");
    }

    // 정지 시간을 복구하고 타이틀 씬으로 돌아갑니다
    public void GoToTitle()
    { 
        if (!presenting || changingScene)
            return;
        changingScene = true;
        RestoreTime(); 
        SceneManager.LoadScene("TitleScene");
    }

    // 씬 전환 이후 시간 0이 남지 않게 이 연출이 소유한 정지만 해제
    void RestoreTime()
    { 
        if (ownsTime)
        {
            Time.timeScale = previousTime > 0 ? previousTime : 1;
            ownsTime = false;
        } 
    }

    // 씬 종료나 HUD 해제 시 이벤트와 연출 소유 상태를 정리
    void OnDisable()
    {
        if (boss != null)
            boss.OnDied -= OnBossDied;
        StopAllCoroutines();
        RestoreTime();

        foreach (var behaviour in locked) if (behaviour != null)
                behaviour.enabled = true;
        locked.Clear();
    }
}
