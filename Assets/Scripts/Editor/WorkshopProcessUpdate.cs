using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EngineAssembly.Editor
{
    public static class WorkshopProcessUpdate
    {
        [Serializable] public class TrayGroup
        {
            public string chapter,type;public string[] ids;public Vector3 center,size;
        }
        [Serializable] class Report { public TrayGroup[] groups;public float tableWidth,tableDepth; }
        public static void ApplyBatch()
        {
            if(!Application.isBatchMode || !Application.dataPath.Replace('\\','/').EndsWith("/Tools/Verification/UnityProject/Assets"))throw new Exception("Use the isolated verification project.");
            var scene=EditorSceneManager.OpenScene(AssemblyWorkshopBuilder.ScenePath,OpenSceneMode.Single);
            var manager=UnityEngine.Object.FindAnyObjectByType<AssemblyManager>();
            var table=scene.GetRootGameObjects().Single(o=>o.name=="Parts Table");
            table.GetComponent<PickupablePlatform>().SetPickupAllowed(false);
            scene.GetRootGameObjects().Single(o=>o.name=="Portable Work Platform").GetComponent<PickupablePlatform>().SetPickupAllowed(true);
            // Save only the explicit platform settings; runtime fixture reconstruction is not scene authoring.
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            manager.LoadRecipe(AssemblyRecipeStore.Load(AssemblyWorkshopBuilder.RecipePath));
            PackTray(manager,table.GetComponent<Renderer>().bounds);
            AssemblyRecipeStore.Save(manager.Recipe,"three-cylinder");AssetDatabase.ImportAsset(AssemblyWorkshopBuilder.RecipePath);
            MobileAssemblyBuilder.GenerateSubAssemblyArtworkBatch();
            AssetDatabase.SaveAssets();EditorApplication.Exit(0);
        }
        static void PackTray(AssemblyManager manager,Bounds table)
        {
            const float margin=.18f,groupGap=.22f,itemGap=.045f;
            float left=table.min.x+margin,right=table.max.x-margin,front=table.min.z+margin,back=table.max.z-margin;
            var records=new List<TrayGroup>();
            foreach(var chapter in manager.Recipe.chapters)
            {
                manager.SetMode(WorkshopMode.Assembly);manager.SelectChapter(manager.Recipe.chapters.IndexOf(chapter));
                var groups=manager.AssemblyParts.Where(p=>p.ChapterId==chapter.id && manager.CarrierDefinition(p.PartId)==null)
                    .GroupBy(AssemblyInventory.TypeName).OrderBy(g=>g.Min(p=>p.Definition.order)).ThenBy(g=>g.Key);
                float cursorX=left,cursorZ=front,rowDepth=0;
                foreach(var group in groups)
                {
                    var items=group.OrderBy(p=>p.PartId).ToArray();
                    foreach(var p in items)p.ResetLoose(manager.TrayFrame,p.Definition.trayPosition,p.Definition.trayRotation);
                    var bounds=items.Select(AssemblyHeldView.WorldBounds).ToArray();
                    float cellX=Mathf.Max(.12f,bounds.Max(b=>b.size.x)+itemGap),cellZ=Mathf.Max(.12f,bounds.Max(b=>b.size.z)+itemGap);
                    int columns=Mathf.Min(Mathf.CeilToInt(Mathf.Sqrt(items.Length)),Mathf.Max(1,Mathf.FloorToInt((right-left)/cellX)));
                    int rows=Mathf.CeilToInt((float)items.Length/columns);
                    float width=columns*cellX,depth=rows*cellZ;
                    if(width>right-left+.001f)throw new Exception("Tray group too wide: "+group.Key);
                    if(cursorX+width>right+.001f){cursorX=left;cursorZ+=rowDepth+groupGap;rowDepth=0;}
                    if(cursorZ+depth>back+.001f)throw new Exception("Tray group exceeds the table: "+chapter.title+" / "+group.Key);
                    for(int i=0;i<items.Length;i++)
                    {
                        var p=items[i];var center=new Vector3(cursorX+(i%columns+.5f)*cellX,table.max.y+.015f+bounds[i].extents.y,cursorZ+(i/columns+.5f)*cellZ);
                        var origin=p.transform.position+center-bounds[i].center;
                        p.Definition.trayPosition=manager.TrayFrame.InverseTransformPoint(origin);
                        p.ResetLoose(manager.TrayFrame,p.Definition.trayPosition,p.Definition.trayRotation);
                    }
                    records.Add(new TrayGroup{chapter=chapter.id,type=group.Key,ids=items.Select(p=>p.PartId).ToArray(),center=new Vector3(cursorX+width/2,table.max.y,cursorZ+depth/2),size=new Vector3(width,0,depth)});
                    cursorX+=width+groupGap;rowDepth=Mathf.Max(rowDepth,depth);
                }
            }
            File.WriteAllText("Tools/Verification/tray-groups.json",JsonUtility.ToJson(new Report{groups=records.ToArray(),tableWidth=table.size.x,tableDepth=table.size.z},true));
        }
    }
}
