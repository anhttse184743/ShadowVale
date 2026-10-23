using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>Replays the approved upper body and mechanical parts over existing Humanoid locomotion.</summary>
    [DefaultExecutionOrder(40)]
    public sealed class Map01Pistol : MonoBehaviour
    {
        CharacterHudSession mission;
        Map01PistolAnimationData data;
        PlayerCombat combat;
        Map01NamActions actions;
        Animator actor;
        Transform[] bones,parts;
        Transform gun,rightHand,magazine;
        ForearmTwist twist;
        bool ownsTwist;
        float aim,weight,shotAt=-100,clock;
        bool wasReloading;
        readonly List<HandSwap> swaps=new();
        sealed class HandSwap
        {
            public SkinnedMeshRenderer renderer; public Mesh mesh; public Material[] materials;
            public Transform[] bones;public Transform root;public Bounds bounds;
            public Map01PistolAnimationData.Hand replacement;
        }
        public float SourceFrame { get; private set; }=1;
        public float PoseWeight=>weight;
        public Weapon Weapon { get; private set; }
        public static Map01Pistol Install(Map01Mission owner)=>Install(CharacterHudSession.For(owner.gameObject));
        public static Map01Pistol Install(CharacterHudSession owner)
        {
            var existing=owner.player.GetComponent<Map01Pistol>();if(existing!=null){existing.mission=owner;existing.combat=owner.ModernCombat;existing.actions=Map01NamActions.For(owner.MapOne);return existing;}
            if(owner.Bundle?.weapons==null||!owner.Bundle.weapons.Any(w=>w!=null&&w.id=="pistol_k54"))return null;
            var asset=Resources.Load<Map01PistolAnimationData>("Weapons/K54V12");
            if(asset==null){Debug.LogError("K54 v12 animation asset is missing; rebuild ShadowVale/K54/Build approved assets.");return null;}
            var result=owner.player.gameObject.AddComponent<Map01Pistol>();result.Bind(owner,asset);return result;
        }
        void Bind(CharacterHudSession owner,Map01PistolAnimationData asset)
        {
            mission=owner;data=asset;combat=owner.ModernCombat;actor=GetComponentInChildren<Animator>();
            actions=Map01NamActions.For(owner.MapOne);
            var map=actor.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
            bones=data.bones.Select(t=>map[t.name]).ToArray();rightHand=map["RightHand"];
            gun=Instantiate(data.prefab,rightHand,false).transform;gun.name="K54 v12";
            parts=data.parts.Select(t=>gun.Find(t.name)).ToArray();
            magazine=parts.FirstOrDefault(t=>t!=null&&t.name=="magazine");
            Weapon=gun.GetComponent<Weapon>();var profile=owner.Bundle.weapons.First(w=>w.id=="pistol_k54");
            var nodes=gun.GetComponentsInChildren<Transform>(true);
            var muzzle=nodes.First(t=>t.name=="K54_Muzzle");
            Weapon.Configure(WeaponKind.Pistol,profile.damage,profile.range,profile.fire_rate,false,muzzle);
            combat.RegisterWeapon(Weapon,new WeaponTuning{magazineCapacity=profile.magazine_size,reloadSeconds=profile.reload_seconds});
            twist=actor.GetComponent<ForearmTwist>();if(twist==null)twist=GetComponentInChildren<ForearmTwist>();
            foreach(var hand in data.hands)
            {
                var renderer=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r=>r.name==hand.name);
                if(renderer==null)continue;
                swaps.Add(new HandSwap{renderer=renderer,mesh=renderer.sharedMesh,materials=renderer.sharedMaterials,bones=renderer.bones,root=renderer.rootBone,bounds=renderer.localBounds,replacement=hand});
                renderer.sharedMesh=hand.mesh;renderer.bones=hand.bones.Select(n=>map[n]).ToArray();renderer.rootBone=map[hand.rootBone];
                renderer.sharedMaterial=data.skin;renderer.localBounds=hand.bounds;
            }
            combat.Attacked+=OnShot;
        }
        void OnShot(WeaponKind kind,Vector3 point){if(kind==WeaponKind.Pistol)shotAt=Time.time;}
        bool Available => mission!=null&&combat!=null&&combat.EquippedKind==WeaponKind.Pistol&&actor!=null&&actor.isActiveAndEnabled
            && !mission.Cinematic && !(mission.ModernHealth?.IsDead??false) && GetComponent<PlayerController>()?.IsProne != true
            && !(mission.Binoculars?.Binoculars??false) && !(actions!=null&&(actions.Busy||actions.StoneActionPlaying));
        void LateUpdate()
        {
            if(data==null||combat==null)return;
            if(GetComponent<PlayerController>()?.IsProne==true && GetComponent<NamProneMotion>()?.CanReloadProne==true) {
                weight=0;ReleaseTwist();wasReloading=false;
                if(gun.parent!=rightHand)gun.SetParent(rightHand,false);
                data.weapon.At(153).Apply(gun);
                for(int i=0;i<parts.Length;i++)if(parts[i]!=null)data.parts[i].At(1).Apply(parts[i]);
                if(magazine!=null)magazine.gameObject.SetActive(true);
                return;
            }
            bool active=Available;
            // Story actions own the arms immediately. A weapon swap blends back into locomotion.
            bool interrupted=combat.EquippedKind==WeaponKind.Pistol&&!active;
            if(interrupted)combat.CancelReload();
            weight=interrupted?0:Mathf.MoveTowards(weight,active?1:0,Time.deltaTime/combat.StanceBlendSeconds);
            if(twist!=null){if(weight>0&&!ownsTwist){ownsTwist=true;twist.BeginAuthoredPose();}else if(weight<=0)ReleaseTwist();}
            if(weight<=0){if(combat.EquippedKind==WeaponKind.Pistol)combat.GunPresentationReady=false;return;}
            clock+=Time.deltaTime;
            bool levelled=combat.IsAiming||Time.time-shotAt<.6f;
            aim=Mathf.MoveTowards(aim,levelled?1:0,Time.deltaTime/.35f);
            if(combat.IsReloading)SourceFrame=Mathf.Lerp(301,492,combat.ReloadProgress);
            else if(wasReloading)SourceFrame=492;
            else if(Time.time-shotAt<Weapon.Cooldown)SourceFrame=Mathf.Lerp(253,300,(Time.time-shotAt)/Weapon.Cooldown);
            else if(aim>0&&aim<1)SourceFrame=levelled?Mathf.Lerp(81,152,aim):Mathf.Lerp(493,572,1-aim);
            else SourceFrame=aim>=1?153+Mathf.Repeat(clock*60,99):1+Mathf.Repeat(clock*60,79);
            wasReloading=combat.IsReloading;
            if(combat.EquippedKind==WeaponKind.Pistol)combat.GunPresentationReady=active && (aim>=.95f || Time.time-shotAt<Weapon.Cooldown);
            for(int i=0;i<bones.Length;i++)data.bones[i].At(SourceFrame).Apply(bones[i],weight);
            if(gun.parent!=rightHand)gun.SetParent(rightHand,false);
            data.weapon.At(SourceFrame).Apply(gun);
            for(int i=0;i<parts.Length;i++)if(parts[i]!=null)data.parts[i].At(SourceFrame).Apply(parts[i]);
            if(magazine!=null&&data.magazineVisible.Length>0)magazine.gameObject.SetActive(data.magazineVisible[Mathf.Clamp(Mathf.RoundToInt(SourceFrame)-1,0,data.magazineVisible.Length-1)]);
        }
        void ReleaseTwist(){if(ownsTwist&&twist!=null)twist.EndAuthoredPose();ownsTwist=false;}
        void OnDisable(){ReleaseTwist();if(combat!=null)combat.GunPresentationReady=true;}
        void OnDestroy()
        {
            ReleaseTwist();if(combat!=null)combat.Attacked-=OnShot;
            foreach(var swap in swaps)if(swap.renderer!=null){swap.renderer.sharedMesh=swap.mesh;swap.renderer.sharedMaterials=swap.materials;swap.renderer.bones=swap.bones;swap.renderer.rootBone=swap.root;swap.renderer.localBounds=swap.bounds;}
        }
    }
}
