using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

/// <summary>
/// 선택 시간이 오면 선택 컨트롤러의 후보를 공통 카드 프리팹 3장에 표시
/// 선택 중 전투 입력, 카메라, 시간을 잠그고, 모든 대기 선택이 끝나면 게임 진행
/// 이 컴포넌트는 꺼지는 패널 자식이 아니라 항상 활성화되는 Canvas 루트에 두고 관리
/// </summary>
public class GrowthCardSelectionUI : MonoBehaviour
{
    [Header("선택 흐름")]
    [SerializeField] GrowthCardSelectionController selectionController;
    [SerializeField] bool autoBindSinglePlayer = true;
    [SerializeField] bool pauseDuringSelection = true;

    [Header("프리팹 내부 연결")]
    [SerializeField] GameObject panel;
    [SerializeField] GrowthCardView[] cardViews;
    [SerializeField] TMP_Text pendingText;

    [Header("비배율 시간으로 실행할 등장 연출")]
    [SerializeField, Min(.05f)] float revealDuration = .36f;
    [SerializeField, Min(0)] float staggerDelay = .08f;

    PlayerGrowth growth;
    Health health;
    PlayerInput playerInput;
    PlayerCameraController cameraController;
    readonly List<InputAction> enabledGameplayActions = new List<InputAction>();
    Coroutine revealRoutine;
    bool subscribed, ownsLock, didPause, previousCameraEnabled, choosing;
    float previousTimeScale;
    CursorLockMode previousCursorLock;
    bool previousCursorVisible;
    GameObject previousSelection;
    GameObject ownedEventSystem;

    public bool IsPresenting => panel != null && panel.activeSelf;
    public IReadOnlyList<GrowthCardView> CardViews => cardViews;

    // 실제 선택이 생기기 전에는 화면을 가립니다
    void Awake() { if (panel != null) panel.SetActive(false); }

    // Inspector로 연결한 컨트롤러가 있다면 즉시 구독합니다
    void OnEnable() { if (selectionController != null) Bind(selectionController); }

    // 씬 참조를 저장할 수 없는 프리팹을 위해 단일 플레이어 씬에서만 자동 연결합니다
    // 컨트롤러가 여러 개라면 임의로 고르지 않고 사용자의 명시적 연결을 기다립니다
    void Start()
    {
        if (selectionController != null) { Bind(selectionController); return; }
        if (!autoBindSinglePlayer) return;
        var controllers = FindObjectsByType<GrowthCardSelectionController>(FindObjectsSortMode.None);
        if (controllers.Length == 1) Bind(controllers[0]);
        else Debug.LogWarning("[성장 UI] 플레이어의 Selection Controller를 직접 연결하세요.", this);
    }

    // 씬에서 사용할 선택 컨트롤러를 연결합니다. 기존 구독과 잠금은 먼저 정리합니다.
    public void Bind(GrowthCardSelectionController controller)
    {
        if (subscribed && selectionController == controller) { Refresh(); return; }
        Unsubscribe();
        Close();
        selectionController = controller;
        if (controller == null) return;
        growth = controller.GetComponent<PlayerGrowth>();
        health = controller.GetComponent<Health>();
        playerInput = controller.GetComponent<PlayerInput>();
        cameraController = controller.GetComponent<PlayerCameraController>();
        controller.OnSelectionChanged += Refresh;
        subscribed = true;
        Refresh();
    }

    // 이벤트 연결을 해제하여 재활성화 시 중복 표시나 중복 적용이 생기지 않게 합니다.
    void Unsubscribe()
    {
        if (subscribed && selectionController != null)
            selectionController.OnSelectionChanged -= Refresh;
        subscribed = false;
    }

    // 후보가 준비되면 표시합니다. 다음 선택이 대기 중이면 전투 잠금을 유지합니다.
    void Refresh()
    {
        if (selectionController == null || !selectionController.isActiveAndEnabled ||
            (health != null && health.IsDead)) { Close(); return; }
        if (!selectionController.IsSelecting)
        {
            StopReveal();
            LockButtons();
            if (selectionController.PendingSelections == 0) Close();
            return;
        }
        if (panel == null || cardViews == null || cardViews.Length == 0)
        {
            Debug.LogError("[성장 UI] Panel과 Card Views 연결을 확인하세요.", this);
            return;
        }
        EnsureEventSystem();
        AcquireLock();
        StopReveal();
        panel.SetActive(true);
        if (pendingText != null)
            pendingText.text = $"남은 선택  {selectionController.PendingSelections + 1}회";
        int count = Mathf.Min(cardViews.Length, selectionController.CurrentCards.Count);
        for (int i = 0; i < cardViews.Length; i++)
        {
            GrowthCardView view = cardViews[i];
            if (view == null) continue;
            view.gameObject.SetActive(i < count);
            if (i >= count) continue;
            var card = selectionController.CurrentCards[i];
            view.Bind(card, i, growth != null ? growth.GetSelectionCount(card.CardId) : 0, Choose);
            view.SetInteractable(false);
            view.SetReveal(.85f, 0);
        }
        Canvas.ForceUpdateCanvases();
        revealRoutine = StartCoroutine(Reveal(count));
    }

    // 후보 소진으로 컨트롤러가 다음 화면을 열지 않는 경우도 잠금을 정상 해제합니다.
    void LateUpdate()
    {
        if (!ownsLock) return;
        if (selectionController == null || !selectionController.isActiveAndEnabled ||
            (health != null && health.IsDead) ||
            (!selectionController.IsSelecting && selectionController.PendingSelections == 0)) Close();
    }

    // 카드가 전달한 후보 번호만 기존 선택 컨트롤러에 넘깁니다. UI가 효과를 직접 적용하지 않습니다.
    void Choose(int index)
    {
        if (choosing || revealRoutine != null || selectionController == null) return;
        choosing = true;
        LockButtons();
        bool applied = selectionController.TrySelectCard(index);
        choosing = false;
        if (!applied && selectionController != null && selectionController.IsSelecting)
            foreach (var view in cardViews) if (view != null && view.gameObject.activeSelf) view.SetInteractable(true);
    }

    // 0.85 → 1.04 → 1 크기와 투명도를 카드별 지연으로 연출합니다.
    // 선택 중 Time.timeScale이 0이어도 움직이도록 unscaledTime을 사용합니다.
    IEnumerator Reveal(int count)
    {
        float start = Time.unscaledTime;
        float duration = Mathf.Max(.05f, revealDuration);
        float delay = Mathf.Max(0, staggerDelay);
        while (Time.unscaledTime - start < duration + Mathf.Max(0, count - 1) * delay)
        {
            for (int i = 0; i < count; i++)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - start - i * delay) / duration);
                float scale = t < .7f
                    ? Mathf.Lerp(.85f, 1.04f, 1f - Mathf.Pow(1f - t / .7f, 3))
                    : Mathf.Lerp(1.04f, 1f, Mathf.SmoothStep(0, 1, (t - .7f) / .3f));
                cardViews[i].SetReveal(scale, Mathf.Clamp01(t / .35f));
            }
            yield return null;
        }
        for (int i = 0; i < count; i++) cardViews[i].FinishReveal();
        revealRoutine = null;
        if (count > 0 && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(cardViews[0].SelectButton.gameObject);
    }

    // 중단한 등장 연출이 이전 카드의 크기나 버튼 상태를 다시 덮어쓰지 않게 합니다.
    void StopReveal()
    {
        if (revealRoutine != null) StopCoroutine(revealRoutine);
        revealRoutine = null;
    }

    // 선택 확정 또는 화면 교체 중에는 모든 버튼을 잠급니다.
    void LockButtons()
    {
        if (cardViews == null) return;
        foreach (var view in cardViews) if (view != null) view.SetInteractable(false);
    }

    // 기존 EventSystem이 있으면 그대로 사용하고, 없는 씬에만 UI 전용 입력 모듈을 만듭니다.
    void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        ownedEventSystem = new GameObject("GrowthCardUI_EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        ownedEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    // 시간과 커서, 카메라 및 현재 게임 액션의 활성 상태를 최초 한 번만 보관합니다.
    // UI 모듈의 별도 액션은 끄지 않아 마우스 클릭과 키보드 이동은 계속 작동합니다.
    void AcquireLock()
    {
        if (ownsLock) return;
        ownsLock = true;
        previousTimeScale = Time.timeScale;
        didPause = pauseDuringSelection;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        enabledGameplayActions.Clear();
        if (playerInput != null && playerInput.currentActionMap != null)
        {
            var map = playerInput.currentActionMap;
            foreach (var action in map.actions) if (action.enabled) enabledGameplayActions.Add(action);
            map.Disable();
        }
        if (cameraController != null)
        {
            previousCameraEnabled = cameraController.enabled;
            cameraController.enabled = false;
        }
        if (didPause) Time.timeScale = 0;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // 이 UI가 잠근 상태만 이전 값으로 돌립니다. 기존 일시정지 상태도 1로 강제 변경하지 않습니다.
    void ReleaseLock()
    {
        if (!ownsLock) return;
        ownsLock = false;
        if (didPause) Time.timeScale = previousTimeScale;
        foreach (var action in enabledGameplayActions) action.Enable();
        enabledGameplayActions.Clear();
        if (cameraController != null) cameraController.enabled = previousCameraEnabled;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
    }

    // 화면을 닫으며 코루틴과 입력 잠금을 정리합니다. Canvas 루트는 계속 활성화해 둡니다.
    void Close()
    {
        StopReveal();
        LockButtons();
        if (panel != null) panel.SetActive(false);
        ReleaseLock();
    }

    // 오브젝트 비활성화 또는 씬 전환 시 시간이 멈춘 채 남지 않도록 복구합니다.
    void OnDisable() { Unsubscribe(); Close(); }

    // 이 UI가 임시로 만든 EventSystem만 제거합니다. 기존 씬의 입력 시스템은 유지합니다.
    void OnDestroy()
    {
        if (ownedEventSystem != null) Destroy(ownedEventSystem);
    }
}
