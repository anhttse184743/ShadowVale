using System;
using System.Linq;
using UnityEngine;
namespace ShadowVale.Map01 {
    /// <summary>Hands and rifle share one socket until the sling takes its weight. No free-floating prop blend.</summary>
    public sealed class Map01InterrogationSling:IDisposable {
        readonly Animator actor;readonly Transform root,gun,parent,chest,hand;
        readonly Vector3 localPosition,localScale,startHand,endHand,centre,barrel;
        readonly Quaternion localRotation,wrist,endWrist,slungRotation;
        readonly Vector3 startElbow,endElbow;
        public float StowWristAngle{get;private set;}
        readonly Vector3 slungPosition;
        readonly Vector3 neutralForward;
        readonly (Transform bone,Quaternion rotation)[] fingers;
        readonly LineRenderer strap;readonly Material material;
        public float Weight{get;private set;}
        public Transform Gun=>gun;
        public float BehindChest=>Vector3.Dot(chest.InverseTransformPoint(gun.TransformPoint(centre)),neutralForward);
        public Map01InterrogationSling(Animator target,Transform character){
            actor=target;root=character;chest=actor.GetBoneTransform(HumanBodyBones.Chest);hand=actor.GetBoneTransform(HumanBodyBones.RightHand);
            var bodyRight=Vector3.ProjectOnPlane(actor.GetBoneTransform(HumanBodyBones.RightUpperArm).position-actor.GetBoneTransform(HumanBodyBones.LeftUpperArm).position,Vector3.up).normalized;
            var bodyForward=Vector3.Cross(bodyRight,Vector3.up).normalized;
            neutralForward=chest.InverseTransformDirection(bodyForward);
            gun=actor.GetComponent<Map01Rifle>().weapon.transform;actor.GetComponent<Map01EnemyGrip>()?.Apply();
            parent=gun.parent;localPosition=gun.localPosition;localRotation=gun.localRotation;localScale=gun.localScale;wrist=hand.localRotation;
            fingers=actor.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("RightHand")&&new[]{"Thumb","Index","Middle","Ring","Pinky"}.Any(f=>t.name.Contains(f))).Select(t=>(t,t.localRotation)).ToArray();
            startHand=chest.InverseTransformPoint(hand.position);
            Bounds extent=default;bool measured=false;
            foreach(var r in gun.GetComponentsInChildren<MeshRenderer>(true)){
                if(!r.enabled||!r.gameObject.activeInHierarchy)continue;
                var filter=r.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)continue;var b=filter.sharedMesh.bounds;
                for(int i=0;i<8;i++){
                    var p=gun.InverseTransformPoint(r.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
                    if(measured)extent.Encapsulate(p);else{extent=new Bounds(p,Vector3.zero);measured=true;}
                }
            }
            centre=extent.center;var size=extent.size;int along=size.x>=size.y&&size.x>=size.z?0:size.y>=size.z?1:2;
            var muzzle=gun.InverseTransformPoint(actor.GetComponent<Map01Rifle>().weapon.Muzzle.position);barrel=Vector3.zero;barrel[along]=muzzle[along]>=centre[along]?1:-1;
            var upper=actor.GetBoneTransform(HumanBodyBones.RightUpperArm);var lower=actor.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var upperRest=upper.localRotation;var lowerRest=lower.localRotation;startElbow=chest.InverseTransformPoint(lower.position);
            int across=size.x<=size.y&&size.x<=size.z?0:size.y<=size.z?1:2;var flat=Vector3.zero;flat[across]=1;
            var vertical=(-Vector3.up-bodyForward*.12f+bodyRight*.04f).normalized;
            var desiredGun=Quaternion.LookRotation(vertical,bodyRight)*Quaternion.Inverse(Quaternion.LookRotation(barrel,flat));
            var desiredHand=desiredGun*Quaternion.Inverse(localRotation);
            float best=float.PositiveInfinity;var bestPoint=hand.position;var bestPole=lower.position;var bestWrist=wrist;
            var anchor=chest.position+bodyRight*.36f-Vector3.up*.1f-bodyForward*.32f;
            foreach(float x in new[]{.32f,.4f,.48f})foreach(float y in new[]{-.25f,-.1f,.05f,.2f})foreach(float z in new[]{-.18f,-.32f,-.46f})
            foreach(float elbowY in new[]{-.35f,-.2f,0f})foreach(float elbowZ in new[]{-.2f,.05f,.25f}){
                upper.localRotation=upperRest;lower.localRotation=lowerRest;hand.localRotation=wrist;
                var point=chest.position+bodyRight*x+Vector3.up*y+bodyForward*z;
                var pole=chest.position+bodyRight*.5f+Vector3.up*elbowY+bodyForward*elbowZ;
                Map01ArmContact.Reach(actor,false,point,pole,1);
                if(Vector3.Distance(hand.position,point)>.015f)continue;
                var required=Quaternion.Inverse(lower.rotation)*desiredHand;
                float wristAngle=Quaternion.Angle(wrist,required);
                hand.rotation=desiredHand;gun.SetLocalPositionAndRotation(localPosition,localRotation);
                float headGap=Vector3.Distance(actor.GetBoneTransform(HumanBodyBones.Head).position,gun.position);
                if(headGap<.20f)continue;
                if(actor.GetComponent<Map01Rifle>().weapon.Muzzle.position.y<root.position.y+.22f)continue;
                float top=float.NegativeInfinity;
                for(int c=0;c<8;c++)top=Mathf.Max(top,gun.TransformPoint(extent.center+Vector3.Scale(extent.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1))).y);
                if(top>actor.GetBoneTransform(HumanBodyBones.Head).position.y+.15f)continue;
                var gunCentre=gun.TransformPoint(centre);
                if(Vector3.Dot(gunCentre-chest.position,bodyForward)>-.15f)continue;
                float distance=Vector3.Distance(anchor,gunCentre);
                float score=wristAngle+Mathf.Max(0,wristAngle-45)*50+distance*80;
                if(score<best){best=score;bestPoint=point;bestPole=pole;bestWrist=required;StowWristAngle=wristAngle;}
            }
            upper.localRotation=upperRest;lower.localRotation=lowerRest;hand.localRotation=wrist;
            Map01ArmContact.Reach(actor,false,bestPoint,bestPole,1);hand.localRotation=bestWrist;
            endHand=chest.InverseTransformPoint(hand.position);endElbow=chest.InverseTransformPoint(bestPole);endWrist=bestWrist;
            gun.SetLocalPositionAndRotation(localPosition,localRotation);
            slungPosition=chest.InverseTransformPoint(gun.position);slungRotation=Quaternion.Inverse(chest.rotation)*gun.rotation;
            var host=new GameObject("Interrogator canvas sling");host.transform.SetParent(root,false);
            strap=host.AddComponent<LineRenderer>();strap.useWorldSpace=true;strap.positionCount=4;strap.widthMultiplier=.026f;strap.numCapVertices=2;
            material=new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));material.color=new Color(.12f,.105f,.07f);strap.sharedMaterial=material;strap.enabled=false;
        }
        public void Sample(float progress,bool solveHands=true){
            float u=Mathf.Clamp01(progress);Weight=u;
            gun.SetParent(parent,false);gun.localScale=localScale;gun.SetLocalPositionAndRotation(localPosition,localRotation);
            if(u>0&&u<1&&solveHands){
                float along=Mathf.SmoothStep(0,1,u/.64f);
                var start=chest.TransformPoint(startHand);var end=chest.TransformPoint(endHand);
                // A raised arc clears the shoulder, keeping the elbow outside the ribcage.
                var goal=Vector3.Lerp(start,end,along)+Vector3.up*(.08f*Mathf.Sin(along*Mathf.PI));
                float reach=1-Mathf.SmoothStep(0,1,(u-.72f)/.28f);
                var grasp=Quaternion.Slerp(wrist,endWrist,along);
                hand.localRotation=Quaternion.Slerp(hand.localRotation,grasp,reach);
                Map01ArmContact.Reach(actor,false,goal,Vector3.Lerp(chest.TransformPoint(startElbow),chest.TransformPoint(endElbow),along),reach);
                hand.localRotation=Quaternion.Slerp(hand.localRotation,grasp,reach);
                foreach(var f in fingers)f.bone.localRotation=Quaternion.Slerp(f.bone.localRotation,f.rotation,reach);
            }
            if(u>=.7f){
                gun.SetParent(chest,false);gun.SetLocalPositionAndRotation(slungPosition,slungRotation);
                var scale=parent.lossyScale;var chestScale=chest.lossyScale;
                gun.localScale=Vector3.Scale(localScale,new Vector3(scale.x/chestScale.x,scale.y/chestScale.y,scale.z/chestScale.z));
            }else if(u>0&&solveHands){
                // The right hand owns the gun. The support hand releases while
                // it is still in front of the body; it never reaches behind the neck.
                var left=actor.GetBoneTransform(HumanBodyBones.LeftHand);var rifle=actor.GetComponent<Map01Rifle>();
                float support=1-Mathf.SmoothStep(0,1,u/.35f);
                Map01ArmContact.Reach(actor,true,rifle.support.position,chest.position-root.right*.4f-Vector3.up*.2f,support);
            }
            strap.enabled=u>=.7f;
            if(strap.enabled){
                var shoulder=actor.GetBoneTransform(HumanBodyBones.RightShoulder).position;
                strap.SetPosition(0,gun.TransformPoint(centre-barrel*.25f));strap.SetPosition(1,shoulder-root.forward*.12f);
                strap.SetPosition(2,shoulder+root.forward*.055f);strap.SetPosition(3,gun.TransformPoint(centre+barrel*.25f));
            }
        }
        public void Dispose(){
            if(gun!=null){gun.SetParent(parent,false);gun.SetLocalPositionAndRotation(localPosition,localRotation);gun.localScale=localScale;}
            if(strap!=null)UnityEngine.Object.Destroy(strap.gameObject);if(material!=null)UnityEngine.Object.Destroy(material);
        }
    }
}
