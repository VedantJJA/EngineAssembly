using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EngineAssembly.Editor
{
    public static class VuforiaGroundPlaneRebuildVerification
    {
        [Serializable] sealed class Report{public bool passed;public string error;public string[] checks;}
        public static void RunBatch()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Run in the isolated verification project.");
            var checks=new List<string>();string error="";
            void Check(bool valid,string message){if(!valid)throw new Exception(message);checks.Add(message);}
            try
            {
                // Force replacement, then run the idempotent build entry point.
                VuforiaSceneSetup.Rebuild();VuforiaSceneSetup.Create();
                foreach(string path in new[]{VuforiaSceneSetup.AssemblyPath,VuforiaSceneSetup.DisplayPath})
                {
                    var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                    var roots=scene.GetRootGameObjects();var nodes=roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
                    Check(roots.Length==2 && roots.Any(r=>r.name=="Engine Experience") && roots.Any(r=>r.name=="Vuforia Ground Plane"),path+": fresh gameplay and Vuforia roots");
                    Check(nodes.All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),path+": no missing scripts");
                    var provider=nodes.SelectMany(t=>t.GetComponents<VuforiaGroundPlaneProvider>()).Single();
                    Check(nodes.SelectMany(t=>t.GetComponents<MobileARSession>()).Count()==1,path+": one AR session");
#if ENGINE_VUFORIA
                    Check(provider.SceneConfigured,path+": current scene configuration is complete");
                    var engine=provider.TrackingCamera.GetComponent<Vuforia.VuforiaBehaviour>();
                    Check(nodes.SelectMany(t=>t.GetComponents<Vuforia.VuforiaBehaviour>()).Count()==1,path+": one Vuforia camera");
                    Check(provider.TrackingCamera.gameObject.activeInHierarchy && !provider.TrackingCamera.enabled && !engine.enabled,path+": camera loads early but waits for permission to start");
                    Check(provider.TrackingCamera.depth>=1 && !provider.TrackingCamera.allowHDR,path+": AR feed renders over fallback cameras without HDR");
                    Check(provider.PlaneFinder.gameObject.activeInHierarchy && provider.PlaneFinder.enabled && provider.PlaneFinder.HitTestMode==Vuforia.HitTestMode.AUTOMATIC,path+": SDK automatic scanning is active before initialization");
                    Check(!provider.PlaneFinder.GetComponent<Vuforia.AnchorInputListenerBehaviour>(),path+": UI owns placement input");
                    Check(provider.ContentPositioning.AnchorStage==provider.GroundPlaneStage && !provider.ContentPositioning.DuplicateStage,path+": one persistent Ground Plane anchor");
                    Check(provider.PlaneFinder.OnAutomaticHitTest.GetPersistentTarget(0)==provider && provider.PlaneFinder.OnAutomaticHitTest.GetPersistentMethodName(0)==nameof(provider.HandleSurfaceHit),path+": SDK automatic hit event reaches provider");
                    Check(provider.ContentPositioning.OnContentPlaced.GetPersistentTarget(0)==provider && provider.ContentPositioning.OnContentPlaced.GetPersistentMethodName(0)==nameof(provider.HandleContentPlaced),path+": successful native anchor creation reaches provider");
                    Check(provider.GroundPlaneStage.GetComponents<DefaultObserverEventHandler>().Length==0,path+": SDK visibility handler cannot hide gameplay");
                    Check(provider.PlaneFinder.PlaneIndicator && provider.PlaneFinder.PlaneIndicator.scene==scene,path+": surface indicator is scene-owned");
#endif
                    var rig=provider.GetComponent<SpatialExperienceRig>();
                    Check(rig.ContentAnchor && rig.ContentAnchor.gameObject.scene==scene && rig.PresentationCamera.gameObject.scene==scene,path+": content and fallback camera references belong to rebuilt scene");
                    var manager=provider.GetComponent<AssemblyManager>();
                    if(manager)
                    {
                        Check(manager.AssemblyParts.Count>300 && manager.AssemblyParts.All(p=>p && p.gameObject.scene==scene && p.TargetSocket && p.TargetSocket.gameObject.scene==scene),path+": every authored part and socket survives scene reconstruction");
                        Check(manager.AssemblyRoot.IsChildOf(rig.ContentAnchor) && manager.TrayFrame.gameObject.scene==scene,path+": assembly frames retain valid workflow references");
                    }
                    else Check(provider.GetComponent<MobileEngineDisplay>() && provider.GetComponent<MobileEngineDisplay>().EngineRoot.IsChildOf(rig.ContentAnchor),path+": display keeps its engine and timeline workflow");
                }
            }
            catch(Exception exception){error=exception.ToString();Debug.LogException(exception);}
            Directory.CreateDirectory("Tools/Verification");File.WriteAllText("Tools/Verification/ar-rebuild-scene-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,error=error,checks=checks.ToArray()},true));
            EditorApplication.Exit(error.Length==0?0:1);
        }
    }
}
