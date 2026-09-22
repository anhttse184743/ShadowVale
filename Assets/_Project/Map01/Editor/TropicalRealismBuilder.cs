using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    [InitializeOnLoad]
    public static class TropicalRealismBuilder
    {
        const string Root="Assets/_Project/Art/Environment/Map01_Realism";
        const string Reports="Tools/Map01OptimizedReports";
        const string Request="Tools/Map01Realism.request";
        public const string Source="SourceArt/Map01_Realism/TropicalAssets.meshdata.json.gz";
        static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
        static Material vegetation;
        static TropicalRealismBuilder(){EditorApplication.update+=Poll;File.WriteAllText(Reports+"/realism.version","4");}
        static void Poll()
        {
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            string action=File.ReadAllText(Request).Trim();File.Delete(Request);
            try{if(action=="capture")Capture();else Apply();File.WriteAllText(Reports+"/realism.status","PASS "+DateTime.UtcNow.ToString("O"));}
            catch(Exception e){File.WriteAllText(Reports+"/realism.status","FAIL "+e);UnityEngine.Debug.LogException(e);}
        }
        static Material Material(string name,string shader)
        {
            string path=Root+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            var s=Shader.Find(shader);if(s==null)throw new Exception("Shader unavailable: "+shader);
            if(m==null){m=new Material(s);AssetDatabase.CreateAsset(m,path);}else m.shader=s;
            m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
        }
        static void Import()
        {
            meshes.Clear();Directory.CreateDirectory(Root+"/Meshes");AssetDatabase.Refresh();
            OptimizedMapBuilder.SourceData data;
                        var imported=new List<OptimizedMapBuilder.MeshData>();
            foreach(var source in new[]{Source,"SourceArt/Map01_Realism/StaticRefinement.meshdata.json.gz"})
                using(var file=File.OpenRead(source))using(var gz=new GZipStream(file,CompressionMode.Decompress))using(var reader=new StreamReader(gz))imported.AddRange(JsonUtility.FromJson<OptimizedMapBuilder.SourceData>(reader.ReadToEnd()).meshes);
            data=new OptimizedMapBuilder.SourceData{meshes=imported.ToArray()};
            AssetDatabase.StartAssetEditing();
            try{foreach(var src in data.meshes)
            {
                string path=Root+"/Meshes/"+src.id+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);bool create=mesh==null;if(create)mesh=new Mesh{name=src.id};else mesh.Clear();
                mesh.indexFormat=IndexFormat.UInt32;int n=src.vertices.Length/3;var v=new Vector3[n];var normals=new Vector3[n];var colors=new Color32[n];
                for(int i=0;i<n;i++){v[i]=new Vector3(src.vertices[i*3],src.vertices[i*3+1],src.vertices[i*3+2]);normals[i]=new Vector3(src.normals[i*3],src.normals[i*3+1],src.normals[i*3+2]);colors[i]=(Color32)new Color(src.colors[i*4],src.colors[i*4+1],src.colors[i*4+2],1);}
                mesh.vertices=v;mesh.normals=normals;mesh.colors32=colors;mesh.triangles=src.triangles;mesh.RecalculateBounds();
                if(create)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);meshes[src.id]=mesh;
            }}finally{AssetDatabase.StopAssetEditing();}
            vegetation=Material("Natural vegetation","ShadowVale/Natural Surface");vegetation.SetColor("_BaseColor",new Color(.95f,1,.91f));
        }
        static bool Ground(float x,float z,out RaycastHit ground)
        {
            foreach(var hit in Physics.RaycastAll(new Vector3(x,80,z),Vector3.down,160,LayerMask.GetMask("Obstacle")).OrderByDescending(h=>h.point.y))
                if(hit.collider.name.Contains("Collision_Walk")||hit.collider.name.StartsWith("Hill ")){ground=hit;return true;}
            ground=default;return false;
        }
        static GameObject Child(string name,Transform parent){var go=new GameObject(name);go.transform.SetParent(parent,false);return go;}
        static void Lods(GameObject root,string id,int count,float[] thresholds,bool shadows=true)
        {
            var lods=new LOD[count];
            for(int i=0;i<count;i++)
            {
                var go=Child("LOD"+i,root.transform);go.AddComponent<MeshFilter>().sharedMesh=meshes[id+"_LOD"+i];var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=vegetation;
                r.shadowCastingMode=shadows&&i<2?ShadowCastingMode.On:ShadowCastingMode.Off;lods[i]=new LOD(thresholds[i],new Renderer[]{r});
            }
            var group=root.GetComponent<LODGroup>();if(group==null)group=root.AddComponent<LODGroup>();group.SetLODs(lods);group.RecalculateBounds();
            if(root.GetComponent<ForestInstanceTint>()==null)root.AddComponent<ForestInstanceTint>();
        }
        static Mesh GrassPatch(int variant,int lod)
        {
            var random=new System.Random(400+variant);var parts=new CombineInstance[16];
            for(int i=0;i<parts.Length;i++)
            {
                float x=(i%4-1.5f)*1.5f+(float)random.NextDouble()*.45f,z=(i/4-1.5f)*1.5f+(float)random.NextDouble()*.45f;
                var position=new Vector3(x,0,z);float angle=(float)random.NextDouble()*360,scale=.85f+(float)random.NextDouble()*.4f;
                parts[i]=new CombineInstance{mesh=meshes["Grass_LOD"+lod],transform=Matrix4x4.TRS(position,Quaternion.Euler(0,angle,0),new Vector3(scale,scale,scale))};
            }
            string name="GrassPatch_"+variant+"_LOD"+lod,path=Root+"/Meshes/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);bool create=mesh==null;
            if(create)mesh=new Mesh{name=name};else mesh.Clear();mesh.indexFormat=IndexFormat.UInt32;mesh.CombineMeshes(parts,true,true);mesh.RecalculateBounds();
            if(create)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);return mesh;
        }
        public static float RiverX(float z)=>8+12*Mathf.Sin(z*.041f)+4*Mathf.Sin(z*.105f);
        [MenuItem("ShadowVale/Map 1/Apply Blender Tropical Realism")]
        public static void Apply()
        {
            var scene=EditorSceneManager.GetActiveScene();if(scene.isDirty)throw new Exception("Save the active scene before applying Blender assets.");
            if(scene.path!=OptimizedMapBuilder.ScenePath)scene=EditorSceneManager.OpenScene(OptimizedMapBuilder.ScenePath);
            Import();var mission=Object.FindFirstObjectByType<ForestMission>();var surface=Object.FindFirstObjectByType<NavMeshSurface>();
            Physics.SyncTransforms();int forest=0,banana=0,grass=0,houses=0,palms=0;
            foreach(var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if(mf.sharedMesh==null)continue;string name=mf.sharedMesh.name;
                                if(name.StartsWith("Chunk_")&&!meshes.ContainsKey(name)){mf.GetComponent<Renderer>().enabled=false;continue;}
                if(name.StartsWith("Chunk_")&&meshes.ContainsKey(name))
                {mf.sharedMesh=meshes[name];if(!name.EndsWith("_water"))mf.GetComponent<Renderer>().sharedMaterial=vegetation;}
                if(name.StartsWith("Tree_")||name.StartsWith("Forest_"))
                {
                    string suffix=name.Substring(name.IndexOf('_')+1);string key="Forest_"+suffix;if(!meshes.ContainsKey(key))continue;
                    mf.sharedMesh=meshes[key];mf.GetComponent<Renderer>().sharedMaterial=vegetation;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(mf);PrefabUtility.RecordPrefabInstancePropertyModifications(mf.GetComponent<Renderer>());
                    if(suffix.EndsWith("LOD0"))
                    {
                        forest++;var tree=mf.transform.parent;var group=tree.GetComponent<LODGroup>();var lods=group.GetLODs();
                        float[] thresholds={.20f,.075f,.018f};for(int l=0;l<lods.Length;l++)lods[l].screenRelativeTransitionHeight=thresholds[l];group.SetLODs(lods);group.RecalculateBounds();PrefabUtility.RecordPrefabInstancePropertyModifications(group);
                        if(Ground(tree.position.x,tree.position.z,out var g)&&g.point.y>tree.position.y+.2f){tree.position=g.point;PrefabUtility.RecordPrefabInstancePropertyModifications(tree);}
                    }
                }
                if(name.EndsWith("_water")){mf.GetComponent<Renderer>().sharedMaterial=Material("River surface","ShadowVale/Tropical River");mf.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;}
            }
            var expansion=GameObject.Find("03 Tropical expansion");if(expansion==null)throw new Exception("Build the tropical expansion before refining its assets.");
            var transforms=expansion.GetComponentsInChildren<Transform>(true);
            foreach(var t in transforms.Where(t=>t.name.StartsWith("Coconut ")&&t.parent.name.Contains("vegetation")).ToArray())Object.DestroyImmediate(t.gameObject);
            foreach(var t in transforms.Where(t=>t!=null&&t.name.StartsWith("Banana ")&&t.parent.name.Contains("vegetation")).ToArray())
            {
                foreach(var child in t.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
                Lods(t.gameObject,"Banana",3,new[]{.15f,.055f,.012f});var c=t.GetComponent<CapsuleCollider>();if(c==null)c=t.gameObject.AddComponent<CapsuleCollider>();c.center=Vector3.up*1.4f;c.height=2.8f;c.radius=.18f;t.gameObject.layer=LayerMask.NameToLayer("Obstacle");banana++;
            }
            for(int v=0;v<3;v++)for(int l=0;l<3;l++)meshes["GrassPatch_"+v+"_LOD"+l]=GrassPatch(v,l);
            foreach(var t in transforms.Where(t=>t!=null&&t.name.StartsWith("Tall grass patch ")).ToArray())
            {
                                var filter=t.GetComponent<MeshFilter>();Vector3 center=filter!=null?filter.sharedMesh.bounds.center:t.position;
                // A full grass cell must remain on dry ground, including its edges.
                if(Mathf.Abs(center.x-RiverX(center.z))<10)
                {
                    string index=t.name.Substring("Tall grass patch ".Length);var hide=expansion.transform.Find("Evenly distributed tropical vegetation/Grass concealment "+index);
                    if(hide!=null)Object.DestroyImmediate(hide.gameObject);Object.DestroyImmediate(t.gameObject);continue;
                }
                if(filter!=null){Object.DestroyImmediate(filter);Object.DestroyImmediate(t.GetComponent<MeshRenderer>());}
                foreach(var child in t.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
                if(Ground(center.x,center.z,out var g)){t.position=g.point;t.rotation=Quaternion.FromToRotation(Vector3.up,g.normal);}
                Lods(t.gameObject,"GrassPatch_"+(grass%3),3,new[]{.18f,.08f,.035f},false);grass++;
            }
            foreach(var t in transforms.Where(t=>t!=null&&t.name.StartsWith("House ")).ToArray())
            {
                foreach(var r in t.GetComponentsInChildren<MeshRenderer>())r.enabled=false;
                var old=t.Find("Blender house");if(old!=null)Object.DestroyImmediate(old.gameObject);
                Lods(Child("Blender house",t),"House",2,new[]{.09f,.008f});houses++;
            }
                        var houseCenters=expansion.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("House ")).Select(t=>t.position).ToArray();
            foreach(var group in Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None))
                if(group.name.StartsWith("Tree ")&&houseCenters.Any(p=>Vector2.Distance(new Vector2(p.x,p.z),new Vector2(group.transform.position.x,group.transform.position.z))<10f))
                {group.gameObject.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(group.gameObject);}
            foreach(var prop in surface.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Prop_")&&PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)).ToArray())
                if(houseCenters.Any(p=>Mathf.Abs(p.x-prop.position.x)<4.5f&&Mathf.Abs(p.z-prop.position.z)<5f))
                {prop.gameObject.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(prop.gameObject);}
            var oldPalms=surface.transform.Find("Riverside coconut palms");if(oldPalms!=null)Object.DestroyImmediate(oldPalms.gameObject);
            var bankRoot=Child("Riverside coconut palms",surface.transform);var palmPositions=new List<Vector3>();
            for(int side=-1;side<=1;side+=2)for(int i=0;i<19;i++)
            {
                float z=-88+i*9.5f+(side==1?3:0);if(Mathf.Abs(z)<9||z>78)continue;
                float x=RiverX(z)+side*(8.8f+1.1f*Mathf.Sin(i*2.1f));if(!Ground(x,z,out var hit))continue;
                if(Physics.OverlapSphere(hit.point+Vector3.up*1.5f,.8f,LayerMask.GetMask("Obstacle")).Any(c=>!c.name.Contains("Collision_Walk")&&!c.name.StartsWith("Hill ")))continue;
                if(!NavMesh.SamplePosition(hit.point,out var nav,2,NavMesh.AllAreas))continue;
                var palm=Child("River coconut "+palms,bankRoot.transform);palm.transform.position=hit.point;palm.transform.rotation=Quaternion.Euler(0,side==1?180:0,0);
                float size=.9f+.17f*Mathf.Sin(i*1.6f);palm.transform.localScale=Vector3.one*size;
                Lods(palm,"Coconut",3,new[]{.20f,.075f,.013f});palm.layer=LayerMask.NameToLayer("Obstacle");var collider=palm.AddComponent<CapsuleCollider>();collider.center=new Vector3(.3f,4,0);collider.height=8;collider.radius=.32f;
                palmPositions.Add(hit.point);palms++;
            }
            var sky=Material("Daylight and clouds","ShadowVale/Tropical Sky");RenderSettings.skybox=sky;RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.0028f;RenderSettings.fogColor=new Color(.66f,.76f,.77f);
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.55f,.66f,.75f);RenderSettings.ambientEquatorColor=new Color(.35f,.40f,.27f);RenderSettings.ambientGroundColor=new Color(.18f,.20f,.12f);
            var ambient=new SphericalHarmonicsL2();ambient.AddAmbientLight(new Color(.30f,.35f,.28f));RenderSettings.ambientProbe=ambient;
            var sun=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l=>l.type==LightType.Directional);sun.intensity=1.35f;sun.color=new Color(1,.96f,.86f);RenderSettings.sun=sun;
            mission.gameCamera.clearFlags=CameraClearFlags.Skybox;mission.gameCamera.farClipPlane=280;
            Physics.SyncTransforms();surface.BuildNavMesh();string navPath=Root+"/RiversideNavMesh.asset";var previous=AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
            if(previous==null)AssetDatabase.CreateAsset(surface.navMeshData,navPath);else{EditorUtility.CopySerialized(surface.navMeshData,previous);surface.RemoveData();surface.navMeshData=previous;surface.AddData();EditorUtility.SetDirty(previous);}
            OptimizedMapBuilder.Validate();
            foreach(var guard in Object.FindObjectsByType<ForestGuard>(FindObjectsSortMode.None))for(int i=0;i<guard.patrol.Length;i++)
            {var path=new NavMeshPath();if(!NavMesh.CalculatePath(guard.patrol[i],guard.patrol[(i+1)%guard.patrol.Length],NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)throw new Exception("Disconnected patrol: "+guard.id);}
            if(palms<12)throw new Exception("Too few viable riverside palms: "+palms);
            var renderers=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);if(renderers.Any(r=>r.sharedMaterials.Any(m=>m==null||m.shader==null)))throw new Exception("Missing renderer material");
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            var lines=new List<string>{"Blender-authored assets applied",$"Forest trees: {forest}; banana plants: {banana}; grass cells: {grass}; detailed houses: {houses}; riverside palms: {palms}","Camera skybox, layered moving clouds, animated river ripple/Fresnel shading", "Navigation: original routes and every guard patrol loop PASS", "Shared LOD meshes + instanced materials; grass shadows disabled; no per-frame vegetation scripts", "Triangles per shared prototype:"};
            foreach(var pair in meshes)lines.Add(pair.Key+": "+pair.Value.triangles.Length/3);
            lines.Add("Palm positions (within 7.7–9.9 m of authored river centre; bridge and exit reserved):");foreach(var p in palmPositions)lines.Add(p.ToString("F2"));
            File.WriteAllLines(Reports+"/realism-summary.txt",lines);Capture();
        }
        public static void Capture()
        {
            var cam=Object.FindFirstObjectByType<ForestMission>().gameCamera;var position=cam.transform.position;var rotation=cam.transform.rotation;bool ortho=cam.orthographic;float fov=cam.fieldOfView;var oldTarget=cam.targetTexture;
            var previous=RenderTexture.active;var rt=RenderTexture.GetTemporary(1440,900,24);var texture=new Texture2D(1440,900,TextureFormat.RGB24,false);
            try
            {
                cam.orthographic=false;cam.fieldOfView=65;cam.targetTexture=rt;
                Action<string> shot=name=>{cam.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1440,900),0,0);texture.Apply();File.WriteAllBytes(Reports+"/realism-"+name+".png",texture.EncodeToPNG());};
                shot("player");
                cam.transform.position=new Vector3(-1,4,-49);cam.transform.LookAt(new Vector3(6,5,-19));shot("river");
                var house=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).First(t=>t.name=="House 1");cam.transform.position=house.position+new Vector3(3,2.5f,-8);cam.transform.LookAt(house.position+Vector3.up*2);shot("house");
                cam.transform.position=new Vector3(-35,10,-45);cam.transform.LookAt(new Vector3(-30,17,-18));shot("canopy");
                var stopwatch=new Stopwatch();for(int i=0;i<5;i++)cam.Render();stopwatch.Start();for(int i=0;i<20;i++)cam.Render();stopwatch.Stop();
                File.WriteAllText(Reports+"/realism-render-timing.txt",$"Editor camera.Render CPU submission average (20 renders at 1440x900): {stopwatch.Elapsed.TotalMilliseconds/20:F2} ms. Not GPU frame time or playable-build FPS.\n");
            }
            finally{cam.targetTexture=oldTarget;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(texture);cam.orthographic=ortho;cam.fieldOfView=fov;cam.transform.SetPositionAndRotation(position,rotation);}
        }
    }
}





