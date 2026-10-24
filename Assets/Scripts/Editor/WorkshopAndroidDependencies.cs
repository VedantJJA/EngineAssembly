using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace EngineAssembly.Editor
{
    // Keep Unity's frame pacing module available when Java cannot reach Google Maven.
    public sealed class WorkshopAndroidDependencies:IPostGenerateGradleAndroidProject
    {
        public int callbackOrder=>1100;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string repository=Path.GetFullPath(Path.Combine(Application.dataPath,"../Tools/Android/maven")).Replace('\\','/');
            if(!File.Exists(Path.Combine(repository,"androidx/games/games-frame-pacing/2.1.2/games-frame-pacing-2.1.2.aar")))
                throw new FileNotFoundException("The verified frame pacing dependency is missing from Tools/Android/maven.");
            string file=Path.Combine(Directory.GetParent(path).FullName,"settings.gradle");
            const string marker="// Workshop Android frame pacing repository";
            string settings=File.ReadAllText(file);if(settings.Contains(marker))return;
            File.AppendAllText(file,"\n"+marker+"\ndependencyResolutionManagement {\n    repositories {\n        exclusiveContent {\n            forRepository { maven { url uri(\""+repository+"\") } }\n            filter { includeModule(\"androidx.games\", \"games-frame-pacing\") }\n        }\n    }\n}\n");
        }
    }
}
