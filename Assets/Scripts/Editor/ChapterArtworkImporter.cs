using UnityEditor;

namespace EngineAssembly.Editor
{
    // Preserve the photograph's actual aspect before the UI crops it.
    public sealed class ChapterArtworkImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/UI/ChapterImages/Light/"))return;
            var importer=(TextureImporter)assetImporter;
            importer.npotScale=TextureImporterNPOTScale.None;
            importer.mipmapEnabled=false;
            importer.wrapMode=UnityEngine.TextureWrapMode.Clamp;
        }
    }
}
