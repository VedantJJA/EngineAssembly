using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EngineAssembly.Editor
{
    public static class EngineColliderBake
    {
        const string Model="Assets/Model/Edit/3cyl_Labeled.fbx";
        const string Wire="Assets/Model/Edit/909_Motor_Wiring_Surface.fbx";
        const string Cache="Tools/Colliders/EngineBake";
        const string Output="Assets/GeneratedColliders/EngineCoACD";
        [Serializable] public class Request { public string backend="CoACD";public Vector3[] vertices;public int[] triangles;public int maxHulls=24;public float threshold=.04f; }
        [Serializable] public class Hull { public Vector3[] vertices;public int[] triangles; }
        [Serializable] public class Result { public Hull[] hulls; }
        [Serializable] public class Record { public string name,key; }
        [Serializable] public class Manifest { public List<Record> parts=new List<Record>(); }
        static string Hash(string value) { using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant(); }
        public static void Export()
        {
            Directory.CreateDirectory(Cache);
            var importer=(ModelImporter)AssetImporter.GetAtPath(Model);
            if(!importer.isReadable) { importer.isReadable=true;importer.SaveAndReimport(); }
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Model);var manifest=new Manifest();
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!filter.sharedMesh)continue;
                var mesh=filter.sharedMesh.vertexCount==0?WireMesh():filter.sharedMesh;
                var json=JsonUtility.ToJson(new Request{vertices=mesh.vertices,triangles=mesh.triangles});
                var key=Hash(json);File.WriteAllText(Cache+"/"+key+".input.json",json);manifest.parts.Add(new Record{name=filter.name,key=key});
            }
            File.WriteAllText(Cache+"/manifest.json",JsonUtility.ToJson(manifest,true));
            Debug.Log("COACD EXPORT: "+manifest.parts.Count+" meshes, "+manifest.parts.Select(p=>p.key).Distinct().Count()+" unique geometries.");
        }
        public static void Apply()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Apply using the isolated bake project to preserve open scene edits.");
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Cache+"/manifest.json"));
            Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            var meshes=new Dictionary<string,Mesh[]>();int hullCount=0;
            foreach(var record in manifest.parts.GroupBy(r=>r.key).Select(g=>g.First()))
            {
                var result=JsonUtility.FromJson<Result>(File.ReadAllText(Cache+"/"+record.key+".output.json"));
                if(result.hulls==null || result.hulls.Length==0)throw new InvalidDataException("Empty hulls: "+record.name);
                var path=Output+"/"+record.key+".asset";
                var existing=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().OrderBy(m=>m.name).ToArray();
                if(existing.Length>0) { meshes[record.key]=existing;continue; }
                var set=new List<Mesh>();
                for(int i=0;i<result.hulls.Length;i++)
                {
                    var h=result.hulls[i];if(h.vertices.Length<4 || h.triangles.Length/3>255)throw new InvalidDataException(record.name);
                    var m=new Mesh{name="Hull_"+i.ToString("000"),vertices=h.vertices,triangles=h.triangles};m.RecalculateNormals();m.RecalculateBounds();
                    if(i==0)AssetDatabase.CreateAsset(m,path);else AssetDatabase.AddObjectToAsset(m,path);set.Add(m);
                }
                meshes[record.key]=set.ToArray();
            }
            foreach(var scenePath in new[]{"Assets/Scenes/AssemblyWorkshop.unity","Assets/Scenes/MobileAssembly.unity"})
            {
                var scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Single);int applied=0;
                foreach(var root in scene.GetRootGameObjects())foreach(var part in root.GetComponentsInChildren<AssemblyPart>(true))
                {
                    var record=manifest.parts.FirstOrDefault(r=>r.name==part.name);if(record==null)continue;
                    Install(part.gameObject,meshes[record.key]);hullCount+=meshes[record.key].Length;applied++;
                }
                if(applied!=manifest.parts.Count)throw new InvalidDataException(scenePath+": expected "+manifest.parts.Count+", applied "+applied);
                Physics.SyncTransforms();
                foreach(var group in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GeneratedColliderGroup>(true)))
                    foreach(var collider in group.GetComponentsInChildren<MeshCollider>())
                        if(!collider.convex || !collider.sharedMesh || collider.sharedMesh.triangles.Length/3>255 || collider.bounds.size.sqrMagnitude==0)
                            throw new InvalidDataException("Invalid cooked collider: "+group.transform.parent.name+"/"+collider.name);
                EditorSceneManager.SaveScene(scene);Debug.Log("COACD APPLIED: "+scenePath+" "+applied+" parts.");
            }
            var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model));model.name="Three Cylinder Engine - CoACD";
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true))
            { var record=manifest.parts.Single(r=>r.name==filter.name);Install(filter.gameObject,meshes[record.key]); }
            Directory.CreateDirectory("Assets/Prefabs");AssetDatabase.Refresh();
            PrefabUtility.SaveAsPrefabAsset(model,"Assets/Prefabs/ThreeCylinderEngine_CoACD.prefab");UnityEngine.Object.DestroyImmediate(model);AssetDatabase.SaveAssets();
            File.WriteAllText(Cache+"/report.json",JsonUtility.ToJson(new BakeReport{parts=manifest.parts.Count,uniqueMeshes=meshes.Count,sceneHulls=hullCount,prefab="Assets/Prefabs/ThreeCylinderEngine_CoACD.prefab"},true));
            Debug.Log("COACD BAKE COMPLETE: "+hullCount+" scene hulls and reusable engine prefab.");
            MobileAssemblyBuilder.GenerateWiringArtworkBatch();
        }
        [Serializable] class BakeReport { public int parts,uniqueMeshes,sceneHulls;public string prefab; }
        static void Install(GameObject target,Mesh[] meshes)
        {
            if(target.name=="909_Motor_Wiring_01")target.GetComponent<MeshFilter>().sharedMesh=WireMesh();
            foreach(var group in target.GetComponentsInChildren<GeneratedColliderGroup>(true).Where(g=>g.transform.parent==target.transform).ToArray())UnityEngine.Object.DestroyImmediate(group.gameObject);
            foreach(var collider in target.GetComponents<Collider>())if(!collider.isTrigger)collider.enabled=false;
            var root=new GameObject("Generated Colliders (CoACD)");root.transform.SetParent(target.transform,false);root.AddComponent<GeneratedColliderGroup>().backend="CoACD";
            foreach(var mesh in meshes)
            { var child=new GameObject(mesh.name);child.transform.SetParent(root.transform,false);var collider=child.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.convex=true; }
        }
        static Mesh WireMesh()
        {
            const string path="Assets/Model/Edit/909_Motor_Wiring_Surface.asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved)return saved;
            var importer=(ModelImporter)AssetImporter.GetAtPath(Wire);
            if(!importer.isReadable) { importer.isReadable=true;importer.SaveAndReimport(); }
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Wire).GetComponentInChildren<MeshFilter>();
            var original=AssetDatabase.LoadAssetAtPath<GameObject>(Model).GetComponentsInChildren<MeshFilter>().Single(f=>f.name=="909_Motor_Wiring_01");
            var matrix=original.transform.worldToLocalMatrix*source.transform.localToWorldMatrix;
            var mesh=UnityEngine.Object.Instantiate(source.sharedMesh);mesh.name="Motor wiring - original CAD frame";
            mesh.vertices=mesh.vertices.Select(v=>matrix.MultiplyPoint3x4(v)).ToArray();
            if(matrix.determinant<0) { var triangles=mesh.triangles;for(int i=0;i<triangles.Length;i+=3) { int t=triangles[i];triangles[i]=triangles[i+1];triangles[i+1]=t; }mesh.triangles=triangles; }
            mesh.RecalculateBounds();mesh.RecalculateNormals();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
    }
}
