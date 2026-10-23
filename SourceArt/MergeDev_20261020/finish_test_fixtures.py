from pathlib import Path
root=Path(r'C:/Users/Admin/.codex/worktrees/merge-dev-thinh-20261020/ShadowVale')
p=root/'Assets/_Project/Map01/Tests/K54IntegrationTests.cs';s=p.read_text(encoding='utf-8-sig')
old='fire.Invoke(combat,null);';new='fire.Invoke(combat,null);typeof(PlayerCombat).GetMethod("ResolvePendingGunfire",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(combat,null);';assert s.count(old)==1;s=s.replace(old,new);p.write_text(s,encoding='utf-8',newline='\n')
p=root/'Assets/_Project/Map01/Tests/WeaponAnimationIntegrationTests.cs';s=p.read_text(encoding='utf-8-sig')
s=s.replace('readonly Mouse device;readonly Mouse[] physical;', 'bool pressed=true;readonly Mouse device;readonly Mouse[] physical;')
s=s.replace('void Pump(){device.MakeCurrent();InputSystem.QueueStateEvent(device,new MouseState().WithButton(MouseButton.Left));}', 'void Pump(){device.MakeCurrent();InputSystem.QueueStateEvent(device,pressed?new MouseState().WithButton(MouseButton.Left):new MouseState());}\n            public void Release(){pressed=false;Pump();InputSystem.Update();}\n            public void Press(){pressed=true;Pump();InputSystem.Update();}')
s=s.replace('thrower.Cancel();yield return WaitGameSeconds(.35f);Assert.IsTrue(Held("grenade_f1")', 'thrower.Cancel();heldKey.Release();yield return WaitGameSeconds(.35f);Assert.IsTrue(Held("grenade_f1")')
s=s.replace('thrower.BeginAim();yield return WaitGameSeconds(.30f);thrower.Release();yield return WaitGameSeconds(.05f);', 'heldKey.Press();thrower.BeginAim();yield return WaitGameSeconds(.30f);thrower.Release();heldKey.Release();yield return WaitGameSeconds(.05f);')
p.write_text(s,encoding='utf-8',newline='\n')
p=root/'Assets/_Project/Map01/Tests/Map01OperationsTests.cs';s=p.read_text(encoding='utf-8-sig')
a=s.index('            var aimRouting=RouteInputToGame();using var heldAim=');b=s.index('            int magazine=',a)
s=s[:a]+'''            // Toggle aim is input-tested in AimToggleIntegrationTests. This flow tests that
            // opening the puzzle cancels the already active mode, independent of editor mouse resets.
            typeof(PlayerCombat).GetMethod("SetAiming",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(M.ModernCombat,new object[]{true});
            Assert.IsTrue(M.ModernCombat.IsAiming);
'''+s[b:]
old='            InputSystem.QueueStateEvent(aimMouse,new MouseState());InputSystem.Update();heldAim.Dispose();RestoreInputRouting(aimRouting);\n';assert old in s;s=s.replace(old,'')
a=s.index('        sealed class HeldAimMouse:IDisposable');b=s.index('        internal static void LookAtOfficer',a);s=s[:a]+s[b:]
p.write_text(s,encoding='utf-8',newline='\n')
print('Corrected K54 deferred-shot test, real throw-release input and puzzle toggle-aim fixture.')
