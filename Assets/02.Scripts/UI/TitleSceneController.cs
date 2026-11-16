using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 타이틀의 게임 시작 버튼을 로딩 씬에 연결합니다
/// UI는 씬에 저장된 오브젝트를 사용하며 이 컴포넌트가 새로 생성하지 않습니다
/// 웹 빌드에서는 종료가 의미 없으므로 종료 버튼을 숨깁니다
/// </summary>
///
public sealed class TitleSceneController : MonoBehaviour
{
    [Header("씬 연결")]
    [SerializeField] string loadingScenePath = "Assets/01.Scenes/LoadingScene.unity";
    [SerializeField] Button startButton;

    [Header("종료")]
    [SerializeField] Button quitButton;     // 메인 메뉴의 종료 버튼. 웹 빌드에서는 숨깁니다

    // 같은 프레임에 버튼을 여러 번 눌러도 씬 로드는 한 번만 요청
    bool isStarting;

    // 타이틀 진입 시 이전 게임의 일시정지를 풀고 메뉴용 커서를 표시
    void Awake()
    {
        PlayerRunState.Clear();

        // 타이틀에서 시작하는 새 게임은 추가 도전의 난이도를 이어받지 않습니다
        ChallengeRunState.Reset();

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 브라우저에서는 Application.Quit이 탭을 닫지 못하므로 버튼 자체를 감춥니다
#if UNITY_WEBGL && !UNITY_EDITOR
        if (quitButton != null)
            quitButton.gameObject.SetActive(false);
#endif
    }

    // StartButton의 Inspector OnClick에서 호출
    // 로딩 씬 등록을 먼저 확인하고, 중복 입력을 차단한 뒤 비동기로 이동시킵니다
    public void StartGame()
    {
        if (isStarting)
            return;

        if (!Application.CanStreamedLevelBeLoaded(loadingScenePath))
        {
            Debug.LogError($"[SceneFlow] 로딩 씬이 Build Profiles에 없습니다: {loadingScenePath}", this);
            return;
        }

        isStarting = true;
        if (startButton != null)
            startButton.interactable = false;

        try
        {
            SceneTransitionData.SetTarget("Assets/01.Scenes/InGameScene.unity");
            Debug.Log("[SceneFlow] TitleScene -> LoadingScene", this);
            AsyncOperation operation = SceneManager.LoadSceneAsync(loadingScenePath, LoadSceneMode.Single);

            if (operation == null)
                RestoreStartButton();
        }
        catch (Exception exception)
        {
            // 잘못된 경로 등으로 요청 자체가 실패하면 타이틀에서 다시 시도할 수 있게 합니다
            RestoreStartButton();
            Debug.LogError($"[SceneFlow] 로딩 씬 이동 실패: {exception.Message}", this);
        }
    }

    // ConfirmQuitButton(종료 확인)의 Inspector OnClick에서 호출
    // 에디터에서는 Play를 멈추고, PC 빌드에서는 게임을 종료합니다
    public void QuitGame()
    {
        Debug.Log("[SceneFlow] 게임 종료 요청", this);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // 씬 전환 요청이 실패했을 때만 시작 입력을 다시 허용합니다
    void RestoreStartButton()
    {
        isStarting = false;
        if (startButton != null)
            startButton.interactable = true;
    }
}
