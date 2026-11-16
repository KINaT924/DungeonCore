using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 씬에 미리 배치된 로딩 UI를 갱신하고 InGameScene을 비동기로 준비
/// 배경 선택,진행률,팁 표시만 제어하며 Canvas나 버튼을 실행 중 생성하지 않습니다
/// </summary>
public sealed class LoadingSceneController : MonoBehaviour
{
    [Header("이동할 씬")]
    [SerializeField] string targetScenePath = "Assets/01.Scenes/InGameScene.unity";
    [SerializeField] string titleScenePath = "Assets/01.Scenes/TitleScene.unity";

    [Header("Hierarchy UI 연결")]
    [SerializeField] Image backgroundImage;
    [SerializeField] Image progressFill;
    [SerializeField] TMP_Text progressText;
    [SerializeField] TMP_Text statusText;
    [SerializeField] TMP_Text tipText;
    [SerializeField] TMP_Text locationText;
    [SerializeField] Button returnTitleButton;

    [Header("배경 3종과 장면 이름 — 같은 순서로 연결")]
    [SerializeField] Sprite[] backgrounds;
    [SerializeField] string[] locationNames = { "던전의 문턱", "코어의 성소", "심연으로 향하는 길" };


    [Header("표시 시간 — 게임 시간 배율과 무관")]
    [SerializeField, Min(0f)] float minimumDisplayTime = 2.5f;
    [SerializeField, Min(0.1f)] float progressSpeed = 0.8f;
    [SerializeField, Min(1f)] float tipChangeInterval = 5f;
    [SerializeField, TextArea(1, 3)] string[] tips =
    {
        "우클릭으로 조준한 상태에서 좌클릭하면 발사합니다.",
        "탄창이 비면 자동으로 재장전합니다.",
        "레벨이 오르면 성장 카드를 선택해 능력치를 강화할 수 있습니다.",
        "코어가 활성화된 동안 몬스터의 움직임에 주의하세요."
    };

    AsyncOperation loadOperation;
    float displayedProgress;
    int tipIndex;

    // 로딩 씬이 켜지면 커서,시간을 초기화하고 배경과 첫 팁을 표시
    void Awake()
    {
        targetScenePath = SceneTransitionData.ConsumeTarget(targetScenePath);       // 전달받은 목적지를 사용

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (returnTitleButton != null)
            returnTitleButton.gameObject.SetActive(false);

        SelectBackground();
        tipIndex = tips != null && tips.Length > 0 ? UnityEngine.Random.Range(0, tips.Length) : 0;
        ShowTip();
        ShowProgress(0f);
    }

    // 첫 화면을 한 프레임 그린 뒤 인게임을 비동기로 로드
    // Unity의 0~0.9 준비 진행률을 정규화하고, 준비 / 최소 시간 / 표시 100%가 모두 충족된 뒤에만 씬 활성화를 허용해 로딩 UI가 갑자기 사라지지 않게 설정
    IEnumerator Start()
    {
        if (backgroundImage == null || progressFill == null || progressText == null || statusText == null)
        {
            ShowError("[Error] 로딩 UI 연결을 확인");
            yield break;
        }

        statusText.text = "다음 구역을 준비하는 중";
        yield return null;

        loadOperation = BeginSceneLoad();
        if (loadOperation == null)
            yield break;

        loadOperation.allowSceneActivation = false;
        float elapsed = 0f;
        float nextTipTime = Mathf.Max(1f, tipChangeInterval);

        while (true)
        {
            // timeScale이 0이 되어도 로딩 표시와 최소 대기 시간은 계속 진행
            elapsed += Time.unscaledDeltaTime;
            bool ready = loadOperation.progress >= 0.9f;
            float target = ready ? 1f : Mathf.Clamp01(loadOperation.progress / 0.9f) * 0.98f;

            displayedProgress = Mathf.MoveTowards(displayedProgress, target, Mathf.Max(0.1f, progressSpeed) * Time.unscaledDeltaTime);
            ShowProgress(displayedProgress);

            if (elapsed >= nextTipTime)
            {
                tipIndex++;
                ShowTip();
                nextTipTime = elapsed + Mathf.Max(1f, tipChangeInterval);
            }

            if (ready && elapsed >= minimumDisplayTime && displayedProgress >= 1f)
                break;

            yield return null;
        }

        // 100%는 파일 바이트 비율이 아니라 Unity의 씬 준비 완료를 뜻합니다
        statusText.text = "준비 완료, 잠시 후 진입";
        ShowProgress(1f);
        yield return new WaitForSecondsRealtime(0.15f);
        Debug.Log($"[SceneFlow] LoadingScene -> {targetScenePath} (준비 완료)", this);
        loadOperation.allowSceneActivation = true;
        while (!loadOperation.isDone)
            yield return null;
    }

    // 등록된 대상 씬을 확인하고 로드 요청을 반환
    // 반환값이 null이면 요청 실패이며, 오류 UI에서 타이틀로 돌아갈 수 있습니다.
    AsyncOperation BeginSceneLoad()
    {
        if (targetScenePath == gameObject.scene.path || !Application.CanStreamedLevelBeLoaded(targetScenePath))
        {
            ShowError("이동할 씬이 올바르게 등록되어 있지 않습니다");
            return null;
        }

        try
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(targetScenePath, LoadSceneMode.Single);
            if (operation == null)
                ShowError("대상 씬 로드를 시작하지 못했습니다");
            return operation;
        }
        catch (Exception exception)
        {
            ShowError($"대상 씬의 로드 실패: {exception.Message}");
            return null;
        }
    }

    // 진입할 때마다 연결된 세 배경 중 하나를 선택하고 대응하는 장소 이름을 표시
    void SelectBackground()
    {
        if (backgroundImage == null || backgrounds == null || backgrounds.Length == 0)
            return;

        int index = UnityEngine.Random.Range(0, backgrounds.Length);
        if (backgrounds[index] != null)
            backgroundImage.sprite = backgrounds[index];
        if (locationText != null && locationNames != null && index < locationNames.Length)
            locationText.text = locationNames[index];
    }

    // 0~1 범위의 표시 진행률을 채움 이미지와 백분율 텍스트에 함께 반영
    void ShowProgress(float value)
    {
        value = Mathf.Clamp01(value);

        if (progressFill != null)
            progressFill.fillAmount = value;
        if (progressText != null)
            progressText.text = $"{Mathf.FloorToInt(value * 100f)}%";
    }

    // 첫 진입과 긴 로딩 중 일정 간격에 호출하여 다음 조작 팁을 보여줍니다
    void ShowTip()
    {
        if (tipText != null && tips != null && tips.Length > 0)
            tipText.text = tips[tipIndex % tips.Length];
    }

    // 로드 요청 실패 원인을 화면과 Console에 남기고 타이틀 복귀 버튼을 표시
    void ShowError(string message)
    {
        if (statusText != null)
            statusText.text = "불러오지 못했습니다";
        if (tipText != null)
            tipText.text = message;
        if (returnTitleButton != null)
            returnTitleButton.gameObject.SetActive(true);
        Debug.LogError($"[SceneFlow] {message}", this);
    }

    // 오류 시 표시되는 버튼에서 호출하여 타이틀 씬으로 복귀
    public void ReturnToTitle()
    {
        if (Application.CanStreamedLevelBeLoaded(titleScenePath))
            SceneManager.LoadSceneAsync(titleScenePath, LoadSceneMode.Single);
        else
            ShowError("타이틀 씬이 Build Profiles에 등록되어 있지 않습니다.");
    }
}
