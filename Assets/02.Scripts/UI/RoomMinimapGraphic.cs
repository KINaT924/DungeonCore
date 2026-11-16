using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 던전의 방별 실제 바닥 타일 윤곽을 UI 메시로 그립니다
/// 모든 방은 같은 지도 아래 함께 표시됩니다
/// </summary>
/// 
[RequireComponent(typeof(CanvasRenderer))]
public class RoomMinimapGraphic : MaskableGraphic
{
    [Tooltip("월드 X/Z 좌표의 바닥 타일 사각형. x/y는 최소 X/Z, width/height는 크기입니다.")]
    public Rect[] tiles = System.Array.Empty<Rect>();
    public bool circular;
    public Vector2 circleCenter;
    [Min(.1f)] public float circleRadius = 24;
    public Vector2 drawOrigin;
    public Color outlineColor = new Color(.4f,.57f,.64f,.85f);
    [Min(.01f)] public float outlineWidth = .18f;

    // Canvas가 다시 그려질 때 채움과 외곽선만 생성
    // 인접 타일 경계는 숨겨 방이 한 덩어리로 보입니다
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (circular)
        { 
            Circle(vh);
            return;
        }

        foreach (Rect tile in tiles)
        {
            Quad(vh, tile, color);
            Vector2 center = tile.center;
            if (!Contains(center + Vector2.left * (tile.width / 2 + .1f)))
                Quad(vh,new Rect(tile.xMin,tile.yMin,outlineWidth,tile.height),outlineColor);
            if (!Contains(center + Vector2.right * (tile.width / 2 + .1f)))
                Quad(vh,new Rect(tile.xMax-outlineWidth,tile.yMin,outlineWidth,tile.height),outlineColor);
            if (!Contains(center + Vector2.down * (tile.height / 2 + .1f)))
                Quad(vh,new Rect(tile.xMin,tile.yMin,tile.width,outlineWidth),outlineColor);
            if (!Contains(center + Vector2.up * (tile.height / 2 + .1f)))
                Quad(vh,new Rect(tile.xMin,tile.yMax-outlineWidth,tile.width,outlineWidth),outlineColor);
        }
    }

    // 월드 X/Z 점이 이 방 바닥 안인지 판정
    // 문턱에서는 작은 오차를 허용
    public bool Contains(Vector2 point)
    {
        if (circular)
            return (point - circleCenter).sqrMagnitude <= circleRadius * circleRadius;

        foreach (Rect tile in tiles)
            if (tile.Contains(point))
                return true;

        return false;
    }

    // 임의의 점에서 가장 가까운 바닥까지 거리 제곱을 반환하여 문턱,방 경계의 공백을 처리
    public float DistanceSquared(Vector2 point)
    {
        float best = float.PositiveInfinity;
        foreach (Rect tile in tiles)
        {
            Vector2 nearest = new Vector2(Mathf.Clamp(point.x,tile.xMin,tile.xMax),Mathf.Clamp(point.y,tile.yMin,tile.yMax));
            best = Mathf.Min(best,(nearest-point).sqrMagnitude);
        }
        return best;
    }

    // 방 전체가 화면에 들어오게 배율을 구할 때 사용하는 월드 경계
    public Rect WorldBounds()
    {
        if (circular)
            return new Rect(circleCenter - Vector2.one * circleRadius, Vector2.one * (circleRadius * 2));

        if (tiles.Length == 0)
            return new Rect();
        Rect result = tiles[0];

        foreach (Rect tile in tiles)
            result = Rect.MinMaxRect(Mathf.Min(result.xMin,tile.xMin),Mathf.Min(result.yMin,tile.yMin),
                Mathf.Max(result.xMax,tile.xMax),Mathf.Max(result.yMax,tile.yMax));
        return result;
    }

    // 원형 보스방의 바닥과 테두리를 96분할로 그립니다
    // 타일 사각형으로 방을 근사하지 않습니다
    void Circle(VertexHelper vh)
    {
        const int count = 96;
        vh.AddVert(circleCenter - drawOrigin, color, Vector2.zero);

        for (int i = 0; i <= count; i++)
        {
            float a = i * Mathf.PI * 2 / count;
            Vector2 direction = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            vh.AddVert(circleCenter - drawOrigin + direction * Mathf.Max(0, circleRadius - outlineWidth), color, Vector2.zero);
            vh.AddVert(circleCenter - drawOrigin + direction * circleRadius, outlineColor, Vector2.zero);
        }

        for (int i = 0; i < count; i++)
        {
            int inside = 1 + i * 2, next = inside + 2;
            vh.AddTriangle(0, next, inside);
            vh.AddTriangle(inside, next, inside + 1); vh.AddTriangle(inside + 1, next, next + 1);
        }
    }

    // UI 사각형을 정점 네 개로 추가
    // 좌표는 부모의 배율로 월드 단위에서 UI 단위로 변환
    void Quad(VertexHelper vh, Rect r, Color tint)
    {
        r.position -= drawOrigin;
        int start = vh.currentVertCount;
        vh.AddVert(new Vector3(r.xMin,r.yMin),tint,Vector2.zero);
        vh.AddVert(new Vector3(r.xMin,r.yMax),tint,Vector2.zero);
        vh.AddVert(new Vector3(r.xMax,r.yMax),tint,Vector2.zero);
        vh.AddVert(new Vector3(r.xMax,r.yMin),tint,Vector2.zero);
        vh.AddTriangle(start,start+1,start+2);
        vh.AddTriangle(start,start+2,start+3);
    }
}
