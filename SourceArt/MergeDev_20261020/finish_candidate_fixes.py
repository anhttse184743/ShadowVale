"""Apply inspected integration fixes after the candidate's first test process exits."""
from pathlib import Path
import shutil
root=Path(r'C:/Users/Admin/.codex/worktrees/merge-dev-thinh-20261020/ShadowVale')
dev=Path(r'C:/Users/Admin/.codex/worktrees/dev-preflight-20261020/ShadowVale')
assert not (root/'Temp/UnityLockfile').exists(), 'Wait until the current Unity process closes.'
for rel in ['Assets/_Project/Map01/Runtime/Map01Extraction.cs','Assets/_Project/Map01/Tests/ExtractionTests.cs']:
 shutil.copy2(dev/rel,root/rel)

p=root/'Assets/_Project/Map01/Runtime/Map01Mission.cs';s=p.read_text(encoding='utf-8-sig')
old='''        public void ReleaseForNextMap() {
            GetComponent<DialogueVoice>()?.Stop();'''
new='''        public void ReleaseForNextMap() {
            // The carried actor outlives Map 1; its scene-specific arm/scope drivers must not.
            if(player.TryGetComponent(out Map01Pistol pistolMotion))pistolMotion.enabled=false;
            if(player.TryGetComponent(out Map01Mosin mosinMotion))mosinMotion.enabled=false;
            ModernCombat.GunPresentationReady=true;
            GetComponent<DialogueVoice>()?.Stop();'''
assert old in s;s=s.replace(old,new);p.write_text(s,encoding='utf-8',newline='\n')

p=root/'Assets/_Project/Map01/Runtime/Map01Mosin.cs';s=p.read_text(encoding='utf-8-sig')
s=s.replace('bool Available=>combat!=null', 'bool Available=>mission!=null&&combat!=null')
s=s.replace('if(data==null||cameraRig==null)return;', 'if(data==null||cameraRig==null)return;\n            if(mission==null){scoped=false;cameraRig.SetWeaponScope(false,3.5f,0);return;}')
p.write_text(s,encoding='utf-8',newline='\n')

p=root/'Assets/_Project/Map01/Runtime/Map01Pistol.cs';s=p.read_text(encoding='utf-8-sig');s=s.replace('bool Available => combat!=null', 'bool Available => mission!=null&&combat!=null');p.write_text(s,encoding='utf-8',newline='\n')

p=root/'Assets/_Project/Map01/Tests/WeaponAnimationIntegrationTests.cs';s=p.read_text(encoding='utf-8-sig')
s=s.replace('readonly Keyboard keyboard;', 'readonly Mouse keyboard;').replace('ThrowKeyHold(Keyboard keyboard,', 'ThrowKeyHold(Mouse keyboard,')
s=s.replace('InputSystem.AddDevice<Keyboard>()', 'InputSystem.AddDevice<Mouse>()').replace('new KeyboardState(Key.Q)', 'new MouseState().WithButton(MouseButton.Left)')
s=s.replace('t.name=="Throwable in hand — "+id', '(t.name=="Throwable in hand — "+id||t.name==id+" in hand")')
s=s.replace('The virtual Q key', 'The virtual left mouse button')
s=s.replace('Assert.IsFalse(Held("grenade_f1").activeSelf);Assert.AreEqual(count,bag.Count("grenade_f1"));', 'Assert.IsTrue(Held("grenade_f1").activeSelf,"Cancelling aim keeps the selected grenade in hand.");Assert.AreEqual(count,bag.Count("grenade_f1"));')
s=s.replace('Assert.IsTrue(M.player.GetComponentsInChildren<Weapon>(true).Where(w=>w.gameObject.activeInHierarchy).SelectMany(w=>w.GetComponentsInChildren<Renderer>()).All(r=>!r.forceRenderingOff),"Cancel restores the equipped weapon\'s rendering.");','Assert.IsTrue(bag.ThrowableEquipped);Assert.IsFalse(M.ModernCombat.InputAllowed(),"The selected throwable still owns the hands.");')
s=s.replace('yield return WaitGameSeconds(1f);Assert.IsFalse(thrower.PresentingThrow);Assert.IsTrue(M.ModernCombat.InputAllowed());', 'yield return WaitGameSeconds(1f);Assert.IsFalse(thrower.PresentingThrow);Assert.IsFalse(M.ModernCombat.InputAllowed());Assert.IsTrue(Held("grenade_f1").activeSelf,"One grenade remains selected after recovery.");\n            Assert.IsTrue(bag.Draw(Map01Inventory.Slot.Primary));yield return null;Assert.IsTrue(M.ModernCombat.InputAllowed());\n            Assert.IsTrue(M.player.GetComponentsInChildren<Weapon>(true).Where(w=>w.gameObject.activeInHierarchy).SelectMany(w=>w.GetComponentsInChildren<Renderer>()).All(r=>!r.forceRenderingOff),"Drawing the primary restores its rendering.");')
s=s.replace('bag.Add("smoke_bomb",1);bag.SelectThrowable("smoke_bomb");', 'bag.Add("smoke_bomb",1);')
s=s.replace('Assert.IsTrue(M.ModernCombat.TryReload());yield return WaitGameSeconds(.20f);', 'Assert.IsTrue(M.ModernCombat.TryReload());yield return WaitGameSeconds(.20f);bag.SelectThrowable("smoke_bomb");')
s=s.replace('Assert.IsFalse(Held("smoke_bomb").activeSelf);Assert.AreEqual(1,bag.Count("smoke_bomb"));', 'Assert.IsTrue(Held("smoke_bomb").activeSelf);Assert.AreEqual(1,bag.Count("smoke_bomb"));\n            Assert.IsTrue(bag.Draw(Map01Inventory.Slot.Primary));yield return null;Assert.IsFalse(Held("smoke_bomb").activeSelf);')
p.write_text(s,encoding='utf-8',newline='\n')

p=root/'Assets/_Project/Map01/Tests/Map02ArrivalTests.cs';s=p.read_text(encoding='utf-8-sig')
marker='            // These Map 1 drivers'
# Add independent lifecycle assertions to the existing transfer checks after arrival.
needle='Assert.IsTrue(arrival.HasLanded);'
if needle in s:
 s=s.replace(needle,needle+'AssertMap1WeaponDriversStopped(arrival);')
else:
 needle='Assert.AreEqual(Map02Arrival.Phase.Gameplay,arrival.CurrentPhase);'
 assert needle in s
 s=s.replace(needle,needle+'AssertMap1WeaponDriversStopped(arrival);')
idx=s.index('[TearDown]')
helper='''static void AssertMap1WeaponDriversStopped(Map02Arrival arrival) {
var carried=arrival.Passengers[0];
var mosin=carried.GetComponent<Map01Mosin>();var pistol=carried.GetComponent<Map01Pistol>();
Assert.NotNull(mosin);Assert.NotNull(pistol);
Assert.IsFalse(mosin.enabled,"Mosin cannot keep querying the unloaded Map 1 mission.");
Assert.IsFalse(pistol.enabled,"Map 1 pistol pose must release the arrival cinematic.");
Assert.IsFalse(mosin.Scoped);Assert.IsTrue(carried.GetComponent<PlayerCombat>().GunPresentationReady);
}
'''
s=s[:idx]+helper+s[idx:];p.write_text(s,encoding='utf-8',newline='\n')
print('Copied verified extraction fix; repaired Map 2 driver lifetime; updated throwable tests to the dev input contract.')
