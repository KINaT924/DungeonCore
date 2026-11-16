using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

public class EnemySpawner : MonoBehaviour
{
    /// <summary>
    /// 플레이어 주변, 코어 주변의 유효한 NavMesh 위치에 몬스터를 생성하고,
    /// 동시에 살아 있을 수 있는 몬스터 수를 제한하는 스포너입니다.
    ///
    /// 사망한 몬스터는 EnemyDeathSink에 의해 비활성화되며,
    /// 이후 새 몬스터가 필요할 때 비활성화된 몬스터를 다시 사용합니다.
    /// </summary>

    [Header("스폰 대상")]
    [SerializeField] EnemyController[] enemyPrefabs;     // 스포너가 생성 가능한 몬스터 프리팹 배열
    [SerializeField] Health playerHealth;                        // 플레이어 위치를 찾기위한 컴포넌트 참조 변수
    [SerializeField] ExperiencePool expPool;                // 생성되는 몬스터에게 전달될 경험치 풀

    [Header("스폰 관련")]
    [SerializeField, Min(0f)] float playerMinDistance = 10f;                     // 플레이어 주변 최소 스폰거리
    [SerializeField, Min(0.1f)] float playerMaxDistance = 25f;                 // 플레이어 주변 최대 스폰거리
    [SerializeField] Transform coreCenter;                                             // 코어 주변 스폰 위치 값
    [SerializeField, Min(0f)] float coreMinDistance = 3f;                         // 코어 주변 최소 스폰거리
    [SerializeField, Min(0.1f)] float coreMaxDistance = 8f;                     // 코어 주변 최대 스폰거리
    [SerializeField, Range(0f, 1f)] float spawnChance = 0.6f;                // 0일 시 플레이어 주변 스폰, 1일 시 코어 주변 스폰
    [SerializeField] LayerMask spawnBlockingMask = ~0;                      // 스폰이 길을 막지는 않는지 확인하는 변수
    [SerializeField, Min(0.1f)] float spawnCheckRadius = 0.5f;              // 현재 몬스터들의 스폰 크기 
    [SerializeField, Min(0.2f)] float spawnCheckHeight = 2f;                // 현재 몬스터들의 스폰 높이
    [SerializeField, Min(0.1f)] float spawnGround = 0.1f;                   // 몬스터가 땅과 접촉된것을 장애물 끼임현상으로 판단하지않게 하기 위해 최소로 뛰움
    [SerializeField, Min(0.1f)] float playerNavMeshDis = 1f;                // 플레이어의 NavMesh를 찾는 거리
 

    [Header("NavMash 관련")]
    [SerializeField, Min(0.1f)] float navMeshDistance = 3f;                     // 무작위 지점 주변에서 NavMesh를 찾기 위한 거리
    [SerializeField, Min(1f)] int maxPosAttempts = 30;                           // 몬스터를 생성한뒤 다음 생성 지점을 검사하는데 필요한 최대 지점 횟수

    [Header("몬스터 관련")]
    [SerializeField, Min(0)] int enemyCount = 5;                                    // 시작하였을 때 몬스터 개채 수
    [SerializeField, Min(1)] int maxEnemyCount = 50;                           // 최대 몬스터 개채 수
    [SerializeField, Min(0f)] float reSpawnDeley = 3f;                            // 리스폰 딜레이


    // 생성되었거나 비활성화 상태인 몬스터를 기록 후
    // 스폰에서 우선적으로 사용
    readonly List<EnemyController> enemyPool = new List<EnemyController>();
    // 인스펙터 목록에서 null과 중복을 제거하고 실제 생성 가능한 프리팹 목록을 나열
    // 무작위 프리팹을 생성
    readonly List<EnemyController> validEnemyPrefabs = new List<EnemyController>();
    // 몬스터 프리팹 종류마다 별도의 풀을 저장
    readonly Dictionary<EnemyController, List<EnemyController>> enemyPoolsByPrefab = new Dictionary<EnemyController, List<EnemyController>>();
    // 각 몬스터의 컴포넌트들을 해당 몬스터와 함께 콜백을 거쳐 저장합니다
    readonly Dictionary<Health, Action> deathHandlers = new Dictionary<Health, Action>();

    int aliveEnemyCount;        // 살아있는 몬스터 수
    bool isCoreActive;             // 코어가 존재하고 있는지
    bool isShuttingDown;        // 리젠 코루틴의 오류발생 방지
    NavMeshPath spawnCheckPath;

    // 초기 몬스터 생성
    private void Start()
    {
        PrepareEnemyPools();

        if (validEnemyPrefabs.Count == 0)
        {
            Debug.LogError("연결을 확인하세요, EnemySpawner - EnemyPrefab", this);
            enabled = false;
            return;
        }
        if (playerHealth == null)
        {
            Debug.LogError("연결을 확인하세요, EnemySpawner - PlayerHealth", this);
            enabled = false;
            return;
        }

        // 최초 스폰 전에 강화 상태 결정
        // 코어가 없거나 비활성 상태라면 일반 몬스터로 생성
        isCoreActive = coreCenter != null && coreCenter.gameObject.activeInHierarchy;

        // 초기 생성 수가 최대 수를 넘지않도록 제한
        int spawnCount = Mathf.Min(enemyCount, maxEnemyCount);
        for(int i =0; i<spawnCount; i++)
        {
            if(!TrySpawnEnemy())
            {
                Debug.LogError($"초기 몬스터 {i + 1}번째 유효 위치를 찾지 못하였습니다.", this);

                StartCoroutine(RespawnRoutine());
            }
        }
    }

    // 활성 코어가 플레이 도중 파괴되어 삭제되거나 비활성화되었는지 업데이트에서 확인
    // 코어가 파괴되면 몬스터의 강화를 해제하고 생성되는 몬스터도 일반상태로 변경
    private void Update()
    {
        if (!isCoreActive)
            return;

        // 삭제된 오브젝트와 비활성화 오브젝트 모두 검사
        bool coreWasRomoeved = coreCenter == null || !coreCenter.gameObject.activeInHierarchy;

        if (coreWasRomoeved)
            // 몬스터 상태 변경 메소드
            SetCoreState(null, false);
    }

    // null을 제외하고 같은 프리팹이 연속으로 연결되어 있다면 중복 방지를 위해 한번만 등록
    void PrepareEnemyPools()
    {
        // 플레이를 다시 시작하거나 초기화 순서가 변경된다면 이전 정보를 비워둠
        validEnemyPrefabs.Clear();
        enemyPoolsByPrefab.Clear();

        if (enemyPrefabs == null)
            return;

        foreach(EnemyController prefab in enemyPrefabs)
        {
            // 인스펙터 배열의 비어있는 칸은 생성 후보에서 제외
            if (prefab == null)
                continue;

            // 중복 제외
            if (enemyPoolsByPrefab.ContainsKey(prefab))
                continue;

            validEnemyPrefabs.Add(prefab);
            enemyPoolsByPrefab.Add(prefab, new List<EnemyController>());
        }
    }


    // 최대 생존 몬스터를 검사하고 유효한 NavMesh 위치를 찾은 뒤
    // 몬스터를 생성합니다. 비활성 몬스터가 존재한다면 재사용합니다
    bool TrySpawnEnemy()
    {
        if (isShuttingDown || !isActiveAndEnabled)
            return false;
        if (aliveEnemyCount >= maxEnemyCount)
            return false;
        if (!TryGetSpawnPosition(out Vector3 spawnPos))
            return false;

        // 이번에 생성될 몬스터를 무작위로 선택
        EnemyController selectedPrefab = SelectEnemyPrefab();
        if(selectedPrefab == null)
            return false;
        EnemyController enemy = GetOrCreateEnemy(selectedPrefab, spawnPos);


        if(enemy ==null)
            return false;

        // 첫 실행 전 추적 대상과 현재 전역코어 강화 상태를 전달합니다
        enemy.Initialize(playerHealth, isCoreActive);

        // 최초 생성과 재사용 모두 현재 씬의 경험치 풀을 전달
        EnemyExperienceDrop expDrop = enemy.GetComponent<EnemyExperienceDrop>();
        if (expDrop != null)
            expDrop.Initialize(expPool);
        else
            Debug.LogWarning("몬스터 프리팹에 EnemyExperienceDrop 스크립트를 추가하세요", this);

            aliveEnemyCount++;
        return true;
    }

    // 스폰 중심이 선택됬다면, 선택된 중심을 기준으로 최소,최대 거리 안에서 무작위 위치를 생성합니다
    bool TryGetSpawnPosition(out Vector3 spawnPos)
    {
        spawnPos = Vector3.zero;

        if(playerHealth == null)
            return false;

        // 플레이어가 서있는 위치를 기준으로 NavMesh를 찾을 수 있는지 검사
        if (!NavMesh.SamplePosition(playerHealth.transform.position, out NavMeshHit playerHit, playerNavMeshDis, NavMesh.AllAreas))
            return false;
        if(spawnCheckPath == null)
            spawnCheckPath = new NavMeshPath();

        for (int i = 0; i < maxPosAttempts; i++)
        {
            // 위치 검사를 실행해 플레이어와 코어 중 어느 위치를 중심으로 사용할지 결정
            SelectSpawnArea(out Vector3 spawnCenter, out float minDis, out float maxDis);
            Vector2 ranDir = Random.insideUnitCircle;

            // 드물게 0의 길이가 선택되면 정규화 결과를 사용하지않고 기본 방향을 사용합니다
            if (ranDir.sqrMagnitude < 0.0001f)
                ranDir = Vector2.right;
            else
                ranDir.Normalize();

            float ranDis = Random.Range(minDis, maxDis);
            Vector3 candidatePos = spawnCenter + new Vector3(ranDir.x, 0f, ranDir.y) * ranDis;
            bool foundNavMesh = NavMesh.SamplePosition(candidatePos, out NavMeshHit hit, navMeshDistance, NavMesh.AllAreas);

            if (!foundNavMesh)
                continue;

            // 최종 위치도 선택한 중심의 거리 조건을 다시 검사
            Vector3 centerOffset = hit.position - spawnCenter;
            centerOffset.y = 0f;

            float centerDisSqr = centerOffset.sqrMagnitude;
            float minDisSqr = minDis * minDis;
            float maxDisSqr = maxDis * maxDis;

            // 너무 가까운 위치나 너무 먼 곳이라면 위치를 다시 검사
            if (centerDisSqr < minDisSqr || centerDisSqr > maxDisSqr)
                continue;

            // 몬스터 몸이 통과할 공간이 없으면 다른 위치를 탐색
            if (IsSpawnPositionBlocked(hit.position))
                continue;
            // 스폰 지점에서 플레이어까지 경로를 계산
            bool foundPath = NavMesh.CalculatePath(hit.position, playerHit.position, NavMesh.AllAreas, spawnCheckPath);
            
            // 목적지까지 연결된 경로만 허용
            if(!foundPath || spawnCheckPath.status != NavMeshPathStatus.PathComplete)
                continue;

            spawnPos = hit.position;
            return true;
        }

        // 모든 위치 검사를 실패한다면 기본값을 전달
        spawnPos = Vector3.zero;
        return false;
    }

    bool IsSpawnPositionBlocked(Vector3 pos)
    {
        float radius = spawnCheckRadius;
        float height = Mathf.Max(spawnCheckHeight, radius * 2f);

        // 캡슐 아래쪽과 위쪽 구의 중심점
        Vector3 bottom = pos + Vector3.up * (radius + spawnGround);
        Vector3 top = pos + Vector3.up * (height - radius + spawnGround);

        return Physics.CheckCapsule(bottom, top, radius, spawnBlockingMask, QueryTriggerInteraction.Ignore);
    }


    // 위치 탐색을 통해 플레이어와 코어 중 어떤 대상을 스폰 중심으로 사용할지 결정
    void SelectSpawnArea(out Vector3 spawnCenter, out float minDis, out float maxDis)
    {
        // 코어 강화 상태가 활성호 되어있고, 코어가 존재하는지 활성화인지 확인하는 과정
        bool canUseCore = isCoreActive && coreCenter != null && coreCenter.gameObject.activeInHierarchy;

        //랜덤 밸류값을 반환하여 확률에 따라 코어중심인지 플레이어 중심인지 선택
        bool spawnAroundCore = canUseCore && Random.value < spawnChance;

        if (spawnAroundCore)
        {
            // 코어 주변 스폰이 결정되었을 때 위치와 거리값을 반환합니다
            spawnCenter = coreCenter.position;
            minDis = coreMinDistance;
            maxDis = coreMaxDistance;
            return;
        }

        // 코어가 없다면 플레이어 위치와 거리값을 반환합니다
        spawnCenter = playerHealth.transform.position;
        minDis = playerMinDistance;
        maxDis = playerMaxDistance;
    }

    // 현재 등록된 몬스터 프리팹 중 하나를 선택, 없다면 null을 반환
    EnemyController SelectEnemyPrefab()
    {
        if (validEnemyPrefabs.Count == 0)
            return null;

        int randomIndex = Random.Range(0, validEnemyPrefabs.Count);
        return validEnemyPrefabs[randomIndex];
    }
    // 오브젝트풀에 저장되어있는 비활성 몬스터를 먼저 탐색합니다
    EnemyController GetOrCreateEnemy(EnemyController selectedPrefab, Vector3 spawnPos)
    {
        if (selectedPrefab == null)
            return null;

        if(!enemyPoolsByPrefab.TryGetValue(selectedPrefab, out List<EnemyController> selectedPool))
        {
            Debug.LogError($"{selectedPrefab.name}의 준비되지 않았습니다,", this);
            return null;
        }

        foreach (EnemyController enemy in selectedPool)
        {
            if(enemy == null)
                continue;
            if (enemy.gameObject.activeSelf)
                continue;

            Health enemyHealth = enemy.GetComponent<Health>();

            if (enemyHealth == null)
            {
                Debug.LogError( $"{enemy.name}에 Health가 없습니다.",enemy);
                continue;
            }

            // OnEnable보다 먼저 체력과 사망 상태를 복구합니다
            enemyHealth.ResetHealth();

            // 활성화 하기전에 위치,회전을 먼저 변경 
            enemy.transform.SetPositionAndRotation(spawnPos, Quaternion.identity);
            enemy.gameObject.SetActive(true);
            return enemy;
        }

        // 재사용 대상이 없을 시 새로운 프리팹을 생성
        EnemyController newEnemy = Instantiate(selectedPrefab, spawnPos, Quaternion.identity, transform);
        selectedPool.Add(newEnemy);
        enemyPool.Add(newEnemy);
        RegisterEnemy(newEnemy);
        return newEnemy;
    }


    // 새로 생성된 몬스터의 사망 이벤트를 연결하는 메소드
    // 최초 생성 시 한번 등록하여 중복을 방지합니다
    void RegisterEnemy(EnemyController enemy)
    {
        Health enemyHealth = enemy.GetComponent<Health>();

        if (deathHandlers.ContainsKey(enemyHealth))
            return;

        // Ondied 이벤트 자체에는 누가 죽었는지 전달이 되지않으므로 현재 enemy를 기억하는 전용 콜백을 생성
        Action deathHandler = () => HandleEnemyDeath(enemy);
        deathHandlers.Add(enemyHealth, deathHandler);
        enemyHealth.OnDied += deathHandler;
    }

    // 관리하는 몬스터가 사망하면 생존 수를 감소시킨 후
    // 리스폰딜레이 이후 대체 몬스터를 생성하는 코루틴 실행
    void HandleEnemyDeath(EnemyController enemy)
    {
        if (isShuttingDown)
            return;

        aliveEnemyCount = Mathf.Max(0, aliveEnemyCount - 1);
        StartCoroutine(RespawnRoutine());
    }

    // 몬스터 사망 이후 딜레이만큼 기다린 다음 플레이어 또는 코어 주변에서 생성합닏
    IEnumerator RespawnRoutine()
    {
        if(reSpawnDeley > 0f)
           yield return new WaitForSeconds(reSpawnDeley);

        while (!isShuttingDown)
        {
            if (TrySpawnEnemy())
                yield break;

            yield return new WaitForSeconds(1f);
        }
    }

    // 실제 코어가 생성되거나 파괴되었을 때 CoreManager가 호출됩니다
    // 활성화 시 모든 몬스터 강화, 비활성화 시 코어 위치를 제거하고 모든 몬스터를 일반상태로 되돌립니다
    public void SetCoreState(Transform activeCore, bool active)
    {
        bool canActivate = active && activeCore != null && activeCore.gameObject.activeInHierarchy;

        isCoreActive = canActivate;
        coreCenter = isCoreActive ? activeCore : null;

        foreach(EnemyController enemy in enemyPool)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) 
                continue;

            Health enemyHealth = enemy.GetComponent<Health>();

            if (enemyHealth == null || enemyHealth.IsDead)
                continue;

            enemy.SetCoreBoosted(isCoreActive);
        }
    }

    // 1라운드 종료 시 일반 몬스터의 리젠을 중단하고
    // 모든 몬스터와 코어를 비활성화 시킵니다. 이 과정에서 경험치는 지급되지않습니다
    public void StopForBossTransition()
    {
        if (isShuttingDown)
            return;

        // 비활성화 과정에서 다른 콜백이 발생하여도 리젠을 하지않도록 설정
        isShuttingDown = true;
        StopAllCoroutines();

        SetCoreState(null, false);

        foreach(EnemyController enemy in enemyPool)
        {
            if (enemy != null)
                enemy.gameObject.SetActive(false);
        }
        aliveEnemyCount = 0;
        enabled = false;
    }


    private void OnDestroy()
    {
        isShuttingDown = true;

        foreach(KeyValuePair<Health, Action> pair in deathHandlers)
        {
            if (pair.Key != null)
                pair.Key.OnDied -= pair.Value;
        }

        deathHandlers.Clear();
    }

    // 인스펙터에서 최소, 최대 거리오 몬스터 수가 서로 모순되지않도록 보정
    void OnValidate()
    {
        playerMaxDistance = Mathf.Max(playerMaxDistance, playerMinDistance + 0.1f);
        coreMaxDistance = Mathf.Max(coreMaxDistance, coreMinDistance + 0.1f);
        maxEnemyCount = Mathf.Max(1, maxEnemyCount);
        enemyCount = Mathf.Clamp(enemyCount, 0, maxEnemyCount);
    }
}
