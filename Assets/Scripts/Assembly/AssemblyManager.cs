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
            Recipe.chapters.Add(new ChapterDefinition { id=Guid.NewGuid().ToString("N"), title=string.IsNullOrWhiteSpace(title)?"New chapter":title.Trim() });
            SelectChapter(Recipe.chapters.Count-1);
        }
        public int CurrentChapterIndex { get; private set; }
        public string CurrentChapterId => Recipe == null ? "" : Recipe.chapters[CurrentChapterIndex].id;
        public Transform AssemblyRoot => assemblyRoot;
        public Transform TrayFrame => trayFrame ? trayFrame : transform;
        public IReadOnlyList<AssemblyPart> AssemblyParts => assemblyParts;
        public bool IsPreparing { get; private set; }
        public string Status { get; private set; } = "";
        readonly Dictionary<string, AssemblyPart> byId = new Dictionary<string, AssemblyPart>();
        bool completionSent;
        public bool ChapterComplete => Recipe != null && Recipe.parts.Any(p => p.chapterId == CurrentChapterId) && Recipe.parts.Where(p => p.chapterId == CurrentChapterId && !p.isBase).All(p => Task==AssemblyTask.Assemble?IsPlaced(p.id):!IsPlaced(p.id));
        public int ChapterPartCount => Recipe?.parts.Count(p => p.chapterId == CurrentChapterId && !p.isBase) ?? 0;
        public int ChapterInstalledCount => Recipe?.parts.Count(p => p.chapterId == CurrentChapterId && !p.isBase && IsPlaced(p.id)) ?? 0;
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
        }
        void OnDestroy() { if (Instance == this) Instance = null; }
        void EnsureFrames()
        {
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
        public IEnumerable<AssemblyPart> Members(SubAssemblyDefinition group)=>group==null?Enumerable.Empty<AssemblyPart>():assemblyParts.Where(p=>p && p.Definition?.subAssemblyId==group.id);
        public bool GroupComplete(SubAssemblyDefinition group)=>group!=null && Members(group).All(p=>IsPlaced(p.PartId));
        public Transform MovableFrame(AssemblyPart part)
        {
            var group=MemberGroup(part)??CarrierDefinition(part.PartId);var carrier=group==null?null:FindPart(group.rootPartId);
            return carrier && !carrier.IsSnapped?carrier.transform:assemblyRoot;
        }
        public AssemblyPart InteractionPart(AssemblyPart part)
        {
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
                bool complete=GroupComplete(group);box.enabled=complete && (Task==AssemblyTask.Assemble || root.IsSnapped || root.IsSelected);
                if(complete && !first){box.center=bounds.center;box.size=bounds.size;}
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
            var candidates = assemblyParts.Where(p => p).ToList();
            if (candidates.GroupBy(p => p.PartId).Any(g => string.IsNullOrEmpty(g.Key) || g.Count() > 1)) errors.Add("Scene has missing or duplicate part IDs. Use Batch Setup to assign stable IDs.");
            foreach (var p in recipe.parts) if (!candidates.Any(a => a.PartId == p.id) && !(recipe.subAssemblies?.Any(s=>s.rootPartId==p.id)??false)) errors.Add("Scene part missing: " + p.id);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            // Validation finishes before changing any scene state.
            IsPreparing = true;
            foreach (var p in candidates) { p.CancelSnap(); p.Unsnap(true); }
            Recipe = AssemblyRecipeStore.Parse(JsonUtility.ToJson(recipe)); // Own an editable copy.
            if(Recipe.subAssemblies==null)Recipe.subAssemblies=new List<SubAssemblyDefinition>();
            foreach(var group in Recipe.subAssemblies)
            {
                if(candidates.Any(p=>p.PartId==group.rootPartId))continue;
                var go=new GameObject(group.rootPartId);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,gameObject.scene);
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
            foreach (var p in candidates.Where(p => !Recipe.parts.Any(d => d.id == p.PartId))) p.gameObject.SetActive(false);
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
            if(Recipe!=null && mode==WorkshopMode.Assembly && !Recipe.parts.Any(p=>p.chapterId==CurrentChapterId))
                CurrentChapterIndex=Mathf.Max(0,Recipe.chapters.FindIndex(c=>Recipe.parts.Any(p=>p.chapterId==c.id)));
            if (Recipe != null) SelectChapter(CurrentChapterIndex);
        }
        public bool SelectChapter(int index)
        {
            if (Recipe == null || index < 0 || index >= Recipe.chapters.Count) return false;
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
                var root=byId[group.rootPartId];root.transform.SetPositionAndRotation(assemblyRoot.TransformPoint(group.benchPosition),assemblyRoot.rotation);
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
            Status = Recipe.chapters[index].title; Changed?.Invoke(); return true;
        }
        public bool ContinueChapter()
        {
            if (Mode != WorkshopMode.Assembly || !ChapterComplete) { Report("Complete this chapter first."); return false; }
            int next=Recipe.chapters.FindIndex(CurrentChapterIndex+1,c=>Recipe.parts.Any(p=>p.chapterId==c.id));
            if (next<0) { Report("Engine assembly complete."); return false; }
            return SelectChapter(next);
        }
        public bool CanPickUp(AssemblyPart part, out string reason)
        {
            reason = "";
            if (!part || IsPreparing || Recipe == null || part.Manager != this || part.ChapterId != CurrentChapterId) { reason = "Select this part's chapter first."; return false; }
            if (part.Definition.isBase) { reason = "Move or flip the workpiece instead."; return false; }
            var carrier=CarrierDefinition(part.PartId);
            if(Mode!=WorkshopMode.Edit && carrier!=null && !GroupComplete(carrier)) { reason="Complete this subassembly before picking it up.";return false; }
            var group=MemberGroup(part);
            if(group!=null && IsPlaced(group.rootPartId)) { reason="Remove the completed subassembly as one unit first.";return false; }
            if(Mode==WorkshopMode.Assembly && Task==AssemblyTask.Assemble && Interaction==WorkshopInteraction.Build && !part.IsSnapped)
                return CanInstall(part,part.TargetSocket,out reason);
            if(Mode==WorkshopMode.Assembly && Task==AssemblyTask.Disassemble && Interaction==WorkshopInteraction.Build && !part.IsSnapped) { reason="This part is already removed.";return false; }
            return !part.IsSnapped || CanRemove(part,out reason);
        }
        public bool CanInstall(AssemblyPart part, AssemblySocket socket, out string reason)
        {
            reason = "";
            if (!part || !socket || Recipe == null || part.Manager != this || part.ChapterId != CurrentChapterId) { reason = "This part belongs to another chapter."; return false; }
            var d = socket.TargetPart ? socket.TargetPart.Definition : part.Definition;
            if (d == null || d.chapterId != CurrentChapterId) { reason = "This socket belongs to another chapter."; return false; }
            if (part.Definition.order != d.order) { reason = "Matching geometry belongs to another step."; return false; }
            if (Mode == WorkshopMode.Edit) return true;
            if(Task==AssemblyTask.Disassemble) { reason="Remove this section in reverse order. Switch to Assemble to install parts.";return false; }
            var memberGroup=MemberGroup(part);
            if(memberGroup!=null && IsPlaced(memberGroup.rootPartId)) { reason="This subassembly is already installed.";return false; }
            foreach (string dependency in AssemblyRecipe.Dependencies(d))
                if (!IsPlaced(dependency)) { reason = "First install " + (FindPart(dependency)?.PartDisplayName ?? dependency); return false; }
            if (Recipe.parts.Any(p => p.chapterId == CurrentChapterId && p.order < d.order && !IsPlaced(p.id))) { reason = "Complete the earlier assembly step."; return false; }
            float up = Vector3.Dot(assemblyRoot.up,Vector3.up);
            if ((d.orientation == AssemblyOrientation.Upright && up < .85f) || (d.orientation == AssemblyOrientation.UpsideDown && up > -.85f))
            { reason = d.orientation == AssemblyOrientation.Upright ? "Turn the base upright." : "Turn the base upside down."; return false; }
            return true;
        }
        public bool CanRemove(AssemblyPart part, out string reason)
        {
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
            if(Recipe==null)return null;
            var candidates=assemblyParts.Where(p=>p && p.gameObject.activeInHierarchy && p.ChapterId==CurrentChapterId && !p.Definition.isBase && !p.IsBusy);
            if(Task==AssemblyTask.Disassemble && Mode==WorkshopMode.Assembly)return candidates.Where(p=>p.IsSnapped && CanRemove(p,out _)).OrderByDescending(p=>p.Definition.order).FirstOrDefault();
            var next=candidates.Where(p=>!p.IsSnapped).OrderBy(p=>p.Definition.order).ToList();
            return next.FirstOrDefault(p=>p.TargetSocket && CanInstall(p,p.TargetSocket,out _))??next.FirstOrDefault();
        }
        public void Report(string message) { Status = message; Changed?.Invoke(); }
        public void Save(string filename)
        {
            if (Mode != WorkshopMode.Edit) throw new InvalidOperationException("Switch to Edit mode to save a recipe.");
            string path = AssemblyRecipeStore.Save(Recipe,filename); Report("Saved " + System.IO.Path.GetFileName(path));
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


