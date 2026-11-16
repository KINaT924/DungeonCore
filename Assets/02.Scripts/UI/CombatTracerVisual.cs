using UnityEngine;

/// <summary>
/// 피해 판정과 독립된 짧은 이동 탄도를 다루는 스크립트
/// 실제 Ray 종점까지만 이동하고 착탄 연출을 재생
/// </summary>
public sealed class CombatTracerVisual : MonoBehaviour
{
    public LineRenderer outerLine;
    public LineRenderer innerLine;

    Vector3 origin, destination, direction, normal;
    float distance, elapsed, duration, tail;
    GameObject impact;
    bool impacted;

    // 실제 Ray 시작점, 종점과 착탄 정보를 받아 이번 시각 연출만 초기화
    public void Initialize(Vector3 start, Vector3 end, Color color, float width, GameObject hitFx, Vector3 hitNormal)
    {
        origin = start;
        destination = end;
        normal = hitNormal;
        impact = hitFx;

        distance = Vector3.Distance(start, end);
        direction = distance > .0001f ? (end - start) / distance : Vector3.forward;
        duration = Mathf.Clamp(distance / 180f, .035f, .16f);
        tail = Mathf.Min(distance, 1.8f);

        outerLine.startWidth = width * 1.8f; outerLine.endWidth = width * .4f;
        innerLine.startWidth = width; innerLine.endWidth = width * .18f;
        innerLine.startColor = innerLine.endColor = color;
        Draw(0f);
    }

    // 배율 시간으로 밝은 짧은 궤적을 이동
    // 카드 선택 중에는 이동, 착탄 예약이 일시정지
    void Update()
    {
        elapsed += Time.deltaTime;
        float head = distance * elapsed / duration;
        Draw(head);

        if (!impacted && head >= distance)
        {
            impacted = true;
            if (impact != null)
            {
                Vector3 outward = normal.sqrMagnitude > .0001f ? normal.normalized : -direction;
                // 착탄을 표면 바깥으로 조금 띄워 별, 고리가 벽과 겹쳐 잘리지 않게 설정
                Instantiate(impact, destination + outward * .025f, Quaternion.LookRotation(outward));
            }
        }
        if (elapsed >= duration + .045f)
            Destroy(gameObject);
    }

    // 탄도의 앞, 뒤를 실제 종점 안으로 제한하여 벽을 뚫는 선을 표시하지 않습니다
    void Draw(float head)
    {
        float front = Mathf.Clamp(head, 0f, distance);
        float back = Mathf.Clamp(head - tail, 0f, distance);
        Vector3 a = origin + direction * back;
        Vector3 b = origin + direction * front;
        outerLine.SetPosition(0, a); outerLine.SetPosition(1, b);
        innerLine.SetPosition(0, a); innerLine.SetPosition(1, b);
    }
}
