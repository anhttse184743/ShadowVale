using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.AI.Perception;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Telemetry;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// Map 2 sends the web backend what Map 1 does (Map02Mission.Telemetry): player positions, noise, the squads' plans
    /// and enemy states, shots and kills by item code, chapter tasks as objectives in chapter order, the player's death
    /// and the failed or completed result. Played in the real scene from Night 3's first lane.
    /// </summary>
    public sealed class Map02TelemetryTests:ForestSceneTestBase
    {
        const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;

        [UnityTest,Timeout(300000)]public IEnumerator AMapTwoSessionCarriesWhatTheDashboardsRead()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            yield return InPlayMode();
            yield return new ExitPlayMode();
        }

        static IEnumerator InPlayMode()
        {
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Chapter!=null&&m.Ready)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.IsNotNull(m,"Map 2 is ready.");
            var c=m.Chapter;var health=m.Player.GetComponent<Health>();health.CinematicInvulnerable=true;
            var snap=m.Capture();var s=new Map02ChapterState();foreach(var t in Map02ChapterDefinition.Load().tasks){if(t.id=="N3_04")break;s.Complete(t.id);}
            snap.chapter=s;m.Restore(snap);float until=Time.time+1.5f;while(Time.time<until)yield return null;

            // A session of its own, kept on disk only (batch runs have telemetry off)
            string root=Path.Combine(Path.GetTempPath(),"svl-map02-telemetry-"+Guid.NewGuid().ToString("N"));
            var running=m.GetComponent<TelemetryService>();if(running!=null)running.End(upload:false);
            var service=TelemetryService.Attach(m.gameObject,Map02Mission.MapCode,new TelemetryConfig
            {
                UploadEnabled=false,PositionSampleSeconds=.5f,RootDirectory=Path.Combine(root,"telemetry"),ResearchDirectory=Path.Combine(root,"research"),
            });
            try
            {
                var lane=Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(o=>o.chapterId=="N3_04").transform.position;
                var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.position=lane;cc.enabled=true;Physics.SyncTransforms();
                m.CharacterNoise(m.Player.position,12,NoiseKind.Stone);
                until=Time.time+25;while(Time.time<until&&!service.Session.Events.Events.Any(e=>e.Type=="coordination_replan"))yield return null;

                // Shot and kill through the same handlers the combat events call, with what is in hand
                var combat=m.Player.GetComponent<PlayerCombat>();
                typeof(Map02Mission).GetMethod("TelemetryShot",Private).Invoke(m,new object[]{combat.EquippedKind,m.Player.position});
                var enemy=c.Enemies.First(e=>e!=null&&!e.Dead&&e.group=="N3_04");
                enemy.HitPoints.TakeDamage(enemy.HitPoints.Current,enemy.transform.position+Vector3.up,m.Player.gameObject);
                typeof(Map02Mission).GetMethod("TelemetryGunKill",Private).Invoke(m,new object[]{enemy.gameObject});
                typeof(Map02Mission).GetMethod("TelemetryTaskCompleted",Private).Invoke(m,new object[]{"N3_04"});

                // Nam falls: death, then the story fails
                health.CinematicInvulnerable=false;health.TakeDamage(health.Current,m.Player.position+Vector3.up,enemy.gameObject);
                until=Time.time+2;while(Time.time<until&&!m.World.story.failed)yield return null;
                yield return null;yield return null;

                var dto=service.End(upload:false);
                Assert.AreEqual("map02",dto.MapId);
                Assert.IsTrue(dto.Events.All(e=>e.MapId=="map02"),"Every event carries Map 2's code (heatmap filter).");
                Assert.Greater(dto.Events.Count(e=>e.Type=="player_position"),0,"The player is sampled: Map 2 has a heatmap.");
                Assert.IsTrue(dto.Events.Any(e=>e.Type=="noise_emitted"),"Map 2's noise reaches telemetry.");
                Assert.IsTrue(dto.Events.Any(e=>e.Type=="coordination_replan"),"The wave is planned and recorded: "+string.Join(",",dto.Summary.EventCounts.Keys));
                Assert.IsTrue(dto.Events.Any(e=>e.Type=="enemy_state_changed"));
                Assert.Greater(service.Session.Plans.Count,0,"Plans become coordination results for the AI comparison.");
                var shot=dto.Events.First(e=>e.Type=="shot_fired");
                StringAssert.DoesNotMatch("^(rifle|pistol|sniper|unarmed)$",(string)shot.Payload["weapon"],"Shots are counted by item code, not weapon kind.");
                var kill=dto.Events.Single(e=>e.Type=="enemy_killed");
                Assert.AreEqual(enemy.id,kill.Payload["victim"]);
                Assert.AreEqual("weapon",kill.Payload["method"]);
                var objective=dto.Events.Single(e=>e.Type=="objective_completed");
                Assert.AreEqual("N3_04",objective.Payload["objective"]);
                Assert.AreEqual(Array.FindIndex(Map02ChapterDefinition.Load().tasks,t=>t.id=="N3_04"),objective.Payload["index"],"Funnel index = place in the chapter.");
                Assert.AreEqual(1,dto.Events.Count(e=>e.Type=="player_death"),"One death, reported once.");
                Assert.AreEqual("failed",dto.MissionResult,"The story failed after Nam fell.");
                Assert.AreEqual("Died",SessionUploadMapper.Outcome(dto.MissionResult),"Uploaded as a death.");
            }
            finally{try{Directory.Delete(root,true);}catch(IOException){}}
        }
    }
}
