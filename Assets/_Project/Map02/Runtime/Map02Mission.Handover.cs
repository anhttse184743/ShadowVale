using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using ShadowVale.Gameplay.Skills;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Map 1 to Map 2 in one sitting: the boat carries Map 1's own Nam (with Map 1's drivers, poses and references on him)
    /// across the scene load. Once he is on the bank, the village plays with its own Nam, the same Player prefab, exactly
    /// as a Map 2 save or the test tools do, and is handed only what he carried: his health, the gun in his hands, the
    /// rounds in each magazine and his skills. The bag, stones and stamina already come from the arrival (26/11/2026).
    /// </summary>
    public sealed partial class Map02Mission
    {
        sealed class Carried
        {
            public float health=-1;public WeaponKind equipped=WeaponKind.Rifle;public bool hasCombat;
            public int rifle,pistol,sniper,current;public SkillProgress skills;public string skillRun;
        }
        Carried carried;

        /// <summary>Puts the village's Nam where the boat's Nam stands, notes what the boat's Nam carries, and retires him.</summary>
        PlayerController TakeOverFromBoat(Transform boatNam,PlayerController villageNam)
        {
            var oldHealth=boatNam.GetComponent<Health>();var oldCombat=boatNam.GetComponent<PlayerCombat>();
            var oldSkills=boatNam.GetComponent<PlayerSkills>();var oldCareer=boatNam.GetComponent<SkillCampaignPersistence>();
            carried=new Carried{health=oldHealth!=null?oldHealth.Current:-1,skills=oldSkills!=null?oldSkills.Capture():null};
            if(oldCombat!=null)
            {
                carried.hasCombat=true;carried.equipped=oldCombat.EquippedKind;carried.current=oldCombat.RoundsInMagazine;
                carried.rifle=oldCombat.MagazineRounds(WeaponKind.Rifle);carried.pistol=oldCombat.MagazineRounds(WeaponKind.Pistol);carried.sniper=oldCombat.MagazineRounds(WeaponKind.Sniper);
            }
            if(oldCareer!=null&&!string.IsNullOrEmpty(oldCareer.RunId)){oldCareer.TryFlush(out _);carried.skillRun=oldCareer.RunId;}

            var controller=villageNam.GetComponent<CharacterController>();if(controller!=null)controller.enabled=false;
            villageNam.transform.SetPositionAndRotation(boatNam.position,boatNam.rotation);
            villageNam.gameObject.SetActive(true);if(controller!=null)controller.enabled=true;
            boatNam.gameObject.SetActive(false);Physics.SyncTransforms();

            // Skills before InitializeSkills reads them: the same career run, the same levels.
            var skills=villageNam.GetComponent<PlayerSkills>();if(skills==null)skills=villageNam.gameObject.AddComponent<PlayerSkills>();
            if(carried.skills!=null)skills.Restore(carried.skills);
            if(!string.IsNullOrEmpty(carried.skillRun))
            {
                var career=villageNam.GetComponent<SkillCampaignPersistence>();if(career==null)career=villageNam.gameObject.AddComponent<SkillCampaignPersistence>();
                if(string.IsNullOrEmpty(career.RunId)){career.Initialize(carried.skillRun);career.Restore(skills.Capture(),null);}
            }
            return villageNam;
        }
        /// <summary>After the character is set up: the health and magazines he arrived with.</summary>
        void ApplyCarried()
        {
            if(carried==null)return;
            if(carried.health>0&&health!=null)health.RestoreHealth(carried.health);
            if(carried.hasCombat&&combat!=null)
            {
                combat.Equip(carried.equipped);
                combat.RestoreMagazine(WeaponKind.Rifle,carried.rifle);combat.RestoreMagazine(WeaponKind.Pistol,carried.pistol);combat.RestoreMagazine(WeaponKind.Sniper,carried.sniper);
                combat.RestoreMagazine(carried.current);
            }
            // As the boat does for its Nam when he steps off (Map02Arrival.Finish): the animator out of its default state and
            // into the carry of the gun in his hands, the gun back in his gameplay grip.
            if(combat!=null){combat.GunPresentationReady=true;combat.RestoreAfterCinematic();}
            player?.RestoreMotion(false);
            carried=null;
        }
        /// <summary>True once the village's own Nam has taken over from the boat's (tests).</summary>
        public bool PlaysVillageNam {get;private set;}
    }
}
