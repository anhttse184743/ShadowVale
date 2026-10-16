using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ShadowVale.Map01.Tests
{
    public sealed class ForestSaveSlotsTests
    {
        [TestCase(-1)] [TestCase(5)]
        public void RejectsOutOfRangeSlot(int slot) => Assert.Throws<ArgumentOutOfRangeException>(() => ForestSaveSlots.PathFor(slot));

        [Test]
        public void MetadataAndCheckpointSurviveSerialization()
        {
            var source = new ForestSaveSlots.Entry { savedAt = DateTime.UtcNow.ToString("o"), location = "Bến sông", playSeconds = 9258,
                checkpoint = "{\"version\":1,\"hp\":73}", thumbnail = "AA==" };
            var restored = ForestSaveSlots.Parse(JsonUtility.ToJson(source));
            Assert.AreEqual(source.location, restored.location);
            Assert.AreEqual(source.checkpoint, restored.checkpoint);
            Assert.AreEqual(source.thumbnail, restored.thumbnail);
            Assert.AreEqual("02:34:18", ForestSaveSlots.Duration(restored.playSeconds));
        }
        [TestCase("{}")] [TestCase("{\"version\":99}")]
        public void RejectsIncompleteOrUnsupportedEnvelope(string json) => Assert.Throws<InvalidDataException>(() => ForestSaveSlots.Parse(json));
        [Test]
        public void ASaveFromTheOldMapLayoutIsRefused()
        {
            var rootField = typeof(ForestSaveSlots).GetField("storageRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            string root = Path.Combine(Application.temporaryCachePath, "old-layout-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            rootField.SetValue(null, root);
            try
            {
                // A v6 checkpoint: positions on the old Map 1's terrain.
                ForestSaveSlots.Write(2, new ForestSaveSlots.Entry { sceneName = "Map 1", savedAt = DateTime.UtcNow.ToString("o"), location = "Bến sông",
                    checkpoint = "{\"version\":6,\"stage\":3,\"items\":[],\"used\":[]}" });
                var refused = Assert.Throws<IOException>(() => Map01SaveSystem.BeginGame(2));
                Assert.AreEqual(Map01SaveSystem.OldLayoutMessage, refused.Message);
            }
            finally
            {
                rootField.SetValue(null, null);
                Directory.Delete(root, true);
            }
        }
        [Test]
        public void RejectsNegativePlaytime()
        {
            var source = new ForestSaveSlots.Entry { savedAt = DateTime.UtcNow.ToString("o"), checkpoint = "{}", playSeconds = -1 };
            Assert.Throws<InvalidDataException>(() => ForestSaveSlots.Parse(JsonUtility.ToJson(source)));
        }
    }
}
