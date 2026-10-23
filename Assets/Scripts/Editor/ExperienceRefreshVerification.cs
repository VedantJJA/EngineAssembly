using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace EngineAssembly.Editor
{
    [InitializeOnLoad] public static class ExperienceRefreshVerification
    {
        static ExperienceRefreshVerification(){EditorApplication.playModeStateChanged+=Changed;}
        public static void RunBatch(){SessionState.SetBool("ExperienceRefresh.Run",true);EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");EditorApplication.isPlaying=true;}
        static void Changed(PlayModeStateChange state){if(!SessionState.GetBool("ExperienceRefresh.Run",false))return;if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Experience refresh verification").AddComponent<ExperienceRefreshRunner>();if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("ExperienceRefresh.Run",false);EditorApplication.Exit(SessionState.GetBool("ExperienceRefresh.Passed",false)?0:1);}}
    }
    public sealed class ExperienceRefreshRunner:MonoBehaviour
    {
        [Serializable] class Report{public bool passed;public string error;public string[] checks;}
        readonly List<string> checks=new List<string>();
        void Check(bool condition,string text){if(!condition)throw new Exception(text);checks.Add(text);}
        IEnumerator Start(){DontDestroyOnLoad(gameObject);var stack=new Stack<IEnumerator>();stack.Push(Scenario());string error="";while(stack.Count>0){bool more=false;object next=null;try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}catch(Exception e){error=e.ToString();break;}if(!more){stack.Pop();continue;}if(next is IEnumerator nested)stack.Push(nested);else yield return next;}Directory.CreateDirectory("Tools/Verification");File.WriteAllText("Tools/Verification/experience-refresh-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,error=error,checks=checks.ToArray()},true));SessionState.SetBool("ExperienceRefresh.Passed",error.Length==0);EditorApplication.isPlaying=false;}
        void Capture(string name,Camera camera)
        {
            var canvases=FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas && c.renderMode!=RenderMode.WorldSpace).ToArray();var modes=canvases.Select(c=>c.renderMode).ToArray();var cameras=canvases.Select(c=>c.worldCamera).ToArray();var rect=camera.rect;
            var scalers=canvases.Select(c=>c.GetComponent<UnityEngine.UI.CanvasScaler>()).ToArray();var scale=canvases.Select(c=>c.scaleFactor).ToArray();
            var target=new RenderTexture(1440,900,24);target.Create();var old=camera.targetTexture;camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);
            for(int i=0;i<canvases.Length;i++){if(scalers[i])scalers[i].enabled=false;canvases[i].scaleFactor=1;canvases[i].renderMode=RenderMode.ScreenSpaceCamera;canvases[i].worldCamera=camera;canvases[i].planeDistance=.5f;}
            Canvas.ForceUpdateCanvases();var menu=FindAnyObjectByType<ExperienceMenu>();if(menu)menu.Show(menu.CurrentPage);Canvas.ForceUpdateCanvases();WorkshopPresentationBuilder.Render(camera,"Tools/Verification/"+name+".png",1440,900);
            for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];canvases[i].scaleFactor=scale[i];if(scalers[i])scalers[i].enabled=true;}
            camera.targetTexture=old;camera.rect=rect;target.Release();Destroy(target);Canvas.ForceUpdateCanvases();if(menu)menu.Show(menu.CurrentPage);
        }
        IEnumerator Load(string scene){yield return SceneManager.LoadSceneAsync(scene);yield return null;yield return null;}
        IEnumerator Scenario()
        {
            yield return null;yield return null;
            var menu=FindAnyObjectByType<ExperienceMenu>();Check(menu,"Main menu starts");menu.Show(ExperienceMenu.Page.Experiences);yield return null;Capture("experiences-blue",Camera.main);
            Check(WorkshopTheme.Accent.b>WorkshopTheme.Accent.r*2 && WorkshopTheme.Accent.b>WorkshopTheme.Accent.g,"Shared menus use a blue accent");
            foreach(var detail in EngineLearningContent.Data.details)Check(!string.IsNullOrEmpty(detail.voiceId) && NarrationText.Find(detail.voiceId)!=null,"Display voice text covers "+detail.partId);
            yield return Load("AssemblyWorkshop");var manager=FindAnyObjectByType<AssemblyManager>();while(manager.Recipe==null)yield return null;yield return null;
            var ui=manager.GetComponent<AssemblyWorkshopUI>();ui.SetOpen(false);
            Check(manager.Recipe.parts.Where(p=>p.chapterId=="chapter-1" && p.order==102).All(p=>p.orientation==AssemblyOrientation.UpsideDown),"All initial bearings require the PC underside flip");
            var narrator=manager.GetComponent<AssemblyNarrator>();var fake=ScriptableObject.CreateInstance<NarrationLibrary>();fake.cues=new[]{"chapter-1-cue-001","chapter-1-cue-002","chapter-1-cue-168","chapter-1-cue-169","flip-upside-down"}.Select(id=>new NarrationLibrary.Cue{id=id,clip=AudioClip.Create(id,24000,1,24000,false)}).ToArray();
            typeof(AssemblyNarrator).GetField("library",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(narrator,fake);manager.SelectChapter(0);yield return null;yield return null;yield return null;
            Check(narrator.CurrentCueId=="chapter-1-cue-001","Initial component lesson starts");manager.FindPart("101_Cylinder_Block_01").InstallImmediate(manager.FindPart("101_Cylinder_Block_01").TargetSocket);yield return null;
            var caption=manager.GetComponentInChildren<LiveNarrationCaption>();Check(caption && caption.Label.text.Contains("<color=#"),"Live captions bind even when the narrator is created after the HUD");
            Check(narrator.CurrentCueId=="chapter-1-cue-001","Required flip does not interrupt current lesson");
            float flipDeadline=Time.realtimeSinceStartup+8;while((narrator.CurrentCueId!="flip-upside-down" || !narrator.Source.isPlaying) && Time.realtimeSinceStartup<flipDeadline)yield return null;
            Check(narrator.CurrentCueId=="flip-upside-down","Flip voice plays after the lesson and pause");
            Check(narrator.LastCueStartedAt-narrator.LastCueFinishedAt>=.70f,"Successive voice cues have a deliberate pause");
            Check(NarrationText.Find("inventory-stack")==null,"The multiple-parts announcement is absent from the narration catalogue");
            yield return PistonChoices(manager);
            Check(manager.Recipe.chapters.FindIndex(c=>c.id==PCDisplayChapter.ChapterId)>manager.Recipe.chapters.FindIndex(c=>c.id==PCRepairChapter.ChapterId),"PC display follows repair");
            manager.SelectChapter(manager.Recipe.chapters.FindIndex(c=>c.id==PCDisplayChapter.ChapterId));yield return null;
            Check(manager.DisplayActive && manager.Recipe.parts.All(p=>manager.IsPlaced(p.id)),"Display chapter builds the complete engine");
            var block=manager.FindPart("101_Cylinder_Block_01");var bounds=AssemblyHeldView.WorldBounds(block);
            Check(manager.GetComponent<PCDisplayChapter>().Inspect(new Ray(bounds.center+Vector3.back*4,Vector3.forward)),"PC display ray selects an engine component");yield return null;Capture("pc-display-blue",Camera.main);
            ui.Open(AssemblyWorkshopUI.Page.Chapters);yield return null;Capture("chapters-blue",Camera.main);ui.Open(AssemblyWorkshopUI.Page.Settings);yield return null;Capture("settings-blue",Camera.main);
            yield return Load("MobileEngineDisplay");var display=FindAnyObjectByType<MobileEngineDisplay>();while(!display.Ready)yield return null;
            var labels=display.GetComponentsInChildren<TextMeshProUGUI>(true).Select(t=>t.text).ToArray();Check(!labels.Contains("START") && !labels.Contains("COMPLETE") && !labels.Contains("×"),"Circled display controls removed");
            display.SetPlaybackSpeed(2);Check(display.PlaybackSpeed==2,"Display supports faster silent assembly playback");display.SetPlaybackSpeed(.25f);Check(display.PlaybackSpeed==.25f,"Display animation supports slow motion");display.SetTimeline(.15f);var before=display.TimelinePosition;display.TogglePlayback();yield return new WaitForSecondsRealtime(.8f);
            Check(display.TimelinePosition>before && display.TimelinePosition-before<.01f,"Slow motion advances the actual assembly timeline");
            var direction=(display.WorldFocus-display.ViewCamera.transform.position).normalized;Check(Vector3.Dot(direction,display.ViewCamera.transform.forward)>.98f,"Timeline orbits around the workspace without chasing the individual part");
            Check(Vector3.Distance(display.ViewCamera.transform.position,display.WorldFocus)>display.PresentationRadius*2,"Timeline camera keeps the assembly context in frame");display.TogglePlayback();display.SetPlaybackSpeed(1);display.SetTimeline(1);display.ResetView();Capture("display-blue",display.ViewCamera);
            CheckDisplayStages(display);
            UnityEngine.Events.UnityAction<Scene,LoadSceneMode> prepareDisplay=(scene,mode)=>{if(scene.name!="MobileDisplayAR")return;var session=FindAnyObjectByType<MobileARSession>();session.Configure(session.gameObject.AddComponent<ARTestTrackingProvider>(),session.FallbackScene);};
            SceneManager.sceneLoaded+=prepareDisplay;yield return Load("MobileDisplayAR");SceneManager.sceneLoaded-=prepareDisplay;
            display=FindAnyObjectByType<MobileEngineDisplay>();while(!display.Ready)yield return null;var displayAR=display.GetComponent<MobileARSession>();while(displayAR.State==MobileARSession.SessionState.Starting)yield return null;displayAR.Place();
            Check(displayAR.AllowsInteraction,"Tracked display places its anchored engine");CheckDisplayStages(display);
            yield return Load("MobileAssembly");var phone=FindAnyObjectByType<MobileAssemblyController>();while(!phone.Ready)yield return null;manager=phone.GetComponent<AssemblyManager>();
            Check(!manager.RequiresWorkpieceOrientation,"Phone placement remains orientation independent");
            phone.ChooseChapter(1);yield return null;
            var pistonGroups=manager.Recipe.subAssemblies.Where(g=>g.exclusiveBuildGroup=="pistons").ToArray();
            Check(pistonGroups.All(g=>phone.VisiblePartIds.Contains(g.previewPartId)),"Phone offers all three unstarted piston choices");
            var third=manager.FindPart(pistonGroups[2].previewPartId);Check(phone.SelectPart(third.PartId),"Phone can start cylinder three first");Check(phone.ActiveWorkspaceId==pistonGroups[2].id,"Selecting a piston activates its own workspace");
            Check((bool)typeof(AssemblyPart).GetMethod("GuidedSnap",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(third,new object[]{third.TargetSocket}),"Phone places the chosen starting piston");yield return null;yield return null;
            Check(third.GetComponentsInChildren<MeshRenderer>().Any(r=>r.enabled),"Fitted piston pieces remain visible in the phone workspace");
            Check(!phone.SelectPart(pistonGroups[0].previewPartId),"Phone locks other piston units while the chosen one is incomplete");
            // Generate thumbnails from the actual surface materials, not the old white CAD preview.
            if(!File.Exists("Tools/Verification/material-cards-generated.txt"))
            {
                foreach(var p in manager.AssemblyParts){p.gameObject.SetActive(true);foreach(var r in p.GetComponentsInChildren<Renderer>(true))r.enabled=true;}
                foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))light.intensity=.9f;
                phone.ViewCamera.backgroundColor=WorkshopTheme.Background;MobileAssemblyBuilder.GenerateArtwork(manager.AssemblyParts.ToList(),phone.ViewCamera,true);AssetDatabase.SaveAssets();File.WriteAllText("Tools/Verification/material-cards-generated.txt","Material thumbnails at full camera viewport.");
            }
            Check(AssetDatabase.LoadAssetAtPath<MobilePartArtwork>("Assets/UI/MobilePartArtwork.asset").parts.Count>=317,"Material thumbnails regenerated for physical parts and carriers");
            yield return Load("MobileAssembly");phone=FindAnyObjectByType<MobileAssemblyController>();while(!phone.Ready)yield return null;yield return null;Capture("assembly-blue",phone.ViewCamera);
            ExperienceMenu.MobileRepairRequested=true;yield return Load("MobileAssembly");phone=FindAnyObjectByType<MobileAssemblyController>();while(!phone.Ready)yield return null;manager=phone.GetComponent<AssemblyManager>();
            Check(manager.RepairActive,"Mobile repair opens its shared repair sequence");
            yield return CompleteRepair(phone);
            phone.ChooseChapter(0);yield return null;Check(!manager.RepairActive,"Leaving touch repair cleans up the replacement");
            UnityEngine.Events.UnityAction<Scene,LoadSceneMode> prepare=(scene,mode)=>{if(scene.name!="MobileAssemblyAR")return;var session=FindAnyObjectByType<MobileARSession>();session.Configure(session.gameObject.AddComponent<ARTestTrackingProvider>(),session.FallbackScene);};
            SceneManager.sceneLoaded+=prepare;yield return Load("MobileAssemblyAR");SceneManager.sceneLoaded-=prepare;
            phone=FindAnyObjectByType<MobileAssemblyController>();while(!phone.Ready)yield return null;var ar=phone.GetComponent<MobileARSession>();while(ar.State==MobileARSession.SessionState.Starting)yield return null;ar.Place();
            Check(ar.AllowsInteraction,"Tracked AR repair anchors at working height");yield return CompleteRepair(phone);ExperienceMenu.MobileRepairRequested=false;
            yield return Load("AssemblyWorkshop");manager=FindAnyObjectByType<AssemblyManager>();while(manager.Recipe==null)yield return null;yield return null;manager.GetComponent<AssemblyWorkshopUI>().SetOpen(false);
            QuestVRSession.Instance.EnableVerification();yield return null;Check(FindAnyObjectByType<QuestVRRig>() && FindObjectsByType<Canvas>(FindObjectsSortMode.None).Any(c=>c.renderMode==RenderMode.WorldSpace),"VR reuses the PC workshop and tracked UI");
            manager.SelectChapter(manager.Recipe.chapters.FindIndex(c=>c.id==PCDisplayChapter.ChapterId));Check(manager.DisplayActive,"VR can enter display after repair");Destroy(QuestVRSession.Instance.gameObject);yield return null;
            ExperienceMenu.MobileRepairRequested=true;yield return Load("MobileAssemblyAR");float deadline=Time.realtimeSinceStartup+20;
            while(SceneManager.GetActiveScene().name!="MobileAssemblyARFallback" && Time.realtimeSinceStartup<deadline)yield return null;
            manager=FindAnyObjectByType<AssemblyManager>();while(manager && manager.Recipe==null)yield return null;yield return null;yield return null;
            Check(SceneManager.GetActiveScene().name=="MobileAssemblyARFallback" && manager.RepairActive,"AR repair falls back to the same repair on unsupported platforms");ExperienceMenu.MobileRepairRequested=false;
        }
        IEnumerator PistonChoices(AssemblyManager manager)
        {
            var groups=manager.Recipe.subAssemblies.Where(g=>g.exclusiveBuildGroup=="pistons").ToArray();
            foreach(var permutation in new[]{new[]{0,1,2},new[]{0,2,1},new[]{1,0,2},new[]{1,2,0},new[]{2,0,1},new[]{2,1,0}})
            {
                manager.SelectChapter(1);
                Check(groups.All(g=>manager.CanPickUp(manager.FindPart(g.previewPartId),out _) && manager.CanInstall(manager.FindPart(g.previewPartId),manager.FindPart(g.previewPartId).TargetSocket,out _)),"Any piston may start build order "+string.Join(",",permutation));
                foreach(int index in permutation)
                {
                    var group=groups[index];var members=manager.Members(group).OrderBy(p=>p.Definition.order).ThenByDescending(p=>p.PartId).ToArray();
                    foreach(var part in members)
                    {
                        Check(manager.CanInstall(part,part.TargetSocket,out var reason),"Local piston fitting order allows "+part.PartId+": "+reason);
                        part.InstallImmediate(part.TargetSocket);
                        if(manager.GroupComplete(group))continue;
                        foreach(var other in groups.Where(g=>g!=group && !manager.GroupComplete(g)))
                        {
                            var start=manager.FindPart(other.previewPartId);Check(!manager.CanPickUp(start,out _) && !manager.CanInstall(start,start.TargetSocket,out _),"Partial piston locks "+other.id);
                        }
                        Check(manager.MemberGroup(manager.NextHintPart())==group,"Hints stay with the active piston");
                    }
                    Check(manager.GroupComplete(group),"Chosen piston completes before the next unit");
                }
                Check(groups.All(manager.GroupComplete),"All piston units finish in chosen build order");yield return null;
            }
        }
        void CheckDisplayStages(MobileEngineDisplay display)
        {
            var steps=EngineLearningContent.Timeline(display.Recipe);
            foreach(var group in display.Recipe.subAssemblies)
            {
                int index=steps.FindIndex(p=>p.id==group.previewPartId);display.SetTimeline((index+.6f)/steps.Count);
                Check(display.CurrentWorkspaceId==group.id && !display.IsPartVisible("101_Cylinder_Block_01"),"Display isolates "+group.id+" from the main engine");
                Check(display.IsPartVisible(group.previewPartId),"The isolated unit shows its fitting component");
                Check(display.Recipe.parts.Where(p=>p.subAssemblyId!=group.id).All(p=>!display.IsPartVisible(p.id)),"Other display workspaces stay hidden");
                CheckClearDisplaySurface(display,group.previewPartId);
            }
            foreach(var prefix in new[]{"102_Crankshaft_Needle","403_","405_Valve","407_Valve","305_Flywheel","703_Turbo"})
            {
                int index=steps.FindIndex(p=>p.id.StartsWith(prefix));if(index<0)continue;
                display.SetTimeline((index+.05f)/steps.Count);display.SetTimeline((index+.97f)/steps.Count);CheckClearDisplaySurface(display,steps[index].id);
            }
            display.SetTimeline(1);Check(display.CurrentWorkspaceId=="engine" && display.VisiblePartCount==317 && !display.CutawayActive,"Complete display restores every engine component");
            Check(display.GetComponentsInChildren<TextMeshProUGUI>(true).All(t=>!t.text.Contains('\u2014')),"Display UI contains no em dashes");
        }
        void CheckClearDisplaySurface(MobileEngineDisplay display,string id)
        {
            var mesh=display.EngineRoot.GetComponentsInChildren<MeshFilter>().First(m=>m.name==id);var vertices=mesh.sharedMesh.vertices;var triangles=mesh.sharedMesh.triangles;bool clear=false;
            bool backfaces=Physics.queriesHitBackfaces;Physics.queriesHitBackfaces=true;
            try
            {
                for(int i=0;i+2<triangles.Length;i+=Mathf.Max(3,triangles.Length/64/3*3))
                {
                    var point=mesh.transform.TransformPoint((vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3);var delta=point-display.ViewCamera.transform.position;
                    var viewport=display.ViewCamera.WorldToViewportPoint(point);if(viewport.z<=display.ViewCamera.nearClipPlane || viewport.x<0 || viewport.x>1 || viewport.y<0 || viewport.y>1)continue;
                    var first=Physics.RaycastAll(display.ViewCamera.transform.position,delta.normalized,delta.magnitude+.02f,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider is MeshCollider && h.transform.IsChildOf(display.EngineRoot)).OrderBy(h=>h.distance).FirstOrDefault();
                    if(first.collider && first.transform==mesh.transform){clear=true;break;}
                }
            }
            finally{Physics.queriesHitBackfaces=backfaces;}
            Check(clear && display.IsPartVisible(id),"Camera has a clear surface view of fitting "+id);
        }
        IEnumerator CompleteRepair(MobileAssemblyController phone)
        {
            var manager=phone.GetComponent<AssemblyManager>();
            for(int i=0;i<manager.Repair.StepCount;i++)
            {
                var part=manager.Repair.NextPart;
                if(manager.Repair.IsPlacementStep){Check(phone.SelectPart(part.PartId),"Repair card selects "+part.PartId);Check((bool)typeof(AssemblyPart).GetMethod("GuidedSnap",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(part,new object[]{part.TargetSocket}),"Repair replacement/reassembly fits "+part.PartId);}
                else
                {
                    bool removed=false;var b=AssemblyHeldView.WorldBounds(part);foreach(var direction in new[]{Vector3.back,Vector3.forward,Vector3.right,Vector3.left,Vector3.up,Vector3.down})
                    {
                        phone.ViewCamera.transform.position=b.center+direction*Mathf.Max(.25f,b.extents.magnitude*2.5f);phone.ViewCamera.transform.LookAt(b.center);
                        var points=new List<Vector3>{b.center};foreach(var mesh in part.GetComponentsInChildren<MeshFilter>()){if(!mesh.sharedMesh)continue;var vertices=mesh.sharedMesh.vertices;var triangles=mesh.sharedMesh.triangles;for(int t=0;t<Mathf.Min(triangles.Length,72);t+=3)points.Add(mesh.transform.TransformPoint((vertices[triangles[t]]+vertices[triangles[t+1]]+vertices[triangles[t+2]])/3));}
                        foreach(var point in points)if(phone.TapTarget(phone.ViewCamera.WorldToScreenPoint(point))){removed=true;break;}
                        if(removed)break;
                    }
                    Check(removed,"Visible touch removes "+part.PartId);
                }
                yield return null;yield return null;
            }
            Check(manager.Repair.Complete,"Complete mobile repair replaces the faulty bearing");
            yield return null;
        }
    }
}

