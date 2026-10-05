using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace EngineAssembly.Editor
{
    public static class CorrectedHeadGeometryBake
    {
        [Serializable] class Request{public string backend="CoACD";public Vector3[] vertices;public int[] triangles;public int maxHulls=256;public float threshold=.02f;public bool merge=true;public int resolution=2000,searchNodes=10,searchIterations=60,searchDepth=2,maxVertices=128,preprocessResolution=75;}
        public static void ExportBatch()
        {
            if(!Application.isBatchMode)throw new Exception("Use isolated Unity project.");
            AssetDatabase.ImportAsset("Assets/Model/Edit/3cyl_Labeled.fbx",ImportAssetOptions.ForceUpdate);
            EngineColliderBake.Export();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/Edit/3cyl_Labeled.fbx");var filters=model.GetComponentsInChildren<MeshFilter>(true);
            if(filters.Length!=318)throw new Exception("Corrected model lost labelled engine parts.");
            var head=filters.Single(f=>f.name=="401_Cylinder_Head_01").sharedMesh;
            Directory.CreateDirectory("Tools/Colliders/HeadCorrected");
            File.WriteAllText("Tools/Colliders/HeadCorrected/head.input.json",JsonUtility.ToJson(new Request{vertices=head.vertices,triangles=head.triangles}));
            Debug.Log("CORRECTED CAD HEAD EXPORT: "+head.vertexCount+" vertices, "+head.triangles.Length/3+" triangles; geometry key "+EngineColliderBake.GeometryKey(head));
            EditorApplication.Exit(0);
        }
    }
}
