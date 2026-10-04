using UnityEngine;
using UnityEngine.UI;

/// <summary>Resolution-independent counterclockwise reset arrow; no font or texture dependency.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ResetViewIcon : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = GetPixelAdjustedRect();
        float radius = Mathf.Min(rect.width, rect.height) * .31f;
        float thickness = radius * .23f;
        Vector2 center = rect.center;
        const int segments = 40;
        // Ends in the upper-left, with the arrowhead following counterclockwise motion.
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(195f, 495f, i / (float)segments) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            mesh.AddVert(center + direction * (radius - thickness * .5f), color, Vector2.zero);
            mesh.AddVert(center + direction * (radius + thickness * .5f), color, Vector2.zero);
            if (i == 0) continue;
            int n = i * 2;
            mesh.AddTriangle(n - 2, n - 1, n);
            mesh.AddTriangle(n - 1, n + 1, n);
        }
        float end = 135f * Mathf.Deg2Rad;
        Vector2 radial = new Vector2(Mathf.Cos(end), Mathf.Sin(end));
        Vector2 tangent = new Vector2(-radial.y, radial.x);
        Vector2 tip = center + radial * radius + tangent * radius * .27f;
        Vector2 back = center + radial * radius - tangent * radius * .24f;
        int start = mesh.currentVertCount;
        mesh.AddVert(tip, color, Vector2.zero);
        mesh.AddVert(back + radial * radius * .37f, color, Vector2.zero);
        mesh.AddVert(back - radial * radius * .37f, color, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
    }
}
