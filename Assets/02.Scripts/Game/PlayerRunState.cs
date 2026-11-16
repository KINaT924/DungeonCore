using UnityEngine;

/// <summary>
/// 일반전에서 저장한 플레이어 성장 상태를 로딩씬을 거쳐 보스전으로 전달하기 위한 스크립트
/// 복사한 수치만 보관했다가 전달하며 전달 성공 후 데이터를 비움
/// </summary>
public static class PlayerRunState
{
    // 아직 보스전에서 사용하지 않은 저장 데이터
    static PlayerGrowthSnapshot pendingSnapshot;
    public static bool HasSnapshot => pendingSnapshot != null;

    // 새로운 게임 플레이 시작 시 이전의 저장되어있던 데이터를 제거
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Clear()
    {
        pendingSnapshot = null;
    }

    // 포탈 진입 직전에 플레이어의 성장 스탯값 전체를 복사
    public static bool TrySave(Health player)
    {
        if (player == null || player.IsDead)
            return false;

        // 체력, 경험치, 카드로인한 성장 값들을 저장
        PlayerGrowth growth = player.GetComponent<PlayerGrowth>();
        PlayerExperience experience = player.GetComponent<PlayerExperience>();
        GrowthCardSelectionController selection = player.GetComponent<GrowthCardSelectionController>();

        if(growth == null || experience == null)
        {
            Debug.LogError("플레이어 성장,경험치 컴포넌트를 확인하세요", player);
            return false;
        }

        // 카드 선택 미완료 일 때 포탈 진입 방지
        if(selection != null && (selection.IsSelecting || selection.PendingSelections > 0))
        {
            Debug.LogWarning("카드 선택을 전부 마친 후 포탈에 진입하세요", player);
            return false;
        }

        PlayerGrowthSnapshot snapshot = growth.CaptureSnapshot();
        experience.CaptureSnapshot(snapshot);
        player.CaptureSnapshot(snapshot);

        // 필요한 값을 모두 복사한 뒤 전달할 데이터로 확정
        pendingSnapshot = snapshot;

        Debug.Log($"성장치 저장 완료, 경험치 : {snapshot.TotalExperience}," +
                                      $"체력 : {snapshot.CurrentHealth}, " +
                            $"최대 체력 추가 : {snapshot.MaxHealthBonus}", player);
        return true;
    }

    // 보스전에서 플레이어의 Awake실행이 전부 끝난 뒤 성장 저장 상태를 복원
    public static bool TryRestore(GameObject player)
    {
        if (pendingSnapshot == null || player == null)
            return false;

        PlayerGrowth growth = player.GetComponent<PlayerGrowth>();
        PlayerExperience experience = player.GetComponent<PlayerExperience>();
        PlayerLevel level = player.GetComponent<PlayerLevel>();
        Health health = player.GetComponent<Health>();
        PlayerGunController gun = player.GetComponent<PlayerGunController>();

        if (growth == null || experience == null || level == null ||
            !level.isActiveAndEnabled || health == null || gun == null)
        {
            Debug.LogError("보스전 플레이어의 필수 컴포넌트 확인 (성장)", player);
            return false;
        }

        PlayerGrowthSnapshot snapshot = pendingSnapshot;

        if (!growth.RestoreSnapshot(snapshot) ||
            !experience.RestoreSnapshot(snapshot) ||
            !health.RestoreSnapshot(snapshot))
        {
            Debug.LogError("성장 상태 복원에 실패", player);
            return false;
        }

        // 레벨업 카드를 다시 지급하지 않고 레벨과 HUD만 갱신
        level.RefreshFromExperience();

        // 성장으로 늘어난 탄창 크기를 모든 무기의 시작 탄약에 반영
        gun.RefillAmmoAfterGrowthRestore();

        // 같은 저장 상태를 다시 적용하지 않도록 성공 후 비움
        pendingSnapshot = null;

        Debug.Log(
            $"성장 복원 완료, 레벨 : {level.CurrentLevel}, " +
            $"경험치 : {experience.TotalExperience}, " +
            $"체력 : {health.CurrentHealth}/{health.MaxHealth}", player);

        return true;
    }
}
