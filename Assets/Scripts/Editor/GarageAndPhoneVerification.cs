using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace EngineAssembly.Editor
{
    [InitializeOnLoad] public static class GarageAndPhoneVerification
    {
        static GarageAndPhoneVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch()
        {
            if(!Application.isBatchMode || !Application.dataPath.Replace('\\','/').EndsWith("/Tools/Verification/UnityProject/Assets"))throw new InvalidOperationException("Isolated verification project only.");
            SessionState.SetBool("Garage.Verify",true);EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity",OpenSceneMode.Single);EditorApplication.isPlaying=true;
        }
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("Garage.Verify",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Garage and phone verification").AddComponent<GarageAndPhoneVerificationRunner>();
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("Garage.Verify",false);EditorApplication.Exit(SessionState.GetBool("Garage.Result",false)?0:1);}
        }
    }
    public sealed class GarageAndPhoneVerificationRunner:MonoBehaviour
    {
        [Serializable] class Report{public bool passed;public string[] checks;public string error;}
        readonly List<string> checks=new List<string>();
        void Check(bool value,string text){if(!value)throw new Exception(text);checks.Add(text);}
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);var stack=new Stack<IEnumerator>();stack.Push(Scenario());string error="";
            while(stack.Count>0){bool more=false;object item=null;try{more=stack.Peek().MoveNext();if(more)item=stack.Peek().Current;}catch(Exception e){error=e.ToString();break;}if(!more){stack.Pop();continue;}if(item is IEnumerator nested)stack.Push(nested);else yield return item;}
            File.WriteAllText("Tools/Verification/garage-phone-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,checks=checks.ToArray(),error=error},true));SessionState.SetBool("Garage.Result",error.Length==0);EditorApplication.isPlaying=false;
        }
        IEnumerator Scenario()
        {
            yield return null;yield return null;FindAnyObjectByType<ExperienceMenu>().Show(ExperienceMenu.Page.Experiences);yield return null;
            foreach(string scene in new[]{"MobileDisplayARFallback","MobileAssemblyARFallback"})
            {
                yield return SceneManager.LoadSceneAsync(scene);yield return null;yield return null;yield return null;
                Check(!FindAnyObjectByType<ExperienceMenu>() && !FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(c=>c.name.Contains("Experience menu")),"Phone scene removes the previous experience menu canvas");
                var display=FindAnyObjectByType<MobileEngineDisplay>();var phone=FindAnyObjectByType<MobileAssemblyController>();var host=display?display.gameObject:phone.gameObject;
                var background=host.GetComponentsInChildren<Camera>().First(c=>c.name=="Phone background camera");var camera=display?display.ViewCamera:phone.ViewCamera;
                Check(background.enabled && background.cullingMask==0 && background.rect==new Rect(0,0,1,1) && background.depth<camera.depth && background.clearFlags==CameraClearFlags.SolidColor,"Fallback clears the full screen before its reserved engine viewport");
                var rt=new RenderTexture(1280,720,24);rt.Create();RenderPipeline.SubmitRenderRequest(background,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                var previous=RenderTexture.active;RenderTexture.active=rt;var sample=new Texture2D(1280,720,TextureFormat.RGBA32,false);sample.ReadPixels(new Rect(0,0,1280,720),0,0);sample.Apply();
                Check(sample.GetPixels32().All(p=>p.r>210 && p.g>210 && p.b>210),"Every background pixel is freshly cleared, including old menu fragment positions");RenderTexture.active=previous;Destroy(sample);rt.Release();Destroy(rt);
                var canvas=host.GetComponentInChildren<Canvas>();Capture(canvas,camera,scene+"-landscape",1920,1080,true);Capture(canvas,camera,scene+"-portrait",1080,1920,true);
                var rig=host.GetComponent<SpatialExperienceRig>();var tracked=new GameObject("Test AR camera").AddComponent<Camera>();rig.BeginTrackedSession(tracked,new Pose(Vector3.zero,Quaternion.identity));Check(!background.gameObject.activeSelf,"AR hides the fallback background so it cannot cover the live camera");rig.EndTrackedSession();Check(background.gameObject.activeSelf,"Returning to phone view restores the complete background clear");Destroy(tracked.gameObject);
            }
            yield return SceneManager.LoadSceneAsync("AssemblyWorkshop");yield return null;yield return null;yield return null;
            var manager=AssemblyManager.Instance;var ui=manager.GetComponent<AssemblyWorkshopUI>();var garage=SceneManager.GetActiveScene().GetRootGameObjects().First(g=>g.name=="Garage environment");
            Check(garage.GetComponentsInChildren<MeshRenderer>().Length>=70,"PC scene contains a custom garage shell and furnished workshop zones");
            Check(garage.GetComponentsInChildren<MeshRenderer>().All(r=>r.sharedMaterials.All(m=>m && m.shader.name=="Universal Render Pipeline/Lit")),"Garage surfaces all use compatible URP materials");
            Check(garage.GetComponentsInChildren<MeshRenderer>().All(r=>r.lightmapIndex<0),"Resized garage does not retain incompatible baked lightmap assignments");
            var floor=garage.GetComponentsInChildren<Renderer>().First(r=>r.name=="Floor");var ceiling=garage.GetComponentsInChildren<Renderer>().First(r=>r.name=="Ceiling");
            Check(floor.bounds.size.x>=14 && floor.bounds.size.z>=14 && Mathf.Abs(floor.bounds.max.y)<.002f && ceiling.bounds.min.y>3.7f,"Garage has full-scale workspace clearance and a ground-level floor");
            Check(Physics.RaycastAll(new Vector3(0,2,-4),Vector3.down,3).Any(hit=>hit.collider.name=="Floor" && Mathf.Abs(hit.point.y)<.01f),"Player spawn has a solid garage floor");
            Check(manager.Recipe.parts.Count==325 && manager.AssemblyRoot.lossyScale==Vector3.one*manager.EngineScale,"Garage preserves the complete engine recipe at the requested reduced scale");
            Check(manager.TrayFrame.lossyScale==new Vector3(9,.2f,3.01f) || Mathf.Abs(manager.TrayFrame.lossyScale.x-9)<.001f,"Garage preserves supply-tray scale and recipe coordinates");
            var supplies=manager.GetComponent<WorkshopSupplyLayout>();
            Check(supplies && supplies.Surfaces.Count==6 && supplies.Surfaces.All(s=>s.center.y>.85f && s.center.y<1),"Six fixed supply tables keep all pickup surfaces at waist height");
            Check(!manager.TrayFrame.GetComponent<Collider>().enabled && !manager.TrayFrame.GetComponent<Renderer>().enabled,"Legacy giant slab no longer obstructs the furnished tables");
            var carriers=manager.Recipe.subAssemblies.Select(s=>s.rootPartId).ToHashSet();int staged=0;
            for(int chapter=0;chapter<manager.Recipe.chapters.Count-1;chapter++)
            {
                manager.SelectChapter(chapter);Physics.SyncTransforms();
                var parts=manager.AssemblyParts.Where(p=>p && p.gameObject.activeSelf && !p.IsSnapped && p.Definition!=null && !p.Definition.isBase && !carriers.Contains(p.PartId)).ToArray();
                foreach(var part in parts)
                {
                    var b=AssemblyHeldView.WorldBounds(part);Check(supplies.Contains(b),"Reachable supply surface: "+part.PartId);staged++;
                    var surface=supplies.Surfaces.First(s=>Mathf.Abs(s.center.y+.025f-b.min.y)<.01f && Mathf.Abs(s.center.x-b.center.x)<s.size.x*.5f && Mathf.Abs(s.center.z-b.center.z)<s.size.y*.5f);
                    var eye=new Vector3(b.center.x,1.65f,surface.center.z-surface.size.y*.5f-.65f);
                    Check(Vector3.Distance(eye,b.center)<2.2f,"Standing pickup range: "+part.PartId);
                    Check(!Physics.RaycastAll(eye,(b.center-eye).normalized,Vector3.Distance(eye,b.center)-.015f,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.GetComponentInParent<AssemblyPart>()==null && h.collider.name!="Player"),"Standing supply approach clear: "+part.PartId);
                }
                for(int a=0;a<parts.Length;a++)for(int b=a+1;b<parts.Length;b++)Check(!AssemblyHeldView.WorldBounds(parts[a]).Intersects(AssemblyHeldView.WorldBounds(parts[b])),"Supply parts do not overlap");
            }
            Check(staged>280,"Every physical assembly chapter has verified supply staging");manager.SelectChapter(0);
            manager.SelectChapter(manager.Recipe.chapters.FindIndex(c=>c.id==PCRepairChapter.ChapterId));yield return null;
            Check(manager.RepairActive && supplies.Contains(AssemblyHeldView.WorldBounds(manager.Repair.Replacement)),"Repair replacement starts on a reachable supply bench");
            for(int step=0;step<manager.Repair.StepCount;step++)
            {
                var part=manager.Repair.NextPart;Check(part,"Repair step has its physical component");
                if(manager.Repair.IsPlacementStep)part.InstallImmediate(part.TargetSocket);else part.Unsnap(true);
                yield return null;yield return null;
                foreach(var loose in manager.AssemblyParts.Where(p=>p && p.gameObject.activeSelf && !p.IsSnapped && p.Definition!=null && !p.Definition.isBase && !carriers.Contains(p.PartId)))
                    Check(supplies.Contains(AssemblyHeldView.WorldBounds(loose)),"Repair set-aside part stays on a reachable bench");
            }
            Check(manager.Repair.Complete,"Furnished workshop supports the full bearing replacement and rebuild");manager.SelectChapter(0);
            var backdrop=ui.GetComponentsInChildren<UnityEngine.UI.Image>().First(i=>i.name=="Menu");Check(backdrop.color.a>0 && backdrop.color.a<1,"PC section selector lets the live workshop show through");
            Capture(ui.GetComponentInChildren<Canvas>(),Camera.main,"garage-chapters",1440,900,false);ui.Open(AssemblyWorkshopUI.Page.Pause);yield return null;Check(backdrop.color.a<1,"Pause keeps the workshop visible behind its cards");Capture(ui.GetComponentInChildren<Canvas>(),Camera.main,"garage-pause",1440,900,false);ui.SetOpen(false);
            FindAnyObjectByType<PlayerAssemblyController>().enabled=false;manager.SelectChapter(3);yield return null;var lift=manager.GetComponent<WorkpieceLift>();lift.Select(0);lift.Move(100);
            Check(manager.WorkpieceBounds(manager.AssemblyRoot).max.y<=ceiling.bounds.min.y-.075f,"Engine lift stops before the garage ceiling");lift.Move(-100);Check(Mathf.Abs(manager.WorkpieceBounds(manager.AssemblyRoot).min.y)<.01f,"Garage retains ground-level lowering");
            Check(manager.GetComponent<AssemblyStepAssist>().NextStep(),"Assembly still installs the next eligible component in the garage");
            Camera.main.transform.SetPositionAndRotation(new Vector3(-4,2.3f,-3.5f),Quaternion.LookRotation(new Vector3(4,-.8f,5)));
            Capture(ui.GetComponentInChildren<Canvas>(),Camera.main,"garage-workshop",1440,900,false);
        }
        // Render the real viewport proportions together with the overlay UI.
        // URP render requests alone expand a camera into the full target texture.
        void Capture(Canvas canvas,Camera camera,string name,int width,int height,bool phone)
        {
            var oldMode=canvas.renderMode;var oldWorld=canvas.worldCamera;var oldPlane=canvas.planeDistance;var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();bool scalerEnabled=scaler.enabled;float scale=canvas.scaleFactor;
            var layers=canvas.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>t.gameObject.layer);foreach(var t in layers.Keys)t.gameObject.layer=31;
            var oldRect=camera.rect;var oldProjection=camera.projectionMatrix;var oldMask=camera.cullingMask;var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;var oldTarget=camera.targetTexture;
            var rt=new RenderTexture(width,height,24);rt.Create();camera.targetTexture=rt;
            Vector2 reference=phone?(width>height?new Vector2(1920,1080):new Vector2(1080,1920)):new Vector2(1440,900);
            scaler.enabled=false;canvas.scaleFactor=width/reference.x;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.5f;
            if(phone)
            {
                bool display=camera.GetComponentInParent<MobileEngineDisplay>();float bottom=display?370:550,top=display?150:360;
                var window=new Rect(0,bottom/reference.y,1,Mathf.Max(.1f,(reference.y-bottom-top)/reference.y));camera.rect=window;
                var host=camera.GetComponentInParent<MobileEngineDisplay>() as MonoBehaviour ?? camera.GetComponentInParent<MobileAssemblyController>();
                host.GetType().GetMethod("UpdateCamera",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.Invoke(host,null);
                if(!display)
                {
                    var mobile=(MobileAssemblyController)host;float mobileDistance=mobile.PresentationRadius/Mathf.Sin(Mathf.Min(camera.fieldOfView*Mathf.Deg2Rad*.5f,Mathf.Atan(Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f)*camera.aspect)))*1.18f;
                    camera.transform.SetPositionAndRotation(mobile.WorldFocus+Vector3.back*mobileDistance,Quaternion.identity);
                }
                var focus=display?((MobileEngineDisplay)host).WorldFocus:((MobileAssemblyController)host).WorldFocus;
                float radius=display?((MobileEngineDisplay)host).PresentationRadius:((MobileAssemblyController)host).PresentationRadius;
                camera.rect=new Rect(0,0,1,1);camera.ResetProjectionMatrix();
                float angle=camera.fieldOfView*Mathf.Deg2Rad*.5f;float distance=radius/Mathf.Sin(Mathf.Min(angle,Mathf.Atan(Mathf.Tan(angle)*camera.aspect)))*1.18f/window.height;
                float offset=(window.y*2+window.height-1)*distance*Mathf.Tan(angle);
                camera.transform.SetPositionAndRotation(focus+Vector3.back*distance-Vector3.up*offset,Quaternion.identity);
            }
            else camera.rect=new Rect(0,0,1,1);
            camera.cullingMask|=1<<31;Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});var active=RenderTexture.active;RenderTexture.active=rt;
            var image=new Texture2D(width,height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes("Tools/Verification/"+name+".png",image.EncodeToPNG());Destroy(image);RenderTexture.active=active;
            camera.targetTexture=oldTarget;camera.rect=oldRect;camera.projectionMatrix=oldProjection;camera.cullingMask=oldMask;camera.transform.SetPositionAndRotation(oldPosition,oldRotation);
            foreach(var layer in layers)if(layer.Key)layer.Key.gameObject.layer=layer.Value;
            canvas.renderMode=oldMode;canvas.worldCamera=oldWorld;canvas.planeDistance=oldPlane;canvas.scaleFactor=scale;scaler.enabled=scalerEnabled;rt.Release();Destroy(rt);Canvas.ForceUpdateCanvases();
        }
    }
}
