using UnityEngine;
using UnityEngine.InputSystem;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Nam's story moves, from the clips made in Blender (chuyen_dong): the knife takedown (played with
    /// the guard's matching clip), the stone throw, the binoculars, looting, bandaging and untying Hùng.
    /// Full-body moves are base-layer states of AC_Player (only the base layer places a humanoid's body):
    /// while one plays his own controls wait, the stance layer is lowered and the take's ground travel is
    /// applied to his model as root motion, then handed to his controller. The moves he can walk through
    /// (stone, binoculars) play on the masked "ActionsUpper" layer, raised only while they play.
    /// </summary>
    public sealed class Map01NamActions : MonoBehaviour
    {
        public const int Takedown = 1, ThrowAim = 2, Throw = 3, Binoculars = 4, BinocularsCrouch = 5,
            PickUp = 6, OpenChest = 7, Bandage = 8, Untie = 9;
        public const string UpperLayer = "ActionsUpper";

        /// <summary>Where the guard stands as the takedown starts, in Nam's frame (right, forward), from the Blender scene.</summary>
        public static readonly Vector2 TakedownGuardOffset = new Vector2(.028f, 1.038f);
        /// <summary>Raise the knife, one neck strike, release and recover. No preceding hip stab.</summary>
        public const float TakedownSeconds = 2.2f;
        public const float TakedownWindupSeconds=.28f, TakedownStrikeSeconds=.72f;
        public const float TakedownReadyFrame = 71f;
        /// <summary>Contact tables come from the source pair; raise directly to the neck-ready pose, omitting the hip strike.</summary>
        public static float TakedownSourceFrame(float seconds)
        {
            if(seconds<TakedownWindupSeconds)return Mathf.Lerp(1,TakedownReadyFrame,Mathf.SmoothStep(0,1,seconds/TakedownWindupSeconds));
            if(seconds<TakedownWindupSeconds+TakedownStrikeSeconds)return Mathf.Lerp(TakedownReadyFrame,87,(seconds-TakedownWindupSeconds)/TakedownStrikeSeconds);
            return Mathf.Lerp(87,141,Mathf.Clamp01((seconds-TakedownWindupSeconds-TakedownStrikeSeconds)/(TakedownSeconds-TakedownWindupSeconds-TakedownStrikeSeconds)));
        }
        /// <summary>Kneeling at Hùng's back working the knot loose (141 frames).</summary>
        public const float UntieSeconds = 141f / 30f;
        private const float Blend = .15f;

        private Map01Mission mission;
        private Map01Inventory inventory;
        private Animator actor;
        private PlayerController controller;
        private PlayerCombat combat;
        private CharacterController body;
        private int upperLayer = -1, stanceLayer = -1;
        private bool hasFullMoves;
        private float upperWeight, fullUntil, upperUntil;
        private bool locked, cancelable, holding, rootMotionWas;
        private bool stonePose;
        public const float StoneReleaseDelay=.18f;
        private float stoneReleaseAt;
        private GameObject handStone;
        private readonly System.Collections.Generic.List<(Transform bone,Quaternion rest,Vector3 axis,float curl)> stoneFingers=new();
        private string handItem;
        private Vector3 palmNormalLocal;
        private Transform stoneIndex,stoneLittle,stoneIndexTip,stoneLittleTip;
        private Transform stoneThumb,stoneThumbMiddle,stoneThumbTip;
        private readonly System.Collections.Generic.Dictionary<Renderer,bool> hiddenWeapons=new();
        private float bandageHideUntil;
        private bool BandageActionPlaying => (locked && ActiveAction == Bandage) || Time.time < bandageHideUntil;
        public bool StoneActionPlaying=>stonePose&&(holding||Time.time<upperUntil);
        public bool ThrowableHeld=>stonePose&&holding;
        public bool ThrowableReleasing=>stonePose&&!holding&&Time.time<upperUntil;
        public float ThrowableElapsed=>ThrowableReleasing?Mathf.Max(0,Time.time-(upperUntil-1.1f)):0;
        public bool NonThrowableUpperAction=>!stonePose&&(holding||Time.time<upperUntil)||BandageActionPlaying;

        private Vector3 modelPosition; private Quaternion modelRotation;

        /// <summary>A full-body move is playing; Nam's own controls are waiting.</summary>
        public bool Busy => locked;
        public int ActiveAction { get; private set; }

        public static Map01NamActions For(Map01Mission mission)
        {
            if (mission == null || mission.player == null) return null;
            var actions = mission.player.GetComponent<Map01NamActions>();
            if (actions == null) actions = mission.player.gameObject.AddComponent<Map01NamActions>();
            if (actions.mission == null) actions.Bind(mission);
            return actions;
        }

        private void Bind(Map01Mission owner)
        {
            mission = owner;
            inventory = owner.GetComponent<Map01Inventory>();
            actor = owner.player.GetComponentInChildren<Animator>();
            controller = owner.ModernPlayer; combat = owner.ModernCombat;
            body = owner.player.GetComponent<CharacterController>();
            if (actor == null || actor.runtimeAnimatorController == null) return;
            upperLayer = actor.GetLayerIndex(UpperLayer); stanceLayer = actor.GetLayerIndex(PlayerCombat.UpperBodyLayer);
            hasFullMoves = actor.HasState(0, Animator.StringToHash("Act_Takedown"));
            // Nam's fingers deliberately use generic bone curves, not Humanoid muscles.
            var rest=new System.Collections.Generic.Dictionary<string,Quaternion>();
            foreach(var bone in actor.avatar.humanDescription.skeleton)rest[bone.name]=bone.rotation;
            var nodes=actor.GetComponentsInChildren<Transform>(true);
            var saved=new System.Collections.Generic.List<(Transform bone,Quaternion rotation)>();
            foreach(var bone in nodes)if(bone!=actor.transform && rest.TryGetValue(bone.name,out var rotation)) {
                saved.Add((bone,bone.localRotation));bone.localRotation=rotation;
            }
            var hand=actor.GetBoneTransform(HumanBodyBones.RightHand);
            // The source rig's reference pose has palms down. Convert that anatomical
            // direction to each joint's own frame, instead of assuming a shared X axis.
            var palmNormal=-actor.transform.up;
            palmNormalLocal=hand.InverseTransformDirection(palmNormal);
            foreach(var bone in nodes) {
                if(bone.name=="RightHandIndex1")stoneIndex=bone;
                if(bone.name=="RightHandPinky1")stoneLittle=bone;
                if(bone.name=="RightHandIndex3")stoneIndexTip=bone;
                if(bone.name=="RightHandPinky3")stoneLittleTip=bone;
                if(bone.name=="RightHandThumb1")stoneThumb=bone;
                if(bone.name=="RightHandThumb2")stoneThumbMiddle=bone;
                if(bone.name=="RightHandThumb3")stoneThumbTip=bone;
                if(!bone.name.StartsWith("RightHand") || !rest.TryGetValue(bone.name,out var rotation))continue;
                int joint=bone.name[bone.name.Length-1]-'0';
                if(joint<1||joint>3)continue;
                bool thumb=bone.name.Contains("Thumb");
                float curl=thumb?(joint==1?35:joint==2?55:45):joint==1?50:joint==2?65:45;
                Vector3 forward=bone.childCount>0?bone.GetChild(0).localPosition.normalized:Vector3.up;
                var inward=bone.InverseTransformDirection(palmNormal);
                var axis=Vector3.Cross(forward,inward).normalized;
                stoneFingers.Add((bone,rotation,axis,curl));
            }
            foreach(var bone in saved)bone.bone.localRotation=bone.rotation;
        }

        private bool Ready(int layer) => actor != null && layer > 0 && actor.isActiveAndEnabled;
        private bool ReadyFull => actor != null && hasFullMoves && actor.isActiveAndEnabled;

        private void Fire(int id) { actor.SetInteger("Action", id); actor.SetTrigger("DoAction"); }

        /// <summary>A full-body move. <paramref name="canCancel"/>: walking off ends it early.</summary>
        public bool Play(int id, float seconds, bool canCancel = false)
        {
            if (!ReadyFull || locked) return false;
            locked = true; ActiveAction=id; cancelable = canCancel; fullUntil = Time.time + seconds;
            modelPosition = actor.transform.localPosition; modelRotation = actor.transform.localRotation;
            rootMotionWas = actor.applyRootMotion; actor.applyRootMotion = true;   // the take's own travel moves his model
            SetControls(false);
            if (id == Bandage) HideWeaponRenderers();
            Fire(id);
            return true;
        }

        /// <summary>An upper-body pose held for as long as <paramref name="on"/> (the stone drawn back, the glasses up).</summary>
        public void Hold(int id, bool on)
        {
            if (!Ready(upperLayer)) return;
            if (on && !holding) {
                stonePose=id==ThrowAim; holding = true; actor.SetBool("ActionHold", true);
                if(stonePose) {
                    actor.SetInteger("Action",id);actor.ResetTrigger("DoAction");
                    HoldStonePose();
                } else Fire(id);
            }
            else if (!on && holding) { holding = false; actor.SetBool("ActionHold", false); }
        }

        /// <summary>The stone leaves the hand: the throw plays on from the wind-up.</summary>
        public void ThrowNow()
        {
            if (!Ready(upperLayer)) return;
            stonePose=true;stoneReleaseAt=Time.time+StoneReleaseDelay;holding = false; actor.SetBool("ActionHold", false);
            Fire(Throw); upperUntil = Time.time + 1.1f;
        }

        /// <summary>
        /// The silent knife takedown: Nam steps in behind <paramref name="guard"/> exactly where the
        /// Blender scene has him, then the two clips run together — he pulls the guard back, covers his
        /// mouth and cuts, the guard drops where he stands.
        /// </summary>
        public bool PlayTakedown(Transform guard)
        {
            if (!ReadyFull || locked || guard == null) return false;
            Vector3 forward = Vector3.ProjectOnPlane(guard.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 1e-4f) forward = transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 spot = guard.position - forward * TakedownGuardOffset.y - right * TakedownGuardOffset.x;
            spot.y = GroundAt(spot, guard.position.y);
            // Both halves were baked from one origin (Nam's): the guard's take holds his body 1.04 m ahead of
            // it. His transform moves back onto Nam's spot, so his body stays exactly where he stood.
            var agent = guard.GetComponent<UnityEngine.AI.NavMeshAgent>();
            Vector3 guardSpot = new Vector3(spot.x, guard.position.y, spot.z);
            if (agent != null && agent.isOnNavMesh) agent.Warp(guardSpot); else guard.position = guardSpot;
            guard.rotation = Quaternion.LookRotation(forward);
            Teleport(spot, Quaternion.LookRotation(forward));
            if (!Play(Takedown, TakedownSeconds)) return false;
            // hands kept on his mouth and neck as in Blender, whatever the retargets did to the two bodies
            var ik = actor.GetComponent<Map01TakedownIK>();
            if (ik == null) ik = actor.gameObject.AddComponent<Map01TakedownIK>();
            ik.Begin(guard.GetComponentInChildren<Animator>(), guard);
            return true;
        }

        /// <summary>Kneels at Hùng's back and works the rope off his wrists.</summary>
        public bool PlayUntie(Transform hung)
        {
            if (!ReadyFull || locked || hung == null) return false;
            Vector3 forward = Vector3.ProjectOnPlane(hung.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 1e-4f) forward = transform.forward;
            Vector3 spot = hung.position - forward * .65f;   // the knot 0.52 m ahead of his hips, at Hùng's back
            spot.y = GroundAt(spot, hung.position.y);
            Teleport(spot, Quaternion.LookRotation(forward));
            return Play(Untie, UntieSeconds);
        }

        /// <summary>
        /// The ground under <paramref name="spot"/>: his own controls are off while a move plays, so nothing
        /// would settle him onto it afterwards. Falls back to the other actor's height.
        /// </summary>
        private float GroundAt(Vector3 spot, float fallback)
        {
            float best = float.NegativeInfinity;
            foreach (var hit in Physics.RaycastAll(spot + Vector3.up * 1.5f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.transform.GetComponentInParent<Map01EnemyController>() == null
                    && hit.transform.GetComponentInParent<Map01HungVisual>() == null && hit.normal.y > .6f && hit.point.y > best) best = hit.point.y;
            return float.IsNegativeInfinity(best) ? fallback : best;
        }

        private void Teleport(Vector3 position, Quaternion rotation)
        {
            bool was = body != null && body.enabled;
            if (was) body.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            if (was) body.enabled = true;
        }

        private void SetControls(bool on)
        {
            if (controller != null) controller.enabled = on;
            if (combat != null) combat.enabled = on;
            if (!on && actor != null) actor.SetFloat(PlayerCombat.AnimatorParams.Speed, 0);
        }

        /// <summary>Walking off, or going for the gun (fire / aim), cuts a cancelable move short.</summary>
        private static bool CancelPressed()
        {
            var kb = Keyboard.current; var mouse = Mouse.current;
            return (kb != null && (kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed))
                || (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed));
        }

        /// <summary>
        /// The move is over: the controller takes over where the take's travel left his model (the takedown
        /// alone carries him about half a metre) — same frame, so the body does not move — and the
        /// controls come back.
        /// </summary>
        private void Finish()
        {
            if (ActiveAction == Bandage) bandageHideUntil = Time.time + Blend;
            if (actor != null) {
                Vector3 travel = actor.transform.position - transform.TransformPoint(modelPosition); travel.y = 0;
                actor.applyRootMotion = rootMotionWas;
                actor.transform.SetLocalPositionAndRotation(modelPosition, modelRotation);
                if (travel.sqrMagnitude > 1e-6f) Teleport(transform.position + travel, transform.rotation);
                actor.SetInteger("Action", 0);   // a move cut short leaves its state (StoryAnimationSetup)
            }
            if (actor != null && actor.TryGetComponent(out Map01TakedownIK ik)) ik.End();
            locked = false; ActiveAction=0; cancelable = false;
            SetControls(true);
            combat?.RestoreAfterCinematic();
        }

        private void Update()
        {
            if (actor == null) return;
            if(stonePose && holding && Ready(upperLayer))HoldStonePose();
            bool dead = mission != null && mission.ModernHealth != null && mission.ModernHealth.IsDead;
            if (locked && (Time.time >= fullUntil || (cancelable && CancelPressed()) || dead))
                Finish();
            // the stance layer (weapon arms) would otherwise sit on top of a full-body move; PlayerCombat,
            // which drives it, is waiting meanwhile and raises it again afterwards
            if (locked && stanceLayer > 0) actor.SetLayerWeight(stanceLayer, Mathf.MoveTowards(actor.GetLayerWeight(stanceLayer), 0, Time.deltaTime / Blend));
            if (Ready(upperLayer)) {
                upperWeight = Mathf.MoveTowards(upperWeight, holding || Time.time < upperUntil ? 1 : 0, Time.deltaTime / Blend);
                actor.SetLayerWeight(upperLayer, upperWeight);
            }
        }

        private void HoldStonePose()
        {
            // A zero-speed non-looping state does not sample its cycle offset.
            // Seek the wind-up explicitly instead of freezing the first idle frame.
            bool crouched=controller!=null && controller.IsSneaking;
            actor.Play(crouched?"Act_ThrowAimCrouch":"Act_ThrowAim",upperLayer,crouched?0f:.42f);
        }

        private void LateUpdate()
        {
            // Keep the equipped weapon and ammunition unchanged while freeing the
            // throwing hand. Renderer overrides also cover weapon switches mid-aim.
            bool stone = StoneActionPlaying;
            if (stone || BandageActionPlaying || (inventory != null && inventory.ThrowableEquipped)) HideWeaponRenderers();
            else RestoreWeaponVisibility();
            bool selectedStone=inventory!=null && inventory.ThrowableEquipped && inventory.Count(inventory.Throwable)>0;
            bool releasing=stonePose && !holding && Time.time<upperUntil;
            bool showStone=!locked && !BandageActionPlaying && (holding || releasing
                ? stone && (holding||Time.time<stoneReleaseAt)
                : selectedStone);
            if(TryGetComponent<NamMotionRefresh>(out var refresh)&&refresh.ControlsThrowable){if(handStone!=null)handStone.SetActive(false);return;}
            if(showStone) {
                CurlStoneFingers();
                string item=holding||releasing?mission.GetComponent<Map01StoneThrow>().Item:inventory.Throwable;
                if(handStone!=null && handItem!=item) {Destroy(handStone);handStone=null;}
                if(handStone==null) {
                    handItem=item;
                    handStone=Map01StoneThrow.CreateHeldVisual(item,item=="stone"?"Stone in hand":item+" in hand");
                    var hand=actor.GetBoneTransform(HumanBodyBones.RightHand);
                    handStone.transform.SetParent(hand,false);
                    var handScale=hand.lossyScale;
                    handStone.transform.localScale=new Vector3(1f/Mathf.Max(.001f,Mathf.Abs(handScale.x)),1f/Mathf.Max(.001f,Mathf.Abs(handScale.y)),1f/Mathf.Max(.001f,Mathf.Abs(handScale.z)));
                }
                // Seat the pebble between the knuckles and the curled fingertips,
                // on the palm side rather than on the back of the hand.
                if(stoneIndex!=null && stoneLittle!=null && stoneIndexTip!=null && stoneLittleTip!=null) {
                    var knuckles=(stoneIndex.position+stoneLittle.position)*.5f;
                    var tips=(stoneIndexTip.position+stoneLittleTip.position)*.5f;
                    var palmNormal=handStone.transform.parent.TransformDirection(palmNormalLocal);
                    var forward=(knuckles-handStone.transform.parent.position).normalized;
                    handStone.transform.position=Vector3.Lerp(knuckles,tips,.3f)+palmNormal*.02f+forward*.028f;
                    if(item!="stone")handStone.transform.localRotation=Quaternion.FromToRotation(Vector3.up,
                        handStone.transform.parent.InverseTransformDirection(stoneIndex.position-stoneLittle.position).normalized);
                } else handStone.transform.localPosition=Vector3.up*.06f;
                CloseThumbOnItem();
                handStone.SetActive(true);
            }else if(handStone!=null)handStone.SetActive(false);
        }
        private void HideWeaponRenderers() {
            foreach(var weapon in GetComponentsInChildren<Weapon>(true))foreach(var r in weapon.GetComponentsInChildren<Renderer>(true)) {
                if(!hiddenWeapons.ContainsKey(r))hiddenWeapons.Add(r,r.forceRenderingOff);
                r.forceRenderingOff=true;
            }
        }
        private void CurlStoneFingers()
        {
            foreach(var finger in stoneFingers)finger.bone.localRotation=finger.rest*Quaternion.AngleAxis(finger.curl,finger.axis);
        }
        private void CloseThumbOnItem()
        {
            if(stoneThumb==null || stoneThumbMiddle==null || stoneThumbTip==null)return;
            // Thumb opposition comes from its base as well as flexion at its two
            // end joints. Aim the curled thumb at the near surface of the held item.
            float endLength=Vector3.Distance(stoneThumbMiddle.position,stoneThumbTip.position)*.8f;
            var tip=stoneThumbTip.position+stoneThumbTip.up*endLength;
            var center=handStone.transform.position;
            var target=center+(stoneThumb.position-center).normalized*.025f;
            var turn=Quaternion.FromToRotation(tip-stoneThumb.position,target-stoneThumb.position);
            stoneThumb.rotation=Quaternion.RotateTowards(Quaternion.identity,turn,65f)*stoneThumb.rotation;
        }
        private void RestoreWeaponVisibility() {
            foreach(var pair in hiddenWeapons)if(pair.Key!=null)pair.Key.forceRenderingOff=pair.Value;
            hiddenWeapons.Clear();stonePose=false;bandageHideUntil=0;
            if(handStone!=null)handStone.SetActive(false);
        }
        private void OnDestroy(){if(handStone!=null)Destroy(handStone);}
        public void CancelAction() { if (locked) Finish(); holding = false; upperUntil = 0; upperWeight = 0; if (Ready(upperLayer)) actor.SetLayerWeight(upperLayer, 0); RestoreWeaponVisibility(); }
        private void OnDisable() {
            if (locked) Finish(); holding = false;upperUntil=0;upperWeight=0;
            if(Ready(upperLayer))actor.SetLayerWeight(upperLayer,0);
            RestoreWeaponVisibility();
        }
    }
}
