using UnityEngine;

namespace EngineAssembly
{
    // Real-world dimensions stay independent of CAD presentation/gesture scale.
    public sealed class ARWorkbench : MonoBehaviour
    {
        public Transform Surface { get; private set; }
        public float Height { get; private set; }
        public const float Width=1.8f,Depth=1.1f;
        Material wood,steel,rubber;
        public static ARWorkbench Create(Transform anchor,float height)
        {
            var root=new GameObject("AR workbench");root.transform.SetParent(anchor,false);
            var bench=root.AddComponent<ARWorkbench>();bench.Height=height;
            bench.wood=bench.Material("Workbench top",new Color(.73f,.64f,.48f),.22f);
            bench.steel=bench.Material("Workbench frame",new Color(.32f,.37f,.4f),.42f,.65f);
            bench.rubber=bench.Material("Workbench feet",new Color(.12f,.14f,.16f),.15f);
            bench.Box("Tabletop",new Vector3(0,height-.03f,0),new Vector3(Width,.06f,Depth),bench.wood);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
            {
                var p=new Vector3(x*(Width*.5f-.1f),height*.5f,z*(Depth*.5f-.1f));
                bench.Box("Steel leg",p,new Vector3(.07f,height-.06f,.07f),bench.steel);
                bench.Box("Rubber foot",new Vector3(p.x,.025f,p.z),new Vector3(.1f,.05f,.1f),bench.rubber);
            }
            bench.Box("Front rail",new Vector3(0,height*.3f,-Depth*.5f+.1f),new Vector3(Width-.2f,.05f,.05f),bench.steel);
            bench.Box("Back rail",new Vector3(0,height*.3f,Depth*.5f-.1f),new Vector3(Width-.2f,.05f,.05f),bench.steel);
            bench.Surface=new GameObject("Workbench surface frame").transform;bench.Surface.SetParent(root.transform,false);bench.Surface.localPosition=Vector3.up*height;
            return bench;
        }
        Material Material(string name,Color color,float smoothness,float metal=0)
        {var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smoothness);m.SetFloat("_Metallic",metal);return m;}
        void Box(string name,Vector3 center,Vector3 size,Material material)
        {var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;box.transform.SetParent(transform,false);box.transform.localPosition=center;box.transform.localScale=size;box.GetComponent<Renderer>().sharedMaterial=material;}
        void OnDestroy(){if(wood)Destroy(wood);if(steel)Destroy(steel);if(rubber)Destroy(rubber);}
    }
}
