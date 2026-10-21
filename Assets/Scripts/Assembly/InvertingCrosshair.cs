using UnityEngine;
namespace EngineAssembly
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InvertingCrosshair : UnityEngine.UI.MaskableGraphic
    {
        Material invert;
        protected override void Awake(){base.Awake();raycastTarget=false;var saved=Resources.Load<Material>("InvertingCrosshair");var shader=saved?saved.shader:Shader.Find("EngineAssembly/UI/Inverting Crosshair");if(shader){invert=new Material(shader);material=invert;}color=Color.white;}
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper helper)
        {helper.Clear();Bar(helper,new Rect(-10,-1,20,2));Bar(helper,new Rect(-1,-10,2,9));Bar(helper,new Rect(-1,1,2,9));}
        static void Bar(UnityEngine.UI.VertexHelper helper,Rect r)
        {int i=helper.currentVertCount;helper.AddVert(new Vector3(r.xMin,r.yMin),Color.white,Vector2.zero);helper.AddVert(new Vector3(r.xMin,r.yMax),Color.white,Vector2.zero);helper.AddVert(new Vector3(r.xMax,r.yMax),Color.white,Vector2.zero);helper.AddVert(new Vector3(r.xMax,r.yMin),Color.white,Vector2.zero);helper.AddTriangle(i,i+1,i+2);helper.AddTriangle(i+2,i+3,i);}
        protected override void OnDestroy(){if(invert)Destroy(invert);base.OnDestroy();}
    }
}
