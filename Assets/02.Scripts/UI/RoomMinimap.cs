using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전체 던전 지형을 북쪽 고정 상태로 그리고 플레이어 중심으로 평행 이동하는 미니맵입니다
/// 전투 상태는 읽기만 합니다
/// </summary>
public class RoomMinimap : MonoBehaviour
{
    [Header("씬에 저장된 참조")]
    public Transform player;
    public CoreController core;
    public RectTransform viewport;
    public RectTransform mapContent;
    public RoomMinimapGraphic[] rooms;
    public RectTransform playerMarker;
    public RectTransform coreMarker;
    public RectTransform coreDirection;
    public Image enemyTemplate;
    public RectTransform enemyLayer;

    [Header("표시 조정 — 기준 해상도 UI 단위")]
    [UnityEngine.Serialization.FormerlySerializedAs("minimumWorldSpan")]
    [Tooltip("미니맵 한 변에 보이는 월드 거리입니다. 작을수록 확대되며 플레이어 이동으로 배율이 변하지 않습니다.")]
    [Min(16)] public float visibleWorldSpan = 64f;
    [Min(1)] public float edgeInset = 10f;
    [Min(.1f)] public float scanInterval = .25f;
    // 매 프레임 전체 씬 검색과 마커 생성을 피하고 스폰/풀 재활성화만 주기적으로 반영
    readonly List<Image> enemyMarkers = new List<Image>();
    EnemyController[] enemies = System.Array.Empty<EnemyController>();
    float nextScan;
    float pixelsPerUnit;
    public float PixelsPerUnit => pixelsPerUnit;
    [Header("동적 지형 스타일")]
    public Color terrainColor = new Color(.1f, .18f, .22f, .9f);
    public Color terrainOutline = new Color(.4f, .57f, .64f, .85f);
    public float terrainOutlineWidth = .18f;

    //UI 활성화 전에 현재 씬의 지형만 생성
    //프리팹에는 특정 던전의 타일을 저장 x
    public void SetLayout(RoomMinimapLayout layout)
    {
        if (rooms != null)
            foreach (var room in rooms)
                if (room != null)
                {
                    room.gameObject.SetActive(false); Destroy(room.gameObject);
                }
        rooms = new RoomMinimapGraphic[layout.regions.Length];
        visibleWorldSpan = layout.visibleWorldSpan;

        for (int i = 0; i < rooms.Length; i++)
        {
            var region = layout.regions[i];
            var go = new GameObject(region.name, typeof(RectTransform), typeof(RoomMinimapGraphic));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(mapContent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);

            var graphic = go.GetComponent<RoomMinimapGraphic>();
            graphic.raycastTarget = false;
            graphic.tiles = region.tiles;
            graphic.circular = region.circular;
            graphic.circleCenter = region.center;
            graphic.circleRadius = region.radius;
            graphic.color = terrainColor;
            graphic.outlineColor = terrainOutline;
            graphic.outlineWidth = terrainOutlineWidth;

            Rect bounds = graphic.WorldBounds();
            rect.anchoredPosition = bounds.center;
            rect.sizeDelta = bounds.size;
            graphic.drawOrigin = bounds.center;
            rooms[i] = graphic;
        }
    }

    // 활성화 직후 검색 예약을 초기화
    // 생성된 마커는 비활성화 후에도 재사용
    void OnEnable()
    { 
        nextScan = 0;
    }

    // 플레이어 이동 후 위치를 읽어 맵만 평행 이동, 카메라와 맵 자체는 회전하지 않습니다
    void LateUpdate()
    {
        RefreshMap();
    }

    // 현재 위치와 실제 코어/적 상태를 UI로 투영
    // 에디터 제작,검증에서도 같은 경로를 사용
    public void RefreshMap()
    {
        if (player == null || viewport == null || mapContent == null || rooms == null || rooms.Length == 0)
            return;
        Vector2 center = XZ(player.position);

        // 전체 지형은 항상 유지하고 표시창 밖만 RectMask2D로 자릅니다
        // 방 경계에서 지도를 교체하지 않습니다
        for (int i = 0; i < rooms.Length; i++)
        {
            if (rooms[i] == null)
                continue;
            if (!rooms[i].gameObject.activeSelf)
                rooms[i].gameObject.SetActive(true);
        }

        pixelsPerUnit = Mathf.Min(viewport.rect.width,viewport.rect.height) / Mathf.Max(16f,visibleWorldSpan);
        mapContent.localScale = Vector3.one*pixelsPerUnit;
        mapContent.anchoredPosition = -center*pixelsPerUnit;
        mapContent.localRotation = Quaternion.identity;

        playerMarker.anchoredPosition = Vector2.zero;
        playerMarker.localRotation = Quaternion.Euler(0,0,-player.eulerAngles.y);
        UpdateCore(center);

        if (Application.isPlaying && Time.unscaledTime >= nextScan)
        {
            enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            nextScan = Time.unscaledTime + scanInterval;
        }
        UpdateEnemies(center);
    }

    // 살아 있는 코어만 보라색 원으로 표시하며 화면 밖이면 가장자리의 작은 초록 화살표로 전환
    void UpdateCore(Vector2 center)
    {
        bool active = core != null && core.gameObject.activeInHierarchy && !core.IsDestroyed;
        coreMarker.gameObject.SetActive(false);
        coreDirection.gameObject.SetActive(false);

        if (!active)
            return;
        Vector2 position = (XZ(core.transform.position)-center)*pixelsPerUnit;
        Vector2 half = viewport.rect.size*.5f-Vector2.one*edgeInset;

        bool inside = Mathf.Abs(position.x)<=half.x && Mathf.Abs(position.y)<=half.y;
        coreMarker.gameObject.SetActive(inside);
        coreDirection.gameObject.SetActive(!inside);

        if (inside)
            coreMarker.anchoredPosition = position;
        else
        {
            coreDirection.anchoredPosition = EdgePosition(position,half);
            coreDirection.localRotation = Quaternion.Euler(0,0,Mathf.Atan2(position.y,position.x) * Mathf.Rad2Deg-90);
        }
    }

    // 현재 표시창 안의 살아 있는 적을 방 구분 없이 빨간 점으로 표시
    // 사망, 풀 반환 즉시 숨깁니다
    void UpdateEnemies(Vector2 center)
    {
        int used = 0;
        foreach (EnemyController enemy in enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy)
                continue;

            Health health = enemy.GetComponent<Health>();
            if (health == null || health.IsDead)
                continue;
            Vector2 world = XZ(enemy.transform.position);
            Vector2 position = (world-center)*pixelsPerUnit;
            Vector2 half = viewport.rect.size*.5f-Vector2.one*4;

            if (Mathf.Abs(position.x)>half.x || Mathf.Abs(position.y)>half.y)
                continue;
            if (used == enemyMarkers.Count)
            {
                Image marker = Instantiate(enemyTemplate,enemyLayer);
                marker.name = "EnemyDot_"+used;
                enemyMarkers.Add(marker);
            }
            Image dot = enemyMarkers[used++];
            dot.gameObject.SetActive(true);
            dot.rectTransform.anchoredPosition = position;
        }
        for (int i = used; i < enemyMarkers.Count; i++) enemyMarkers[i].gameObject.SetActive(false);
    }

    // 중앙에서 목표로 향하는 방향을 사각형 가장자리로 줄이며 영벡터는 중앙을 반환
    public static Vector2 EdgePosition(Vector2 direction, Vector2 half)
    {
        float ratio = Mathf.Max(Mathf.Abs(direction.x)/Mathf.Max(1,half.x),Mathf.Abs(direction.y)/Mathf.Max(1,half.y));
        return ratio > 0 ? direction/ratio : Vector2.zero;
    }

    // 월드 위치를 위쪽이 북쪽인 2차원 좌표로 변환
    static Vector2 XZ(Vector3 position)
    { 
        return new Vector2(position.x,position.z);
    }
}

