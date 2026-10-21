using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
namespace EngineAssembly
{
    [DisallowMultipleComponent,RequireComponent(typeof(AssemblyManager))]
    public sealed class WorkpieceLift : MonoBehaviour
    {
        sealed class Station
        {
            public Transform frame,root,foot,deck,column,rod,panel,leftDoor,rightDoor,pit,tray;
            public string label;public float ceiling,opening,aperture,depth;public Vector3[] support;public Vector2 traySize;public WorkshopSupplyLayout.Surface traySurface;public AssemblyPart[] trayParts;public bool gateBusy;public Vector3 parked,anchor;
            public LiftControlHandle lever,flip;
        }
        [SerializeField] float speed=.75f,groundHeight=0;
        readonly List<Station> stations=new List<Station>();readonly List<Material> owned=new List<Material>();
        AssemblyManager manager;Material white,grey,steel,chrome,black,red,yellow;int selected,visit=-1;float roomCeiling=float.PositiveInfinity;
        public Transform ActiveFrame{get{RefreshStations();return stations.Count>0?stations[selected].frame:null;}}
        public string ActiveLabel{get{RefreshStations();return stations.Count>0?stations[selected].label:"Engine";}}
        public int StationCount{get{RefreshStations();return stations.Count;}}
        public float GroundHeight=>groundHeight;
        public Transform StationFrame(int index){RefreshStations();return stations[index].frame;}
        public Transform StationBase(int index){RefreshStations();return stations[index].foot;}
        public Transform StationPanel(int index){RefreshStations();return stations[index].panel;}
        public Bounds StationOpeningBounds(int index){RefreshStations();var s=stations[index];return new Bounds(s.anchor,new Vector3(s.aperture,.2f,s.depth));}
        public Transform StationDeck(int index){RefreshStations();return stations[index].deck;}
        public int SelectedStation=>selected;
        public Color StationColor(int index){RefreshStations();return stations[index].foot.GetComponent<Renderer>().sharedMaterial.color;}
        public LiftControlHandle StationLever(int index){RefreshStations();return stations[index].lever;}
        public LiftControlHandle StationFlip(int index){RefreshStations();return stations[index].flip;}
        public float GateOpening(int index){RefreshStations();return stations[index].opening;}
        public bool IsTransitioning=>stations.Any(s=>s.gateBusy);
        void Awake()
        {
            manager=GetComponent<AssemblyManager>();var garage=gameObject.scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="Garage environment");var ceiling=garage?garage.GetComponentsInChildren<Renderer>().FirstOrDefault(r=>r.name=="Ceiling"):null;if(ceiling)roomCeiling=ceiling.bounds.min.y;
            white=Material("Active lift",new Color(.94f,.95f,.96f),.35f,.45f);grey=Material("Inactive lift",new Color(.28f,.3f,.32f),.35f,.45f);
            steel=Resources.Load<Material>("Lift/Steel") ?? Material("Grey treadplate steel",new Color(.4f,.43f,.46f),.4f,.8f);chrome=Material("Hydraulic polished rod",new Color(.65f,.72f,.76f),.82f,.95f);
            black=Material("Lift rubber",new Color(.04f,.045f,.05f),.14f);red=Material("Lift controls",new Color(.65f,.025f,.055f),.4f);yellow=Material("Safety yellow",new Color(.95f,.64f,.035f),.28f);
            foreach(var root in gameObject.scene.GetRootGameObjects())if(root.name=="Engine Stand"){foreach(var r in root.GetComponentsInChildren<Renderer>())r.enabled=false;foreach(var c in root.GetComponentsInChildren<Collider>())c.enabled=false;}
        }
        Material Material(string label,Color color,float smoothness,float metal=0){var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=label,color=color};m.SetFloat("_Smoothness",smoothness);m.SetFloat("_Metallic",metal);owned.Add(m);return m;}
        void RefreshStations()
        {
            if(!manager || manager.Recipe==null || manager.IsPreparing)return;if(stations.Count==0)AddStation(manager.AssemblyRoot,"Main engine");
            if(visit!=manager.ChapterVisit)
            {
                CancelTransitions();foreach(var old in stations.Skip(1)){old.root.gameObject.SetActive(false);Destroy(old.root.gameObject);}stations.RemoveRange(1,stations.Count-1);visit=manager.ChapterVisit;selected=0;
                foreach(var g in manager.Recipe.subAssemblies.Where(g=>manager.FindPart(g.rootPartId).ChapterId==manager.CurrentChapterId))AddStation(manager.FindPart(g.rootPartId).transform,manager.FindPart(g.rootPartId).PartDisplayName);
                ArrangeSubstations();
                int index=stations.FindIndex(s=>s.frame==manager.SuggestedWorkpieceFrame());if(index>=0)selected=index;
            }
            if(manager.RepairActive){var unit=manager.FindPart(PCRepairChapter.UnitId);if(unit && !unit.IsSnapped && !unit.IsSelected && !unit.IsBusy && !stations.Any(s=>s.frame==unit.transform)){AddStation(unit.transform,"Repair · timing idler");ArrangeSubstations();}}
            if(selected>0 && !StationAvailable(stations[selected]))selected=0;
        }
        static bool StationAvailable(Station s){var part=s.frame.GetComponent<AssemblyPart>();return !part || !part.IsSnapped && !part.IsSelected && !part.IsBusy;}
        Transform Shape(string label,Transform parent,PrimitiveType shape,Vector3 local,Vector3 scale,Material material,bool collider=false)
        {var go=GameObject.CreatePrimitive(shape);go.name=label;go.transform.SetParent(parent,false);go.transform.localPosition=local;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;if(!collider)Destroy(go.GetComponent<Collider>());return go.transform;}
        void AddStation(Transform frame,string label)
        {
            var root=new GameObject("Lift · "+label).transform;root.SetParent(transform,false);var s=new Station{frame=frame,root=root,label=label,ceiling=frame.position.y+2};int index=stations.Count;stations.Add(s);
            var b=manager.PlannedWorkpieceBounds(frame);
            // A complete-unit envelope leaves room for growth and either flipped pose.
            s.aperture=Mathf.Max(index==0?1.25f:.6f,b.size.x+.36f);s.depth=Mathf.Max(index==0?1.25f:.6f,b.size.z+.36f);
            if(index>0)s.support=Corners(b).Select(frame.InverseTransformPoint).ToArray();
            // Keep the main deck centered on the block; later accessories must not
            // shift the hydraulic column away from its fixed floor base.
            var center=index==0?manager.WorkpieceBounds(frame).center:b.center;
            s.anchor=new Vector3(center.x,groundHeight,center.z);
            var size=new Vector3(s.aperture+.22f,.06f,s.depth+.22f);
            s.foot=Shape("Fixed-height lift base",root,PrimitiveType.Cube,Vector3.zero,size,grey);s.foot.position=s.anchor+Vector3.up*.03f;
            s.pit=Shape("Black flip recess",root,PrimitiveType.Cube,Vector3.zero,new Vector3(s.aperture,.004f,s.depth),Resources.Load<Material>("Lift/Void") ?? black);s.pit.position=s.anchor+Vector3.up*.063f;
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2){Shape("Anchor bolt",s.foot,PrimitiveType.Cylinder,new Vector3(x*.485f,.65f,z*.485f),new Vector3(.022f,.14f,.022f),chrome);Shape("Rubber mounting foot",s.foot,PrimitiveType.Cube,new Vector3(x*.39f,-.24f,z*.38f),new Vector3(.16f,.2f,.18f),black);}
            s.leftDoor=Hatch(s,-1);s.rightDoor=Hatch(s,1);
            for(int i=0;i<8;i++)Shape("Hatch hazard marking",s.foot,PrimitiveType.Cube,new Vector3(-.35f+i*.1f,.52f,-.48f),new Vector3(.055f,.02f,.02f),i%2==0?yellow:black);
            var deckSize=index==0?new Vector2(1.05f,1.1f):new Vector2(s.aperture-.24f,s.depth-.24f);
            s.deck=Shape("Lift deck",root,PrimitiveType.Cube,Vector3.zero,new Vector3(deckSize.x,.045f,deckSize.y),steel);s.column=Shape("Hydraulic outer sleeve",root,PrimitiveType.Cylinder,Vector3.zero,new Vector3(.16f,.25f,.16f),grey);s.rod=Shape("Hydraulic polished piston",root,PrimitiveType.Cylinder,Vector3.zero,new Vector3(.085f,.4f,.085f),chrome);
            Shape("Hydraulic collar",s.column,PrimitiveType.Cylinder,new Vector3(0,.85f,0),new Vector3(1.2f,.13f,1.2f),steel);for(int z=-1;z<=1;z+=2)Shape("Deck reinforcement",s.deck,PrimitiveType.Cube,new Vector3(0,-.8f,z*.28f),new Vector3(.86f,.45f,.11f),grey);
            s.panel=new GameObject("Fixed control panel · "+label).transform;s.panel.SetParent(root,false);s.panel.localScale=Vector3.one*.72f;
            Shape("Control housing",s.panel,PrimitiveType.Cube,Vector3.zero,new Vector3(.5f,.28f,.11f),steel,true);Shape("Control face",s.panel,PrimitiveType.Cube,new Vector3(0,0,-.062f),new Vector3(.46f,.24f,.017f),grey);Shape("Lever travel slot",s.panel,PrimitiveType.Cube,new Vector3(-.13f,0,-.075f),new Vector3(.035f,.16f,.012f),black);
            var pivot=new GameObject("Spring-centered joystick pivot").transform;pivot.SetParent(s.panel,false);pivot.localPosition=new Vector3(-.13f,0,-.08f);Shape("Lever shaft",pivot,PrimitiveType.Cube,new Vector3(0,0,-.065f),new Vector3(.026f,.026f,.13f),chrome,true);
            var grip=Shape("Red joystick grip",pivot,PrimitiveType.Sphere,new Vector3(0,0,-.145f),Vector3.one*.078f,red,true);s.lever=pivot.gameObject.AddComponent<LiftControlHandle>();s.lever.Configure(this,index,LiftControlHandle.ControlKind.Lever,pivot,grip.GetComponent<Collider>());
            // The narrow CAD-scale shaft is difficult to target from a walking camera.
            // Keep the visual size but give the physical lever a generous, invisible grab area.
            var grab=new GameObject("Lever grab area");grab.transform.SetParent(pivot,false);grab.transform.localPosition=new Vector3(0,0,-.13f);
            var grabCollider=grab.AddComponent<BoxCollider>();grabCollider.size=new Vector3(.16f,.26f,.13f);
            s.lever.RefreshColliders();
            var button=Shape("Flip push button",s.panel,PrimitiveType.Cylinder,new Vector3(.14f,0,-.08f),new Vector3(.11f,.017f,.11f),red,true);button.localRotation=Quaternion.Euler(90,0,0);
            // The collider is expressed in the button's small visual mesh scale.
            var buttonArea=button.gameObject.AddComponent<BoxCollider>();buttonArea.size=new Vector3(1f,2f,1f);
            s.flip=button.gameObject.AddComponent<LiftControlHandle>();s.flip.Configure(this,index,LiftControlHandle.ControlKind.Flip,null,buttonArea);
            Text(s.panel,"UP",new Vector3(-.18f,.10f,-.079f),.023f);Text(s.panel,"DOWN",new Vector3(-.18f,-.10f,-.079f),.023f);Text(s.panel,"FLIP",new Vector3(.14f,0,-.115f),.028f);
            PositionHardware(s,index);
        }
        Transform Hatch(Station s,int side)
        {
            var hinge=new GameObject(side<0?"Left inward hatch hinge":"Right inward hatch hinge").transform;hinge.SetParent(s.root,false);
            Shape("Textured hatch door",hinge,PrimitiveType.Cube,new Vector3(-side*s.aperture*.25f,0,0),new Vector3(s.aperture*.5f-.006f,.018f,s.depth-.012f),steel);
            return hinge;
        }
        void PositionHardware(Station s,int index)
        {
            s.foot.position=s.anchor+Vector3.up*.03f;s.pit.position=s.anchor+Vector3.up*.063f;
            s.leftDoor.position=s.anchor+new Vector3(-s.aperture*.5f,.084f,0);s.rightDoor.position=s.anchor+new Vector3(s.aperture*.5f,.084f,0);
            // Controls face the exterior walking aisle, beyond all hatch travel.
            float edge=s.foot.localScale.x*.5f;
            s.panel.SetPositionAndRotation(s.anchor+(index==0?new Vector3(-edge-.36f,.98f,-s.foot.localScale.z*.5f+.12f):new Vector3(edge+.36f,.98f,0)),Quaternion.Euler(18,index==0?0:-90,0));
            var post=Shape("Fixed control pedestal",s.root,PrimitiveType.Cube,Vector3.zero,new Vector3(.045f,.86f,.045f),steel);post.position=new Vector3(s.panel.position.x,groundHeight+.43f,s.panel.position.z);
            var shoe=Shape("Control pedestal foot",s.root,PrimitiveType.Cube,Vector3.zero,new Vector3(.22f,.025f,.22f),steel);shoe.position=new Vector3(s.panel.position.x,groundHeight+.0125f,s.panel.position.z);
        }
        void ArrangeSubstations()
        {
            if(stations.Count<2)return;
            var supplies=manager.GetComponent<WorkshopSupplyLayout>();supplies?.ClearLocalSurfaces();
            foreach(var s in stations.Skip(1))
            {
                var group=manager.Recipe.subAssemblies.FirstOrDefault(g=>manager.FindPart(g.rootPartId).transform==s.frame);
                s.traySize=supplies && group!=null?supplies.MeasureSubassembly(manager,group,Mathf.Max(1.1f,s.foot.localScale.x)):Vector2.zero;
            }
            float total=stations.Skip(1).Sum(s=>s.foot.localScale.z+s.traySize.y+.4f)-.4f;
            float end=stations[0].anchor.z+total*.5f;
            if(supplies && supplies.Surfaces.Count>0)end=Mathf.Min(end,supplies.Surfaces.Min(s=>s.center.z-s.size.y*.5f)-.25f);
            float cursor=end-total;
            foreach(var s in stations.Skip(1))
            {
                var b=manager.PlannedWorkpieceBounds(s.frame);float half=Mathf.Max(s.foot.localScale.x,s.traySize.x)*.5f;
                var center=new Vector3(stations[0].anchor.x+stations[0].foot.localScale.x*.5f+.85f+half,b.center.y,cursor+s.foot.localScale.z*.5f);
                // Installed preview/disassembly carriers must remain at their sockets.
                if(StationAvailable(s)){s.frame.position+=center-b.center+Vector3.up*(.92f-b.min.y);s.ceiling=s.frame.position.y+2;}
                s.anchor=new Vector3(center.x,groundHeight,center.z);
                var offset=s.anchor+Vector3.up*.03f-s.foot.position;s.root.position+=offset;
                if(supplies && s.traySize.y>0 && StationAvailable(s))
                {
                    var surface=new WorkshopSupplyLayout.Surface{center=new Vector3(center.x,.9f,s.anchor.z+s.foot.localScale.z*.5f+.12f+s.traySize.y*.5f),size=s.traySize};
                    s.tray=new GameObject("Subassembly parts table").transform;s.tray.SetParent(s.root,false);s.traySurface=surface;
                    var table=Shape("Nearby subassembly supply tray",s.tray,PrimitiveType.Cube,Vector3.zero,new Vector3(surface.size.x,.05f,surface.size.y),steel,true);table.position=surface.center-Vector3.up*.025f;
                    for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2){var leg=Shape("Supply tray leg",s.tray,PrimitiveType.Cube,Vector3.zero,new Vector3(.11f,.85f,.11f),grey,true);leg.position=surface.center+new Vector3(x*(surface.size.x*.5f-.09f),-.475f,z*(surface.size.y*.5f-.09f));}
                    var group=manager.Recipe.subAssemblies.First(g=>manager.FindPart(g.rootPartId).transform==s.frame);supplies.StageSubassembly(manager,group,surface);
                    s.trayParts=manager.Members(group).ToArray();
                }
                cursor+=s.foot.localScale.z+s.traySize.y+.4f;
            }
        }
        static IEnumerable<Vector3> Corners(Bounds b){for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)yield return b.center+Vector3.Scale(b.extents,new Vector3(x,y,z));}
        Bounds SupportBounds(Station s)
        {
            if(s.support==null)return manager.WorkpieceBounds(s.frame);
            var b=new Bounds(s.frame.TransformPoint(s.support[0]),Vector3.zero);foreach(var p in s.support)b.Encapsulate(s.frame.TransformPoint(p));return b;
        }

        void Text(Transform parent,string text,Vector3 position,float size){var go=new GameObject(text,typeof(TextMeshPro));go.transform.SetParent(parent,false);go.transform.localPosition=position;var t=go.GetComponent<TextMeshPro>();t.font=Resources.Load<TMP_FontAsset>("WorkshopHeading");t.fontSize=size*10;t.text=text;t.color=Color.white;t.rectTransform.sizeDelta=new Vector2(.18f,.06f);t.alignment=TextAlignmentOptions.Center;t.textWrappingMode=TextWrappingModes.NoWrap;}
        public bool Select(int index){RefreshStations();if(index<0 || index>=stations.Count || index>0 && !StationAvailable(stations[index]))return false;selected=index;manager.Report("Lift selected: "+stations[index].label);return true;}
        public void Cycle(int direction){RefreshStations();for(int i=1;i<=stations.Count;i++){int next=(selected+direction*i+stations.Count*2)%stations.Count;if(Select(next))break;}}
        public bool Move(float metres)
        {
            RefreshStations();return MoveStation(selected,metres);
        }
        public bool CanOperate(int index){RefreshStations();return index>=0 && index<stations.Count && stations[index].root.gameObject.activeInHierarchy && StationAvailable(stations[index]) && !stations[index].gateBusy;}
        public bool MoveStation(int index,float metres)
        {
            if(Mathf.Approximately(metres,0) || !CanOperate(index))return false;var s=stations[index];var frame=s.frame;var platform=frame.GetComponent<PickupablePlatform>();if(platform && (platform.IsFlipping || platform.IsHeld))return false;
            var b=SupportBounds(s);float floor=frame.position.y+groundHeight+.16f-b.min.y;float top=Mathf.Max(floor,Mathf.Min(s.ceiling,frame.position.y+roomCeiling-b.max.y-.08f));float next=Mathf.Clamp(frame.position.y+metres,floor,top);float delta=next-frame.position.y;if(Mathf.Abs(delta)<.00001f)return false;frame.position+=Vector3.up*delta;return true;
        }
        public void MoveFromInput(float direction){if(!Mathf.Approximately(direction,0))Move(direction*speed*Time.deltaTime);}
        public void MoveStationFromInput(int index,float direction){if(!Mathf.Approximately(direction,0))MoveStation(index,direction*speed*Time.deltaTime);}
        public bool FlipActive()
        {
            RefreshStations();return FlipStation(selected);
        }
        public bool FlipStation(int index)
        {
            if(!CanOperate(index) || manager.AssemblyParts.AnyBusy())return false;var s=stations[index];var platform=s.frame.GetComponent<PickupablePlatform>();if(!platform || platform.IsFlipping || platform.IsHeld)return false;s.lever.Release();s.gateBusy=true;s.parked=manager.WorkpieceBounds(s.frame).center;StartCoroutine(Flip(s,platform));return true;
        }
        IEnumerator Flip(Station s,PickupablePlatform platform)
        {
            var visible=manager.WorkpieceBounds(s.frame);var support=SupportBounds(s);var turn=Quaternion.AngleAxis(180,s.frame.right);
            float turnedBottom=Corners(support).Min(p=>(visible.center+turn*(p-visible.center)).y);
            float returnOffset=support.min.y-turnedBottom;
            float start=s.opening;for(float t=0;t<.35f;t+=Time.deltaTime){s.opening=Mathf.Lerp(start,1,Mathf.SmoothStep(0,1,t/.35f));yield return null;}s.opening=1;if(platform && platform.FlipThroughGround(visible,groundHeight,returnOffset))while(platform && platform.IsFlipping)yield return null;
            float parked=0;for(float t=0;t<.35f;t+=Time.deltaTime){s.opening=Mathf.Lerp(1,parked,Mathf.SmoothStep(0,1,t/.35f));yield return null;}s.opening=parked;s.gateBusy=false;
        }
        public void CancelTransitions(){StopAllCoroutines();foreach(var s in stations){if(s.frame)s.frame.GetComponent<PickupablePlatform>()?.CancelFlip();s.gateBusy=false;s.opening=0;s.lever?.Release();}}
        void LateUpdate()
        {
            RefreshStations();for(int i=0;i<stations.Count;i++)
            {
                var s=stations[i];bool available=i==0 || StationAvailable(s);s.root.gameObject.SetActive(available);if(!available)continue;
                if(s.tray)s.tray.gameObject.SetActive(s.trayParts!=null && s.trayParts.Any(p=>PartOnTray(p,s.traySurface)));
                var b=SupportBounds(s);if(!s.gateBusy && b.min.y<groundHeight+.16f){s.frame.position+=Vector3.up*(groundHeight+.16f-b.min.y);b=SupportBounds(s);}
                var center=new Vector3(s.anchor.x,b.center.y,s.anchor.z);var paint=i==selected?white:grey;s.foot.GetComponent<Renderer>().sharedMaterial=paint;s.column.GetComponent<Renderer>().sharedMaterial=paint;
                // Ordinary travel stops above the closed hatch; only an explicit
                // flip can lower the assembly through the opened doors.
                if(!s.gateBusy)s.opening=0;
                s.leftDoor.localRotation=Quaternion.Euler(0,0,-90*s.opening);s.rightDoor.localRotation=Quaternion.Euler(0,0,90*s.opening);float top=b.min.y-.025f;float height=Mathf.Max(0,top-groundHeight);
                s.deck.position=new Vector3(center.x,top,center.z);s.deck.gameObject.SetActive(top>groundHeight+.075f);s.column.position=new Vector3(center.x,groundHeight+Mathf.Min(height*.5f,.22f),center.z);s.column.localScale=new Vector3(.16f,Mathf.Max(.001f,Mathf.Min(height*.5f,.22f)),.16f);s.column.gameObject.SetActive(height>.075f);s.rod.position=new Vector3(center.x,groundHeight+height*.5f,center.z);s.rod.localScale=new Vector3(.085f,Mathf.Max(.001f,height*.5f),.085f);s.rod.gameObject.SetActive(height>.075f);
            }
        }
        static bool PartOnTray(AssemblyPart part,WorkshopSupplyLayout.Surface tray)
        {
            if(!part || !part.gameObject.activeInHierarchy || part.IsSnapped || part.IsSelected)return false;
            var b=AssemblyHeldView.WorldBounds(part);
            return b.center.x>=tray.center.x-tray.size.x*.5f && b.center.x<=tray.center.x+tray.size.x*.5f
                && b.center.z>=tray.center.z-tray.size.y*.5f && b.center.z<=tray.center.z+tray.size.y*.5f
                && Mathf.Abs(b.min.y-tray.center.y-.025f)<.12f;
        }
        void OnDestroy(){foreach(var m in owned)if(m)Destroy(m);}
    }
}
