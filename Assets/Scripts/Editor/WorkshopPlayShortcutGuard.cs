using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace EngineAssembly.Editor
{
    [InitializeOnLoad]
    public static class WorkshopPlayShortcutGuard
    {
        // Unity's global GUI handlers run before the shortcut dispatcher. Prepend our
        // scoped handler without changing the user's saved shortcut profile.
        static readonly FieldInfo handlers=typeof(EditorApplication).GetField("globalEventHandler",BindingFlags.Static|BindingFlags.NonPublic);
        static readonly Delegate callback;
        public static int SuppressedSaveEvents { get; private set; }
        static WorkshopPlayShortcutGuard()
        {
            if(handlers==null)return;
            callback=Delegate.CreateDelegate(handlers.FieldType,typeof(WorkshopPlayShortcutGuard).GetMethod(nameof(Handle),BindingFlags.Static|BindingFlags.NonPublic));
            handlers.SetValue(null,Delegate.Combine(callback,handlers.GetValue(null) as Delegate));
            AssemblyReloadEvents.beforeAssemblyReload+=Remove;
            EditorApplication.quitting+=Remove;
        }
        static void Remove(){if(handlers!=null && callback!=null)handlers.SetValue(null,Delegate.Remove(handlers.GetValue(null) as Delegate,callback));}
        static void Handle()
        {
            var ui=AssemblyManager.Instance?AssemblyManager.Instance.GetComponent<AssemblyWorkshopUI>():null;
            Consume(Event.current,EditorApplication.isPlaying,EditorWindow.focusedWindow && EditorWindow.focusedWindow.GetType().Name=="GameView",ui && !ui.IsOpen);
        }
        public static bool Consume(Event evt,bool playing,bool gameViewFocused,bool workshopInput)
        {
            if(evt==null || !playing || !gameViewFocused || !workshopInput || evt.type!=EventType.KeyDown || evt.keyCode!=KeyCode.S || !(evt.control || evt.command))return false;
            evt.Use();SuppressedSaveEvents++;return true;
        }
    }
}
