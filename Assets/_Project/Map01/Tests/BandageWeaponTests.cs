using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    public sealed class BandageWeaponTests : ForestSceneTestBase
    {
        [UnityTest]
        public IEnumerator BandagingHidesWeaponsAndRestoresThemOnFinishCancelAndDisable()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode(); yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            foreach(var enemy in mission.Enemies) enemy.enabled=false;
            var combat=mission.ModernCombat;
            Assert.IsTrue(combat.Equip(WeaponKind.Rifle));
            var actions=Map01NamActions.For(mission);
            var inventory=mission.GetComponent<Map01Inventory>();
            var renderers=mission.player.GetComponentsInChildren<Weapon>(true).SelectMany(w=>w.GetComponentsInChildren<Renderer>(true)).Distinct().ToArray();
            Assert.IsNotEmpty(renderers);
            var visibility=renderers.Select(r=>r.forceRenderingOff).ToArray();
            int rounds=combat.RoundsInMagazine;
            inventory.Add("medkit_small",1);
            int kits=inventory.Count("medkit_small");
            mission.RestoreHealthFromSave(mission.Settings.playerHP-30);
            Assert.IsTrue(inventory.UseItem("medkit_small"));
            Assert.IsTrue(actions.Busy);
            Assert.IsTrue(renderers.All(r=>r.forceRenderingOff),"Hide immediately when bandaging begins.");
            Assert.AreEqual(kits-1,inventory.Count("medkit_small"));
            float until=Time.time+5;
            while(actions.Busy && Time.time<until)
            {
                yield return new WaitForEndOfFrame();
                if(actions.Busy)Assert.IsTrue(renderers.All(r=>r.forceRenderingOff),"The gun must stay hidden throughout the bandage.");
            }
            Assert.IsFalse(actions.Busy);
            yield return WaitGameSeconds(.25f);
            CollectionAssert.AreEqual(visibility,renderers.Select(r=>r.forceRenderingOff).ToArray());
            Assert.AreEqual(WeaponKind.Rifle,combat.EquippedKind);
            Assert.AreEqual(rounds,combat.RoundsInMagazine);

            Assert.IsTrue(actions.Play(Map01NamActions.Bandage,2,false));
            yield return new WaitForEndOfFrame();
            Assert.IsTrue(renderers.All(r=>r.forceRenderingOff));
            actions.CancelAction();
            CollectionAssert.AreEqual(visibility,renderers.Select(r=>r.forceRenderingOff).ToArray());

            Assert.IsTrue(actions.Play(Map01NamActions.Bandage,2,false));
            actions.enabled=false;
            CollectionAssert.AreEqual(visibility,renderers.Select(r=>r.forceRenderingOff).ToArray());
            actions.enabled=true;
            Assert.AreEqual(rounds,combat.RoundsInMagazine);

            actions.Hold(Map01NamActions.ThrowAim,true);
            yield return new WaitForEndOfFrame();
            Assert.IsTrue(renderers.All(r=>r.forceRenderingOff),"Stone aiming must continue to hide weapons.");
            actions.CancelAction();
            CollectionAssert.AreEqual(visibility,renderers.Select(r=>r.forceRenderingOff).ToArray());
            yield return new ExitPlayMode();
        }
    }
}
