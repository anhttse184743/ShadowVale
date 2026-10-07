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
        private Quaternion cameraRotation;
        private float cameraFov, duration, skipHeld;
        private bool clearShot, shot, playerEnabled, combatEnabled, characterEnabled, hungVisualEnabled, invulnerable;
        private Vector3[] namPath, hungPath;
        private PosePlayer namPose, hungPose;
        private Map01HungVisual hungVisual;
        public bool ControlsEnemy(Map01EnemyController guard) => CurrentMode == Mode.Execution && guard == enemy;

        private void Begin(Map01Rescue owner, Mode mode, float seconds)
        {
            rescue = owner; mission = owner.GetComponent<Map01Mission>();
            rig = mission.gameCamera.GetComponent<ThirdPersonCamera>();
            cameraStart = mission.gameCamera.transform.position; cameraRotation = mission.gameCamera.transform.rotation;
            cameraFov = mission.gameCamera.fieldOfView;
            shelterCameraPosition=cameraStart;cameraVelocity=Vector3.zero;
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
            var end = layout.reportPoint.position;
            hungPath = RouteThrough(owner.GetComponent<Map01Mission>().hung.position,
                layout.shelterRun.Take(layout.shelterRun.Length - 1).Select(t => t.position).Append(end + Vector3.right * 1.2f).ToArray());
            if (namPath.Length < 2 || hungPath.Length < 2 || layout.namRun == null || layout.hungRun == null)
            { Debug.LogError("Shelter return requires two connected ground routes and run clips.", this); return false; }
            Begin(owner, Mode.Shelter, layout.shelterSeconds);
            playerEnabled = mission.ModernPlayer.enabled; combatEnabled = mission.ModernCombat.enabled;
            mission.ModernCombat.PrepareCinematicCarry();
            var character = mission.player.GetComponent<CharacterController>(); characterEnabled = character != null && character.enabled;
            mission.ModernPlayer.enabled = mission.ModernCombat.enabled = false;
            if (character != null) character.enabled = false;
            var agent = mission.hung.GetComponent<NavMeshAgent>(); if (agent != null && agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = true; }
            hungVisual = mission.hung.GetComponentInChildren<Map01HungVisual>();
            hungVisualEnabled = hungVisual != null && hungVisual.enabled;
            if (hungVisual != null) hungVisual.enabled = false;
            bool rifle=mission.ModernCombat.EquippedKind==WeaponKind.Rifle;
            namPose = new PosePlayer(mission.player.GetComponentInChildren<Animator>(), mission.player, rifle?layout.namRun:layout.hungRun, rifle?layout.namIdle:layout.hungIdle);
            hungPose = new PosePlayer(mission.hung.GetComponentInChildren<Animator>(), mission.hung, layout.hungRun, layout.hungIdle);
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
                MoveAlong(mission.player, namPath, Elapsed, 0, namPose);
                MoveAlong(mission.hung, hungPath, Elapsed, .3f, hungPose);
            }
            if (Elapsed >= duration || skipHeld >= 1)
            {
                var actions = mission.player.GetComponent<Map01NamActions>();
                if (CurrentMode == Mode.Takedown && actions != null && actions.Busy) return;
                Finish();
            }
        }
        private void MoveAlong(Transform root, Vector3[] points, float time, float delay, PosePlayer pose)
        {
            float travelSeconds = duration - delay - 1.3f;
            float u = Mathf.Clamp01((time - delay) / travelSeconds);
            // Gentle acceleration/deceleration, constant pace through the middle of the path.
            float progress = u < .1f ? u * u / .18f : u > .9f ? 1 - (1-u)*(1-u)/.18f : (u-.05f)/.9f;
            float length = Length(points), d = Mathf.Clamp01(progress) * length;
            int segment = 1;
            while (segment < points.Length - 1 && d > Vector3.Distance(points[segment-1], points[segment]))
            { d -= Vector3.Distance(points[segment-1], points[segment]); segment++; }
            var delta = points[segment]-points[segment-1];
            root.position = Vector3.Lerp(points[segment-1], points[segment], delta.magnitude > .001f ? d/delta.magnitude : 1);
            var facing = Vector3.ProjectOnPlane(delta, Vector3.up);
            if (facing.sqrMagnitude > .001f) root.rotation = Quaternion.RotateTowards(root.rotation, Quaternion.LookRotation(facing), 260 * Time.deltaTime);
            float movement = time > delay && u < 1 ? Mathf.Min(Mathf.Clamp01(u/.06f), Mathf.Clamp01((1-u)/.08f)) : 0;
            // Advance the cycle by distance actually covered, including acceleration on the ramp.
            float runSpeed=root==mission.player&&mission.ModernCombat.EquippedKind==WeaponKind.Rifle?rescue.Layout.namRunSpeed:rescue.Layout.hungRunSpeed;
            pose.Sample(Mathf.Clamp01(progress)*length / Mathf.Max(.1f,runSpeed), movement);
        }
        private static float Length(Vector3[] p) { float d=0;for(int i=1;i<p.Length;i++)d+=Vector3.Distance(p[i-1],p[i]);return d; }
        private bool CameraClear(Vector3 focus, Vector3 camera)
        {
            if (mission.SightCover != null && mission.SightCover.Blocks(focus, camera)) return false;
            return !Physics.Linecast(focus, camera, mission.ObstructionMask, QueryTriggerInteraction.Ignore);
        }
        private void LateUpdate()
        {
            if (!IsPlaying) return;
            if (CurrentMode == Mode.Takedown)
            {
                float u = (Elapsed - 2.05f) / rescue.Layout.killCameraSeconds;
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
                namPose?.Ground(); hungPose?.Ground();
                var target=Vector3.Lerp(mission.player.position,mission.hung.position,.5f)+Vector3.up;
                var layout=rescue.Layout;
                var outside=target+mission.player.rotation*layout.shelterCameraOffset;
                // Keep the view along the narrow ramp instead of looking through its retaining wall.
                var corridor=target-mission.player.forward*2.5f+Vector3.up*1.25f;
                float entering=1-Mathf.SmoothStep(0,1,(mission.player.position.y-.6f)/1.8f);
                var desired=Vector3.Lerp(outside,corridor,entering);
                bool indoors=mission.player.position.y<.6f&&mission.hung.position.y<.6f
                    &&Vector3.Distance(mission.hung.position,layout.reportPoint.position)<7;
                if(indoors)desired=layout.reportPoint.position+layout.interiorCameraOffset;
                shelterCameraPosition=Vector3.SmoothDamp(shelterCameraPosition,desired,ref cameraVelocity,Mathf.Max(.08f,layout.cameraDamping),25,Time.deltaTime);
                // Validate the blended position as well as its goal, so the camera cannot cross a wall.
                var offset=shelterCameraPosition-target;
                if(offset.sqrMagnitude>.001f){
                    // The focus is between two passengers. Their capsules must never push
                    // the lens into a body; only scenery obstructs this cinematic view.
                    var blocking=Physics.SphereCastAll(target,.18f,offset.normalized,offset.magnitude,mission.ObstructionMask,QueryTriggerInteraction.Ignore)
                        .Where(h=>!h.transform.IsChildOf(mission.player)&&!h.transform.IsChildOf(mission.hung)
                            &&h.transform.GetComponentInParent<Health>()==null
                            &&h.transform.GetComponentInParent<Animator>()==null
                            &&h.transform.GetComponentInChildren<Animator>()==null)
                        .OrderBy(h=>h.distance).ToArray();
                    if(blocking.Length>0)shelterCameraPosition=target+offset.normalized*Mathf.Max(.3f,blocking[0].distance-.2f);
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
            if(mode==Mode.Shelter){MoveAlong(mission.player,namPath,duration+1,0,namPose);MoveAlong(mission.hung,hungPath,duration+1,.3f,hungPose);}
            Cleanup();
            if(mode==Mode.Shelter){rescue.CompleteDelivery();GetComponent<Map01SaveSystem>().SaveSlot(ForestSaveSlots.AutoSlot,out _,true);}
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
                var agent=mission.hung.GetComponent<NavMeshAgent>();if(agent!=null&&agent.enabled){agent.Warp(mission.hung.position);agent.ResetPath();}
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
                mixer=AnimationMixerPlayable.Create(graph,2);graph.Connect(idle,0,mixer,0);graph.Connect(run,0,mixer,1);
                AnimationPlayableOutput.Create(graph,"Run and settle",actor).SetSourcePlayable(mixer);graph.Play();
                skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>();
                contact=new Map01SkinContact(actor);
            }
            public void Sample(float time,float weight)
            {
                run.SetTime(time);idle.SetTime(time);mixer.SetInputWeight(0,1-weight);mixer.SetInputWeight(1,weight);graph.Evaluate(0);
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
