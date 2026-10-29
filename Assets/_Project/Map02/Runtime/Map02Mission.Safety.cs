using System.Linq;
using UnityEngine;
using UnityEngine.AI;
namespace ShadowVale.Map01
{
    public sealed partial class Map02Mission
    {
        // The surrounding hills and fields are scenery without collision. The authored
        // playable ground ends at x +/-110 and z +/-100 (Map02Surroundings).
        public const float PlayableHalfX=108.5f,PlayableHalfZ=98.5f;
        Vector3 lastSafeGround;bool hasSafeGround;float safeSampleAt;
        MeshCollider riverGround;
        public int BoundaryRecoveryCount {get;private set;}
        public static bool WithinPlayableBounds(Vector3 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z)&&Mathf.Abs(p.x)<PlayableHalfX&&Mathf.Abs(p.z)<PlayableHalfZ;
        void InstallMapSafety()
        {
            var root=new GameObject("Map 2 playable perimeter — landscape beyond is scenery");root.transform.SetParent(transform,false);
            void Wall(string name,Vector3 position,Vector3 size){var wall=new GameObject(name);wall.transform.SetParent(root.transform,false);wall.transform.position=position;wall.AddComponent<BoxCollider>().size=size;}
            Wall("East",new Vector3(109,20,0),new Vector3(1,100,202));
            Wall("West",new Vector3(-109,20,0),new Vector3(1,100,202));
            Wall("North",new Vector3(0,20,99),new Vector3(222,100,1));
            Wall("South",new Vector3(0,20,-99),new Vector3(222,100,1));
            ResetMapSafety();
        }
        void ResetMapSafety(){hasSafeGround=false;safeSampleAt=0;}
        bool SafeRiverGround(Vector3 at,out Vector3 point)
        {
            point=at;
            if(!WithinPlayableBounds(at)||Chapter==null||!Chapter.TryUnlockedRiverSurface(at,out var water)||at.y<water-1.6f||at.y>water+1.5f)return false;
            if(riverGround==null)riverGround=GameObject.Find("Continuous ground with meandering canal")?.GetComponent<MeshCollider>();
            if(riverGround==null||!riverGround.enabled)return false;
            // Only the authored terrain supports wading. Missing ground or falling below it
            // still invokes normal map recovery; the seven-metre store is not a river bed.
            if(!riverGround.Raycast(new Ray(new Vector3(at.x,water+.75f,at.z),Vector3.down),out var hit,2.4f)||hit.normal.y<=.5f||at.y<hit.point.y-.2f||at.y>hit.point.y+1.4f)return false;
            point=hit.point;return true;
        }
        bool SafeGround(Vector3 at,out Vector3 point)
        {
            if(SafeRiverGround(at,out point))return true;
            point=at;if(!WithinPlayableBounds(at)||at.y<-.35f||at.y>35)return false;
            // A real collider and NavMesh must agree; decorative skirts and water are not safe ground.
            if(!NavMesh.SamplePosition(at,out var nav,1.25f,NavMesh.AllAreas)||!WithinPlayableBounds(nav.position)||Mathf.Abs(nav.position.y-at.y)>1)return false;
            var hit=Physics.RaycastAll(at+Vector3.up*.65f,Vector3.down,1.6f,~0,QueryTriggerInteraction.Ignore)
                .Where(h=>!h.transform.IsChildOf(Player)&&h.transform!=Player&&h.collider.GetComponentInParent<Map02ChapterActor>()==null&&h.normal.y>.5f&&Mathf.Abs(h.point.y-nav.position.y)<.5f)
                .OrderBy(h=>h.distance).FirstOrDefault();
            if(hit.collider==null)return false;point=nav.position;return true;
        }
        void TickMapSafety()
        {
            if(Player==null||health==null||health.IsDead||Cinematic)return;
            var p=Player.position;
            if(Chapter?.UndergroundStore?.ContainsAuthorizedPlayer(p)==true)return;
            bool invalid=!WithinPlayableBounds(p)||(p.y<-.65f&&!SafeRiverGround(p,out _))||p.y>60;
            if(!invalid)
            {
                if(Time.unscaledTime>=safeSampleAt){safeSampleAt=Time.unscaledTime+.25f;if(SafeGround(p,out var ground)){lastSafeGround=ground;hasSafeGround=true;}}
                return;
            }
            if(!hasSafeGround)
            {
                // Old saves may already contain an outside/underworld position.
                var clamped=new Vector3(float.IsFinite(p.x)?Mathf.Clamp(p.x,-106,106):0,1,float.IsFinite(p.z)?Mathf.Clamp(p.z,-96,96):0);
                if(NavMesh.SamplePosition(clamped,out var nearest,12,NavMesh.AllAreas)&&SafeGround(nearest.position,out var ground))lastSafeGround=ground;
                else if(Chapter!=null&&SafeGround(Chapter.Sites["arrival"].position,out ground))lastSafeGround=ground;
                else return;
                hasSafeGround=true;
            }
            var cc=Player.GetComponent<CharacterController>();bool wasEnabled=cc!=null&&cc.enabled;if(cc!=null)cc.enabled=false;
            Player.position=lastSafeGround+Vector3.up*.08f;if(cc!=null)cc.enabled=wasEnabled;
            player.RestoreMotion(false);cameraRig?.ClearRecoil();BoundaryRecoveryCount++;routedId=null;routeAt=0;
            Physics.SyncTransforms();Say("Đã đưa Nam về phần đường an toàn gần nhất.");
        }
    }
}
