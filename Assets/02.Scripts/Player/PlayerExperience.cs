using System;
using UnityEngine;
#if UNITY_EDITOR
using Unity.Cinemachine.Editor;
#endif
public class PlayerExperience : MonoBehaviour
{
    /// <summary>
    /// 플레이어가 이번 플레이에서 획득한 총 경험치를 관리
    /// 누정량은 이벤트를 통해 UI에 전달
    /// 경험치의 알갱이나 레벨 등은 이 스크립트에서 관리하지 않습니다
    /// </summary>

    [Header("레퍼런스")]
    [SerializeField] PlayerGrowth playerGrowth;

    [Header("테스트 - 실행중 확인")]
    [SerializeField] int totalExp;          // 이번 플레이에서 얻은 총 경험치량
    [SerializeField] double expFraction;
    // 외부에서 경험치를 덮어쓰지않도록 제한
    public int TotalExperience { get { return totalExp; } }

    // 경험치 지급 후 변경된 총량을 전달
    // UI나 성장 시스템이 이 이벤트를 통해 구독
    public event Action<int> OnExpChanged;

    private void Awake()
    {
        if (playerGrowth == null)
            playerGrowth = GetComponent<PlayerGrowth>();

        // 오브젝트가 처음 초기화 될 때 시작 경험치를 0으로 설정
        totalExp = 0;
        expFraction = 0d;
    }

    // 경험치 알갱이가 도착하는 등 보상 획득 시 외부에서 호출
    // 잘못된 지급량과 정수 범위 초과를 방지하고 경험치 누적량을 플레이어에게 전달
    public void AddExperience(int amout)
    {
        // 음수로 경험치가 감소되거나 0 지급이 되는 경우를 방지
        if (amout <= 0)
            return;

        // 참조가 없으면 기존과 동일하게 기본 획득량 지급
        float bonus = playerGrowth != null ? playerGrowth.ExperienceGainBonus : 0f;

        // 이번 획득량에 강화 비율을 적용하고 이전 소수점 잔여량을 추가
        double exactGain = amout * (1d + bonus) + expFraction;
        double wholeGain = Math.Floor(exactGain);


        // 정수 범위를 넘어 음수로 바뀌는것을 방지
        if (wholeGain > int.MaxValue - totalExp)
        {
            Debug.LogWarning("경험치가 누적량을 초과하여 지급을 일시 중단", this);
            return;
        }

        int gainedExp = (int)wholeGain;
        expFraction = exactGain - wholeGain;
        totalExp += gainedExp;

        Debug.Log(
            $"[경험치] 기본 {amout}, 증가 {bonus:P0}, " +
            $"실제 지급 {gainedExp}, 소수점 잔여 {expFraction:F3}, " +
            $"누적 {totalExp}",
            this);


        // 플레이어가 존재한다면 변경된 총 경험치량을 전달받습니다
        OnExpChanged?.Invoke(totalExp);
    }

    // 보스전에 진입하기 전 호출되어 현재 경험치 상태를 저장
    public void CaptureSnapshot(PlayerGrowthSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        // 값을 저장
        snapshot.TotalExperience = totalExp;
        snapshot.ExperienceFraction = expFraction;
    }

    // 플레이어의 경험치를 복원
    public bool RestoreSnapshot(PlayerGrowthSnapshot snapshot)
    {
        if (snapshot == null)
            return false;

        // 저장된 값을 복원
        totalExp = snapshot.TotalExperience;
        expFraction = snapshot.ExperienceFraction;

        return true;
    }

    [ContextMenu("경험치 테스트 - 10지급")]
    void AddTestExp()
    {
        if (!Application.isPlaying)
            return;

        AddExperience(10);
    }
}
