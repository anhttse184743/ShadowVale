using UnityEngine;
using System.Linq;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        Map02FenceClearance fenceClearance;
        void PrepareFenceClearance()
        {
            fenceClearance=new Map02FenceClearance(sites.Values.Select(t=>t.position));
            fenceClearance.Protect(Layout.dayTwoOpenRoute);fenceClearance.Protect(Layout.dayTwoCoveredRoute);
            fenceClearance.Protect(Layout.nightTwoEvacuationRoute);fenceClearance.Protect(Layout.nightTwoBankRoute);
            foreach(var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if((renderer.name=="Weathered garden fence post"||renderer.name=="Bamboo garden rail")&&!fenceClearance.AllowsBounds(renderer.bounds))renderer.gameObject.SetActive(false);
        }
        public int NightFenceSections { get; private set; }
        void BuildNightFencePerimeter()
        {
            var east=postGates[0];var north=postGates[1];
            // Boundary follows the village edge; homes and mission lanes always take priority.
            // Unsafe spans are omitted, leaving deliberate openings instead of clipping buildings.
            var path=new[]{east.position+east.right*4.66f,
                new Vector3(-57.2f,0,1),new Vector3(-49,0,-10),new Vector3(-43,0,-18),
                new Vector3(-43,0,-120),new Vector3(-120,0,-120),new Vector3(-120,0,120),
                new Vector3(-43,0,120),new Vector3(-43,0,43),new Vector3(-48,0,30),
                new Vector3(-54,0,19),east.position-east.right*4.66f};
            // Leave passages for both Day 2 trails as well as the village lanes.
            // Every route a task walks crosses the fence through an open passage, never round it (26/11/2026, measured from
            // the NavMesh routes between consecutive task markers with and without the fence):
            //   (-46.30,34.43) and (-62.85,21) Day 1, Thành's briefing to Hùng at the family yard (D1_01 -> D1_02);
            //   (-56.96,2.48) beside the east gate, Day 2's walk back with Tâm (scout -> command) and Night 2 (N2_04 -> N2_05).
            var crossings=new[]{(new Vector3(-51,0,-7.317f),false),(new Vector3(-51,0,24),false),   // (-43,-70) is inside the open riverside stretch (OnTheTankRoad)
                (new Vector3(-54.53f,0,16.42f),true),(new Vector3(-47.31f,0,-12.25f),true),
                (new Vector3(-46.30f,0,34.43f),true),(new Vector3(-62.85f,0,21f),true),(new Vector3(-56.96f,0,2.48f),true)};
            for(int i=1;i<path.Length;i++)FenceWithCrossings(path[i-1],path[i],crossings);
            FenceWithCrossings(north.position-north.right*4.66f,new Vector3(-120,0,21),crossings);
            FenceWithCrossings(north.position+north.right*4.66f,new Vector3(-52.5f,0,21),crossings);
        }
        void FenceWithCrossings(Vector3 start,Vector3 end,(Vector3 position,bool clearPassage)[] crossings)
        {
            start.y=end.y=sites["bamboo"].position.y;
            var delta=end-start;var direction=delta.normalized;
            var cursor=start;
            foreach(var crossing in crossings.Select(c=>Vector3.Dot(new Vector3(c.position.x,start.y,c.position.z)-start,direction))
                .Where(d=>d>2&&d<delta.magnitude-2).OrderBy(d=>d)){
                var point=start+direction*crossing;
                if(!crossings.Any(c=>Vector3.Distance(new Vector3(c.position.x,start.y,c.position.z),point)<.12f))continue;
                FenceRun(cursor,point-direction*2);cursor=point+direction*2;
            }
            FenceRun(cursor,end);
        }
        /// <summary>The riverside stretch of the east line (x = -43) is left open: the Night 3 tank comes out of the bamboo to
        /// the head of the dock road here and Hùng runs from the bank ditch to its tracks, so a fence (and its NavMesh carving)
        /// would stand across both (23/11/2026).</summary>
        static bool OnTheTankRoad(Vector3 p)=>Mathf.Abs(p.x+43)<1.5f&&p.z<-48&&p.z>-91;
        void FenceRun(Vector3 start,Vector3 end)
        {
            float distance=Vector3.ProjectOnPlane(end-start,Vector3.up).magnitude;
            int count=Mathf.Max(1,Mathf.CeilToInt(distance/3.45f));
            for(int i=0;i<count;i++){
                var a=Vector3.Lerp(start,end,(float)i/count);var b=Vector3.Lerp(start,end,(float)(i+1)/count);
                if(OnTheTankRoad((a+b)*.5f))continue;
                a.y=NightGround(a);b.y=NightGround(b);var direction=(b-a).normalized;
                if(!fenceClearance.Allows(a,b))continue;
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
