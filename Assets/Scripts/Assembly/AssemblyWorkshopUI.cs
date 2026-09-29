using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace EngineAssembly
{
    [RequireComponent(typeof(AssemblyManager))]
    public class AssemblyWorkshopUI : MonoBehaviour
    {
        public bool IsOpen { get; private set; }=true;
        AssemblyManager manager;
        Vector2 chaptersScroll,partsScroll,filesScroll;
        string filename="engine-assembly";
        string selectedId="",message="";
        string[] files=Array.Empty<string>();
        bool showFiles;
        AssemblyRecipeCatalog catalog;
        void Awake() { manager=GetComponent<AssemblyManager>();RefreshFiles(); }
        public void Toggle()=>SetOpen(!IsOpen);
        public void SetOpen(bool open)
        {
            IsOpen=open;
            if(open)foreach(var player in FindObjectsByType<PlayerAssemblyController>())player.DropHeldPart();
        }
        void RefreshFiles() { files=AssemblyRecipeStore.Files();catalog=Resources.Load<AssemblyRecipeCatalog>("AssemblyRecipeCatalog"); }
        void Run(Action action) { try { action();message=""; }catch(Exception ex) { message=ex.Message; } }
        void OnGUI()
        {
            if(!IsOpen || !manager)return;
            float width=Mathf.Min(850,Screen.width-24),height=Mathf.Min(760,Screen.height-24);
            GUILayout.BeginArea(new Rect(12,12,width,height),GUI.skin.box);
            GUILayout.Label(manager.Recipe?.title??"Assembly workshop",new GUIStyle(GUI.skin.label){fontSize=22,fontStyle=FontStyle.Bold});
            GUILayout.BeginHorizontal();
            if(GUILayout.Toggle(manager.Mode==WorkshopMode.Assembly,"Assembly",GUI.skin.button) && manager.Mode!=WorkshopMode.Assembly)Run(()=>manager.SetMode(WorkshopMode.Assembly));
            if(GUILayout.Toggle(manager.Mode==WorkshopMode.Edit,"Edit",GUI.skin.button) && manager.Mode!=WorkshopMode.Edit)Run(()=>manager.SetMode(WorkshopMode.Edit));
            if(GUILayout.Button("JSON library")) { showFiles=!showFiles;RefreshFiles(); }
            if(GUILayout.Button("Return to workshop [Tab]"))SetOpen(false);
            GUILayout.EndHorizontal();
            GUILayout.Label(manager.Status);
            if(!string.IsNullOrEmpty(message))GUILayout.Label(message);
            if(showFiles)
            {
                filesScroll=GUILayout.BeginScrollView(filesScroll,GUILayout.Height(105));
                foreach(var path in files)if(GUILayout.Button("Load "+Path.GetFileName(path)))Run(()=>manager.LoadRecipe(AssemblyRecipeStore.Load(path)));
                if(!Application.isEditor && catalog && catalog.recipes!=null)
                    foreach(var asset in catalog.recipes)if(asset && GUILayout.Button("Built-in: "+asset.name))Run(()=>manager.LoadRecipe(AssemblyRecipeStore.Parse(asset.text)));
                if(files.Length==0 && (!catalog || catalog.recipes==null || catalog.recipes.Length==0))GUILayout.Label("No saved recipes yet.");
                GUILayout.EndScrollView();
            }
            if(manager.Recipe!=null)
            {
                GUILayout.BeginHorizontal();
                chaptersScroll=GUILayout.BeginScrollView(chaptersScroll,GUILayout.Width(230));
                for(int i=0;i<manager.Recipe.chapters.Count;i++)
                {
                    int index=i;var chapter=manager.Recipe.chapters[i];
                    if(GUILayout.Button((i==manager.CurrentChapterIndex?"• ":"")+(i+1)+". "+chapter.title,GUILayout.Height(36)))
                        Run(()=> { manager.SelectChapter(index);selectedId=""; });
                }
                GUILayout.EndScrollView();
                GUILayout.BeginVertical();
                var current=manager.Recipe.chapters[manager.CurrentChapterIndex];
                GUILayout.Label(current.description);
                GUILayout.Label(manager.ChapterInstalledCount+" / "+manager.ChapterPartCount+" parts installed");
                if(manager.Mode==WorkshopMode.Assembly)
                {
                    GUI.enabled=manager.ChapterComplete;
                    if(GUILayout.Button(manager.CurrentChapterIndex==manager.Recipe.chapters.Count-1?"Finish assembly":"Continue to next chapter"))manager.ContinueChapter();
                    GUI.enabled=true;
                    if(GUILayout.Button("Restart chapter"))manager.SelectChapter(manager.CurrentChapterIndex);
                    GUILayout.Label("Equal order numbers can be assembled in any order.\nBlue: target   Green: ready   Amber: blocked");
                }
                partsScroll=GUILayout.BeginScrollView(partsScroll);
                foreach(var d in manager.Recipe.parts.Where(p=>p.chapterId==current.id).OrderBy(p=>p.order).ThenBy(p=>p.displayName))
                {
                    GUILayout.BeginHorizontal();
                    if(GUILayout.Button((manager.IsPlaced(d.id)?"✓ ":"")+d.order.ToString("000")+"  "+d.displayName))selectedId=d.id;
                    if(manager.Mode==WorkshopMode.Edit)
                    {
                        if(GUILayout.Button("−",GUILayout.Width(28)))d.order=Mathf.Max(0,d.order-1);
                        if(GUILayout.Button("+",GUILayout.Width(28)))d.order++;
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
                var selected=manager.Recipe.parts.FirstOrDefault(p=>p.id==selectedId);
                if(selected!=null && manager.Mode==WorkshopMode.Edit)
                {
                    GUILayout.Label(selected.displayName);
                    GUILayout.BeginHorizontal();GUILayout.Label("Assembly priority",GUILayout.Width(130));
                    if(int.TryParse(GUILayout.TextField(selected.order.ToString()),out int order))selected.order=Mathf.Max(0,order);
                    GUILayout.EndHorizontal();
                    selected.isBase=GUILayout.Toggle(selected.isBase,"Preset base (move with workpiece)");
                    GUILayout.Label("Base orientation required:");
                    selected.orientation=(AssemblyOrientation)GUILayout.SelectionGrid((int)selected.orientation,new[]{"Any","Upright","Upside down"},3);
                    GUILayout.BeginHorizontal();
                    if(GUILayout.Button("Capture target from part"))Run(()=>manager.CaptureTarget(manager.FindPart(selected.id)));
                    if(GUILayout.Button("Capture loose tray position"))Run(()=>manager.CaptureTray(manager.FindPart(selected.id)));
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();GUILayout.EndHorizontal();
                if(manager.Mode==WorkshopMode.Edit)
                {
                    GUILayout.BeginHorizontal();filename=GUILayout.TextField(filename);
                    if(GUILayout.Button("Save JSON"))Run(()=> { manager.Save(filename);RefreshFiles(); });
                    if(GUILayout.Button("Apply / preview edits"))Run(()=>manager.LoadRecipe(manager.Recipe));
                    GUILayout.EndHorizontal();
                }
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("Flip workpiece 180°"))manager.AssemblyRoot.GetComponent<PickupablePlatform>().Flip();
                if(GUILayout.Button("Move workpiece")) { var player=FindAnyObjectByType<PlayerAssemblyController>();if(player) { SetOpen(false);player.PickUpWorkpiece(); } }
                if(GUILayout.Button("Play selected chapter")) { Run(()=>manager.SetMode(WorkshopMode.Assembly));SetOpen(false); }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }
    }
}


