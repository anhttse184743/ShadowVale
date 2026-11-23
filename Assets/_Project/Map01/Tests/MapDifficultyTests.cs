using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadowVale.Map01.Tests
{
    public sealed class MapDifficultyTests
    {
        private static MapDifficulty Config(int damage, int vision, string preset) =>
            MapDifficulty.Read(new JObject
            {
                ["difficulty"] = new JObject
                {
                    ["active_level"] = "hard",
                    ["levels"] = new JObject
                    {
                        ["normal"] = new JObject
                        {
                            ["guard_damage_percent"] = 100,
                            ["guard_vision_percent"] = 100,
                            ["layout_preset_id"] = MapDifficulty.OriginalPreset
                        },
                        ["hard"] = new JObject
                        {
                            ["guard_damage_percent"] = damage,
                            ["guard_vision_percent"] = vision,
                            ["layout_preset_id"] = preset
                        }
                    }
                }
            });

        [TestCase(120, 90, 24f, 18f)]
        [TestCase(80, 110, 16f, 22f)]
        [TestCase(151, 80, 20f, 20f)]
        public void ActiveLevelControlsDamageAndVision(int damage, int vision, float expectedDamage, float expectedVision)
        {
            var difficulty = Config(damage, vision, "original");
            Assert.AreEqual(expectedDamage, difficulty.Damage(20), .001f);
            Assert.AreEqual(expectedVision, difficulty.Vision(20), .001f);
            Assert.AreSame(MapDifficulty.Default, MapDifficulty.Read(null));
        }

        [Test]
        public void JsonNumbersAcceptIntegralDecimalNotation()
        {
            var rules = JObject.Parse(@"{
                ""difficulty"": {""active_level"": ""normal"", ""levels"": {
                    ""normal"": {""guard_damage_percent"": 120.0, ""guard_vision_percent"": 90.0, ""layout_preset_id"": ""original""}
                }}
            }");
            Assert.AreEqual(120, MapDifficulty.Read(rules).DamagePercent);
            rules["difficulty"]["levels"]["normal"]["guard_damage_percent"] = 120.5;
            Assert.AreSame(MapDifficulty.Default, MapDifficulty.Read(rules));
        }

        [TestCase("scout_patrol", true)]
        [TestCase("N3_05A_b", true)]
        [TestCase("night2_patrol", false)]
        [TestCase("night2_evacuation_patrol", false)]
        [TestCase("N3_08", false)]
        public void MapTwoOnlyScalesSelectedCombatGroups(string group, bool expected)
        {
            Assert.AreEqual(expected, Map02ChapterEnemy.UsesMapDifficulty(group));
        }

        [Timeout(600000)]
        [TestCase("Map 1", "map01", "forest_south", "forest_north")]
        [TestCase("Map 2", "map02", "canal_south", "canal_north")]
        public void PresetsOnlyChangeMarkedDecorationRenderers(string sceneName, string map, string first, string second)
        {
            var scene = EditorSceneManager.OpenScene($"Assets/_Project/Scenes/Maps/{sceneName}.unity", OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var colliders = roots.SelectMany(root => root.GetComponentsInChildren<Collider>(true)).ToArray();
                var colliderEnabled = colliders.Select(collider => collider.enabled).ToArray();
                int Off() => roots.Sum(root => root.GetComponentsInChildren<MeshRenderer>(true).Count(renderer => renderer.forceRenderingOff));
                var baseline = Off();
                Assert.AreEqual(0, Config(100, 100, "unknown").ApplyLayout(scene, map));
                Assert.AreEqual(baseline, Off());
                Assert.AreEqual(18, Config(100, 100, first).ApplyLayout(scene, map));
                var firstCount = Off();
                Assert.Greater(firstCount, baseline);
                Assert.AreEqual(18, Config(100, 100, second).ApplyLayout(scene, map));
                Assert.Greater(Off(), firstCount);
                CollectionAssert.AreEqual(colliderEnabled, colliders.Select(collider => collider.enabled).ToArray());
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
