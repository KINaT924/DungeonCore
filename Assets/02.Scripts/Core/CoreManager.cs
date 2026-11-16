using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;


public class CoreManager : MonoBehaviour
{
    /// <summary>
    /// 코어 자동 생성 주기와 생성 위치를 관리하는 스크립트
    /// 생성 시기에 플레이어 위치를 받아와 가장 가까운 코어중 하나가 생성
    /// 이미 활성화 상태인 코어가 존재한다면 추가 생성 x
    /// </summary>

    [Header("레퍼런스")]
    [SerializeField] CoreController core;
    [SerializeField] Transform player;
    [SerializeField] Transform[] spawnPoints;


    [Header("생성 설정")]
    [SerializeField, Min(0.1f)] float spawnInterval = 15f;          // 다음 생성 주기 시간
    [SerializeField, Min(1)] int nearestPointCount = 3;             // 스폰 생성 후보 지점의 수

    readonly List<Transform> validSpawnPoints = new List<Transform>();
    float spawnTimer;                                               // 코어생성 타이머
    bool wasCoreActive;                                             // 코어가 존재하는지


    void Start()
    {
        if( core == null || player == null)
        {
            Debug.LogError("연결참조를 확인하세요, CoreManager", this);
            enabled = false;
            return;
        }

        validSpawnPoints.Clear();               // 스폰지점 계산 초기화

        if (spawnPoints != null)
        {
            foreach (Transform point in spawnPoints)
            {
                // 비어있거나 중복 지점은 제외
                if (point == null || validSpawnPoints.Contains(point))
                    continue;

                validSpawnPoints.Add(point);
            }
        }

        // 스폰지점이 0개일경우
        if(validSpawnPoints.Count == 0)
        {
            Debug.LogError("코어 스폰 지점이 0개입니다. 확인하세요, CoreManager", this);

            enabled = false;
            return;
        }

        spawnInterval = Mathf.Max(0.1f, spawnInterval);
        nearestPointCount = Mathf.Max(1, nearestPointCount);

        spawnTimer = spawnInterval;
        wasCoreActive = core.gameObject.activeInHierarchy;
    }

    // 매 프레임마다 코어 활성 여부와 생성 시간을 확인
    void Update()
    {
        if (core == null || player == null)
            return;

        bool isCoreActive = core.gameObject.activeInHierarchy;

        // 코어가 활성화가 이미 되어있다면 생성 x
        if (isCoreActive)
        {
            wasCoreActive = true;
            return;
        }
        if(wasCoreActive)
        {
            wasCoreActive = false;
            spawnTimer = spawnInterval;

            Debug.Log($"코어 다음 생성까지 {spawnInterval}초", this);
            return;
        }

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
            SpawnCore();    
    }

    // 생성될 때 호출, 플레이어와 가까운 지점을 찾고 해당 위치에 활성화
    void SpawnCore()
    {
        if (core == null || player == null)
            return;
        if (core.gameObject.activeInHierarchy)
            return;
        Transform parnet = core.transform.parent;

        // 부모 오브젝트가 꺼져있으면 코어가 활성화되도 켜지지않습니다
        if(parnet != null && !parnet.gameObject.activeInHierarchy)
        {
            Debug.LogError("참조 연결을 확인하세요, CoreManager", this);
            enabled = false;
            return;
        }

        Transform point = SelectSpawnPoint();

        if (point == null)
        {
            Debug.LogError("생성 지점이 존재하지않습니다, CoreManager", this);
            enabled = false;
            return;
        }

        core.transform.SetPositionAndRotation(point.position, point.rotation);

        // OnEnable에서 몬스터 강화와 체력 초기화를 실행시킵니다
        core.gameObject.SetActive(true);
        wasCoreActive = true;
        Debug.Log($"코어를 플레이어와 가장 가까운 지점{point.name}에 생성합니다", this);
    }


    // 호출 시점에 플레이어 위치를 받아 생성지점을 가까운 순서대로 정렬
    Transform SelectSpawnPoint()
    {
        // 실행 도중 삭제된 지점은 정렬에서 제외
        validSpawnPoints.RemoveAll(point => point == null);

        // 유효지점이 0갤면 null 반환
        if (validSpawnPoints.Count == 0)
            return null;

        // 생성 위치를 플레이어 위치에서 가까운 곳을 선택하기위해 플레이어 위치값 저장
        Vector3 playerPos = player.position;

        // 정렬
        validSpawnPoints.Sort((a, b) =>
        {
            float disA = GetHorizontalSqrDistance(a.position, playerPos);
            float disB = GetHorizontalSqrDistance(b.position, playerPos);
            return disA.CompareTo(disB);
        });

        int candidateCount = Mathf.Min(nearestPointCount, validSpawnPoints.Count);

        // 스폰지점 3개중 랜덤 한개 선택
        int index = Random.Range(0, candidateCount);
        return validSpawnPoints[index];
    }

    // 두 위치 사이의 높이 차이를 제외한 거리의 제곱을 반환
    // 코어 중심과 플레이어 루트의 높이가 다르더라도 바닥기준으로 Y차이를 제거
    float GetHorizontalSqrDistance(Vector3 a, Vector3 b)
    {
        Vector3 offset = a - b;
        offset.y = 0f;

        return offset.sqrMagnitude;
    }

    // 1라운드 종료 후  코어의 재생성을 중단 및 비활성화
    public void StopForBossTransition()
    {
        enabled = false;
        StopAllCoroutines();

        if(core != null)
            core.gameObject.SetActive(false);
        wasCoreActive = false;
    }

}
