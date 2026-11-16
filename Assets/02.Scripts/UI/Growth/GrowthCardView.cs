using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 공통 카드 프리팹 한 장의 표시와 버튼 입력을 담당합니다.
/// 효과 적용은 하지 않고 선택한 후보 번호만 화면 컨트롤러에 전달합니다.
/// 고정 CardSlot의 자식 CardVisual만 움직여 레이아웃이 흔들리지 않게 합니다.
/// </summary>
public class GrowthCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("카드 표시 참조")]
    [SerializeField] RectTransform visual;
    [SerializeField] CanvasGroup visualGroup;
    [SerializeField] Image icon;
    [SerializeField] TMP_Text categoryText;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text descriptionText;
    [SerializeField] TMP_Text countText;
    [SerializeField] TMP_Text buttonText;
    [SerializeField] Button selectButton;

    [Header("포인터·키보드 강조")]
    [SerializeField] float hoverScale = 1.035f;
    [SerializeField] float hoverSpeed = 15f;

    GrowthCardData card;
    Action<int> onChosen;
    int candidateIndex;
    bool pointerInside;
    bool hoverEnabled;

    public RectTransform Visual => visual;
    public CanvasGroup VisualGroup => visualGroup;
    public Button SelectButton => selectButton;
    public GrowthCardData Card => card;

    // 후보 표시를 준비할 때 호출합니다. index는 추첨 결과의 0부터 시작하는 위치입니다.
    // selectionCount는 이미 선택한 횟수이며, callback에는 사용자가 누른 후보 번호를 전달합니다.
    public void Bind(GrowthCardData data, int index, int selectionCount, Action<int> callback)
    {
        card = data;
        candidateIndex = index;
        onChosen = callback;
        pointerInside = false;

        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(HandleClick);
            selectButton.onClick.AddListener(HandleClick);
        }

        if (data == null)
        {
            SetInteractable(false);
            return;
        }

        if (icon != null)
        {
            icon.sprite = data.Icon;
            icon.enabled = data.Icon != null;
        }
        if (categoryText != null)
            categoryText.text = data.WeaponTarget == GrowthWeaponTarget.Common ? "공통 강화" : "무기 강화";
        if (titleText != null) titleText.text = data.CardName;
        if (descriptionText != null) descriptionText.text = data.Description;
        if (countText != null) countText.text = $"선택 횟수  {selectionCount} / {data.MaxSelections}";
        if (buttonText != null) buttonText.text = "강화 선택";
    }

    // 버튼의 UnityEvent에서 호출됩니다. 실제 효과 적용 권한은 상위 컨트롤러가 가집니다.
    void HandleClick()
    {
        if (card == null || selectButton == null || !selectButton.interactable)
            return;
        onChosen?.Invoke(candidateIndex);
    }

    // 등장 연출과 선택 처리 중에는 버튼을 잠그고, 연출이 끝난 후보만 선택하게 합니다.
    public void SetInteractable(bool value)
    {
        if (selectButton != null) selectButton.interactable = value && card != null;
    }

    // 등장 코루틴에서 크기와 투명도를 지정합니다. 연출 중에는 호버 계산을 중단합니다.
    public void SetReveal(float scale, float alpha)
    {
        hoverEnabled = false;
        if (visual != null) visual.localScale = Vector3.one * scale;
        if (visualGroup != null) visualGroup.alpha = alpha;
    }

    // 등장 완료 시 기준 크기로 맞춘 후 포인터와 키보드 포커스 강조를 허용합니다.
    public void FinishReveal()
    {
        if (visual != null) visual.localScale = Vector3.one;
        if (visualGroup != null) visualGroup.alpha = 1;
        hoverEnabled = true;
        SetInteractable(true);
    }

    // 일시정지 중에도 마우스와 키보드 포커스 강조가 작동하도록 비배율 시간을 사용합니다.
    void Update()
    {
        if (!hoverEnabled || visual == null || selectButton == null)
            return;
        bool focused = EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject == selectButton.gameObject;
        float target = selectButton.interactable && (pointerInside || focused) ? hoverScale : 1f;
        float blend = 1f - Mathf.Exp(-hoverSpeed * Time.unscaledDeltaTime);
        visual.localScale = Vector3.Lerp(visual.localScale, Vector3.one * target, blend);
    }

    // 자식 그래픽 위에 포인터가 들어오면 카드 전체를 강조합니다.
    public void OnPointerEnter(PointerEventData eventData) { pointerInside = true; }

    // 카드 영역을 벗어나면 기본 크기로 돌아갑니다.
    public void OnPointerExit(PointerEventData eventData) { pointerInside = false; }

    // 패널이 숨겨질 때 다음 표시로 포인터 상태가 이어지지 않게 정리합니다.
    void OnDisable()
    {
        pointerInside = false;
        hoverEnabled = false;
    }

    // 이 컴포넌트가 등록한 버튼 콜백만 해제합니다. 다른 Inspector 연결은 건드리지 않습니다.
    void OnDestroy()
    {
        if (selectButton != null) selectButton.onClick.RemoveListener(HandleClick);
    }
}
