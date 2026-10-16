using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01 {
    /// <summary>Finite, saved stone pickups on the rescue approaches. No terrain edits.</summary>
    public sealed class Map01RescueStones : MonoBehaviour {
        public const string IdPrefix="rescue_stones_";
        readonly List<(ForestPoint point,GameObject visual)> piles=new();
        Material fallback;
        public IReadOnlyList<ForestPoint> Pickups=>piles.Select(p=>p.point).ToArray();
        public static Map01RescueStones Ensure(Map01Mission mission,Map01RescueLayout layout,Vector3 captive){
            var owner=mission.GetComponent<Map01RescueStones>();
            if(owner==null)owner=mission.gameObject.AddComponent<Map01RescueStones>();
            if(owner.piles.Count>0)return owner;
            var source=FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).FirstOrDefault(m=>m.sharedMesh!=null&&m.sharedMesh.vertexCount<8000&&m.GetComponent<Renderer>()!=null
                &&(m.name.IndexOf("rock",StringComparison.OrdinalIgnoreCase)>=0||m.sharedMesh.name.IndexOf("rock",StringComparison.OrdinalIgnoreCase)>=0||m.name.IndexOf("boulder",StringComparison.OrdinalIgnoreCase)>=0));
            for(int i=0;i<layout.stoneOffsets.Length;i++){
                string id=IdPrefix+i.ToString("D2");
                if(mission.Points.Any(p=>p.id==id))continue;
                if(!NavMesh.SamplePosition(captive+layout.stoneOffsets[i],out var nav,4,NavMesh.AllAreas))continue;
                if(mission.Enemies.Any(e=>e!=null&&e.Alive&&Vector3.Distance(e.transform.position,nav.position)<5))continue;
                if(!Physics.Raycast(nav.position+Vector3.up*.5f,Vector3.down,out var ground,1.5f,mission.ObstructionMask,QueryTriggerInteraction.Ignore)
                    ||ground.normal.y<.7f||Mathf.Abs(ground.point.y-nav.position.y)>.35f)continue;
                if(Physics.CheckSphere(ground.point+Vector3.up*.3f,.24f,mission.ObstructionMask,QueryTriggerInteraction.Ignore))continue;
                if(owner.piles.Any(p=>Vector3.Distance(p.point.transform.position,ground.point)<3))continue;
                var root=new GameObject("Stone pickup "+i.ToString("D2"));root.transform.SetParent(owner.transform,false);root.transform.position=ground.point;
                var point=root.AddComponent<ForestPoint>();point.id=id;point.kind=ForestPointKind.Loot;point.radius=2.5f;
                int count=Mathf.Max(1,layout.stonesPerPile);point.label=$"Đá ném — {count} viên";
                point.items=new[]{new ForestIngredient{item_id="stone",count=count}};point.restockSeconds=0;
                var visual=new GameObject("Pickable stones");visual.transform.SetParent(root.transform,false);
                for(int n=0;n<3;n++){
                    var offset=new Vector3((n-1)*.13f,0,n%2==0?0:.12f);
                    if(source!=null){
                        var rock=new GameObject("Stone "+n);rock.transform.SetParent(visual.transform,false);
                        var mesh=source.sharedMesh;var bounds=mesh.bounds;float scale=.22f/Mathf.Max(.001f,Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z)));
                        rock.transform.localScale=Vector3.one*scale;rock.transform.localPosition=offset-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
                        rock.AddComponent<MeshFilter>().sharedMesh=mesh;rock.AddComponent<MeshRenderer>().sharedMaterials=source.GetComponent<Renderer>().sharedMaterials;
                    }else{
                        var rock=GameObject.CreatePrimitive(PrimitiveType.Sphere);rock.name="Stone "+n;Destroy(rock.GetComponent<Collider>());rock.transform.SetParent(visual.transform,false);
                        rock.transform.localScale=new Vector3(.2f,.12f,.16f);rock.transform.localPosition=offset+Vector3.up*.06f;
                        if(owner.fallback==null){owner.fallback=new Material(Shader.Find("Universal Render Pipeline/Lit"));owner.fallback.color=new Color(.51f,.48f,.40f);}
                        rock.GetComponent<Renderer>().sharedMaterial=owner.fallback;
                    }
                }
                mission.RegisterPoint(point);owner.piles.Add((point,visual));
            }
            return owner;
        }
        void LateUpdate(){foreach(var p in piles)if(p.point!=null&&p.visual!=null&&p.visual.activeSelf==p.point.used)p.visual.SetActive(!p.point.used);}
        void OnDestroy(){if(fallback!=null)Destroy(fallback);}
    }
}
