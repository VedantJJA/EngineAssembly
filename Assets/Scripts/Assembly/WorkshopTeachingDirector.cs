using UnityEngine;
namespace EngineAssembly
{
    [DefaultExecutionOrder(-50),DisallowMultipleComponent]
    public sealed class WorkshopTeachingDirector:MonoBehaviour
    {
        public static bool VerificationEnabled {get;set;}
        public string LastLessonId {get;private set;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset()=>VerificationEnabled=false;
        public static void Ensure(GameObject owner){if(!owner.GetComponent<WorkshopTeachingDirector>())owner.AddComponent<WorkshopTeachingDirector>();}
        void Update()=>Tick();
        public void Tick()
        {
            if(Application.isBatchMode && !VerificationEnabled || !WorkshopPractice.IsAssemblyExperience(gameObject))return;
            var practice=GetComponent<WorkshopPractice>();if(!practice || !practice.Active)return;
            var active=WorkshopTutorial.Active;
            if(active && active.Owner==gameObject && active.Platform!=WorkshopTutorial.DetectPlatform(gameObject))
            {active.Close(false);practice.OpenGuide();}
            LastLessonId=active?active.CurrentLessonId:null;
        }
        public string NeededLesson(string platform)=>null;
    }
}
