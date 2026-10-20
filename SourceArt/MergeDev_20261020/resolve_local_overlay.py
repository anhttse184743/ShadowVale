from pathlib import Path
import re,json
main=Path(r'D:/DO_AN_GAME/ShadowVale')
root=Path(r'C:/Users/Admin/.codex/worktrees/merge-dev-thinh-20261020/ShadowVale')
backup=main/'Temp/MergeDev_20261020_Before/Files'
rows=json.loads((main/'Docs/Reviews/MergeDev_20261020/local_overlay.json').read_text())
rx=re.compile(r'^<<<<<<< .*?\n(.*?)^\|\|\|\|\|\|\| .*?\n(.*?)^=======\n(.*?)^>>>>>>> .*?\n',re.M|re.S)
for row in rows:
 if row['result']!='conflict':continue
 rel=row['path'];p=root/rel;s=p.read_text(encoding='utf-8-sig');name=p.name
 if name=='DialogueVoiceTests.cs':
  s=(backup/rel).read_text(encoding='utf-8-sig')
  s=s.replace('"m1_ops_commander_report_02","m1_ops_retreat_02"','"m1_ops_commander_report_02","m1_ops_retreat_02",\n            "m1_c3_gate_checkin_call","m1_c3_gate_checkin_reply","m1_c3_gate_no_answer",\n            "m1_c3_gate_alarm","m1_c3_mosin_order"')
  s=s.replace('        [Test] public void LegacyBriefingUsesAlignedVoiceAssets()', '''        [Test] public void NewRescueRecordingsMatchTheUpdatedCaptions() {
            StringAssert.Contains("cầu gỗ",DialogueVoice.Caption("m1_story_09"));
            StringAssert.Contains("Tay phải còn đau",DialogueVoice.Caption("m1_rescue_freed"));
            StringAssert.DoesNotContain("đã bàn giao",DialogueVoice.Caption("m1_rescue_delivered"));
        }
        [Test] public void LegacyBriefingUsesAlignedVoiceAssets()''')
 else:
  def pick(m):
   local,base,dev=m.groups()
   if name=='ForestInventory.cs':
    return dev.replace('"Bắn K-54: chưa có"','"R nạp đạn"').replace('"Bắn Mosin: chưa có"','"R nạp từng viên"')
   if name=='Map01CinematicPose.cs':return dev
   if name=='Map01Hud.Combat.cs':return dev.replace('primary, false)', 'primary, primary=="rifle_mosin"&&kind==WeaponKind.Sniper&&!inventory.ThrowableEquipped)')
   if name=='Map01Mission.cs':return dev.replace('!stones.Aiming','!stones.PresentingThrow')
   if name=='Map01NamActions.cs':return dev
   if name=='Map01StoryDialogue.cs':return dev+'            if (mission != null) mission.Say(null, 0);\n'
   if name=='PlayerCombat.cs':return dev.replace('        internal bool RifleLevelled => Gun != null &&', '        public bool GunPresentationReady { get; set; } = true;\n        internal bool RifleLevelled => Gun != null && GunPresentationReady &&')
   if name=='Weapon.cs':return local+dev
   raise AssertionError(name)
  s=rx.sub(pick,s)
 assert not re.search(r'^(<<<<<<<|\|\|\|\|\|\|\||=======|>>>>>>>)',s,re.M),rel
 p.write_text(s,encoding='utf-8',newline='\n')
 print(rel)
