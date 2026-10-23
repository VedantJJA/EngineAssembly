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
            var text=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/NarrationText.json");
            var required=text?JsonUtility.FromJson<SpokenCatalogue>(text.text).cues.Select(c=>c.id).ToHashSet():null;
            var exported=manifest.clips.Select(c=>c.id).ToHashSet();
            if(required!=null && !required.IsSubsetOf(exported))throw new InvalidOperationException("Narration generation is incomplete. Finish exporting the missing voice clips before connecting the full library.");
            var cues=manifest.clips.Select(entry=>
            {
                string clipPath=folder+"/"+entry.file;
                AssetDatabase.ImportAsset(clipPath,ImportAssetOptions.ForceSynchronousImport);
                var importer=(AudioImporter)AssetImporter.GetAtPath(clipPath);
                if(!importer)throw new InvalidOperationException("Narration file missing: "+clipPath);
                var settings=importer.defaultSampleSettings;
                bool update=settings.loadType!=AudioClipLoadType.CompressedInMemory || settings.compressionFormat!=AudioCompressionFormat.Vorbis || !Mathf.Approximately(settings.quality,.85f) || settings.sampleRateSetting!=AudioSampleRateSetting.PreserveSampleRate || settings.preloadAudioData || !importer.loadInBackground || !importer.forceToMono;
                if(update){settings.loadType=AudioClipLoadType.CompressedInMemory;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.85f;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;settings.preloadAudioData=false;importer.defaultSampleSettings=settings;importer.loadInBackground=true;importer.forceToMono=true;importer.SaveAndReimport();}
                var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);if(!clip)throw new InvalidOperationException("Narration import failed: "+clipPath);
                if(clip.length<=0)throw new InvalidOperationException("Narration clip has no playable audio: "+clipPath);
                return new NarrationLibrary.Cue{id=entry.id,clip=clip};
            }).ToArray();
            var library=AssetDatabase.LoadAssetAtPath<NarrationLibrary>(path);if(!library){library=ScriptableObject.CreateInstance<NarrationLibrary>();AssetDatabase.CreateAsset(library,path);}
            library.cues=cues;EditorUtility.SetDirty(library);AssetDatabase.SaveAssetIfDirty(library);
            var retained=manifest.clips.Select(entry=>folder+"/"+entry.file).ToHashSet();
            // A running generation manifest is partial. Never delete clips that
            // are waiting to be regenerated or are still referenced by a scene.
            // A Resources-owned material keeps the inversion shader in player builds.
            const string crosshairPath="Assets/Resources/InvertingCrosshair.mat";
            if(!AssetDatabase.LoadAssetAtPath<Material>(crosshairPath))AssetDatabase.CreateAsset(new Material(Shader.Find("EngineAssembly/UI/Inverting Crosshair")),crosshairPath);
            File.WriteAllText("Tools/Verification/narration-connection-report.json","{\"passed\":true,\"clips\":"+cues.Length+"}");
        }
    }
    public sealed class NarrationExportWatcher : AssetPostprocessor
    {
        static bool queued;
        [InitializeOnLoadMethod] static void Initialize(){EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=ConnectAvailable;};EditorApplication.delayCall+=()=>{if(!EditorApplication.isPlaying)ConnectAvailable();};}
        static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] oldPaths)
        {
            if(queued || !imported.Any(p=>p=="Assets/Audio/Narration/English/manifest.json" || p.StartsWith("Assets/Audio/Narration/English/") && p.EndsWith(".wav")))return;
            queued=true;EditorApplication.delayCall+=()=>{queued=false;if(EditorApplication.isCompiling || EditorApplication.isPlaying)return;ConnectAvailable();};
        }
        public static void ConnectAvailable()
        {
            const string folder="Assets/Audio/Narration/English";
            var library=AssetDatabase.LoadAssetAtPath<NarrationLibrary>("Assets/Resources/NarrationLibrary.asset");if(!library)return;
            var text=Resources.Load<TextAsset>("NarrationText");var required=text?JsonUtility.FromJson<SpokenCatalogue>(text.text).cues.Select(c=>c.id).ToHashSet():null;
            var entries=library.cues.Where(c=>required==null || required.Contains(c.id)).ToDictionary(c=>c.id,c=>c);
            foreach(var path in System.IO.Directory.EnumerateFiles(folder,"*.wav",SearchOption.AllDirectories))
            {
                string asset=path.Replace('\\','/');var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(asset);if(!clip)continue;
                string id=System.IO.Path.GetFileNameWithoutExtension(path);if(required==null || required.Contains(id))entries[id]=new NarrationLibrary.Cue{id=id,clip=clip};
            }
            library.cues=entries.Values.ToArray();EditorUtility.SetDirty(library);AssetDatabase.SaveAssetIfDirty(library);
        }
        void OnPreprocessAudio()
        {
            if(!assetPath.StartsWith("Assets/Audio/Narration/English/"))return;
            var importer=(AudioImporter)assetImporter;var settings=importer.defaultSampleSettings;
            settings.loadType=AudioClipLoadType.CompressedInMemory;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.85f;settings.preloadAudioData=false;
            importer.defaultSampleSettings=settings;importer.loadInBackground=true;importer.forceToMono=true;
        }
    }
}
