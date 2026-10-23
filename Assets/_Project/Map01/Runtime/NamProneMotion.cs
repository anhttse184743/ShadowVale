using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>Shared full-body crawl for both maps; the CharacterController owns travel.</summary>
    [DefaultExecutionOrder(80)]
    public sealed class NamProneMotion : MonoBehaviour
    {
        CharacterHudSession mission;
        PlayerController movement;
        PlayerCombat combat;
        Map01NamActions actions;
        Map01Inventory bag;
        Animator actor;
        ForearmTwist twist;
        bool ownsTwist;
        NamProneAnimationData data;
        Transform[] bones;
        Map01PistolAnimationData.Pose[] previous;
        Map01PistolAnimationData.Pose[] displayed;
        string selected;
        float weight, clock, aim, change = 1;
        Weapon seatedWeapon;
        Renderer gripRenderer, receiverRenderer, magazineRenderer;
        Transform rightHand;
        Transform oldMagazine, newMagazine;
        Renderer[] hiddenMagazines;
        bool[] magazineEnabled;
        Quaternion magazineHome;
        Vector3 magazineOffset;
        ReloadMagazineCarrier magazineCarrier;
        bool reloadingPose;
        public bool CanReloadProne => enabled && data != null && data.Find("reload") != null
            && combat != null && combat.EquippedIsGun && Available;
        public float ReloadPoseProgress { get; private set; }
        public float PoseWeight => weight;
        public string SelectedPose => selected;
        public static NamProneMotion Install(CharacterHudSession owner)
        {
            if (owner?.player == null) return null;
            var result = owner.player.GetComponent<NamProneMotion>();
            if (result != null) return result;
            var data = Resources.Load<NamProneAnimationData>("Animations/NamProne");
            if (data == null) { Debug.LogError("Missing shared Nam prone animation data."); return null; }
            result = owner.player.gameObject.AddComponent<NamProneMotion>();
            result.mission = owner; result.data = data;
            result.movement = owner.player.GetComponent<PlayerController>(); result.combat = owner.ModernCombat;
            result.actor = owner.player.GetComponentInChildren<Animator>();
            result.actions = owner.player.GetComponent<Map01NamActions>(); result.bag = owner.GetComponent<Map01Inventory>();
            var nodes = result.actor.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            result.bones = data.Find("unarmed").bones.Select(t => nodes[t.name]).ToArray();
            result.previous = new Map01PistolAnimationData.Pose[result.bones.Length];
            result.displayed = new Map01PistolAnimationData.Pose[result.bones.Length];
            result.twist = result.actor.GetComponent<ForearmTwist>();
            result.rightHand = result.actor.GetBoneTransform(HumanBodyBones.RightHand);
            result.movement.ProneAllowed = () => result.Available && result.movement.enabled && !result.combat.IsReloading;
            result.combat.ProneReloadAvailable = () => result != null && result.CanReloadProne;
            PlayerStealthAdapter.For(owner.player.gameObject).VisibilityProbe = () => VillageRiceInstances.Conceals(result.movement) ? 0f : 1f;
            return result;
        }
        bool Available => mission != null && !mission.Cinematic && !(mission.ModernHealth?.IsDead ?? false)
            && !(actions != null && actions.Busy) && !(mission.Binoculars?.Binoculars ?? false);
        void LateUpdate()
        {
            if (data == null || actor == null || movement == null) return;
            bool active = movement.IsProne && Available;
            bool reload = active && combat.IsReloading && CanReloadProne;
            if (reloadingPose && !reload) {
                RestoreMagazines();
                // Leaving prone cancels this take and its actual ammo transaction together.
                if (combat.IsReloading) combat.CancelReload();
            }
            reloadingPose = reload;
            // Standing story actions get their original full-body pose.
            if (movement.IsProne && !Available) movement.TrySetProne(false);
            weight = Mathf.MoveTowards(weight, active ? 1 : 0, Time.deltaTime / .24f);
            if (weight > 0 && !ownsTwist && twist != null) { twist.BeginAuthoredPose(); ownsTwist = true; }
            if (weight <= 0) { ReleaseTwist(); selected = null; return; }
            if (movement.PlanarSpeed > .03f) clock += Time.deltaTime * Mathf.Clamp(movement.PlanarSpeed / .65f, 0, 1.4f);
            string kind = bag != null && bag.ThrowableEquipped ? (bag.Throwable == "stone" ? "stone" : "grenade")
                : combat == null || combat.EquippedKind == WeaponKind.Unarmed ? "unarmed"
                : combat.EquippedKind == WeaponKind.Knife ? "knife" : "rifle";
            if (reload) kind = "reload";
            if (kind != selected) {
                // Animator has already evaluated its standing pose this frame.
                // Continue from the last prone output, including interrupted blends.
                for (int i = 0; i < bones.Length; i++) previous[i] = selected == null
                    ? Map01PistolAnimationData.Pose.Read(bones[i]) : displayed[i];
                selected = kind; change = 0;
            }
            change = Mathf.MoveTowards(change, 1, Time.deltaTime / .2f);
            aim = Mathf.MoveTowards(aim, combat != null && combat.EquippedIsGun && (combat.IsAiming || combat.AttackCooldownRemaining > 0) ? 1 : 0, Time.deltaTime / .3f);
            var take = data.Find(kind); var aimed = data.Find("aim");
            float frame = 1 + Mathf.Repeat(clock * data.fps, data.frames - 1);
            if (reload) {
                ReloadPoseProgress = combat.ReloadProgress;
                frame = 1 + ReloadPoseProgress * (take.bones[0].poses.Length - 1);
            }
            for (int i = 0; i < bones.Length; i++) {
                var pose = take.bones[i].At(frame);
                if (kind == "rifle") pose = Map01PistolAnimationData.Pose.Mix(pose, aimed.bones[i].At(frame), aim);
                if (change < 1) pose = Map01PistolAnimationData.Pose.Mix(previous[i], pose, Mathf.SmoothStep(0, 1, change));
                displayed[i] = pose;
                pose.Apply(bones[i], weight);
            }
            if((kind == "rifle" || reload) && take.grip != null && aimed.grip != null && combat != null) {
                var gun = combat.WeaponFor(combat.EquippedKind);
                if(gun != seatedWeapon) {
                    seatedWeapon = gun;
                    gripRenderer = gun != null ? gun.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name.StartsWith("bl_grip_low")) : null;
                    receiverRenderer = gun != null ? gun.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name.StartsWith("ul_dustcover")) : null;
                    magazineRenderer = gun != null ? gun.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name.StartsWith("bm_magazine_low")) : null;
                }
                if(gun != null) {
                    var seat=reload ? take.grip.At(frame) : Map01PistolAnimationData.Pose.Mix(take.grip.At(frame), aimed.grip.At(frame), aim);
                    Vector3 authoredCenter=rightHand.TransformPoint(seat.position);
                    Vector3 center=reload ? authoredCenter : authoredCenter-transform.up*.05f;
                    Quaternion authored=rightHand.rotation*seat.rotation;
                    Vector3 forward=authored*Vector3.forward;
                    // Keep the magazine down in both carry and aim. The source
                    // carry take was rolled sideways; gameplay uses an upright AK.
                    Quaternion facing=reload ? authored : Quaternion.LookRotation(forward,transform.up);
                    Quaternion handRoll=facing*Quaternion.Inverse(authored);
                    if (!reload) { RollHand(true,authoredCenter,handRoll); RollHand(false,authoredCenter,handRoll); }
                    // The muzzle marker only promises a forward axis. Derive roll from
                    // actual receiver/magazine geometry rather than its arbitrary up axis.
                    Vector3 up=receiverRenderer != null && magazineRenderer != null
                        ? receiverRenderer.bounds.center-magazineRenderer.bounds.center : gun.Muzzle.up;
                    Quaternion current=Quaternion.LookRotation(gun.Muzzle.forward,up);
                    Vector3 pivot=rightHand.position;
                    Quaternion turn=Quaternion.Slerp(Quaternion.identity,facing*Quaternion.Inverse(current),weight);
                    gun.transform.SetPositionAndRotation(pivot+turn*(gun.transform.position-pivot),turn*gun.transform.rotation);
                    if(gripRenderer != null)gun.transform.position+= (center-gripRenderer.bounds.center)*weight;
                    // Mosin keeps its internal magazine and per-round ammo commits. Only
                    // the body gesture is shared; no detachable AK magazine is fabricated.
                    if (reload && combat.EquippedKind != WeaponKind.Sniper) PresentMagazines(gun, take, frame);
                }
                combat.GunPresentationReady=true;
            }
        }
        void PresentMagazines(Weapon gun, NamProneAnimationData.Take take, float frame)
        {
            if (oldMagazine == null) {
                hiddenMagazines=gun.ReloadModel.GetComponentsInChildren<Renderer>(true)
                    .Where(r=>r.gameObject.activeInHierarchy && r.name.IndexOf("magazine",System.StringComparison.OrdinalIgnoreCase)>=0).ToArray();
                magazineEnabled=hiddenMagazines.Select(r=>r.enabled).ToArray();
                if(hiddenMagazines.Length==0)return;
                var center=hiddenMagazines.Aggregate(Vector3.zero,(sum,r)=>sum+r.bounds.center)/hiddenMagazines.Length;
                magazineHome=Quaternion.Inverse(actor.transform.rotation);
                magazineOffset=actor.transform.InverseTransformPoint(center)-take.oldMagazine.At(1).position;
                magazineCarrier=GetComponentInChildren<ReloadMagazineCarrier>();
                if(magazineCarrier!=null)magazineCarrier.ExternalPose=true;
                oldMagazine=new GameObject("Prone reload old magazine").transform;
                oldMagazine.SetPositionAndRotation(center,Quaternion.identity);
                foreach(var source in hiddenMagazines) {
                    var mesh=source.GetComponent<MeshFilter>();if(mesh==null)continue;
                    var part=new GameObject(source.name);part.layer=source.gameObject.layer;
                    part.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
                    part.transform.localScale=source.transform.lossyScale;
                    part.AddComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;
                    var renderer=part.AddComponent<MeshRenderer>();renderer.sharedMaterials=source.sharedMaterials;renderer.enabled=source.enabled;
                    part.transform.SetParent(oldMagazine,true);
                }
                newMagazine=Instantiate(oldMagazine.gameObject).transform;newMagazine.name="Prone reload replacement magazine";
                oldMagazine.SetParent(actor.transform,true);newMagazine.SetParent(actor.transform,true);
                for(int i=0;i<hiddenMagazines.Length;i++)hiddenMagazines[i].enabled=false;
            }
            if(oldMagazine==null)return;
            int key=Mathf.Clamp(Mathf.FloorToInt(frame-1),0,take.oldVisible.Length-1);
            void Place(Transform prop,Map01PistolAnimationData.Track track,bool visible) {
                var pose=track.At(frame);
                float u=(frame-1)/(take.oldVisible.Length-1);
                float seated=prop==oldMagazine ? 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.1f,.3f,u))
                    : Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,.84f,u));
                prop.localPosition=pose.position+magazineOffset*seated;prop.localRotation=pose.rotation*magazineHome;
                prop.gameObject.SetActive(visible);
            }
            Place(oldMagazine,take.oldMagazine,take.oldVisible[key]);Place(newMagazine,take.newMagazine,take.newVisible[key]);
        }
        void RestoreMagazines()
        {
            if(hiddenMagazines!=null)for(int i=0;i<hiddenMagazines.Length;i++)if(hiddenMagazines[i]!=null)hiddenMagazines[i].enabled=magazineEnabled[i];
            if(oldMagazine!=null)Destroy(oldMagazine.gameObject);if(newMagazine!=null)Destroy(newMagazine.gameObject);
            oldMagazine=newMagazine=null;hiddenMagazines=null;magazineEnabled=null;ReloadPoseProgress=0;
            if(magazineCarrier!=null)magazineCarrier.ExternalPose=false;magazineCarrier=null;
        }
        void RollHand(bool left,Vector3 pivot,Quaternion rotation)
        {
            var hand=actor.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            var elbow=actor.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
            Vector3 target=pivot+rotation*(hand.position-pivot);
            Quaternion orientation=rotation*hand.rotation;
            Map01ArmContact.Reach(actor,left,target,elbow.position,weight);
            hand.rotation=Quaternion.Slerp(hand.rotation,orientation,weight);
        }
        void ReleaseTwist() { if (ownsTwist && twist != null) twist.EndAuthoredPose(); ownsTwist = false; }
        void OnDisable() { RestoreMagazines(); if(reloadingPose && combat!=null)combat.CancelReload(); reloadingPose=false; ReleaseTwist(); weight = 0; selected = null; if (movement != null) movement.RestoreMotion(false); }
    }
}
