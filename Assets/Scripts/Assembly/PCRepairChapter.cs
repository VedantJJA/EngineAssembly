using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EngineAssembly
{
    // Shared repair sequence for desktop, tracked VR and touch/AR.
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
            new Step("512_Timing_Belt_01",false,"We will replace one bearing inside a timing-belt guide pulley. A worn bearing can stop the pulley turning freely. First find the toothed belt on the end opposite the flywheel. Lift it off the large pulleys and small idlers, then set it on the parts table. This gives access to the faulty idler."),
            new Step("511_Idler_Shaft_Circlip_02",false,"Find the first small timing idler, labelled timing idler one. The split ring on the exposed end of its shaft stops the pulley sliding off. Remove this outer ring and set it aside. Leave the inner ring, behind the pulley, on the shaft."),
            new Step(UnitId,false,"Slide the whole first idler off its fixed shaft. Carry it to the small work platform and set it down with its centre opening accessible. We are removing the pulley and its internal bearings together, not removing the shaft from the engine."),
            new Step("511_Idler_Bore_Circlip_01",false,"Look inside the pulley opening. The two rings in grooves inside this opening hold the bearings in the pulley. Remove the first internal retaining ring and set it aside for reuse. These internal rings are different from the ring you removed from the engine shaft."),
            new Step("511_Idler_Bore_Circlip_02",false,"Remove the second internal retaining ring from the other end of the pulley opening. Set it aside with the first. Both bearings are now free of their retaining rings, so we can reach the damaged bearing without forcing it past a clip."),
            new Step("510_Idler_Ball_Bearing_6203_02",false,"Remove the outer bearing ring from the pulley centre and set it aside for reuse. Its balls let the pulley rotate around the fixed shaft. This bearing is still good; removing it gives access to the inner bearing that needs replacement."),
            new Step(FaultyId,false,"Remove the faulty inner bearing from the now-open pulley centre. Set this damaged bearing aside separately. We will use a new bearing of the same size instead of putting the faulty one back."),
            new Step(ReplacementId,true,"Pick up the part labelled New replacement timing idler bearing. Fit it into the empty inner bearing position inside the pulley centre. Its outer ring seats in the pulley, and its small centre hole lines up with the pulley axis."),
            new Step("510_Idler_Ball_Bearing_6203_02",true,"Pick up the original good outer bearing. Refit it into the remaining seat at the outer end of the pulley opening. The two small bearing holes must line up so the engine's fixed shaft can pass through both."),
            new Step("511_Idler_Bore_Circlip_01",true,"Refit the first internal split ring into its groove inside the pulley opening, just outside the bearing. It stops the bearing sliding out. Keep the centre opening clear; the ring sits at the edge of the opening."),
            new Step("511_Idler_Bore_Circlip_02",true,"Refit the second internal split ring in the groove at the other end of the opening. Both bearings are now retained. The rebuilt idler contains one new bearing and one reused bearing, held by the original two internal rings."),
            new Step(UnitId,true,"Pick up the rebuilt idler as one unit. Slide its bearing holes over the original shaft on the engine's timing-idler plate. Move it inward until it reaches the inner shaft ring that you left in place. Leave the outer shaft groove visible."),
            new Step("511_Idler_Shaft_Circlip_02",true,"Refit the outer shaft clip into the exposed groove just beyond the pulley. The two shaft clips now keep the whole idler on the engine, while its internal bearings allow it to turn."),
            new Step("512_Timing_Belt_01",true,"Refit the toothed timing belt around the lower crankshaft pulley, the two upper camshaft pulleys and the small guide idlers. You have replaced the faulty bearing and reused the good parts. The belt again links piston movement to valve movement. This simulation shows the fitted route; real timing and tension checks require engine-specific instructions.")
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
            if(part && part.PartId!=UnitId && manager.TryGetComponent<WorkshopSupplyLayout>(out var supplies))return supplies.RepairDestination;
            var platform=part && part.PartId==UnitId?GameObject.Find("Portable Work Platform"):manager.TrayFrame.gameObject;
            var surface=platform?platform.GetComponent<Renderer>():null;
            return surface?new Vector3(surface.bounds.center.x,surface.bounds.max.y+.25f,surface.bounds.center.z):manager.TrayFrame.position;
        }
        void StageUnit(AssemblyPart part)
        {
            if(manager.GetComponent<MobileAssemblyController>())
            {
                StageCentered(part,manager.AssemblyRoot.position+manager.AssemblyRoot.TransformVector(new Vector3(2,.15f,0)));
                var rig=manager.GetComponent<SpatialExperienceRig>();if(rig && rig.IsTracked){part.transform.SetParent(rig.ContentAnchor,true);manager.GetComponent<MobileARSession>()?.RememberHome(part.transform);}
                SetAsidePosition=AssemblyHeldView.WorldBounds(part).center;return;
            }
            var platform=GameObject.Find("Portable Work Platform");
            var bounds=platform?platform.GetComponent<Renderer>().bounds:new Bounds(manager.AssemblyRoot.position+Vector3.right*2,Vector3.one);
            StageCentered(part,new Vector3(bounds.center.x,bounds.max.y+.03f,bounds.center.z));
            SetAsidePosition=AssemblyHeldView.WorldBounds(part).center;
        }
        void StageOnTable(AssemblyPart part,int index)
        {
            if(manager.TryGetComponent<WorkshopSupplyLayout>(out var supplies)){supplies.StageRepairPart(part);return;}
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
