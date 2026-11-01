using System;
using System.Collections.Generic;
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
    /// The fight from the boat: pursuers on the shore, and Nam and the commander turning their rifles on them
    /// (aim, arm IK, the safe-shot check).
    /// </summary>
    public sealed partial class Map01Extraction
    {
        private void SpawnPursuitIfNeeded()
        {
            if(mission.Enemies.Any(e=>e.Alive && SafeShot(namActor,namActor.GetBoneTransform(HumanBodyBones.Chest).position,e.transform.position+Vector3.up*1.3f))) return;
            var template=mission.Enemies.First(e=>e.name.StartsWith("Outpost guard "));
            for(int i=0;i<2;i++) {
                Vector3 chosen=default;bool found=false;
                for(int attempt=0;attempt<24;attempt++) {
                    var candidate=boatOrigin+new Vector3(-12-attempt*.6f,2,-8+i*3);
                    if(!NavMesh.SamplePosition(candidate,out var hit,6,NavMesh.AllAreas))continue;
                    var view=mission.gameCamera.WorldToViewportPoint(hit.position+Vector3.up);
                    if(view.z>0 && view.x>-.1f && view.x<1.1f && view.y>-.1f && view.y<1.1f)continue;
                    chosen=hit.position;found=true;break;
                }
                if(!found) continue; // Never materialize a soldier in the camera frustum.
                var clone=Instantiate(template.gameObject,chosen,Quaternion.identity,transform);clone.name="Cinematic pursuer "+i;
                foreach(var loot in clone.GetComponents<ForestPoint>())Destroy(loot);
                var enemy=clone.GetComponent<Map01EnemyController>();enemy.carriesSupplyKey=false;enemy.operationIndex=0;enemy.enabled=false;
                clone.GetComponent<Health>().Revive();
                var actor=clone.GetComponentInChildren<Animator>();actor.Rebind();actor.Update(0);Map01Rifle.Attach(actor);
                var agent=clone.GetComponent<NavMeshAgent>();
                if(agent.isOnNavMesh && NavMesh.SamplePosition(boatOrigin+new Vector3(-5,2,7+i*2),out var bank,5,NavMesh.AllAreas)) {agent.isStopped=false;agent.SetDestination(bank.position);}
                pursuit.Add(clone);
            }
        }
        private void PrepareShoreActors()
        {
            foreach(var enemy in mission.Enemies.Where(e=>e.Alive && !e.IsBoss && Vector3.Distance(e.transform.position,boat.position)<70)) {
                if(!enemy.enabled)continue;
                shoreEnemies.Add(enemy);enemy.enabled=false;
                var agent=enemy.GetComponent<NavMeshAgent>();
                if(agent.isOnNavMesh) {
                    var destination=boatOrigin+new Vector3(-5,2,2+(shoreEnemies.Count%3)*2);
                    if(NavMesh.SamplePosition(destination,out var hit,5,NavMesh.AllAreas)) {
                        agent.isStopped=false;agent.SetDestination(hit.position);
                    }
                }
            }
        }
        private void UpdateShoreActors()
        {
            foreach(var enemy in shoreEnemies.Where(e=>e!=null).Select(e=>e.gameObject).Concat(pursuit.Where(p=>p!=null))) {
                var health=enemy.GetComponent<Health>();var agent=enemy.GetComponent<NavMeshAgent>();
                if(health.IsDead) {if(agent.isOnNavMesh)agent.isStopped=true;continue;}
                var actor=enemy.GetComponentInChildren<Animator>();
                actor.SetFloat(PlayerCombat.AnimatorParams.Speed,agent.velocity.magnitude);
                if(agent.velocity.sqrMagnitude<.01f)enemy.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(boat.position-enemy.transform.position,Vector3.up));
                if(CurrentPhase==Phase.Departing && clock<5 && Time.unscaledTime>=nextEnemyShot) {
                    nextEnemyShot=Time.unscaledTime+3.5f;actor.SetTrigger(PlayerCombat.AnimatorParams.Attack);
                    var rifle=actor.GetComponent<Map01Rifle>();
                    if(rifle!=null) RetreatTrace(rifle.weapon.Muzzle.position,boat.position+boat.right*2.5f);
                }
            }
        }
        private IEnumerable<Transform> LivingShoreTargets() => mission.Enemies.Where(e=>e.Alive && !e.IsBoss).Select(e=>e.transform)
            .Concat(pursuit.Where(p=>p!=null && !p.GetComponent<Health>().IsDead).Select(p=>p.transform));
        readonly Dictionary<Map01Rifle,(Quaternion basis,Vector3 grip)> rifleGeometry=new();
        readonly Dictionary<Animator,Quaternion> supportWristFrames=new();
        (Quaternion basis,Vector3 grip) RifleGeometry(Map01Rifle rifle){
            if(rifleGeometry.TryGetValue(rifle,out var frame))return frame;
            var gun=rifle.weapon.transform;
            var handle=gun.GetComponentsInChildren<Renderer>().FirstOrDefault(r=>r.name.StartsWith("bl_grip_low"));
            var magazine=gun.GetComponentsInChildren<Renderer>().FirstOrDefault(r=>r.name.StartsWith("bm_magazine_low"));
            var grip=handle!=null?handle.bounds.center:gun.position;
            var barrel=(rifle.weapon.Muzzle.position-grip).normalized;
            var down=magazine!=null?Vector3.ProjectOnPlane(magazine.bounds.center-grip,barrel).normalized:-gun.up;
            frame=(Quaternion.LookRotation(gun.InverseTransformDirection(barrel),gun.InverseTransformDirection(-down)),gun.InverseTransformPoint(grip));
            rifleGeometry[rifle]=frame;return frame;
        }
        Quaternion AimGun(Map01Rifle rifle,Vector3 direction)=>Quaternion.LookRotation(direction,boat.up)*Quaternion.Inverse(RifleGeometry(rifle).basis);
        Vector3 GripWrist(Map01Rifle rifle,Transform hand,Vector3 grip,Quaternion handRotation,Quaternion gunRotation)=>
            grip-handRotation*Vector3.Scale(rifle.weapon.transform.localPosition,hand.lossyScale)-gunRotation*Vector3.Scale(RifleGeometry(rifle).grip,rifle.weapon.transform.lossyScale);
        sealed class HeldGunPose { public Vector3 grip;public Quaternion rotation; }
        readonly Dictionary<Animator,HeldGunPose> heldGunPoses=new();
        readonly Dictionary<Transform,Vector3> armPoles=new();
        readonly Dictionary<Transform,Quaternion> armFrames=new();
        readonly Dictionary<Transform,Quaternion> heldFingers=new();
        readonly float[] coverYaw=new float[2];
        void DriveHeldGun(Animator actor,Map01Rifle rifle,Vector3 grip,Quaternion rotation){
            if(!heldGunPoses.TryGetValue(actor,out var state)){
                state=new HeldGunPose{grip=actor.transform.InverseTransformPoint(rifle.weapon.transform.TransformPoint(RifleGeometry(rifle).grip)),rotation=Quaternion.Inverse(actor.transform.rotation)*rifle.weapon.transform.rotation};heldGunPoses[actor]=state;
                foreach(var handBone in new[]{HumanBodyBones.LeftHand,HumanBodyBones.RightHand})foreach(var finger in actor.GetBoneTransform(handBone).GetComponentsInChildren<Transform>())
                    if(new[]{"Thumb","Index","Middle","Ring","Pinky"}.Any(n=>finger.name.Contains(n)))heldFingers[finger]=finger.localRotation;
            }
            float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
            state.grip=Vector3.MoveTowards(state.grip,actor.transform.InverseTransformPoint(grip),1.5f*dt);
            state.rotation=Quaternion.RotateTowards(state.rotation,Quaternion.Inverse(actor.transform.rotation)*rotation,480*dt);
            var worldRotation=actor.transform.rotation*state.rotation;var hand=actor.GetBoneTransform(HumanBodyBones.RightHand);
            var handRotation=worldRotation*Quaternion.Inverse(rifle.weapon.transform.localRotation);
            var wrist=GripWrist(rifle,hand,actor.transform.TransformPoint(state.grip),handRotation,worldRotation);
            SolveCoverArm(actor,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,wrist,handRotation,1,1);
            SupportRifle(actor,rifle,1);
            foreach(var finger in heldFingers)if(finger.Key!=null)finger.Key.localRotation=finger.Value;
        }
        void TurnCoverChest(Animator actor,int index,float yaw){
            coverYaw[index]=Mathf.MoveTowards(coverYaw[index],yaw,180*Mathf.Min(Time.unscaledDeltaTime,.05f));
            var chest=actor.GetBoneTransform(HumanBodyBones.Chest);chest.rotation=Quaternion.AngleAxis(coverYaw[index],boat.up)*chest.rotation;
        }
        Quaternion ArmFrame(Transform bone,Transform child,Vector3 normal){
            if(!armFrames.TryGetValue(bone,out var basis))armFrames[bone]=basis=Quaternion.LookRotation(bone.InverseTransformDirection(child.position-bone.position),bone.InverseTransformDirection(normal));return basis;
        }
        void SolveHeldArm(Animator actor,Transform upper,Transform lower,Transform hand,Vector3 target,Quaternion wrist,float side,float weight){
            var origin=upper.position;float a=Vector3.Distance(origin,lower.position),b=Vector3.Distance(lower.position,hand.position);
            var axis=(target-origin).normalized;float d=Mathf.Clamp(Vector3.Distance(origin,target),Mathf.Abs(a-b)+.001f,(a+b)*.97f);target=origin+axis*d;
            var authored=Vector3.ProjectOnPlane(lower.position-origin,axis);
            var candidate=Vector3.ProjectOnPlane(actor.transform.right*side*.6f-boat.up*.7f,axis);
            Vector3 bend;
            if(armPoles.TryGetValue(upper,out var previous))bend=Vector3.ProjectOnPlane(actor.transform.TransformDirection(previous),axis);else bend=authored;
            if(bend.sqrMagnitude<.0001f)bend=candidate;if(bend.sqrMagnitude<.0001f)bend=Vector3.ProjectOnPlane(actor.transform.forward,axis);bend.Normalize();
            if(candidate.sqrMagnitude>.01f){candidate.Normalize();if(Vector3.Dot(candidate,bend)<0)candidate=-candidate;bend=Vector3.Slerp(bend,candidate,1-Mathf.Exp(-12*Mathf.Min(Time.unscaledDeltaTime,.05f))).normalized;}
            armPoles[upper]=actor.transform.InverseTransformDirection(bend);
            var originalNormal=Vector3.Cross(lower.position-origin,hand.position-lower.position);if(originalNormal.sqrMagnitude<.0001f)originalNormal=Vector3.Cross(bend,axis);originalNormal.Normalize();
            var upperFrame=ArmFrame(upper,lower,originalNormal);var lowerFrame=ArmFrame(lower,hand,originalNormal);
            float along=(a*a+d*d-b*b)/(2*d);var elbow=origin+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));var normal=Vector3.Cross(bend,axis).normalized;
            upper.rotation=Quaternion.Slerp(upper.rotation,Quaternion.LookRotation(elbow-origin,normal)*Quaternion.Inverse(upperFrame),weight);
            lower.rotation=Quaternion.Slerp(lower.rotation,Quaternion.LookRotation(target-lower.position,normal)*Quaternion.Inverse(lowerFrame),weight);
            hand.rotation=Quaternion.Slerp(hand.rotation,wrist,weight);
        }
        private void CoverDeparture()
        {
            for(int i=0;i<1;i++) {
                var actor=namActor;var pose=i==0?namPose:commanderPose;
                if(clock<coverReloadUntil[i] || pendingCoverShot[i]>0)continue;
                if(coverReloadUntil[i]>0) {
                    coverReloadUntil[i]=0;coverAcquiredAt[i]=clock;
                    nextCoverShot[i]=clock+.4f;pose.Play(seatedReady);
                }
                var origin=actor.GetBoneTransform(HumanBodyBones.RightUpperArm).position;
                var target=coverTargets[i];
                if(target==null || target.GetComponent<Health>().IsDead || Vector3.Distance(target.position,boat.position)>65 || (clock>aimUntil[i]&&!SafeShot(actor,origin,target.position+Vector3.up*1.3f))) {
                    target=LivingShoreTargets().Where(t=>Vector3.Distance(t.position,boat.position)<65)
                        .OrderBy(t=>Vector3.Distance(t.position,origin)+(t==coverTargets[1-i]?20:0))
                        .FirstOrDefault(t=>SafeShot(actor,origin,t.position+Vector3.up*1.3f));
                    coverTargets[i]=target;coverAcquiredAt[i]=clock;nextCoverShot[i]=clock+.4f;
                    if(target!=null)pose.Play(seatedReady);
                }
                if(target==null) {pose.Play(travel);continue;}
                lastTargets[i]=target.position;if(SafeShot(actor,origin,target.position+Vector3.up*1.3f))aimUntil[i]=clock+.6f;
                if(clock<nextCoverShot[i])continue;
                if(coverRounds[i]>0 && coverRounds[i]%12==0) {
                    pose.Play(seatedReload,true);coverReloadUntil[i]=clock+seatedReload.length;
                    coverRounds[i]++;continue;
                }
                pose.Play(seatedFire,true);pendingCoverShot[i]=clock+.06f;
                nextCoverShot[i]=clock+(++coverRounds[i]%3==0?2.2f:.48f);
            }
        }
        private void AimAndFireCover(int index)
        {
            var target=coverTargets[index];
            if(target==null || target.GetComponent<Health>().IsDead) {
                pendingCoverShot[index]=0;
                var watcher=index==0?namActor:commanderActor;TurnCoverChest(watcher,index,0);
                namBoatCarry?.Draw();if(index==0)namPose?.FreeArms(false);HoldSeatedRifle(watcher,.14f);
                var head=watcher.GetBoneTransform(HumanBodyBones.Head);
                head.rotation=Quaternion.AngleAxis(Mathf.Sin(clock*.7f+index)*8,boat.up)*head.rotation;
                return;
            }
            namBoatCarry?.Draw();if(index==0)namPose?.FreeArms(false);
            if(clock<coverReloadUntil[index]){heldGunPoses.Remove(namActor);return;}
            var actor=index==0?namActor:commanderActor;var gun=actor.GetComponent<Map01Rifle>();
            if(gun==null)return;
            var chest=actor.GetBoneTransform(HumanBodyBones.Chest);var end=target.position+Vector3.up*1.3f;
            // Seated chest bones sit low on this rig. Aim from the shoulders and solve both grips,
            // rather than tipping the entire torso (and lowering the muzzle into the jetty).
            Vector3 direction=(end-actor.GetBoneTransform(HumanBodyBones.RightUpperArm).position).normalized;
            float yaw=Mathf.Clamp(Vector3.SignedAngle(actor.transform.forward,Vector3.ProjectOnPlane(direction,boat.up),boat.up),-45,45);
            float aimWeight=Mathf.SmoothStep(0,1,(clock-coverAcquiredAt[index])/.35f);
            TurnCoverChest(actor,index,yaw*aimWeight);
            float recoil=Mathf.Exp(-Mathf.Max(0,clock-lastCoverShot[index])*22);
            var shoulder=actor.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Vector3 trigger=shoulder.position+direction*(.10f-.025f*recoil)-actor.transform.right*.20f-boat.up*.10f;
            Quaternion rifleRotation=AimGun(gun,(end-trigger).normalized);
            DriveHeldGun(actor,gun,trigger,rifleRotation);
            if(pendingCoverShot[index]<=0 || clock<pendingCoverShot[index])return;
            pendingCoverShot[index]=0;
            var origin=gun.weapon.Muzzle.position;
            if(!SafeShot(actor,origin,end))return;
            if(coverRounds[index]%3==1) RetreatTrace(origin,end);
            if(rifleAudio!=null)motor.PlayOneShot(rifleAudio,.32f);
            var flash=new GameObject("Cinematic muzzle flash");flash.transform.position=origin;
            var light=flash.AddComponent<Light>();light.color=new Color(1,.7f,.25f);light.range=.65f;light.intensity=.7f;Destroy(flash,.035f);
            lastCoverShot[index]=clock;
            if(index==0)NamCoverShots++;else CommanderCoverShots++;
            var health=target.GetComponent<Health>();var enemy=target.GetComponent<Map01EnemyController>();
            if(enemy!=null)enemy.ReceiveExtractionHit(health.Max*.38f,end);
            else health.TakeDamage(health.Max*.38f,end,null);
            if(health.IsDead)CoverKills++;
        }
        // Correct contacts during waiting/lowering as well as active fire. The wrist is
        // behind the palm contact, not at the foregrip itself (these rigs have Hand_end).
        private void SupportRifle(Animator actor,Map01Rifle gun,float weight)
        {
            var hand=actor.GetBoneTransform(HumanBodyBones.LeftHand);
            var lower=actor.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            var tip=hand.Cast<Transform>().FirstOrDefault(t=>t.name.IndexOf("end",StringComparison.OrdinalIgnoreCase)>=0);
            Vector3 localPalm=tip!=null?hand.InverseTransformPoint(tip.position)*.45f:Vector3.zero;
            Quaternion neutral=lower.rotation;
            Vector3 localFingers=tip!=null?hand.InverseTransformDirection(tip.position-hand.position).normalized:Vector3.right;
            // Fingers wrap across the barrel; the forearm supplies a stable roll reference.
            if(!supportWristFrames.TryGetValue(actor,out var gripRotation)){
                var initial=Quaternion.FromToRotation(neutral*localFingers,gun.weapon.transform.up)*neutral;
                supportWristFrames[actor]=gripRotation=Quaternion.Inverse(gun.weapon.transform.rotation)*initial;
            }
            Quaternion wrist=actor==namActor?gun.weapon.transform.rotation*gripRotation:Quaternion.FromToRotation(neutral*localFingers,gun.weapon.transform.up)*neutral;
            var upper=actor.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            float reach=(Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,hand.position))*.96f;
            Vector3 palmOffset=wrist*Vector3.Scale(localPalm,hand.lossyScale);
            // The new commander has shorter arms than Hung. Move the rifle through its holding hand,
            // rather than stretching the support arm or leaving its palm floating off the foregrip.
            for(int i=0;i<4 && weight>0&&actor!=namActor;i++) {
                Vector3 desired=gun.support.position-palmOffset;
                Vector3 delta=desired-upper.position;
                if(delta.magnitude<=reach)break;
                Vector3 correction=upper.position+delta.normalized*reach-desired;
                var holdingHand=actor.GetBoneTransform(HumanBodyBones.RightHand);
                SolveCoverArm(actor,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,
                    holdingHand.position+correction,holdingHand.rotation,1,weight);
            }
            Vector3 target=gun.support.position-palmOffset;
            SolveCoverArm(actor,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,target,wrist,-1,weight);
        }
        public void HoldSeatedRifle(Animator actor,float lowering)
        {
            var gun=actor.GetComponent<Map01Rifle>();if(gun==null||!gun.weapon.gameObject.activeInHierarchy)return;
            var shoulder=actor.GetBoneTransform(HumanBodyBones.RightUpperArm);var hand=actor.GetBoneTransform(HumanBodyBones.RightHand);
            Vector3 direction=(actor.transform.forward-boat.up*.18f).normalized;
            Vector3 trigger=shoulder.position+direction*.08f-actor.transform.right*.20f-boat.up*lowering;
            if(actor==namActor&&enabled){DriveHeldGun(actor,gun,trigger,AimGun(gun,direction));return;}
            Quaternion rotation=Quaternion.LookRotation(direction,boat.up)*Quaternion.Euler(0,0,-90)*Quaternion.Inverse(gun.weapon.transform.localRotation);
            Vector3 wrist=trigger-rotation*Vector3.Scale(gun.weapon.transform.localPosition,hand.lossyScale);
            SolveCoverArm(actor,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,wrist,rotation,1,1);SupportRifle(actor,gun,1);
        }
        private void RetreatTrace(Vector3 from,Vector3 to)
        {
            var trail=new GameObject("Extraction faint tracer");
            var line=trail.AddComponent<LineRenderer>();line.sharedMaterial=mission.trailMaterial;
            line.positionCount=2;line.SetPosition(0,from);line.SetPosition(1,to);
            line.startWidth=.012f;line.endWidth=.003f;
            line.startColor=new Color(.8f,.68f,.4f,.45f);line.endColor=new Color(.8f,.68f,.4f,0);
            Destroy(trail,.045f);
        }
        private void SolveCoverArm(Animator actor,HumanBodyBones upperId,HumanBodyBones lowerId,HumanBodyBones handId,Vector3 target,Quaternion wrist,float side,float weight)
        {
            var upper=actor.GetBoneTransform(upperId);var lower=actor.GetBoneTransform(lowerId);var hand=actor.GetBoneTransform(handId);
            if(actor==namActor&&enabled){SolveHeldArm(actor,upper,lower,hand,target,wrist,side,weight);return;}
            var origin=upper.position;float a=Vector3.Distance(origin,lower.position),b=Vector3.Distance(lower.position,hand.position);
            var axis=(target-origin).normalized;float d=Mathf.Clamp(Vector3.Distance(origin,target),Mathf.Abs(a-b)+.001f,(a+b)*.97f);
            target=origin+axis*d;
            var bend=Vector3.ProjectOnPlane(actor.transform.right*side*.6f-boat.up*.7f,axis);
            if(bend.sqrMagnitude<.0001f)bend=Vector3.ProjectOnPlane(actor.transform.right*side*.6f-boat.up*.7f,axis);
            if(bend.sqrMagnitude<.0001f)bend=Vector3.ProjectOnPlane(actor.transform.forward,axis);
            bend.Normalize();
            float along=(a*a+d*d-b*b)/(2*d);
            var elbow=origin+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            upper.rotation=Quaternion.Slerp(upper.rotation,Quaternion.FromToRotation(lower.position-origin,elbow-origin)*upper.rotation,weight);
            lower.rotation=Quaternion.Slerp(lower.rotation,Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation,weight);
            hand.rotation=Quaternion.Slerp(hand.rotation,wrist,weight);
        }
        private bool SafeShot(Animator actor,Vector3 from,Vector3 to)
        {
            if(Vector3.Distance(from,to)>65)return false;
            if(Vector3.Angle(actor.transform.forward,Vector3.ProjectOnPlane(to-from,boat.up))>105)return false;
            foreach(var other in new[]{namActor,hungActor,commanderActor}) {
                if(other==actor)continue;
                var d=to-from;
                foreach(var bone in new[]{HumanBodyBones.Hips,HumanBodyBones.Chest,HumanBodyBones.Head}) {
                    var p=other.GetBoneTransform(bone).position;
                    float t=Mathf.Clamp01(Vector3.Dot(p-from,d)/Mathf.Max(.001f,d.sqrMagnitude));
                    if(Vector3.Distance(p,from+d*t)<.33f)return false;
                }
            }
            foreach(var hit in Physics.RaycastAll(from,(to-from).normalized,Vector3.Distance(from,to),mission.ObstructionMask,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance)) {
                if(hit.transform.IsChildOf(actor.transform))continue;
                var hitEnemy=hit.collider.GetComponentInParent<Map01EnemyController>();
                if(hitEnemy!=null) {
                    if(!hitEnemy.Alive)continue; // The old upright capsule does not match the fallen corpse.
                    return Vector3.Distance(hitEnemy.transform.position+Vector3.up*1.3f,to)<.3f;
                }
                return false;
            }
            return true;
        }
    }
}
