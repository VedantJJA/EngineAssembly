using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace EngineAssembly.Editor
{
    [InitializeOnLoad] public static class QuestHandsInstaller
    {
        static SearchRequest search;static AddRequest add;static double deadline;
        static QuestHandsInstaller(){if(SessionState.GetBool("QuestHands.Install",false)){deadline=EditorApplication.timeSinceStartup+600;EditorApplication.update+=Poll;}}
        public static void Install()
        {
            SessionState.SetBool("QuestHands.Install",true);deadline=EditorApplication.timeSinceStartup+600;
            search=Client.Search("com.unity.xr.hands");EditorApplication.update-=Poll;EditorApplication.update+=Poll;
        }
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup>deadline){Finish(false,"XR Hands package resolution timed out");return;}
            if(search!=null)
            {
                if(!search.IsCompleted)return;
                if(search.Status!=StatusCode.Success || search.Result.Length==0){Finish(false,search.Error?.message??"XR Hands not found");return;}
                Debug.Log("XR Hands registry package found: "+search.Result[0].name);search=null;add=Client.Add("com.unity.xr.hands");return;
            }
            if(add!=null){if(!add.IsCompleted)return;if(add.Status!=StatusCode.Success){Finish(false,add.Error?.message);return;}}
            var info=UnityEditor.PackageManager.PackageInfo.FindForPackageName("com.unity.xr.hands");if(info==null)return;
            Directory.CreateDirectory("Tools/Verification");File.WriteAllText("Tools/Verification/quest-hands-package.json",JsonUtility.ToJson(new Installed{version=info.version,path=info.resolvedPath},true));Finish(true,info.name+"@"+info.version);
        }
        [Serializable] sealed class Installed{public string version,path;}
        static void Finish(bool ok,string message){EditorApplication.update-=Poll;SessionState.SetBool("QuestHands.Install",false);Debug.Log(message);EditorApplication.Exit(ok?0:1);}
    }
}
