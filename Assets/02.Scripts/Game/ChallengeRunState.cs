using UnityEngine;

/// <summary>
/// 추가 도전 횟수와 적 능력치 배율만 씬 사이에 유지
/// 플레이어와 성장 데이터는 보관하지 않으므로
/// InGameScene 재진입 시 새 게임으로 시작
/// 에셋 원본을 수정하지 않아 풀 재사용이나 다음 실행에서 강화가 중복되면 안됨
/// </summary>

public static class ChallengeRunState
{
    // 추가 도전마다 직전 체력/공격력의 110%: 1 → 1.1 → 1.21.
    public static int AdditionalChallenges { get; private set; }
    public static float EnemyMultiplier { get; private set; } = 1f;


    // 도메인 재로드를 꺼도 Play 시작 때 이전 테스트의 난이도를 제거
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        AdditionalChallenges = 0;
        EnemyMultiplier = 1f;
    }

    // 클리어 화면의 추가 도전 버튼에서 씬 이동 직전에 한 번 호출
    public static void Advance()
    {
        AdditionalChallenges++;
        EnemyMultiplier *= 1.1f;
    }
}
