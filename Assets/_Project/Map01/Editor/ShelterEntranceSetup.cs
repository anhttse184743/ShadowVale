using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    /// <summary>Replaces the coarse entrance cut with a sealed, textured earth trench.</summary>
    public static class ShelterEntranceSetup
    {
        private const string Folder = "Assets/_Project/Art/Environment/LayoutTest/";
        private const string PrefabPath = Folder + "Prefabs/ShelterShell.prefab";
        private const float FrontZ = -69.25f, BackZ = -85f;
        private static readonly float[] Cross = {-4,-3.5f,-2.9f,-2.3f,-1.85f,-1.55f,-1.42f,-1,-.5f,0,.5f,1,1.42f,1.55f,1.85f,2.3f,2.9f,3.5f,4};

        [MenuItem("ShadowVale/Environment/Rebuild natural shelter entrance")]
        public static void Bake()
        {
            if(Application.isPlaying || EditorSceneManager.GetActiveScene().path!="Assets/_Project/Scenes/Maps/Map 1.unity")
                throw new InvalidOperationException("Open Map 1 outside Play mode before rebuilding its entrance.");
            var shelter=GameObject.Find("ShelterShell");var ground=GameObject.Find("Continuous hills — 220 x 200");
            if(shelter==null||ground==null)throw new InvalidOperationException("Shelter and terrain are required.");
            var source=ground.GetComponent<MeshFilter>().sharedMesh;
            var grid=new Dictionary<Vector2Int,float>();
            foreach(var vertex in source.vertices) {
                var p=ground.transform.TransformPoint(vertex);
                grid[new Vector2Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.z))]=p.y;
            }
            float Terrain(float x,float z) {
                int ix=Mathf.FloorToInt(x),iz=Mathf.FloorToInt(z);
                return Mathf.Lerp(Mathf.Lerp(grid[new Vector2Int(ix,iz)],grid[new Vector2Int(ix+1,iz)],x-ix),
                    Mathf.Lerp(grid[new Vector2Int(ix,iz+1)],grid[new Vector2Int(ix+1,iz+1)],x-ix),z-iz);
            }
            float Floor(float z) {
                if(z>=-69.7f)return .192f;
                if(z>=-70f) {
                    float t=Mathf.InverseLerp(-69.7f,-70,z);
                    return Mathf.Lerp(.192f,Terrain(-65,-70),t*t*(2-t));
                }
                // Hermite interpolation keeps the existing route heights, with continuous slopes.
                int a=Mathf.FloorToInt(z);float t0=z-a;
                float h0=Terrain(-65,a),h1=Terrain(-65,a+1);
                float m0=(h1-Terrain(-65,a-1))*.5f,m1=(Terrain(-65,a+2)-h0)*.5f;
                if(a+1>=-70)m1=-.302f;
                return (2*t0*t0*t0-3*t0*t0+1)*h0+(t0*t0*t0-2*t0*t0+t0)*m0
                    +(-2*t0*t0*t0+3*t0*t0)*h1+(t0*t0*t0-t0*t0)*m1;
            }
            float Profile(float x,float z) {
                if(Mathf.Abs(z-BackZ)<.001f)return Terrain(-65+x,BackZ);
                float floor=Floor(z),blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-81,-84,z));
                float bank=Mathf.Clamp01((Mathf.Abs(x)-1.42f)/(4-1.42f));
                float edge=Terrain(-65+Mathf.Sign(x)*4,Mathf.Min(z,-70));
                float y=Mathf.Lerp(floor,edge,Mathf.Sin(Mathf.Pow(bank,.62f)*Mathf.PI*.5f));
                y+=Mathf.Sin(z*2.1f+x*3)*Mathf.Sin(bank*Mathf.PI)*.065f*(1-blend);
                return Mathf.Lerp(y,Terrain(-65+x,Mathf.Min(z,-70)),blend);
            }
            var material=MakeSoilMaterial(ground.GetComponent<Renderer>().sharedMaterial,Profile);
            var prefab=PrefabUtility.LoadPrefabContents(PrefabPath);
            try {
                var previous=prefab.transform.Find("Natural entrance");if(previous!=null)Object.DestroyImmediate(previous.gameObject);
                var root=new GameObject("Natural entrance");root.transform.SetParent(prefab.transform,false);
                var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
                void Vertex(Vector3 p) {vertices.Add(shelter.transform.InverseTransformPoint(p));uv.Add(new Vector2((p.x+69)/8,(p.z-BackZ)/(FrontZ-BackZ)));}
                const int rows=64;int columns=Cross.Length;
                for(int row=0;row<=rows;row++) {
                    float z=Mathf.Lerp(BackZ,FrontZ,row/(float)rows);
                    for(int col=0;col<columns;col++) {
                        float x=Cross[col],y=Profile(x,z);
                        Vertex(new Vector3(-65+x,y,z));
                        if(row<rows&&col<columns-1) {
                            int i=row*columns+col;
                            triangles.AddRange(new[]{i,i+columns,i+columns+1,i,i+columns+1,i+1});
                        }
                    }
                    // Unwrap along the actual surface distance rather than projecting
                    // downwards, which stretches earth into streaks on steep banks.
                    var arc=new float[columns];
                    for(int c=1;c<columns;c++)arc[c]=arc[c-1]+Vector3.Distance(vertices[row*columns+c-1],vertices[row*columns+c]);
                    for(int c=0;c<columns;c++)uv[row*columns+c]=new Vector2(arc[c]/arc[columns-1],row/(float)rows);
                }
                // Seal every edge with a buried skirt, including the threshold. Backfaces
                // can no longer expose the sky through the missing terrain triangles.
                var perimeter=new List<int>();
                for(int c=0;c<columns;c++)perimeter.Add(c);
                for(int r=1;r<=rows;r++)perimeter.Add(r*columns+columns-1);
                for(int c=columns-2;c>=0;c--)perimeter.Add(rows*columns+c);
                for(int r=rows-1;r>0;r--)perimeter.Add(r*columns);
                for(int p=0;p<perimeter.Count;p++) {
                    Vector3 a=vertices[perimeter[p]],b=vertices[perimeter[(p+1)%perimeter.Count]];
                    int n=vertices.Count;vertices.AddRange(new[]{a,b,b-Vector3.up*.8f,a-Vector3.up*.8f});
                    uv.AddRange(new[]{uv[perimeter[p]],uv[perimeter[(p+1)%perimeter.Count]],uv[perimeter[(p+1)%perimeter.Count]],uv[perimeter[p]]});
                    triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
                }
                var mesh=new Mesh{name="Sealed organic entrance",vertices=vertices.ToArray(),uv=uv.ToArray(),triangles=triangles.ToArray()};
                mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();
                var stored=Store(mesh,Folder+"Meshes/ShelterEntranceTerrain.asset");
                root.AddComponent<MeshFilter>().sharedMesh=stored;root.AddComponent<MeshRenderer>().sharedMaterial=material;
                root.AddComponent<MeshCollider>().sharedMesh=stored;
                AddRocks(root.transform,shelter.transform,Floor,Terrain);
                PrefabUtility.SaveAsPrefabAsset(prefab,PrefabPath);
            }finally{PrefabUtility.UnloadPrefabContents(prefab);}

            // Remove only the coarse surface replaced by the prefab. Keep the height-grid
            // vertices intact so subsequent bakes can sample the original route again.
            var points=source.vertices;var old=source.triangles;var kept=new List<int>(old.Length);
            for(int i=0;i<old.Length;i+=3) {
                var p=ground.transform.TransformPoint((points[old[i]]+points[old[i+1]]+points[old[i+2]])/3);
                if(p.x>-69&&p.x<-61&&p.z>BackZ&&p.z<-70)continue;
                kept.AddRange(new[]{old[i],old[i+1],old[i+2]});
            }
            source.triangles=kept.ToArray();EditorUtility.SetDirty(source);
            var collider=ground.GetComponent<MeshCollider>();collider.sharedMesh=null;collider.sharedMesh=source;
            foreach(var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if(renderer.name=="Ramp anti-slip timber"&&Mathf.Abs(renderer.transform.position.x+65)<2&&renderer.transform.position.z<-69&&renderer.transform.position.z>-85) {
                    renderer.enabled=false;EditorUtility.SetDirty(renderer);
                }
            AssetDatabase.SaveAssets();Physics.SyncTransforms();
            var surface=Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None).First();
            var previousData=surface.navMeshData;string navPath=AssetDatabase.GetAssetPath(previousData);
            if(string.IsNullOrEmpty(navPath))throw new InvalidOperationException("The navigation asset must be saved before rebuilding.");
            surface.BuildNavMesh();var built=surface.navMeshData;surface.RemoveData();
            EditorUtility.CopySerialized(built,previousData);surface.navMeshData=previousData;surface.AddData();Object.DestroyImmediate(built);
            EditorUtility.SetDirty(previousData);EditorUtility.SetDirty(surface);AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Map01MinimapSurveySetup.Bake();
        }

        private static void AddRocks(Transform root,Transform shelter,Func<float,float> floor,Func<float,float,float> terrain)
        {
            var assets=new[]{"05_MossBoulder","05_MossBoulder","01_BroadBoulder"}.Select(name=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Environment/AnGiangRocks/Prefabs/"+name+".prefab")).ToArray();
            var random=new System.Random(7361);
            for(int side=-1;side<=1;side+=2)for(int i=0;i<9;i++) {
                float z=-70.15f-i*1.45f-(float)random.NextDouble()*.5f,x=side*(1.75f+(float)random.NextDouble()*.95f);
                float u=Mathf.Clamp01((Mathf.Abs(x)-1.42f)/(4-1.42f));
                float h=Mathf.Lerp(floor(z),terrain(-65+side*4,Mathf.Min(z,-70)),Mathf.Sin(Mathf.Pow(u,.62f)*Mathf.PI*.5f));
                var rock=(GameObject)PrefabUtility.InstantiatePrefab(assets[random.Next(assets.Length)],root.gameObject.scene);rock.name="Embedded entrance rock "+side+" "+i;rock.transform.SetParent(root,false);
                var renderers=rock.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                float size=.32f+(float)random.NextDouble()*.46f,scale=size/Mathf.Max(bounds.size.x,bounds.size.z);
                rock.transform.localScale=Vector3.one*scale;rock.transform.localRotation=Quaternion.Euler(i*13%25,i*71+side*40,side*(i*9%17));
                rock.transform.localPosition=shelter.InverseTransformPoint(new Vector3(-65+x,h-size*.3f,z));
                foreach(var mf in rock.GetComponentsInChildren<MeshFilter>())if(mf.name=="LOD0"&&mf.GetComponent<Collider>()==null)mf.gameObject.AddComponent<MeshCollider>().sharedMesh=mf.sharedMesh;
            }
        }

        private static Material MakeSoilMaterial(Material ground,Func<float,float,float> profile)
        {
            const int w=512,h=1024;var pixels=new Color[w*h];var heights=new float[w*h];
            var forest=(Texture2D)ground.GetTexture("_BaseMap");
            for(int y=0;y<h;y++) {
                float z=BackZ+y*(FrontZ-BackZ)/(h-1);var arc=new float[Cross.Length];
                for(int c=1;c<Cross.Length;c++) {
                    float dx=Cross[c]-Cross[c-1],dy=profile(Cross[c],z)-profile(Cross[c-1],z);
                    arc[c]=arc[c-1]+Mathf.Sqrt(dx*dx+dy*dy);
                }
                int segment=0;float length=arc[arc.Length-1];
                for(int x=0;x<w;x++) {
                float distance=x*length/(w-1);
                while(segment<Cross.Length-2&&distance>arc[segment+1])segment++;
                float wx=-65+Mathf.Lerp(Cross[segment],Cross[segment+1],Mathf.InverseLerp(arc[segment],arc[segment+1],distance));
                float across=distance-length*.5f;
                float fine=Mathf.PerlinNoise(across*32+91,z*32+43),grain=Mathf.PerlinNoise(across*7+51,z*7+68),patch=Mathf.PerlinNoise(across*1.8f+13,z*1.8f+29);
                float tone=.66f+fine*.26f+grain*.22f+patch*.13f;
                var earth=new Color(.32f,.264f,.18f)*tone;
                float side=Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.5f,4,Mathf.Abs(wx+65)));
                float lip=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-81,-84,z));
                pixels[y*w+x]=Color.Lerp(earth,forest.GetPixelBilinear((wx+110)/220,(z+100)/200),Mathf.Max(side,lip));
                heights[y*w+x]=Mathf.Lerp(fine*.75f+grain*.25f,.5f,Mathf.Max(side,lip));
                }
            }
            var normal=new Color[w*h];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++) {
                float dx=heights[y*w+Mathf.Min(w-1,x+1)]-heights[y*w+Mathf.Max(0,x-1)];
                float dy=heights[Mathf.Min(h-1,y+1)*w+x]-heights[Mathf.Max(0,y-1)*w+x];
                var n=new Vector3(-dx*2,-dy*2,1).normalized;normal[y*w+x]=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f);
            }
            string albedo=Folder+"Materials/EntranceSoil.png",normals=Folder+"Materials/EntranceSoilNormal.png";
            SaveTexture(albedo,w,h,pixels,false);SaveTexture(normals,w,h,normal,true);
            string path=Folder+"Materials/EntranceSoil.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",Color.white);material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(albedo));
            material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normals));material.SetFloat("_BumpScale",.38f);material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Smoothness",.04f);material.enableInstancing=true;EditorUtility.SetDirty(material);return material;
        }
        private static void SaveTexture(string path,int w,int h,Color[] pixels,bool normal)
        {
            var image=new Texture2D(w,h,TextureFormat.RGB24,true,normal);image.SetPixels(pixels);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.wrapMode=TextureWrapMode.Clamp;importer.anisoLevel=8;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.SaveAndReimport();
        }
        private static Mesh Store(Mesh mesh,string path)
        {
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);return existing;
        }
    }
}
