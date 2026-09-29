using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EngineAssembly.Editor
{
    public class AssemblyChapterEditor : EditorWindow
    {
        [SerializeField] AssemblyRecipe recipe;
        [SerializeField] int chapterIndex;
        [SerializeField] string selectedId,filename="engine-assembly";
        Vector2 scroll,details;
        string status="";
        string[] files=Array.Empty<string>();
        public static void Open()=>GetWindow<AssemblyChapterEditor>("Assembly chapters");
        void OnEnable()=>RefreshFiles();
        void RefreshFiles()=>files=AssemblyRecipeStore.Files();
        void Run(Action action)
        {
            try { action();status=""; }catch(Exception ex) { status=ex.Message;Debug.LogException(ex); }
            Repaint();
        }
        void OnGUI()
        {
            EditorGUILayout.LabelField("Chapter recipe editor",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Stable IDs link JSON entries to scene parts. Lower priorities assemble first; equal priorities share a step. Socket positions are relative to their parent part or the movable workpiece.",MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Capture scene"))Run(Capture);
            if(GUILayout.Button("Import JSON")) { string path=EditorUtility.OpenFilePanel("Import assembly recipe",AssemblyRecipeStore.DirectoryPath,"json");if(!string.IsNullOrEmpty(path))Run(()=>Load(path)); }
            if(GUILayout.Button("Refresh library"))RefreshFiles();
            EditorGUILayout.EndHorizontal();
            if(files.Length>0)
            {
                int index=EditorGUILayout.Popup("Saved recipes",-1,files.Select(Path.GetFileNameWithoutExtension).ToArray());
                if(index>=0)Run(()=>Load(files[index]));
            }
            if(!string.IsNullOrEmpty(status))EditorGUILayout.HelpBox(status,MessageType.Warning);
            if(recipe==null)return;
            Undo.RecordObject(this,"Edit assembly recipe");EditorGUI.BeginChangeCheck();
            recipe.title=EditorGUILayout.TextField("Title",recipe.title);
            chapterIndex=Mathf.Clamp(chapterIndex,0,recipe.chapters.Count-1);
            chapterIndex=EditorGUILayout.Popup("Chapter",chapterIndex,recipe.chapters.Select(c=>c.title).ToArray());
            var chapter=recipe.chapters[chapterIndex];
            chapter.title=EditorGUILayout.TextField("Chapter title",chapter.title);
            chapter.description=EditorGUILayout.TextField("Instructions",chapter.description);
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Move chapter earlier") && chapterIndex>0) { recipe.chapters.RemoveAt(chapterIndex);recipe.chapters.Insert(--chapterIndex,chapter); }
            if(GUILayout.Button("Move chapter later") && chapterIndex<recipe.chapters.Count-1) { recipe.chapters.RemoveAt(chapterIndex);recipe.chapters.Insert(++chapterIndex,chapter); }
            if(GUILayout.Button("Add chapter")) { recipe.chapters.Add(new ChapterDefinition{id=Guid.NewGuid().ToString("N"),title="New chapter"});chapterIndex=recipe.chapters.Count-1; }
            if(GUILayout.Button("Frame chapter"))FrameChapter(chapter.id);
            EditorGUILayout.EndHorizontal();
            scroll=EditorGUILayout.BeginScrollView(scroll,GUILayout.Height(210));
            foreach(var p in recipe.parts.Where(p=>p.chapterId==chapter.id).OrderBy(p=>p.order).ThenBy(p=>p.displayName).ToArray())
            {
                EditorGUILayout.BeginHorizontal();
                p.order=Mathf.Max(0,EditorGUILayout.IntField(p.order,GUILayout.Width(55)));
                if(GUILayout.Toggle(selectedId==p.id,p.displayName,GUI.skin.button))selectedId=p.id;
                if(GUILayout.Button("Select",GUILayout.Width(55))) { var scenePart=ScenePart(p.id);if(scenePart) { Selection.activeGameObject=scenePart.gameObject;SceneView.lastActiveSceneView?.FrameSelected(); } }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            var part=recipe.parts.FirstOrDefault(p=>p.id==selectedId);
            details=EditorGUILayout.BeginScrollView(details);
            if(part!=null)
            {
                EditorGUILayout.LabelField("Selected part",EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(part.id,GUILayout.Height(18));
                part.displayName=EditorGUILayout.TextField("Display name",part.displayName);
                int ch=recipe.chapters.FindIndex(c=>c.id==part.chapterId);
                part.chapterId=recipe.chapters[EditorGUILayout.Popup("Belongs to chapter",ch,recipe.chapters.Select(c=>c.title).ToArray())].id;
                part.order=Mathf.Max(0,EditorGUILayout.IntField("Assembly priority",part.order));
                part.isBase=EditorGUILayout.Toggle("Preset base",part.isBase);
                part.geometryGroup=EditorGUILayout.TextField("Interchangeable geometry ID",part.geometryGroup);
                part.parentPartId=EditorGUILayout.TextField("Socket parent part ID",part.parentPartId);
                string dependencies=EditorGUILayout.TextField("Prerequisite IDs (comma)",string.Join(",",part.prerequisites));
                part.prerequisites=dependencies.Split(',').Select(s=>s.Trim()).Where(s=>s.Length>0).Distinct().ToList();
                part.orientation=(AssemblyOrientation)EditorGUILayout.EnumPopup("Required base orientation",part.orientation);
                part.rotationMode=(SnapRotationMode)EditorGUILayout.EnumPopup("Rotation matching",part.rotationMode);
                part.symmetry=EditorGUILayout.IntSlider("Rotational symmetry",part.symmetry,1,12);
                part.snapDistance=EditorGUILayout.Slider("Snap distance (metres)",part.snapDistance,.01f,1);
                part.snapAngle=EditorGUILayout.Slider("Rotation tolerance",part.snapAngle,0,180);
                part.targetPosition=EditorGUILayout.Vector3Field("Target local position",part.targetPosition);
                part.targetRotation=Quaternion.Euler(EditorGUILayout.Vector3Field("Target local rotation",part.targetRotation.eulerAngles));
                part.trayPosition=EditorGUILayout.Vector3Field("Tray local position",part.trayPosition);
                part.trayRotation=Quaternion.Euler(EditorGUILayout.Vector3Field("Tray local rotation",part.trayRotation.eulerAngles));
                if(GUILayout.Button("Copy current scene part pose to tray"))Run(()=>CapturePose(part,false));
                if(GUILayout.Button("Copy current scene part pose to target"))Run(()=>CapturePose(part,true));
            }
            EditorGUILayout.EndScrollView();
            if(EditorGUI.EndChangeCheck())EditorUtility.SetDirty(this);
            filename=EditorGUILayout.TextField("JSON filename",filename);
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Validate")) { var errors=recipe.Validate();status=errors.Count==0?"Recipe is valid.":string.Join("\n",errors); }
            if(GUILayout.Button("Save JSON",GUILayout.Height(30)))Run(()=> { AssemblyRecipeStore.Save(recipe,filename);AssetDatabase.Refresh();RefreshFiles(); });
            if(GUILayout.Button("Preview in Play mode"))Run(()=> {
                if(!Application.isPlaying)throw new InvalidOperationException("Enter Play mode first. JSON editing and saving work outside Play mode.");
                var manager=FindAnyObjectByType<AssemblyManager>();if(!manager)throw new InvalidOperationException("No AssemblyManager in scene.");
                manager.LoadRecipe(recipe);manager.SetMode(WorkshopMode.Edit);manager.SelectChapter(chapterIndex);
            });
            EditorGUILayout.EndHorizontal();
        }
        void Load(string path) { recipe=AssemblyRecipeStore.Load(path);filename=Path.GetFileNameWithoutExtension(path);chapterIndex=0;selectedId=""; }
        void Capture()
        {
            var manager=FindAnyObjectByType<AssemblyManager>();
            if(!manager)throw new InvalidOperationException("Create a workshop scene or add AssemblyManager, then batch-setup its parts.");
            manager.RefreshPartsList();recipe=manager.CaptureScene();chapterIndex=0;selectedId="";
        }
        static AssemblyPart ScenePart(string id)=>FindObjectsByType<AssemblyPart>(FindObjectsInactive.Include).FirstOrDefault(p=>p.PartId==id);
        void FrameChapter(string id)
        {
            Selection.objects=recipe.parts.Where(p=>p.chapterId==id).Select(p=>ScenePart(p.id)).Where(p=>p).Select(p=>(UnityEngine.Object)p.gameObject).ToArray();
            SceneView.lastActiveSceneView?.FrameSelected();
        }
        static void CapturePose(PartDefinition part,bool target)
        {
            var manager=FindAnyObjectByType<AssemblyManager>();var scene=ScenePart(part.id);
            if(!manager || !scene)throw new InvalidOperationException("Manager or part is missing from this scene.");
            Transform frame=target?(string.IsNullOrEmpty(part.parentPartId)?manager.AssemblyRoot:ScenePart(part.parentPartId)?.transform):manager.TrayFrame;
            if(!frame)throw new InvalidOperationException("Missing pose reference frame.");
            if(target) { part.targetPosition=frame.InverseTransformPoint(scene.transform.position);part.targetRotation=Quaternion.Inverse(frame.rotation)*scene.transform.rotation; }
            else { part.trayPosition=frame.InverseTransformPoint(scene.transform.position);part.trayRotation=Quaternion.Inverse(frame.rotation)*scene.transform.rotation; }
        }
    }
}


