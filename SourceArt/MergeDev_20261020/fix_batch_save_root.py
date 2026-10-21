from pathlib import Path
root=Path(r'C:/Users/Admin/.codex/worktrees/merge-dev-thinh-20261020/ShadowVale')
p=root/'Assets/_Project/Map01/Runtime/ForestSaveSlots.cs';s=p.read_text(encoding='utf-8-sig')
old='''                if (!string.IsNullOrEmpty(testRoot)) return testRoot;
#endif'''
new='''                if (!string.IsNullOrEmpty(testRoot)) return testRoot;
                // Failed tests can clear SessionState before Play Mode has fully stopped.
                // Automated editor runs must never fall through to the player's real slots.
                if (Application.isBatchMode) return Path.GetFullPath("Temp/Map01BatchFallbackSaves");
#endif'''
assert old in s;s=s.replace(old,new);p.write_text(s,encoding='utf-8',newline='\n')
p=root/'Assets/_Project/Map01/Tests/ForestSaveSlotsTests.cs';s=p.read_text(encoding='utf-8-sig')
at=s.index('        [TestCase(-1)]')
test='''        [Test]
        public void BatchModeCannotReachPlayerSlotsAfterFixtureCleanup()
        {
            if(!Application.isBatchMode)Assert.Ignore("This regression checks the batch editor fallback.");
            var field=typeof(ForestSaveSlots).GetField("storageRoot",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            var previous=field.GetValue(null);string key=ForestSaveSlots.EditorTestRootKey;
            string saved=UnityEditor.SessionState.GetString(key,"");
            try {
                field.SetValue(null,null);UnityEditor.SessionState.EraseString(key);
                string path=ForestSaveSlots.PathFor(0);
                StringAssert.StartsWith(Path.GetFullPath("Temp/Map01BatchFallbackSaves"),path);
                Assert.AreNotEqual(Path.Combine(Application.persistentDataPath,"shadowvale-slot-0.json"),path);
            }finally{field.SetValue(null,previous);if(string.IsNullOrEmpty(saved))UnityEditor.SessionState.EraseString(key);else UnityEditor.SessionState.SetString(key,saved);}
        }
'''
s=s[:at]+test+s[at:];p.write_text(s,encoding='utf-8',newline='\n')
print('Added batch-only fallback save isolation and regression.')
