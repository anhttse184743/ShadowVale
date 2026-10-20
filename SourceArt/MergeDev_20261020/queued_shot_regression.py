"""Add a regression for the newly combined deferred shot + stealth damage evidence."""
from pathlib import Path
root=Path(r'C:/Users/Admin/.codex/worktrees/merge-dev-thinh-20261020/ShadowVale')
p=root/'Assets/_Project/Map01/Tests/MosinStealthTests.cs';s=p.read_text(encoding='utf-8-sig')
at=s.index('        [UnityTest] public IEnumerator RealMosinShotsAt35_50_70Metres')
case='''        [UnityTest] public IEnumerator QueuedMosinKeepsItsIdentityWhenSwitchedBeforeTheFinalShotPass()
        {
            Open();yield return new EnterPlayMode();yield return WaitForMap01Ready();Quiet();Equip();Targets();PlaceNam(M,Lane);
            Stage(G,Lane+Vector3.forward*35,200);Stage(Ally,Lane+new Vector3(6,0,40),80);
            yield return WaitGameSeconds(.5f);Aim(G.Position+Vector3.up);
            VerifyQueuedSwitchIdentity();yield return null;yield return null;
            Assert.AreEqual(105,G.GetComponent<Health>().Current,.01f);NoInventedContact();
            yield return new ExitPlayMode();
        }
        static void VerifyQueuedSwitchIdentity()
        {
            var cc=M.player.GetComponent<CharacterController>();cc.enabled=true;cc.Move(Vector3.down*.04f);
            Assert.IsTrue(cc.isGrounded,"Keep the queued-shot probe independent of airborne spread.");
            var combat=M.ModernCombat;var health=G.GetComponent<Health>();DamageNotice observed=default;
            Action<DamageNotice> capture=d=>observed=d;health.Damaged+=capture;
            bool enabled=combat.enabled;combat.enabled=true;
            try {
                typeof(PlayerCombat).GetMethod("SetAiming",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(combat,new object[]{true});
                typeof(PlayerCombat).GetMethod("Attack",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(combat,null);
                Assert.AreEqual(200,health.Current,"The shot is still waiting for its final animation/camera pass.");
                Assert.IsTrue(combat.Equip(WeaponKind.Rifle));
                typeof(PlayerCombat).GetMethod("ResolvePendingGunfire",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(combat,null);
                Assert.AreEqual(WeaponKind.Sniper,observed.Weapon,"Damage must identify the weapon that fired, even though Nam now holds AK.");
                Assert.IsTrue(observed.IsBullet);Assert.AreEqual(95,observed.Amount,.01f);
            } finally {health.Damaged-=capture;combat.enabled=enabled;}
        }

'''
s=s[:at]+case+s[at:];p.write_text(s,encoding='utf-8',newline='\n')
print('Added queued-switch regression without changing production code.')
