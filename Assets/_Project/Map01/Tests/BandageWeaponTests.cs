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
        [UnityTest,Timeout(240000)]public IEnumerator MapTwoUsesTheSameBandageAnimationAndReleasesControlsOnLoad()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Bag!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Bag);
            var preview=System.Type.GetType("ShadowVale.Map01.Editor.Map02RoutePreview, ShadowVale.Map01.Editor",true).GetMethod("ApplyToMission");preview.Invoke(null,new object[]{m,2});
            var health=m.Player.GetComponent<Health>();health.RestoreHealth(30);m.Bag.Receive("medkit_small",3);yield return null;
            var actions=Map01NamActions.For(CharacterHudSession.For(m.gameObject));var actor=m.Player.GetComponentInChildren<Animator>();
            var weapons=m.Player.GetComponentsInChildren<Weapon>(true).SelectMany(w=>w.GetComponentsInChildren<Renderer>(true)).Distinct().ToArray();
            var visibility=weapons.Select(r=>r.forceRenderingOff).ToArray();var combat=m.Player.GetComponent<PlayerCombat>();int magazine=combat.RoundsInMagazine;int kits=m.Bag.Count("medkit_small");
            Assert.IsTrue(m.Bag.UseItem("medkit_small"));Assert.IsTrue(actions.Busy);Assert.AreEqual(Map01NamActions.Bandage,actions.ActiveAction);Assert.AreEqual(kits-1,m.Bag.Count("medkit_small"));Assert.Greater(health.Current,30);
            yield return WaitGameSeconds(.35f);Assert.IsTrue(actor.GetCurrentAnimatorStateInfo(0).IsName("Act_Bandage"),"Map 2 must play the actual Map 1 bandage state.");Assert.IsTrue(weapons.All(r=>r.forceRenderingOff));Assert.IsFalse(m.Bag.UseItem("medkit_small"));Assert.AreEqual(kits-1,m.Bag.Count("medkit_small"));Assert.IsFalse(m.CanSave());
            deadline=Time.time+5;while(actions.Busy&&Time.time<deadline)yield return null;
            Assert.IsFalse(actions.Busy);yield return WaitGameSeconds(.25f);CollectionAssert.AreEqual(visibility,weapons.Select(r=>r.forceRenderingOff).ToArray());Assert.IsTrue(m.Player.GetComponent<ShadowVale.Gameplay.Player.PlayerController>().enabled);Assert.IsTrue(combat.enabled);Assert.AreEqual(magazine,combat.RoundsInMagazine);
            var saved=m.Capture();Assert.IsTrue(actions.Play(Map01NamActions.Bandage,3,true));m.Restore(saved);Assert.IsFalse(actions.Busy);Assert.IsTrue(combat.enabled);CollectionAssert.AreEqual(visibility,weapons.Select(r=>r.forceRenderingOff).ToArray());
            Assert.AreEqual(0,Object.FindObjectsByType<Map01Mission>(FindObjectsSortMode.None).Length);
            yield return new ExitPlayMode();
        }
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
