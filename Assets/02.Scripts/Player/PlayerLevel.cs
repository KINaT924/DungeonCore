using System;
using UnityEngine;

[RequireComponent(typeof(PlayerExperience))]
public class PlayerLevel : MonoBehaviour
{
    /// <summary>
    /// PlayerExperience의 누적 경험치를 받아와 현재 레벨을 계산하는 스크립트
    /// 누적 경험치량은 레벨업으로 인해 하지않으며
    /// 레벨에 따라 요구하는 경험치량을 제공합니다
    /// </summary>


    [Header("레퍼런스")]
    [SerializeField] PlayerExperience playerExp;

    [Header("요구 경험치")]
    [SerializeField] int[] requiredExpByLv = { 100, 120, 150 };

    [Header("실행 중 확인")]
    [SerializeField] int curLv = 1;
    [SerializeField] int curLvExp;
    [SerializeField] int requiredExp;

    public int CurrentLevel => curLv;
    public int CurrentLevelExp => curLvExp;
    public int RequiredExp => requiredExp;
    // 테이블에 등록된 마지막 레벨에 도달했는지 나타내는 용도 -> 게임의 최종 레벨 상관 x
    public bool IsAtConfiguredMaxLevel => CurrentLevel >= requiredExpByLv.Length + 1;
    // 레벨업 할때 호출되는 이벤트 
    public event Action<int> OnLevelUp;
    // 현재 레벨, 레벨 내 경험치, 다음 레벨 요구 경험치를 받을 이벤트입니다
    public event Action<int, int, int> OnLevelProgressChaned;


    private void Awake()
    {
        if(playerExp == null)
            playerExp = GetComponent<PlayerExperience>();

        if(requiredExpByLv == null || requiredExpByLv.Length == 0)
        {
            Debug.LogError("레벨 경험치를 한개 이상 등록하십시오", this);
            enabled = false;
            return;
        }

        foreach(int amount in requiredExpByLv)
        {
            if(amount <= 0)
            {
                Debug.LogError("레벨 경험치의 요구 경험치는 1이상이여야만 합니다 ", this);
                enabled = false;
                return;
            }
        }
    }

    // 활성화 될 때 실행하는 메소드
    // 현재 누적 경험치로 상태를 복원하되 레벨업 보상 이벤트는 실행x
    // 카드 보상이 중복되는것을 방지는 역할을 합니다
    private void OnEnable()
    {
        if (playerExp == null)
            return;

        playerExp.OnExpChanged += HandleExpChanged;
        RecalculateLate(playerExp.TotalExperience, false);
    }

    
    void Start()
    {
        RecalculateLate(playerExp.TotalExperience, false);
    }

    // 비활성화 됬을 시에 경험치 이벤트 구독을 해제
    private void OnDisable()
    {
        if(playerExp != null)
            playerExp.OnExpChanged -= HandleExpChanged;
    }

    // PlayerExperience에서 누적 경험치가 바뀐다면 호출
    // totalExp는 이번 획득량이 아닌 지금까지의 누적량
    // 실제 획득에 따른 변경이므로 레벨 상승 이벤트도 허용
    void HandleExpChanged(int totalExp)
    {
        RecalculateLate(totalExp, true);
    }

    // 저장된 경험치를 복원한 후에 호출
    // 누적된 경험치와 레벨을 계산하여 HUD에 갱신
    public void RefreshFromExperience()
    {
        if (playerExp == null || !enabled)
            return;

        RecalculateLate(playerExp.TotalExperience, false);
    }

    // 누적 경험치 총량에서 각 레벨의 요구량을 순서대로 차감하여 도달 가능한 레벨과 남은 경험치량을 계산합니다
    // emitLevelUp이 true면 레벨 상승 이벤트를 발생
    void RecalculateLate(int totaExp, bool emitLevelUp)
    {
        int previousLv = curLv;
        int remainingExp = Mathf.Max(0, totaExp);
        int calculatedLv = 1;

        // 여러 번의 레벨 상승도 한번에 처리
        for(int i = 0; i < requiredExpByLv.Length; i++)
        {
            int cost = requiredExpByLv[i];

            if (remainingExp < cost)
                break;

            remainingExp -= cost;
            calculatedLv++;
        }

        curLv = calculatedLv;
        curLvExp = remainingExp;

        // 마지막 등록 레벨에는 다음 요구량이 없으므로 0으로 표시
        requiredExp = IsAtConfiguredMaxLevel ? 0 : requiredExpByLv[curLv - 1];

        if(emitLevelUp)
        {
            for(int lv = previousLv + 1; lv <= curLv; lv++)
            {
                Debug.Log($"[레벨 업 !] 레벨 {lv} 도달", this);
                OnLevelUp?.Invoke(lv);
            }
        }

        OnLevelProgressChaned?.Invoke(curLv, curLvExp, requiredExp);
    }
}
