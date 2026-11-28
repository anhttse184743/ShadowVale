using System;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>Plays approved poses after legacy stance layers, while gameplay retains all timers and physics.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class NamMotionRefresh : MonoBehaviour
    {
        sealed class PropPlayer
        {
            public NamMotionData.Prop data;
            public GameObject root,detached;
            public Transform[] parts;
        }
        public NamMotionData Data { get; private set; }
        public string MotionId { get; private set; }
        public float SourceFrame { get; private set; }
        public bool ControlsPistol => Ready && !ControlsThrowable && combat.EquippedKind==WeaponKind.Pistol;
        public bool ControlsThrowable => Ready && (actions.StoneActionPlaying || inventory.ThrowableEquipped&&inventory.Count(inventory.Throwable)>0);
        public bool ControlsKnife => Ready && !ControlsThrowable && combat.EquippedKind==WeaponKind.Knife;
        public bool JumpPlaying => jumping||landing;
        public string ActiveProp { get; private set; }
        public float AppliedWeight { get; private set; }
        Map01Mission mission;Map01Inventory inventory;Map01NamActions actions;PlayerCombat combat;PlayerController motor;CharacterController body;Animator actor;Map01Pistol pistol;
        ForearmTwist twist;bool ownsTwist;
        Transform[] bones;bool[] upper;Transform hand;
        Dictionary<string,NamMotionData.Clip> clips;
        readonly Dictionary<string,PropPlayer> props=new();
        readonly Dictionary<Renderer,bool> knifeRenderers=new();
        readonly Dictionary<Renderer,bool> holsterRenderers=new();
        Transform[] pistolParts;
        Map01PistolAnimationData.Pose[] previous,last;
        float transitionAt,clock,gaitClock,aim,lastShot=-100,lastKnife=-100,heldAt,landedAt,jumpElapsed,drawAt=-100,holsterAt=-100;
        bool wasHolding,wasReloading,jumping,landing,grounded=true;
        uint jumpSequence;
        WeaponKind lastWeapon;
        bool Ready => Data!=null&&actor!=null&&actor.isActiveAndEnabled&&mission!=null&&mission.IsInitialized
            && !mission.Cinematic&&!combat.HandsOccupied&&(!mission.Stopped||mission.Paused)&&!(mission.ModernHealth?.IsDead??false)
            && actions!=null&&!actions.Busy&&!(mission.Binoculars?.Binoculars??false)&&!actions.NonThrowableUpperAction;

        public static NamMotionRefresh Install(Map01Mission owner)
        {
            var existing=owner.player.GetComponent<NamMotionRefresh>();if(existing!=null)return existing;
            var data=Resources.Load<NamMotionData>("Characters/NamMotionV3");
            if(data==null){Debug.LogError("Approved Nam v3 motion data is missing.");return null;}
            if(!NamMotionData.FitsSkeleton(owner.player.GetComponentInChildren<Animator>()?.transform,data.bones))return null;
            var result=owner.player.gameObject.AddComponent<NamMotionRefresh>();result.Bind(owner,data);return result;
        }
        void Bind(Map01Mission owner,NamMotionData data)
        {
            mission=owner;Data=data;inventory=owner.GetComponent<Map01Inventory>();actions=Map01NamActions.For(owner);combat=owner.ModernCombat;motor=owner.ModernPlayer;actor=owner.player.GetComponentInChildren<Animator>();pistol=GetComponent<Map01Pistol>();
            var map=actor.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
            bones=data.bones.Select(t=>map[t.name]).ToArray();upper=data.bones.Select(t=>Upper(t.name)).ToArray();hand=map["RightHand"];
            previous=new Map01PistolAnimationData.Pose[bones.Length];last=new Map01PistolAnimationData.Pose[bones.Length];
            for(int i=0;i<bones.Length;i++)last[i]=Map01PistolAnimationData.Pose.Read(bones[i]);
            clips=data.clips.ToDictionary(c=>c.id);twist=actor.GetComponent<ForearmTwist>();if(twist==null)twist=GetComponentInChildren<ForearmTwist>();
            foreach(var spec in data.props.Where(p=>p.key!="Pistol"))
            {
                var go=Instantiate(spec.prefab,hand,false);go.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);go.transform.localScale=Vector3.one;
                go.name=spec.key=="F1"?"grenade_f1 in hand":spec.key=="Smoke"?"smoke_bomb in hand":spec.key=="Stone"?"stone in hand":"Approved knife v3";
                var auxiliary=new GameObject(spec.key+" detached mechanism");auxiliary.transform.SetParent(hand,false);
                props[spec.key]=new PropPlayer{data=spec,root=go,detached=auxiliary,parts=spec.parts.Select(p=>go.transform.Find(p.track.name)).ToArray()};go.SetActive(false);auxiliary.SetActive(false);
            }
            if(pistol!=null&&pistol.GunTransform!=null)
            {
                var parts=pistol.GunTransform.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
                pistolParts=data.FindProp("Pistol").parts.Select(p=>parts.TryGetValue(p.track.name,out var t)?t:null).ToArray();
            }
            combat.Attacked+=Attacked;lastWeapon=combat.EquippedKind;jumpSequence=motor.JumpSequence;grounded=motor.IsGrounded;
        }
        static bool Upper(string n)=>n.StartsWith("Spine")||n.StartsWith("Neck")||n.StartsWith("Head")||new[]{"Left","Right"}.Any(s=>n.StartsWith(s+"Shoulder")||n.StartsWith(s+"Arm")||n.StartsWith(s+"ForeArm")||n.StartsWith(s+"Hand"));
        void Attacked(WeaponKind kind,Vector3 at){if(kind==WeaponKind.Pistol)lastShot=Time.time;if(kind==WeaponKind.Knife)lastKnife=Time.time;else holsterAt=-100;}
        NamMotionData.Clip Clip(string id)=>clips[id];
        string ThrowableKey()=>actions.StoneActionPlaying?Key(mission.GetComponent<Map01StoneThrow>().Item):Key(inventory.Throwable);
        static string Key(string item)=>item=="grenade_f1"?"f1":item=="smoke_bomb"?"smoke":"stone";
        static string PropKey(string key)=>key=="f1"?"F1":key=="smoke"?"Smoke":key=="stone"?"Stone":key=="knife"?"Knife":key=="pistol"?"Pistol":null;
        void LateUpdate()
        {
            if(Data==null)return;
            clock+=Time.deltaTime;
            if(!Ready){Release();wasHolding=false;jumping=landing=false;return;}
            gaitClock+=Time.deltaTime*Mathf.Max(0,motor.PlanarSpeed/(motor.IsSneaking?1.2f:motor.IsSprinting?6f:2.5f));
            bool throwable=ControlsThrowable,pistolOn=ControlsPistol,knife=ControlsKnife;
            bool holding=actions.ThrowableHeld;
            if(holding&&!wasHolding)heldAt=Time.time;wasHolding=holding;
            if(combat.EquippedKind!=lastWeapon)
            {
                if(knife)drawAt=Time.time;
                if(lastWeapon==WeaponKind.Knife&&!knife&&!throwable)holsterAt=Time.time;
                // Claude 26/11 (step 3): K-54 draw and holster, when the data carries them. One hand at a time: the knife goes
                // away before the pistol comes out, and the pistol before the knife.
                if(HasPistolDraw)
                {
                    if(lastWeapon==WeaponKind.Pistol&&!pistolOn&&!throwable)pistolHolsterAt=Time.time;
                    if(pistolOn&&lastWeapon!=WeaponKind.Pistol)pistolDrawAt=Time.time+Mathf.Max(0,1-(Time.time-holsterAt));
                    if(knife&&Time.time-pistolHolsterAt<Clip("pistol_holster").Seconds)drawAt=pistolHolsterAt+Clip("pistol_holster").Seconds;
                }
                lastWeapon=combat.EquippedKind;
            }
            // Only a running body leaves the ground. A teleport (save load, retry, a scripted placement) switches the controller off
            // for a moment; read as airborne, it played a jump and its landing on the spot.
            if(body==null)body=motor.GetComponent<CharacterController>();
            bool nowGrounded=!motor.enabled||body==null||!body.enabled||motor.IsGrounded;
            // Claude 26/11 (approved step 1): a placement a few centimetres up or a step down a kerb left the ground for a few
            // frames and played the whole jump and landing (0.55 s) on the spot. Only the jump key or a fall that lasts plays it,
            // and a teleport (further than any run moves in a frame) drops a jump that was under way.
            Vector3 root=motor.transform.position;
            if(rootKnown&&(root-lastRoot).sqrMagnitude>TeleportMetres*TeleportMetres&&motor.JumpSequence==jumpSequence){airborneFor=0;jumping=landing=false;}
            lastRoot=root;rootKnown=true;
            airborneFor=nowGrounded?0:airborneFor+Time.deltaTime;
            bool fell=!jumping&&airborneFor>=FallGraceSeconds&&airborneFor-Time.deltaTime<FallGraceSeconds;
            if(motor.JumpSequence!=jumpSequence||fell)
            {
                jumping=true;landing=false;jumpElapsed=0;jumpSequence=motor.JumpSequence;
            }
            if(jumping)
            {
                jumpElapsed+=Time.deltaTime;
                if(nowGrounded&&jumpElapsed>.04f){jumping=false;landing=true;landedAt=Time.time;}
            }
            if(landing&&Time.time-landedAt>=28f/60)landing=false;
            if(throwable||jumping||landing||combat.IsAiming||combat.IsReloading)holsterAt=-100;
            grounded=nowGrounded;
            string id=null,prop=null;float frame=0,bodyFrame=-1,bodyWeight=1;
            bool pistolHolstering=HasPistolDraw&&Time.time-pistolHolsterAt<Clip("pistol_holster").Seconds;
            bool owns=throwable||pistolOn||knife||jumping||landing||Time.time-holsterAt<1||pistolHolstering;
            bool pistolHidden=false;
            if(!owns){FadeOut();return;}
            fadeOutAt=-1;
            if(jumping||landing)
            {
                string key=throwable?ThrowableKey():pistolOn?"pistol":knife?"knife":"unarmed";id="jump_"+key;var c=Clip(id);
                float nominalFlight=2*Mathf.Sqrt(2*motor.JumpHeight/Mathf.Max(.01f,-motor.Gravity));
                float phase=jumping?8+45*Mathf.Clamp01(jumpElapsed/nominalFlight):54+(Time.time-landedAt)*60;
                frame=Mathf.Min(c.end,c.start+phase);prop=PropKey(key);
                if(combat.EquippedIsGun&&!pistolOn&&!throwable)bodyWeight=0; // Existing long-gun IK keeps its upper-body authority.
            }
            else if(Time.time-holsterAt<1)
            {
                id="knife_holster";frame=Clip(id).At(Time.time-holsterAt);prop="Knife";
                if(motor.PlanarSpeed>.05f)bodyFrame=-2;else if(motor.IsSneaking)bodyFrame=CrouchLegs();
            }
            else if(pistolHolstering)
            {
                id="pistol_holster";frame=Clip(id).At(Time.time-pistolHolsterAt);
                if(motor.PlanarSpeed>.05f)bodyFrame=-2;else if(motor.IsSneaking)bodyFrame=CrouchLegs();
            }
            else if(throwable)
            {
                string key=ThrowableKey(),prefix=key+(motor.IsSneaking?"_crouch_":"_");prop=PropKey(key);
                if(actions.ThrowableReleasing){id=prefix+"throw";frame=Clip(id).At(actions.ThrowableElapsed);}
                else if(holding)
                {
                    float t=Time.time-heldAt;var prep=Clip(prefix+"prepare");var wind=Clip(prefix+"wind");
                    if(t<prep.Seconds){id=prep.id;frame=prep.At(t);}
                    else if(t<prep.Seconds+wind.Seconds){id=wind.id;frame=wind.At(t-prep.Seconds);}
                    else{id=prefix+"aim";frame=Clip(id).At(t-prep.Seconds-wind.Seconds,true);}
                }
                else{id=prefix+"prepare";frame=Clip(id).start;}
                if(motor.PlanarSpeed>.05f)bodyFrame=-2;else if(motor.IsSneaking)bodyFrame=CrouchLegs();
            }
            else if(pistolOn)
            {
                bool levelled=combat.IsAiming||Time.time-lastShot<.6f;
                aim=Mathf.MoveTowards(aim,levelled?1:0,Time.deltaTime/.35f);
                bool drawing=HasPistolDraw&&Time.time>=pistolDrawAt&&Time.time-pistolDrawAt<Clip("pistol_draw").Seconds&&!levelled&&!combat.IsReloading;
                if(drawing){id="pistol_draw";frame=Clip(id).At(Time.time-pistolDrawAt);pistolHidden=frame<ClipEvent("pistol_draw");}
                else if(Time.time<pistolDrawAt&&!levelled){id=Locomotion("pistol");frame=MotionFrame(id);pistolHidden=true;}
                else if(combat.IsReloading){id="pistol_reload";frame=Clip(id).Normalized(combat.ReloadProgress);}
                else if(wasReloading){id="pistol_reload";frame=Clip(id).end;}
                else if(Time.time-lastShot<combat.WeaponFor(WeaponKind.Pistol).Cooldown){id="pistol_fire";frame=Clip(id).At(Time.time-lastShot);}
                else if(aim>0&&aim<1){id=levelled?"pistol_raise":"pistol_lower";frame=Clip(id).Normalized(levelled?aim:1-aim);}
                else if(aim>=1){id=motor.PlanarSpeed>.05f&&!motor.IsSneaking?"pistol_aim_walk":"pistol_aim";frame=Clip(id).At(clock,true);}
                else{id=Locomotion("pistol");frame=MotionFrame(id);}
                wasReloading=combat.IsReloading;prop="Pistol";
                bodyFrame=MotionFrame(Locomotion("pistol"));
            }
            else if(knife)
            {
                prop="Knife";
                if(Time.time-lastKnife<Clip("knife_attack").Seconds){id="knife_attack";frame=Clip(id).At(Time.time-lastKnife);}
                else if(Time.time-drawAt<1){id="knife_draw";frame=Clip(id).At(Time.time-drawAt);}
                else{id=Locomotion("knife");frame=MotionFrame(id);}
                bodyFrame=MotionFrame(Locomotion("knife"));
            }
            else{id="knife_holster";frame=Clip(id).At(Time.time-holsterAt);prop="Knife";}
            if(id==null){Release();return;}
            if(MotionId!=id)
            {
                Array.Copy(last,previous,last.Length);transitionAt=Time.time;MotionId=id;
                blendSeconds=BlendSeconds(id,frame,bodyWeight);
            }
            float blend=Mathf.SmoothStep(0,1,(Time.time-transitionAt)/blendSeconds);SourceFrame=frame;AppliedWeight=blend;
            if(bodyWeight>0&&!ownsTwist&&twist!=null){twist.BeginAuthoredPose();ownsTwist=true;}
            else if(bodyWeight==0&&ownsTwist&&twist!=null){twist.EndAuthoredPose();ownsTwist=false;}
            for(int i=0;i<bones.Length;i++)
            {
                if(bodyFrame==-2&&!upper[i]||bodyWeight==0&&upper[i]){last[i]=Map01PistolAnimationData.Pose.Read(bones[i]);continue;}
                float f=!upper[i]&&bodyFrame>=0?bodyFrame:frame;
                var pose=Data.bones[i].At(f);
                if(landing&&!upper[i])pose=Map01PistolAnimationData.Pose.Mix(pose,Map01PistolAnimationData.Pose.Read(bones[i]),Mathf.SmoothStep(0,1,(Time.time-landedAt-.18f)/.28f));
                pose=Map01PistolAnimationData.Pose.Mix(previous[i],pose,blend);pose.Apply(bones[i]);last[i]=pose;
            }
            ActiveProp=prop;
            foreach(var entry in props)
            {
                bool on=entry.Key==prop;
                bool released=on&&throwable&&actions.ThrowableReleasing&&actions.ThrowableElapsed>=Map01NamActions.StoneReleaseDelay;
                entry.Value.root.SetActive(on&&!released);entry.Value.detached.SetActive(on);
                if(on)ApplyProp(entry.Value,frame,released);
            }
            SetKnifeHidden(prop=="Knife");
            bool holstering=id=="knife_holster"||id=="pistol_holster";
            SetHolsterHidden(holstering||pistolHidden);
            if(holstering||id=="pistol_draw"||pistolHidden)combat.GunPresentationReady=false;
            ShowPistolVisual(id=="pistol_holster"&&frame<ClipEvent("pistol_holster"),frame);
            if(prop=="Pistol")ApplyPistol(frame);
        }
        // Crouched, a throw or the knife's holster is the upper body's: the legs keep the crouch they are in, the refreshed
        // knife or pistol crouch or the Animator's (long guns). The takes' own legs stood Nam up for the holster's second
        // after a knife-to-rifle switch, and the crouched throw sits 6 cm above the rifle crouch (2026-10-23).
        float CrouchLegs()=>combat.EquippedKind==WeaponKind.Knife?MotionFrame(Locomotion("knife"))
            :combat.EquippedKind==WeaponKind.Pistol?MotionFrame(Locomotion("pistol")):-2;
        string Locomotion(string kind)=>motor.IsSneaking?kind+"_crouch":motor.PlanarSpeed>.05f?kind+(motor.IsSprinting?"_run":"_walk"):kind+(kind=="knife"?"_idle":"_carry");
        float MotionSeconds()=>gaitClock;
        float MotionFrame(string id)=>Clip(id).At(motor.PlanarSpeed>.05f?MotionSeconds():id.EndsWith("_crouch")?0:clock,true);
        void ApplyProp(PropPlayer prop,float frame,bool released)
        {
            for(int i=0;i<prop.parts.Length;i++)
            {
                // The pin stays with the left hand. Map01StoneThrow owns the one airborne F1 lever.
                var t=prop.parts[i];bool detached=released&&t.name.StartsWith("ring",StringComparison.Ordinal);
                var parent=detached?prop.detached.transform:prop.root.transform;
                if(t.parent!=parent)t.SetParent(parent,false);
                prop.data.parts[i].track.At(frame).Apply(t);t.gameObject.SetActive(prop.data.parts[i].Visible(frame));
            }
        }
        void ApplyPistol(float frame)
        {
            if(pistol==null||pistol.GunTransform==null||pistolParts==null)return;
            var gun=pistol.GunTransform;
            // Gameplay restore may return the gun to the generic weapon anchor. The take is right-hand local.
            if(gun.parent!=hand)gun.SetParent(hand,false);
            var prop=Data.FindProp("Pistol");prop.root.At(frame).Apply(gun);
            for(int i=0;i<pistolParts.Length;i++)
            {
                var t=pistolParts[i];if(t==null)continue;
                var world=hand.localToWorldMatrix*NamMotionData.Matrix(prop.parts[i].track.At(frame));
                var local=t.parent.worldToLocalMatrix*world;
                new Map01PistolAnimationData.Pose{position=local.GetColumn(3),rotation=local.rotation,scale=local.lossyScale}.Apply(t);
                t.gameObject.SetActive(prop.parts[i].Visible(frame));
            }
        }
        void SetKnifeHidden(bool hidden)
        {
            var weapon=combat.WeaponFor(WeaponKind.Knife);
            if(hidden&&weapon!=null)
                foreach(var r in weapon.GetComponentsInChildren<Renderer>(true)){if(!knifeRenderers.ContainsKey(r))knifeRenderers[r]=r.enabled;r.enabled=false;}
            if(!hidden){foreach(var pair in knifeRenderers)if(pair.Key!=null)pair.Key.enabled=pair.Value;knifeRenderers.Clear();}
        }
        void SetHolsterHidden(bool hidden)
        {
            var weapon=combat.WeaponFor(combat.EquippedKind);
            if(hidden&&weapon!=null)
                foreach(var r in weapon.GetComponentsInChildren<Renderer>(true)){if(!holsterRenderers.ContainsKey(r))holsterRenderers[r]=r.enabled;r.enabled=false;}
            if(!hidden){foreach(var pair in holsterRenderers)if(pair.Key!=null)pair.Key.enabled=pair.Value;holsterRenderers.Clear();}
        }
        void Release()
        {
            ReleaseState();fadeOutAt=-1;
            if(bones!=null)for(int i=0;i<bones.Length;i++)last[i]=Map01PistolAnimationData.Pose.Read(bones[i]);
        }
        // Claude 26/11 (step 3). The K-54 draw/holster takes appended by T049NamArmFix (clip label "...|event=<frame>": the frame the
        // gun leaves the hand, or is in it). Equip switches the real pistol off before its holster plays, so a render-only copy
        // made inside an inactive holder (no Weapon script ever wakes on it) shows the gun going back into the holster.
        float pistolDrawAt=-100,pistolHolsterAt=-100;GameObject pistolVisual,pistolVisualHolder;
        bool HasPistolDraw=>clips!=null&&clips.ContainsKey("pistol_draw")&&clips.ContainsKey("pistol_holster");
        float ClipEvent(string id)
        {
            if(clips==null||!clips.TryGetValue(id,out var c)||c.label==null)return float.MaxValue;
            int at=c.label.IndexOf("|event=",StringComparison.Ordinal);
            return at>=0&&float.TryParse(c.label.Substring(at+7),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var f)?f:float.MaxValue;
        }
        void ShowPistolVisual(bool show,float frame)
        {
            if(!show){if(pistolVisual!=null)pistolVisual.SetActive(false);return;}
            if(pistolVisual==null)
            {
                if(pistol==null||pistol.GunTransform==null)return;
                pistolVisualHolder=new GameObject("K54 holster visual holder");pistolVisualHolder.SetActive(false);pistolVisualHolder.transform.SetParent(hand,false);
                pistolVisual=Instantiate(pistol.GunTransform.gameObject,pistolVisualHolder.transform,false);pistolVisual.name="K54 holster visual";
                foreach(var b in pistolVisual.GetComponentsInChildren<MonoBehaviour>(true))DestroyImmediate(b);
                foreach(var b in pistolVisual.GetComponentsInChildren<Behaviour>(true))b.enabled=false; // lights, audio: off (URP light data depends on the Light)
                foreach(var c in pistolVisual.GetComponentsInChildren<Collider>(true))DestroyImmediate(c);
                // Only the gun's meshes, each as the real gun has it: no muzzle flame, tracer or particles.
                var real=pistol.GunTransform.GetComponentsInChildren<Renderer>(true);var copy=pistolVisual.GetComponentsInChildren<Renderer>(true);
                for(int i=0;i<copy.Length;i++)
                    copy[i].enabled=(copy[i] is MeshRenderer||copy[i] is SkinnedMeshRenderer)&&i<real.Length&&real[i].enabled;
                pistolVisual.SetActive(true);pistolVisual.transform.SetParent(hand,false);pistolVisualHolder.SetActive(true);
                Destroy(pistolVisualHolder);
            }
            pistolVisual.SetActive(true);
            Data.FindProp("Pistol").root.At(frame).Apply(pistolVisual.transform);
        }
        void ReleaseState()
        {
            if(pistolVisual!=null)pistolVisual.SetActive(false);
            AppliedWeight=0;MotionId=null;ActiveProp=null;
            foreach(var p in props.Values){p.root.SetActive(false);p.detached.SetActive(false);}
            SetKnifeHidden(false);
            SetHolsterHidden(false);
            if(ownsTwist&&twist!=null)twist.EndAuthoredPose();ownsTwist=false;
        }

        // Claude 26/11 (approved step 1). Every take used to blend in over 0.1 s however far the hands had to go: a reload from
        // the low carry moved the wrist 20 cm a frame (about 12 m/s). The blend now lasts as long as the hands need at about
        // 3 m/s at its fastest, 0.1-0.4 s. Reactions (a stab, a shot, a throw, a jump) keep their snap.
        const float TeleportMetres=1.5f,FallGraceSeconds=.15f,FadeOutSeconds=.2f,MinBlend=.1f,MaxBlend=.4f,BlendSecondsPerMetre=.5f;
        float blendSeconds=MinBlend,fadeOutAt=-1,airborneFor;
        Vector3 lastRoot;bool rootKnown;
        Map01PistolAnimationData.Pose[] fadeFrom,measureHold;
        float BlendSeconds(string id,float frame,float bodyWeight)
        {
            if(bodyWeight==0||id.StartsWith("jump_",StringComparison.Ordinal)||id=="knife_attack"||id=="pistol_fire"||id.EndsWith("_throw",StringComparison.Ordinal))return MinBlend;
            var hips=actor.GetBoneTransform(HumanBodyBones.Hips);var left=actor.GetBoneTransform(HumanBodyBones.LeftHand);var right=actor.GetBoneTransform(HumanBodyBones.RightHand);
            if(hips==null||left==null||right==null)return MinBlend;
            if(measureHold==null||measureHold.Length!=bones.Length)measureHold=new Map01PistolAnimationData.Pose[bones.Length];
            for(int i=0;i<bones.Length;i++)if(upper[i])measureHold[i]=Map01PistolAnimationData.Pose.Read(bones[i]);
            for(int i=0;i<bones.Length;i++)if(upper[i])previous[i].Apply(bones[i]);
            Vector3 l0=hips.InverseTransformPoint(left.position),r0=hips.InverseTransformPoint(right.position);
            for(int i=0;i<bones.Length;i++)if(upper[i])Data.bones[i].At(frame).Apply(bones[i]);
            Vector3 l1=hips.InverseTransformPoint(left.position),r1=hips.InverseTransformPoint(right.position);
            for(int i=0;i<bones.Length;i++)if(upper[i])measureHold[i].Apply(bones[i]);
            float metres=Mathf.Max(hips.TransformVector(l1-l0).magnitude,hips.TransformVector(r1-r0).magnitude);
            return Mathf.Clamp(metres*BlendSecondsPerMetre,MinBlend,MaxBlend);
        }

        // Claude 26/11 (approved step 1). Handing the arms back to the Animator (to the rifle, or empty hands) cut over in one frame
        // (wrists 25-29 cm). The last pose is now held and faded out over the Animator's in 0.2 s. A cinematic, a story action or
        // death still takes the arms at once through Release().
        void FadeOut()
        {
            if(MotionId!=null)
            {
                if(fadeFrom==null||fadeFrom.Length!=last.Length)fadeFrom=new Map01PistolAnimationData.Pose[last.Length];
                Array.Copy(last,fadeFrom,last.Length);fadeOutAt=Time.time;ReleaseState();
            }
            if(fadeOutAt<0){Release();return;}
            float weight=1f-Mathf.SmoothStep(0,1,(Time.time-fadeOutAt)/FadeOutSeconds);
            if(weight<=0){Release();return;}
            for(int i=0;i<bones.Length;i++)
            {
                var pose=Map01PistolAnimationData.Pose.Mix(Map01PistolAnimationData.Pose.Read(bones[i]),fadeFrom[i],weight);
                pose.Apply(bones[i]);last[i]=pose;
            }
        }
        void OnDisable(){if(Data!=null)Release();}
        void OnDestroy()
        {
            if(combat!=null)combat.Attacked-=Attacked;
            if(pistolVisual!=null)Destroy(pistolVisual);
            foreach(var p in props.Values){if(p.root!=null)Destroy(p.root);if(p.detached!=null)Destroy(p.detached);}
        }
    }
}
