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
    /// <summary>Short kill shot, hostage execution, and the physical return into the existing shelter.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class Map01RescueCinematic : MonoBehaviour
    {
        public enum Mode { None, Takedown, Execution, Shelter }
        public Mode CurrentMode { get; private set; }
        public float Elapsed { get; private set; }
        public bool IsPlaying => CurrentMode != Mode.None;
        private Map01Mission mission;
        private Map01Rescue rescue;
        private Map01EnemyController enemy;
        private ThirdPersonCamera rig;
        private Vector3 cameraStart, killPosition;
        private Vector3 shelterCameraPosition, cameraVelocity;
        private bool shelterRoomShot;
        private Quaternion cameraRotation;
        private float cameraFov, duration, skipHeld;
        private bool clearShot, shot, playerEnabled, combatEnabled, characterEnabled, hungVisualEnabled, invulnerable;
        private Vector3[] namPath, hungPath;
        private Vector3[] hungYieldPath;
        private float namDistance,hungDistance,yieldDistance,namCovered,hungCovered,namWeight,hungWeight,settleTime;
        private float namCycleSpeed;
        private NavMeshAgent hungAgent;
        private bool hungUpdatesPosition,hungUpdatesRotation;
        private PosePlayer namPose, hungPose;
        private Map01HungVisual hungVisual;
        public bool ControlsEnemy(Map01EnemyController guard) => CurrentMode == Mode.Execution && guard == enemy;

        private void Begin(Map01Rescue owner, Mode mode, float seconds)
        {
            rescue = owner; mission = owner.GetComponent<Map01Mission>();
            rig = mission.gameCamera.GetComponent<ThirdPersonCamera>();
            cameraStart = mission.gameCamera.transform.position; cameraRotation = mission.gameCamera.transform.rotation;
            cameraFov = mission.gameCamera.fieldOfView;
            shelterCameraPosition=cameraStart;cameraVelocity=Vector3.zero;shelterRoomShot=false;
            CurrentMode = mode; Elapsed = skipHeld = 0; duration = seconds;
            mission.CloseGameplayPanel(); mission.GetComponent<Map01StoneThrow>()?.Cancel();
            mission.GetComponent<Map01Scouting>()?.HoldBinoculars(false);
            rig.SetBinoculars(false, 1.6f);
            mission.Cinematic = true;
            foreach(var guard in mission.Enemies)if(guard!=null)guard.PauseForCinematic(mode==Mode.Execution&&guard==enemy);
            invulnerable = mission.ModernHealth.CinematicInvulnerable;
            mission.ModernHealth.CinematicInvulnerable = true;
        }
        public void ShowTakedown(Map01Rescue owner, Map01EnemyController victim)
        {
            if (IsPlaying || owner == null) return;
            enemy = victim; Begin(owner, Mode.Takedown, Map01NamActions.TakedownSeconds);
            var focus = mission.player.position + Vector3.up * 1.2f + victim.transform.forward * .4f;
            clearShot = false;
            foreach (float side in new[] { 1f, -1f })
            {
                var p = focus + victim.transform.right * side * 2.1f - victim.transform.forward * .4f + Vector3.up * .3f;
                if (CameraClear(focus, p)) { killPosition = p; clearShot = true; break; }
            }
        }
        public void ExecuteHostage(Map01Rescue owner, Map01EnemyController overseer)
        {
            if (IsPlaying) return;
            enemy = overseer; Begin(owner, Mode.Execution, 1.6f);
            var actor = overseer.GetComponentInChildren<Animator>();
            actor.speed = 1;
            if(actor.parameters.Any(p=>p.name=="Engaged"))actor.SetBool("Engaged",true);
            if (actor.HasState(0, Animator.StringToHash("Aim"))) actor.CrossFadeInFixedTime("Aim", .15f, 0);
            overseer.FaceImmediate(mission.hung.position);
        }
        public bool RunHome(Map01Rescue owner)
        {
            if (IsPlaying || owner.Layout == null) return false;
            var layout = owner.Layout;
            namPath = RouteThrough(owner.GetComponent<Map01Mission>().player.position, layout.shelterRun.Select(t => t.position).ToArray());
            if(namPath.Length<2)return false;
            var player=owner.GetComponent<Map01Mission>();
            float gap=Mathf.Max(1.2f,layout.shelterFollowGap);
            // Single file in the real narrow ramp. Stop Hung behind Nam inside the room;
            // independent normalized timelines formerly made him catch and pass through Nam.
            var hungEnd=AtDistance(namPath,Mathf.Max(0,Length(namPath)-gap),out _);
            var entranceHeading=layout.shelterRun.Length>1?layout.shelterRun[1].position-layout.shelterDoor.position:namPath[1]-namPath[0];
            hungYieldPath=YieldRoute(player.player.position,player.hung.position,namPath,entranceHeading,gap);
            var hungStart=hungYieldPath!=null?hungYieldPath[hungYieldPath.Length-1]:player.hung.position;
            hungPath=RouteThrough(hungStart,layout.shelterRun.Take(layout.shelterRun.Length-1).Select(t=>t.position).Append(hungEnd).ToArray());
            bool rifle=player.ModernCombat.EquippedKind==WeaponKind.Rifle;
            var namClip=rifle?layout.namWalk:layout.namKnifeWalk;
            if(namClip==null)namClip=Resources.Load<AnimationClip>("Rescue/"+(rifle?"Nam_Shelter_Walk":"Nam_Shelter_Knife_Walk"));
            var hungClip=layout.hungWalk??Resources.Load<AnimationClip>("Rescue/Hung_Shelter_Walk");
            if(hungPath.Length<2||namClip==null||hungClip==null){Debug.LogError("Shelter return requires connected ground routes and walk clips.",this);return false;}
            float walkingSeconds=(Mathf.Max(Length(namPath),Length(hungPath))+(hungYieldPath!=null?Length(hungYieldPath):0)+gap)/Mathf.Max(.5f,layout.shelterWalkSpeed)+2;
            Begin(owner,Mode.Shelter,Mathf.Max(layout.shelterSeconds,walkingSeconds));
            namDistance=hungDistance=yieldDistance=namCovered=hungCovered=namWeight=hungWeight=settleTime=0;
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
            mission.Say("Nam: Vào hầm thôi, Hùng. Ở đây an toàn rồi.", 4);
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
        private void Update()
        {
            if (!IsPlaying || mission.Paused || ForestMenu.Visible) return;
            Elapsed += Time.deltaTime;
            if (CurrentMode == Mode.Shelter || CurrentMode == Mode.Execution)
                skipHeld = Keyboard.current != null && Keyboard.current.escapeKey.isPressed ? skipHeld + Time.unscaledDeltaTime : 0;
            if (CurrentMode == Mode.Execution && !shot && Elapsed >= .7f)
            {
                shot = true; enemy.FireAtHostage(mission.hung.position, 0);
                var audio = rescue.Layout.gunshot;
                if (audio != null) AudioSource.PlayClipAtPoint(audio, enemy.transform.position, .65f);
                rescue.CompleteExecution();
            }
            if (CurrentMode == Mode.Shelter)
            {
                WalkShelter(Time.deltaTime);
                if(skipHeld>=1||settleTime>=.9f)Finish();
                return;
            }
            if (Elapsed >= duration || skipHeld >= 1)
            {
                var actions = mission.player.GetComponent<Map01NamActions>();
                if (CurrentMode == Mode.Takedown && actions != null && actions.Busy) return;
                Finish();
            }
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
            float namNext=Advance(namDistance,Length(namPath),speed,dt);
            WalkAlong(mission.player,namPath,namNext,namPose,ref namCovered,ref namWeight,namCycleSpeed);namDistance=namNext;
            float hungNext=Advance(hungDistance,Length(hungPath),speed,dt);
            var candidate=AtDistance(hungPath,hungNext,out _);
            float gap=Mathf.Max(1.2f,rescue.Layout.shelterFollowGap);
            if(Vector3.Distance(candidate,mission.player.position)<gap-.025f)hungNext=hungDistance;
            WalkAlong(mission.hung,hungPath,hungNext,hungPose,ref hungCovered,ref hungWeight,rescue.Layout.hungWalkCycleSpeed);hungDistance=hungNext;
            if(namDistance>=Length(namPath)-.001f&&hungDistance>=Length(hungPath)-.001f)settleTime+=dt;
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
        private void LateUpdate()
        {
            if (!IsPlaying) return;
            if (CurrentMode == Mode.Takedown)
            {
                float u = (Elapsed - .20f) / rescue.Layout.killCameraSeconds;
                if (!clearShot || u <= 0 || u >= 1) { rig.ClearCinematicView(); return; }
                var actor = mission.player.GetComponentInChildren<Animator>();
                var chest = actor.GetBoneTransform(HumanBodyBones.Chest).position;
                var target = chest + mission.player.forward * .35f;
                float blend = Mathf.Sin(u*Mathf.PI);
                if (CameraClear(target,killPosition)) rig.SetCinematicView(killPosition,Quaternion.LookRotation(target-killPosition),46,blend);
                else rig.ClearCinematicView();
            }
            else if (CurrentMode == Mode.Execution)
            {
                enemy.FaceImmediate(mission.hung.position);
                var target = Vector3.Lerp(enemy.transform.position,mission.hung.position,.65f)+Vector3.up*.8f;
                var desired = target + new Vector3(-3,2,-3);
                if (!CameraClear(target,desired)) desired=cameraStart;
                float blend=Mathf.SmoothStep(0,1,Elapsed/.25f);
                rig.SetCinematicView(Vector3.Lerp(cameraStart,desired,blend),Quaternion.LookRotation(target-desired),50,1);
            }
            else
            {
                // Unity evaluates the gameplay Animator between Update and LateUpdate.
                // Apply the cinematic pose afterwards, before skin contacts and camera rendering,
                // so the idle controller cannot replace a moving character's run cycle.
                namPose?.Sample(); hungPose?.Sample();
                namPose?.Ground(); hungPose?.Ground();
                var target=Vector3.Lerp(mission.player.position,mission.hung.position,.5f)+Vector3.up;
                var layout=rescue.Layout;
                var outside=target+mission.player.rotation*layout.shelterCameraOffset;
                // Keep the view along the narrow ramp instead of looking through its retaining wall.
                var corridor=target-mission.player.forward*2.5f+Vector3.up*1.25f;
                float entering=1-Mathf.SmoothStep(0,1,(mission.player.position.y-.6f)/1.8f);
                var desired=Vector3.Lerp(outside,corridor,entering);
                bool indoors=mission.player.position.y<.6f
                    &&Vector3.Distance(mission.player.position,layout.reportPoint.position)<8;
                if(indoors){
                    desired=layout.reportPoint.position+layout.interiorCameraOffset;
                    // Frame Nam from the room as he enters. Waiting for the follower on
                    // the ramp put the midpoint inside the doorway's retaining wall.
                    if(mission.hung.position.y>=.6f)target=mission.player.position+Vector3.up;
                    if(!shelterRoomShot&&ShelterCameraClear(target,desired)){
                        // The outside and inside shots are on opposite sides of the walkers.
                        // Make a camera cut at the doorway; a damped crossing passes through Nam.
                        shelterCameraPosition=desired;cameraVelocity=Vector3.zero;shelterRoomShot=true;
                    }
                }
                shelterCameraPosition=Vector3.SmoothDamp(shelterCameraPosition,desired,ref cameraVelocity,Mathf.Max(.08f,layout.cameraDamping),25,Time.deltaTime);
                // Validate the blended position as well as its goal, so the camera cannot cross a wall.
                var offset=shelterCameraPosition-target;
                if(offset.sqrMagnitude>.001f){
                    // The focus is between two passengers. Their capsules must never push
                    // the lens into a body; only scenery obstructs this cinematic view.
                    var blocking=Physics.SphereCastAll(target,.18f,offset.normalized,offset.magnitude,mission.ObstructionMask,QueryTriggerInteraction.Ignore)
                        .Where(h=>ShelterScenery(h.transform))
                        .OrderBy(h=>h.distance).ToArray();
                    if(blocking.Length>0){
                        // Cut to the clear room shot if the damped transition crosses its
                        // doorway wall, rather than squeezing the lens into a passenger.
                        if(indoors&&ShelterCameraClear(target,desired)){shelterCameraPosition=desired;cameraVelocity=Vector3.zero;}
                        else shelterCameraPosition=target+offset.normalized*Mathf.Max(.3f,blocking[0].distance-.2f);
                    }
                }
                float blend=Mathf.SmoothStep(0,1,Elapsed/.45f);
                rig.SetCinematicView(shelterCameraPosition,Quaternion.Slerp(cameraRotation,Quaternion.LookRotation(target-shelterCameraPosition),blend),Mathf.Lerp(cameraFov,layout.shelterCameraFov,blend),1);
            }
        }
        public void Skip() { if(CurrentMode==Mode.Shelter||CurrentMode==Mode.Execution)Finish(); }
        private void Finish()
        {
            if (!IsPlaying) return;
            var mode=CurrentMode;
            if(mode==Mode.Execution&&!shot){shot=true;rescue.CompleteExecution();}
            if(mode==Mode.Shelter){
                mission.player.position=namPath[namPath.Length-1];mission.hung.position=hungPath[hungPath.Length-1];
                namPose.Sample(namCovered/Mathf.Max(.1f,namCycleSpeed),0);hungPose.Sample(hungCovered/Mathf.Max(.1f,rescue.Layout.hungWalkCycleSpeed),0);
                namPose.Sample();hungPose.Sample();
            }
            Cleanup();
            if(mode==Mode.Shelter){
                rescue.CompleteDelivery();
                hungVisual?.RestoreAfterCinematic();
                GetComponent<Map01SaveSystem>().SaveSlot(ForestSaveSlots.AutoSlot,out _,true);
            }
            ForestMenu.SuppressKeysAfterCutscene();
        }
        private void Cleanup()
        {
            var mode=CurrentMode;CurrentMode=Mode.None;
            namPose?.Dispose();hungPose?.Dispose();namPose=hungPose=null;
            if(mission==null)return;
            mission.Cinematic=false;mission.ModernHealth.CinematicInvulnerable=invulnerable;rig?.ClearCinematicView();
            if(mode==Mode.Execution&&enemy!=null){var actor=enemy.GetComponentInChildren<Animator>();if(actor!=null&&actor.parameters.Any(p=>p.name=="Engaged"))actor.SetBool("Engaged",false);}
            if(mode==Mode.Shelter){
                var cc=mission.player.GetComponent<CharacterController>();if(cc!=null)cc.enabled=characterEnabled;
                mission.ModernPlayer.enabled=playerEnabled;mission.ModernCombat.enabled=combatEnabled;
                if(hungVisual!=null)hungVisual.enabled=hungVisualEnabled;
                if(hungAgent!=null){
                    if(hungAgent.enabled){hungAgent.Warp(mission.hung.position);if(hungAgent.isOnNavMesh)hungAgent.ResetPath();}
                    hungAgent.updatePosition=hungUpdatesPosition;hungAgent.updateRotation=hungUpdatesRotation;
                }
                hungVisual?.RestoreAfterCinematic();
            }
            if(mode!=Mode.Takedown)mission.ModernCombat?.RestoreAfterCinematic();
        }
        private void OnGUI()
        {
            if(CurrentMode!=Mode.Shelter&&CurrentMode!=Mode.Execution)return;
            var color=GUI.color;int depth=GUI.depth;GUI.depth=-90;GUI.color=Color.black;
            GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height*.09f),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0,Screen.height*.91f,Screen.width,Screen.height*.09f),Texture2D.whiteTexture);
            GUI.color=Color.white;GUI.Label(new Rect(Screen.width-245,14,240,30),"Giữ Esc 1 giây để bỏ qua");GUI.color=color;GUI.depth=depth;
            if(CurrentMode==Mode.Shelter&&Elapsed<4){
                var subtitle=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,wordWrap=true,fontSize=Mathf.RoundToInt(Screen.height*.026f)};
                GUI.color=Color.white;GUI.Label(new Rect(Screen.width*.12f,Screen.height*.91f,Screen.width*.76f,Screen.height*.08f),"Nam: Vào hầm thôi, Hùng. Ở đây an toàn rồi.",subtitle);GUI.color=color;
            }
        }
        private void OnDisable(){if(IsPlaying)Cleanup();}
        private sealed class PosePlayer : IDisposable
        {
            private readonly Animator actor;
            private readonly Transform characterRoot;
            private readonly RuntimeAnimatorController controller;
            private readonly bool rootMotion;
            private readonly AnimatorCullingMode culling;
            private readonly Vector3 localPosition;
            private readonly Quaternion localRotation;
            private PlayableGraph graph;
            private AnimationClipPlayable run,idle;
            private AnimationMixerPlayable mixer;
            private readonly float runLength,idleLength;
            private float sampleTime,movementWeight;
            private readonly Mesh baked=new Mesh();
            private readonly SkinnedMeshRenderer[] skins;
            private readonly Map01SkinContact contact;
            public PosePlayer(Animator actor,Transform characterRoot,AnimationClip moving,AnimationClip resting)
            {
                this.actor=actor;this.characterRoot=characterRoot;controller=actor.runtimeAnimatorController;rootMotion=actor.applyRootMotion;culling=actor.cullingMode;
                localPosition=actor.transform.localPosition;localRotation=actor.transform.localRotation;
                actor.applyRootMotion=false;actor.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                graph=PlayableGraph.Create("Rescue shelter locomotion");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                run=AnimationClipPlayable.Create(graph,moving);idle=AnimationClipPlayable.Create(graph,resting);run.SetApplyFootIK(true);idle.SetApplyFootIK(true);
                runLength=moving.length;idleLength=resting.length;
                run.SetSpeed(0);idle.SetSpeed(0);
                mixer=AnimationMixerPlayable.Create(graph,2);graph.Connect(idle,0,mixer,0);graph.Connect(run,0,mixer,1);
                AnimationPlayableOutput.Create(graph,"Run and settle",actor).SetSourcePlayable(mixer);graph.Play();
                skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>();
                contact=new Map01SkinContact(actor);
            }
            public void Sample(float time,float weight)
            {
                sampleTime=time;movementWeight=weight;
            }
            public void Sample()
            {
                if(!graph.IsValid())return;
                // Distance controls phase. Wrap explicitly so a manually sampled cycle keeps
                // stepping for the entire route, including after the first clip duration.
                run.SetTime(runLength>.001f?Mathf.Repeat(sampleTime,runLength):0);
                idle.SetTime(idleLength>.001f?Mathf.Repeat(sampleTime,idleLength):0);
                mixer.SetInputWeight(0,1-movementWeight);mixer.SetInputWeight(1,movementWeight);graph.Evaluate(0);
            }
            public void Ground()
            {
                // Keep the lowest sole on the ramp while retaining authored leg motion.
                float ground=float.NegativeInfinity;
                foreach(var hit in Physics.RaycastAll(characterRoot.position+Vector3.up*1.2f,Vector3.down,2.5f,~0,QueryTriggerInteraction.Ignore))
                    if(hit.normal.y>.6f&&hit.point.y<=characterRoot.position.y+.25f&&hit.transform.GetComponentInParent<Health>()==null
                        &&!hit.transform.IsChildOf(characterRoot)&&hit.transform.GetComponentInParent<Map01HungVisual>()==null)
                        ground=Mathf.Max(ground,hit.point.y);
                if(float.IsNegativeInfinity(ground))ground=characterRoot.position.y;
                float sole=contact.LowestY();
                if(float.IsInfinity(sole))foreach(var skin in skins){skin.BakeMesh(baked);foreach(var v in baked.vertices)sole=Mathf.Min(sole,skin.transform.TransformPoint(v).y);}
                if(!float.IsInfinity(sole))actor.transform.position+=Vector3.up*Mathf.Clamp(ground-sole,-.35f,.35f);
            }
            public void Dispose()
            {
                if(graph.IsValid())graph.Destroy();
                UnityEngine.Object.Destroy(baked);
                if(actor==null)return;
                actor.runtimeAnimatorController=controller;actor.applyRootMotion=rootMotion;actor.cullingMode=culling;
                actor.transform.SetLocalPositionAndRotation(localPosition,localRotation);actor.Rebind();actor.Update(0);
            }
        }
    }
}
