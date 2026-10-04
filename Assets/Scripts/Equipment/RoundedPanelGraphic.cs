using UnityEngine;
using UnityEngine.UI;
/// <summary>Rounded card without runtime texture allocations.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class RoundedPanelGraphic : MaskableGraphic
{
    [SerializeField] float radius = 28f;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        float corner = Mathf.Min(radius, Mathf.Min(r.width, r.height) * .5f);
        vh.AddVert(r.center, color, Vector2.zero);
        const int segments = 8;
        for (int c = 0; c < 4; c++)
        {
            Vector2 center = new Vector2(c == 0 || c == 3 ? r.xMax - corner : r.xMin + corner, c < 2 ? r.yMax - corner : r.yMin + corner);
            for (int s = 0; s <= segments; s++)
            {
                float a = (c * 90f + s * 90f / segments) * Mathf.Deg2Rad;
                vh.AddVert(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * corner, color, Vector2.zero);
            }
        }
        int perimeter = 4 * (segments + 1);
        for (int i = 0; i < perimeter; i++) vh.AddTriangle(0, i + 1, (i + 1) % perimeter + 1);
    }
}
