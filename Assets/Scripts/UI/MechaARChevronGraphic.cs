using UnityEngine;
using UnityEngine.UI;

/// <summary>Font-independent navigation chevron inside its existing touch target.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MechaARChevronGraphic : MaskableGraphic
{
    [SerializeField] bool pointLeft;
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect r = GetPixelAdjustedRect();
        float h = Mathf.Min(r.height, r.width * 1.5f), w = h * .5f;
        float direction = pointLeft ? -1f : 1f;
        Vector2 a = r.center + new Vector2(-w * .5f * direction, h * .5f);
        Vector2 b = r.center + new Vector2(w * .5f * direction, 0);
        Vector2 c = r.center + new Vector2(-w * .5f * direction, -h * .5f);
        Segment(mesh, a, b, Mathf.Max(2, h * .1f));
        Segment(mesh, b, c, Mathf.Max(2, h * .1f));
    }
    void Segment(VertexHelper mesh, Vector2 a, Vector2 b, float width)
    {
        Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
        int start = mesh.currentVertCount;
        mesh.AddVert(a - normal, color, Vector2.zero);
        mesh.AddVert(a + normal, color, Vector2.zero);
        mesh.AddVert(b + normal, color, Vector2.zero);
        mesh.AddVert(b - normal, color, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start, start + 2, start + 3);
    }
}
