using System;
using System.Linq;
using UnityEngine;
using ShadowVale.Gameplay.Combat;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The acted close-ups on the chapter's lines: posed actors (legs folded, hips placed, grounded), the radio
    /// and antenna gestures, each shot's camera, and their audio.
    /// </summary>
    public sealed partial class Map02ChapterDirector
    {
        sealed class PerformanceActor:IDisposable
        {
            public readonly Animator actor;public readonly Map02ChapterActor role;
            readonly Map01CinematicPose pose;readonly bool ownsPose;readonly AnimationClip idle;
            readonly Vector3 local;readonly Quaternion rotation;readonly Vector3 position;readonly Quaternion facing;
            readonly HumanPoseHandler muscles;
            readonly Mesh groundMesh=new Mesh();readonly System.Collections.Generic.List<Vector3> groundVertices=new System.Collections.Generic.List<Vector3>();
            public PerformanceActor(Animator actor,Map02ChapterActor role=null,Map01CinematicPose existing=null)
            {
                this.actor=actor;this.role=role;local=actor.transform.localPosition;rotation=actor.transform.localRotation;
                if(role!=null){position=role.transform.position;facing=role.transform.rotation;role.ConversationHeld=true;}
                idle=actor.runtimeAnimatorController!=null?actor.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name.Contains("Idle")):null;
                ownsPose=existing==null;pose=existing??new Map01CinematicPose(actor,skinGrounding:true);
                if(actor.isHuman)muscles=new HumanPoseHandler(actor.avatar,actor.transform);
            }
            public void Sample(float seconds,AnimationClip clip=null,bool loop=false)
            {pose.Sample(clip!=null?clip:idle,seconds,loop||clip==null);actor.transform.SetLocalPositionAndRotation(local,rotation);}
            public void SlowToIdle(AnimationClip walk,float phase,AnimationClip standing,float blend)
            {
                if(blend<=0){Sample(phase,walk,true);return;}
                pose.SampleBlend(walk,phase,standing,0,blend,true);
                actor.transform.SetLocalPositionAndRotation(local,rotation);
            }
            public float[] CaptureMuscles()
            {
                var p=new HumanPose();muscles.GetHumanPose(ref p);return p.muscles;
            }
            public void FoldLegs(float amount,float[] seatedMuscles=null,float openHands=0)
            {
                if(muscles==null)return;var p=new HumanPose();muscles.GetHumanPose(ref p);
                for(int i=0;i<HumanTrait.MuscleName.Length;i++)
                {
                    var n=HumanTrait.MuscleName[i];
                    if(seatedMuscles!=null&&(n.Contains(" Leg ")||n.Contains(" Foot ")||n.Contains(" Toes ")))
                        p.muscles[i]=Mathf.Lerp(p.muscles[i],seatedMuscles[i],amount);
                    else if(seatedMuscles==null&&n.Contains("Upper Leg Front-Back"))p.muscles[i]=Mathf.Lerp(p.muscles[i],.65f,amount);
                    else if(seatedMuscles==null&&n.Contains("Lower Leg Stretch"))p.muscles[i]=Mathf.Lerp(p.muscles[i],-.8f,amount);
                    else if(openHands>0&&n.EndsWith(" Stretched",StringComparison.Ordinal))p.muscles[i]=Mathf.Lerp(p.muscles[i],.30f,openHands);
                }
                muscles.SetHumanPose(ref p);
            }
            public void PlaceHips(Vector3 point,Quaternion bodyRotation)
            {actor.transform.rotation=bodyRotation;var hips=actor.GetBoneTransform(HumanBodyBones.Hips);if(hips!=null)actor.transform.position+=point-hips.position;}
            public void Ground(float floor,float weight=1)
            {
                float lowest=float.PositiveInfinity;
                var replacement=actor.transform.Find("Nam_AoBaBa_Visual");
                foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                {if(!skin.enabled||skin.forceRenderingOff||replacement!=null&&!skin.transform.IsChildOf(replacement))continue;skin.BakeMesh(groundMesh,replacement!=null);groundMesh.GetVertices(groundVertices);foreach(var v in groundVertices)lowest=Mathf.Min(lowest,skin.transform.TransformPoint(v).y);}
                if(!float.IsPositiveInfinity(lowest))actor.transform.position+=Vector3.up*(Mathf.Clamp(floor-lowest,-1,1)*weight);
            }
            public void SettleBack(float surface,float weight)
            {
                var hips=actor.GetBoneTransform(HumanBodyBones.Hips).position;
                var chest=actor.GetBoneTransform(HumanBodyBones.Chest).position;
                var axis=Vector3.ProjectOnPlane(chest-hips,Vector3.up).normalized;
                var across=Vector3.Cross(Vector3.up,axis);
                var pelvisContact=new Vector3(0,float.PositiveInfinity,0);
                var backContact=pelvisContact;
                float torsoLength=Vector3.Dot(chest-hips,axis);
                foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if(!skin.enabled||skin.forceRenderingOff)continue;
                    skin.BakeMesh(groundMesh);groundMesh.GetVertices(groundVertices);
                    foreach(var vertex in groundVertices)
                    {
                        var p=skin.transform.TransformPoint(vertex);var offset=p-hips;
                        if(Mathf.Abs(Vector3.Dot(offset,across))>.155f)continue;
                        float along=Vector3.Dot(offset,axis);
                        if(Mathf.Abs(along)<.12f&&p.y<pelvisContact.y)pelvisContact=p;
                        if(Mathf.Abs(along-torsoLength)<.115f&&p.y<backContact.y)backContact=p;
                    }
                }
                if(float.IsInfinity(pelvisContact.y)||float.IsInfinity(backContact.y)){Ground(surface,weight);return;}
                var contactAxis=backContact-pelvisContact;
                float pitch=Mathf.Atan2(contactAxis.y,Vector3.Dot(contactAxis,axis))*Mathf.Rad2Deg;
                var correction=Quaternion.AngleAxis(Mathf.Clamp(pitch,-15,15)*weight,across);
                var anchoredPelvis=hips+correction*(pelvisContact-hips);
                var anchoredBack=hips+correction*(backContact-hips);
                float lift=(surface-Mathf.Min(anchoredPelvis.y,anchoredBack.y))*weight;
                actor.transform.SetPositionAndRotation(hips+correction*(actor.transform.position-hips)+Vector3.up*lift,correction*actor.transform.rotation);
            }
            public void MeasureBlanketProfile(Transform mat,float back,float rowStep,float left,float columnStep,float[,] profile)
            {
                Array.Clear(profile,0,profile.Length);
                foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if(!skin.enabled||skin.forceRenderingOff)continue;
                    skin.BakeMesh(groundMesh);groundMesh.GetVertices(groundVertices);
                    var intoMat=mat.worldToLocalMatrix*skin.transform.localToWorldMatrix;
                    foreach(var v in groundVertices)
                    {
                        var p=intoMat.MultiplyPoint3x4(v);
                        if(Mathf.Abs(p.x)>.48f||p.y<.025f||p.y>.56f)continue;
                        int row=Mathf.RoundToInt((p.z-back)/rowStep),column=Mathf.RoundToInt((p.x-left)/columnStep);
                        for(int i=Mathf.Max(0,row-2);i<=Mathf.Min(profile.GetLength(0)-1,row+2);i++)
                            for(int j=Mathf.Max(0,column-2);j<=Mathf.Min(profile.GetLength(1)-1,column+2);j++)
                                profile[i,j]=Mathf.Max(profile[i,j],p.y-.014f*Mathf.Abs(i-row)-.024f*Mathf.Abs(j-column));
                    }
                }
            }
            public void Dispose()
            {muscles?.Dispose();UnityEngine.Object.Destroy(groundMesh);if(ownsPose)pose.Dispose();if(actor!=null)actor.transform.SetLocalPositionAndRotation(local,rotation);if(role!=null){role.StopAt(position);role.transform.rotation=facing;role.ConversationHeld=false;}}
        }
        PerformanceActor performanceHung,performanceNam;
        float nightOutroAt=-1;
        GameObject nightJacket;Mesh jacketMesh;Material jacketMaterial;AudioSource performanceSound,nightTheme;
        GameObject performanceAudioRoot;
        bool jacketSoundPlayed;
        public string PerformanceBeat {get;private set;}="";
        public bool NightJacketVisible=>nightJacket!=null&&nightJacket.activeSelf;
        bool NightSilentOutro=>sceneTask?.id=="N2_05"&&nightOutroAt>=0;

        void BeginPerformance(Map02ChapterTask task)
        {
            ReleasePerformance();
            if(task.id=="N1_04")
            {EnsurePerformanceAudio();nightTheme.clip=Layout.hungTheme!=null?Layout.hungTheme:Resources.Load<AudioClip>("OperationsAudio/hung_theme_night1");if(nightTheme.clip!=null)nightTheme.Play();return;}
            if(task.id!="D2_01"&&task.id!="D2_06"&&task.id!="N2_05")return;
            var hung=roles["Hùng"];if(hung.Animator!=null&&hung.Animator.isHuman)performanceHung=new PerformanceActor(hung.Animator,hung);
            var nam=Mission.Player.GetComponentInChildren<Animator>();if(nam!=null&&nam.isHuman)performanceNam=new PerformanceActor(nam,existing:namPose);
            if(task.id=="N2_05")BeginNightRest();
            if(task.id=="D2_06"&&performanceHung!=null)StageAntennaRadio();
        }
        bool PerformanceCanEnd()
        {
            if(sceneTask?.id!="N2_05")return true;
            if(sceneClock<nightWalkUntil)return false;
            if(nightOutroAt<0){nightOutroAt=sceneClock;return false;}
            return sceneClock-nightOutroAt>=12;
        }
        void SamplePerformance()
        {
            if(sceneTask==null||performanceHung==null)return;
            if(sceneTask.id=="N2_05"){SampleNightRest();return;}
            var hung=performanceHung.actor;var role=roles["Hùng"];var nam=performanceNam?.actor;
            performanceHung.Sample(sceneClock,sceneTask.id=="D2_01"?Layout.hungHurtReaction:sceneTask.id=="D2_06"?Layout.hungLeftRadio:null);
            if(sceneTask.id=="D2_01")
            {
                PerformanceBeat=lineIndex<3?"Nhận lệnh":lineIndex<5?"Hùng khựng lại":"Hùng quay về máy";
                if(lineIndex>=3)
                {
                    float weight=Mathf.SmoothStep(0,1,lineClock/.6f);
                    RotateBone(hung,HumanBodyBones.Head,new Vector3(18,-22,0),weight);
                    RotateBone(hung,HumanBodyBones.Chest,new Vector3(5,-10,0),weight);
                    if(lineIndex>=5)role.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(sites["radio"].position-role.transform.position+Vector3.forward*.1f,Vector3.up));
                    if(nam!=null&&lineIndex==3)
                    {
                        performanceNam.Sample(sceneClock,Layout.namCheckRifle);
                        RotateBone(nam,HumanBodyBones.Head,new Vector3(15,18,0),1);
                        var chest=nam.GetBoneTransform(HumanBodyBones.Chest);Map01ArmContact.Reach(nam,true,chest.position+nam.transform.forward*.3f-nam.transform.right*.1f,chest.position-nam.transform.right*.35f-Vector3.up*.15f,1);
                    }
                }
                foreach(var item in hiddenWeapons)if(item!=null&&item.TryGetComponent<Weapon>(out var weapon)&&weapon.Kind==WeaponKind.Rifle)item.SetActive(lineIndex==3);
                return;
            }
            if(sceneTask.id=="D2_06")
            {SampleAntennaRadio(hung);return;}
        }
        static void RotateBone(Animator actor,HumanBodyBones id,Vector3 angles,float weight)
        {var bone=actor.GetBoneTransform(id);if(bone!=null)bone.localRotation*=Quaternion.Slerp(Quaternion.identity,Quaternion.Euler(angles),weight);}
        static void LeftRadioGesture(Animator actor)
        {
            var chest=actor.GetBoneTransform(HumanBodyBones.Chest);var head=actor.GetBoneTransform(HumanBodyBones.Head);
            Map01ArmContact.Reach(actor,true,chest.position+actor.transform.forward*.3f-actor.transform.right*.12f-Vector3.up*.15f,chest.position-actor.transform.right*.38f,1);
            RotateBone(actor,HumanBodyBones.Head,new Vector3(16,0,0),1);
        }
        void SampleAntennaSupport()
        {
            var hung=roles["Hùng"];
            if(hung.Animator==null||!hung.Animator.isHuman)return;
            performanceHung??=new PerformanceActor(hung.Animator,hung);
            performanceHung.Sample(workClock,Layout.hungLeftRadio);LeftRadioGesture(hung.Animator);
            var delta=Vector3.ProjectOnPlane(antennaFoot-hung.transform.position,Vector3.up);if(delta.sqrMagnitude>.01f)hung.transform.rotation=Quaternion.LookRotation(delta);
            if(climbHeight>1)RotateBone(hung.Animator,HumanBodyBones.Head,new Vector3(-20,0,0),1);
            PerformanceBeat="Hùng hướng dẫn bằng tay trái";
        }
        void EnsurePerformanceAudio()
        {
            if(performanceAudioRoot!=null)return;
            performanceAudioRoot=new GameObject("Hùng — nhạc và tiếng áo");performanceAudioRoot.transform.SetParent(root.transform,false);
            performanceSound=performanceAudioRoot.AddComponent<AudioSource>();performanceSound.playOnAwake=false;performanceSound.clip=Resources.Load<AudioClip>("OperationsAudio/bag_cloth");performanceSound.volume=.35f;performanceSound.spatialBlend=0;
            nightTheme=performanceAudioRoot.AddComponent<AudioSource>();nightTheme.playOnAwake=false;nightTheme.volume=.22f;
        }
        void ReleasePerformance()
        {
            performanceHung?.Dispose();performanceHung=null;performanceNam?.Dispose();performanceNam=null;
            if(antennaHandRadio!=null)antennaHandRadio.gameObject.SetActive(false);
            if(nightJacket!=null)Destroy(nightJacket);if(jacketMesh!=null)Destroy(jacketMesh);if(jacketMaterial!=null)Destroy(jacketMaterial);
            if(performanceAudioRoot!=null)Destroy(performanceAudioRoot);performanceAudioRoot=null;
            ReleaseNightRest();
            nightJacket=null;jacketMesh=null;jacketMaterial=null;performanceSound=null;nightTheme=null;
            nightOutroAt=-1;jacketSoundPlayed=false;PerformanceBeat="";
        }
    }
}
