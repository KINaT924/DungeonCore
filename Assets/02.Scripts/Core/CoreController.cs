using UnityEngine;

[RequireComponent(typeof(Health))]
public class CoreController : MonoBehaviour
{
    /// <summary>
    /// 코어 관련 컴포넌트
    /// 코어의 활성화or비활성화 / 체력 / 파괴 처리를 담당
    /// 코어 활성화 시 EnemySpawner에 코어 정보를 전달하여 전구간 몬스터 강화, 파괴 시 전구간 몬스터 강화상태 해제
    /// 코어 파괴 시 경험치풀에서 보상 생성을 요청, 실제 지급은 알갱이 흡수 시 처리
    /// </summary>

    [Header("레퍼런스")]
    [SerializeField] Health health;
    [SerializeField] EnemySpawner enemySpawner;
    [SerializeField] ExperiencePool expPool;

    [Header("코어 파괴 보상")]
    [SerializeField, Min(0)] int totalExp = 100;                        // 코어 파괴 시 획득 경험치 총량

    [Header("파괴 연출")]
    [SerializeField] ParticleSystem breakFxPrefab;                      // 코어 파괴 시 재생할 파티클 프리팹
    [SerializeField] Vector3 breakFxOffset = new Vector3(0f, 1f, 0f);   // 파티클 재생 위치 오프셋


    [Header("카툰 파괴 연출 - 전체 루트")]
    [SerializeField] GameObject cartoonBreakFx; // 여러 자식 파티클과 수명 관리를 함께 생성
    [SerializeField] Transform destructionAnchor; // 새 외형의 실제 파괴 중심

    bool isDestroyed;               // 코어 파괴 처리 중복 실행 방지
    bool isRegistered;              // 코어가 스포너에 활성 상태인지 기록
    public bool IsDestroyed => isDestroyed;      // 외부에서 현재 코어가 파괴된 상태인지 확인 할 수 있는 프로퍼티


    // 오브젝트가 최초로 초기화될 때 호출
    private void Awake()
    {
        if (health == null)
            health = GetComponent<Health>();
    }

    // 코어가 활성화 될때마다 호출
    private void OnEnable()
    {
        if (health == null || enemySpawner == null)
        {
            Debug.LogError("참조연결을 확인하세요. CoreController", this);
            return;
        }
        isDestroyed = false;

        // 이벤트 중복 구독을 방지
        health.OnDied -= HandleCoreDestroyed;
        health.ResetHealth();
        health.OnDied += HandleCoreDestroyed;

        enemySpawner.SetCoreState(transform, true);
        isRegistered = true;

        Debug.Log("코어 활성화, 몬스터강화", this);
    }

    // 체력이 0이 되면 호출
    // 코어 파괴를 기록하고 몬스터 강화 해제
    void HandleCoreDestroyed()
    {
        if (isDestroyed)
            return;
        isDestroyed = true;
        PlayBreakEffect();
        DropExperience();
        ReleaseCoreState();
        

        Debug.Log("코어 파괴, 몬스터 강화 해제", this);
        gameObject.SetActive(false);
    }
    
    // 코어 파괴 시 비활성화전에 호출
    // 부모 없이 효과를 생성하여 코어가 꺼저있어도 빛과 파편이 끝까지 재생하는 역할
    void PlayBreakEffect()
    {
        // 새 FX는 루트에 ParticleSystem이 없으므로 GameObject 전체를 월드에 생성
        // 기존 ParticleSystem 참조는 호환을 위해 보존
        if (cartoonBreakFx != null)
        {
            Vector3 center = destructionAnchor != null ? destructionAnchor.position : transform.position + breakFxOffset;
            Instantiate(cartoonBreakFx, center, Quaternion.identity);
            return;
        }
        if(breakFxPrefab == null)
            return;

        Vector3 pos = transform.position + breakFxOffset;
        Instantiate(breakFxPrefab, pos, Quaternion.identity);
    }

    // 코어 파괴 후 처리에서 비활성화 전에 호출
    // 경험치 보상을 10 단위로 나누어 경험치 알갱이 생성
    void DropExperience()
    {
        // 보상이 0이면 경험치 풀에 요청하지 않음
        if(totalExp <= 0)
            return;

        // 연결이 빠진 경우 로그에러 호출
        if(expPool == null)
        {
            Debug.LogError("참조연결을 확인하세요. CoreController", this);
            return;
        }

        int remaining = totalExp;

        while (remaining > 0)
        {
            int amout = Mathf.Min(10, remaining);

            // 같은 위치에 겹치지 않게 생성
            // 흡수 연출을 코어 원점보다 조금 위에서 시작
            Vector2 offset = Random.insideUnitCircle * 0.4f;
            Vector3 pos = transform.position + new Vector3(offset.x, 0.5f, offset.y);

            expPool.Spawn(pos, amout);
            remaining -= amout;
        }
    }
    // 코어가 파괴되어 비활성화 상태일 때 호출
    private void OnDisable()
    {
        if(health != null)
            health.OnDied -= HandleCoreDestroyed; ;
        ReleaseCoreState();
    }

    // 코어가 등록한 스폰 상태를 해제하는 메소드
    void ReleaseCoreState()
    {
        if (!isRegistered)
            return;
        isRegistered = false;

        if (enemySpawner != null)
            enemySpawner.SetCoreState(null, false);
    }
}
