using UnityEngine;

[RequireComponent(typeof(Health))]
[DisallowMultipleComponent]
public class EnemyExperienceDrop : MonoBehaviour
{
    /// <summary>
    /// 몬스터 사망 이벤트를 받아와 총 경험치를 분할하여 풀에 생성을 요청하는 스크립트
    /// 체력 AI와 보상 처리를 분리하며, 몬스터 재사용 시 드롭 기록을 초기화합니다
    /// </summary>


    [SerializeField, Min(0)] int totalExp = 30;    // 몬스터 한마리의 경험치 드랍양
    Health health;
    ExperiencePool expPool;     // 풀 참조는 스포너가 전달
    bool dropped;               // 현재 생명에서 드롭했는지 기록

    // 최초 생성 시 같은 오브젝트에서 Health 스크립트를 찾아 사망 이벤트 연결을 춘비
    private void Awake()
    {
        health = GetComponent<Health>();
    }
    // 활성화마다 드롭 기록을 초기화한 후 현재 오브젝트의 사망 소식을 전달
    private void OnEnable()
    {
        dropped = false;
        health.OnDied += HandleDeath;
    }

    // 비활성화 시 전달을 취소하여 재사용 시 중복 호출하는것을 방지
    private void OnDisable()
    {
        health.OnDied -= HandleDeath;
    }
    
    // 스포너가 생성, 재사용 직후 호출됩니다 알갱이를 관리하는 역할
    public void Initialize(ExperiencePool pool)
    {
        expPool = pool;
    }    


    // 사망 시 한번 실행, 10단위의 경험치로 분할합니다
    void HandleDeath()
    {
        if (dropped)
            return;
        dropped = true;
        if(expPool == null)
        {
            Debug.LogError("EnemySpwaner의 참조 연결을 확인하세요", this);
            return;
        }
        int remaining = totalExp;
        while(remaining > 0)
        {
            int amount = Mathf.Min(10, remaining);
            Vector2 offset = Random.insideUnitCircle * 0.4f;
            Vector3 pos = transform.position + new Vector3(offset.x, 0.5f, offset.y);
            expPool.Spawn(pos, amount);
            remaining -= amount;
        }
    }
}
