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
        bool lastWalking;
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
            namContact=new Map01SkinContact(nam,owner.Mission.Player);tamContact=new Map01SkinContact(tam,tamRoot);
            namSkins=owner.Mission.Player.GetComponentsInChildren<SkinnedMeshRenderer>(true);tamSkins=tamRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            namFeet=Feet(nam);tamFeet=Feet(tam);groundMesh=new Mesh();
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
            // Find the anatomical left from Nam's actual skeleton; model import yaw does not choose the side.
            float side=Vector3.Dot(leftShoulder.position-rightUpper.position,player.right)>=0?1:-1;
            var actor=owner.Actor("Tâm");actor.transform.SetPositionAndRotation(player.position+player.right*(side*.48f)+player.forward*.02f,player.rotation);
            for(int i=0;i<rightArm.Length;i++){positions[i]=rightArm[i].localPosition;rotations[i]=rightArm[i].localRotation;}
            namData.Apply(namBones,clock,walking);tamData.Apply(tamBones,clock,walking);
            gunWeight=Mathf.MoveTowards(gunWeight,combat.IsAiming||Time.time-lastShot<.6f?1:0,Time.deltaTime/.18f);
            if(Time.time-lastShot<.06f)gunWeight=1; // The shot resolves later, after this frame's raised hand.
            for(int i=0;i<rightArm.Length;i++)rightArm[i].SetLocalPositionAndRotation(Vector3.Lerp(rightArm[i].localPosition,positions[i],gunWeight),Quaternion.Slerp(rightArm[i].localRotation,rotations[i],gunWeight));
            // The upright take supplies the leg pose; sole grounding determines body height.
            nam.transform.localPosition=namBase;
            tam.transform.localPosition=tamBase;
            Ground(nam,namContact,namFeet,namSkins,player.position.y);
            Ground(tam,tamContact,tamFeet,tamSkins,player.position.y);
            if(combat.IsReloading)rightUpper.rotation=Quaternion.AngleAxis(ReloadLowering(combat.ReloadProgress)*42,player.right)*rightUpper.rotation;
            else if(gunWeight>0)
            {
                var muzzle=combat.WeaponFor(WeaponKind.Pistol)?.Muzzle;var view=mission.GameCamera;
                if(muzzle!=null&&view!=null)rightUpper.rotation=Quaternion.Slerp(Quaternion.identity,Quaternion.FromToRotation(muzzle.forward,view.transform.forward),gunWeight)*rightUpper.rotation;
            }
        }
        static Transform[] Feet(Animator actor)=>actor.GetComponentsInChildren<Transform>().Where(t=>t.name=="LeftFoot"||t.name=="RightFoot").ToArray();
        void Ground(Animator actor,Map01SkinContact contact,Transform[] feet,SkinnedMeshRenderer[] skins,float rootFloor)
        {
            var foot=feet.OrderBy(t=>t.position.y).FirstOrDefault();var origin=foot!=null?foot.position:actor.transform.position;
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
            if(float.IsNegativeInfinity(floor))floor=rootFloor;
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
        public void Release()
        {
            if(!bound)return;
            if(combat!=null)combat.Attacked-=Shot;
            if(cameraRig!=null&&cameraRig.IgnoredCompanion==companionRoot)cameraRig.IgnoredCompanion=previousIgnoredCompanion;
            nam.transform.localPosition=namBase;tam.transform.localPosition=tamBase;
            for(int i=0;i<namBones.Length;i++)namOriginalPose[i].Apply(namBones[i]);
            for(int i=0;i<tamBones.Length;i++)tamOriginalPose[i].Apply(tamBones[i]);
            if(nam.isActiveAndEnabled)nam.Update(0);if(tam.isActiveAndEnabled)tam.Update(0);
            bound=false;
            if(groundMesh!=null){if(Application.isPlaying)Destroy(groundMesh);else DestroyImmediate(groundMesh);groundMesh=null;}
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
