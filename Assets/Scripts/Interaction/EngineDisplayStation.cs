using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace EngineAssembly
{
    // Fixed world controls share the same display actions as the on-screen settings.
    public sealed class EngineDisplayStation:MonoBehaviour
    {
        AssemblyManager manager;PCDisplayChapter display;Transform face,rocker;Renderer switchSurface;TextMeshPro state;
        readonly List<Material> materials=new List<Material>();Material blue,grey;
        public DisplaySceneControl ModeSwitch {get;private set;}
        public DisplaySceneControl ExplanationButton {get;private set;}
        public bool Available=>manager && manager.DisplayActive;
        public bool MotionMode=>display && display.MotionMode;
        public Vector3 FaceForward=>face.forward;
        public static EngineDisplayStation Create(PCDisplayChapter display,AssemblyManager manager)
        {
            var existing=manager.GetComponentInChildren<EngineDisplayStation>(true);if(existing)return existing;
            var root=new GameObject("Engine display control station");root.transform.SetParent(manager.transform,false);var station=root.AddComponent<EngineDisplayStation>();station.Build(display,manager);return station;
        }
        Material Paint(Color color){var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=color;materials.Add(material);return material;}
        void Build(PCDisplayChapter owner,AssemblyManager assembly)
        {
            display=owner;manager=assembly;grey=Paint(new Color(.16f,.20f,.25f));blue=Paint(new Color(.26f,.51f,.88f));var dark=Paint(new Color(.065f,.08f,.105f));var chrome=Paint(new Color(.46f,.5f,.56f));
            var bounds=manager.WorkpieceBounds(manager.AssemblyRoot);var camera=Camera.main;var toward=camera?Vector3.ProjectOnPlane(camera.transform.position-bounds.center,Vector3.up).normalized:Vector3.back;if(toward.sqrMagnitude<.1f)toward=Vector3.back;
            var side=Vector3.Cross(toward,Vector3.up);float floor=manager.GetComponent<WorkpieceLift>()?manager.GetComponent<WorkpieceLift>().GroundHeight:0;
            transform.SetPositionAndRotation(new Vector3(bounds.center.x,floor,bounds.center.z)+toward*(Mathf.Max(bounds.extents.x,bounds.extents.z)+.65f)+side*.78f,Quaternion.LookRotation(-toward,Vector3.up));
            Shape("Station foot",transform,PrimitiveType.Cube,new Vector3(0,.025f,0),new Vector3(.38f,.05f,.34f),dark);
            Shape("Station pedestal",transform,PrimitiveType.Cube,new Vector3(0,.49f,0),new Vector3(.075f,.94f,.075f),chrome);
            face=new GameObject("Display console face").transform;face.SetParent(transform,false);face.localPosition=Vector3.up*1.05f;face.localRotation=Quaternion.Euler(18,0,0);
            Shape("Console housing",face,PrimitiveType.Cube,Vector3.zero,new Vector3(.64f,.32f,.10f),dark,true);
            Label("ENGINE DISPLAY",new Vector3(0,.116f,-.058f),.045f,new Vector2(.58f,.06f));
            Shape("Selector bezel",face,PrimitiveType.Cube,new Vector3(-.13f,-.018f,-.061f),new Vector3(.265f,.122f,.025f),chrome);
            rocker=Shape("Blue display selector",face,PrimitiveType.Cube,new Vector3(-.13f,-.018f,-.093f),new Vector3(.215f,.082f,.058f),blue,true);switchSurface=rocker.GetComponent<Renderer>();
            ModeSwitch=rocker.gameObject.AddComponent<DisplaySceneControl>();ModeSwitch.Configure(this,rocker.GetComponent<Collider>(),false);
            Label("INFO       MOTION",new Vector3(-.13f,.066f,-.075f),.028f,new Vector2(.3f,.04f));
            state=Label("INFO",new Vector3(-.13f,-.115f,-.06f),.03f,new Vector2(.3f,.05f));
            var button=Shape("Explain cycle push button",face,PrimitiveType.Cylinder,new Vector3(.19f,-.018f,-.082f),new Vector3(.105f,.022f,.105f),blue,true);button.localRotation=Quaternion.Euler(90,0,0);
            var capsule=button.GetComponent<Collider>();capsule.enabled=false;Destroy(capsule);var buttonSurface=button.gameObject.AddComponent<MeshCollider>();buttonSurface.sharedMesh=button.GetComponent<MeshFilter>().sharedMesh;buttonSurface.convex=true;
            ExplanationButton=button.gameObject.AddComponent<DisplaySceneControl>();ExplanationButton.Configure(this,buttonSurface,true);button.gameObject.SetActive(false);
            Label("EXPLAIN",new Vector3(.19f,.066f,-.075f),.03f,new Vector2(.2f,.04f));Label("CYCLE",new Vector3(.19f,-.115f,-.06f),.03f,new Vector2(.2f,.05f));
        }
        public void Activate(bool explanation){if(explanation)display.ExplainEngine();else {display.SetMotionMode(!display.MotionMode);if(display.MotionMode)display.TogglePlay();}}
        void Update()
        {
            if(!display)return;rocker.localRotation=Quaternion.Euler(0,MotionMode?-16:16,0);switchSurface.sharedMaterial=MotionMode?blue:grey;
            state.text=MotionMode?"MOTION":"INFO / TIMELINE";ExplanationButton.gameObject.SetActive(MotionMode);
        }
        Transform Shape(string name,Transform parent,PrimitiveType type,Vector3 position,Vector3 scale,Material material,bool interactive=false)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;
            if(!interactive){var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);}return go.transform;
        }
        TextMeshPro Label(string text,Vector3 position,float size,Vector2 area)
        {
            var label=new GameObject(text,typeof(TextMeshPro)).GetComponent<TextMeshPro>();label.transform.SetParent(face,false);label.transform.localPosition=position;label.font=Resources.Load<TMP_FontAsset>("WorkshopHeading");label.fontSize=size*10;label.text=text;label.color=Color.white;label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=area;label.textWrappingMode=TextWrappingModes.NoWrap;return label;
        }
        void OnDestroy(){foreach(var material in materials)if(material)Destroy(material);}
    }
}
