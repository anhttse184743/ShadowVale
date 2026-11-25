using UnityEngine;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        public int NightFenceSections { get; private set; }
        void BuildNightFencePerimeter()
        {
            var east=postGates[0];var north=postGates[1];
            // Closed scenery loop: the only breaks are real gateways, never blunt
            // cut ends in the player's view. The distant returns lie beyond the
            // playable terrain, while existing village lanes remain open.
            var path=new[]{east.position+east.right*4.66f,
                new Vector3(-57.2f,0,1),new Vector3(-49,0,-10),new Vector3(-43,0,-18),
                new Vector3(-43,0,-120),new Vector3(-120,0,-120),new Vector3(-120,0,120),
                new Vector3(-43,0,120),new Vector3(-43,0,43),new Vector3(-48,0,30),
                new Vector3(-54,0,19),east.position-east.right*4.66f};
            // Leave passages for both Day 2 trails as well as the village lanes.
            var crossings=new[]{(new Vector3(-51,0,-7.317f),false),(new Vector3(-43,0,-70),false),(new Vector3(-51,0,24),false),
                (new Vector3(-54.53f,0,16.42f),true),(new Vector3(-47.31f,0,-12.25f),true)};
            for(int i=1;i<path.Length;i++)FenceWithCrossings(path[i-1],path[i],crossings);
            var noCrossings=System.Array.Empty<(Vector3,bool)>();
            FenceWithCrossings(north.position-north.right*4.66f,new Vector3(-120,0,21),noCrossings);
            FenceWithCrossings(north.position+north.right*4.66f,new Vector3(-52.5f,0,21),noCrossings);
        }
        void FenceWithCrossings(Vector3 start,Vector3 end,(Vector3 position,bool clearPassage)[] crossings)
        {
            start.y=end.y=sites["bamboo"].position.y;
            var delta=end-start;var direction=delta.normalized;
            foreach(var (point,clearPassage) in crossings){
                var p=point;p.y=start.y;float along=Vector3.Dot(p-start,direction);
                if(along<1.2f||along>delta.magnitude-1.2f||Vector3.Distance(start+direction*along,p)>.12f)continue;
                FenceRun(start,p-direction*1.10f);FenceRun(p+direction*1.10f,end);
                var passage=NightModel("VillageGate",p);passage.position=new Vector3(p.x,NightGround(p),p.z);
                passage.rotation=Quaternion.LookRotation(Vector3.Cross(direction,Vector3.up));
                foreach(var t in passage.GetComponentsInChildren<Transform>(true))
                    if(t.name=="FenceWingLeft"||t.name=="FenceWingRight"||t.name=="Village boundary fence")t.gameObject.SetActive(false);
                passage.name="Open village lane passage";
                var gate=passage.GetComponent<Map02NightWatchVisual>();
                if(clearPassage)gate.gateLeaf.gameObject.SetActive(false);
                gate.SetGate(false,false);
                return;
            }
            FenceRun(start,end);
        }
        void FenceRun(Vector3 start,Vector3 end)
        {
            float distance=Vector3.ProjectOnPlane(end-start,Vector3.up).magnitude;
            int count=Mathf.Max(1,Mathf.CeilToInt(distance/3.45f));
            for(int i=0;i<count;i++){
                var a=Vector3.Lerp(start,end,(float)i/count);var b=Vector3.Lerp(start,end,(float)(i+1)/count);
                a.y=NightGround(a);b.y=NightGround(b);var direction=(b-a).normalized;
                var section=NightModel("FenceSection",(a+b)*.5f);
                var normal=Vector3.Cross(direction,Vector3.up).normalized;
                section.rotation=Quaternion.LookRotation(normal,Vector3.Cross(normal,direction));
                section.localScale=new Vector3(Vector3.Distance(a,b)/3.50f,1,1);
                section.name="Continuous village boundary fence "+NightFenceSections++;
                nightFenceRuns.Add((section,a,b));
            }
        }
        readonly System.Collections.Generic.List<(Transform section,Vector3 a,Vector3 b)> nightFenceRuns=new();
        /// <summary>Night 3: Hùng's ditch runs under the boundary fence. Leave the sections it crosses open so he never crawls through the bamboo (or around a carved obstacle).</summary>
        void OpenNightFenceAlong(Vector3[] path)
        {
            nightFenceRuns.RemoveAll(r=>r.section==null);
            foreach(var run in nightFenceRuns)
            {
                if(!run.section.gameObject.activeSelf)continue;
                for(int i=1;i<path.Length;i++)
                    if(FlatSegmentGap(run.a,run.b,path[i-1],path[i])<.6f){run.section.gameObject.SetActive(false);break;}
            }
        }
        static float FlatSegmentGap(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            Vector2 A=new(a.x,a.z),B=new(b.x,b.z),C=new(c.x,c.z),D=new(d.x,d.z);
            static float Cross(Vector2 u,Vector2 v)=>u.x*v.y-u.y*v.x;
            float d1=Cross(B-A,C-A),d2=Cross(B-A,D-A),d3=Cross(D-C,A-C),d4=Cross(D-C,B-C);
            if(d1*d2<0&&d3*d4<0)return 0;
            static float ToSegment(Vector2 p,Vector2 s,Vector2 e){var se=e-s;float t=se.sqrMagnitude<1e-6f?0:Mathf.Clamp01(Vector2.Dot(p-s,se)/se.sqrMagnitude);return Vector2.Distance(p,s+se*t);}
            return Mathf.Min(Mathf.Min(ToSegment(A,C,D),ToSegment(B,C,D)),Mathf.Min(ToSegment(C,A,B),ToSegment(D,A,B)));
        }
    }
}
