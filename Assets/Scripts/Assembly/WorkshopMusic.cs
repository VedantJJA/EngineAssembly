using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace EngineAssembly
{
    public sealed class WorkshopMusic : MonoBehaviour
    {
        static WorkshopMusic instance;
        AudioSource source;AssemblyNarrator[] narrators;
        public AudioSource Source=>source;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Create()
        {
            if(instance)return;
            var clip=Resources.Load<AudioClip>("Music/Contemplation");if(!clip)return;
            instance=new GameObject("Workshop background music").AddComponent<WorkshopMusic>();DontDestroyOnLoad(instance.gameObject);
            instance.source=instance.gameObject.AddComponent<AudioSource>();instance.source.clip=clip;instance.source.loop=true;instance.source.spatialBlend=0;instance.source.volume=.12f;instance.source.priority=200;instance.source.Play();
            SceneManager.sceneLoaded+=instance.SceneChanged;
        }
        void SceneChanged(Scene scene,LoadSceneMode mode)=>narrators=FindObjectsByType<AssemblyNarrator>(FindObjectsSortMode.None);
        void Update()
        {
            bool speaking=(narrators!=null && narrators.Any(n=>n && n.Source && n.Source.isPlaying)) || FindObjectsByType<NarrationSpeech>(FindObjectsSortMode.None).Any(n=>n.Source && n.Source.isPlaying);
            source.volume=Mathf.MoveTowards(source.volume,speaking?.025f:.12f,Time.unscaledDeltaTime*.25f);
        }
        void OnDestroy(){SceneManager.sceneLoaded-=SceneChanged;if(instance==this)instance=null;}
    }
}
