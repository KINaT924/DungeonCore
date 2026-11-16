using UnityEngine;

/// <summary>
/// 저장된 마법진/충격파 프리팹의 회전, 성장, 소멸만 담당
/// 공격은 해당 스크립트에서 다루지 않음
/// </summary>

public class BossSpellVisual : MonoBehaviour
{
    public float lifetime = 0;
    public float spin = 24;
    public bool expanding;
    float age;
    float forwardOffset;

    Vector3 baseScale;
    Transform anchor, facing;
    LineRenderer[] lines;
    Color[] colors;


    void Awake()
    {
        // 프리팹 원래 크기와 선 색을 보관하여 공유 재질을 변경하지 않고 페이드
        baseScale = transform.localScale;
        lines = GetComponentsInChildren<LineRenderer>();
        colors = new Color[lines.Length];

        for (int i = 0; i < lines.Length; i++)
            colors[i] = lines[i].startColor;
    }

    void Start()
    {
        //생성자가 지정한 폭발 반경/경고 크기를 첫 갱신 전에 보관
        baseScale = transform.localScale;
    }

    //손 앞 충전 마법진을 실제 손에 따라가게 하는 메소드
    //보스 스케일의 영향을 받지 않는 월드 크기를 유지
    public void Follow(Transform target, Transform direction, float offset)
    {
        anchor = target;
        facing = direction;
        forwardOffset = offset;
    }

    // 게임 시간으로 외형만 갱신
    // lifetime = 0은 시전 컨트롤러가 직접 제거할 때까지 유지
    void LateUpdate()
    {
        age += Time.deltaTime;

        if (anchor != null)
            transform.position = anchor.position + facing.forward * forwardOffset;
        transform.Rotate(Vector3.forward, spin * Time.deltaTime, Space.Self);

        float scale = Mathf.Lerp(.3f, 1, Mathf.SmoothStep(0, 1, age / .2f));
        if (expanding)
            scale *= 1 + age * 1.8f;
        transform.localScale = baseScale * scale;

        float alpha = lifetime > 0 ? Mathf.Clamp01((lifetime - age) / .4f) : 1;

        for (int i = 0; i < lines.Length; i++)
        { 
            Color color = colors[i];
            color.a *= alpha;
            lines[i].startColor = lines[i].endColor = color;
        }

        if (lifetime > 0 && age >= lifetime)
            Destroy(gameObject);
    }
}
