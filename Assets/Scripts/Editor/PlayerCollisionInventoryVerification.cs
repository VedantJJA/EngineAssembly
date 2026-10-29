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
    [InitializeOnLoad]
    public static class PlayerCollisionInventoryVerification
    {
        static PlayerCollisionInventoryVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch()
        {
            if(!Application.isBatchMode || !Application.dataPath.Replace('\\','/').EndsWith("/Tools/Verification/UnityProject/Assets"))throw new InvalidOperationException("Isolated verification project only.");
            SessionState.SetBool("PlayerCollision.Verify",true);
            EditorSceneManager.OpenScene(AssemblyWorkshopBuilder.ScenePath,OpenSceneMode.Single);EditorApplication.isPlaying=true;
        }
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("PlayerCollision.Verify",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Player collision and stack-drop checks").AddComponent<PlayerCollisionInventoryVerificationRunner>();
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("PlayerCollision.Verify",false);EditorApplication.Exit(SessionState.GetBool("PlayerCollision.Result",false)?0:1);}
        }
    }
    public sealed class PlayerCollisionInventoryVerificationRunner : MonoBehaviour
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
                if(!more){stack.Pop();continue;}if(step is IEnumerator nested)stack.Push(nested);else yield return step;
            }
            File.WriteAllText("Tools/Verification/player-collision-inventory-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,checks=checks.ToArray(),error=error},true));
            SessionState.SetBool("PlayerCollision.Result",error.Length==0);EditorApplication.isPlaying=false;
        }
        IEnumerator Scenario()
        {
            yield return null;yield return null;yield return null;
            var manager=AssemblyManager.Instance;manager.GetComponent<AssemblyWorkshopUI>().SetOpen(false);
            var player=FindAnyObjectByType<PlayerAssemblyController>();player.enabled=false;
            var body=player.GetComponent<CharacterController>();body.enabled=false;
            var collision=AssemblyPlayerCollision.Attach(body);collision.RefreshParts();
            Assert(body.gameObject.layer==AssemblyPlayerCollision.PlayerLayer,"PC character uses the dedicated player contact layer");
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Inventory drop test floor in garage aisle";
            floor.transform.position=new Vector3(-2,-.03f,-1.5f);floor.transform.localScale=new Vector3(3,.06f,3);
            foreach(int count in new[]{5,12})
            {
                manager.SetDifficulty(SnapDifficulty.Easy,false);manager.SelectChapter(3);
                foreach(var part in manager.AssemblyParts.Where(p=>p.ChapterId==manager.CurrentChapterId && p.Definition.order<403).OrderBy(p=>p.Definition.order))part.InstallImmediate(part.TargetSocket);
                foreach(var part in manager.AssemblyParts.Where(p=>p.PartId.StartsWith("402_") || p.PartId.StartsWith("403_")))part.InstallImmediate(part.TargetSocket);
                var head=manager.FindPart("401_Cylinder_Head_01");manager.TargetFrame(head.Definition).rotation=Quaternion.identity;
                // Chapter preparation includes the lift's first LateUpdate: it
                // creates the nearby supply tray and stages each loose member.
                // Begin pickup only after that normal preparation has completed.
                yield return null;yield return null;
                var springs=manager.AssemblyParts.Where(p=>p.PartId.StartsWith("405_")).OrderBy(p=>p.PartId).Take(count).ToArray();
                foreach(var spring in springs)player.PickUp(spring);
                Assert(player.Inventory.Count==count && springs.All(p=>p.IsSelected),count+" separate CAD spring identities are held");
                player.ViewCamera.transform.SetPositionAndRotation(new Vector3(-2,3,-3),Quaternion.identity);
                player.DropHeldPart(false);Physics.SyncTransforms();
                Assert(player.Inventory.Count==0 && !player.CurrentHeldPart,"Dropping "+count+" items empties the inventory");
                Assert(springs.All(p=>p.State==AssemblyPartState.Loose && !p.Body.isKinematic && p.Body.useGravity && p.Body.detectCollisions && !p.transform.parent),"All "+count+" dropped items have independent, active physical bodies");
                Assert(springs.All(p=>p.GetComponentsInChildren<MeshRenderer>(true).Any(r=>r.enabled)),"All "+count+" previously hidden carried meshes are restored");
                for(int i=0;i<springs.Length;i++)for(int j=i+1;j<springs.Length;j++)
                    Assert(!AssemblyHeldView.WorldBounds(springs[i]).Intersects(AssemblyHeldView.WorldBounds(springs[j])),"Distinct release volumes: "+springs[i].PartId+" / "+springs[j].PartId);
                var releasedCenters=springs.ToDictionary(p=>p,p=>AssemblyHeldView.WorldBounds(p).center);
                float releasedFixedTime=Time.fixedTime;
                Assert(Time.timeScale>0,"The drop simulation is running after the workshop menu closes");
                // WaitForFixedUpdate can resume beside the simulation boundary.
                // Observe several completed physics frames and their rendered
                // transforms, including the Rigidbody interpolation delay.
                for(int frame=0;frame<4;frame++){yield return new WaitForFixedUpdate();yield return null;}
                bool falling=Time.fixedTime>releasedFixedTime && springs.All(p=>p.Body.linearVelocity.y<-.01f && AssemblyHeldView.WorldBounds(p).center.y<releasedCenters[p].y-.001f);
                string physicsState=falling?"":string.Join("; ",springs.Select(p=>p.PartId+" velocity="+p.Body.linearVelocity.ToString("F4")+" meshCenter="+AssemblyHeldView.WorldBounds(p).center.ToString("F4")+" initialCenter="+releasedCenters[p].ToString("F4")+" CADOrigin="+p.transform.position.ToString("F4")+" bodyPosition="+p.Body.position.ToString("F4")+" gravity="+p.Body.useGravity+" kinematic="+p.Body.isKinematic+" sleeping="+p.Body.IsSleeping()+" constraints="+p.Body.constraints));
                Assert(falling,"Every released body falls normally instead of remaining an invisible held stack"+(falling?"":". Fixed seconds="+(Time.fixedTime-releasedFixedTime).ToString("F4")+". "+physicsState));
            }
            manager.SelectChapter(0);collision.RefreshParts();yield return null;
            var block=manager.FindPart("101_Cylinder_Block_01");
            var proxy=block.GetComponentsInChildren<AssemblyPlayerCollisionProxy>(true).First(p=>p.Part==block);
            Assert(proxy.Collider.enabled && proxy.Collider.gameObject.layer==2 && AssemblyPlayerCollision.CanContact(body,proxy.Collider),"The engine blocks player movement using one simple query-excluded box");
            var precise=block.GetComponentsInChildren<MeshCollider>(true).Where(c=>c.GetComponentInParent<AssemblyPart>()==block).ToArray();
            Assert(precise.Length>0 && precise.All(c=>!AssemblyPlayerCollision.CanContact(body,c)),"Dense CAD surfaces and convex decomposition hulls do not contact the character");
            var bounds=proxy.Collider.bounds;body.enabled=false;player.transform.position=new Vector3(bounds.center.x,bounds.center.y-body.height*.5f,bounds.min.z-body.radius-.3f);body.enabled=true;Physics.SyncTransforms();
            for(int i=0;i<20;i++)body.Move(Vector3.forward*.04f);
            Assert(player.transform.position.z<bounds.min.z,"Walking into the engine stops at its simple player-only collider");body.enabled=false;
            var query=player.GetComponent<AssemblyPickupQuery>();var point=AssemblyHeldView.WorldBounds(block).center;
            Assert(query.Raycast(new Ray(point-Vector3.forward*4,Vector3.forward),8,~0,out var picked,out _,out _) && picked,"Player-only blocking does not replace exact CAD pickup queries");
            var display=manager.GetComponent<PCDisplayChapter>();display.EnsureChapter();
            manager.SelectChapter(manager.Recipe.chapters.FindIndex(c=>c.id==PCDisplayChapter.ChapterId));
            var simulation=manager.GetComponent<AssemblyDisplayPhysics>();simulation.Refresh();yield return null;
            Assert(manager.AssemblyParts.Where(p=>p).SelectMany(p=>p.GetComponentsInChildren<Collider>(true)).Where(c=>!c.GetComponent<AssemblyPlayerCollisionProxy>()).All(c=>!c.enabled),"Display mode suppresses every physical CAD contact collider");
            manager.SelectChapter(0);simulation.Refresh();yield return null;
            Assert(precise.Any(c=>c.enabled && !c.convex),"Leaving display restores the precise physical cylinder-block surface");
        }
        void Assert(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
    }
}
