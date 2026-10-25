using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// The supplies Nam fetches and carries (the store's stock on the straw cart, what reaches the south shelter, the
    /// outline on what E would lift, the gun put away while carrying), the cast's size and footing beside Nam, Day 3
    /// scenes leaving Nam where he stood, and the Day 3 / Night 3 test-run entry points.
    /// </summary>
    public sealed class Map02SuppliesAndCastTests:ForestSceneTestBase
    {
        const string MapPath="Assets/_Project/Scenes/Maps/Map 2.unity";
        static Map02Mission mission;static Map02ChapterDirector chapter;
        static readonly string[] Adults={"Anh Thành","Anh Lực","Tâm","Chị Lành","Ông Sáu","Chú Bảy"};

        static IEnumerator Open()
        {
            float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){mission=Object.FindFirstObjectByType<Map02Mission>();if(mission?.Chapter!=null&&mission.Ready)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(mission?.Chapter);chapter=mission.Chapter;
        }
        // Every task before `before` done, `trips` trips of `tripsFor` counted, optionally carrying.
        static void Set(string before,string tripsFor=null,int trips=0,string carry=null,WeaponKind equipped=WeaponKind.Rifle)
        {
            var snap=mission.Capture();var s=new Map02ChapterState();
            foreach(var t in Map02ChapterDefinition.Load().tasks){if(t.id==before)break;s.Complete(t.id);}
            for(int k=0;k<trips;k++)s.Increment(tripsFor);
            s.carry=carry;snap.chapter=s;snap.equipped=(int)equipped;mission.Restore(snap);
        }
        static IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
        static string Stock(){var p=GameObject.Find("The store's stock on the cart");return p==null?"":string.Join(",",Enumerable.Range(0,p.transform.childCount).Select(i=>p.transform.GetChild(i).name));}
        static Map02ObjectiveObject Marker(string id)=>Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).First(m=>m.chapterId==id);
        static void Place(Vector3 p){var cc=mission.Player.GetComponent<CharacterController>();cc.enabled=false;mission.Player.position=p;cc.enabled=true;Physics.SyncTransforms();}
        static Animator Skeleton(Transform t)=>t.GetComponentsInChildren<Animator>().FirstOrDefault(a=>a.isHuman);
        static float Toes(Animator a)
        {
            Transform B(HumanBodyBones toes,HumanBodyBones foot)=>a.GetBoneTransform(toes)??a.GetBoneTransform(foot);
            return Mathf.Min(B(HumanBodyBones.LeftToes,HumanBodyBones.LeftFoot).position.y,B(HumanBodyBones.RightToes,HumanBodyBones.RightFoot).position.y);
        }
        static float HeadHeight(Animator a)=>a.GetBoneTransform(HumanBodyBones.Head).position.y-Toes(a);
        static float Ground(Transform who,Vector3 at)
        {
            foreach(var h in Physics.RaycastAll(at+Vector3.up,Vector3.down,6,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
                if(!h.transform.IsChildOf(who)&&h.collider is not CharacterController)return h.point.y;
            return float.NaN;
        }

        [UnityTest,Timeout(300000)]public IEnumerator SuppliesGoFromTheStoreCartToWhereTheStoryTakesThem()
        {
            EditorSceneManager.OpenScene(MapPath);yield return new EnterPlayMode();
            yield return SuppliesInPlayMode();
            yield return new ExitPlayMode();
        }
        static IEnumerator SuppliesInPlayMode()
        {
            yield return Open();
            Set("D1_04");yield return Frames(3);
            Assert.NotNull(chapter.StrawCart,"A straw cart stands by the store.");
            Assert.IsFalse(Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Where(o=>string.IsNullOrEmpty(o.chapterId))
                .SelectMany(o=>o.GetComponentsInChildren<MeshRenderer>()).Any(r=>r.enabled),"The pre-chapter grey blocks are not shown under the chapter.");
            Assert.IsFalse(Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Where(o=>string.IsNullOrEmpty(o.chapterId))
                .SelectMany(o=>o.GetComponentsInChildren<MeshFilter>()).Where(f=>f.sharedMesh!=null&&f.sharedMesh.name=="Cube").SelectMany(f=>f.GetComponents<Collider>()).Any(c=>c.enabled),"Nor do they block the way as invisible boxes.");
            Assert.AreEqual("",Stock(),"Nothing is on the cart before Day 3.");
            // Day 1: the landing's crates are the Day 1 props (carried and packed into the store by the Day 1 activity).
            Set("D1_04","D1_04",1);yield return Frames(3);Assert.AreEqual("",Stock(),"Day 1's crates go into the store, not onto the cart.");
            // Day 3 starts with exactly what Day 1 brought in, and E outlines the item it would lift.
            Set("D3_01");yield return Frames(3);Assert.AreEqual("ammo,medkit,grenades",Stock());
            Assert.IsFalse(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t=>t.name.StartsWith("Supply crate ")&&t.gameObject.activeInHierarchy),"From Day 3 the cart holds that stock; the Day 1 crates are not shown twice.");
            Assert.AreEqual("medkit",chapter.HighlightFor(Marker("D3_01"))?.name,"The first trip lifts the medicine.");
            Place(chapter.Sites["store"].position+Vector3.back*.6f);
            bool outlined=false;for(int i=0;i<30&&!outlined;i++){yield return null;outlined=InteractHighlight.Current!=null&&InteractHighlight.Current.name=="medkit";}
            Assert.IsTrue(outlined,"Walking up to the store outlines the medicine on the cart.");
            // Carrying: the bag is in Nam's arms, the rifle out of sight; it is gone from the cart.
            Set("D3_01",null,0,"D3_01");yield return Frames(5);
            Assert.AreEqual("medkit",chapter.HeldCargoKind);Assert.AreEqual("ammo,grenades",Stock());
            Assert.IsFalse(mission.Player.GetComponentsInChildren<Weapon>().Any(),"Both hands are on the load, even after a load: the rifle is put away.");
            // Set down: the rifle comes back, the bag stays at the south shelter.
            Set("D3_01","D3_01",1);yield return Frames(5);
            Assert.IsNull(chapter.HeldCargoKind);Assert.IsTrue(mission.Player.GetComponentsInChildren<Weapon>().Any(),"The rifle returns once the load is down.");
            Assert.AreEqual(1,GameObject.Find("Delivered: D3_01")?.transform.childCount,"The bag waits at the south shelter.");
            Assert.AreEqual("ammo",chapter.HighlightFor(Marker("D3_01"))?.name,"The second trip lifts the ammunition.");
            // The grenade crate for the bank hole is the last of the stock; afterwards the cart is empty.
            Set("D3_02");yield return Frames(3);Assert.AreEqual("grenades",Stock());
            Assert.AreEqual("grenades",chapter.HighlightFor(Marker("D3_02"))?.name);
            Assert.AreEqual(2,GameObject.Find("Delivered: D3_01")?.transform.childCount,"Both of Day 3's trips reached the south shelter.");
            Set("D3_03");yield return Frames(3);Assert.AreEqual("",Stock(),"Everything Day 1 stored has gone where Day 3 took it.");
            Assert.NotNull(chapter.CrateProp,"The grenade crate is in the bank hole.");
        }

        [UnityTest,Timeout(300000)]public IEnumerator TheCastStandsAsTallAsNamWithTheirFeetOnTheGround()
        {
            EditorSceneManager.OpenScene(MapPath);yield return new EnterPlayMode();
            yield return CastInPlayMode();
            yield return new ExitPlayMode();
        }
        static IEnumerator CastInPlayMode()
        {
            yield return Open();
            Set("D3_01");yield return Frames(40);   // the feet settle over the first frames
            float nam=HeadHeight(Skeleton(mission.Player));
            foreach(var role in Adults)
            {
                var a=Skeleton(chapter.Actor(role).visual);Assert.NotNull(a,role+" has a humanoid model");
                Assert.AreEqual(1,HeadHeight(a)/nam,.05f,role+" stands as tall as Nam (toes to head).");
            }
            var child=Skeleton(chapter.Actor("Bé Tí").visual);Assert.AreEqual(.66f,HeadHeight(child)/nam,.06f,"Bé Tí is a seven-year-old beside Nam.");
            foreach(var role in Adults.Concat(new[]{"Hùng","Bé Tí"}))
            {
                var actor=chapter.Actor(role);if(actor?.visual==null||!actor.visual.gameObject.activeInHierarchy)continue;
                var a=Skeleton(actor.visual);float lowest=float.PositiveInfinity;
                for(int i=0;i<30;i++){yield return null;lowest=Mathf.Min(lowest,Toes(a)-Ground(actor.visual,a.GetBoneTransform(HumanBodyBones.Hips).position));}
                Assert.Less(lowest,.035f,role+"'s planted foot is on the ground, not above it.");
                Assert.Greater(lowest,-.05f,role+" does not sink into the ground.");
            }
        }

        [UnityTest,Timeout(300000)]public IEnumerator HungWalksOngSauDownToTheDockStepsAfterTheFamilies()
        {
            EditorSceneManager.OpenScene(MapPath);yield return new EnterPlayMode();
            yield return StepsInPlayMode();
            yield return new ExitPlayMode();
        }
        static IEnumerator StepsInPlayMode()
        {
            yield return Open();
            Set("D3_04");yield return Frames(3);float settle=Time.time+1.5f;while(Time.time<settle)yield return null;   // past the day's fade
            var finish=typeof(Map02ChapterDirector).GetMethod("Finish",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            finish.Invoke(chapter,new object[]{Map02ChapterDefinition.Load().Find("D3_04")});
            Assert.IsTrue(chapter.StepsRehearsal,"Hùng sets off with Ông Sáu once the families are told.");Assert.IsFalse(mission.InputBlocked,"Nam keeps his controls meanwhile.");
            var hung=chapter.Actor("Hùng").transform;var sau=chapter.Actor("Ông Sáu").transform;float apart=0;bool reached=false;float joined=Time.time+3;   // he steps over to Hùng first
            Time.timeScale=4;
            try
            {
                float until=Time.realtimeSinceStartup+90;
                while(chapter.StepsRehearsal&&Time.realtimeSinceStartup<until)
                {
                    yield return null;
                    if(Time.time>joined)apart=Mathf.Max(apart,Vector3.Distance(hung.position,sau.position));
                    reached|=Vector3.Distance(hung.position,chapter.DockSteps)<1.2f;
                }
            }
            finally{Time.timeScale=1;}
            Assert.IsTrue(reached,"Hùng gets down to the dock steps.");
            Assert.IsFalse(chapter.StepsRehearsal,"…and brings the old man home again.");
            Assert.Less(apart,2.5f,"Ông Sáu stays at Hùng's side the whole way.");
            Assert.Less(Vector3.Distance(hung.position,chapter.Sites["family"].position),3,"Hùng is home for supper.");
        }

        [UnityTest,Timeout(300000)]public IEnumerator DayThreeScenesAndTestRunsStartWhereTheyShould()
        {
            EditorSceneManager.OpenScene(MapPath);yield return new EnterPlayMode();
            yield return ScenesInPlayMode();
            yield return new ExitPlayMode();
        }
        static IEnumerator ScenesInPlayMode()
        {
            yield return Open();
            // Day 3 scenes are staged for the place Nam stands; the face-to-face blocking of earlier days must not move him.
            foreach(var id in new[]{"D3_03","D3_04","D3_05"})
            {
                Set(id);yield return Frames(3);var marker=Marker(id);Place(marker.transform.position+Vector3.back*1.2f);yield return null;
                var before=mission.Player.position;Assert.IsTrue(mission.Interact(marker),id+" begins");
                yield return Frames(10);Assert.AreEqual(id,chapter.CurrentScene?.id);
                Assert.Less(Vector3.Distance(before,mission.Player.position),.05f,id+": Nam stays where he started the scene.");
                chapter.Cancel();
            }
            // ShadowVale > Map 2 > Chạy thử Ngày 3 / Đêm 3: the right day and night, Nam beside the next task.
            var apply=System.Type.GetType("ShadowVale.Map01.Editor.Map02RoutePreview, ShadowVale.Map01.Editor",true).GetMethod("ApplyAfter");
            foreach(var (after,phase,next) in new[]{("N2_05",4,"D3_01"),("D3_06",5,"N3_00"),("N3_07",5,"N3_08")})
            {
                apply.Invoke(null,new object[]{mission,after});yield return Frames(3);
                Assert.AreEqual(phase,chapter.State.phase,after);Assert.IsTrue(chapter.State.Done(after));Assert.IsFalse(chapter.State.Done(next));
                var site=chapter.Sites[Map02ChapterDefinition.Load().Find(next).site].position;
                Assert.Less(Vector3.Distance(site,mission.Player.position),6,after+": Nam starts beside "+next);
                Assert.Greater(mission.Inventory.Values.Sum(),0,"The test run hands Nam his kit.");
            }
        }
    }
}
