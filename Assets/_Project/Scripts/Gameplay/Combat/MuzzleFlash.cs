using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadowVale.Gameplay.Combat
{
    /// <summary>A brief, reusable flame burst attached to the actual barrel exit.</summary>
    [DefaultExecutionOrder(10010),DisallowMultipleComponent]
    public sealed class MuzzleFlash : MonoBehaviour
    {
        private const float Duration=.055f;
        private Mesh mesh;
        private Material material;
        private MeshRenderer flame;
        private Light glow;
        private bool pending;
        private float startedAt=-10;
        public bool Visible=>flame!=null && flame.enabled;

        private void Awake()
        {
            var shader=Resources.Load<Shader>("Combat/MuzzleFlash");
            if(shader==null)return;
            material=new Material(shader){name="Muzzle flame (runtime)"};
            mesh=new Mesh{name="Crossed muzzle flame planes"};
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d) {
                int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});
                uv.AddRange(new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)});
                triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            for(int i=0;i<4;i++) {
                var right=Quaternion.AngleAxis(i*45,Vector3.forward)*Vector3.right;
                Quad(-right*.10f+Vector3.forward*.005f,right*.10f+Vector3.forward*.005f,
                    right*.12f+Vector3.forward*.22f,-right*.12f+Vector3.forward*.22f);
            }
            for(int i=0;i<4;i++) {
                var side=Quaternion.AngleAxis(i*90,Vector3.forward)*Vector3.right;
                var across=Vector3.Cross(side,Vector3.forward)*.035f;
                var start=Vector3.forward*.015f;var end=side*.11f+Vector3.forward*.055f;
                Quad(start-across,start+across,end+across,end-across);
            }
            // Also visible when the viewer looks almost straight down the barrel.
            Quad(new Vector3(-.055f,-.055f,.055f),new Vector3(.055f,-.055f,.055f),new Vector3(.055f,.055f,.055f),new Vector3(-.055f,.055f,.055f));
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            var filter=GetComponent<MeshFilter>();if(filter==null)filter=gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh=mesh;
            flame=GetComponent<MeshRenderer>();if(flame==null)flame=gameObject.AddComponent<MeshRenderer>();flame.sharedMaterial=material;
            flame.shadowCastingMode=ShadowCastingMode.Off;flame.receiveShadows=false;flame.enabled=false;
            glow=GetComponent<Light>();if(glow==null)glow=gameObject.AddComponent<Light>();glow.type=LightType.Point;glow.color=new Color(1,.55f,.16f);
            glow.range=1.25f;glow.intensity=0;glow.shadows=LightShadows.None;glow.enabled=false;
        }

        public void Play()=>pending=true;

        private void LateUpdate()
        {
            if(flame==null)return;
            if(pending) {
                pending=false;startedAt=Time.time;
                material.SetFloat("_Seed",Random.value*100);
            }
            float fade=Mathf.Clamp01(1-(Time.time-startedAt)/Duration);
            flame.enabled=fade>0;
            material.SetFloat("_Intensity",fade);
            glow.enabled=fade>0;glow.intensity=.1f*fade;
        }

        private void OnDisable(){pending=false;startedAt=-10;if(flame!=null)flame.enabled=false;if(glow!=null)glow.enabled=false;}
        private void OnDestroy(){if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
    }
}
