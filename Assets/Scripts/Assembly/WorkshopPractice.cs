using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EngineAssembly
{
    [DisallowMultipleComponent] public sealed class WorkshopPractice:MonoBehaviour
    {
        public const string ChapterId="practice-controls";
        public static bool VerificationEnabled {get;set;}
        AssemblyManager manager;readonly List<AssemblyPart> shapes=new List<AssemblyPart>();readonly List<Material> materials=new List<Material>();
        bool guideOpened;int visit=-1;Vector3 enginePosition;Quaternion engineRotation;
        readonly Dictionary<string,float> actions=new Dictionary<string,float>();
        readonly HashSet<string> dropped=new HashSet<string>();
        Vector3 lastViewPosition;Quaternion lastViewRotation;bool observingView;
        public float Progress(string action)=>action=="place"?shapes.Count(p=>p && !p.Definition.isBase && p.IsSnapped):actions.TryGetValue(action,out var value)?value:0;
        public void EnsureLooseCube()
        {
            var cubes=shapes.Where(p=>p.PartId.StartsWith("practice-cube")).ToArray();if(cubes.Any(p=>!p.IsSnapped))return;
            var cube=cubes.FirstOrDefault();if(cube)cube.ResetLoose(manager.TrayFrame,cube.Definition.trayPosition,cube.Definition.trayRotation);
        }
        public static void Record(string action,float amount=1)
        {
            var manager=AssemblyManager.Instance;var practice=manager?manager.GetComponent<WorkshopPractice>():null;
            if(!practice || !practice.Active || manager.Mode!=WorkshopMode.Assembly || manager.IsPreparing || amount<=0)return;
            practice.actions[action]=practice.Progress(action)+amount;
        }
        public static void PickedUp(AssemblyPart part)
        {
            if(!part || part.ChapterId!=ChapterId)return;Record("pickup");
            var practice=part.Manager.GetComponent<WorkshopPractice>();if(practice && practice.dropped.Contains(part.PartId))Record("repickup");
        }
        public static void Dropped(AssemblyPart part)
        {
            if(!part || part.ChapterId!=ChapterId)return;Record("drop");
            var practice=part.Manager.GetComponent<WorkshopPractice>();if(practice)practice.dropped.Add(part.PartId);
        }
        public bool Active=>manager && manager.CurrentChapterId==ChapterId;
        public bool ControlsFinished {get;private set;}
        public int ShapeCount=>shapes.Count(p=>p.Definition!=null && !p.Definition.isBase);
        public static bool IsAssemblyExperience(GameObject owner)
        {
            var manager=owner.GetComponent<AssemblyManager>();
            return manager && !owner.GetComponent<MobileEngineDisplay>() && !owner.GetComponent<RepairVRFallback>() && !manager.RepairActive && !manager.DisplayActive
                && !ExperienceMenu.MobileRepairRequested && ExperienceMenu.RequestedChapter!=PCRepairChapter.ChapterId && ExperienceMenu.RequestedChapter!=PCDisplayChapter.ChapterId;
        }
        public static bool SkipOnPhone(GameObject owner)=>owner.GetComponent<MobileAssemblyController>() || owner.GetComponent<MobileEngineDisplay>() || ExperienceMenu.MobileWorkshopRequested || Application.isMobilePlatform && !QuestVRSession.IsQuestModel(SystemInfo.deviceModel);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset()=>VerificationEnabled=false;
        public static void Ensure(AssemblyManager manager)
        {
            if(Application.isBatchMode && !VerificationEnabled || SkipOnPhone(manager.gameObject) || !IsAssemblyExperience(manager.gameObject) || manager.GetComponent<WorkshopPractice>())return;
            manager.gameObject.AddComponent<WorkshopPractice>().Build(manager);
        }
        void Build(AssemblyManager owner)
        {
            manager=owner;enginePosition=manager.AssemblyRoot.localPosition;engineRotation=manager.AssemblyRoot.localRotation;
            manager.Recipe.chapters.Insert(0,new ChapterDefinition{id=ChapterId,title="Practice controls",description="Learn every control using cubes, a cylinder and a ball before starting the engine."});
            var center=manager.WorkpieceBounds(manager.AssemblyRoot).center;center.y=1;
            Create("practice-base","Practice board",PrimitiveType.Cube,new Vector3(.86f,.04f,.5f),center,new Color(.35f,.42f,.5f),true,0);
            for(int i=0;i<3;i++)Create("practice-cube_"+i.ToString("00"),"Practice cube",PrimitiveType.Cube,Vector3.one*.12f,center+new Vector3(-.26f+i*.26f,.085f,-.12f),new Color(.32f,.57f,.9f),false,i);
            Create("practice-cylinder","Practice cylinder",PrimitiveType.Cylinder,new Vector3(.12f,.08f,.12f),center+new Vector3(-.18f,.105f,.12f),new Color(.95f,.72f,.35f),false,3);
            Create("practice-ball","Practice ball",PrimitiveType.Sphere,Vector3.one*.14f,center+new Vector3(.18f,.095f,.12f),new Color(.65f,.52f,.85f),false,4);
            manager.SelectChapter(0);
            var ui=manager.GetComponent<AssemblyWorkshopUI>();if(ui)ui.SetOpen(false);
        }
        void Create(string id,string title,PrimitiveType type,Vector3 scale,Vector3 target,Color color,bool isBase,int slot)
        {
            var go=GameObject.CreatePrimitive(type);go.name=id;UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,gameObject.scene);go.transform.localScale=scale;
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=color;go.GetComponent<Renderer>().sharedMaterial=material;materials.Add(material);
            var part=go.AddComponent<AssemblyPart>();part.SetIdentity(id,title);
            var tray=manager.TrayFrame.GetComponent<Renderer>();var b=tray?tray.bounds:new Bounds(manager.TrayFrame.position,new Vector3(2,1,1));
            var supply=new Vector3(b.center.x+(slot-2)*.20f,b.max.y+go.GetComponent<Renderer>().bounds.extents.y+.015f,b.center.z);
            var d=new PartDefinition{id=id,displayName=title,chapterId=ChapterId,isBase=isBase,order=isBase?0:1,geometryGroup=id.StartsWith("practice-cube")?"practice-cube":id,
                targetPosition=manager.AssemblyRoot.InverseTransformPoint(target),targetRotation=Quaternion.identity,trayPosition=manager.TrayFrame.InverseTransformPoint(supply),trayRotation=Quaternion.identity,orientation=AssemblyOrientation.Any};
            manager.Recipe.parts.Add(d);part.Configure(d,manager);manager.RegisterRepairPart(part);
            part.TargetSocket=new GameObject(id+" socket").AddComponent<AssemblySocket>();part.TargetSocket.Configure(part);part.TargetSocket.transform.SetParent(manager.AssemblyRoot,false);part.TargetSocket.transform.SetLocalPositionAndRotation(d.targetPosition,d.targetRotation);shapes.Add(part);
        }
        public void BeforeChapter(int index)
        {
            if(Active && manager.Recipe.chapters[index].id!=ChapterId){if(WorkshopTutorial.Active && WorkshopTutorial.Active.Owner==gameObject)WorkshopTutorial.Active.Close(false);manager.AssemblyRoot.SetLocalPositionAndRotation(enginePosition,engineRotation);}
        }
        public void ChapterSelected()
        {
            foreach(var shape in shapes){shape.gameObject.SetActive(Active);shape.TargetSocket.gameObject.SetActive(Active);}
            if(Active){guideOpened=false;ControlsFinished=false;visit=manager.ChapterVisit;actions.Clear();dropped.Clear();observingView=false;}
        }
        public void FinishControls(){ControlsFinished=true;manager.CheckChapterCompletion();}
        public void OpenGuide(){guideOpened=true;WorkshopTutorial.OpenAllControls(gameObject);}
        void Update()
        {
            if(!Active || manager.Mode!=WorkshopMode.Assembly)return;
            var ui=manager.GetComponent<AssemblyWorkshopUI>();var mobile=manager.GetComponent<MobileAssemblyController>();
            if(Time.timeScale>0 && (!ui || !ui.IsOpen) && (!mobile || !mobile.ChapterMenuOpen))
            {
                var view=Camera.main;if(view)
                {
                    if(observingView){var distance=Vector3.ProjectOnPlane(view.transform.position-lastViewPosition,Vector3.up).magnitude;if(distance<3)Record("walk",distance);Record("look",Quaternion.Angle(lastViewRotation,view.transform.rotation));}
                    lastViewPosition=view.transform.position;lastViewRotation=view.transform.rotation;observingView=true;
                }
                if(mobile && MobileARSession.CanInteract(gameObject))Record("workspace");
            }
            else observingView=false;
            if(visit!=manager.ChapterVisit){visit=manager.ChapterVisit;guideOpened=false;ControlsFinished=false;}
            if(!guideOpened && !WorkshopTutorial.IsOpen && (!ui || !ui.IsOpen) && (!mobile || mobile.Ready))OpenGuide();
        }
        public static int ChapterNumber(AssemblyManager manager)=>!manager || manager.Recipe==null?0:manager.Recipe.chapters.Any(c=>c.id==ChapterId)?manager.CurrentChapterIndex:manager.CurrentChapterIndex+1;
        public static void Strip(AssemblyRecipe recipe){recipe.parts.RemoveAll(p=>p.chapterId==ChapterId);recipe.chapters.RemoveAll(c=>c.id==ChapterId);}
        void OnDestroy(){foreach(var material in materials)if(material)Destroy(material);}
    }
}
