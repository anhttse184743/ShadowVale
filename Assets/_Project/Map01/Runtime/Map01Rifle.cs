using System.Linq;
using UnityEngine;
using ShadowVale.Gameplay.Combat;
namespace ShadowVale.Map01
{
    // One prop per actor. Both hand contacts belong to the pose, not a world-space barrel override.
    public sealed class Map01Rifle : MonoBehaviour
    {
        public Weapon weapon;
        public Transform support;
        public static Map01Rifle Attach(Animator actor)
        {
            if(actor==null || !actor.isHuman) return null;
            if(actor.runtimeAnimatorController!=null && actor.parameters.Any(p=>p.name==PlayerCombat.AnimatorParams.Weapon))
                actor.SetInteger(PlayerCombat.AnimatorParams.Weapon,(int)WeaponKind.Rifle);
            var owner=actor.GetComponent<Map01Rifle>();
            if(owner!=null) return owner;
            var assets=Map01ActorAssets.Load();
            if(assets==null || assets.rifle==null) return null;
            owner=actor.gameObject.AddComponent<Map01Rifle>();
            owner.weapon=actor.GetComponentsInChildren<Weapon>(true).FirstOrDefault(w=>w.IsGun);
            if(owner.weapon==null) owner.weapon=Instantiate(assets.rifle,actor.GetBoneTransform(HumanBodyBones.RightHand),false);
            owner.weapon.transform.SetParent(actor.GetBoneTransform(HumanBodyBones.RightHand),false);
            owner.weapon.transform.localPosition=assets.riflePosition;
            owner.weapon.transform.localRotation=Quaternion.Euler(assets.rifleEuler);
            owner.weapon.transform.localScale=Vector3.one*1.15f;
            owner.weapon.gameObject.SetActive(true);
            foreach(var renderer in owner.weapon.GetComponentsInChildren<Renderer>(true)) {renderer.enabled=true;renderer.forceRenderingOff=false;}
            owner.support=new GameObject("Left hand foregrip").transform;
            owner.support.SetParent(owner.weapon.transform,false);owner.support.localPosition=new Vector3(0,0,.25f);
            return owner;
        }
    }
}
