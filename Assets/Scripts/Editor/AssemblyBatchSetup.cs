using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug=UnityEngine.Debug;

namespace EngineAssembly.Editor
{
    public class AssemblyBatchSetup : EditorWindow
    {
        enum ColliderMethod { Mesh, CoACD, VHACD }
        ColliderMethod method;
        bool convex=true,addParts=true;
        int maxHulls=16;
        float threshold=.05f;
        bool detailed;
        int timeLimitMinutes=30;
        string python="",status="";
        Vector2 scroll;
        readonly HashSet<GameObject> selected=new HashSet<GameObject>();
        readonly Queue<MeshFilter> pending=new Queue<MeshFilter>();
        Process process;
        MeshFilter current;
        string outputPath;
        StringBuilder processLog=new StringBuilder();
        double started;
        bool running;
        int completed,total;
        ColliderMethod jobMethod;
        bool jobConvex,jobAddParts;
        int jobMaxHulls;
        float jobThreshold;
        string jobPython;
        bool jobDetailed;
        int jobTimeLimit;
        double lastProgress;
        [Serializable] class Request { public string backend; public Vector3[] vertices;public int[] triangles;public int maxHulls;public float threshold;public bool merge=true;public int maxVertices=64,preprocessResolution=50,resolution=2000,searchNodes=20,searchDepth=3; }
        [Serializable] class Hull { public Vector3[] vertices;public int[] triangles; }
        [Serializable] class Result { public Hull[] hulls; }
        [Serializable] class LocalSettings { public string python; }
        [MenuItem("Tools/Engine Assembly/Batch Setup Parts")]
        public static void ShowWindow() { var window=GetWindow<AssemblyBatchSetup>("Assembly batch");window.minSize=new Vector2(490,720); }
        void OnEnable()
        {
            minSize=new Vector2(490,720);
            python=EditorPrefs.GetString("EngineAssembly.ColliderPython","");
            string settings=Path.GetFullPath("Tools/Colliders/settings.local.json");
            if(!File.Exists(python) && File.Exists(settings))python=JsonUtility.FromJson<LocalSettings>(File.ReadAllText(settings)).python;
            EditorApplication.update+=Tick;AssemblyReloadEvents.beforeAssemblyReload+=Cancel;
        }
        void OnDisable() { Cancel();EditorApplication.update-=Tick;AssemblyReloadEvents.beforeAssemblyReload-=Cancel; }
        void OnGUI()
        {
            EditorGUILayout.LabelField("Scene objects",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Choose roots below. Each mesh below the selected roots receives its own collider. Existing author colliders are disabled only after successful generation; triggers are preserved. Undo restores the previous setup.",MessageType.Info);
            using(new EditorGUI.DisabledScope(running))
            {
                if(GUILayout.Button("Use Hierarchy selection")) { selected.Clear();foreach(var go in Selection.gameObjects)if(go.scene.IsValid())selected.Add(go); }
                EditorGUILayout.BeginHorizontal();
                if(GUILayout.Button("Select all"))
                {
                    selected.Clear();
                    for(int s=0;s<SceneManager.sceneCount;s++)
                    {
                        var scene=SceneManager.GetSceneAt(s);if(!scene.isLoaded)continue;
                        foreach(var root in scene.GetRootGameObjects())
                            if(root.GetComponentsInChildren<MeshFilter>(true).Any(m=>m.sharedMesh && !m.GetComponentInParent<GeneratedColliderGroup>()))selected.Add(root);
                    }
                }
                if(GUILayout.Button("Clear selection"))selected.Clear();
                EditorGUILayout.EndHorizontal();
                scroll=EditorGUILayout.BeginScrollView(scroll,GUILayout.Height(Mathf.Clamp(position.height-(detailed && method==ColliderMethod.CoACD?530:420),90,270)));
                for(int s=0;s<SceneManager.sceneCount;s++)
                {
                    var scene=SceneManager.GetSceneAt(s);if(!scene.isLoaded)continue;
                    EditorGUILayout.LabelField(scene.name,EditorStyles.boldLabel);
                    foreach(var root in scene.GetRootGameObjects())
                    {
                        int count=root.GetComponentsInChildren<MeshFilter>(true).Count(m=>m.sharedMesh && !m.GetComponentInParent<GeneratedColliderGroup>());
                        if(count==0)continue;
                        bool check=EditorGUILayout.ToggleLeft(root.name+" ("+count+" meshes)",selected.Contains(root));
                        if(check)selected.Add(root);else selected.Remove(root);
                    }
                }
                foreach(var go in selected.Where(go=>go && go.transform.parent).ToArray())EditorGUILayout.LabelField("Selected: "+go.name);
                EditorGUILayout.EndScrollView();
                method=(ColliderMethod)EditorGUILayout.EnumPopup("Collider generator",method);
                if(method==ColliderMethod.Mesh)convex=EditorGUILayout.Toggle("Convex (movable objects)",convex);
                else
                {
                    if(method==ColliderMethod.CoACD)
                    {
                        EditorGUI.BeginChangeCheck();detailed=EditorGUILayout.Toggle("Detailed CAD cavities",detailed);
                        if(EditorGUI.EndChangeCheck()){maxHulls=detailed?256:24;threshold=detailed?.02f:.05f;timeLimitMinutes=detailed?60:30;}
                        threshold=EditorGUILayout.Slider("Concavity threshold",threshold,.01f,.2f);
                        if(detailed)EditorGUILayout.HelpBox("Fine convex hulls for loose physics, plus the exact CAD surface while carried or installed so small bores stay open. Complex CAD can take several minutes. Use the saved precise-head tool to reuse the completed head bake.",MessageType.Info);
                    }
                    maxHulls=EditorGUILayout.IntSlider("Maximum hulls",maxHulls,1,512);
                    timeLimitMinutes=EditorGUILayout.IntSlider("Time limit per mesh (min)",timeLimitMinutes,5,120);
                    EditorGUILayout.BeginHorizontal();python=EditorGUILayout.TextField("Python executable",python);
                    if(GUILayout.Button("Browse",GUILayout.Width(65))) { string p=EditorUtility.OpenFilePanel("Python executable","","exe");if(!string.IsNullOrEmpty(p)) { python=p;EditorPrefs.SetString("EngineAssembly.ColliderPython",p); } }
                    EditorGUILayout.EndHorizontal();
                }
                addParts=EditorGUILayout.Toggle("Add AssemblyPart + sockets",addParts);
                if(GUILayout.Button("Generate for selected objects",GUILayout.Height(34)))StartBatch();
            }
            EditorGUILayout.LabelField(status,EditorStyles.wordWrappedLabel);
            if(running && GUILayout.Button("Cancel remaining work"))Cancel();
        }
        void StartBatch()
        {
            if(method==ColliderMethod.Mesh && !convex && addParts) { status="Movable assembly parts require convex colliders.";return; }
            if(method!=ColliderMethod.Mesh && !File.Exists(python)) { status="Choose a Python executable with the project collider dependencies installed.";return; }
            var meshes=selected.Where(g=>g).SelectMany(g=>g.GetComponentsInChildren<MeshFilter>(true))
                .Where(m=>m.sharedMesh && !m.GetComponentInParent<GeneratedColliderGroup>()).Distinct().ToArray();
            if(meshes.Length==0) { status="Select at least one scene object containing meshes.";return; }
            jobMethod=method;jobConvex=convex;jobAddParts=addParts;jobMaxHulls=maxHulls;jobThreshold=threshold;jobPython=python;
            jobDetailed=detailed && method==ColliderMethod.CoACD;jobTimeLimit=timeLimitMinutes;EditorPrefs.SetString("EngineAssembly.ColliderPython",python);
            pending.Clear();foreach(var m in meshes)pending.Enqueue(m);
            total=pending.Count;completed=0;running=true;status="Starting...";
        }
        void Tick()
        {
            if(!running)return;
            if(EditorApplication.isPlayingOrWillChangePlaymode) { Cancel();status="Stopped before entering Play mode.";return; }
            try
            {
                if(process!=null)
                {
                    if(!process.HasExited)
                    {
                        double elapsed=EditorApplication.timeSinceStartup-started;
                        if(elapsed>jobTimeLimit*60)throw new TimeoutException("Collider generation exceeded "+jobTimeLimit+" minutes for "+current.name+". Increase the time limit for complex CAD; existing colliders were retained.");
                        if(EditorApplication.timeSinceStartup-lastProgress>1)
                        {
                            string message;lock(processLog)message=processLog.ToString().Trim().Split('\n').LastOrDefault()??"Loading backend";
                            status=(completed+1)+" / "+total+": "+current.name+" · "+TimeSpan.FromSeconds(elapsed).ToString(@"mm\:ss")+"\n"+message.Trim();lastProgress=EditorApplication.timeSinceStartup;Repaint();
                        }
                        return;
                    }
                    process.WaitForExit();int exit=process.ExitCode;process.Dispose();process=null;
                    if(exit!=0)throw new InvalidOperationException(jobMethod+" exited with code "+exit+". "+processLog.ToString()+"\nFull log: "+outputPath+".log");
                    if(!current)throw new InvalidOperationException("Target was removed during generation.");
                    var result=JsonUtility.FromJson<Result>(File.ReadAllText(outputPath));
                    if(result?.hulls==null || result.hulls.Length==0)throw new InvalidDataException("No collider hulls returned.");
                    var hulls=new List<Mesh>();
                    foreach(var h in result.hulls)
                    {
                        if(h.vertices==null || h.vertices.Length<4 || h.triangles==null || h.triangles.Length<12 || h.triangles.Length/3>255 || h.triangles.Any(i=>i<0 || i>=h.vertices.Length))throw new InvalidDataException("Invalid collider hull.");
                        var mesh=new Mesh { vertices=h.vertices,triangles=h.triangles };mesh.RecalculateBounds();mesh.RecalculateNormals();hulls.Add(mesh);
                    }
                    Apply(current,hulls.ToArray(),true);completed++;
                }
                if(pending.Count==0) { running=false;status="Finished: "+completed+" objects. Undo is available per object.";AssetDatabase.SaveAssets();Repaint();return; }
                current=pending.Dequeue();if(!current)return;
                status=(completed+1)+" / "+total+": "+current.name;Repaint();
                if(jobMethod==ColliderMethod.Mesh)
                {
                    if(!jobConvex && current.GetComponentInParent<Rigidbody>())throw new InvalidOperationException("Non-convex mesh collider cannot be applied beneath a Rigidbody: "+current.name);
                    Apply(current,new[]{current.sharedMesh},jobConvex);completed++;return;
                }
                if(!current.sharedMesh.isReadable)throw new InvalidOperationException("Enable Read/Write on the model importer before decomposition: "+current.name);
                string dir=Path.GetFullPath("Library/AssemblyColliderJobs");Directory.CreateDirectory(dir);
                string id=Guid.NewGuid().ToString("N"),input=Path.Combine(dir,id+".input.json");outputPath=Path.Combine(dir,id+".output.json");
                File.WriteAllText(input,JsonUtility.ToJson(new Request { backend=jobMethod.ToString(),vertices=current.sharedMesh.vertices,triangles=current.sharedMesh.triangles,maxHulls=jobMaxHulls,threshold=jobThreshold,maxVertices=jobDetailed?128:64,preprocessResolution=jobDetailed?75:50,searchNodes=jobDetailed?10:20,searchDepth=jobDetailed?2:3 }));
                var start=new ProcessStartInfo(jobPython,"-u "+Quote(Path.GetFullPath("Tools/Colliders/decompose.py"))+" --input "+Quote(input)+" --output "+Quote(outputPath))
                { UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true,WorkingDirectory=Path.GetFullPath(".") };
                start.EnvironmentVariables["OMP_NUM_THREADS"]="4";
                processLog.Clear();process=new Process { StartInfo=start };
                string logPath=outputPath+".log";
                DataReceivedEventHandler log=(s,e)=> { if(e.Data!=null)lock(processLog){processLog.AppendLine(e.Data);File.AppendAllText(logPath,e.Data+Environment.NewLine);} };
                process.ErrorDataReceived+=log;process.OutputDataReceived+=log;
                process.Start();process.BeginErrorReadLine();process.BeginOutputReadLine();started=EditorApplication.timeSinceStartup;
            }
            catch(Exception ex) { Cancel();status="Stopped: "+ex.Message;Debug.LogError(status);Repaint(); }
        }
        static string Quote(string text)=>"\""+text.Replace("\"","\\\"")+"\"";
        void Apply(MeshFilter target,Mesh[] meshes,bool isConvex)
        {
            // CAD hull coordinates may be microscopic even when their object scale is huge.
            // Cook centred, unit-sized hulls before creating assets or replacing scene components.
            var prepared=isConvex?meshes.Select((m,i)=>ColliderHullCooking.Prepare(m,target.name+"_Hull_"+i)).Where(h=>h!=null).ToArray():null;
            if(isConvex && prepared.Length==0)throw new InvalidOperationException("The decomposition contains no solid collider hulls. Existing colliders were retained.");
            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Generate "+jobMethod+" colliders");
            GameObject container=null;
            try
            {
                // Prepare assets before replacing any existing collider objects.
                if(prepared!=null)
                {
                    Directory.CreateDirectory("Assets/GeneratedColliders");AssetDatabase.Refresh();
                    string path=AssetDatabase.GenerateUniqueAssetPath("Assets/GeneratedColliders/Collider.asset");
                    var asset=ScriptableObject.CreateInstance<ColliderHullSet>();asset.hulls=prepared;AssetDatabase.CreateAsset(asset,path);
                    foreach(var hull in prepared)AssetDatabase.AddObjectToAsset(hull.mesh,path);
                    EditorUtility.SetDirty(asset);
                }
                var previous=target.GetComponentsInChildren<GeneratedColliderGroup>(true).Where(g=>g.transform.parent==target.transform).ToArray();
                container=new GameObject("Generated Colliders ("+jobMethod+")");Undo.RegisterCreatedObjectUndo(container,"Create colliders");
                container.transform.SetParent(target.transform,false);container.AddComponent<GeneratedColliderGroup>().backend=jobMethod.ToString();
                if(prepared!=null)foreach(var hull in prepared)ColliderHullCooking.Add(container.transform,hull,true);
                else foreach(var mesh in meshes)
                {
                    var go=new GameObject("Hull");Undo.RegisterCreatedObjectUndo(go,"Create hull");go.transform.SetParent(container.transform,false);
                    var collider=Undo.AddComponent<MeshCollider>(go);collider.convex=isConvex;collider.sharedMesh=mesh;
                    collider.contactOffset=.001f;
                }
                foreach(var old in target.GetComponents<Collider>().Where(c=>!c.isTrigger)) { Undo.RecordObject(old,"Disable old collider");old.enabled=false; }
                foreach(var old in previous)Undo.DestroyObjectImmediate(old.gameObject);
                if(jobAddParts)SetupPart(target.gameObject);
                if(jobDetailed)AddPrecisionSurface(target.gameObject);
                EditorSceneManager.MarkSceneDirty(target.gameObject.scene);Undo.CollapseUndoOperations(undo);
            }
            catch { Undo.RevertAllDownToGroup(undo);throw; }
        }
        public static AssemblyPart SetupPart(GameObject go)
        {
            var precision=go.GetComponent<AssemblyPrecisionSurface>();if(precision)precision.PreparePhysics(false);
            var body=go.GetComponent<Rigidbody>();if(!body)body=Undo.AddComponent<Rigidbody>(go);Undo.RecordObject(body,"Stage part");body.isKinematic=true;body.useGravity=false;
            if(precision)precision.RefreshPhysics();
            var part=go.GetComponent<AssemblyPart>();if(!part)part=Undo.AddComponent<AssemblyPart>(go);
            Undo.RecordObject(part,"Configure assembly part");
            bool duplicate=FindObjectsByType<AssemblyPart>(FindObjectsInactive.Include).Any(p=>p!=part && p.PartId==part.PartId);
            if(string.IsNullOrEmpty(part.PartId) || duplicate)part.SetIdentity(Guid.NewGuid().ToString("N"),go.name);
            if(int.TryParse(go.name.Split('_')[0],out int priority))part.OrderIndex=priority;
            if(!part.TargetSocket)
            {
                var socket=new GameObject(go.name+"_Socket");Undo.RegisterCreatedObjectUndo(socket,"Create socket");
                socket.transform.SetParent(go.transform.parent,true);socket.transform.SetPositionAndRotation(go.transform.position,go.transform.rotation);
                part.TargetSocket=Undo.AddComponent<AssemblySocket>(socket);part.TargetSocket.Configure(part);
            }
            EditorUtility.SetDirty(part);return part;
        }
        public static void AddPrecisionSurface(GameObject go)
        {
            var surface=go.transform.Find("Precision CAD surface");
            if(!surface){var child=new GameObject("Precision CAD surface");Undo.RegisterCreatedObjectUndo(child,"Create precision surface");child.transform.SetParent(go.transform,false);surface=child.transform;}
            var collider=surface.GetComponent<MeshCollider>();if(!collider)collider=Undo.AddComponent<MeshCollider>(surface.gameObject);
            Undo.RecordObject(collider,"Configure precision surface");collider.convex=false;collider.sharedMesh=go.GetComponent<MeshFilter>().sharedMesh;collider.contactOffset=.001f;collider.enabled=false;
            if(go.name=="401_Cylinder_Head_01" || go.GetComponent<MeshFilter>().sharedMesh.name=="401_Cylinder_Head_01")
            {
                const string path="Assets/GeneratedColliders/CylinderHeadExactNormalized.asset";
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);var source=go.GetComponent<MeshFilter>().sharedMesh;
                var normalized=AssemblyQueryGeometry.Normalize(source,true);
                if(!mesh){mesh=UnityEngine.Object.Instantiate(normalized);mesh.hideFlags=HideFlags.None;AssetDatabase.CreateAsset(mesh,path);}
                else{EditorUtility.CopySerialized(normalized,mesh);mesh.hideFlags=HideFlags.None;EditorUtility.SetDirty(mesh);}
                Undo.RecordObject(surface,"Normalize head collider frame");AssemblyQueryGeometry.SetFrame(surface,source);collider.cookingOptions=AssemblyQueryGeometry.Cooking;collider.sharedMesh=mesh;
            }
            var precision=go.GetComponent<AssemblyPrecisionSurface>();if(!precision)precision=Undo.AddComponent<AssemblyPrecisionSurface>(go);
            Undo.RecordObject(precision,"Configure precision surface");precision.Configure(collider);EditorUtility.SetDirty(precision);
        }
        void Cancel()
        {
            if(process!=null) { try { if(!process.HasExited)process.Kill(); }catch(Exception){} process.Dispose();process=null; }
            pending.Clear();running=false;status="Cancelled. Completed objects remain; each can be undone.";
        }
    }
}


