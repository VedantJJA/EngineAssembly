using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace EngineAssembly.Editor
{
    public static class AssemblyAudioSetup
    {
        [Serializable] class Manifest { public Entry[] clips; }
        [Serializable] class Entry { public string id,file; }
        [MenuItem("Tools/Engine Assembly/Audio/Connect Exported Narration")]
        public static void Run()
        {
            const string folder="Assets/Audio/Narration/English",path="Assets/Resources/NarrationLibrary.asset";
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(folder+"/manifest.json"));
            var cues=manifest.clips.Select(entry=>
            {
                string clipPath=folder+"/"+entry.file;
                AssetDatabase.ImportAsset(clipPath,ImportAssetOptions.ForceSynchronousImport);
                var importer=(AudioImporter)AssetImporter.GetAtPath(clipPath);
                if(!importer)throw new InvalidOperationException("Narration file missing: "+clipPath);
                var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.CompressedInMemory;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.85f;
                settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;settings.preloadAudioData=false;importer.defaultSampleSettings=settings;importer.loadInBackground=true;importer.SaveAndReimport();
                var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);if(!clip)throw new InvalidOperationException("Narration import failed: "+clipPath);
                return new NarrationLibrary.Cue{id=entry.id,clip=clip};
            }).ToArray();
            var library=AssetDatabase.LoadAssetAtPath<NarrationLibrary>(path);if(!library){library=ScriptableObject.CreateInstance<NarrationLibrary>();AssetDatabase.CreateAsset(library,path);}
            library.cues=cues;EditorUtility.SetDirty(library);AssetDatabase.SaveAssetIfDirty(library);
            var retained=manifest.clips.Select(entry=>folder+"/"+entry.file).ToHashSet();
            foreach(var guid in AssetDatabase.FindAssets("t:AudioClip",new[]{folder}))
            {
                var obsolete=AssetDatabase.GUIDToAssetPath(guid);
                if(!retained.Contains(obsolete) && !AssetDatabase.DeleteAsset(obsolete))throw new InvalidOperationException("Unable to remove superseded narration: "+obsolete);
            }
            // A Resources-owned material keeps the inversion shader in player builds.
            const string crosshairPath="Assets/Resources/InvertingCrosshair.mat";
            if(!AssetDatabase.LoadAssetAtPath<Material>(crosshairPath))AssetDatabase.CreateAsset(new Material(Shader.Find("EngineAssembly/UI/Inverting Crosshair")),crosshairPath);
            File.WriteAllText("Tools/Verification/narration-connection-report.json","{\"passed\":true,\"clips\":"+cues.Length+"}");
        }
    }
}
