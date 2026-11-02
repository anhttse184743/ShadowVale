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
            public void Sample(float seconds,AnimationClip clip=null)
            {pose.Sample(clip!=null?clip:idle,seconds,clip==null);actor.transform.SetLocalPositionAndRotation(local,rotation);}
            public void FoldLegs(float amount)
            {
                if(muscles==null)return;var p=new HumanPose();muscles.GetHumanPose(ref p);
                for(int i=0;i<HumanTrait.MuscleName.Length;i++)
                {var n=HumanTrait.MuscleName[i];if(n.Contains("Upper Leg Front-Back"))p.muscles[i]=Mathf.Lerp(p.muscles[i],.65f,amount);else if(n.Contains("Lower Leg Stretch"))p.muscles[i]=Mathf.Lerp(p.muscles[i],-.8f,amount);}
                muscles.SetHumanPose(ref p);
            }
            public void PlaceHips(Vector3 point,Quaternion bodyRotation)
            {actor.transform.rotation=bodyRotation;var hips=actor.GetBoneTransform(HumanBodyBones.Hips);if(hips!=null)actor.transform.position+=point-hips.position;}
            public void Ground(float floor)
            {
                float lowest=float.PositiveInfinity;
                foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                {if(!skin.enabled)continue;skin.BakeMesh(groundMesh);groundMesh.GetVertices(groundVertices);foreach(var v in groundVertices)lowest=Mathf.Min(lowest,skin.transform.TransformPoint(v).y);}
                if(!float.IsPositiveInfinity(lowest))actor.transform.position+=Vector3.up*Mathf.Clamp(floor-lowest,-1,1);
            }
            public void Dispose()
            {muscles?.Dispose();UnityEngine.Object.Destroy(groundMesh);if(ownsPose)pose.Dispose();if(actor!=null)actor.transform.SetLocalPositionAndRotation(local,rotation);if(role!=null){role.StopAt(position);role.transform.rotation=facing;role.ConversationHeld=false;}}
        }
        PerformanceActor performanceHung,performanceNam;
        float nightRestAt=-1,nightOutroAt=-1;
        GameObject nightJacket;Mesh jacketMesh;Material jacketMaterial;AudioSource performanceSound,nightTheme;
        GameObject performanceAudioRoot;
        readonly System.Collections.Generic.Dictionary<Renderer,bool> quietFrameRenderers=new System.Collections.Generic.Dictionary<Renderer,bool>();
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
            if(task.id=="N2_05")BuildNightJacket();
        }
        void OnPerformanceLine()
        {if(sceneTask?.id=="N2_05"&&lineIndex>=5&&nightRestAt<0)nightRestAt=sceneClock;}
        bool PerformanceCanEnd()
        {
            if(sceneTask?.id!="N2_05")return true;
            if(nightOutroAt<0){nightOutroAt=sceneClock;PerformanceBeat="Hùng thiếp ngủ";return false;}
            return sceneClock-nightOutroAt>=12;
        }
        void SamplePerformance()
        {
            if(sceneTask==null||performanceHung==null)return;
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
            {PerformanceBeat="Hùng nối máy bằng tay trái";LeftRadioGesture(hung);return;}
            if(nightRestAt<0)
            {
                PerformanceBeat="Làm lành tại điểm hẹn";
                RotateBone(hung,HumanBodyBones.Head,new Vector3(4,lineIndex==1?-25:0,0),1);
                if(nam!=null&&lineIndex>=2&&lineIndex<=4)RotateBone(nam,HumanBodyBones.Head,new Vector3(-3,0,Mathf.Sin(sceneClock*2)*2),1);
                return;
            }
            PerformanceBeat=NightSilentOutro?"Ngủ và đắp áo":"Đổi ca tại chỗ nghỉ";
            // Tâm rests beside this spot in gameplay. Exclude the non-speaking
            // background actor from this close shot without changing his saved position.
            if(quietFrameRenderers.Count==0&&roles["Tâm"].visual!=null)
                foreach(var renderer in roles["Tâm"].visual.GetComponentsInChildren<Renderer>(true)){quietFrameRenderers[renderer]=renderer.forceRenderingOff;renderer.forceRenderingOff=true;}
            var rest=sites["rest"].position;role.transform.SetPositionAndRotation(rest,Quaternion.Euler(0,180,0));
            lockedPosition=rest+Vector3.right*1.05f;lockedRotation=Quaternion.Euler(0,180,0);Mission.Player.SetPositionAndRotation(lockedPosition,lockedRotation);
            float outro=nightOutroAt<0?0:sceneClock-nightOutroAt;
            float lie=nightOutroAt<0?0:Mathf.SmoothStep(0,1,outro/3);
            performanceHung.Sample(sceneClock,lie>.5f?Layout.hungNightSleep:Layout.hungNightRest);
            if(Layout.hungNightSleep==null||lie<.5f)
            {
                performanceHung.FoldLegs(1);
                var upright=role.transform.rotation;
                performanceHung.PlaceHips(rest+Vector3.up*Mathf.Lerp(.48f,.28f,lie),Quaternion.Slerp(upright,Quaternion.AngleAxis(90,role.transform.forward)*upright,lie));
                performanceHung.Ground(rest.y);
                var rightArm=hung.GetBoneTransform(HumanBodyBones.RightLowerArm);var chest=hung.GetBoneTransform(HumanBodyBones.Chest);
                Map01ArmContact.Reach(hung,true,rightArm.position+hung.transform.forward*.05f,chest.position+hung.transform.up*.3f,1);
                RotateBone(hung,HumanBodyBones.Head,new Vector3(8,0,-8),1);
                RotateBone(hung,HumanBodyBones.Chest,new Vector3(Mathf.Sin(sceneClock*1.3f)*.5f,0,0),lie);
            }
            if(nam!=null&&outro>=3)
            {
                performanceNam.Sample(sceneClock,Layout.namCoverJacket);
                if(Layout.namCoverJacket==null)
                {
                    performanceNam.FoldLegs(Mathf.SmoothStep(0,1,(outro-3)/1.5f));
                    performanceNam.PlaceHips(lockedPosition+Vector3.up*.65f,Quaternion.LookRotation(Vector3.ProjectOnPlane(rest-lockedPosition,Vector3.up)));
                    performanceNam.Ground(rest.y);
                    var chest=nam.GetBoneTransform(HumanBodyBones.Chest);
                    var contact=hung.GetBoneTransform(HumanBodyBones.Chest).position+Vector3.up*.12f;
                    float reach=Mathf.Sin(Mathf.Clamp01((outro-3)/5)*Mathf.PI);
                    Map01ArmContact.Reach(nam,true,Vector3.Lerp(chest.position-nam.transform.right*.2f,contact,reach),chest.position-nam.transform.right*.4f,1);
                    Map01ArmContact.Reach(nam,false,Vector3.Lerp(chest.position+nam.transform.right*.2f,contact+Vector3.forward*.25f,reach),chest.position+nam.transform.right*.4f,1);
                    if(outro>8)RotateBone(nam,HumanBodyBones.Head,new Vector3(0,-35,0),1);
                }
            }
            if(nightJacket!=null)
            {
                nightJacket.SetActive(outro>=4);
                var chest=hung.GetBoneTransform(HumanBodyBones.Chest).position;
                var hips=hung.GetBoneTransform(HumanBodyBones.Hips).position;
                var bodyAxis=Vector3.ProjectOnPlane(hung.GetBoneTransform(HumanBodyBones.Head).position-hips,Vector3.up).normalized;
                var from=nam!=null?nam.GetBoneTransform(HumanBodyBones.LeftHand).position:chest+Vector3.up;
                float settle=Mathf.SmoothStep(0,1,(outro-4)/3);
                nightJacket.transform.SetPositionAndRotation(Vector3.Lerp(from,hips-bodyAxis*.12f+Vector3.up*.14f,settle),Quaternion.LookRotation(bodyAxis.sqrMagnitude>.01f?bodyAxis:role.transform.forward));
                if(outro>=6&&!jacketSoundPlayed)
                {jacketSoundPlayed=true;performanceSound.Play();nightTheme.clip=Layout.hungThemeNightTwo!=null?Layout.hungThemeNightTwo:Resources.Load<AudioClip>("OperationsAudio/hung_theme_night2");if(nightTheme.clip!=null)nightTheme.Play();}
            }
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
        void SamplePerformanceCamera()
        {
            if(sceneTask?.id!="N2_05"||nightRestAt<0)return;
            var rest=sites["rest"].position;var focus=rest+Vector3.up*.55f+Vector3.right*.5f;
            var eye=rest+new Vector3(-3.2f,1.8f,-3.5f);
            rig.SetCinematicView(eye,Quaternion.LookRotation(focus-eye),52,1);
        }
        void BuildNightJacket()
        {
            nightJacket=new GameObject("Đêm 2 — áo khoác Nam đắp cho Hùng");nightJacket.transform.SetParent(root.transform,false);
            jacketMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Áo khoác dã chiến"};jacketMaterial.SetColor("_BaseColor",new Color(.17f,.21f,.12f));jacketMaterial.SetFloat("_Smoothness",.12f);jacketMaterial.SetFloat("_Cull",0);
            const int rows=14,columns=10;var vertices=new Vector3[(rows+1)*(columns+1)];var triangles=new int[rows*columns*6];
            for(int z=0;z<=rows;z++)for(int x=0;x<=columns;x++)
            {float u=x/(float)columns*2-1,v=z/(float)rows;float width=v>.65f?.45f:.32f;vertices[z*(columns+1)+x]=new Vector3(u*width,Mathf.Cos(u*Mathf.PI*.5f)*.12f+Mathf.Sin(v*30+u*3)*.015f,(v-.5f)*.9f);}
            int k=0;for(int z=0;z<rows;z++)for(int x=0;x<columns;x++){int a=z*(columns+1)+x,b=a+columns+1;triangles[k++]=a;triangles[k++]=b;triangles[k++]=a+1;triangles[k++]=a+1;triangles[k++]=b;triangles[k++]=b+1;}
            jacketMesh=new Mesh{name="Áo khoác mềm — dựng cảnh"};jacketMesh.vertices=vertices;jacketMesh.triangles=triangles;jacketMesh.RecalculateNormals();jacketMesh.RecalculateBounds();nightJacket.AddComponent<MeshFilter>().sharedMesh=jacketMesh;nightJacket.AddComponent<MeshRenderer>().sharedMaterial=jacketMaterial;nightJacket.SetActive(false);
            EnsurePerformanceAudio();
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
            if(nightJacket!=null)Destroy(nightJacket);if(jacketMesh!=null)Destroy(jacketMesh);if(jacketMaterial!=null)Destroy(jacketMaterial);
            if(performanceAudioRoot!=null)Destroy(performanceAudioRoot);performanceAudioRoot=null;
            foreach(var pair in quietFrameRenderers)if(pair.Key!=null)pair.Key.forceRenderingOff=pair.Value;quietFrameRenderers.Clear();
            nightJacket=null;jacketMesh=null;jacketMaterial=null;performanceSound=null;nightTheme=null;
            nightRestAt=nightOutroAt=-1;jacketSoundPlayed=false;PerformanceBeat="";
        }
    }
}
