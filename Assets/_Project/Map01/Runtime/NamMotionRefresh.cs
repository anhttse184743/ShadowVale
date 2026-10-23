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
            && !mission.Cinematic&&(!mission.Stopped||mission.Paused)&&!(mission.ModernHealth?.IsDead??false)
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
                lastWeapon=combat.EquippedKind;
            }
            // Only a running body leaves the ground. A teleport (save load, retry, a scripted placement) switches the controller off
            // for a moment; read as airborne, it played a jump and its landing on the spot.
            if(body==null)body=motor.GetComponent<CharacterController>();
            bool nowGrounded=!motor.enabled||body==null||!body.enabled||motor.IsGrounded;
            if(motor.JumpSequence!=jumpSequence||(!nowGrounded&&grounded))
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
            bool owns=throwable||pistolOn||knife||jumping||landing||Time.time-holsterAt<1;
            if(!owns){Release();return;}
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
                if(combat.IsReloading){id="pistol_reload";frame=Clip(id).Normalized(combat.ReloadProgress);}
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
            }
            float blend=Mathf.SmoothStep(0,1,(Time.time-transitionAt)/.1f);SourceFrame=frame;AppliedWeight=blend;
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
            bool holstering=id=="knife_holster";
            SetHolsterHidden(holstering);
            if(holstering)combat.GunPresentationReady=false;
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
            var prop=Data.FindProp("Pistol");prop.root.At(frame).Apply(pistol.GunTransform);
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
            AppliedWeight=0;MotionId=null;ActiveProp=null;
            foreach(var p in props.Values){p.root.SetActive(false);p.detached.SetActive(false);}
            SetKnifeHidden(false);
            SetHolsterHidden(false);
            if(ownsTwist&&twist!=null)twist.EndAuthoredPose();ownsTwist=false;
            if(bones!=null)for(int i=0;i<bones.Length;i++)last[i]=Map01PistolAnimationData.Pose.Read(bones[i]);
        }
        void OnDisable(){if(Data!=null)Release();}
        void OnDestroy()
        {
            if(combat!=null)combat.Attacked-=Attacked;
            foreach(var p in props.Values){if(p.root!=null)Destroy(p.root);if(p.detached!=null)Destroy(p.detached);}
        }
    }
}
