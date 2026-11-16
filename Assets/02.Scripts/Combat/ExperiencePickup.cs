using UnityEngine;
using UnityEngine.Android;

public class ExperiencePickup : MonoBehaviour
{
    /// <summary>
    /// 경험치 알갱이를 플레이어의 흡수 지점으로 이동시키고 도착시 한번 지급
    /// </summary>


    [Header("경험치 관련")]
    [SerializeField] PlayerExperience playerExp;        // 경험치를 받을 컴포넌트
    [SerializeField] Health playerHealth;               // 플레이어가 사망시 경험치가 흡수되지않게
    [SerializeField] Transform absorbPoint;             // 경험치가 플레이어에게 흡수당하는 지점
    [SerializeField, Min(1)] int expAmount = 10;        
    [SerializeField, Min(0.1f)] float startSpeed = 3f;  // 알갱이 스피드
    [SerializeField, Min(0f)] float acceleration = 15f;
    [SerializeField, Min(0.1f)] float absorbDistance = 0.15f;
    float currentSpeed;     // 비행 중 가속되는 현재속도
    bool collected;         // 같은 알갱이의 중복 지급 방지

    [Header("경험치 연출")]
    [SerializeField, Min(0.01f)] float scatterDur = 0.25f;      // 흩어지는 시간
    [SerializeField, Min(0f)] float scatterDis = 0.8f;          // 수평 이동거리
    [SerializeField, Min(0f)] float scatterHeight = 0.4f;       // 포물선의 최대 높이
    float scatterTimer;     // 이번 흩어짐의 경과 시간
    Vector3 scatterStart;   // 흩어짐 생성 위치
    Vector3 scatterEnd;     // 흩어짐 끝날 위치


    // 최초 활성화 시 지급 기록과 속도를 초기화
    // 경험치가 흩어지는 연출로서 생성 위치에서부터 퍼질 목적지를 계산
    // Initialize가 위치를 설정한 뒤 활성화하므로 새 생성 위치를 사용
    private void OnEnable()
    {
        collected = false;
        currentSpeed = startSpeed;
        scatterTimer = 0f;
        scatterStart = transform.position;
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle));
        scatterEnd = scatterStart + dir * scatterDis;
    }

    // 프레임마다 플레이어 위치를 향해 가속 비행
    // 흩어지는 연출동안은 경험치를 지급하지않고, 흡수 단계에서 지급
    void Update()
    {
        if (collected)
            return;
        if(playerExp == null || playerHealth == null || absorbPoint == null || playerHealth.IsDead || !playerExp.gameObject.activeInHierarchy)
        {
            gameObject.SetActive(false);
            return;
        }
        if (Time.deltaTime <= 0f)
            return;

        if(scatterTimer < scatterDur)
        {
            scatterTimer += Time.deltaTime;
            float t = Mathf.Clamp01(scatterTimer / scatterDur);
            // 수평 이동 가운데에서 가장 높아지는 포물선 높이를 더해 계산
            float height = 4f * t * (1f - t) * scatterHeight;
            transform.position = Vector3.Lerp(scatterStart, scatterEnd, t) + Vector3.up * height;
            return;
        }

        currentSpeed += acceleration * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, absorbPoint.position, currentSpeed * Time.deltaTime);
        if ((transform.position - absorbPoint.position).sqrMagnitude <= absorbDistance * absorbDistance)
            Collect();
    }

    // 도착 시 기록을 잠궈 중복 획득을 방지
    // 경험치를 지급한 뒤 알갱이 비활성화
    void Collect()
    {
        if (collected)
            return;
        collected = true;
        playerExp.AddExperience(expAmount);
        gameObject.SetActive(false);
    }

    public void Initialize(PlayerExperience target, Health health, Transform point, int amount, Vector3 pos)
    {
        gameObject.SetActive(false);

        playerExp = target;
        playerHealth = health;
        absorbPoint = point;
        expAmount = amount;
        transform.position = pos;

        gameObject.SetActive(true);
    }
}
