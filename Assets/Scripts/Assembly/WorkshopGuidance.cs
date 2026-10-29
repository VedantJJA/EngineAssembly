using System;
using UnityEngine;

namespace EngineAssembly
{
    public static class WorkshopGuidance
    {
        public const string HintsKey="Assembly.GhostHints",ArrowKey="Assembly.DirectionArrow",CaptionsKey="Assembly.Captions";
        public static event Action Changed;
        static bool loaded,hints,arrow;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset(){loaded=false;Changed=null;}
        static void Load(){if(loaded)return;loaded=true;hints=PlayerPrefs.GetInt(HintsKey,1)!=0;arrow=PlayerPrefs.GetInt(ArrowKey,0)!=0;}
        public static bool Hints {get{Load();return hints;}}
        public static bool Arrow {get{Load();return arrow;}}
        public static bool Captions=>PlayerPrefs.GetInt(CaptionsKey,1)!=0;
        public static void SetCaptions(bool value){PlayerPrefs.SetInt(CaptionsKey,value?1:0);PlayerPrefs.Save();Changed?.Invoke();}
        public static void SetHints(bool value){Load();if(hints!=value)WorkshopPractice.Record("hints");hints=value;PlayerPrefs.SetInt(HintsKey,value?1:0);PlayerPrefs.Save();Changed?.Invoke();}
        public static void SetArrow(bool value){Load();if(arrow!=value)WorkshopPractice.Record("arrow");arrow=value;PlayerPrefs.SetInt(ArrowKey,value?1:0);PlayerPrefs.Save();Changed?.Invoke();}
    }
}
