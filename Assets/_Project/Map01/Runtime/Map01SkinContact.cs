using System.Collections.Generic;
using UnityEngine;
namespace ShadowVale.Map01
{
    /// <summary>Lowest evaluated skin contact from current bone matrices. No GPU skin cache or frame delay.</summary>
    internal sealed class Map01SkinContact
    {
        private sealed class Skin
        {
            public Transform[] bones;
            public Matrix4x4[] matrices;
            public Map01SkinContactData.Vertex[] vertices;
        }
        private readonly List<Skin> skins=new();
        public Map01SkinContact(Animator actor)
        {
            var saved=Resources.Load<Map01SkinContactData>("Rescue/SkinContactData");
            foreach(var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh=renderer.sharedMesh;if(mesh==null)continue;
                Map01SkinContactData.Vertex[] cached=null;
                if(saved!=null)foreach(var profile in saved.skins)if(profile.mesh==mesh){cached=profile.vertices;break;}
                if(cached!=null){skins.Add(new Skin{bones=renderer.bones,matrices=new Matrix4x4[renderer.bones.Length],vertices=cached});continue;}
                if(!mesh.isReadable)continue;
                var source=mesh.vertices;var weights=mesh.boneWeights;var bind=mesh.bindposes;var bones=renderer.bones;
                if(weights.Length!=source.Length||bind.Length==0)continue;
                var points=new Map01SkinContactData.Vertex[source.Length];
                for(int i=0;i<points.Length;i++)
                {
                    var w=weights[i];var p=source[i];
                    points[i]=new Map01SkinContactData.Vertex{a=w.boneIndex0,b=w.boneIndex1,c=w.boneIndex2,d=w.boneIndex3,
                        wa=w.weight0,wb=w.weight1,wc=w.weight2,wd=w.weight3,
                        pa=bind[w.boneIndex0].MultiplyPoint3x4(p),pb=bind[w.boneIndex1].MultiplyPoint3x4(p),
                        pc=bind[w.boneIndex2].MultiplyPoint3x4(p),pd=bind[w.boneIndex3].MultiplyPoint3x4(p)};
                }
                skins.Add(new Skin{bones=bones,matrices=new Matrix4x4[bones.Length],vertices=points});
            }
        }
        public float LowestY()
        {
            float lowest=float.PositiveInfinity;
            foreach(var skin in skins)
            {
                for(int i=0;i<skin.bones.Length;i++)skin.matrices[i]=skin.bones[i]!=null?skin.bones[i].localToWorldMatrix:Matrix4x4.identity;
                foreach(var v in skin.vertices)
                {
                    float y=v.wa*Y(skin.matrices[v.a],v.pa);
                    if(v.wb>0)y+=v.wb*Y(skin.matrices[v.b],v.pb);
                    if(v.wc>0)y+=v.wc*Y(skin.matrices[v.c],v.pc);
                    if(v.wd>0)y+=v.wd*Y(skin.matrices[v.d],v.pd);
                    lowest=Mathf.Min(lowest,y);
                }
            }
            return lowest;
        }
        private static float Y(Matrix4x4 m,Vector3 p)=>m.m10*p.x+m.m11*p.y+m.m12*p.z+m.m13;
    }
}
