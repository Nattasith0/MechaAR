using UnityEngine;
using UnityEngine.UI;

/// <summary>Original lightweight circuit-board illustration; no textures, cameras or per-frame allocations.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MechaARArtworkGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Color ink = new Color32(20,43,41,255), sage = new Color32(91,128,107,255), lime = new Color32(212,242,104,255);
        for (int i=0;i<8;i++)
        {
            float y=.12f+i*.10f;
            Line(vh,new Vector2(.04f,y),new Vector2(.24f,y),sage, .003f);
            Line(vh,new Vector2(.24f,y),new Vector2(.34f,y+.1f),sage,.003f);
            Line(vh,new Vector2(.76f,y),new Vector2(.96f,y),sage,.003f);
        }
        Quad(vh,new Vector2(.29f,.15f),new Vector2(.72f,.24f),new Vector2(.82f,.69f),new Vector2(.39f,.60f),ink);
        Quad(vh,new Vector2(.29f,.22f),new Vector2(.72f,.31f),new Vector2(.82f,.76f),new Vector2(.39f,.67f),sage);
        Quad(vh,new Vector2(.45f,.36f),new Vector2(.63f,.40f),new Vector2(.68f,.60f),new Vector2(.50f,.56f),ink);
        Quad(vh,new Vector2(.50f,.42f),new Vector2(.58f,.44f),new Vector2(.62f,.55f),new Vector2(.54f,.53f),lime);
        for(int i=0;i<9;i++)
        {
            float x=.40f+i*.041f;
            Line(vh,new Vector2(x,.68f+(x-.4f)*.21f),new Vector2(x-.015f,.60f+(x-.4f)*.21f),lime,.011f);
            Line(vh,new Vector2(x-.06f,.24f+(x-.4f)*.21f),new Vector2(x-.045f,.31f+(x-.4f)*.21f),lime,.011f);
        }
        Quad(vh,new Vector2(.34f,.36f),new Vector2(.43f,.38f),new Vector2(.45f,.50f),new Vector2(.36f,.48f),new Color32(232,237,218,255));
        // Open scan brackets frame the board without implying a real AR preview.
        foreach(var p in new[]{new Vector2(.14f,.12f),new Vector2(.86f,.12f),new Vector2(.14f,.89f),new Vector2(.86f,.89f)})
        {
            float dx=p.x<.5f?.10f:-.10f, dy=p.y<.5f?.12f:-.12f;
            Line(vh,p,p+new Vector2(dx,0),lime,.007f); Line(vh,p,p+new Vector2(0,dy),lime,.007f);
        }
    }
    Vector2 Map(Vector2 p){var r=rectTransform.rect;return new Vector2(r.xMin+p.x*r.width,r.yMin+p.y*r.height);}
    void Quad(VertexHelper v,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color tint)
    {int n=v.currentVertCount;v.AddVert(Map(a),tint,Vector2.zero);v.AddVert(Map(b),tint,Vector2.zero);v.AddVert(Map(c),tint,Vector2.zero);v.AddVert(Map(d),tint,Vector2.zero);v.AddTriangle(n,n+1,n+2);v.AddTriangle(n,n+2,n+3);}
    void Line(VertexHelper v,Vector2 a,Vector2 b,Color tint,float width)
    {Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;Quad(v,a+n,b+n,b-n,a-n,tint);}
}
