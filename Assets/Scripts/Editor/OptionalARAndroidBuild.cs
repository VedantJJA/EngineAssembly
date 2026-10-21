using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace EngineAssembly.Editor
{
    // AR is optional: Play Store must not filter out phones that can use the
    // normal mobile scenes. Work on the generated Gradle manifest only.
    public sealed class OptionalARAndroidBuild : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder=>1000;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string file=Path.Combine(path,"src/main/AndroidManifest.xml");
            var document=new XmlDocument();document.Load(file);var root=document.DocumentElement;
            const string android="http://schemas.android.com/apk/res/android",tools="http://schemas.android.com/tools";
            root.SetAttribute("xmlns:tools",tools);
            // CAMERA permission also implies autofocus unless explicitly
            // optional. Fixed-focus devices must retain access to phone view.
            foreach(string name in new[]{"android.hardware.camera","android.hardware.camera.autofocus","android.hardware.camera.any","android.hardware.camera.ar"})
            {
                XmlElement feature=null;
                foreach(XmlElement item in root.SelectNodes("uses-feature"))if(item.GetAttribute("name",android)==name)feature=item;
                if(feature==null){feature=document.CreateElement("uses-feature");root.AppendChild(feature);}
                feature.SetAttribute("name",android,name);feature.SetAttribute("required",android,"false");feature.SetAttribute("replace",tools,"android:required");
            }
            var app=(XmlElement)root.SelectSingleNode("application");
            XmlElement metadata=null;foreach(XmlElement item in app.SelectNodes("meta-data"))if(item.GetAttribute("name",android)=="com.google.ar.core")metadata=item;
            if(metadata==null){metadata=document.CreateElement("meta-data");app.AppendChild(metadata);}
            metadata.SetAttribute("name",android,"com.google.ar.core");metadata.SetAttribute("value",android,"optional");metadata.SetAttribute("replace",tools,"android:value");
            document.Save(file);
        }
    }
}
