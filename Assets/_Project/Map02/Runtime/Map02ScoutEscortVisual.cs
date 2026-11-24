using System.Linq;
using UnityEngine;
using ShadowVale.Gameplay.Player;
using ShadowVale.Gameplay.Combat;

namespace ShadowVale.Map01
{
    /// <summary>Approved paired Blender motion, applied after the gun pose. The pistol retains its right arm.</summary>
    [DefaultExecutionOrder(3000)]
    public sealed class Map02ScoutEscortVisual:MonoBehaviour
    {
        Map02ChapterDirector owner;Animator nam,tam;
        Map02EscortPoseData namData,tamData;Transform[] namBones,tamBones;Vector3 lastPlayerPosition;
        Map01PistolAnimationData.Pose[] namOriginalPose,tamOriginalPose;
        Vector3 namBase,tamBase;float clock;bool bound;
        PlayerCombat combat;float gunWeight,lastShot=-100;
        Transform leftShoulder,rightUpper;
        bool lastWalking, useEscortFit;
        int namHipsIndex;
        Map01SkinContact namContact,tamContact;
        SkinnedMeshRenderer[] namSkins,tamSkins;
        Transform[] namFeet,tamFeet;
        ThirdPersonCamera cameraRig;Transform companionRoot,previousIgnoredCompanion;
        readonly RaycastHit[] groundHits=new RaycastHit[32];
        Mesh groundMesh;readonly System.Collections.Generic.List<Vector3> groundVertices=new();
        Transform[] rightArm;Vector3[] positions;Quaternion[] rotations;
        public void Bind(Map02ChapterDirector chapter)
        {
            if(bound)return;owner=chapter;
            nam=owner.Mission.Player.GetComponentInChildren<Animator>();tam=owner.Actor("Tâm").Animator;
            namData=Map02EscortPoseData.Load("Nam");tamData=Map02EscortPoseData.Load("Tam");
            if(nam==null||tam==null||namData==null||tamData==null){Debug.LogError("Tâm escort bone motion data are missing.",this);return;}
            namBones=namData.Bind(nam);tamBones=tamData.Bind(tam);lastPlayerPosition=owner.Mission.Player.position;
            namOriginalPose=namBones.Select(Map01PistolAnimationData.Pose.Read).ToArray();tamOriginalPose=tamBones.Select(Map01PistolAnimationData.Pose.Read).ToArray();
            namBase=nam.transform.localPosition;tamBase=tam.transform.localPosition;
            rightArm=nam.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("RightHand")||t.name=="RightShoulder"||t.name=="RightArm"||t.name=="RightForeArm").ToArray();
            positions=new Vector3[rightArm.Length];rotations=new Quaternion[rightArm.Length];clock=0;bound=true;
            combat=owner.Mission.Player.GetComponent<PlayerCombat>();combat.Attacked+=Shot;gunWeight=0;lastShot=-100;lastWalking=false;
            leftShoulder=nam.GetComponentsInChildren<Transform>().First(t=>t.name=="LeftArm");rightUpper=rightArm.First(t=>t.name=="RightArm");
            var tamRoot=owner.Actor("Tâm").visual;
            useEscortFit=nam.isHuman&&tam.isHuman;
            namHipsIndex=System.Array.IndexOf(namData.names,"Hips");
            if(!useEscortFit)
            {
                namContact=new Map01SkinContact(nam,owner.Mission.Player);tamContact=new Map01SkinContact(tam,tamRoot);
                namSkins=owner.Mission.Player.GetComponentsInChildren<SkinnedMeshRenderer>(true);tamSkins=tamRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                namFeet=Feet(nam);tamFeet=Feet(tam);groundMesh=new Mesh();
            }
            companionRoot=owner.Actor("Tâm").transform;cameraRig=owner.Mission.GameCamera?.GetComponent<ThirdPersonCamera>();
            if(cameraRig!=null){previousIgnoredCompanion=cameraRig.IgnoredCompanion;cameraRig.IgnoredCompanion=companionRoot;}
        }
        void LateUpdate()
        {
            if(!bound||!owner.ScoutEscortActive)return;
            var mission=owner.Mission;var player=mission.Player;
            if(mission.World.story.failed||player.GetComponent<Health>()?.IsDead==true){Release();return;}
            bool paused=mission.Paused||ForestMenu.Visible;
            float travelled=Vector3.ProjectOnPlane(player.position-lastPlayerPosition,Vector3.up).magnitude;lastPlayerPosition=player.position;
            var motor=player.GetComponent<PlayerController>();bool walking=paused?lastWalking:travelled>.0001f||motor.PlanarSpeed>.12f;lastWalking=walking;
            if(walking&&!paused)clock+=travelled/Map02ChapterDirector.ScoutAuthoredSpeed;
            for(int i=0;i<rightArm.Length;i++){positions[i]=rightArm[i].localPosition;rotations[i]=rightArm[i].localRotation;}
            namData.Apply(namBones,clock,walking);tamData.Apply(tamBones,clock,walking);
            // Preserve the live model's bind dimensions when applying the older recorded pose.
            if(useEscortFit)
                for(int i=0;i<namBones.Length;i++)
                {
                    if(i==namHipsIndex)continue;
                    namBones[i].localPosition=namOriginalPose[i].position;
                    namBones[i].localScale=namOriginalPose[i].scale;
                }
            gunWeight=Mathf.MoveTowards(gunWeight,combat.IsAiming||Time.time-lastShot<.6f?1:0,Time.deltaTime/.18f);
            if(Time.time-lastShot<.06f)gunWeight=1; // The shot resolves later, after this frame's raised hand.
            for(int i=0;i<rightArm.Length;i++)rightArm[i].SetLocalPositionAndRotation(Vector3.Lerp(rightArm[i].localPosition,positions[i],gunWeight),Quaternion.Slerp(rightArm[i].localRotation,rotations[i],gunWeight));
            var right=Vector3.ProjectOnPlane(rightUpper.position-leftShoulder.position,Vector3.up).normalized;
            if(!useEscortFit)right=Vector3.Dot(right,player.right)>=0 ? player.right : -player.right;
            companionRoot.SetPositionAndRotation(player.position-right*(useEscortFit ? .443f : .48f)+
                player.forward*(useEscortFit ? -.125f : .02f),player.rotation);
            nam.transform.localPosition=namBase;
            tam.transform.localPosition=tamBase;
            if(useEscortFit)FitEscort(player,right,walking);
            else
            {
                Ground(nam,namContact,namFeet,namSkins,player.position.y);
                Ground(tam,tamContact,tamFeet,tamSkins,player.position.y);
            }
            if(combat.IsReloading)rightUpper.rotation=Quaternion.AngleAxis(ReloadLowering(combat.ReloadProgress)*42,player.right)*rightUpper.rotation;
            else if(gunWeight>0)
            {
                var muzzle=combat.WeaponFor(WeaponKind.Pistol)?.Muzzle;var view=mission.GameCamera;
                if(muzzle!=null&&view!=null)rightUpper.rotation=Quaternion.Slerp(Quaternion.identity,Quaternion.FromToRotation(muzzle.forward,view.transform.forward),gunWeight)*rightUpper.rotation;
            }
        }
        void FitEscort(Transform player,Vector3 right,bool walking)
        {
            var up=Vector3.up;
            var forward=player.forward;
            float stanceOffset=walking ? .10f : .128f;
            WidenLeg(nam,true,-right,forward,stanceOffset);
            WidenLeg(nam,false,right,forward,stanceOffset);
            Bone(tam,HumanBodyBones.Hips).localScale*=.985f;
            GroundFeet(nam,player.position.y,.077f);
            GroundFeet(tam,player.position.y,.115f);

            var spine=Bone(tam,HumanBodyBones.Spine);
            var neck=Bone(tam,HumanBodyBones.Neck);
            spine.rotation=Quaternion.FromToRotation(neck.position-spine.position,up)*spine.rotation;
            var clavicle=Bone(tam,HumanBodyBones.RightShoulder);
            var shoulder=Bone(tam,HumanBodyBones.RightUpperArm);
            var otherShoulder=Bone(tam,HumanBodyBones.LeftUpperArm);
            if(clavicle!=null)
            {
                var axis=shoulder.position-clavicle.position;
                var lowered=axis;
                lowered.y=Mathf.Min(axis.y,otherShoulder.position.y+.018f-clavicle.position.y);
                clavicle.rotation=Quaternion.FromToRotation(axis,lowered)*clavicle.rotation;
            }

            var far=rightUpper.position;
            PlacePalm(tam,false,far-right*.071f+forward*.044f-up*.032f,
                forward-right*.11f-up*.76f,-up-forward*.25f,far-right*.17f-forward*.16f+up*.12f);

            var hips=Bone(tam,HumanBodyBones.Hips).position;
            var abdomen=Vector3.Lerp(hips,spine.position,.85f)+up*.105f-right*.055f+forward*.16f;
            PlacePalm(tam,true,abdomen,right+up*.23f,-forward,
                otherShoulder.position-right*.17f-up*.29f+forward*.08f);

            var waist=Vector3.Lerp(hips,spine.position,.82f)+up*.042f-forward*.16f;
            PlacePalm(nam,true,waist,-right-up*.12f,forward,
                leftShoulder.position-right*.008f-forward*.44f-up*.13f);
        }

        static Transform Bone(Animator actor,HumanBodyBones bone)=>actor.GetBoneTransform(bone);

        static void WidenLeg(Animator actor,bool left,Vector3 outward,Vector3 forward,float offset)
        {
            var thigh=Bone(actor,left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
            var knee=Bone(actor,left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
            var foot=Bone(actor,left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
            var delta=foot.position+outward*offset-thigh.position;
            var axis=delta.normalized;
            float upper=Vector3.Distance(thigh.position,knee.position);
            float lower=Vector3.Distance(knee.position,foot.position);
            float reach=Mathf.Clamp(delta.magnitude,Mathf.Abs(upper-lower)+.005f,(upper+lower)*.998f);
            float along=(upper*upper+reach*reach-lower*lower)/(2*reach);
            float bend=Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            var kneePlane=Vector3.ProjectOnPlane(forward+outward*.31f,axis).normalized;
            var kneeTarget=thigh.position+axis*along+kneePlane*bend;
            var ankle=foot.rotation;
            thigh.rotation=Quaternion.FromToRotation(knee.position-thigh.position,kneeTarget-thigh.position)*thigh.rotation;
            knee.rotation=Quaternion.FromToRotation(foot.position-knee.position,thigh.position+axis*reach-knee.position)*knee.rotation;
            foot.rotation=ankle;
        }

        static void PlacePalm(Animator actor,bool left,Vector3 palm,Vector3 fingers,Vector3 normal,Vector3 elbow)
        {
            var fingerDirection=fingers.normalized;
            var hand=Bone(actor,left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            var forearm=Bone(actor,left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
            var index=Bone(actor,left?HumanBodyBones.LeftIndexProximal:HumanBodyBones.RightIndexProximal);
            var middle=Bone(actor,left?HumanBodyBones.LeftMiddleProximal:HumanBodyBones.RightMiddleProximal);
            var little=Bone(actor,left?HumanBodyBones.LeftLittleProximal:HumanBodyBones.RightLittleProximal);
            for(int pass=0;pass<2;pass++)
            {
                Map01ArmContact.Reach(actor,left,palm-fingerDirection*.052f,elbow,1);
                fingerDirection=Vector3.RotateTowards((hand.position-forearm.position).normalized,fingers.normalized,58*Mathf.Deg2Rad,0);
            }
            if(index==null||middle==null||little==null)return;
            var palmNormal=Vector3.Cross(index.position-hand.position,little.position-hand.position)*(left?1:-1);
            var current=Quaternion.LookRotation(middle.position-hand.position,palmNormal);
            hand.rotation=Quaternion.LookRotation(fingerDirection,normal)*Quaternion.Inverse(current)*hand.rotation;
        }

        void GroundFeet(Animator actor,float rootFloor,float soleOffset)
        {
            var left=Bone(actor,HumanBodyBones.LeftFoot).position;
            var right=Bone(actor,HumanBodyBones.RightFoot).position;
            float raise=Mathf.Max(FloorAt(left,rootFloor)-left.y,FloorAt(right,rootFloor)-right.y);
            actor.transform.position+=Vector3.up*(soleOffset+raise);
        }

        static Transform[] Feet(Animator actor)=>actor.GetComponentsInChildren<Transform>().Where(t=>t.name=="LeftFoot"||t.name=="RightFoot").ToArray();
        void Ground(Animator actor,Map01SkinContact contact,Transform[] feet,SkinnedMeshRenderer[] skins,float rootFloor)
        {
            var foot=feet.OrderBy(t=>t.position.y).FirstOrDefault();var origin=foot!=null?foot.position:actor.transform.position;
            float floor=FloorAt(origin,rootFloor);
            float sole=contact.LowestY();
            if(!float.IsFinite(sole))
            {
                sole=float.PositiveInfinity;
                foreach(var skin in skins)
                {
                    if(!skin.enabled||!skin.gameObject.activeInHierarchy)continue;
                    skin.BakeMesh(groundMesh);groundMesh.GetVertices(groundVertices);
                    foreach(var vertex in groundVertices)sole=Mathf.Min(sole,skin.transform.TransformPoint(vertex).y);
                }
            }
            if(float.IsFinite(sole))actor.transform.position+=Vector3.up*(floor+.005f-sole);
        }

        float FloorAt(Vector3 origin,float rootFloor)
        {
            float floor=float.NegativeInfinity;
            int count=Physics.RaycastNonAlloc(new Vector3(origin.x,rootFloor+2,origin.z),Vector3.down,groundHits,6,~0,QueryTriggerInteraction.Ignore);
            var hits=count==groundHits.Length?Physics.RaycastAll(new Vector3(origin.x,rootFloor+2,origin.z),Vector3.down,6,~0,QueryTriggerInteraction.Ignore):groundHits;
            if(hits!=groundHits)count=hits.Length;
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];
                if(hit.normal.y<.55f||hit.point.y>rootFloor+.7f||hit.transform.IsChildOf(owner.Mission.Player)||hit.transform.IsChildOf(companionRoot)||hit.collider is CharacterController||hit.collider.GetComponentInParent<Health>()!=null||hit.collider.GetComponentInParent<Map02ChapterActor>()!=null)continue;
                floor=Mathf.Max(floor,hit.point.y);
            }
            return float.IsNegativeInfinity(floor)?rootFloor:floor;
        }
        public void Release()
        {
            if(!bound)return;
            if(combat!=null)combat.Attacked-=Shot;
            if(cameraRig!=null&&cameraRig.IgnoredCompanion==companionRoot)cameraRig.IgnoredCompanion=previousIgnoredCompanion;
            bound=false;
            RestorePose(nam,namBase,namBones,namOriginalPose);
            RestorePose(tam,tamBase,tamBones,tamOriginalPose);
            if(groundMesh!=null){if(Application.isPlaying)Destroy(groundMesh);else DestroyImmediate(groundMesh);groundMesh=null;}
        }
        static void RestorePose(Animator actor,Vector3 position,Transform[] bones,Map01PistolAnimationData.Pose[] originals)
        {
            if(actor==null)return;
            actor.transform.localPosition=position;
            for(int i=0;i<bones.Length;i++)if(bones[i]!=null)originals[i].Apply(bones[i]);
            if(actor.isActiveAndEnabled)actor.Update(0);
        }
        void Shot(WeaponKind kind,Vector3 point){if(kind==WeaponKind.Pistol)lastShot=Time.time;}
        public static float ReloadLowering(float progress)
        {
            float p=Mathf.Clamp01(progress);
            return p<.25f?Mathf.SmoothStep(0,1,p/.25f):p>.75f?Mathf.SmoothStep(1,0,(p-.75f)/.25f):1;
        }
        void OnDisable()=>Release();
    }
}
