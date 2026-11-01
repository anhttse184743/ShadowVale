using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using ShadowVale.Content;
using ShadowVale.Networking;
using ShadowVale.Networking.Dto;

namespace ShadowVale.Tests.EditMode.Data
{
    /// <summary>
    /// The web backend's schema 1.0 bundle maps onto the runtime model, survives the disk cache and is
    /// only accepted when its bytes match the manifest checksum (docs/game-integration.md of ShadowVale_web).
    /// </summary>
    public class BackendContentTests
    {
        private string _directory;

        [SetUp]
        public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), "svl-content-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        private static string FallbackJson => File.ReadAllText(FallbackBundleProvider.AbsolutePath);

        [Test]
        public void FlatRowsAreJoinedByCode()
        {
            Assert.IsTrue(FallbackBundleProvider.TryParse(FallbackJson, out var b, out var error), error);

            var patrol = b.LootTables.Single(t => t.Id == "lt_m1_patrol");
            Assert.AreEqual(1, patrol.RollsMin);
            Assert.IsTrue(patrol.Entries.Any(e => e.ItemId == "ammo_rifle" && e.Tier == 5), "Guaranteed entries keep their tier.");
            var cache = b.LootTables.Single(t => t.Id == "lt_m1_mosin_cache");
            Assert.AreEqual(0, cache.Rolls, "A one-off cache only gives its tier 5 entries.");

            var medkit = b.CraftRecipes.Single(r => r.Id == "craft_medkit_small");
            CollectionAssert.AreEquivalent(new[] { "cloth", "herb" }, medkit.Inputs.Select(i => i.ItemId));
            Assert.AreEqual("engineering", medkit.RequiredSkill);

            var map1 = b.Maps.Single(m => m.Id == "map01");
            Assert.AreEqual("Map 1", map1.SceneName, "CoordinationSettings.ForScene finds the map by scene.");
            Assert.Greater(map1.NavGraphNodes.Count, 20);
            Assert.IsTrue(map1.NavGraphNodes.Any(n => n.IsCover), "nav_graph.nodes keeps the snake_case cover fields.");
            Assert.Greater(map1.EscapeRoutes.Count, 0);
            Assert.IsTrue(map1.LootPlacements.Any(p => p.ContainerId == "camp_chest_c1" && p.LootTableId == "lt_m1_chest_c1"));

            var knife = b.Weapons.Single(w => w.Id == "knife");
            Assert.AreEqual("melee", knife.Class);
            Assert.IsNull(knife.AmmoType);

            var quest = b.Quests.Single(q => q.Id == "map01_main");
            Assert.AreEqual("rescue_hung", quest.Objectives[0].Id, "Objective index = objective_completed payload.index.");
        }

        [Test]
        public void EnemyTypesReadTheirFsmParams()
        {
            Assert.IsTrue(FallbackBundleProvider.TryParse(FallbackJson, out var b, out var error), error);
            var grunt = b.EnemyArchetypes.Single(e => e.Id == "grunt");
            Assert.AreEqual(8f, grunt.AlertDecaySeconds, 1e-4);
            Assert.AreEqual(.25f, grunt.RetreatHpThreshold, 1e-4);
            Assert.AreEqual(90f, grunt.VisionAngle, 1e-4);
            Assert.Greater(grunt.Accuracy, 0);
            Assert.IsNull(grunt.Boss);
            Assert.IsNotNull(b.EnemyArchetypes.Single(e => e.Id == "m1_commander").Boss, "fsm_params.boss → BossProfile.");
        }

        [Test]
        public void TheCacheReturnsWhatWasSavedAndRefusesChangedBytes()
        {
            var cache = new BundleCache(_directory);
            Assert.IsFalse(cache.TryLoad(out _, out var empty));
            StringAssert.Contains("no cached bundle", empty);

            var bytes = Encoding.UTF8.GetBytes(FallbackJson);
            var meta = new BundleCache.Meta { VersionId = "6f1c1f6e-0000-4000-8000-000000000001", VersionNo = 3, Checksum = BundleCache.Sha256(bytes), PublishedAt = "2026-10-29T08:00:00Z" };
            cache.Save(meta, bytes);
            Assert.IsTrue(cache.TryLoad(out var bundle, out var error), error);
            Assert.AreEqual(meta.VersionId, bundle.ContentVersionId);
            Assert.AreEqual(meta.Checksum, bundle.Checksum);
            Assert.AreEqual("cache", cache.SourceName);

            File.WriteAllText(Path.Combine(_directory, meta.VersionId + ".json"), FallbackJson.Replace("\"damage\": 20,", "\"damage\": 99,"));
            Assert.IsFalse(cache.TryLoad(out _, out var corrupt));
            StringAssert.Contains("checksum mismatch", corrupt);
        }

        [Test]
        public void ADownloadIsCachedOnlyWhenItIsAValidBundle()
        {
            var cache = new BundleCache(_directory);
            var bytes = Encoding.UTF8.GetBytes(FallbackJson);
            var manifest = new ContentManifest { VersionId = "6f1c1f6e-0000-4000-8000-000000000002", VersionNo = 4, Checksum = BundleCache.Sha256(bytes) };

            var broken = new BundleFetcher.Result { Status = BundleFetcher.Status.Downloaded, Manifest = manifest, Bytes = Encoding.UTF8.GetBytes("{\"schema_version\":\"1.0\"") };
            Assert.IsFalse(BundleFetcher.Accept(broken, cache, out _, out _));
            Assert.IsNull(cache.Current, "A rejected download never replaces the cache.");

            var good = new BundleFetcher.Result { Status = BundleFetcher.Status.Downloaded, Manifest = manifest, Bytes = bytes };
            Assert.IsTrue(BundleFetcher.Accept(good, cache, out var bundle, out var error), error);
            Assert.AreEqual(manifest.VersionId, bundle.ContentVersionId);
            Assert.AreEqual(manifest.Checksum, cache.Current.Checksum);
        }

        [Test]
        public void WithoutAKeyOrABackendTheFetcherGivesUpWithoutThrowing()
        {
            var noKey = new BundleFetcher(new BackendClient("http://127.0.0.1:9", null), null).FetchAsync();
            Assert.IsTrue(noKey.Wait(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(BundleFetcher.Status.Unavailable, noKey.Result.Status);
            StringAssert.Contains("no game key", noKey.Result.Error);

            // Port 9 (discard) refuses at once on a normal machine
            var down = Task.Run(() => new BundleFetcher(new BackendClient("http://127.0.0.1:9", new string('k', 32)), null).FetchAsync());
            Assert.IsTrue(down.Wait(TimeSpan.FromSeconds(40)));
            Assert.AreEqual(BundleFetcher.Status.Unavailable, down.Result.Status);
        }

        [Test]
        public void TheManifestIsReadFromTheBackendsCamelCase()
        {
            var m = ContentManifest.Parse("{\"versionId\":\"a1\",\"versionNo\":7,\"label\":\"x\",\"schemaVersion\":\"1.0\",\"checksum\":\"ABC\",\"publishedAt\":\"2026-10-29T08:00:00+00:00\"}");
            Assert.AreEqual("a1", m.VersionId);
            Assert.AreEqual(7, m.VersionNo);
            Assert.AreEqual("abc", m.Checksum);
            Assert.AreEqual("2026-10-29T08:00:00+00:00", m.PublishedAt);
            Assert.IsNull(ContentManifest.Parse("{\"message\":\"No content version is published yet.\"}"));
        }

        [TestCase(200, BackendOutcome.Ok)]
        [TestCase(409, BackendOutcome.Ok)]
        [TestCase(304, BackendOutcome.NotModified)]
        [TestCase(400, BackendOutcome.Rejected)]
        [TestCase(413, BackendOutcome.Rejected)]
        [TestCase(401, BackendOutcome.RetryLater)]
        [TestCase(404, BackendOutcome.RetryLater)]
        [TestCase(429, BackendOutcome.RetryLater)]
        [TestCase(503, BackendOutcome.RetryLater)]
        public void StatusCodesFollowTheBackendsRetryRule(int status, BackendOutcome expected) =>
            Assert.AreEqual(expected, BackendClient.Classify(status));
    }
}
