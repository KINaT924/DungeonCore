using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 가장자리의 타원형 붉은 원호를 UGUI 메시로 그립니다.
/// 화면비가 바뀌어도 중심 조준 영역을 가리지 않으며
/// 방향별 밝기를 합치므로 여러 적이 같은 방향에 있어도 중복으로 불투명해지지 않습니다
/// </summary>

[RequireComponent(typeof(CanvasRenderer))]
public sealed class ThreatArcGraphic : MaskableGraphic
{
    [Range(15f, 90f)] public float halfAngle = 35f;
    [Range(.7f, .99f)] public float edgeRadius = .96f;
    [Range(8f, 80f)] public float glowWidth = 36f;
    [Range(1f, 6f)] public float lineWidth = 2f;
    // 매 프레임 임시 List/배열 생성 없이 화면 둘레 192구간의 위험 밝기를 재사용합니다
    const int Segments = 192;
    readonly float[] strengths = new float[Segments + 1];
    float pulse;

    //새 위험 검사를 시작하기 전에 이전 프레임의 방향만 비웁니다
    public void ClearDirections()
    { 
        System.Array.Clear(strengths, 0, strengths.Length);
    }

    // 지정한 방향의 원호를 더합니다
    // 0도는 오른쪽, 90도는 위, -90도는 뒤쪽/아래입니다
    public void AddDirection(float degrees)
    {
        for (int i = 0; i <= Segments; i++)
        {
            float t = Mathf.Clamp01(1f - Mathf.Abs(Mathf.DeltaAngle(i * 360f / Segments, degrees)) / halfAngle);
            strengths[i] = Mathf.Max(strengths[i], Mathf.SmoothStep(0f, 1f, t));
        }
    }

    //위험 방향을 모두 합친 뒤 한 번만 메시 갱신을 요청
    public void Refresh(float brightness)
    { 
        pulse = brightness; SetVerticesDirty();
    }

    // UGUI의 메시 갱신 때 바깥 번짐,밝은 가는 선,안쪽 번짐을 겹쳐 원호를 그립니다
    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();

        if (pulse <= 0f)
            return;

        Rect rect = rectTransform.rect;
        Vector2 radius = new Vector2(rect.width, rect.height) * (.5f * edgeRadius);

        for (int i = 0; i < Segments; i++)
        {
            if (strengths[i] <= 0 && strengths[i + 1] <= 0)
                continue;

            float a = i * Mathf.PI * 2f / Segments, b = (i + 1) * Mathf.PI * 2f / Segments;
            Band(helper, rect.center, radius, a, b, -glowWidth * .35f, 0, 0, .3f, i);
            Band(helper, rect.center, radius, a, b, 0, lineWidth, 1, 1, i);
            Band(helper, rect.center, radius, a, b, lineWidth, glowWidth * .35f, .4f, .12f, i);
            Band(helper, rect.center, radius, a, b, glowWidth * .35f, glowWidth, .12f, 0, i);
        }
    }

    // 인접한 각도 사이를 사각형 띠로 연결하고 안팎의 투명도를 보간
    void Band(VertexHelper helper, Vector2 center, Vector2 radius, float a, float b,
        float outer, float inner, float outerAlpha, float innerAlpha, int segment)
    {
        int start = helper.currentVertCount;

        Vertex(helper, center, radius, a, outer, outerAlpha * strengths[segment]);
        Vertex(helper, center, radius, b, outer, outerAlpha * strengths[segment + 1]);
        Vertex(helper, center, radius, b, inner, innerAlpha * strengths[segment + 1]);
        Vertex(helper, center, radius, a, inner, innerAlpha * strengths[segment]);

        helper.AddTriangle(start, start + 1, start + 2);
        helper.AddTriangle(start + 2, start + 3, start);
    }

    // 타원 위의 위치와 알파를 한 정점으로 전달
    // 모든 좌표는 Canvas 기준 크기입니다
    void Vertex(VertexHelper helper, Vector2 center, Vector2 radius, float angle, float inset, float alpha)
    {
        Vector2 position = center + new Vector2(Mathf.Cos(angle) * (radius.x - inset), Mathf.Sin(angle) * (radius.y - inset));
        Color tint = color; 
        tint.a *= alpha * pulse;
        helper.AddVert(position, tint, Vector2.zero);
    }
}
