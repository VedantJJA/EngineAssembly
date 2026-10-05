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
    [InitializeOnLoad] public static class WorkshopProcessVerification
    {
        static WorkshopProcessVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch(){SessionState.SetBool("Process.Verify",true);EditorSceneManager.OpenScene(AssemblyWorkshopBuilder.ScenePath,OpenSceneMode.Single);EditorApplication.isPlaying=true;}
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("Process.Verify",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Process checks").AddComponent<WorkshopProcessVerificationRunner>();
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("Process.Verify",false);EditorApplication.Exit(SessionState.GetBool("Process.Result",false)?0:1);}
        }
    }
    public sealed class WorkshopProcessVerificationRunner:MonoBehaviour
    {
        [Serializable] class Report { public bool passed;public string[] checks;public string error; }
        readonly List<string> checks=new List<string>();AssemblyManager manager;AssemblyEasyGuide guide;PlayerAssemblyController player;
        IEnumerator Start()
        {
            var routine=Scenario();string error="";
            while(true){bool more=false;object current=null;try{more=routine.MoveNext();if(more)current=routine.Current;}catch(Exception ex){error=ex.ToString();}if(!more || error.Length>0)break;yield return current;}
            File.WriteAllText("Tools/Verification/process-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,checks=checks.ToArray(),error=error},true));SessionState.SetBool("Process.Result",error.Length==0);EditorApplication.isPlaying=false;
        }
        IEnumerator Scenario()
        {
            yield return null;yield return null;yield return null;
            manager=AssemblyManager.Instance;manager.GetComponent<AssemblyWorkshopUI>().SetOpen(false);
            player=FindAnyObjectByType<PlayerAssemblyController>();player.enabled=false;player.GetComponent<CharacterController>().enabled=false;guide=manager.GetComponent<AssemblyEasyGuide>();
            var table=GameObject.Find("Parts Table");var platform=table.GetComponent<PickupablePlatform>();var position=table.transform.position;
            Assert(!platform.CanPickUp && !platform.BeginGrab(),"The large spawn table cannot be picked up");platform.MoveHeld(position+Vector3.one,Quaternion.Euler(20,30,40));Assert(Vector3.Distance(position,table.transform.position)<.001f,"The locked spawn table cannot be moved through platform controls");
            var small=GameObject.Find("Portable Work Platform").GetComponent<PickupablePlatform>();Assert(small.CanPickUp && small.BeginGrab(),"The small platform remains pickupable");var original=small.transform.position;small.MoveHeld(original+Vector3.right,small.transform.rotation);Assert(Vector3.Distance(small.transform.position,original)> .99f,"The small platform can be repositioned");small.Release();
            Assert(manager.Recipe.Validate().Count==0,"Revised sump/timing recipe validates without dependency cycles");
            foreach(var p in manager.AssemblyParts){var data=new SerializedObject(p);data.FindProperty("snapDuration").floatValue=0;data.ApplyModifiedPropertiesWithoutUndo();}
            var layout=JsonUtility.FromJson<Layout>(File.ReadAllText("Tools/Verification/tray-groups.json"));var top=table.GetComponent<Renderer>().bounds.max.y;
            foreach(var chapter in manager.Recipe.chapters)
            {
                manager.SelectChapter(manager.Recipe.chapters.IndexOf(chapter));
                foreach(var group in layout.groups.Where(g=>g.chapter==chapter.id))
                {
                    var area=new Rect(group.center.x-group.size.x/2,group.center.z-group.size.z/2,group.size.x,group.size.z);
                    foreach(var id in group.ids){var bounds=AssemblyHeldView.WorldBounds(manager.FindPart(id));Assert(area.Contains(new Vector2(bounds.center.x,bounds.center.z)) && bounds.min.y>=top+.01f,"Tray group retains and supports "+id);}
                }
                var groups=layout.groups.Where(g=>g.chapter==chapter.id).ToArray();
                for(int i=0;i<groups.Length;i++)for(int j=i+1;j<groups.Length;j++)
                {
                    Rect Area(WorkshopProcessUpdate.TrayGroup g)=>new Rect(g.center.x-g.size.x/2,g.center.z-g.size.z/2,g.size.x,g.size.z);
                    Assert(!Area(groups[i]).Overlaps(Area(groups[j])),"Separate contiguous families: "+groups[i].type+" / "+groups[j].type);
                }
            }
            var canvas=manager.GetComponentInChildren<Canvas>();canvas.enabled=false;manager.SelectChapter(4);
            var tableBounds=table.GetComponent<Renderer>().bounds;player.ViewCamera.transform.SetPositionAndRotation(tableBounds.center+new Vector3(0,9,-4),Quaternion.LookRotation(new Vector3(0,-9,4)));
            WorkshopPresentationBuilder.Render(player.ViewCamera,"Tools/Verification/grouped-parts-table.png",1440,900);canvas.enabled=true;
            manager.SelectChapter(4);manager.AssemblyRoot.rotation=Quaternion.identity;manager.SetDifficulty(SnapDifficulty.Easy,false);
            foreach(var p in manager.AssemblyParts.Where(p=>p.ChapterId==manager.CurrentChapterId && p.Definition.order<506).OrderBy(p=>p.Definition.order))p.InstallImmediate(p.TargetSocket);
            var hubbolts=manager.AssemblyParts.Where(p=>p.PartId.StartsWith("590_Bolt_ISO_4162_M12")).ToArray();
            var washers=manager.AssemblyParts.Where(p=>p.PartId.StartsWith("507_Camshaft")).ToArray();
            Assert(hubbolts.All(p=>!manager.CanInstall(p,p.TargetSocket,out _)),"Camshaft hub bolts stay locked until the inner washers are seated");
            foreach(var washer in washers)PlaceVisible(washer);
            Assert(hubbolts.All(p=>manager.CanInstall(p,p.TargetSocket,out _)),"Hub washers unlock the bolts before outer timing pulleys");
            foreach(var bolt in hubbolts)PlaceVisible(bolt);
            foreach(var difficulty in new[]{SnapDifficulty.Easy,SnapDifficulty.Hard})
            {
                manager.SelectChapter(2);manager.AssemblyRoot.rotation=Quaternion.Euler(180,0,0);manager.SetDifficulty(difficulty,false);
                foreach(var p in manager.AssemblyParts.Where(p=>p.ChapterId==manager.CurrentChapterId && p.Definition.order<304).OrderBy(p=>p.Definition.order))p.InstallImmediate(p.TargetSocket);
                var group=manager.CarrierDefinition("380_Complete_Oil_Sump");var root=manager.FindPart(group.rootPartId);root.transform.rotation=Quaternion.identity;
                Assert(!manager.CanPickUp(root,out _),"Incomplete sump cannot be carried as a unit in "+difficulty);
                var members=manager.Members(group).OrderBy(p=>p.Definition.order).ThenBy(p=>p.PartId).ToArray();
                Assert(members.First().PartId=="304_Oil_Sump_01" && members[1].PartId=="303_Oil_Sump_Baffle_Plate_01","Sump body precedes baffle and its four screws in "+difficulty);
                foreach(var p in members)PlaceVisible(p);
                Assert(manager.GroupComplete(group),"Sump bench unit becomes complete in "+difficulty);
                var local=members.ToDictionary(p=>p,p=>root.transform.InverseTransformPoint(p.transform.position));
                player.PickUp(root);Assert(player.CurrentHeldPart==root,"The sump carries as one complete unit in "+difficulty);
                var pose=root.GetTargetPose(root.TargetSocket);root.MoveHeld(pose.position,pose.rotation);
                Assert(local.All(pair=>Vector3.Distance(pair.Key.transform.position,root.transform.TransformPoint(pair.Value))<.001f),"Carrying the sump retains the baffle and every screw");
                var ray=ClearRay(root);Assert(guide.Place(ray,10,player.ViewCamera) && root.IsSnapped,"Complete sump installs on the inverted engine in "+difficulty);player.DropHeldPart(false);
                Assert(manager.AssemblyParts.Where(p=>p.ChapterId==manager.CurrentChapterId && p.Definition.order==308).All(p=>manager.CanInstall(p,p.TargetSocket,out _)),"Installed sump unlocks its mounting fasteners");
            }
        }
        [Serializable] class Layout { public WorkshopProcessUpdate.TrayGroup[] groups; }
        void PlaceVisible(AssemblyPart part)
        {
            Assert(part.BeginGrab(),"Eligible pickup: "+part.PartId);var ray=ClearRay(part);var pose=part.GetTargetPose(part.TargetSocket);part.MoveHeld(pose.position,pose.rotation);
            Assert(guide.Place(ray,10,player.ViewCamera) && part.IsSnapped,"Visible installation: "+part.PartId);
        }
        Ray ClearRay(AssemblyPart part)
        {
            var pose=part.GetTargetPose(part.TargetSocket);var center=part.transform.InverseTransformPoint(AssemblyHeldView.WorldBounds(part).center);center=pose.position+pose.rotation*Vector3.Scale(center,part.transform.lossyScale);
            for(int i=0;i<96;i++)
            {
                float angle=(i%16)*Mathf.PI/8;float height=i<32?1.5f:i<64?.4f:-1.5f;float radius=i%32<16?.7f:1.5f;
                var origin=center+new Vector3(Mathf.Cos(angle)*radius,height,Mathf.Sin(angle)*radius);var ray=new Ray(origin,center-origin);
                player.ViewCamera.transform.SetPositionAndRotation(origin,Quaternion.LookRotation(ray.direction));
                if(guide.Aim(ray,10,player.ViewCamera)==part)return ray;
            }
            throw new Exception("No clear placement ray for "+part.PartId);
        }
        void Assert(bool value,string text){if(!value)throw new Exception(text);checks.Add(text);}
    }
}
