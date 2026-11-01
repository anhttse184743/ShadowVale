using System;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The run home to the shelter: Nam's and Hùng's routes, walking them, and keeping the camera clear.
    /// </summary>
    public sealed partial class Map01RescueCinematic
    {
        public bool RunHome(Map01Rescue owner,bool hungArrivedFirst=false)
        {
            if (IsPlaying || owner.Layout == null) return false;
            var layout = owner.Layout;
            var player=owner.GetComponent<Map01Mission>();hungFirst=hungArrivedFirst;
            float gap=Mathf.Max(1.4f,layout.shelterFollowGap);
            var leader=hungFirst?player.hung:player.player;var follower=hungFirst?player.player:player.hung;
            var leaderPath=RouteThrough(leader.position,layout.shelterRun.Select(t=>t.position).ToArray());
            if(leaderPath.Length<2)return false;
            var followerEnd=AtDistance(leaderPath,Mathf.Max(0,Length(leaderPath)-gap),out _);
            var entranceHeading=layout.shelterRun.Length>1?layout.shelterRun[1].position-layout.shelterDoor.position:leaderPath[1]-leaderPath[0];
            hungYieldPath=hungFirst?null:YieldRoute(player.player.position,player.hung.position,leaderPath,entranceHeading,gap);
            var followerStart=hungYieldPath!=null?hungYieldPath[hungYieldPath.Length-1]:follower.position;
            var followerPath=RouteThrough(followerStart,layout.shelterRun.Take(layout.shelterRun.Length-1).Select(t=>t.position).Append(followerEnd).ToArray());
            namPath=hungFirst?followerPath:leaderPath;hungPath=hungFirst?leaderPath:followerPath;
            // Stop at Nam's first interior marker, independent of Hung's progress.
            var insideRoute=RouteThrough(player.player.position,layout.shelterRun.Take(Mathf.Min(3,layout.shelterRun.Length)).Select(t=>t.position).ToArray());
            if(insideRoute.Length<2)return false;
            namInsideDistance=Mathf.Min(Length(namPath),Length(insideRoute));
            bool rifle=player.ModernCombat.EquippedKind==WeaponKind.Rifle;
            var namClip=rifle?layout.namWalk:layout.namKnifeWalk;
            if(namClip==null)namClip=Resources.Load<AnimationClip>("Rescue/"+(rifle?"Nam_Shelter_Walk":"Nam_Shelter_Knife_Walk"));
            var hungClip=layout.hungWalk??Resources.Load<AnimationClip>("Rescue/Hung_Shelter_Walk");
            if(hungPath.Length<2||namClip==null||hungClip==null){Debug.LogError("Shelter return requires connected ground routes and walk clips.",this);return false;}
            float walkingSeconds=(Mathf.Max(Length(namPath),Length(hungPath))+(hungYieldPath!=null?Length(hungYieldPath):0)+gap)/Mathf.Max(.5f,layout.shelterWalkSpeed)+2;
            Begin(owner,Mode.Shelter,Mathf.Max(layout.shelterSeconds,walkingSeconds));
            namDistance=hungDistance=yieldDistance=namCovered=hungCovered=namWeight=hungWeight=0;
            namCycleSpeed=rifle?layout.namWalkCycleSpeed:layout.namKnifeWalkCycleSpeed;
            playerEnabled = mission.ModernPlayer.enabled; combatEnabled = mission.ModernCombat.enabled;
            mission.ModernCombat.PrepareCinematicCarry();
            var character = mission.player.GetComponent<CharacterController>(); characterEnabled = character != null && character.enabled;
            mission.ModernPlayer.enabled = mission.ModernCombat.enabled = false;
            if (character != null) character.enabled = false;
            hungAgent=mission.hung.GetComponent<NavMeshAgent>();
            if(hungAgent!=null){
                hungUpdatesPosition=hungAgent.updatePosition;hungUpdatesRotation=hungAgent.updateRotation;
                if(hungAgent.isOnNavMesh){hungAgent.ResetPath();hungAgent.isStopped=true;}
                hungAgent.updatePosition=hungAgent.updateRotation=false;
            }
            hungVisual = mission.hung.GetComponentInChildren<Map01HungVisual>();
            hungVisualEnabled = hungVisual != null && hungVisual.enabled;
            if(hungVisual!=null){
                // A checkpoint may reach the trigger before the follower's next Update.
                // Set the freed standing state and hide the captive rope before taking over.
                hungVisual.RestoreAfterCinematic();
                if(hungAgent!=null&&hungAgent.isOnNavMesh)hungAgent.isStopped=true;
                hungVisual.enabled=false;
            }
            namPose = new PosePlayer(mission.player.GetComponentInChildren<Animator>(),mission.player,namClip,rifle?layout.namIdle:layout.hungIdle);
            hungPose = new PosePlayer(mission.hung.GetComponentInChildren<Animator>(),mission.hung,hungClip,layout.hungIdle);
            Voice().Play("m1_rescue_shelter");
            return true;
        }
        private static Vector3[] RouteThrough(Vector3 start, Vector3[] markers)
        {
            var route = new System.Collections.Generic.List<Vector3> { start };
            foreach (var marker in markers)
            {
                var path = Map01Rescue.Path(route[route.Count - 1], marker);
                if (path.Length == 0) return Array.Empty<Vector3>();
                foreach (var p in path.Skip(1)) if (Vector3.Distance(route[route.Count - 1], p) > .02f) route.Add(p);
            }
            return route.ToArray();
        }
        private static Vector3[] YieldRoute(Vector3 nam,Vector3 hung,Vector3[] route,Vector3 entranceHeading,float gap)
        {
            bool inWay=Vector3.Distance(nam,hung)<gap;
            float travelled=0;
            for(int i=1;i<route.Length&&travelled<4;i++){
                var delta=route[i]-route[i-1];
                float u=Mathf.Clamp01(Vector3.Dot(hung-route[i-1],delta)/Mathf.Max(.001f,delta.sqrMagnitude));
                if(Vector3.Distance(hung,route[i-1]+delta*u)<gap)inWay=true;
                travelled+=delta.magnitude;
            }
            if(!inWay)return null;
            var forward=Vector3.ProjectOnPlane(entranceHeading,Vector3.up).normalized;
            var right=Vector3.Cross(Vector3.up,forward);
            float side=Vector3.Dot(nam-hung,right)>0?-1:1;
            // Prefer stepping back along the approach. Sideways bank points are fallbacks,
            // since its steep earth shoulders cannot support a comfortable walk.
            foreach(float lateral in new[]{0,side*1.6f,-side*1.6f,side*2.2f}){
                var candidate=hung+right*lateral-forward*2.2f;
                // The ramp top is lower than the surrounding dry bank. A small 3D sample
                // radius rejects every yielding point even though the bank is connected.
                if(!NavMesh.SamplePosition(candidate,out var hit,2f,NavMesh.AllAreas)||Vector3.Distance(hit.position,nam)<gap+.3f)continue;
                var path=Map01Rescue.Path(hung,hit.position);if(path.Length<2)continue;
                float initial=Vector3.Distance(nam,hung);bool clear=true;
                for(float d=.2f;d<Length(path);d+=.2f)
                    if(Vector3.Distance(AtDistance(path,d,out _),nam)<Mathf.Min(gap,initial)-.05f){clear=false;break;}
                if(clear)return path;
            }
            return null;
        }

        private void WalkShelter(float dt)
        {
            float speed=Mathf.Clamp(rescue.Layout.shelterWalkSpeed,.5f,1.65f);
            if(hungYieldPath!=null){
                yieldDistance=Mathf.Min(Length(hungYieldPath),yieldDistance+speed*dt);
                WalkAlong(mission.hung,hungYieldPath,yieldDistance,hungPose,ref hungCovered,ref hungWeight,rescue.Layout.hungWalkCycleSpeed);
                namPose.Sample(namCovered/Mathf.Max(.1f,namCycleSpeed),0);
                if(yieldDistance>=Length(hungYieldPath)-.001f)hungYieldPath=null;
                return;
            }
            float gap=Mathf.Max(1.4f,rescue.Layout.shelterFollowGap);
            if(hungFirst){
                float h=Advance(hungDistance,Length(hungPath),speed,dt);
                WalkAlong(mission.hung,hungPath,h,hungPose,ref hungCovered,ref hungWeight,rescue.Layout.hungWalkCycleSpeed);hungDistance=h;
                float n=Advance(namDistance,Length(namPath),speed,dt);
                if(Vector3.Distance(AtDistance(namPath,n,out _),mission.hung.position)<gap-.025f)n=namDistance;
                WalkAlong(mission.player,namPath,n,namPose,ref namCovered,ref namWeight,namCycleSpeed);namDistance=n;
            }else{
                float n=Advance(namDistance,Length(namPath),speed,dt);
                WalkAlong(mission.player,namPath,n,namPose,ref namCovered,ref namWeight,namCycleSpeed);namDistance=n;
                float h=Advance(hungDistance,Length(hungPath),speed,dt);
                if(Vector3.Distance(AtDistance(hungPath,h,out _),mission.player.position)<gap-.025f)h=hungDistance;
                WalkAlong(mission.hung,hungPath,h,hungPose,ref hungCovered,ref hungWeight,rescue.Layout.hungWalkCycleSpeed);hungDistance=h;
            }
        }
        private static float Advance(float distance,float length,float speed,float dt)=>
            Mathf.Min(length,distance+speed*Mathf.Clamp((length-distance)/.6f,.25f,1)*dt);
        private void WalkAlong(Transform root,Vector3[] points,float distance,PosePlayer pose,ref float covered,ref float weight,float cycleSpeed)
        {
            var position=AtDistance(points,distance,out var delta);
            float moved=Vector3.Distance(root.position,position);covered+=moved;root.position=position;
            var facing=Vector3.ProjectOnPlane(delta,Vector3.up);
            if(moved>.001f&&facing.sqrMagnitude>.001f)root.rotation=Quaternion.RotateTowards(root.rotation,Quaternion.LookRotation(facing),180*Time.deltaTime);
            weight=Mathf.MoveTowards(weight,moved>.001f?1:0,Time.deltaTime/.18f);
            pose.Sample(covered/Mathf.Max(.1f,cycleSpeed),weight);
        }
        private static Vector3 AtDistance(Vector3[] points,float distance,out Vector3 direction)
        {
            float d=Mathf.Clamp(distance,0,Length(points));
            int segment = 1;
            while (segment < points.Length - 1 && d > Vector3.Distance(points[segment-1], points[segment]))
            { d -= Vector3.Distance(points[segment-1], points[segment]); segment++; }
            var delta = points[segment]-points[segment-1];
            direction=delta;
            return Vector3.Lerp(points[segment-1],points[segment],delta.magnitude>.001f?d/delta.magnitude:1);
        }
        private static float Length(Vector3[] p) { float d=0;for(int i=1;i<p.Length;i++)d+=Vector3.Distance(p[i-1],p[i]);return d; }
        private bool CameraClear(Vector3 focus, Vector3 camera)
        {
            if (mission.SightCover != null && mission.SightCover.Blocks(focus, camera)) return false;
            return !Physics.Linecast(focus, camera, mission.ObstructionMask, QueryTriggerInteraction.Ignore);
        }
        private bool ShelterScenery(Transform obstacle)=>
            !obstacle.IsChildOf(mission.player)&&!obstacle.IsChildOf(mission.hung)
            &&obstacle.GetComponentInParent<Health>()==null
            &&obstacle.GetComponentInParent<Animator>()==null
            &&obstacle.GetComponentInChildren<Animator>()==null;
        private bool ShelterCameraClear(Vector3 focus,Vector3 position)
        {
            var delta=position-focus;
            return delta.sqrMagnitude>.001f&&!Physics.SphereCastAll(focus,.18f,delta.normalized,delta.magnitude,mission.ObstructionMask,QueryTriggerInteraction.Ignore)
                .Any(h=>ShelterScenery(h.transform));
        }
    }
}
