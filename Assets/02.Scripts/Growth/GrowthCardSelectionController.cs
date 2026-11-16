using System;
using System.Collections.Generic;
using UnityEngine;


public class GrowthCardSelectionController : MonoBehaviour
{
    /// <summary>
    /// 레벨업으로 얻은 카드 선택 기회를 누적하고 순서대로 처리합니다.
    /// 카드 추첨과 효과 적용은 각각의 담당 컴포넌트에 요청합니다.
    /// </summary>


    [Header("레퍼런스")]
    [SerializeField] PlayerLevel playerLevel;
    [SerializeField] PlayerGrowth playerGrowth;
    [SerializeField] GrowthCardDeck cardDeck;
    [SerializeField] Health health;


    [Header("진행 상태 확인")]
    // 아직 추첨을 시작하지 않은 선택 기회입니다
    // 현재 선택 중인 기회는 이 숫자에 포함하지 않습니다
    [SerializeField] int pendingSelections;
    [SerializeField] bool isSelecting;
    // 현재 제시한 후보입니다. 선택이 끝날 때까지 다시 추첨하지 않습니다
    [SerializeField]List<GrowthCardData> currentCards = new List<GrowthCardData>();

    // 선택 직후 같은 프레임에 다음 선택까지 처리되는 것을 막습니다
    int lastSelectionFrame = -1;

    public bool IsSelecting => isSelecting;
    public int PendingSelections => pendingSelections;
    public IReadOnlyList<GrowthCardData> CurrentCards => currentCards;

    // 이후 UI가 선택 화면을 열고 닫거나 내용을 갱신할 때 사용
    public event Action OnSelectionChanged;

    private void Awake()
    {
        if (playerLevel == null)
            playerLevel = GetComponent<PlayerLevel>();
        if (playerGrowth == null)
            playerGrowth = GetComponent<PlayerGrowth>();
        if (cardDeck == null)
            cardDeck = GetComponent<GrowthCardDeck>();
        if (health == null)
            health = GetComponent<Health>();

        pendingSelections = 0;
        isSelecting = false;
        currentCards.Clear();

        if (playerLevel == null || playerGrowth == null ||cardDeck == null || health == null)
        {
            Debug.LogError("[카드 선택] 필요한 컴포넌트 연결을 확인하세요.", this);
            enabled = false;
        }
    }

    // 활성화될 때 레벨업 알림을 구독합니다.
    private void OnEnable()
    {
        if (playerLevel != null)
            playerLevel.OnLevelUp += HandleLevelUp;
    }

    // 비활성화될 때 구독을 해제하여 중복 호출을 방지
    private void OnDisable()
    {
        if (playerLevel != null)
            playerLevel.OnLevelUp -= HandleLevelUp;
    }

    // 레벨이 하나 오를 때마다 호출
    // newLevel은 이번에 도달한 레벨이며 선택 기회 하나를 적립
    void HandleLevelUp(int newLevel)
    {
        pendingSelections++;

        Debug.Log(
            $"[카드 선택] 레벨 {newLevel} 보상 적립. " + $"대기 기회: {pendingSelections}", this);
    }

    // 해당 프레임의 레벨업 알림 처리가 끝난 뒤 선택을 준비
    // 현재 후보가 있다면 유지하고 새 추첨으로 덮어쓰지 않습니다
    private void LateUpdate()
    {
        if (health.IsDead || isSelecting)
            return;
        if (lastSelectionFrame == Time.frameCount)
            return;

        OpenNextSelection();
    }

    // 대기 기회를 하나 사용하여 후보를 준비합니다
    // 후보가 없으면 그 기회를 건너뛰고 남은 기회를 계속 처리합니다
    void OpenNextSelection()
    {
        while (pendingSelections > 0)
        {
            pendingSelections--;

            currentCards.Clear();
            currentCards.AddRange(cardDeck.DrawCards(3));

            if (currentCards.Count == 0)
            {
                Debug.Log("[카드 선택] 가능한 후보가 없어 기회를 건너뜁니다.", this);
                continue;
            }

            isSelecting = true;

            Debug.Log($"[카드 선택] 후보 {currentCards.Count}장 준비. " + $"이후 대기 기회: {pendingSelections}",
                this);

            for (int i = 0; i < currentCards.Count; i++)
            {
                Debug.Log($"[{i + 1}] {currentCards[i].CardName} " + $"({currentCards[i].CardId})", this);
            }

            OnSelectionChanged?.Invoke();
            return;
        }
    }

    // 이후 카드 버튼에서 호출할 선택 진입점입니다
    // index는 0부터 시작하는 후보 위치이며, 적용 성공 시 true를 반환
    public bool TrySelectCard(int index)
    {
        if (!isSelecting || health.IsDead ||
            lastSelectionFrame == Time.frameCount)
            return false;
        if (index < 0 || index >= currentCards.Count)
            return false;

        GrowthCardData card = currentCards[index];

        // 효과 적용 중 발생하는 이벤트에서 중복 선택되지 않도록 먼저 잠급니다
        isSelecting = false;

        if (!playerGrowth.TryApplyCard(card))
        {
            // 실패하면 같은 후보를 유지하고 다시 선택할 수 있게 합니다
            isSelecting = true;

            Debug.LogWarning("[카드 선택] 효과 적용에 실패했습니다. 후보는 유지합니다.", this);
            return false;
        }

        lastSelectionFrame = Time.frameCount;
        currentCards.Clear();

        Debug.Log($"[카드 선택] {card.CardName} 적용 완료. " + $"남은 대기 기회: {pendingSelections}", this);

        OnSelectionChanged?.Invoke();
        return true;
    }

    // UI 연결 전, Play Mode에서 첫 번째 후보를 선택하는 임시 테스트입니다
    [ContextMenu("테스트/첫 번째 카드 선택")]
    void TestSelectFirstCard()
    {
        if (!Application.isPlaying)
            return;

        if (!TrySelectCard(0))
            Debug.LogWarning("[카드 선택] 현재 선택할 수 없습니다.", this);
    }

    // 여러 레벨 상승을 한 번에 발생시켜 선택 기회 누적을 검증합니다
    // 경험치 강화가 없는 새 플레이에서 사용하는 테스트입니다
    [ContextMenu("테스트/경험치 370 지급")]
    void TestMultipleLevelUps()
    {
        if (!Application.isPlaying)
            return;

        PlayerExperience experience = GetComponent<PlayerExperience>();

        if (experience != null)
            experience.AddExperience(370);
    }
}