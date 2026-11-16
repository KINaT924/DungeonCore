using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// 각 전투 씬의 참조를 보관하고 공용 HUD/카드 UI를 한 번 생성, 씬 종료 시 함께 정리됨
/// </summary>
[DefaultExecutionOrder(-150), DisallowMultipleComponent]
public class GameUISpawner : MonoBehaviour
{
    public enum BattleMode
    { 
        Survival,
        Boss 
    }

    [Header("공용 프리팹")]
    public GameHudView hudPrefab;
    public GrowthCardSelectionUI cardsPrefab;
    public StageDefeatPresentation defeatPrefab;

    [Header("씬에서 직접 편집하는 HUD — 연결하면 프리팹 생성 대신 사용")]
    public GameHudView sceneHud;

    [Header("현재 씬의 데이터")]
    public BattleMode mode;
    public GameObject player;
    public Camera uiCamera;
    public CoreController core;
    public DungeonRunController run;
    public BossPortalPresentation portalPresentation;
    public RoomMinimapLayout minimapLayout;

    [Header("보스전 표시 — 실제 Health가 연결되면 체력 표시")]
    public string bossName = "몽마퀸";
    public Health bossHealth;

    GameObject ownedRoot;
    public GameHudView Instance { get; private set; }
    public GrowthCardSelectionUI CardsInstance { get; private set; }
    public StageDefeatPresentation DefeatInstance { get; private set; }


    // 클리어와 패배 중 먼저 확정된 결과만 표시하여 두 패널의 시간/입력 잠금이 겹치지 않게 합니다.
    public enum BattleResult
    { 
        None,
        Cleared,
        Defeated
    }
    public BattleResult Result { get; private set; }


    // 종료 이벤트에서 호출
    // 미확정 전투만 한 번 종료하며 수락하면 true를 반환
    public bool TryFinishBattle(BattleResult result)
    {
        if (result == BattleResult.None || Result != BattleResult.None)
            return false;

        Result = result;
        return true;
    }

    // 일반 게임 컴포넌트의 Start 전에 UI를 생성하고 현재 씬의 실제 참조를 연결
    void Awake()
    { 
        CreateUI();
    }

    // 중복 호출해도 기존 UI를 반환
    // 비활성 부모 아래 생성하여 OnEnable 구독 전에 연결을 마침
    public void CreateUI()
    {
        if (ownedRoot != null)
            return;
        if ((sceneHud == null && hudPrefab == null) || player == null || minimapLayout == null)
        {
            Debug.LogError("[Game UI] HUD 프리팹, 플레이어, 미니맵 데이터를 연결하세요.", this);
            return;
        }
        ownedRoot = new GameObject("RuntimeGameUI");
        ownedRoot.transform.SetParent(transform, false);
        ownedRoot.SetActive(false);

        // 씬 HUD는 Hierarchy의 배치와 자식 구성을 그대로 사용
        // 활성 UI도 먼저 끈 뒤 Bind하여 OnEnable이 빈 플레이어 참조를 구독하지 않게 설정
        if (sceneHud != null)
        {
            Instance = sceneHud;
            Instance.gameObject.SetActive(false);
        }
        else
        {
            Instance = Instantiate(hudPrefab, ownedRoot.transform);
            Instance.name = "InGameHUDCanvas";
        }
        Instance.Bind(this);

        if (cardsPrefab != null)
        {
            CardsInstance = Instantiate(cardsPrefab, ownedRoot.transform);
            CardsInstance.name = "GrowthCardSelectionCanvas";
            CardsInstance.Bind(player.GetComponent<GrowthCardSelectionController>());
        }

        if (defeatPrefab != null)
        {
            DefeatInstance = Instantiate(defeatPrefab, ownedRoot.transform);
            DefeatInstance.name = "StageDefeatCanvas";
            DefeatInstance.Bind(this);
        }

        if (portalPresentation != null)
            portalPresentation.BindAnnouncement(Instance.PortalNotice);

        // 카드 선택용 입력 모듈은 씬에 이미 있으면 재사용하고 없는 씬에서만 생성
        if (EventSystem.current == null && FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 0)
        {
            var events = new GameObject("UI_EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(ownedRoot.transform, false);
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        ownedRoot.SetActive(true);

        if (sceneHud != null)
            Instance.gameObject.SetActive(true);
    }

    // 씬과 다른 수명으로 UI를 남기지 않습니다
    // 카드 UI의 OnDisable이 구독과 입력 잠금도 정리
    void OnDestroy()
    {
        if (ownedRoot != null)
            Destroy(ownedRoot);

        Instance = null;
        CardsInstance = null;
        DefeatInstance = null;
    }
}
