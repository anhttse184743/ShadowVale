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
                new Vector3(-54.5f,0,17),east.position-east.right*4.66f};
            var crossings=new[]{new Vector3(-51,0,-7.317f),new Vector3(-51,0,24)};
            for(int i=1;i<path.Length;i++)FenceWithCrossings(path[i-1],path[i],crossings);
            FenceWithCrossings(north.position-north.right*4.66f,new Vector3(-120,0,21),System.Array.Empty<Vector3>());
            FenceWithCrossings(north.position+north.right*4.66f,new Vector3(-52.5f,0,21),System.Array.Empty<Vector3>());
        }
        void FenceWithCrossings(Vector3 start,Vector3 end,Vector3[] crossings)
        {
            start.y=end.y=sites["bamboo"].position.y;
            var delta=end-start;var direction=delta.normalized;
            foreach(var point in crossings){
                var p=point;p.y=start.y;float along=Vector3.Dot(p-start,direction);
                if(along<1.2f||along>delta.magnitude-1.2f||Vector3.Distance(start+direction*along,p)>.12f)continue;
                FenceRun(start,p-direction*1.10f);FenceRun(p+direction*1.10f,end);
                var passage=NightModel("VillageGate",p);passage.position=new Vector3(p.x,NightGround(p),p.z);
                passage.rotation=Quaternion.LookRotation(Vector3.Cross(direction,Vector3.up));
                foreach(var t in passage.GetComponentsInChildren<Transform>(true))
                    if(t.name=="FenceWingLeft"||t.name=="FenceWingRight"||t.name=="Village boundary fence")t.gameObject.SetActive(false);
                passage.name="Open village lane passage";passage.GetComponent<Map02NightWatchVisual>().SetGate(false,false);
                return;
            }
            FenceRun(start,end);
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
                var section=NightModel("FenceSection",(a+b)*.5f);
                var normal=Vector3.Cross(direction,Vector3.up).normalized;
                section.rotation=Quaternion.LookRotation(normal,Vector3.Cross(normal,direction));
                section.localScale=new Vector3(Vector3.Distance(a,b)/3.50f,1,1);
                section.name="Continuous village boundary fence "+NightFenceSections++;
            }
        }
    }
}
