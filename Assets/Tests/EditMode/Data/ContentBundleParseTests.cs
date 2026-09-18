using System.IO;
using System.Linq;
using NUnit.Framework;
using ShadowVale.Content;
using ShadowVale.Data.Content;

namespace ShadowVale.Tests.EditMode.Data
{
    /// <summary>The shipped fallback bundle must parse and pass validation; corrupt bundles must not.</summary>
    public class ContentBundleParseTests
    {
        [Test]
        public void FallbackBundle_LoadsAndValidates()
        {
            var provider = new FallbackBundleProvider();
            Assert.IsTrue(provider.TryLoad(out var bundle, out var error), error);
            Assert.AreEqual("1.0.0", bundle.BundleVersion);
            Assert.AreEqual(1, bundle.SchemaVersion);
            Assert.GreaterOrEqual(bundle.Weapons.Count, 5, "proposal promises 5–6 weapon classes");
            Assert.GreaterOrEqual(bundle.EnemyArchetypes.Count, 3);
            Assert.GreaterOrEqual(bundle.Maps.Count, 1);
            Assert.AreEqual("greedy", bundle.AiSettings.DefaultSolverVariant);
            Assert.AreEqual(120, bundle.AiSettings.LatencyBudgetMs);
        }

        [Test]
        public void FallbackBundle_SnakeCaseKeysMapOntoPascalCase()
        {
            var json = File.ReadAllText(FallbackBundleProvider.AbsolutePath);
            StringAssert.Contains("\"bundle_version\"", json);
            StringAssert.Contains("\"enemy_archetypes\"", json);
            StringAssert.Contains("\"durability_per_shot\"", json);

            var b = ContentJson.Deserialize<ContentBundle>(json);
            var rifle = b.Weapons.First(w => w.Id == "rifle_standard");
            Assert.Greater(rifle.DurabilityPerShot, 0);
            Assert.AreEqual("ammo_rifle", rifle.AmmoType);
        }

        [Test]
        public void FallbackBundle_IdsAreUniqueAndReferencesResolve()
        {
            new FallbackBundleProvider().TryLoad(out var b, out _);
            var svc = new ContentService();
            svc.Apply(b, "test");
            foreach (var e in b.EnemyArchetypes) Assert.NotNull(svc.GetWeapon(e.WeaponId), e.Id);
            foreach (var w in b.Weapons) Assert.NotNull(svc.GetItem(w.AmmoType), w.Id);
            Assert.AreEqual(b.Weapons.Count, b.Weapons.Select(w => w.Id).Distinct().Count());
        }

        [Test]
        public void Validator_RejectsDuplicateIdsAndDanglingReferences()
        {
            new FallbackBundleProvider().TryLoad(out var b, out _);
            b.Weapons.Add(new WeaponDefinition { Id = b.Weapons[0].Id, AmmoType = "nope", Damage = 1, MagazineSize = 1, DurabilityMax = 1 });
            var errors = SchemaValidator.Validate(b);
            Assert.IsTrue(errors.Any(e => e.Contains("duplicate id")), string.Join("\n", errors));
            Assert.IsTrue(errors.Any(e => e.Contains("ammo_type 'nope'")), string.Join("\n", errors));
        }

        [Test]
        public void Validator_RejectsWrongSchemaVersion()
        {
            var ok = FallbackBundleProvider.TryParse("{\"bundle_version\":\"9.9.9\",\"schema_version\":2}", out var bundle, out var error);
            Assert.IsFalse(ok);
            Assert.IsNull(bundle);
            StringAssert.Contains("schema_version 2 unsupported", error);
        }

        [Test]
        public void Validator_RejectsMalformedJson()
        {
            Assert.IsFalse(FallbackBundleProvider.TryParse("{ not json", out _, out var error));
            StringAssert.Contains("malformed", error);
        }

        [Test]
        public void ContentService_ThrowsWithIdOnMissingKey()
        {
            new FallbackBundleProvider().TryLoad(out var b, out _);
            var svc = new ContentService();
            svc.Apply(b, "test");
            var ex = Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => svc.GetWeapon("plasma_cannon"));
            StringAssert.Contains("plasma_cannon", ex.Message);
        }
    }
}
