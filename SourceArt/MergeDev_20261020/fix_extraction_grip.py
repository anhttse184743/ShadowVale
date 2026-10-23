"""Keep the existing weapon and skeleton, fitting its grip inside both arm reaches."""
from pathlib import Path
root=Path(r'C:/Users/Admin/.codex/worktrees/dev-preflight-20261020/ShadowVale')
p=root/'Assets/_Project/Map01/Runtime/Map01Extraction.cs'
s=p.read_text(encoding='utf-8-sig')
old='''            Vector3 target=gun.support.position-wrist*Vector3.Scale(localPalm,hand.lossyScale);
            SolveCoverArm(actor,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,target,wrist,-1,weight);'''
new='''            var upper=actor.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            float reach=(Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,hand.position))*.96f;
            Vector3 palmOffset=wrist*Vector3.Scale(localPalm,hand.lossyScale);
            // The new commander has shorter arms than Hung. Move the rifle through its holding hand,
            // rather than stretching the support arm or leaving its palm floating off the foregrip.
            for(int i=0;i<4 && weight>0;i++) {
                Vector3 desired=gun.support.position-palmOffset;
                Vector3 delta=desired-upper.position;
                if(delta.magnitude<=reach)break;
                Vector3 correction=upper.position+delta.normalized*reach-desired;
                var holdingHand=actor.GetBoneTransform(HumanBodyBones.RightHand);
                SolveCoverArm(actor,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,
                    holdingHand.position+correction,holdingHand.rotation,1,weight);
            }
            Vector3 target=gun.support.position-palmOffset;
            SolveCoverArm(actor,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,target,wrist,-1,weight);'''
assert s.count(old)==1
p.write_text(s.replace(old,new),encoding='utf-8',newline='\n')
q=root/'Assets/_Project/Map01/Tests/MergeDevGripDiagnosticsTests.cs';s=q.read_text(encoding='utf-8');s=s.replace('grip-diagnostics.txt','grip-diagnostics-fixed.txt').replace('grip-before.png','grip-after.png');s=s.replace('    rows.Add("afterExplicitSolve="+Vector3.Distance(Vector3.Lerp(h.position,tip.position,.45f),g.support.position));','    rows.Add("afterExplicitSolve="+Vector3.Distance(Vector3.Lerp(h.position,tip.position,.45f),g.support.position));\n    Assert.Less(Vector3.Distance(Vector3.Lerp(h.position,tip.position,.45f),g.support.position),.025f,actor.name);')
q.write_text(s,encoding='utf-8',newline='\n')
print('Applied measured-reach correction in isolated dev worktree only.')
