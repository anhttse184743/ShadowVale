using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The tank smashes the bamboo where it comes out to the head of the dock road (N3_07): clumps on its way break apart as it
    /// reaches them, and the clumps in front of it where it stops go as it swings its hull to the boat, so nothing hides the escort Nam has to drop in the cover fire (25/11/2026). The fall follows the
    /// story state: before the tank comes out the bamboo stands, after it (or on a load past it) only snapped stems are left.
    /// </summary>
    public sealed partial class Map02ChapterDirector
    {
        sealed class CrushedClump
        {
            public Transform root;public Vector3 position,pivot;public Quaternion rotation;public bool onRoad;public float fall=-1;public GameObject debris;
        }
        const float CrushFrontReach=14;
        List<CrushedClump> clumps;ParticleSystem crushDust;

        public int CrushedBamboo=>clumps?.Count(c=>c.fall>=1)??0;
        public int BambooToCrush=>clumps?.Count??0;

        static bool BambooClumpName(string name)
        {
            name=name.ToLowerInvariant();return name.Contains("bamboo")&&!name.Contains("rail")&&!name.Contains("village")&&!name.Contains("house");
        }
        static Bounds ClumpBounds(Transform root)
        {
            var renderers=root.GetComponentsInChildren<Renderer>();var b=new Bounds(root.position,Vector3.zero);bool any=false;
            foreach(var r in renderers){if(!any){b=r.bounds;any=true;}else b.Encapsulate(r.bounds);}
            return b;
        }
        static bool BambooClump(Transform t)
        {
            if(BambooClumpName(t.name))return true;
            foreach(var filter in t.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh!=null&&BambooClumpName(filter.sharedMesh.name))return true;
            return false;
        }
        /// <summary>The bamboo clumps the tank drives through on its way out, and those in front of it where it stops, its hull
        /// turned to the last boat (and Nam's bank); found once.</summary>
        List<CrushedClump> FindClumpsToCrush()
        {
            var found=new List<CrushedClump>();if(!sites.ContainsKey("tankDock")||!sites.ContainsKey("tankHide"))return found;
            var dock=sites["tankDock"].position;var road=Leg("tankHide","tankDock");if(road.Length<2)road=new[]{sites["tankHide"].position,dock};
            var heading=Vector3.ProjectOnPlane(road[road.Length-1]-road[road.Length-2],Vector3.up).normalized;
            var ahead=Vector3.ProjectOnPlane(water["boatMid"]-dock,Vector3.up).normalized;if(ahead.sqrMagnitude<.01f)ahead=heading;
            var side=Vector3.Cross(Vector3.up,ahead);
            // Top-most objects that are bamboo by name or by mesh; a clump split into parts counts once.
            var candidates=FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>!(r is SkinnedMeshRenderer)&&!(r is ParticleSystemRenderer)&&Flat(r.bounds.center,dock)<30)
                .Select(r=>r.transform).Where(t=>tank==null||!t.IsChildOf(tank.transform)).ToList();
            var roots=new HashSet<Transform>();
            foreach(var t in candidates)
            {
                // The highest bamboo-named parent that is still one clump (a grove's group is bigger and is skipped).
                Transform clump=null;
                for(var up=t;up!=null&&up!=root.transform;up=up.parent)
                    if(BambooClumpName(up.name)){var b=ClumpBounds(up);if(Mathf.Max(b.extents.x,b.extents.z)<=8)clump=up;else break;}
                if(clump==null&&BambooClump(t))clump=t;
                if(clump!=null)roots.Add(clump);
            }
            foreach(var clumpRoot in roots)
            {
                var bounds=ClumpBounds(clumpRoot);var center=bounds.center;float radius=Mathf.Max(bounds.extents.x,bounds.extents.z);
                if(radius>8)continue;   // a whole grove or village block, not a clump
                bool onRoad=DistanceToPath(center,road)<radius+2.2f;
                var off=Vector3.ProjectOnPlane(center-dock,Vector3.up);float along=Vector3.Dot(off,ahead),across=Mathf.Abs(Vector3.Dot(off,side));
                bool inFront=along>-1&&along<CrushFrontReach+radius&&across<4+radius;
                if(!onRoad&&!inFront)continue;
                found.Add(new CrushedClump{root=clumpRoot,position=clumpRoot.position,rotation=clumpRoot.rotation,pivot=new Vector3(center.x,bounds.min.y,center.z),onRoad=onRoad});
                Debug.Log($"[Map2] tank crushes bamboo '{clumpRoot.name}' at {center:F1} ({(onRoad?"on its road":"in front of it")})");
            }
            return found;
        }
        static float DistanceToPath(Vector3 p,Vector3[] path)
        {
            float best=float.MaxValue;p.y=0;
            for(int i=1;i<path.Length;i++){var a=path[i-1];var b=path[i];a.y=b.y=0;var ab=b-a;float t=Mathf.Clamp01(Vector3.Dot(p-a,ab)/Mathf.Max(ab.sqrMagnitude,1e-4f));best=Mathf.Min(best,Vector3.Distance(p,a+ab*t));}
            return best;
        }
        /// <summary>Called every frame after the tank has moved.</summary>
        void TickBambooCrush(float dt)
        {
            bool before=TankStage=="hidden"||TankStage=="breach"||TankStage=="leaving"||TankStage=="waiting";
            if(before&&!State.Done("N3_07")){if(clumps!=null)foreach(var c in clumps)if(c.fall>=0)Pose(c,0,true);return;}
            if(tank==null)return;
            clumps??=FindClumpsToCrush();
            bool emerging=TankStage=="emerging";var at=tank.transform.position;
            foreach(var c in clumps)
            {
                if(c.fall<0)
                {
                    if(!emerging){Pose(c,1,false);continue;}   // past the drive (or loaded there): already flat
                    bool hull=c.onRoad?Flat(at,c.pivot)<4.5f:sceneClock>=EmergeEnd-1.2f;   // the clumps in front go as it stops and turns to the boat
                    if(!hull)continue;
                    CrushDust(c.pivot);Pose(c,1,false);
                }
            }
        }
        /// <summary>Broken: the clump goes and its stems lie snapped and scattered where it stood; standing puts it back.</summary>
        void Pose(CrushedClump c,float fall,bool standing)
        {
            if(c.root==null)return;
            if(standing){c.fall=-1;c.root.gameObject.SetActive(true);if(c.debris!=null)Destroy(c.debris);c.debris=null;return;}
            c.fall=1;if(c.debris!=null)return;
            var material=c.root.GetComponentInChildren<Renderer>(true)?.sharedMaterial;
            c.debris=new GameObject("Bamboo broken by the tank");c.debris.transform.SetParent(root.transform,false);c.debris.transform.position=c.pivot;
            var random=new System.Random(c.root.GetInstanceID());float R()=>(float)random.NextDouble();
            for(int i=0;i<11;i++)
            {
                var stem=GameObject.CreatePrimitive(PrimitiveType.Cylinder);Destroy(stem.GetComponent<Collider>());stem.name="Snapped stem";
                stem.transform.SetParent(c.debris.transform,false);
                float length=.6f+R()*1.6f,yaw=R()*360;var flat=Quaternion.Euler(0,yaw,0);
                stem.transform.localPosition=flat*Vector3.forward*(R()*1.8f)+Vector3.up*.06f;
                stem.transform.localRotation=flat*Quaternion.Euler(86+R()*8,0,0);stem.transform.localScale=new Vector3(.07f+R()*.04f,length*.5f,.07f+R()*.04f);
                var r=stem.GetComponent<Renderer>();if(material!=null)r.sharedMaterial=material;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            for(int i=0;i<3;i++)   // the stumps left standing
            {
                var stump=GameObject.CreatePrimitive(PrimitiveType.Cylinder);Destroy(stump.GetComponent<Collider>());stump.name="Stump";stump.transform.SetParent(c.debris.transform,false);
                float h=.15f+R()*.25f;stump.transform.localPosition=new Vector3(R()-.5f,h,R()-.5f);stump.transform.localScale=new Vector3(.09f,h,.09f);
                if(material!=null)stump.GetComponent<Renderer>().sharedMaterial=material;
            }
            c.root.gameObject.SetActive(false);
        }
        void CrushDust(Vector3 at)
        {
            if(crushDust==null)
            {
                crushDust=Map01Fx.System(root.transform,"Bamboo crushed by the tank",at,Map01Fx.Lit,1.2f,60);
                var main=crushDust.main;main.startLifetime=new ParticleSystem.MinMaxCurve(1.2f,2.2f);main.startSpeed=new ParticleSystem.MinMaxCurve(.6f,1.8f);
                main.startSize=new ParticleSystem.MinMaxCurve(.8f,1.8f);main.startColor=new Color(.42f,.37f,.3f,.55f);
                var shape=crushDust.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=1.2f;
            }
            crushDust.transform.position=at+Vector3.up*.6f;crushDust.Emit(40);
        }
    }
}
