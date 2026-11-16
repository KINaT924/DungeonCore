using System.Collections.Generic;
using UnityEngine;

public class GrowthCardDeck : MonoBehaviour
{
    /// <summary>
    /// 플레이어의 강화 상태를 기준으로 선택 가능한 카드를 추첨
    /// 카드 효과 적용과 선택 UI 처리는 담당하지 않습니다
    /// </summary>


    [Header("레퍼런스")]
    [SerializeField] PlayerGrowth playerGrowth;

    [Header("추첨에 사용할 카드")]
    [SerializeField] GrowthCardData[] allCards;


    private void Awake()
    {
        if (playerGrowth == null)
            playerGrowth = GetComponent<PlayerGrowth>();
    }

    // 카드 선택 화면을 준비할 때 호출
    // count는 원하는 카드 수이며, 반환값은 중복 없이 추첨한 새로운 목록을 보여줌
    // 후보가 부족하면 있는 만큼, 후보가 없으면 빈 목록을 반환
    public List<GrowthCardData> DrawCards(int count = 3)
    {
        var result = new List<GrowthCardData>();

        if (count <= 0)
            return result;

        if (playerGrowth == null)
        {
            Debug.LogError(
                "[카드 추첨] PlayerGrowth 연결을 확인하세요.",
                this);
            return result;
        }

        if (allCards == null)
            return result;

        var candidates = new List<GrowthCardData>();

        // Inspector에 같은 카드가 여러 번 들어가도 추첨 확률이 높아지거나 화면에 중복 등장하지 않도록 방지
        var includedIds = new HashSet<string>();

        foreach (GrowthCardData card in allCards)
        {
            // 카드 누락, 미구현 효과, 최대 선택 횟수 도달 등을
            // 실제 효과 적용 시 사용하는 동일한 규칙으로 추가적인 검사 진행
            if (!playerGrowth.CanSelectCard(card))
                continue;

            // 선택 횟수 관리와 동일하게 Card ID를 중복 기준으로 사용
            if (!includedIds.Add(card.CardId))
                continue;

            candidates.Add(card);
        }

        int drawCount = Mathf.Min(count, candidates.Count);

        for (int i = 0; i < drawCount; i++)
        {
            // int 범위의 Random.Range는 최댓값을 포함하지 않게 설정
            // 현재 후보 중 하나를 같은 확률로 선택
            int index = Random.Range(0, candidates.Count);
            result.Add(candidates[index]);

            // 뽑은 카드를 이번 후보 목록에서 제거하여 중복을 방지
            // Inspector 배열과 원본 카드 에셋은 변경하지 않도록 설정
            candidates.RemoveAt(index);
        }

        return result;
    }

    // Play Mode에서 추첨 결과만 확인하는 임시 테스트
    [ContextMenu("테스트/카드 3장 추첨")]
    void TestDrawCards()
    {
        if (!Application.isPlaying)
            return;

        List<GrowthCardData> cards = DrawCards(3);

        Debug.Log($"[카드 추첨] 결과: {cards.Count}장", this);

        for (int i = 0; i < cards.Count; i++)
        {
            GrowthCardData card = cards[i];

            Debug.Log(
                $"[{i + 1}] {card.CardName} ({card.CardId}) / " +
                $"선택 횟수: " +
                $"{playerGrowth.GetSelectionCount(card.CardId)}" +
                $"/{card.MaxSelections}",
                this);
        }
    }
}