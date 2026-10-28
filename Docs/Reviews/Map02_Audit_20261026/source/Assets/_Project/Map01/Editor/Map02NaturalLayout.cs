using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Unity.AI.Navigation;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Editor
{
    public static partial class Map02VillageBuilder
    {
        // A continuous, boundary-preserving deformation keeps paths, banks and fields aligned.
        static Vector3 Rural(Vector3 p)
        {
            float fade=Mathf.SmoothStep(0,1,Mathf.Min((110-Mathf.Abs(p.x))/15,(100-Mathf.Abs(p.z))/13));
            return p+new Vector3((6.5f*Mathf.Sin(p.z*.053f+p.x*.019f)+2.3f*Mathf.Sin(p.z*.111f))*fade,0,(7*Mathf.Sin(p.x*.041f+p.z*.022f)+2*Mathf.Sin(p.x*.093f))*fade);
        }
        static float RuralAngle(Vector3 p){var d=Rural(p+Vector3.forward*.1f)-Rural(p);return Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;}
        static int ruralMeshId;
        static Mesh RuralMesh(Mesh source,Transform owner,string name)
        {
            var m=Object.Instantiate(source);var v=m.vertices;
            for(int i=0;i<v.Length;i++)v[i]=owner.InverseTransformPoint(Rural(owner.TransformPoint(v[i])));
            m.vertices=v;m.RecalculateNormals();m.RecalculateBounds();return SaveMesh(m,"Natural_"+name);
        }
        static void RuralObject(Transform t,bool turn=true)
        {
            var p=t.position;t.position=Rural(p);if(turn)t.rotation=Quaternion.Euler(0,RuralAngle(p),0)*t.rotation;
            if(PrefabUtility.IsPartOfPrefabInstance(t))PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }
        // Tessellated ribbons replace long cube roads and rectangular bunds, so curves are continuous.
        static void RuralSurface(Transform t,string label, bool earth=false)
        {
            var size=t.lossyScale;int nx=Mathf.Max(1,Mathf.CeilToInt(Mathf.Abs(size.x)/1.5f)),nz=Mathf.Max(1,Mathf.CeilToInt(Mathf.Abs(size.z)/1.5f));
            var v=new List<Vector3>();var c=new List<Color>();var triangles=new List<int>();
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)
            {
                var p=t.TransformPoint(new Vector3((float)x/nx-.5f,.5f,(float)z/nz-.5f));p=Rural(p);v.Add(p);
                float grain=Mathf.PerlinNoise(p.x*.23f,p.z*.23f);c.Add(earth?Color.Lerp(new Color(.38f,.29f,.15f),new Color(.57f,.45f,.25f),grain):Color.white);
                if(x>0&&z>0){int i=z*(nx+1)+x;triangles.AddRange(new[]{i-nx-2,i,i-nx-1,i-nx-2,i-1,i});}
            }
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.SetVertices(v);mesh.SetColors(c);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh=SaveMesh(mesh,"Natural_Surface_"+ruralMeshId++);
            var go=Model(label,mesh,Vector3.zero,t.parent,earth?palette:t.GetComponent<MeshRenderer>().sharedMaterial);go.transform.position=Vector3.zero;
            Object.DestroyImmediate(t.gameObject);
        }
        static void RuralPatch(Transform parent,Vector3 center,float rx,float rz,Material mat,string label,float yaw=0)
        {
            var v=new List<Vector3>{center};var tri=new List<int>();var colors=new List<Color>{Color.white};
            for(int i=0;i<=40;i++){float a=i*Mathf.PI*2/40;float wobble=1+.07f*Mathf.Sin(a*3+center.x)+.04f*Mathf.Cos(a*7);v.Add(center+Quaternion.Euler(0,yaw,0)*new Vector3(Mathf.Cos(a)*rx*wobble,0,Mathf.Sin(a)*rz*wobble));colors.Add(Color.white);if(i>0)tri.AddRange(new[]{0,i+1,i});}
            var m=new Mesh();m.SetVertices(v);m.SetColors(colors);m.SetTriangles(tri,0);m.RecalculateNormals();Model(label,SaveMesh(m,"Natural_Patch_"+ruralMeshId++),Vector3.zero,parent,mat);
        }
        [MenuItem("ShadowVale/Map 2/Naturalize village and field paths")]
        public static void Naturalize()
        {
            Directory.CreateDirectory(Reports);var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!=ScenePath){if(scene.isDirty)throw new Exception("Save current scene before editing Map 2.");scene=EditorSceneManager.OpenScene(ScenePath);}
            env=scene.GetRootGameObjects().First(g=>g.name.StartsWith("01 Environment")).transform;
            if(env.Find("Natural village gardens")!=null)throw new Exception("Natural layout already applied; restore the pre-natural backup to regenerate.");
            EditorSceneManager.SaveScene(scene,Reports+"/Map2_before_natural_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
            palette=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Map01_Optimized/Materials/MapPalette.mat");soil=Mat("Natural sunlit earth",new Color(.48f,.37f,.20f));wood=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Timber.mat");
            rng=new System.Random(23745);ruralMeshId=0;
            foreach(var s in env.GetComponents<NavMeshSurface>()){s.RemoveData();Object.DestroyImmediate(s);}
            foreach(var label in new[]{"Continuous ground with meandering canal","Winding canal water"}){var t=env.Find(label);var f=t.GetComponent<MeshFilter>();f.sharedMesh=RuralMesh(f.sharedMesh,t,label=="Winding canal water"?"Canal":"Terrain");var col=t.GetComponent<MeshCollider>();if(col!=null)col.sharedMesh=f.sharedMesh;}
            var roads=env.Find("Earth roads linking homes between paddies");foreach(var t in roads.Cast<Transform>().ToArray())RuralSurface(t,"Winding "+t.name,true);
            var village=env.Cast<Transform>().First(t=>t.name.StartsWith("Village •"));
            var houses=village.Cast<Transform>().Where(t=>t.name.StartsWith("Stilt house")||t.name.StartsWith("Bamboo thatch house")).ToArray();
            var originalHomes=houses.Select(t=>t.position).ToArray();
            foreach(var t in village.Cast<Transform>().ToArray())
            {
                RuralObject(t);
                if(houses.Contains(t)){t.Rotate(0,Rand(-19,19),0,Space.World);PrefabUtility.RecordPrefabInstancePropertyModifications(t);}
            }
            var fields=env.Find("Rice fields surrounding individual homes");Object.DestroyImmediate(fields.Find("Golden green rice rows").gameObject);
            var crop=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Rice_Clump.asset");var cropParts=new List<CombineInstance>();int fieldId=0;
            foreach(var plot in fields.Cast<Transform>().ToArray())
            {
                var surface=plot.Find("Paddy soil");float w=surface.lossyScale.x,d=surface.lossyScale.z;var center=surface.position;
                foreach(var t in plot.Cast<Transform>().ToArray())
                {
                    if(t.name=="Dry homestead island"){Object.DestroyImmediate(t.gameObject);continue;}
                    RuralSurface(t,t.name=="Bund"?"Uneven planted bund":"Irregular rice parcel",t.name=="Bund");
                }
                float spacing=.88f+fieldId%3*.10f;
                for(float x=-w/2+1;x<w/2-1;x+=spacing)for(float z=-d/2+1;z<d/2-1;z+=.95f)
                {
                    var old=new Vector3(center.x+x,1.06f,center.z+z);var pos=Rural(old);bool clear=false;
                    foreach(var house in houses){var q=house.InverseTransformPoint(new Vector3(pos.x,house.position.y,pos.z));if(q.x*q.x/64+q.z*q.z/110<1.1f){clear=true;break;}}
                    // Keep the approach from each southern doorway open.
                    foreach(var home in originalHomes)if(Mathf.Abs(old.x-home.x)<1.7f&&old.z<home.z-4&&old.z>home.z-17)clear=true;
                    if(clear||rng.NextDouble()<.045)continue;
                    pos.x+=Rand(-.17f,.17f);pos.z+=Rand(-.15f,.15f);
                    cropParts.Add(new CombineInstance{mesh=crop,transform=Matrix4x4.TRS(pos,Quaternion.Euler(0,Rand(0,360),0),Vector3.one*Rand(.95f,1.5f))});
                }
                fieldId++;
            }
            // Bound each text-serialized mesh below GitHub's 100 MiB file limit.
            // Keep every part under one root so the botanical replacement removes all parts.
            int clumpsPerMesh=Mathf.Max(1,900000/crop.vertexCount);
            Transform riceRoot=null;
            for(int offset=0,part=0;offset<cropParts.Count;offset+=clumpsPerMesh,part++)
            {
                var riceMesh=new Mesh{indexFormat=IndexFormat.UInt32};
                riceMesh.CombineMeshes(cropParts.GetRange(offset,Mathf.Min(clumpsPerMesh,cropParts.Count-offset)).ToArray());
                string meshName=part==0?"Natural_Rice":"Natural_Rice_Part"+(part+1).ToString("D2");
                var rice=Model(part==0?"Irregular dense rice rows":"Rice part "+(part+1).ToString("D2"),SaveMesh(riceMesh,meshName),Vector3.zero,part==0?fields:riceRoot,palette);
                rice.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
                if(part==0)riceRoot=rice.transform;
            }
            foreach(var t in env.Cast<Transform>().Where(t=>t.name.Contains("bridge")).ToArray())RuralObject(t);
            var details=env.Find("Orchards palms and reference landmarks");
            foreach(var t in details.Cast<Transform>().ToArray())
            {
                if(t.name=="Northern helicopter landing clearing"){var p=Rural(t.position);Object.DestroyImmediate(t.gameObject);RuralPatch(details,new Vector3(p.x,1.055f,p.z),21,11,soil,"Grassy irregular landing clearing");}
                else RuralObject(t);
            }
            markers=scene.GetRootGameObjects().First(g=>g.name.StartsWith("02 Story")).transform;foreach(var t in markers.Cast<Transform>())RuralObject(t,false);
            var gardens=Child("Natural village gardens",env).transform;RuralGardens(gardens,houses);
            // Use scene-local materials and lighting; shared Map 1 assets are untouched.
            var sun=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>()).First(l=>l.type==LightType.Directional);sun.transform.rotation=Quaternion.Euler(43,-32,0);sun.intensity=1.25f;sun.color=new Color(1,.93f,.77f);sun.shadows=LightShadows.Soft;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.50f,.60f,.67f);RenderSettings.ambientEquatorColor=new Color(.34f,.39f,.28f);RenderSettings.ambientGroundColor=new Color(.19f,.22f,.13f);
            Physics.SyncTransforms();var nav=env.gameObject.AddComponent<NavMeshSurface>();nav.collectObjects=CollectObjects.Children;nav.useGeometry=NavMeshCollectGeometry.PhysicsColliders;nav.overrideVoxelSize=true;nav.voxelSize=.2f;nav.BuildNavMesh();
            var path=Root+"/Natural_NavMesh.asset";var previous=AssetDatabase.LoadAssetAtPath<NavMeshData>(path);if(previous!=null)AssetDatabase.DeleteAsset(path);AssetDatabase.CreateAsset(nav.navMeshData,path);
            var checks=new List<string>();
            foreach(var target in new[]{new Vector3(32,1,-4),new Vector3(64,1,-68),new Vector3(-56,1,60)}.Concat(originalHomes.Select(p=>new Vector3(p.x,1,p.z-12))))
            {
                var np=new NavMeshPath();bool ok=NavMesh.SamplePosition(Rural(new Vector3(-100,1,-4)),out var a,3,NavMesh.AllAreas)&&NavMesh.SamplePosition(Rural(target),out var b,3,NavMesh.AllAreas)&&NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,np)&&np.status==NavMeshPathStatus.PathComplete;checks.Add((ok?"PASS":"FAIL")+" route "+Rural(target));
            }
            checks.Add("Homes: "+houses.Length+"; paddies: "+fieldId+"; rice clumps: "+cropParts.Count);File.WriteAllLines(Reports+"/natural-checks.txt",checks);
            var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).First();camera.transform.position=new Vector3(18,175,-200);camera.transform.LookAt(new Vector3(0,0,8));camera.orthographicSize=108;
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            Capture(camera,new Vector3(18,175,-200),new Vector3(0,0,8),108,"map02-natural-overview");Capture(camera,new Vector3(-115,53,-82),new Vector3(-54,1,4),0,"map02-natural-village");Capture(camera,new Vector3(22,21,-42),new Vector3(-45,3,1),0,"map02-natural-canal");
            if(checks.Any(s=>s.StartsWith("FAIL")))throw new Exception("Natural layout navigation failed; see natural-checks.txt");
        }
        static Mesh RuralTreeMesh()
        {
            var parts=new List<CombineInstance>();var colors=new List<Color>();var clones=new List<Mesh>();
            Action<PrimitiveType,Vector3,Vector3,Quaternion,Color> add=(type,p,scale,rotation,color)=>{var temp=GameObject.CreatePrimitive(type);var m=Object.Instantiate(temp.GetComponent<MeshFilter>().sharedMesh);m.colors=Enumerable.Repeat(color,m.vertexCount).ToArray();clones.Add(m);parts.Add(new CombineInstance{mesh=m,transform=Matrix4x4.TRS(p,rotation,scale)});Object.DestroyImmediate(temp);};
            var bark=new Color(.30f,.26f,.17f);add(PrimitiveType.Cylinder,new Vector3(0,3.5f,0),new Vector3(.85f,3.5f,.85f),Quaternion.Euler(0,0,-5),bark);
            for(int i=0;i<8;i++){float a=i*Mathf.PI*2/8;var tip=new Vector3(Mathf.Cos(a)*3.5f,7+Rand(-1,2),Mathf.Sin(a)*3.5f);var start=new Vector3(0,4,0);add(PrimitiveType.Cylinder,(start+tip)*.5f,new Vector3(.27f,(tip-start).magnitude*.5f,.27f),Quaternion.FromToRotation(Vector3.up,tip-start),bark);}
            for(int i=0;i<22;i++){float a=i*2.4f,r=Rand(.7f,4.7f);var p=new Vector3(Mathf.Cos(a)*r,8+Rand(-1,2),Mathf.Sin(a)*r);add(PrimitiveType.Sphere,p,new Vector3(Rand(3.4f,5.2f),Rand(2.7f,4.2f),Rand(3.2f,4.8f)),Quaternion.Euler(Rand(-20,20),Rand(0,360),0),Color.Lerp(new Color(.16f,.29f,.065f),new Color(.40f,.48f,.12f),Rand(0,1)));}
            var result=new Mesh{indexFormat=IndexFormat.UInt32};result.CombineMeshes(parts.ToArray());foreach(var m in clones)Object.DestroyImmediate(m);return SaveMesh(result,"Natural_Shade_Tree");
        }
        static Mesh RuralHayMesh()
        {
            var v=new List<Vector3>();var colors=new List<Color>();var tri=new List<int>();int n=28;
            for(int j=0;j<=8;j++)for(int i=0;i<=n;i++){float a=i*2*Mathf.PI/n,y=j*.43f;float radius=j==8?.12f:1.6f*Mathf.Pow(1-j/9f,.53f);radius*=1+.045f*Mathf.Sin(i*7+j);v.Add(new Vector3(Mathf.Cos(a)*radius,y,Mathf.Sin(a)*radius));colors.Add(Color.Lerp(new Color(.48f,.34f,.10f),new Color(.77f,.62f,.25f),Rand(0,1)));if(i>0&&j>0){int k=j*(n+1)+i;tri.AddRange(new[]{k-n-2,k-n-1,k,k-n-2,k,k-1});}}
            var m=new Mesh();m.SetVertices(v);m.SetColors(colors);m.SetTriangles(tri,0);m.RecalculateNormals();return SaveMesh(m,"Natural_Haystack");
        }
        static void RuralGardens(Transform root,Transform[] houses)
        {
            var tree=RuralTreeMesh();var hay=RuralHayMesh();var banana=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Environment/Map01_Realism/Meshes/Banana_LOD1.asset");
            int i=0;foreach(var house in houses)
            {
                var p=house.position;p.y=1.075f;RuralPatch(root,p+house.forward*.8f,7.8f,9.4f,soil,"Soft-edged homestead yard",house.eulerAngles.y);
                var shade=house.position+house.right*(i%2==0?10:-10)+house.forward*7;shade.y=1;var t=Model("Homestead shade tree",tree,shade,root,palette);t.transform.localScale=Vector3.one*Rand(.65f,1);t.transform.rotation=Quaternion.Euler(0,Rand(0,360),0);ColliderBox(t.transform,"Shade tree trunk",new Vector3(0,3,0),new Vector3(.9f,6,.9f));
                for(int j=0;j<3;j++){var bp=p+house.right*(j%2==0?8:-8)+house.forward*Rand(3,9);bp.y=1;var b=Model("Banana garden clump",banana,bp,root,palette);b.transform.localScale=Vector3.one*Rand(.8f,1.3f);b.transform.rotation=Quaternion.Euler(0,Rand(0,360),0);}
                var hp=p+house.right*5.5f-house.forward*2;hp.y=1;var h=Model("Harvest haystack",hay,hp,root,palette);h.transform.localScale=Vector3.one*Rand(.7f,1.15f);ColliderBox(h.transform,"Haystack obstacle",new Vector3(0,1.3f,0),new Vector3(2.4f,2.6f,2.4f));
                // Short, incomplete fences avoid repeating a closed rectangle around every home.
                if(i%3!=1)for(int j=0;j<5;j++){var fp=p+house.right*(j*.8f-2)-house.forward*8;Box("Weathered garden fence post",root,fp+Vector3.up*.5f,new Vector3(.10f,1.1f,.10f),wood);if(j<4){var rail=Box("Bamboo garden rail",root,fp+house.right*.4f+Vector3.up*.65f,new Vector3(.9f,.07f,.07f),wood,false);rail.transform.rotation=house.rotation;}}
                i++;
            }
            // Groves have clusters and gaps rather than identical rows along every boundary.
            foreach(var seed in new[]{new Vector3(-37,1,38),new Vector3(-35,1,-42),new Vector3(22,1,73),new Vector3(48,1,-86),new Vector3(-86,1,81),new Vector3(-63,1,-86)})
                for(int j=0;j<4;j++){var p=Rural(seed+new Vector3(Rand(-7,7),0,Rand(-6,6)));var t=Model("Irregular orchard shade",tree,p,root,palette);t.transform.localScale=Vector3.one*Rand(.55f,1.1f);t.transform.rotation=Quaternion.Euler(0,Rand(0,360),0);ColliderBox(t.transform,"Orchard trunk",new Vector3(0,3,0),new Vector3(.9f,6,.9f));}
            var grass=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Environment/Map01_Optimized/Meshes/Prop_Grass2.asset");
            for(int j=0;j<180;j++){float z=Rand(-95,95);if(Mathf.Abs(z+4)<6||Mathf.Abs(z+68)<6||Mathf.Abs(z-62)<5||Mathf.Abs(z+82)<5)continue;var p=Rural(new Vector3(Canal(z)+(j%2==0?-1:1)*Rand(7.8f,9.0f),1,z));var g=Model("Patchy canal grasses",grass,p,root,palette);g.transform.localScale=Vector3.one*Rand(1.2f,2.4f);}
        }
    }
}
