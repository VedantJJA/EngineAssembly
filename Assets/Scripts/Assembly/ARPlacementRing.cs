using UnityEngine;
namespace EngineAssembly
{
    // Retains the component identity used by existing scenes; the guide is now a
    // square and is displayed only after a real Vuforia Ground Plane hit.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ARPlacementRing:UnityEngine.UI.MaskableGraphic
    {
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();float half=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.42f;
            Border(mesh,half+3,6,new Color(0,0,0,.7f));Border(mesh,half,3,color);
        }
        static void Border(UnityEngine.UI.VertexHelper mesh,float half,float width,Color tint)
        {
            Quad(mesh,new Vector2(-half-width,-half-width),new Vector2(-half+width,half+width),tint);
            Quad(mesh,new Vector2(half-width,-half-width),new Vector2(half+width,half+width),tint);
            Quad(mesh,new Vector2(-half+width,half-width),new Vector2(half-width,half+width),tint);
            Quad(mesh,new Vector2(-half+width,-half-width),new Vector2(half-width,-half+width),tint);
        }
        static void Quad(UnityEngine.UI.VertexHelper mesh,Vector2 low,Vector2 high,Color tint)
        {
            int n=mesh.currentVertCount;mesh.AddVert(low,tint,Vector2.zero);mesh.AddVert(new Vector2(low.x,high.y),tint,Vector2.zero);mesh.AddVert(high,tint,Vector2.zero);mesh.AddVert(new Vector2(high.x,low.y),tint,Vector2.zero);mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n,n+2,n+3);
        }
    }
}
