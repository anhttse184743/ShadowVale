using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadowVale.Map01 {
    /// <summary>Map 1 presentation only. Opaque original cover/detection geometry is untouched.</summary>
    public sealed class Map01FoliageReveal : MonoBehaviour {
        sealed class Plant { public Bounds bounds;public Renderer[] renderers;public Material[][] materials;public MaterialPropertyBlock[] blocks;public MaterialPropertyBlock[][] indexed;public float opacity=1;public bool swapped; }
        const float Cell=4;
        readonly Dictionary<Vector2Int,List<Plant>> grid=new();
        readonly HashSet<Plant> near=new();
        readonly HashSet<Plant> active=new();
        readonly List<Plant> update=new();
        readonly Dictionary<Material,Material> variants=new();
        MaterialPropertyBlock properties;
        Map01Mission mission;
        [Range(.4f,1f)] public float minimumOpacity=.62f;
        [Range(.4f,1.5f)] public float revealRadius=.85f;
        public Transform ExtraFocus{get;set;}
        public int ActivePlants=>active.Count;
        void Awake(){properties=new MaterialPropertyBlock();}
        void Start() {
            mission=GetComponent<Map01Mission>();
            if(mission==null||gameObject.scene.name!="Map 1"){enabled=false;return;}
            foreach(var lod in FindObjectsByType<LODGroup>(FindObjectsSortMode.None)) {
                // Reveal only authored hiding foliage, never crates, fences or shared tree trunks.
                string label=lod.name.ToLowerInvariant();
                if(!label.Contains("shrub")&&!label.Contains("grass")&&!label.Contains("bush")&&!label.Contains("fern"))continue;
                var renderers=lod.GetLODs().SelectMany(l=>l.renderers).Where(r=>r!=null&&r is MeshRenderer).Distinct().ToArray();
                if(renderers.Length==0)continue;
                var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                if(bounds.size.y<.3f||bounds.size.y>4||bounds.size.x>8||bounds.size.z>8)continue;
                var plant=new Plant{bounds=bounds,renderers=renderers,materials=renderers.Select(r=>r.sharedMaterials).ToArray(),blocks=new MaterialPropertyBlock[renderers.Length],indexed=new MaterialPropertyBlock[renderers.Length][]};
                for(int i=0;i<renderers.Length;i++){
                    plant.blocks[i]=new MaterialPropertyBlock();renderers[i].GetPropertyBlock(plant.blocks[i]);
                    plant.indexed[i]=new MaterialPropertyBlock[plant.materials[i].Length];
                    for(int j=0;j<plant.indexed[i].Length;j++){plant.indexed[i][j]=new MaterialPropertyBlock();renderers[i].GetPropertyBlock(plant.indexed[i][j],j);}
                }
                for(int x=Mathf.FloorToInt(bounds.min.x/Cell);x<=Mathf.FloorToInt(bounds.max.x/Cell);x++)
                for(int z=Mathf.FloorToInt(bounds.min.z/Cell);z<=Mathf.FloorToInt(bounds.max.z/Cell);z++) {
                    var key=new Vector2Int(x,z);if(!grid.TryGetValue(key,out var list))grid[key]=list=new List<Plant>();list.Add(plant);
                }
            }
        }
        Material Variant(Material source) {
            if(source==null)return null;
            if(variants.TryGetValue(source,out var result))return result;
            result=new Material(source){name=source.name+" (Map 1 reveal)",renderQueue=3000};
            result.SetFloat("_Surface",1);result.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);result.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            result.SetFloat("_ZWrite",0);result.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");result.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            result.SetFloat("_AlphaClip",0);result.DisableKeyword("_ALPHATEST_ON");
            result.SetOverrideTag("RenderType","Transparent");result.SetShaderPassEnabled("DepthOnly",false);
            result.SetShaderPassEnabled("DepthNormalsOnly",false);
            variants.Add(source,result);return result;
        }
        void Query(Vector3 position) {
            var cell=new Vector2Int(Mathf.FloorToInt(position.x/Cell),Mathf.FloorToInt(position.z/Cell));
            for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)if(grid.TryGetValue(cell+new Vector2Int(x,z),out var list))
                foreach(var plant in list)if(Vector3.Distance(plant.bounds.ClosestPoint(position+Vector3.up*.7f),position+Vector3.up*.7f)<revealRadius)near.Add(plant);
        }
        void LateUpdate() {
            if(mission==null||mission.player==null)return;
            near.Clear();if(mission.TryGetHideSpot(mission.player.position,out _))Query(mission.player.position);
            if(ExtraFocus!=null){Query(mission.player.position);Query(ExtraFocus.position);}
            foreach(var plant in near)active.Add(plant);
            update.Clear();update.AddRange(active);
            foreach(var plant in update) {
                float goal=near.Contains(plant)?minimumOpacity:1;
                plant.opacity=Mathf.MoveTowards(plant.opacity,goal,Time.deltaTime*(1-minimumOpacity)/(goal<1?.35f:.45f));
                if(!plant.swapped&&plant.opacity<.999f) {
                    for(int i=0;i<plant.renderers.Length;i++)if(plant.renderers[i]!=null)plant.renderers[i].sharedMaterials=plant.materials[i].Select(Variant).ToArray();plant.swapped=true;
                }
                if(plant.opacity>=.999f){Restore(plant);active.Remove(plant);continue;}
                for(int i=0;i<plant.renderers.Length;i++) {
                    var renderer=plant.renderers[i];if(renderer==null)continue;
                    for(int j=0;j<plant.materials[i].Length;j++){
                        var material=plant.materials[i][j];if(material==null||!material.HasProperty("_BaseColor"))continue;
                        renderer.GetPropertyBlock(properties,j);var color=material.GetColor("_BaseColor");color.a*=plant.opacity;
                        properties.SetColor("_BaseColor",color);renderer.SetPropertyBlock(properties,j);
                    }
                }
            }
        }
        static void Restore(Plant plant) {
            if(!plant.swapped)return;
            for(int i=0;i<plant.renderers.Length;i++)if(plant.renderers[i]!=null){
                plant.renderers[i].sharedMaterials=plant.materials[i];plant.renderers[i].SetPropertyBlock(plant.blocks[i]);
                for(int j=0;j<plant.indexed[i].Length;j++)plant.renderers[i].SetPropertyBlock(plant.indexed[i][j],j);
            }
            plant.swapped=false;plant.opacity=1;
        }
        void OnDisable(){foreach(var plant in active)Restore(plant);active.Clear();ExtraFocus=null;}
        void OnDestroy(){OnDisable();foreach(var material in variants.Values)if(material!=null)Destroy(material);}
    }
}
