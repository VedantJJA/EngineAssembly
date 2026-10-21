using System.Collections;
using UnityEngine;

namespace EngineAssembly
{
    [RequireComponent(typeof(AssemblyManager))]
    public sealed class RepairVRFallback : MonoBehaviour
    {
        public bool Ready { get; private set; }
        IEnumerator Start()
        {
            var manager=GetComponent<AssemblyManager>();
            while(manager.Recipe==null)yield return null;
            if(!GetComponent<PCRepairChapter>())gameObject.AddComponent<PCRepairChapter>();
            var ui=GetComponent<AssemblyWorkshopUI>();
            while(!ui){yield return null;ui=GetComponent<AssemblyWorkshopUI>();}
            SpatialExperienceRig.Ensure(gameObject,manager.AssemblyRoot,Camera.main);
            manager.SetMode(WorkshopMode.Assembly);manager.SetDifficulty(SnapDifficulty.Easy,false);
            int index=manager.Recipe.chapters.FindIndex(c=>c.id==PCRepairChapter.ChapterId);
            if(index<0){manager.Report("Repair recipe unavailable.");yield break;}
            manager.SelectChapter(index);ui.SetOpen(false);Ready=true;
            manager.Report(QuestVRSession.Instance && QuestVRSession.Instance.Active?"Repair workspace · grip to pick up, trigger to fit, release grip to set aside. Replace the timing idler bearing.":"Repair workspace · desktop fallback. Replace the timing idler bearing.");
        }
    }
}
