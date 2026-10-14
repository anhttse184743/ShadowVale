using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
namespace ShadowVale.Map01
{
    /// <summary>
    /// Lowest evaluated skin contact from current bone matrices. No GPU skin cache or frame delay.
    /// A character is tens of thousands of samples and this is asked every frame, so the samples are
    /// kept in small patches of skin: a patch that cannot reach below the lowest point found so far is
    /// skipped whole. The answer is the one a pass over every sample gives (<see cref="LowestYUnculled"/>).
    /// </summary>
    internal sealed class Map01SkinContact
    {
        /// <summary>How far a sample's weights may be from summing to 1 and still be bounded by its bones.</summary>
        private const float WeightSlack=1e-4f;
        private const float FloorMargin=1e-3f;
        /// <summary>Small enough that a limb lying along the ground is not evaluated whole.</summary>
        private const int PatchSize=256;
        private sealed class Skin
        {
            public Transform[] bones;
            public Map01SkinContactData.Vertex[] vertices;
            public Layout layout;
            /// <summary>Per bone, this frame: the row of its matrix that gives world height.</summary>
            public Vector4[] rows;
            /// <summary>Per patch, this frame: none of its samples can be lower than this.</summary>
            public float[] floors;
        }
        /// <summary>Where one bone would carry a patch's samples: their bounds in that bone's bind space.</summary>
        private struct Reach
        {
            public int bone;
            public Vector3 centre,half;
        }
        /// <summary>How a mesh's samples are divided. It depends on the mesh alone, so actors share it.</summary>
        private sealed class Layout
        {
            public int boneCount;
            public int[] usedBones;
            /// <summary>Per patch: its samples, and a box for every bone that moves any of them.</summary>
            public int[][] members;
            public Reach[][] reaches;
            /// <summary>Samples whose weights are not a blend (they do not sum to 1): always evaluated.</summary>
            public int[] loose;
        }
        private static readonly ConditionalWeakTable<Map01SkinContactData.Vertex[],Layout> layouts=new();
        private readonly List<Skin> skins=new();
        /// <summary>All samples, and how many the last <see cref="LowestY"/> had to evaluate.</summary>
        public int Samples{get;private set;}
        public int Evaluated{get;private set;}
        public Map01SkinContact(Animator actor)
        {
            var saved=Resources.Load<Map01SkinContactData>("Rescue/SkinContactData");
            foreach(var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh=renderer.sharedMesh;if(mesh==null)continue;
                Map01SkinContactData.Vertex[] cached=null;
                if(saved!=null)foreach(var profile in saved.skins)if(profile.mesh==mesh){cached=profile.vertices;break;}
                if(cached!=null){Add(renderer.bones,cached);continue;}
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
                Add(bones,points);
            }
        }
        private void Add(Transform[] bones,Map01SkinContactData.Vertex[] vertices)
        {
            var layout=layouts.GetValue(vertices,BuildLayout);
            skins.Add(new Skin{bones=bones,vertices=vertices,layout=layout,rows=new Vector4[layout.boneCount],floors=new float[layout.members.Length]});
            Samples+=vertices.Length;
        }
        private static Layout BuildLayout(Map01SkinContactData.Vertex[] vertices)
        {
            int boneCount=0;
            foreach(var v in vertices)
            {
                boneCount=Mathf.Max(boneCount,v.a+1);
                if(v.wb>0)boneCount=Mathf.Max(boneCount,v.b+1);
                if(v.wc>0)boneCount=Mathf.Max(boneCount,v.c+1);
                if(v.wd>0)boneCount=Mathf.Max(boneCount,v.d+1);
            }
            var used=new bool[boneCount];var byMainBone=new List<int>[boneCount];var loose=new List<int>();
            for(int i=0;i<vertices.Length;i++)
            {
                var v=vertices[i];
                used[v.a]=true;if(v.wb>0)used[v.b]=true;if(v.wc>0)used[v.c]=true;if(v.wd>0)used[v.d]=true;
                float sum=v.wa+(v.wb>0?v.wb:0)+(v.wc>0?v.wc:0)+(v.wd>0?v.wd:0);
                if(!(v.wa>=0)||!(Mathf.Abs(sum-1)<=WeightSlack)){loose.Add(i);continue;}
                (byMainBone[v.a]??=new List<int>()).Add(i);
            }
            var usedBones=new List<int>();var members=new List<int[]>();var reaches=new List<Reach[]>();
            var bones=new List<int>();var low=new List<Vector3>();var high=new List<Vector3>();
            void Grow(int bone,Vector3 p)
            {
                int at=bones.IndexOf(bone);
                if(at<0){bones.Add(bone);low.Add(p);high.Add(p);return;}
                low[at]=Vector3.Min(low[at],p);high[at]=Vector3.Max(high[at],p);
            }
            // The samples of one main bone, halved across their longest side until each patch is small.
            void Divide(List<int> samples)
            {
                if(samples.Count>PatchSize)
                {
                    var least=vertices[samples[0]].pa;var most=least;
                    foreach(int i in samples){least=Vector3.Min(least,vertices[i].pa);most=Vector3.Max(most,vertices[i].pa);}
                    var size=most-least;int axis=size.x>=size.y&&size.x>=size.z?0:size.y>=size.z?1:2;
                    float middle=(least[axis]+most[axis])*.5f;
                    var near=new List<int>();var far=new List<int>();
                    foreach(int i in samples)(vertices[i].pa[axis]<middle?near:far).Add(i);
                    // Coincident samples leave one side empty: they stay together.
                    if(near.Count>0&&far.Count>0){Divide(near);Divide(far);return;}
                }
                bones.Clear();low.Clear();high.Clear();
                foreach(int i in samples)
                {
                    var v=vertices[i];
                    Grow(v.a,v.pa);if(v.wb>0)Grow(v.b,v.pb);if(v.wc>0)Grow(v.c,v.pc);if(v.wd>0)Grow(v.d,v.pd);
                }
                var boxes=new Reach[bones.Count];
                for(int i=0;i<boxes.Length;i++)boxes[i]=new Reach{bone=bones[i],centre=(low[i]+high[i])*.5f,half=(high[i]-low[i])*.5f};
                members.Add(samples.ToArray());reaches.Add(boxes);
            }
            for(int b=0;b<boneCount;b++)
            {
                if(used[b])usedBones.Add(b);
                if(byMainBone[b]!=null)Divide(byMainBone[b]);
            }
            return new Layout{boneCount=boneCount,usedBones=usedBones.ToArray(),members=members.ToArray(),reaches=reaches.ToArray(),loose=loose.ToArray()};
        }
        public float LowestY()
        {
            Skin firstSkin=null;int first=-1;float firstFloor=float.PositiveInfinity;
            foreach(var skin in skins)
            {
                Pose(skin);
                var rows=skin.rows;var reaches=skin.layout.reaches;
                for(int g=0;g<reaches.Length;g++)
                {
                    // A sample is a blend of where each of its bones would carry it, so it is no lower
                    // than the lowest corner of those boxes.
                    float floor=float.PositiveInfinity;
                    var boxes=reaches[g];
                    for(int i=0;i<boxes.Length;i++)
                    {
                        ref var box=ref boxes[i];ref var row=ref rows[box.bone];
                        floor=Mathf.Min(floor,Y(ref row,ref box.centre)-(Mathf.Abs(row.x)*box.half.x+Mathf.Abs(row.y)*box.half.y+Mathf.Abs(row.z)*box.half.z));
                    }
                    floor-=WeightSlack*Mathf.Abs(floor)+FloorMargin;
                    skin.floors[g]=floor;
                    if(floor<firstFloor){firstFloor=floor;firstSkin=skin;first=g;}
                }
            }
            float lowest=float.PositiveInfinity;
            Evaluated=0;
            foreach(var skin in skins)lowest=Lowest(skin,skin.layout.loose,lowest);
            // The patch that reaches lowest first: with a real contact height in hand, most others drop out.
            if(firstSkin!=null)lowest=Lowest(firstSkin,firstSkin.layout.members[first],lowest);
            foreach(var skin in skins)
                for(int g=0;g<skin.floors.Length;g++)
                    if(!(skin.floors[g]>=lowest)&&(skin!=firstSkin||g!=first))lowest=Lowest(skin,skin.layout.members[g],lowest);
            return lowest;
        }
        /// <summary>The same height from every sample, without skipping any: what <see cref="LowestY"/> must equal.</summary>
        public float LowestYUnculled()
        {
            float lowest=float.PositiveInfinity;
            foreach(var skin in skins)
            {
                Pose(skin);
                var vertices=skin.vertices;var rows=skin.rows;
                for(int i=0;i<vertices.Length;i++)lowest=Mathf.Min(lowest,Height(ref vertices[i],rows));
            }
            return lowest;
        }
        private static void Pose(Skin skin)
        {
            foreach(int b in skin.layout.usedBones)
            {
                var bone=b<skin.bones.Length?skin.bones[b]:null;
                var m=bone!=null?bone.localToWorldMatrix:Matrix4x4.identity;
                skin.rows[b]=new Vector4(m.m10,m.m11,m.m12,m.m13);
            }
        }
        private float Lowest(Skin skin,int[] samples,float lowest)
        {
            var vertices=skin.vertices;var rows=skin.rows;
            Evaluated+=samples.Length;
            for(int i=0;i<samples.Length;i++)lowest=Mathf.Min(lowest,Height(ref vertices[samples[i]],rows));
            return lowest;
        }
        private static float Height(ref Map01SkinContactData.Vertex v,Vector4[] rows)
        {
            float y=v.wa*Y(ref rows[v.a],ref v.pa);
            if(v.wb>0)y+=v.wb*Y(ref rows[v.b],ref v.pb);
            if(v.wc>0)y+=v.wc*Y(ref rows[v.c],ref v.pc);
            if(v.wd>0)y+=v.wd*Y(ref rows[v.d],ref v.pd);
            return y;
        }
        private static float Y(ref Vector4 row,ref Vector3 p)=>row.x*p.x+row.y*p.y+row.z*p.z+row.w;
    }
}
