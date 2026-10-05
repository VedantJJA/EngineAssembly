using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EngineAssembly
{
    // A PC-only service exercise. It reuses assembly sockets without changing the saved recipe.
    [DisallowMultipleComponent, RequireComponent(typeof(AssemblyManager))]
    public sealed class PCRepairChapter : MonoBehaviour
    {
        public const string ChapterId="bonus-repair";
        public const string FaultyId="510_Idler_Ball_Bearing_6203_01";
        public const string ReplacementId="Repair_Replacement_Timing_Idler_Bearing";
        public const string UnitId="530_Timing_Idler_Unit_01";
        public sealed class Step
        {
            public string partId,instruction;public bool install;
            public Step(string id,bool fit,string text){partId=id;install=fit;instruction=text;}
        }
        readonly List<Step> steps=new List<Step>
        {
            new Step("512_Timing_Belt_01",false,"Remove the timing belt to expose the idler. Pick it up, then press G to set it aside."),
            new Step("511_Idler_Shaft_Circlip_02",false,"Remove the outer shaft circlip from timing idler 1. Press G to set it aside; leave the inner shaft clip fitted."),
            new Step(UnitId,false,"Pull timing idler 1 off its shaft as a complete unit. Press G to put it on the small work platform."),
            new Step("511_Idler_Bore_Circlip_01",false,"At the small platform, remove the first bore circlip retaining the idler bearings. Press G to set it aside."),
            new Step("511_Idler_Bore_Circlip_02",false,"Remove the second bore circlip from the idler and press G to set it aside."),
            new Step("510_Idler_Ball_Bearing_6203_02",false,"Remove the outer idler bearing to reach the faulty inner bearing. Press G to set it aside for reuse."),
            new Step(FaultyId,false,"Remove the faulty inner idler bearing. Press G to discard it from this repair; use the new replacement bearing next."),
            new Step(ReplacementId,true,"Pick up the new replacement bearing from the parts table and fit it into the vacant inner bearing seat in the idler."),
            new Step("510_Idler_Ball_Bearing_6203_02",true,"Refit the original outer bearing into the remaining idler bearing seat."),
            new Step("511_Idler_Bore_Circlip_01",true,"Refit the first bore circlip to retain the bearings."),
            new Step("511_Idler_Bore_Circlip_02",true,"Refit the second bore circlip. The idler is complete again."),
            new Step(UnitId,true,"Pick up the rebuilt timing idler and fit it back onto its shaft, against the retained inner shaft circlip."),
            new Step("511_Idler_Shaft_Circlip_02",true,"Refit the outer shaft circlip to secure the rebuilt idler."),
            new Step("512_Timing_Belt_01",true,"Refit the timing belt around its original pulleys and idlers to complete the repair.")
        };
        AssemblyManager manager;AssemblyPart replacement;string originalSocketGeometry;
        bool running;
        float supplyX,supplyZ,supplyDepth;
        public bool Active=>running && manager && manager.Mode==WorkshopMode.Assembly && manager.CurrentChapterId==ChapterId;
        public int CompletedSteps { get; private set; }
        public int StepCount=>steps.Count;
        public bool Complete=>Active && CompletedSteps==steps.Count;
        public Step CurrentStep=>Active && CompletedSteps<steps.Count?steps[CompletedSteps]:null;
        public AssemblyPart NextPart=>CurrentStep==null?null:manager.FindPart(CurrentStep.partId);
        public bool IsPlacementStep=>CurrentStep?.install??false;
        public AssemblyPart Replacement=>replacement;
        public IReadOnlyList<Step> Steps=>steps;
        public Vector3 SetAsidePosition { get; private set; }
        void Awake(){manager=GetComponent<AssemblyManager>();EnsureChapter();}
        public void EnsureChapter()
        {
            if(manager.Recipe==null || steps.Any(s=>s.partId!=ReplacementId && !manager.FindPart(s.partId)))return;
            if(!manager.Recipe.chapters.Any(c=>c.id==ChapterId))manager.Recipe.chapters.Add(new ChapterDefinition{id=ChapterId,title="Repair",description="Replace a faulty timing-idler bearing, then rebuild the timing drive."});
        }
        public void Begin()
        {
            if(manager.CurrentChapterId!=ChapterId || manager.Mode!=WorkshopMode.Assembly)return;
            var original=manager.FindPart(FaultyId);if(!original)return;
            var copy=Instantiate(original.gameObject);copy.name=ReplacementId;copy.transform.localScale=original.transform.lossyScale;
            replacement=copy.GetComponent<AssemblyPart>();
            var definition=JsonUtility.FromJson<PartDefinition>(JsonUtility.ToJson(original.Definition));
            definition.id=ReplacementId;definition.displayName="New replacement · timing idler bearing";
            definition.geometryGroup="repair-bearing-01";
            replacement.Configure(definition,manager);replacement.TargetSocket=original.TargetSocket;
            originalSocketGeometry=original.TargetSocket.GeometryGroupId;original.TargetSocket.GeometryGroupId=definition.geometryGroup;
            manager.RegisterRepairPart(replacement);
            var table=manager.TrayFrame.GetComponent<Renderer>();var tableBounds=table?table.bounds:new Bounds(manager.TrayFrame.position,new Vector3(4,1,3));
            supplyX=tableBounds.min.x+.25f;supplyZ=tableBounds.min.z+.25f;supplyDepth=0;
            replacement.ResetLoose(manager.TrayFrame,definition.trayPosition,definition.trayRotation);
            // A distinct supply position avoids overlap with the reused, removed bearing.
            StageOnTable(replacement,0);
            CompletedSteps=0;running=true;manager.Report(CurrentStep.instruction);
        }
        public void End()
        {
            if(running){var player=FindAnyObjectByType<PlayerAssemblyController>();if(player && player.CurrentHeldPart)player.DropHeldPart(false);}
            running=false;
            if(!replacement)return;
            replacement.CancelSnap();replacement.Unsnap(true);
            var original=manager.FindPart(FaultyId);if(original){original.TargetSocket.GeometryGroupId=originalSocketGeometry;original.gameObject.SetActive(true);}
            manager.UnregisterRepairPart(replacement);replacement.gameObject.SetActive(false);Destroy(replacement.gameObject);replacement=null;
        }
        void Update()
        {
            var step=CurrentStep;var part=NextPart;
            if(step==null || step.install || !part || part.IsSnapped || part.IsSelected || part.IsBusy)return;
            if(part.PartId==UnitId)StageUnit(part);
            else if(part.PartId==FaultyId){part.gameObject.SetActive(false);}
            else StageOnTable(part,CompletedSteps+1);
            Advance();
        }
        void Advance(){CompletedSteps++;manager.Report(Complete?"Repair complete. New bearing fitted and timing drive rebuilt.":CurrentStep.instruction);}
        public void OnInstalled(AssemblyPart part){if(CurrentStep?.install==true && part==NextPart)Advance();}
        public bool CanPickUp(AssemblyPart part,out string reason)
        {
            reason=Complete?"Repair complete.":CurrentStep?.instruction??"Choose Repair to start.";
            return part && part.Manager==manager && part==NextPart && !part.IsBusy && (!IsPlacementStep || !part.IsSnapped);
        }
        public bool CanRemove(AssemblyPart part,out string reason)
        {
            reason="Follow the current repair removal step.";
            return CurrentStep!=null && !CurrentStep.install && part==NextPart && part.IsSnapped;
        }
        public bool CanInstall(AssemblyPart part,AssemblySocket socket,out string reason)
        {
            reason="Follow the current repair replacement or reassembly step.";
            return CurrentStep?.install==true && part==NextPart && socket==part.TargetSocket;
        }
        public AssemblyPart InteractionPart(AssemblyPart part)
        {
            var group=manager.MemberGroup(part);
            return NextPart && NextPart.PartId==UnitId && group?.rootPartId==UnitId?NextPart:part;
        }
        public Vector3 RemovalDestination(AssemblyPart part)
        {
            var platform=part && part.PartId==UnitId?GameObject.Find("Portable Work Platform"):manager.TrayFrame.gameObject;
            var surface=platform?platform.GetComponent<Renderer>():null;
            return surface?new Vector3(surface.bounds.center.x,surface.bounds.max.y+.25f,surface.bounds.center.z):manager.TrayFrame.position;
        }
        void StageUnit(AssemblyPart part)
        {
            var platform=GameObject.Find("Portable Work Platform");
            var bounds=platform?platform.GetComponent<Renderer>().bounds:new Bounds(manager.AssemblyRoot.position+Vector3.right*2,Vector3.one);
            StageCentered(part,new Vector3(bounds.center.x,bounds.max.y+.03f,bounds.center.z));
            SetAsidePosition=AssemblyHeldView.WorldBounds(part).center;
        }
        void StageOnTable(AssemblyPart part,int index)
        {
            var renderer=manager.TrayFrame.GetComponent<Renderer>();
            var bounds=renderer?renderer.bounds:new Bounds(manager.TrayFrame.position,new Vector3(4,1,3));
            var size=AssemblyHeldView.WorldBounds(part).size;float width=Mathf.Max(.3f,size.x+.18f),depth=Mathf.Max(.3f,size.z+.18f);
            if(supplyX+width>bounds.max.x-.25f){supplyX=bounds.min.x+.25f;supplyZ+=supplyDepth+.2f;supplyDepth=0;}
            StageCentered(part,new Vector3(supplyX+width*.5f,bounds.max.y+.03f,supplyZ+depth*.5f));
            supplyX+=width+.2f;supplyDepth=Mathf.Max(supplyDepth,depth);
        }
        static void StageCentered(AssemblyPart part,Vector3 bottom)
        {
            var bounds=AssemblyHeldView.WorldBounds(part);var origin=part.transform.position+bottom+Vector3.up*bounds.extents.y-bounds.center;
            part.ResetLoose(part.Manager.TrayFrame,part.Manager.TrayFrame.InverseTransformPoint(origin),Quaternion.Inverse(part.Manager.TrayFrame.rotation)*part.transform.rotation);
        }
        void OnDestroy(){if(replacement)replacement.gameObject.SetActive(false);}
    }
}
