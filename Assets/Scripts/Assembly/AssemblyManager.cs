using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace EngineAssembly
{
    [DisallowMultipleComponent]
    public class AssemblyManager : MonoBehaviour
    {
        public static AssemblyManager Instance { get; private set; }
        [SerializeField] Transform assemblyRoot;
        [SerializeField] Transform trayFrame;
        [SerializeField] TextAsset initialRecipe;
        [SerializeField] List<AssemblyPart> assemblyParts = new List<AssemblyPart>();
        [SerializeField] bool showWorkshopUI = true;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip snapSound;
        public UnityEvent<AssemblyPart> onPartSnapped = new UnityEvent<AssemblyPart>(), onPartUnsnapped = new UnityEvent<AssemblyPart>();
        public UnityEvent onChapterCompleted = new UnityEvent(), onAssemblyCompleted = new UnityEvent();
        public event Action Changed;
        public AssemblyRecipe Recipe { get; private set; }
        PCRepairChapter repair;
        public PCRepairChapter Repair=>repair?repair:repair=GetComponent<PCRepairChapter>();
        public bool RepairActive=>Repair && Repair.Active;
        public bool DisplayActive=>CurrentChapterId==PCDisplayChapter.ChapterId;
        public bool IsPlayableChapter(string id)=>id==PCDisplayChapter.ChapterId || id==PCRepairChapter.ChapterId && Repair || Recipe.parts.Any(p=>p.chapterId==id);
        internal void RegisterRepairPart(AssemblyPart part){assemblyParts.Add(part);byId[part.PartId]=part;}
        internal void UnregisterRepairPart(AssemblyPart part){assemblyParts.Remove(part);byId.Remove(part.PartId);}
        public WorkshopMode Mode { get; private set; }
        public WorkshopInteraction Interaction { get; private set; }
        public AssemblyTask Task { get; private set; }
        public void SetTask(AssemblyTask task) { Task=task;if(Recipe!=null)SelectChapter(CurrentChapterIndex); }
        public SnapDifficulty Difficulty { get; private set; } = SnapDifficulty.Hard;
        public void SetDifficulty(SnapDifficulty difficulty, bool remember = true)
        {
            Difficulty = difficulty;if(remember) PlayerPrefs.SetInt("Assembly.SnapDifficulty", (int)difficulty); Changed?.Invoke();
        }
        public void SetInteraction(WorkshopInteraction interaction) { Interaction = interaction; Changed?.Invoke(); }
        public void AddChapter(string title)
        {
            if (Mode != WorkshopMode.Edit || Recipe == null) return;
            int index=Recipe.chapters.FindIndex(c=>c.id==PCRepairChapter.ChapterId);if(index<0)index=Recipe.chapters.Count;
            Recipe.chapters.Insert(index,new ChapterDefinition { id=Guid.NewGuid().ToString("N"), title=string.IsNullOrWhiteSpace(title)?"New chapter":title.Trim() });
            SelectChapter(index);
        }
        public int CurrentChapterIndex { get; private set; }
        public int ChapterVisit { get; private set; }
        public string CurrentChapterId => Recipe == null ? "" : Recipe.chapters[CurrentChapterIndex].id;
        [SerializeField] bool useHeadBenchPosition;
        [SerializeField] Vector3 headBenchPosition = new Vector3(2.4f,-.5f,0);
        public static Quaternion InitialFixtureRotation(SubAssemblyDefinition group)=>group.rootPartId.StartsWith("230_Piston_Rod_Unit_")?Quaternion.Euler(180,0,0):Quaternion.identity;
        public Vector3 BenchPosition(SubAssemblyDefinition group)=>useHeadBenchPosition && group.rootPartId=="480_Complete_Cylinder_Head"?headBenchPosition:group.benchPosition;
        public Transform AssemblyRoot => assemblyRoot;
        public Transform TrayFrame => trayFrame ? trayFrame : transform;
        public IReadOnlyList<AssemblyPart> AssemblyParts => assemblyParts;
        public bool IsPreparing { get; private set; }
        public string Status { get; private set; } = "";
        readonly Dictionary<string, AssemblyPart> byId = new Dictionary<string, AssemblyPart>();
        bool completionSent;
        [SerializeField,Range(.1f,1)] float engineScale=.6f;
        bool scaled;
        public float EngineScale=>engineScale;
        public bool RequiresWorkpieceOrientation=>!GetComponent<MobileAssemblyController>() && FindAnyObjectByType<PlayerAssemblyController>();
        public bool ChapterComplete => RepairActive?Repair.Complete:Recipe != null && Recipe.parts.Any(p => p.chapterId == CurrentChapterId) && Recipe.parts.Where(p => p.chapterId == CurrentChapterId && !p.isBase).All(p => Task==AssemblyTask.Assemble?IsPlaced(p.id):!IsPlaced(p.id));
        public int ChapterPartCount => RepairActive?Repair.StepCount:Recipe?.parts.Count(p => p.chapterId == CurrentChapterId && !p.isBase) ?? 0;
        public int ChapterInstalledCount => RepairActive?Repair.CompletedSteps:Recipe?.parts.Count(p => p.chapterId == CurrentChapterId && !p.isBase && IsPlaced(p.id)) ?? 0;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ClearInstance() => Instance = null;
        void Awake() { if (!Instance) Instance = this; Difficulty=(SnapDifficulty)Mathf.Clamp(PlayerPrefs.GetInt("Assembly.SnapDifficulty",0),0,2); EnsureFrames(); }
        void Start()
        {
            if (Recipe == null)
            {
                try { RefreshPartsList(); LoadRecipe(initialRecipe ? AssemblyRecipeStore.Parse(initialRecipe.text) : CaptureScene()); }
                catch (Exception ex) { Report(ex.Message); Debug.LogError(ex, this); }
            }
            if (showWorkshopUI && !GetComponent<AssemblyWorkshopUI>()) gameObject.AddComponent<AssemblyWorkshopUI>();
            if (!GetComponent<AssemblyEasyGuide>()) gameObject.AddComponent<AssemblyEasyGuide>();
            if (!GetComponent<AssemblyHint>()) gameObject.AddComponent<AssemblyHint>();
            if (!GetComponent<AssemblyStepAssist>()) gameObject.AddComponent<AssemblyStepAssist>();
            if (!GetComponent<AssemblyNarrator>()) gameObject.AddComponent<AssemblyNarrator>();
            if (FindAnyObjectByType<PlayerAssemblyController>() && !GetComponent<WorkpieceLift>()) gameObject.AddComponent<WorkpieceLift>();
        }
        void OnDestroy() { if (Instance == this) Instance = null; }
        void EnsureFrames()
        {
            // The spawn tray is a fixed supply surface. Portable work platforms
            // retain their own pickup setting, including in existing saved scenes.
            if(trayFrame && trayFrame!=assemblyRoot && trayFrame.TryGetComponent<PickupablePlatform>(out var supplyTable))supplyTable.SetPickupAllowed(false);
            if (!assemblyRoot)
            {
                assemblyRoot = new GameObject("Assembly Workpiece").transform;
                assemblyRoot.SetParent(transform,false);
            }
            if (!assemblyRoot.GetComponent<PickupablePlatform>()) assemblyRoot.gameObject.AddComponent<PickupablePlatform>();
        }
        public void Configure(Transform root, IEnumerable<AssemblyPart> parts, TextAsset recipe = null)
        {
            assemblyRoot = root; assemblyParts = parts.ToList(); initialRecipe = recipe; EnsureFrames();
        }
        public void RefreshPartsList()
        {
            assemblyParts = FindObjectsByType<AssemblyPart>(FindObjectsInactive.Include)
                .Where(p => p.gameObject.scene == gameObject.scene && (!p.Manager || p.Manager == this)).ToList();
        }
        public AssemblyPart FindPart(string id) => id != null && byId.TryGetValue(id, out var p) ? p : null;
        public SubAssemblyDefinition CarrierDefinition(string id)=>Recipe?.subAssemblies?.FirstOrDefault(s=>s.rootPartId==id);
        public SubAssemblyDefinition MemberGroup(AssemblyPart part)=>Recipe?.subAssemblies?.FirstOrDefault(s=>s.id==part?.Definition?.subAssemblyId);
        public IEnumerable<AssemblyPart> Members(SubAssemblyDefinition group)=>group==null?Enumerable.Empty<AssemblyPart>():Recipe.parts.Where(p=>p.subAssemblyId==group.id).Select(p=>FindPart(p.id)).Where(p=>p).Select(p=>p.TargetSocket && p.TargetSocket.CurrentPart?p.TargetSocket.CurrentPart:p);
        public bool GroupComplete(SubAssemblyDefinition group)=>group!=null && Members(group).All(p=>IsPlaced(p.PartId));
        public SubAssemblyDefinition ActiveExclusiveBuild(string choice)=>string.IsNullOrEmpty(choice)?null:Recipe?.subAssemblies.FirstOrDefault(g=>g.exclusiveBuildGroup==choice && !GroupComplete(g) && Members(g).Any(p=>IsPlaced(p.PartId)));
        bool BuildChoiceReady(PartDefinition definition,out string reason)
        {
            reason="";var group=Recipe.subAssemblies.FirstOrDefault(g=>g.id==definition.subAssemblyId);
            var active=group==null?null:ActiveExclusiveBuild(group.exclusiveBuildGroup);
            if(active==null || active==group)return true;
            reason="Finish "+FindPart(active.rootPartId).PartDisplayName+" before starting another unit.";return false;
        }
        bool EarlierStepPending(PartDefinition definition)
        {
            var group=Recipe.subAssemblies.FirstOrDefault(g=>g.id==definition.subAssemblyId);
            return Recipe.parts.Any(p=>p.chapterId==CurrentChapterId && p.order<definition.order && !IsPlaced(p.id)
                && !(group!=null && !string.IsNullOrEmpty(group.exclusiveBuildGroup) && p.subAssemblyId!=group.id
                    && Recipe.subAssemblies.Any(g=>g.id==p.subAssemblyId && g.exclusiveBuildGroup==group.exclusiveBuildGroup)));
        }
        public Transform MovableFrame(AssemblyPart part)
        {
            var group=MemberGroup(part)??CarrierDefinition(part.PartId);var carrier=group==null?null:FindPart(group.rootPartId);
            return carrier && !carrier.IsSnapped?carrier.transform:assemblyRoot;
        }
        public Transform SuggestedWorkpieceFrame()
        {
            var next=NextHintPart();
            // A complete unit installs into its destination; flip that destination,
            // rather than the loose unit that is being carried from the bench.
            if(!next || CarrierDefinition(next.PartId)!=null)return next?TargetFrame(next.Definition):assemblyRoot;
            return TargetFrame(next.Definition);
        }
        public Transform FlipFrame()=>GetComponent<WorkpieceLift>() is WorkpieceLift lift && lift.ActiveFrame?lift.ActiveFrame:SuggestedWorkpieceFrame();
        public void FlipWorkpiece(){var lift=GetComponent<WorkpieceLift>();if(lift){lift.FlipActive();return;}var frame=FlipFrame();if(frame && frame.TryGetComponent<PickupablePlatform>(out var platform))platform.Flip(WorkpieceBounds(frame).center);}
        public Bounds WorkpieceBounds(Transform frame)
        {
            if(frame.TryGetComponent<PickupablePlatform>(out var platform) && platform.TryGeometryBounds(out var installed))return installed;
            // An empty fixture has no visible meshes yet. Use its next component's
            // target geometry, rather than the off-centre CAD transform origin.
            var next=NextHintPart();var group=CarrierDefinition(frame.GetComponent<AssemblyPart>()?.PartId);
            if(group!=null && (!next || TargetFrame(next.Definition)!=frame))next=FindPart(group.previewPartId);
            var bounds=new Bounds(frame.position,Vector3.zero);bool first=true;
            if(!next || !next.TargetSocket || TargetFrame(next.Definition)!=frame)return bounds;
            var pose=next.GetTargetPose(next.TargetSocket);
            var target=Matrix4x4.TRS(pose.position,pose.rotation,next.transform.lossyScale)*next.transform.worldToLocalMatrix;
            foreach(var mesh in next.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!mesh.sharedMesh || mesh.GetComponentInParent<GeneratedColliderGroup>() || !OwnsMesh(next,mesh))continue;
                var b=mesh.sharedMesh.bounds;var matrix=target*mesh.transform.localToWorldMatrix;
                for(int i=0;i<8;i++)
                {
                    var point=matrix.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
                }
            }
            return bounds;
        }
        // Size fixtures for the completed unit, including geometry which is still
        // on a supply bench. Parent sockets are evaluated at their assembled poses.
        public Bounds PlannedWorkpieceBounds(Transform frame)
        {
            var group=CarrierDefinition(frame.GetComponent<AssemblyPart>()?.PartId);
            var matrices=new Dictionary<string,Matrix4x4>();
            Matrix4x4 TargetMatrix(AssemblyPart part)
            {
                if(matrices.TryGetValue(part.PartId,out var cached))return cached;
                var d=part.Definition;var member=MemberGroup(part);Matrix4x4 parent;
                if(member!=null)parent=group==member?frame.localToWorldMatrix:TargetMatrix(FindPart(member.rootPartId));
                else if(!string.IsNullOrEmpty(d.parentPartId))parent=TargetMatrix(FindPart(d.parentPartId));
                else parent=frame.localToWorldMatrix;
                var pose=part.GetTargetPose(part.TargetSocket);var socket=part.TargetSocket;
                // Snap poses preserve the source's world scale. Never propagate a
                // parent's imported CAD scale into its children's mesh scale.
                var socketRotation=parent.rotation*d.targetRotation;
                var rotation=socketRotation*Quaternion.Inverse(socket.SnapRotation)*pose.rotation;
                var offset=Quaternion.Inverse(socket.SnapRotation)*(pose.position-socket.SnapPosition);
                var position=parent.MultiplyPoint3x4(d.targetPosition)+socketRotation*offset;
                return matrices[part.PartId]=Matrix4x4.TRS(position,rotation,part.transform.lossyScale);
            }
            bool first=true;var result=new Bounds(frame.position,Vector3.zero);
            foreach(var d in Recipe.parts.Where(d=>group==null || d.subAssemblyId==group.id))
            {
                var part=FindPart(d.id);if(!part || !part.TargetSocket)continue;
                var target=TargetMatrix(part)*part.transform.worldToLocalMatrix;
                foreach(var mesh in part.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(!mesh.sharedMesh || mesh.gameObject.hideFlags!=HideFlags.None || mesh.GetComponentInParent<AssemblyPart>(true)!=part || mesh.GetComponentInParent<GeneratedColliderGroup>(true))continue;
                    var b=mesh.sharedMesh.bounds;var matrix=target*mesh.transform.localToWorldMatrix;
                    for(int i=0;i<8;i++)
                    {
                        var point=matrix.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                        if(first){result=new Bounds(point,Vector3.zero);first=false;}else result.Encapsulate(point);
                    }
                }
            }
            return first?WorkpieceBounds(frame):result;
        }
        public AssemblyPart InteractionPart(AssemblyPart part)
        {
            if(RepairActive)return Repair.InteractionPart(part);
            var group=MemberGroup(part);var root=group==null?null:FindPart(group.rootPartId);
            return root && GroupComplete(group) && (Task==AssemblyTask.Assemble || root.IsSnapped || root.IsSelected)?root:part;
        }
        public bool OwnsMesh(AssemblyPart part,MeshFilter mesh)
        {
            var owner=mesh.GetComponentInParent<AssemblyPart>();
            return owner==part || (CarrierDefinition(part.PartId) is SubAssemblyDefinition group && owner?.Definition?.subAssemblyId==group.id);
        }
        public bool IsCarriedMember(AssemblyPart part)
        {
            var group=MemberGroup(part);var root=group==null?null:FindPart(group.rootPartId);
            return root && (root.IsSelected || root.IsBusy);
        }
        public Transform TargetFrame(PartDefinition definition)
        {
            var group=Recipe?.subAssemblies?.FirstOrDefault(s=>s.id==definition.subAssemblyId);
            return group!=null?FindPart(group.rootPartId).transform:string.IsNullOrEmpty(definition.parentPartId)?assemblyRoot:FindPart(definition.parentPartId).transform;
        }
        void RefreshCarriers()
        {
            foreach(var group in Recipe.subAssemblies)
            {
                var root=FindPart(group.rootPartId);var box=root.GetComponent<BoxCollider>();
                var meshes=Members(group).SelectMany(p=>p.GetComponentsInChildren<MeshFilter>()).Where(m=>m.sharedMesh).ToArray();
                Bounds bounds=new Bounds();bool first=true;
                foreach(var mesh in meshes)
                {
                    var b=mesh.sharedMesh.bounds;var matrix=root.transform.worldToLocalMatrix*mesh.transform.localToWorldMatrix;
                    for(int i=0;i<8;i++) { var corner=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));var point=matrix.MultiplyPoint3x4(corner);if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point); }
                }
                // The loose unit proxy must never fill cavities once the unit is installed.
                bool complete=GroupComplete(group);box.enabled=complete && !root.IsSnapped;
                if(complete && !first){box.center=bounds.center;box.size=bounds.size;}
                root.GetComponent<SubAssembly>()?.RefreshPhysics();
            }
        }
        public bool IsPlaced(string id)
        {
            var p = FindPart(id);
            return p && p.TargetSocket && p.TargetSocket.CurrentPart && p.TargetSocket.CurrentPart.IsSnapped;
        }
        public void LoadRecipe(AssemblyRecipe recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            var errors = recipe.Validate();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            Repair?.End();
            var candidates = assemblyParts.Where(p => p).ToList();
            if (candidates.GroupBy(p => p.PartId).Any(g => string.IsNullOrEmpty(g.Key) || g.Count() > 1)) errors.Add("Scene has missing or duplicate part IDs. Use Batch Setup to assign stable IDs.");
            foreach (var p in recipe.parts) if (!candidates.Any(a => a.PartId == p.id) && !(recipe.subAssemblies?.Any(s=>s.rootPartId==p.id)??false)) errors.Add("Scene part missing: " + p.id);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            // Validation finishes before changing any scene state.
            IsPreparing = true;
            foreach (var p in candidates) { p.CancelSnap(); p.Unsnap(true); }
            if(!scaled)
            {
                // Detach first so both mounted and staged meshes scale once. Socket
                // coordinates stay in the original recipe's reference frame.
                foreach(var p in candidates){p.transform.SetParent(null,true);p.transform.localScale*=engineScale;}
                assemblyRoot.localScale*=engineScale;scaled=true;
            }
            Recipe = AssemblyRecipeStore.Parse(JsonUtility.ToJson(recipe)); // Own an editable copy.
            if(Recipe.subAssemblies==null)Recipe.subAssemblies=new List<SubAssemblyDefinition>();
            foreach(var group in Recipe.subAssemblies)
            {
                if(candidates.Any(p=>p.PartId==group.rootPartId))continue;
                var go=new GameObject(group.rootPartId);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,gameObject.scene);
                go.transform.localScale=assemblyRoot.lossyScale;
                var carrier=go.AddComponent<AssemblyPart>();carrier.SetIdentity(group.rootPartId,group.rootPartId);go.AddComponent<BoxCollider>().enabled=false;
                go.AddComponent<SubAssembly>();
                go.AddComponent<PickupablePlatform>();
                candidates.Add(carrier);assemblyParts.Add(carrier);
            }
            byId.Clear();
            foreach (var p in candidates) byId.Add(p.PartId,p);
            foreach (var d in Recipe.parts)
            {
                var p = byId[d.id]; p.Configure(d,this);
                if (!p.TargetSocket)
                {
                    var go = new GameObject(d.id + "_Socket"); p.TargetSocket = go.AddComponent<AssemblySocket>();
                }
                p.TargetSocket.Configure(p);
            }
            foreach (var d in Recipe.parts)
            {
                Transform parent = TargetFrame(d);
                var socket = byId[d.id].TargetSocket;
                socket.transform.SetParent(parent,false);
                socket.transform.localPosition = d.targetPosition; socket.transform.localRotation = d.targetRotation;
            }
            foreach (var p in candidates.Where(p => !Recipe.parts.Any(d => d.id == p.PartId))) { p.ClearRecipeConfiguration();p.gameObject.SetActive(false); }
            Repair?.EnsureChapter();
            GetComponent<PCDisplayChapter>()?.EnsureChapter();
            IsPreparing = false;
            SelectChapter(Mathf.Clamp(CurrentChapterIndex,0,Recipe.chapters.Count-1));
        }
        public AssemblyRecipe CaptureScene()
        {
            if(Recipe!=null)return AssemblyRecipeStore.Parse(JsonUtility.ToJson(Recipe));
            if(initialRecipe)return AssemblyRecipeStore.Parse(initialRecipe.text);
            EnsureFrames();
            var recipe = new AssemblyRecipe();
            foreach (var p in assemblyParts.Where(p => p))
            {
                int priority = int.TryParse(p.name.Split('_')[0], out int number) ? number : Mathf.Max(1,p.OrderIndex);
                string chapterId = "chapter-" + Mathf.Max(1,priority / 100);
                if (!recipe.chapters.Any(c => c.id == chapterId)) recipe.chapters.Add(new ChapterDefinition { id=chapterId,title="Chapter " + Mathf.Max(1,priority / 100) });
                Transform target = p.TargetSnapPoint ? p.TargetSnapPoint : p.transform;
                recipe.parts.Add(new PartDefinition { id=p.PartId,displayName=p.PartDisplayName,chapterId=chapterId,order=priority,
                    geometryGroup=p.GeometryGroupId,targetPosition=assemblyRoot.InverseTransformPoint(target.position),targetRotation=Quaternion.Inverse(assemblyRoot.rotation)*target.rotation,
                    trayPosition=TrayFrame.InverseTransformPoint(p.transform.position),trayRotation=Quaternion.Inverse(TrayFrame.rotation)*p.transform.rotation });
            }
            recipe.chapters = recipe.chapters.OrderBy(c => int.Parse(c.id.Substring(8))).ToList();
            return recipe;
        }
        public void SetMode(WorkshopMode mode)
        {
            Mode = mode;
            if(Recipe!=null && mode==WorkshopMode.Assembly && !IsPlayableChapter(CurrentChapterId))
                CurrentChapterIndex=Mathf.Max(0,Recipe.chapters.FindIndex(c=>IsPlayableChapter(c.id)));
            if (Recipe != null) SelectChapter(CurrentChapterIndex);
        }
        public bool SelectChapter(int index)
        {
            if (Recipe == null || index < 0 || index >= Recipe.chapters.Count) return false;
            GetComponent<PCDisplayChapter>()?.StopAnimation();
            ChapterVisit++;
            GetComponent<WorkpieceLift>()?.CancelTransitions();
            Repair?.End();
            IsPreparing = true; CurrentChapterIndex = index; completionSent = false;
            foreach (var p in assemblyParts.Where(p => p && p.Definition != null)) { p.CancelSnap(); p.Unsnap(true); }
            foreach (var d in Recipe.parts)
            {
                var p = byId[d.id]; int chapter = Recipe.chapters.FindIndex(c => c.id == d.chapterId);
                p.gameObject.SetActive(true);
                p.ResetLoose(TrayFrame,d.trayPosition,d.trayRotation);
                p.TargetSocket.gameObject.SetActive(chapter <= index);
            }
            foreach(var group in Recipe.subAssemblies)
            {
                var root=byId[group.rootPartId];root.transform.SetPositionAndRotation(assemblyRoot.TransformPoint(BenchPosition(group)),group.rootPartId.StartsWith("230_Piston_Rod_Unit_")?InitialFixtureRotation(group):assemblyRoot.rotation);
            }
            // Topological installation makes socket parenting independent of file order.
            var pending = Recipe.parts.Where(d => Recipe.chapters.FindIndex(c => c.id == d.chapterId) < index
                || (d.chapterId == CurrentChapterId && (Mode == WorkshopMode.Edit || Task==AssemblyTask.Disassemble || d.isBase))).ToList();
            while (pending.Count > 0)
            {
                var d = pending.FirstOrDefault(p => AssemblyRecipe.Dependencies(p).All(IsPlaced));
                if (d == null) break;
                byId[d.id].InstallImmediate(byId[d.id].TargetSocket); pending.Remove(d);
            }
            foreach (var d in Recipe.parts)
                if (Recipe.chapters.FindIndex(c => c.id == d.chapterId) > index) byId[d.id].gameObject.SetActive(false);
            IsPreparing = false;
            RefreshCarriers();
            GetComponent<WorkshopSupplyLayout>()?.StageChapter(this);
            if(CurrentChapterId==PCRepairChapter.ChapterId && Mode==WorkshopMode.Assembly){Task=AssemblyTask.Assemble;Repair?.Begin();}
            Status = Recipe.chapters[index].title; Changed?.Invoke(); return true;
        }
        public bool ContinueChapter()
        {
            if (Mode != WorkshopMode.Assembly || !ChapterComplete) { Report("Complete this chapter first."); return false; }
            int next=Recipe.chapters.FindIndex(CurrentChapterIndex+1,c=>IsPlayableChapter(c.id));
            if (next<0) { Report("Engine assembly complete."); return false; }
            return SelectChapter(next);
        }
        public bool CanPickUp(AssemblyPart part, out string reason)
        {
            if(part && part.GetComponentInParent<PickupablePlatform>() is PickupablePlatform turning && turning.IsFlipping){reason="Wait for the lift to finish turning.";return false;}
            if(RepairActive)return Repair.CanPickUp(part,out reason);
            reason = "";
            if (!part || IsPreparing || Recipe == null || part.Manager != this || part.ChapterId != CurrentChapterId) { reason = "Select this part's chapter first."; return false; }
            if (part.Definition.isBase) { reason = "Move or flip the workpiece instead."; return false; }
            var carrier=CarrierDefinition(part.PartId);
            if(Mode!=WorkshopMode.Edit && carrier!=null && !GroupComplete(carrier)) { reason="Complete this subassembly before picking it up.";return false; }
            var group=MemberGroup(part);
            if(group!=null && IsPlaced(group.rootPartId)) { reason="Remove the completed subassembly as one unit first.";return false; }
            if(Mode==WorkshopMode.Assembly && Task==AssemblyTask.Assemble && Interaction==WorkshopInteraction.Build && !part.IsSnapped)
            {
                // Staging does not require the destination to be upright yet.
                // Installation still checks its orientation in CanInstall.
                if(!BuildChoiceReady(part.Definition,out reason))return false;
                foreach(var id in AssemblyRecipe.Dependencies(part.Definition))if(!IsPlaced(id)){reason="First install "+(FindPart(id)?.PartDisplayName??id);return false;}
                if(EarlierStepPending(part.Definition)){reason="Complete the earlier assembly step.";return false;}
                return true;
            }
            if(Mode==WorkshopMode.Assembly && Task==AssemblyTask.Disassemble && Interaction==WorkshopInteraction.Build && !part.IsSnapped) { reason="This part is already removed.";return false; }
            return !part.IsSnapped || CanRemove(part,out reason);
        }
        public bool CanCollect(AssemblyPart part,AssemblyPart stack,out string reason)
        {
            if(RepairActive){reason="Complete the current repair step before picking another part.";return false;}
            reason="Carry one matching part type at a time.";
            if(!part || !stack || !AssemblyInventory.Matches(part,stack))return false;
            if(part.Manager!=this || part.ChapterId!=CurrentChapterId || part.Definition.isBase || part.IsSnapped || part.IsSelected || part.IsBusy || Task==AssemblyTask.Disassemble)return false;
            var group=MemberGroup(part);if(group!=null && IsPlaced(group.rootPartId))return false;
            if(Mode==WorkshopMode.Assembly && !BuildChoiceReady(part.Definition,out reason))return false;
            reason="";return true; // Future sockets stay locked by CanInstall.
        }
        public bool CanInstall(AssemblyPart part, AssemblySocket socket, out string reason)
        {
            var destination=part && part.Definition!=null?TargetFrame(socket && socket.TargetPart?socket.TargetPart.Definition:part.Definition):null;
            if(destination && destination.GetComponentInParent<PickupablePlatform>() is PickupablePlatform turning && turning.IsFlipping){reason="Wait for the lift to finish turning.";return false;}
            if(RepairActive)return Repair.CanInstall(part,socket,out reason);
            reason = "";
            if (!part || !socket || Recipe == null || part.Manager != this || part.ChapterId != CurrentChapterId) { reason = "This part belongs to another chapter."; return false; }
            var d = socket.TargetPart ? socket.TargetPart.Definition : part.Definition;
            if (d == null || d.chapterId != CurrentChapterId) { reason = "This socket belongs to another chapter."; return false; }
            if (part.Definition.order != d.order) { reason = "Matching geometry belongs to another step."; return false; }
            if (Mode == WorkshopMode.Edit) return true;
            if(Task==AssemblyTask.Disassemble) { reason="Remove this section in reverse order. Switch to Assemble to install parts.";return false; }
            var memberGroup=MemberGroup(part);
            if(memberGroup!=null && IsPlaced(memberGroup.rootPartId)) { reason="This subassembly is already installed.";return false; }
            if(!BuildChoiceReady(d,out reason))return false;
            foreach (string dependency in AssemblyRecipe.Dependencies(d))
                if (!IsPlaced(dependency)) { reason = "First install " + (FindPart(dependency)?.PartDisplayName ?? dependency); return false; }
            if (EarlierStepPending(d)) { reason = "Complete the earlier assembly step."; return false; }
            if(RequiresWorkpieceOrientation && !OrientationReady(d)) { reason=d.orientation==AssemblyOrientation.UpsideDown?"Turn this workpiece upside down with F before fitting this part.":"Turn this workpiece upright with F before fitting this part.";return false; }
            return true;
        }
        public bool OrientationReady(PartDefinition definition)
        {
            if(definition==null || definition.orientation==AssemblyOrientation.Any || !RequiresWorkpieceOrientation)return true;
            float up=Vector3.Dot(TargetFrame(definition).up,Vector3.up);
            return definition.orientation==AssemblyOrientation.Upright?up>.5f:up<-.5f;
        }
        public bool CanRemove(AssemblyPart part, out string reason)
        {
            if(RepairActive)return Repair.CanRemove(part,out reason);
            reason = "";
            if (part.ChapterId != CurrentChapterId || part.Definition.isBase) { reason = "This part is fixed for this chapter."; return false; }
            string id = part.InstalledSocket && part.InstalledSocket.TargetPart ? part.InstalledSocket.TargetPart.PartId : part.PartId;
            var memberGroup=MemberGroup(part);
            if(memberGroup!=null && (IsPlaced(memberGroup.rootPartId) || FindPart(memberGroup.rootPartId).IsSelected)) { reason="Remove and set down the complete subassembly first.";return false; }
            if (Recipe.parts.Any(d => d.id != id && IsPlaced(d.id) && AssemblyRecipe.Dependencies(d).Contains(id)))
            { reason = "Remove the dependent parts first."; return false; }
            var slot = Recipe.parts.First(d => d.id == id);
            if (Mode == WorkshopMode.Assembly && Recipe.parts.Any(d => d.chapterId == CurrentChapterId && d.order > slot.order && IsPlaced(d.id)))
            { reason = "Remove the later assembly steps first."; return false; }
            return true;
        }
        public void NotifyPartSnapped(AssemblyPart part)
        {
            if (IsPreparing) return;
            RefreshCarriers();
            if(RepairActive)Repair.OnInstalled(part);
            if (audioSource && snapSound) audioSource.PlayOneShot(snapSound);
            onPartSnapped.Invoke(part); Changed?.Invoke();
            if (Mode == WorkshopMode.Assembly && ChapterComplete && !completionSent)
            {
                completionSent = true; onChapterCompleted.Invoke();
                Report("Chapter complete. Continue when ready.");
                if (!Recipe.chapters.Skip(CurrentChapterIndex+1).Any(c=>Recipe.parts.Any(p=>p.chapterId==c.id))) onAssemblyCompleted.Invoke();
            }
        }
        public void NotifyPartUnsnapped(AssemblyPart part)
        {
            if(IsPreparing)return;
            RefreshCarriers();
            if(Task!=AssemblyTask.Disassemble || !ChapterComplete)completionSent=false;
            onPartUnsnapped.Invoke(part);Changed?.Invoke();
            if(Mode==WorkshopMode.Assembly && Task==AssemblyTask.Disassemble && ChapterComplete && !completionSent)
            {
                completionSent=true;onChapterCompleted.Invoke();Report("Section disassembled. Continue when ready.");
                if(!Recipe.chapters.Skip(CurrentChapterIndex+1).Any(c=>Recipe.parts.Any(p=>p.chapterId==c.id)))onAssemblyCompleted.Invoke();
            }
        }
        public AssemblyPart NextHintPart()
        {
            if(RepairActive)return Repair.NextPart;
            if(Recipe==null)return null;
            AssemblyPart fallback=null,available=null;
            bool removing=Task==AssemblyTask.Disassemble && Mode==WorkshopMode.Assembly;
            foreach(var part in assemblyParts)
            {
                if(!part || !part.gameObject.activeInHierarchy || part.ChapterId!=CurrentChapterId || part.Definition.isBase || part.IsBusy)continue;
                if(removing)
                {
                    if(part.IsSnapped && (!available || part.Definition.order>available.Definition.order) && CanRemove(part,out _))available=part;
                    continue;
                }
                if(part.IsSnapped)continue;
                if(!fallback || CompareRecommendation(part,fallback)<0)fallback=part;
                if((!available || CompareRecommendation(part,available)<0) && part.TargetSocket && CanInstall(part,part.TargetSocket,out _))available=part;
            }
            return available??fallback;
        }
        int CompareRecommendation(AssemblyPart a,AssemblyPart b)
        {
            int order=a.Definition.order.CompareTo(b.Definition.order);if(order!=0)return order;
            int family=RecommendationFamily(a).CompareTo(RecommendationFamily(b));if(family!=0)return family;
            return Recipe.parts.IndexOf(a.Definition).CompareTo(Recipe.parts.IndexOf(b.Definition));
        }
        static int RecommendationFamily(AssemblyPart part)
        {
            if(part.ChapterId!="chapter-4" || !part.PartId.Contains("_Valves_"))return 0;
            int family=0;foreach(char c in part.PartId){if(c=='_')return family;if(c<'0' || c>'9')return 0;family=family*10+c-'0';}return 0;
        }
        public IOrderedEnumerable<AssemblyPart> RecommendedParts(IEnumerable<AssemblyPart> parts)=>parts.OrderBy(p=>p.Definition.order)
            .ThenBy(p=>p.ChapterId=="chapter-4" && p.PartId.Contains("_Valves_") && int.TryParse(p.PartId.Split('_')[0],out var family)?family:0)
            .ThenBy(p=>Recipe.parts.IndexOf(p.Definition));
        public void Report(string message) { Status = message; Changed?.Invoke(); }
        public void Save(string filename)
        {
            if (Mode != WorkshopMode.Edit) throw new InvalidOperationException("Switch to Edit mode to save a recipe.");
            var saved=AssemblyRecipeStore.Parse(JsonUtility.ToJson(Recipe));saved.chapters.RemoveAll(c=>c.id==PCRepairChapter.ChapterId);
            string path = AssemblyRecipeStore.Save(saved,filename); Report("Saved " + System.IO.Path.GetFileName(path));
        }
        public void CaptureTray(AssemblyPart part)
        {
            if (Mode != WorkshopMode.Edit || part.IsSnapped) throw new InvalidOperationException("Pick up and place a loose part before capturing its tray pose.");
            part.Definition.trayPosition=TrayFrame.InverseTransformPoint(part.transform.position);
            part.Definition.trayRotation=Quaternion.Inverse(TrayFrame.rotation)*part.transform.rotation;
            GetComponent<AssemblyWorkshopUI>()?.MarkDirty();
        }
        public void CaptureTarget(AssemblyPart part)
        {
            if (Mode != WorkshopMode.Edit) return;
            var d=part.Definition; Transform parent=TargetFrame(d);
            d.targetPosition=parent.InverseTransformPoint(part.AnchorWorldPosition); d.targetRotation=Quaternion.Inverse(parent.rotation)*part.AnchorWorldRotation;
            part.TargetSocket.transform.SetLocalPositionAndRotation(d.targetPosition,d.targetRotation);
            GetComponent<AssemblyWorkshopUI>()?.MarkDirty();
        }
    }
}


