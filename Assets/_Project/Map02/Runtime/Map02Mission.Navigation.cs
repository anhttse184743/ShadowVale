using UnityEngine;
using UnityEngine.AI;
namespace ShadowVale.Map01
{
    public sealed partial class Map02Mission
    {
        Vector3[] route=System.Array.Empty<Vector3>();Vector3 routedFrom,routedTo;string routedId;float routeAt;
        void RefreshRoute()
        {
            var goal=TrackedObjective;if(goal==null){route=System.Array.Empty<Vector3>();return;}
            var point=Point(goal);
            if(Time.unscaledTime<routeAt&&routedId==goal.saveId)return;
            routeAt=Time.unscaledTime+.6f;
            if(routedId==goal.saveId&&(Player.position-routedFrom).sqrMagnitude<1&&(point-routedTo).sqrMagnitude<1)return;
            routedId=goal.saveId;routedFrom=Player.position;routedTo=point;
            var path=new NavMeshPath();
            if(NavMesh.SamplePosition(Player.position,out var start,3,NavMesh.AllAreas)&&NavMesh.SamplePosition(point,out var end,3,NavMesh.AllAreas)&&NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete)route=path.corners;
            else route=System.Array.Empty<Vector3>();
        }
        void DrawRoute(Rect area,bool disc)
        {
            var tint=GUI.color;GUI.color=new Color(1,.8f,.3f,.65f);int dots=0;
            for(int i=1;i<route.Length&&dots<100;i++)
            {
                float length=Vector3.Distance(route[i-1],route[i]);
                for(float distance=0;distance<length&&dots<100;distance+=3)
                {
                    var at=Map02Navigation.Plot(Vector3.Lerp(route[i-1],route[i],distance/length),area);
                    if(disc&&Vector2.Distance(at,area.center)>area.width*.46f)continue;
                    if(!area.Contains(at))continue;
                    GUI.DrawTexture(new Rect(at.x-1.5f,at.y-1.5f,3,3),Texture2D.whiteTexture);dots++;
                }
            }
            GUI.color=tint;
        }
    }
}
