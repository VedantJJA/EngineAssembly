using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EngineAssembly
{
    // A reversible teaching animation driven by the source CAD's measured joint positions.
    // It does not alter mesh assets or pretend to be a calibrated engine dynamics solver.
    public sealed class EngineOperatingAnimation : MonoBehaviour
    {
        sealed class Node
        {
            public string id;public Transform transform;public MeshFilter mesh;public Renderer renderer;
            public bool visible;
            public Vector3 localPosition,localScale,rootScale,position,center;public Quaternion localRotation,rotation;
        }
        sealed class Cylinder
        {
            public int index;public Vector3 pin,big;public float length,throwRadius,phase,crownOffset,bore;
            public Node piston,rod;public Node[] reciprocating,rodParts;
            public Transform gas;public Renderer gasRenderer;public MaterialPropertyBlock gasBlock=new MaterialPropertyBlock();
        }
        sealed class Valve
        {
            public Node stem,cam;public Node[] followers;public Node spring;public int cylinder;public bool intake;
            public Vector3 direction,tip,tipMesh,springAnchor,springAnchorMesh;public float travel,lobePhase,tappetLength,springLength;public int springAxis;
            public Quaternion springRotation;
            public EngineCamContact contact;public Transform tappet;
        }
        Transform root;AssemblyRecipe recipe;Camera viewer;EngineSectionView section;
        readonly Dictionary<string,Node> nodes=new Dictionary<string,Node>();
        readonly List<Cylinder> cylinders=new List<Cylinder>();readonly List<Valve> valves=new List<Valve>();
        readonly List<Action<float>> rotations=new List<Action<float>>();
        readonly Dictionary<Node,float> camPhase=new Dictionary<Node,float>();readonly Dictionary<Node,Vector3> camLobePhases=new Dictionary<Node,Vector3>();readonly HashSet<Node> moving=new HashSet<Node>();
        Renderer[] shells;Node[] fixedFasteners,hiddenInMotion,sectionPipes;Vector3 crank;float roof;
        public bool Active {get;private set;}
        public bool Paused {get;set;}
        public bool SectionEnabled {get;set;}=true;
        public float TeachingRPM {get;private set;}=12;
        public float CrankAngle {get;private set;}
        public float CamAngle=>CrankAngle*.5f;
        public int MovingCylinderCount=>cylinders.Count;
        public int MovingValveCount=>valves.Count;
        public float MaximumCamContactError=>!Active?0:valves.Max(v=>
            Mathf.Abs(Vector3.Dot(root.InverseTransformPoint(v.tappet.TransformPoint(Vector3.down))-v.cam.position,v.direction)-v.contact.Reach(CamAngle+camPhase[v.cam]+v.lobePhase))
            +Vector3.Distance(v.tappet.TransformPoint(Vector3.up),v.stem.transform.TransformPoint(v.tipMesh)));
        public void SetSpeed(float rpm)=>TeachingRPM=Mathf.Clamp(rpm,4,30);
        public void SetCamera(Camera camera)=>viewer=camera;
        public void Configure(Transform engine,AssemblyRecipe data,Camera camera,EngineSectionView treatment=null)
        {root=engine;recipe=data;viewer=camera;section=treatment?treatment:gameObject.GetComponent<EngineSectionView>()??gameObject.AddComponent<EngineSectionView>();}
        public void Play()
        {
            if(Active){Paused=false;return;}
            Cache();Active=true;Paused=false;CrankAngle=0;SetCrankAngle(0);
        }
        void Cache()
        {
            nodes.Clear();cylinders.Clear();valves.Clear();rotations.Clear();moving.Clear();camPhase.Clear();camLobePhases.Clear();
            foreach(var mesh in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!mesh.sharedMesh || !recipe.parts.Any(p=>p.id==mesh.name) || !mesh.TryGetComponent<Renderer>(out var renderer))continue;
                var t=mesh.transform;nodes[mesh.name]=new Node{id=mesh.name,transform=t,mesh=mesh,renderer=renderer,visible=renderer.enabled,localPosition=t.localPosition,localRotation=t.localRotation,localScale=t.localScale,
                    rootScale=new Vector3(t.lossyScale.x/root.lossyScale.x,t.lossyScale.y/root.lossyScale.y,t.lossyScale.z/root.lossyScale.z),position=root.InverseTransformPoint(t.position),rotation=Quaternion.Inverse(root.rotation)*t.rotation,center=root.InverseTransformPoint(t.TransformPoint(mesh.sharedMesh.bounds.center))};
            }
            crank=nodes["103_Crankshaft_01"].position;
            for(int i=0;i<3;i++)
            {
                string tag="Cyl"+(i+1).ToString("00");var piston=nodes.Values.First(n=>n.id.StartsWith("201_Piston_"+tag));var rod=nodes.Values.First(n=>n.id.StartsWith("202_Connecting_Rod_"+tag));
                var pin=nodes.Values.First(n=>n.id.StartsWith("203_Piston_Wrist_Pin_"+tag)).center;
                var big=new Vector3(rod.position.x,rod.position.y,pin.z);var radial=big-new Vector3(crank.x,crank.y,pin.z);
                var group=recipe.parts.First(p=>p.id==piston.id).subAssemblyId;
                var c=new Cylinder{index=i,piston=piston,rod=rod,pin=pin,big=big,length=Vector3.Distance(pin,big),throwRadius=radial.magnitude,phase=Mathf.Atan2(-radial.x,radial.y)*Mathf.Rad2Deg};
                var matrix=root.worldToLocalMatrix*piston.transform.localToWorldMatrix;var bounds=piston.mesh.sharedMesh.bounds;
                var points=Enumerable.Range(0,8).Select(k=>matrix.MultiplyPoint3x4(bounds.center+Vector3.Scale(bounds.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1)))).ToArray();
                c.crownOffset=points.Max(p=>p.y)-pin.y;c.bore=Mathf.Max(points.Max(p=>p.x)-points.Min(p=>p.x),.02f);
                c.reciprocating=nodes.Values.Where(n=>recipe.parts.First(p=>p.id==n.id).subAssemblyId==group && !n.id.StartsWith("202_") && !n.id.Contains("Stud_")).ToArray();
                c.rodParts=nodes.Values.Where(n=>n==rod || n.id.Contains(tag) && (n.id.Contains("Stud_") || n.id.StartsWith("206_") || n.id.Contains("Nut_JIS_B_1170"))).ToArray();
                cylinders.Add(c);foreach(var n in c.reciprocating.Concat(c.rodParts))moving.Add(n);
            }
            roof=cylinders.Max(c=>crank.y+c.throwRadius+c.length+c.crownOffset)+.025f;
            foreach(var stem in nodes.Values.Where(n=>n.id.StartsWith("404_") && n.id.Contains("Valve_")))
            {
                var definition=recipe.parts.First(p=>p.id==stem.id);string station=Station(definition.displayName);
                var related=nodes.Values.Where(n=>Station(recipe.parts.First(p=>p.id==n.id).displayName)==station).ToArray();
                var size=Vector3.Scale(stem.mesh.sharedMesh.bounds.size,stem.localScale);int axis=0;for(int a=1;a<3;a++)if(size[a]>size[axis])axis=a;
                var direction=stem.rotation*(axis==0?Vector3.right:axis==1?Vector3.up:Vector3.forward);if(direction.y>0)direction=-direction;
                var spring=related.FirstOrDefault(n=>n.id.StartsWith("405_"));int springAxis=2;
                // The valve's axis defines compression, not the longest mesh bound.
                // Use the root frame so nested PC subassemblies and the flat phone
                // presentation give the same seat position and spring travel.
                if(spring!=null){var localDirection=Quaternion.Inverse(spring.rotation)*direction;springAxis=0;for(int a=1;a<3;a++)if(Mathf.Abs(localDirection[a])>Mathf.Abs(localDirection[springAxis]))springAxis=a;}
                bool intake=stem.id.Contains("Intake");var cam=nodes.Values.First(n=>n.id.StartsWith(intake?"502_Intake":"502_Exhaust"));
                var points=stem.mesh.sharedMesh.vertices.Select(p=>root.InverseTransformPoint(stem.transform.TransformPoint(p))).ToArray();float end=points.Max(p=>Vector3.Dot(p,-direction));var tips=points.Where(p=>end-Vector3.Dot(p,-direction)<.0001f).ToArray();var tip=tips.Aggregate(Vector3.zero,(a,p)=>a+p)/tips.Length;
                var camPoints=cam.mesh.sharedMesh.vertices.Select(p=>root.InverseTransformPoint(cam.transform.TransformPoint(p))).ToArray();
                var contact=new EngineCamContact(camPoints,cam.position,tip,direction);
                var valve=new Valve{stem=stem,cam=cam,cylinder=Array.FindIndex(new[]{"Cyl01","Cyl02","Cyl03"},stem.id.Contains),intake=intake,direction=direction.normalized,tip=tip,contact=contact,travel=contact.MaximumReach-contact.BaseReach,
                    tipMesh=stem.transform.InverseTransformPoint(root.TransformPoint(tip)),
                    followers=related.Where(n=>n.id.StartsWith("406_") || n.id.StartsWith("407_")).ToArray(),spring=spring,springAxis=springAxis,tappetLength=Vector3.Dot(tip-cam.position,direction)-contact.BaseReach};
                if(spring!=null)
                {
                    var bounds=spring.mesh.sharedMesh.bounds;var anchor=bounds.center;var axisVector=Vector3.zero;axisVector[springAxis]=1;anchor[springAxis]=Vector3.Dot(spring.rotation*axisVector,direction)>0?bounds.max[springAxis]:bounds.min[springAxis];
                    valve.springAnchorMesh=anchor;valve.springAnchor=root.InverseTransformPoint(spring.transform.TransformPoint(anchor));valve.springLength=bounds.size[springAxis]*Mathf.Abs(spring.rootScale[springAxis]);
                    var springDirection=spring.rotation*axisVector;if(Vector3.Dot(springDirection,direction)<0)springDirection=-springDirection;
                    // The CAD exhaust springs are tilted slightly away from their
                    // valve stems. Align the operating pose about the fixed seat.
                    valve.springRotation=Quaternion.FromToRotation(springDirection,direction)*spring.rotation;
                }
                valves.Add(valve);moving.Add(stem);foreach(var follower in valve.followers)moving.Add(follower);if(spring!=null)moving.Add(spring);
            }
            foreach(var cam in valves.Select(v=>v.cam).Distinct())
            {
                var first=valves.First(v=>v.cam==cam && v.cylinder==0);camPhase[cam]=Mathf.DeltaAngle(0,first.contact.PeakAngle-(first.intake?450:270)*.5f);var phases=Vector3.zero;
                for(int i=0;i<3;i++){var valve=valves.First(v=>v.cam==cam && v.cylinder==i);float cycleOffset=cylinders[i].phase-(i==0?0:360);float desired=Mathf.Repeat((valve.intake?450:270)-cycleOffset,720)*.5f;phases[i]=Mathf.DeltaAngle(0,valve.contact.PeakAngle-desired-camPhase[cam]);}
                camLobePhases[cam]=phases;foreach(var valve in valves.Where(v=>v.cam==cam))valve.lobePhase=phases[valve.cylinder];
            }
            var followerMaterial=Resources.Load<EngineSurfaceLibrary>("EngineSurfaceLibrary")?.Find("Machined steel");
            foreach(var valve in valves)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=Station(recipe.parts.First(p=>p.id==valve.stem.id).displayName)+" display tappet";Destroy(go.GetComponent<Collider>());go.transform.SetParent(root,false);go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,valve.direction);go.transform.localScale=new Vector3(.028f,Mathf.Max(.005f,valve.tappetLength)*.5f,.028f);valve.tappet=go.transform;
                if(followerMaterial)go.GetComponent<Renderer>().sharedMaterial=followerMaterial;
            }
            AddRotation(nodes["103_Crankshaft_01"],crank,1);
            foreach(var n in nodes.Values)
            {
                if(n.id.StartsWith("305_") || n.id.StartsWith("306_") || n.id.StartsWith("505_") || n.id.StartsWith("506_Crankshaft") || n.id.StartsWith("507_Crankshaft") || n.id.StartsWith("590_Nut_ISO_4035_M22") || n.id.StartsWith("390_Bolt_ISO_4017"))AddRotation(n,crank,1);
                else if(n.id.StartsWith("502_") || n.id.StartsWith("503_") || n.id.StartsWith("504_") || n.id.StartsWith("506_Camshaft") || n.id.StartsWith("507_Camshaft") || n.id.StartsWith("590_Bolt_ISO_4162_M12") || n.id.StartsWith("590_Bolt_ISO_4162_M8_x_35_"))
                {
                    var cam=nodes.Values.Where(m=>m.id.StartsWith("502_")).OrderBy(m=>Mathf.Abs(m.position.x-n.position.x)).First();AddRotation(n,cam.position,.5f,Vector3.forward,camPhase[cam]);
                }
                else if(n.id.Contains("Timing_Idler_Pulley") || n.id.Contains("Accessory_Idler_Pulley") || n.id.StartsWith("905_"))AddRotation(n,n.center,1);
                else if(n.id.StartsWith("702_") || n.id.StartsWith("704_"))
                {
                    var shaft=nodes.Values.First(m=>m.id.StartsWith("702_"));var size=Vector3.Scale(shaft.mesh.sharedMesh.bounds.size,shaft.localScale);int axis=0;for(int a=1;a<3;a++)if(size[a]>size[axis])axis=a;
                    AddRotation(n,shaft.position,3,shaft.rotation*(axis==0?Vector3.right:axis==1?Vector3.up:Vector3.forward));
                }
            }
            shells=nodes.Values.Where(n=>new[]{"101_","301_","302_","303_","304_","401_","601_","605_","701_","706_","801_","803_","901_","902_","903_","904_","908_","501_Front_"}.Any(n.id.StartsWith) || n.id.Contains("Main_Bearing_Shell") || !moving.Contains(n) && new[]{"Bolt","Screw","Nut","Washer","Circlip"}.Any(n.id.Contains)).Select(n=>n.renderer).ToArray();
            fixedFasteners=nodes.Values.Where(n=>!moving.Contains(n) && new[]{"Bolt","Screw","Nut","Washer","Circlip"}.Any(n.id.Contains)).ToArray();
            hiddenInMotion=nodes.Values.Where(n=>n.id=="705_Turbo_Outlet_Band_Clamp_01" || n.id=="790_Bolt_ISO_4762_M8_x_12_Fasteners_01").ToArray();
            sectionPipes=nodes.Values.Where(n=>n.id.StartsWith("802_Intake_Pipe_") || n.id=="803_External_Service_Tube_01").ToArray();
            var material=Resources.Load<Material>("EngineCycleGas");if(material)foreach(var c in cylinders)
            {
                var gas=GameObject.CreatePrimitive(PrimitiveType.Cylinder);gas.name="Cylinder "+(c.index+1)+" working gas";Destroy(gas.GetComponent<Collider>());gas.transform.SetParent(root,false);
                c.gas=gas.transform;c.gasRenderer=gas.GetComponent<Renderer>();c.gasRenderer.sharedMaterial=material;c.gasRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;c.gasRenderer.receiveShadows=false;
            }
        }
        void AddRotation(Node node,Vector3 pivot,float ratio)=>AddRotation(node,pivot,ratio,Vector3.forward);
        void AddRotation(Node node,Vector3 pivot,float ratio,Vector3 axis,float phase=0){moving.Add(node);rotations.Add(angle=>{var q=Quaternion.AngleAxis(angle*ratio+phase,axis);Pose(node,pivot+q*(node.position-pivot),q*node.rotation);});}
        void Pose(Node node,Vector3 position,Quaternion rotation){node.transform.SetPositionAndRotation(root.TransformPoint(position),root.rotation*rotation);}
        public float CylinderCycle(int index)=>Mathf.Repeat(CrankAngle+cylinders[index].phase-(index==0?0:360),720);
        public string StrokeName(int index){float cycle=CylinderCycle(index);return cycle<180?"Power":cycle<360?"Exhaust":cycle<540?"Intake":"Compression";}
        public string CycleSummary=>Active?string.Join("  ·  ",cylinders.Select(c=>"C"+(c.index+1)+" "+StrokeName(c.index))) : "";
        public string Explanation
        {
            get
            {
                if(!Active)return "";
                switch(StrokeName(0))
                {
                    case "Intake":return "INTAKE · Cam lobes press the tappets to open the intake valves. The descending piston draws fresh air into the cylinder. Blue shows the incoming air.";
                    case "Compression":return "COMPRESSION · Both sets of valves are closed. The rising piston squeezes the air into a smaller space. Gold shows compression.";
                    case "Power":return "POWER · Fuel burns in the hot compressed air. Expanding gas pushes the piston down, turning the crankshaft. Orange shows combustion.";
                    default:return "EXHAUST · Cam lobes press the tappets to open the exhaust valves. The rising piston pushes used gas out of the cylinder. Grey shows the exhaust gas.";
                }
            }
        }
        public void SetCrankAngle(float degrees)
        {
            if(!Active)return;CrankAngle=Mathf.Repeat(degrees,720);foreach(var rotation in rotations)rotation(CrankAngle);
            foreach(var c in cylinders)
            {
                var q=Quaternion.AngleAxis(CrankAngle,Vector3.forward);var axis=new Vector3(crank.x,crank.y,c.big.z);var big=axis+q*(c.big-axis);
                float dx=c.pin.x-big.x;var pin=new Vector3(c.pin.x,big.y+Mathf.Sqrt(Mathf.Max(.00001f,c.length*c.length-dx*dx)),c.pin.z);
                var rodRotation=Quaternion.FromToRotation(c.pin-c.big,pin-big);
                foreach(var node in c.rodParts)Pose(node,big+rodRotation*(node.position-c.big),rodRotation*node.rotation);
                foreach(var node in c.reciprocating)Pose(node,node.position+Vector3.up*(pin.y-c.pin.y),node.rotation);
                if(c.gas)
                {
                    float floor=pin.y+c.crownOffset+.002f,height=Mathf.Max(.004f,roof-floor);c.gas.localPosition=new Vector3(pin.x,(roof+floor)*.5f,pin.z);c.gas.localScale=new Vector3(c.bore*.88f,height*.5f,c.bore*.88f);
                    float phase=CylinderCycle(c.index),compressed=1-Mathf.Clamp01((height-.023f)/(c.throwRadius*2));
                    var colour=GasColour(phase,compressed);float transition=Mathf.SmoothStep(0,1,(phase%180)/12);
                    colour=Color.Lerp(GasColour(Mathf.Repeat(Mathf.Floor(phase/180)*180-.01f,720),compressed),colour,transition);
                    c.gasBlock.SetColor("_BaseColor",colour);c.gasBlock.SetFloat("_Heat",phase<180?Mathf.Sin(Mathf.Clamp01(phase/12)*Mathf.PI*.5f)*Mathf.Exp(-phase/40):0);c.gasRenderer.SetPropertyBlock(c.gasBlock);
                }
            }
            foreach(var valve in valves)
            {
                float lift=Mathf.Max(0,valve.contact.Reach(CamAngle+camPhase[valve.cam]+valve.lobePhase)-valve.contact.BaseReach);
                var offset=valve.direction*lift;Pose(valve.stem,valve.stem.position+offset,valve.stem.rotation);
                foreach(var follower in valve.followers)Pose(follower,follower.position+offset,follower.rotation);
                valve.tappet.localPosition=valve.tip+offset-valve.direction*(valve.tappetLength*.5f);
                if(valve.spring!=null)
                {
                    var spring=valve.spring;float compression=Mathf.Clamp01(lift/Mathf.Max(.000001f,valve.springLength));
                    var scale=spring.localScale;scale[valve.springAxis]*=1-compression;spring.transform.localScale=scale;
                    var rootScale=spring.rootScale;rootScale[valve.springAxis]*=1-compression;
                    Pose(spring,valve.springAnchor-valve.springRotation*Vector3.Scale(valve.springAnchorMesh,rootScale),valve.springRotation);
                }
            }
            RefreshSection();
        }
        static Color GasColour(float phase,float compressed)
        {
            if(phase<180)return Color.Lerp(new Color(1,.2f,.025f,.12f),new Color(1,.56f,.08f,.38f),compressed);
            if(phase<360)return new Color(.48f,.52f,.57f,.1f+.08f*(1-compressed));
            if(phase<540)return new Color(.12f,.52f,1,.13f);
            return Color.Lerp(new Color(.12f,.52f,1,.13f),new Color(1,.71f,.08f,.34f),compressed);
        }
        static string Station(string name)=>name.Split('·').Last().ToUpperInvariant().Replace("INTAKE VALVE ","IN").Replace("EXHAUST VALVE ","EX").Replace(" ","");
        void RefreshSection()
        {
            if(!viewer || !viewer.isActiveAndEnabled)viewer=Camera.main;var eye=viewer?root.InverseTransformPoint(viewer.transform.position):Vector3.right;
            var normal=root.TransformDirection(eye.x>=crank.x?Vector3.right:Vector3.left);var point=root.TransformPoint(new Vector3(crank.x,0,0));
            section.SetSection(shells,SectionEnabled,point,normal,root.lossyScale.magnitude*.001f);
            // Remove each fastener as a whole on the opened side. A plane through a
            // screw otherwise leaves a small unsupported stem floating at the cut.
            float side=eye.x>=crank.x?1:-1;
            foreach(var fastener in fixedFasteners){bool shown=fastener.visible && (!SectionEnabled || (fastener.center.x-crank.x)*side<-.0001f);if(fastener.renderer.enabled!=shown)fastener.renderer.enabled=shown;}
            foreach(var node in hiddenInMotion)if(node.renderer.enabled)node.renderer.enabled=false;
            // Plumbing is supported by the removed intake housing. Remove the
            // complete fittings with the cutaway, then restore them in exterior view.
            foreach(var pipe in sectionPipes){bool shown=pipe.visible && !SectionEnabled;if(pipe.renderer.enabled!=shown)pipe.renderer.enabled=shown;}
            foreach(var cam in camLobePhases.Keys)section.SetCamPose(cam.renderer,root,cam.position,(cylinders[0].pin.z+cylinders[1].pin.z)*.5f,(cylinders[1].pin.z+cylinders[2].pin.z)*.5f,camLobePhases[cam],true);
        }
        public bool IsClipped(Renderer renderer,Vector3 point)=>Active && (!renderer || !renderer.enabled || section.IsClipped(renderer,point));
        void Update(){if(Active && !Paused && Time.timeScale>0 && MobileARSession.CanInteract(gameObject))SetCrankAngle(CrankAngle+Mathf.Min(Time.unscaledDeltaTime,.1f)*TeachingRPM*6);}
        void LateUpdate(){if(Active)RefreshSection();}
        public void Stop()
        {
            if(!Active)return;Active=false;foreach(var c in cylinders)if(c.gas)Destroy(c.gas.gameObject);foreach(var valve in valves)if(valve.tappet)Destroy(valve.tappet.gameObject);
            foreach(var cam in camLobePhases.Keys)section.SetCamPose(cam.renderer,root,cam.position,0,0,Vector3.zero,false);
            foreach(var node in nodes.Values)if(node.transform){node.transform.SetLocalPositionAndRotation(node.localPosition,node.localRotation);node.transform.localScale=node.localScale;node.renderer.enabled=node.visible;}
            section.SetSection(Array.Empty<Renderer>(),false,Vector3.zero,Vector3.right,.001f);Physics.SyncTransforms();
        }
        void OnDisable()=>Stop();
        void OnDestroy()=>Stop();
    }
}
