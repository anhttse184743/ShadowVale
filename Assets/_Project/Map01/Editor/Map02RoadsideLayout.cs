using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Object = UnityEngine.Object;
namespace ShadowVale.Map01.Editor
{
    public static partial class Map02VillageBuilder
    {
        [InitializeOnLoadMethod] static void RegisterRoadsideRequest() { EditorApplication.update += PollRoadsideRequest; }
        static void PollRoadsideRequest() {
            if(File.Exists("Tools/Map02RoadsidePolish.request")&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!EditorApplication.isPlayingOrWillChangePlaymode) {
                File.Delete("Tools/Map02RoadsidePolish.request");
                try { PolishRoadsideAssets();File.WriteAllText(Reports+"/roadside-polish.status","PASS"); }
                catch(Exception exception) {File.WriteAllText(Reports+"/roadside-polish.status","FAIL "+exception);Debug.LogException(exception);}
                return;
            }
            const string request="Tools/Map02Roadside.request";
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(request);
            try { ApplyLushRoadside();File.WriteAllText(Reports+"/roadside.status","PASS"); }
            catch(Exception exception) { File.WriteAllText(Reports+"/roadside.status","FAIL "+exception);Debug.LogException(exception); }
        }
        static void PolishRoadsideAssets() {
            var scene=EditorSceneManager.GetActiveScene();if(scene.path!=ScenePath||scene.isDirty)throw new Exception("Keep the saved Map 2 scene active for polish.");
            ImportRoadsideMeshes();var mat=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Roadside_Textured_Ground.mat");mat.SetTexture("_RoadTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Ricefield_Gravel_Albedo.png"));EditorUtility.SetDirty(mat);
            var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).ToArray();foreach(var group in all.Select(t=>t.GetComponent<LODGroup>()).Where(g=>g!=null))group.RecalculateBounds();
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);scene=EditorSceneManager.OpenScene(ScenePath);all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).ToArray();var camera=all.Select(t=>t.GetComponent<Camera>()).First(c=>c!=null);
            Capture(camera,new Vector3(0,205,-130),Vector3.zero,112,"map02-roadside-overview");Capture(camera,new Vector3(-105,65,-82),new Vector3(-65,1,10),53,"map02-roadside-village");
            var b=all.Where(t=>t.name=="Blender Bamboo").OrderBy(t=>Vector3.Distance(t.position,new Vector3(45,1,-7))).First().position;Capture(camera,b+new Vector3(-12,11,-20),b+Vector3.up*3.5f,0,"map02-roadside-bamboo");
            var h=all.Where(t=>t.name=="Blender Hedge").OrderBy(t=>Vector3.Distance(t.position,new Vector3(60,1,20))).First().position;Capture(camera,h+new Vector3(11,6,-14),h+Vector3.up,0,"map02-roadside-hedge");
            File.AppendAllText(Reports+"/roadside-checks.txt",Environment.NewLine+"PASS final Blender crown meshes imported and road material updated; planting and navigation unchanged."+Environment.NewLine);
        }
        static bool useRoundedVillageRoads;
        static float[] roundedRoadGrid;
        static readonly List<Vector2[]> roundedLanes = new List<Vector2[]>();
        static Dictionary<string,Mesh> roadsideMeshes;
        static float SoftMinimum(float a,float b,float radius) {
            float h=Mathf.Max(radius-Mathf.Abs(a-b),0)/radius;
            return Mathf.Min(a,b)-h*h*radius*.25f;
        }
        static Vector2[] RoundedLane(params Vector2[] points) {
            var result=new List<Vector2>();
            for(int i=0;i<points.Length-1;i++) {
                var a=points[Mathf.Max(0,i-1)];var b=points[i];var c=points[i+1];var d=points[Mathf.Min(points.Length-1,i+2)];
                int steps=Mathf.Max(2,Mathf.CeilToInt(Vector2.Distance(b,c)/2));
                for(int j=0;j<steps;j++){float t=(float)j/steps;result.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t));}
            }result.Add(points.Last());return result.ToArray();
        }
        static float RoundedRoadExact(Vector2 q) {
            float distance=1000;
            foreach(var line in roundedLanes){float nearest=1000;for(int i=1;i<line.Length;i++)nearest=Mathf.Min(nearest,SegmentDistance(q,line[i-1],line[i]));distance=SoftMinimum(distance,nearest-1.7f,2.7f);}
            return SoftMinimum(distance,Vector2.Distance(q,new Vector2(-64,11))-8.7f,3.5f);
        }
        static float RoundedRoad(Vector2 q) {
            float x=Mathf.Clamp((q.x+110)*2,0,439.999f),z=Mathf.Clamp((q.y+100)*2,0,399.999f);int ix=(int)x,iz=(int)z;
            return Mathf.Lerp(Mathf.Lerp(roundedRoadGrid[iz*441+ix],roundedRoadGrid[iz*441+ix+1],x-ix),Mathf.Lerp(roundedRoadGrid[(iz+1)*441+ix],roundedRoadGrid[(iz+1)*441+ix+1],x-ix),z-iz);
        }
        static void PrepareRoundedLanes(Transform[] homes) {
            roundedLanes.Clear();
            roundedLanes.Add(RoundedLane(new Vector2(-76,-49),new Vector2(-75,-21),new Vector2(-76,7),new Vector2(-74,37),new Vector2(-74,69)));
            roundedLanes.Add(RoundedLane(new Vector2(-52,-47),new Vector2(-53,-18),new Vector2(-51,12),new Vector2(-51,40),new Vector2(-52,70)));
            roundedLanes.Add(RoundedLane(new Vector2(-108,10),new Vector2(-82,7),new Vector2(-64,11),new Vector2(-47,8),new Vector2(-29,-4),new Vector2(0,-4),new Vector2(33,-3),new Vector2(68,-7),new Vector2(110,-4)));
            // Bridge approaches retain their exact elevations and crossing locations.
            roundedLanes.Add(RoundedLane(new Vector2(-110,-68),new Vector2(-71,-65),new Vector2(-38,-70),new Vector2(-18,-68),new Vector2(8,-68),new Vector2(45,-64),new Vector2(79,-70),new Vector2(110,-68)));
            foreach(var home in homes){var front=Unwarp(home.position-home.forward*8);float lane=Mathf.Abs(front.x+75)<Mathf.Abs(front.x+52)?-75:-52;var a=new Vector2(front.x,front.z);var b=new Vector2(lane,front.z+2);roundedLanes.Add(RoundedLane(a,Vector2.Lerp(a,b,.5f)+new Vector2(0,-1),b));}
            roundedRoadGrid=new float[441*401];for(int z=0;z<=400;z++)for(int x=0;x<=440;x++)roundedRoadGrid[z*441+x]=RoundedRoadExact(new Vector2(x*.5f-110,z*.5f-100));
        }
        static void ImportRoadsideMeshes() {
            VillageSourceData data;using(var file=File.OpenRead("SourceArt/Map02_Village/Roadside.meshdata.json.gz"))using(var zip=new GZipStream(file,CompressionMode.Decompress))using(var reader=new StreamReader(zip))data=JsonUtility.FromJson<VillageSourceData>(reader.ReadToEnd());
            roadsideMeshes=new Dictionary<string,Mesh>();
            foreach(var source in data.meshes){var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};int count=source.vertices.Length/3;var vertices=new Vector3[count];var normals=new Vector3[count];var colors=new Color[count];for(int i=0;i<count;i++){vertices[i]=new Vector3(source.vertices[i*3],source.vertices[i*3+1],source.vertices[i*3+2]);normals[i]=new Vector3(source.normals[i*3],source.normals[i*3+1],source.normals[i*3+2]);colors[i]=new Color(source.colors[i*4],source.colors[i*4+1],source.colors[i*4+2],1);}mesh.vertices=vertices;mesh.normals=normals;mesh.colors=colors;mesh.triangles=source.triangles;mesh.RecalculateBounds();roadsideMeshes[source.id]=SaveMesh(mesh,"Roadside_"+source.id);roadsideMeshes[source.id].UploadMeshData(false);}
        }
        static void RoadsideModel(string kind,Vector3 p,float scale,Transform parent) {
            var go=Child("Blender "+kind,parent);go.transform.position=p;go.transform.rotation=Quaternion.Euler(0,Rand(0,360),0);go.transform.localScale=Vector3.one*scale;
            var lods=new LOD[2];for(int lod=0;lod<2;lod++){var model=Model(kind+" LOD"+lod,roadsideMeshes[kind+"_LOD"+lod],Vector3.zero,go.transform,palette);lods[lod]=new LOD(lod==0?.13f:.008f,new[]{model.GetComponent<Renderer>()});}var group=go.AddComponent<LODGroup>();group.SetLODs(lods);group.RecalculateBounds();
            if(kind!="Hedge")ColliderBox(go.transform,"Plant base obstacle",new Vector3(0,1,0),kind=="Bamboo"?new Vector3(1.5f,2,1.5f):new Vector3(.5f,2,.5f));
        }
        static bool RoadsideDry(Vector2 q) {
            return Mathf.Abs(q.x)<103&&Mathf.Abs(q.y)<93&&Mathf.Abs(q.x-Canal(q.y))>10&&new Vector2((q.x+52)/23f,(q.y-83)/15f).magnitude>1.1f&&Vector2.Distance(q,new Vector2(83,82))>8&&Vector2.Distance(q,new Vector2(81,-83))>8;
        }
        [MenuItem("ShadowVale/Map 2/Add Blender roadside bamboo and lush banks")]
        public static void ApplyLushRoadside() {
            Directory.CreateDirectory(Reports);var scene=EditorSceneManager.GetActiveScene();if(scene.isDirty)throw new Exception("Save active scene before applying roadside revision.");
            scene=EditorSceneManager.OpenScene(ScenePath);env=scene.GetRootGameObjects().First(g=>g.name.StartsWith("01 Environment")).transform;
            if(env.Find("Blender lush roads and field banks")!=null)throw new Exception("Roadside revision already applied. Restore Map2_before_roadside.unity before regenerating.");
            if(env.Find("Clustered village reference layout")==null)throw new Exception("Clustered village revision is required.");
            EditorSceneManager.SaveScene(scene,Reports+"/Map2_before_roadside.unity",true);ImportRoadsideMeshes();rng=new System.Random(261002);palette=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Botanical_Palette.mat");
            var homes=env.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Stilt house")||t.name.StartsWith("Bamboo thatch house")).ToArray();PrepareRoundedLanes(homes);useRoundedVillageRoads=true;
            try {
                var root=Child("Blender lush roads and field banks",env).transform;var ground=env.Find("Continuous ground with meandering canal");var gf=ground.GetComponent<MeshFilter>();var gc=ground.GetComponent<MeshCollider>();var mesh=Object.Instantiate(gf.sharedMesh);var vertices=mesh.vertices;var colors=mesh.colors;
                for(int i=0;i<vertices.Length;i++){var q3=Unwarp(ground.TransformPoint(vertices[i]));var q=new Vector2(q3.x,q3.z);int id;float inset=VillageParcel(q,out id),wet=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,2,inset));var p=Rural(new Vector3(q.x,ReferenceHeight(q.x,q.y)-.28f*wet,q.y));vertices[i]=ground.InverseTransformPoint(p);float noise=Mathf.PerlinNoise(p.x*.29f+300,p.z*.29f+300);colors[i]=Color.Lerp(Color.Lerp(new Color(.22f,.32f,.075f),new Color(.36f,.42f,.14f),noise),id%4==0?new Color(.28f,.39f,.08f):new Color(.52f,.45f,.12f),wet);}
                mesh.vertices=vertices;mesh.colors=colors;mesh.RecalculateNormals();mesh.RecalculateBounds();mesh=SaveMesh(mesh,"Roadside_Continuous_Terrain");gf.sharedMesh=mesh;gc.sharedMesh=mesh;
                const int resolution=1024;var mask=new Texture2D(resolution,resolution,TextureFormat.RGB24,false,true);var pixels=new Color32[resolution*resolution];
                for(int z=0;z<resolution;z++)for(int x=0;x<resolution;x++){var q=Unwarp(new Vector3(x*220f/(resolution-1)-110,1,z*200f/(resolution-1)-100));float paint=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.4f,.65f,RoundedRoad(new Vector2(q.x,q.z))));if(Mathf.Abs(q.x-Canal(q.z))<8.6f)paint=0;pixels[z*resolution+x]=new Color32((byte)(255*paint),0,0,255);}mask.SetPixels32(pixels);mask.Apply();string maskPath=Root+"/Roadside_Rounded_Lanes.png";File.WriteAllBytes(maskPath,mask.EncodeToPNG());Object.DestroyImmediate(mask);AssetDatabase.ImportAsset(maskPath);var importer=(TextureImporter)AssetImporter.GetAtPath(maskPath);importer.sRGBTexture=false;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
                var material=new Material(Shader.Find("ShadowVale/Map 2 Joined Soil"));material.SetTexture("_RoadTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Ricefield_Gravel_Albedo.png"));material.SetTexture("_SurfaceMask",AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath));material.SetFloat("_GravelScale",.65f);AssetDatabase.CreateAsset(material,Root+"/Roadside_Textured_Ground.mat");ground.GetComponent<MeshRenderer>().sharedMaterial=material;
                // Remove old grass batches so density is rebuilt once, not layered over the previous planting.
                var oldGrass=env.GetComponentsInChildren<VillageRiceInstances>().Where(r=>!r.name.StartsWith("Connected rice parcel")).ToArray();int oldCount=oldGrass.Sum(r=>r.plants.Length);foreach(var grass in oldGrass)Object.DestroyImmediate(grass.gameObject);
                Physics.SyncTransforms();
                var obstacleBounds=env.GetComponentsInChildren<BoxCollider>().Where(c=>c.enabled).Select(c=>c.bounds).ToArray();
                Func<Vector3,float,bool> clear=(p,radius)=>!homes.Any(h=>{var d=h.InverseTransformPoint(new Vector3(p.x,h.position.y,p.z));return Mathf.Abs(d.x)<5+radius&&Mathf.Abs(d.z)<7+radius;})&&!obstacleBounds.Any(b=>{var point=new Vector3(p.x,Mathf.Clamp(p.y+.5f,b.min.y,b.max.y),p.z);return b.SqrDistance(point)<radius*radius;});
                var planted=env.GetComponentsInChildren<LODGroup>().Select(l=>l.transform.position).ToList();var newPlants=new List<Vector3>();var bambooPositions=new List<Vector3>();int bambooCount=0,treeCount=0,hedgeCount=0;
                var candidates=new List<Vector2>();for(float x=-101;x<103;x+=2)for(float z=-91;z<93;z+=2)candidates.Add(new Vector2(x+Rand(-.6f,.6f),z+Rand(-.6f,.6f)));for(int i=candidates.Count-1;i>0;i--){int j=rng.Next(i+1);var tmp=candidates[i];candidates[i]=candidates[j];candidates[j]=tmp;}
                foreach(var q in candidates){float road=RoundedRoad(q);int id;float inset=VillageParcel(q,out id);if(!RoadsideDry(q)||road<2.4f||road>5.8f||inset>2)continue;var p=Rural(new Vector3(q.x,1,q.y));if(!clear(p,2)||planted.Any(v=>Vector2.Distance(new Vector2(v.x,v.z),new Vector2(p.x,p.z))<7.5f))continue;
                    string kind=bambooCount<28&&(q.x>Canal(q.y)||q.y<-40||q.y>65)?"Bamboo":"RoadTree";if(kind=="RoadTree"&&treeCount>=38)continue;if(kind=="Bamboo"&&bambooCount>=28)continue;
                    if(!gc.Raycast(new Ray(p+Vector3.up*5,Vector3.down),out var hit,10))continue;p.y=hit.point.y;RoadsideModel(kind,p,kind=="Bamboo"?Rand(.78f,1.05f):Rand(.85f,1.15f),root);planted.Add(p);newPlants.Add(p);if(kind=="Bamboo"){bambooCount++;bambooPositions.Add(p);}else treeCount++;
                }
                // Broken hedgerows follow parcel edges; crossing points and the walked road remain open.
                var hedges=new List<Vector3>();
                foreach(var q in candidates){int id;float inset=VillageParcel(q,out id);float road=RoundedRoad(q);if(!RoadsideDry(q)||inset<-.82f||inset>.28f||road<1.4f||Mathf.PerlinNoise(q.x*.043f+91,q.y*.043f+177)<.50f)continue;var p=Rural(new Vector3(q.x,1,q.y));if(!clear(p,.85f)||hedges.Any(v=>Vector3.Distance(v,p)<1.45f)||newPlants.Any(v=>Vector3.Distance(v,p)<2.1f))continue;if(!gc.Raycast(new Ray(p+Vector3.up*5,Vector3.down),out var hit,10))continue;p.y=hit.point.y;RoadsideModel("Hedge",p,Rand(.70f,1.08f),root);hedges.Add(p);hedgeCount++;}
                // Crop roots follow the revised continuous surface and avoid new roadside clumps.
                int rice=0;foreach(var ins in env.GetComponentsInChildren<VillageRiceInstances>()){var kept=new List<Vector4>();var sizes=new List<float>();for(int i=0;i<ins.plants.Length;i++){var p=ins.plants[i];var w=ins.transform.TransformPoint(new Vector3(p.x,p.y,p.z));var q=Unwarp(w);int id;if(VillageParcel(new Vector2(q.x,q.z),out id)<.35f||newPlants.Any(v=>Vector2.Distance(new Vector2(v.x,v.z),new Vector2(w.x,w.z))<1.8f))continue;if(!gc.Raycast(new Ray(new Vector3(w.x,5,w.z),Vector3.down),out var hit,10))continue;w.y=hit.point.y+.008f;var local=ins.transform.InverseTransformPoint(w);kept.Add(new Vector4(local.x,local.y,local.z,p.w));sizes.Add(ins.sizes[i]);}ins.plants=kept.ToArray();ins.sizes=sizes.ToArray();ins.Rebuild();EditorUtility.SetDirty(ins);rice+=kept.Count;}
                int grassTotal=0;var buckets=new Dictionary<Vector2Int,List<Vector4>>();
                for(float x=-107;x<108;x+=.32f)for(float z=-96;z<97;z+=.32f){var q=new Vector2(x+Rand(-.11f,.11f),z+Rand(-.11f,.11f));int id;float inset=VillageParcel(q,out id);if(Mathf.Abs(q.x-Canal(q.y))<9.4f||inset>.05f||RoundedRoad(q)<.35f||new Vector2((q.x+52)/23f,(q.y-83)/15f).magnitude<1.02f)continue;var p=Rural(new Vector3(q.x,1,q.y));if(!clear(p,.3f)||Rand(0,1)>.91f)continue;if(!gc.Raycast(new Ray(p+Vector3.up*4,Vector3.down),out var hit,10))continue;p.y=hit.point.y+.007f;var key=new Vector2Int(Mathf.FloorToInt(p.x/20),Mathf.FloorToInt(p.z/20));if(!buckets.ContainsKey(key))buckets[key]=new List<Vector4>();buckets[key].Add(new Vector4(p.x-key.x*20,p.y,p.z-key.y*20,Rand(0,360)));grassTotal++;}
                foreach(var bucket in buckets){var go=Child("Dense Blender meadow "+bucket.Key,root);go.transform.position=new Vector3(bucket.Key.x*20,0,bucket.Key.y*20);var ins=go.AddComponent<VillageRiceInstances>();ins.detailedMesh=roadsideMeshes["Meadow_LOD0"];ins.distantMesh=roadsideMeshes["Meadow_LOD1"];ins.material=palette;ins.plants=bucket.Value.ToArray();ins.sizes=ins.plants.Select(_=>Rand(.70f,1.22f)).ToArray();ins.Rebuild();}
                foreach(var nav in env.GetComponents<NavMeshSurface>()){nav.RemoveData();Object.DestroyImmediate(nav);}Physics.SyncTransforms();var surface=env.gameObject.AddComponent<NavMeshSurface>();surface.collectObjects=CollectObjects.Children;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.overrideVoxelSize=true;surface.voxelSize=.2f;surface.BuildNavMesh();AssetDatabase.CreateAsset(surface.navMeshData,Root+"/Roadside_NavMesh.asset");
                var report=new List<string>{"Blender source: Map02_Roadside_Details.blend","Bamboo clumps: "+bambooCount,"Additional roadside trees: "+treeCount,"Bund hedgerow shrubs: "+hedgeCount,"Grass clumps before: "+oldCount,"Grass clumps after: "+grassTotal,"Rice clumps: "+rice};
                foreach(var target in homes.Select(h=>h.position-h.forward*9).Concat(new[]{Rural(new Vector3(35,1,-4)),Rural(new Vector3(65,1,-68))})){var path=new NavMeshPath();bool ok=NavMesh.SamplePosition(Rural(new Vector3(-64,1,11)),out var a,3,NavMesh.AllAreas)&&NavMesh.SamplePosition(target,out var b,3,NavMesh.AllAreas)&&NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;report.Add((ok?"PASS":"FAIL")+" route "+target);}
                if(grassTotal<oldCount*1.5f||bambooCount<10||treeCount<10||hedgeCount<30)report.Add("FAIL planting density target");
                AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);scene=EditorSceneManager.OpenScene(ScenePath);var saved=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<VillageRiceInstances>()).Where(r=>r.name.StartsWith("Dense Blender meadow")).Sum(r=>r.plants.Length);report.Add((saved==grassTotal?"PASS":"FAIL")+" saved meadow instances "+saved);File.WriteAllLines(Reports+"/roadside-checks.txt",report);
                var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).First();Capture(camera,new Vector3(0,205,-130),Vector3.zero,112,"map02-roadside-overview");Capture(camera,new Vector3(-105,65,-82),new Vector3(-65,1,10),53,"map02-roadside-village");if(bambooPositions.Count>0){var p=bambooPositions[0];Capture(camera,p+new Vector3(12,8,-15),p+Vector3.up*3,0,"map02-roadside-bamboo");}if(hedges.Count>0){var p=hedges[hedges.Count/2];Capture(camera,p+new Vector3(10,5,-13),p+Vector3.up,0,"map02-roadside-hedge");}
                if(report.Any(s=>s.StartsWith("FAIL")))throw new Exception("Roadside verification failed; see roadside-checks.txt.");Debug.Log("ROADSIDE_COMPLETE");
            }finally{useRoundedVillageRoads=false;}
        }
    }
}




