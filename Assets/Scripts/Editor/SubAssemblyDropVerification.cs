using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EngineAssembly.Editor
{
    [InitializeOnLoad] public static class SubAssemblyDropVerification
    {
        static SubAssemblyDropVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch()
        {
            if(!Application.isBatchMode)throw new Exception("Isolated verification only.");
            SessionState.SetBool("Drop.Verify",true);
            EditorSceneManager.OpenScene(AssemblyWorkshopBuilder.ScenePath,OpenSceneMode.Single);
            EditorApplication.isPlaying=true;
        }
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("Drop.Verify",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Subassembly drop checks").AddComponent<SubAssemblyDropVerificationRunner>();
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("Drop.Verify",false);EditorApplication.Exit(SessionState.GetBool("Drop.Result",false)?0:1);}
        }
    }
    public sealed class SubAssemblyDropVerificationRunner:MonoBehaviour
    {
        [Serializable] class Report { public bool passed;public string[] checks;public string error; }
        readonly List<string> checks=new List<string>();
        IEnumerator Start()
        {
            var stack=new Stack<IEnumerator>();stack.Push(Scenario());string error="";
            while(stack.Count>0)
            {
                bool more=false;object step=null;
                try{more=stack.Peek().MoveNext();if(more)step=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();break;}
                if(!more){stack.Pop();continue;}if(step is IEnumerator nested){stack.Push(nested);continue;}yield return step;
            }
            File.WriteAllText("Tools/Verification/subassembly-drop-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,checks=checks.ToArray(),error=error},true));
            SessionState.SetBool("Drop.Result",error.Length==0);EditorApplication.isPlaying=false;
        }
        IEnumerator Scenario()
        {
            yield return null;yield return null;yield return null;
            var manager=AssemblyManager.Instance;manager.GetComponent<AssemblyWorkshopUI>().SetOpen(false);
            var player=FindAnyObjectByType<PlayerAssemblyController>();player.enabled=false;player.GetComponent<CharacterController>().enabled=false;
            Assert(manager.Recipe.Validate().Count==0,"Retainer recipe has no missing dependencies or cycles");
            Assert(!manager.Recipe.parts.Any(p=>p.id=="909_Motor_Wiring_01") && !manager.FindPart("909_Motor_Wiring_01").gameObject.activeSelf,"Motor wiring is excluded from gameplay");
            Before(manager,"203_Piston_Wrist_Pin_Cyl01_01","205_Wrist_Pin_Circlip_Cyl01_01");
            Before(manager,"203_Piston_Wrist_Pin_Cyl02_01","205_Wrist_Pin_Circlip_Cyl02_02");
            Before(manager,"203_Piston_Wrist_Pin_Cyl03_01","205_Wrist_Pin_Circlip_Cyl03_01");
            foreach(var clip in new[]{"511_Idler_Shaft_Circlip_01","511_Idler_Shaft_Circlip_04"})
            {
                Before(manager,clip,"511_Timing_Idler_Pulley_01");Before(manager,clip,"530_Timing_Idler_Unit_02");
            }
            Before(manager,"530_Timing_Idler_Unit_01","511_Idler_Shaft_Circlip_02");
            Before(manager,"530_Timing_Idler_Unit_02","511_Idler_Shaft_Circlip_03");
            Before(manager,"705_Turbo_Outlet_Band_Clamp_01","790_Bolt_ISO_4762_M8_x_12_Fasteners_01");
            Before(manager,"901_Accessory_Upper_Mounting_Bracket_01","901_Accessory_Lower_Mounting_Bracket_01");
            Before(manager,"907_Idler_Shaft_Circlip_02","909_Accessory_Idler_Unit");
            Before(manager,"909_Accessory_Idler_Unit","907_Idler_Shaft_Circlip_01");
            var pad=GameObject.CreatePrimitive(PrimitiveType.Cube);pad.name="Drop test floor";pad.transform.position=new Vector3(40,-.25f,0);pad.transform.localScale=new Vector3(25,.5f,25);
            foreach(var group in manager.Recipe.subAssemblies)
            {
                var root=manager.FindPart(group.rootPartId);int chapter=manager.Recipe.chapters.FindIndex(c=>c.id==root.Definition.chapterId);
                manager.SelectChapter(chapter);manager.SetDifficulty(SnapDifficulty.Easy,false);
                foreach(var data in manager.Recipe.parts.Where(p=>p.chapterId==manager.CurrentChapterId && p.order<root.Definition.order).OrderBy(p=>p.order))
                {var part=manager.FindPart(data.id);part.InstallImmediate(part.TargetSocket);}
                Assert(manager.GroupComplete(group),"Completed unit: "+root.PartId);
                var members=manager.Members(group).ToArray();var local=members.ToDictionary(p=>p,p=>root.transform.InverseTransformPoint(p.transform.position));
                var rotations=members.ToDictionary(p=>p,p=>Quaternion.Inverse(root.transform.rotation)*p.transform.rotation);
                for(int drop=0;drop<3;drop++)
                {
                    player.ViewCamera.transform.SetPositionAndRotation(new Vector3(40,4,-4),Quaternion.identity);
                    player.PickUp(members.First());Assert(player.CurrentHeldPart==root,"Whole-unit pickup cycle "+drop+": "+root.PartId);
                    root.MoveHeld(root.transform.position,Quaternion.identity);player.DropHeldPart(false);Physics.SyncTransforms();
                    Assert(!root.Body.isKinematic && root.Body.useGravity && root.GetComponent<BoxCollider>().enabled,"Dropped unit has a dynamic handling body: "+root.PartId);
                    Assert(members.All(p=>!p.Body.detectCollisions && p.GetComponentsInChildren<Collider>(true).Where(c=>c.attachedRigidbody==p.Body).All(c=>!c.enabled)),"Member surfaces cannot self-collide during handling: "+root.PartId);
                    var start=root.transform.position;float maxSpeed=0,maxUp=0;
                    for(int frame=0;frame<85;frame++)
                    {
                        yield return new WaitForFixedUpdate();yield return null;
                        maxSpeed=Mathf.Max(maxSpeed,root.Body.linearVelocity.magnitude);maxUp=Mathf.Max(maxUp,root.Body.linearVelocity.y);
                        if(float.IsNaN(root.transform.position.x) || root.transform.position.y>start.y+.2f || maxUp>1 || maxSpeed>16)throw new Exception("Unstable drop "+root.PartId+" speed="+maxSpeed+" upward="+maxUp);
                        if(!local.All(pair=>Vector3.Distance(pair.Key.transform.position,root.transform.TransformPoint(pair.Value))<.004f) || !rotations.All(pair=>Quaternion.Angle(pair.Key.transform.rotation,root.transform.rotation*pair.Value)<.1f))throw new Exception("Member geometry drift on drop: "+root.PartId);
                    }
                    Assert(root.GetComponent<BoxCollider>().bounds.min.y>-.1f && root.GetComponent<BoxCollider>().bounds.min.y<.12f,"Unit settles on floor without flying or falling through: "+root.PartId+" cycle "+drop);
                }
                root.InstallImmediate(root.TargetSocket);yield return null;
                Assert(!root.GetComponent<BoxCollider>().enabled && !root.GetComponent<SubAssembly>().UsesHandlingPhysics,"Handling proxy removed on installation: "+root.PartId);
                Assert(members.All(p=>p.Body.detectCollisions && p.GetComponentsInChildren<Collider>(true).Any(c=>c.enabled && !c.isTrigger)),"Original member collision restored on installation: "+root.PartId);
                foreach(var precise in members.Select(p=>p.GetComponent<AssemblyPrecisionSurface>()).Where(p=>p))
                    Assert(precise.GetComponentsInChildren<MeshCollider>(true).Any(c=>c.enabled && !c.convex),"Installed CAD cavity surface restored: "+precise.name);
            }
        }
        void Before(AssemblyManager manager,string first,string second)
        {
            var a=manager.FindPart(first);var b=manager.FindPart(second);Assert(a.Definition.order<b.Definition.order,first+" precedes "+second);
            manager.SelectChapter(manager.Recipe.chapters.FindIndex(c=>c.id==b.ChapterId));
            foreach(var data in manager.Recipe.parts.Where(p=>p.chapterId==b.ChapterId && p.order<b.Definition.order && p.id!=first).OrderBy(p=>p.order)){var p=manager.FindPart(data.id);p.InstallImmediate(p.TargetSocket);}
            Assert(!manager.CanPickUp(b,out _) && !manager.CanInstall(b,b.TargetSocket,out _),"Pickup and installation enforce missing earlier component: "+first);
        }
        void Assert(bool value,string text){if(!value)throw new Exception(text);checks.Add(text);}
    }
}
