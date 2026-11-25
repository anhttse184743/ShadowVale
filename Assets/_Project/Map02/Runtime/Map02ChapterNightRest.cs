using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        const float NightWalkPace=1.45f;
        Transform nightRestHouse,nightRestProps;
        Vector3[] nightPairPath=Array.Empty<Vector3>();
        Vector3 nightMatCenter,nightNamSeat,nightNamStart,nightHungStart;
        Quaternion nightNamStartRotation,nightHungLeftFoot,nightHungRightFoot;
        float nightPairDistance,nightWalkUntil,nightHungSide;
        AnimationClip nightHungWalk;
        float[] nightSeatedMuscles,nightHungSitMuscles;
        Material nightMatMaterial,nightMatTrim,nightLampMetal,nightLampGlow,nightPillowReed,nightPillowBand;
        Mesh nightPillowMesh;
        Transform nightPillow;

        void BeginNightRest()
        {
            // House 6 is the west-facing thatch house immediately north of Lành's house (house 3).
            nightRestHouse=GameObject.Find("Bamboo thatch house 6")?.transform;
            var floor=nightRestHouse!=null?nightRestHouse.GetComponentsInChildren<BoxCollider>()
                .FirstOrDefault(c=>c.name=="Continuous floor"):null;
            if(floor==null)
            {
                nightRestHouse=null;
                Debug.LogWarning("Không tìm thấy sàn căn nhà nghỉ cuối Đêm 2.",this);
                return;
            }

            var deck=floor.bounds.max.y;
            Vector3 OnDeck(float x,float z)
            {var p=nightRestHouse.TransformPoint(new Vector3(x,0,z));p.y=deck;return p;}
            var entry=OnDeck(0,-5.65f);
            var porch=OnDeck(0,-4.04f);
            var doorway=OnDeck(0,-1.78f);
            nightMatCenter=OnDeck(-.76f,.34f);
            nightNamSeat=OnDeck(.27f,-.10f);
            var crossLegged=DayClip("LoiHuaNam");
            if(performanceNam!=null&&crossLegged!=null)
            {
                performanceNam.Sample(.5f,crossLegged);
                nightSeatedMuscles=performanceNam.CaptureMuscles();
            }
            var sit=DayClip("RestHung");
            if(performanceHung!=null&&sit!=null)
            {
                performanceHung.Sample(.18f,sit);
                nightHungSitMuscles=performanceHung.CaptureMuscles();
            }
            if(performanceHung!=null)
            {
                performanceHung.Sample(0,namCalmIdle);
                var actor=performanceHung.actor;
                var facing=Quaternion.Inverse(performanceHung.role.transform.rotation);
                nightHungLeftFoot=facing*actor.GetBoneTransform(HumanBodyBones.LeftFoot).rotation;
                nightHungRightFoot=facing*actor.GetBoneTransform(HumanBodyBones.RightFoot).rotation;
            }
            nightNamStart=Mission.Player.position;
            nightNamStartRotation=Mission.Player.rotation;
            nightHungStart=roles["Hùng"].transform.position;
            nightPairPath=NightRestPath((nightHungStart+nightNamStart)*.5f,entry,porch,doorway,(nightMatCenter+nightNamSeat)*.5f);
            nightPairDistance=Length(nightPairPath);
            nightWalkUntil=nightPairDistance/NightWalkPace;
            if(nightPairPath.Length>1)
            {
                var side=NightWalkFacing(nightPairPath,0,nightRestHouse.forward)*Vector3.right;
                nightHungSide=Vector3.Dot(nightHungStart-nightNamStart,side)>=0?1:-1;
            }
            nightHungWalk=Resources.Load<AnimationClip>("Rescue/Hung_Shelter_Walk")??ordinaryWalk;
            BuildNightRestProps();
        }

        static Vector3[] NightRestPath(Vector3 start,Vector3 entry,Vector3 porch,Vector3 doorway,Vector3 spot)
        {
            var path=new NavMeshPath();
            if(NavMesh.SamplePosition(start,out var from,2,NavMesh.AllAreas)&&NavMesh.SamplePosition(entry,out var threshold,2,NavMesh.AllAreas)
                &&NavMesh.CalculatePath(from.position,threshold.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete)
                return path.corners.Concat(new[]{porch,doorway,spot}).ToArray();
            return Array.Empty<Vector3>();
        }

        static Quaternion NightWalkFacing(Vector3[] path,float distance,Vector3 fallback)
        {
            var ahead=Along(path,distance+.4f)-Along(path,Mathf.Max(0,distance-.18f));
            ahead.y=0;
            return Quaternion.LookRotation(ahead.sqrMagnitude>.005f?ahead:fallback);
        }

        Vector3 NightPairPosition(Vector3 center,Vector3 right,float distance,bool hung)
        {
            float side=hung?nightHungSide:-nightHungSide;
            var offset=center+right*(side*.49f);
            if(distance<nightPairDistance-6f&&NavMesh.Raycast(center,offset,out var edge,NavMesh.AllAreas))
                offset=Vector3.MoveTowards(center,edge.position,Mathf.Max(0,edge.distance-.07f));
            float start=Mathf.SmoothStep(0,1,distance/2.25f);
            float stop=Mathf.SmoothStep(0,1,1-(nightPairDistance-distance)/2.0f);
            return Vector3.Lerp(Vector3.Lerp(hung?nightHungStart:nightNamStart,offset,start),hung?nightMatCenter:nightNamSeat,stop);
        }

        void SampleNightRest()
        {
            if(nightRestHouse==null||performanceNam==null)
            {
                performanceHung.Sample(sceneClock);
                return;
            }
            var hung=performanceHung.role.transform;
            if(sceneClock<nightWalkUntil&&nightPairPath.Length>1)
            {
                float distance=sceneClock*NightWalkPace;
                var center=Along(nightPairPath,distance);
                var facing=NightWalkFacing(nightPairPath,distance,nightRestHouse.forward);
                var right=facing*Vector3.right;
                float arrive=Mathf.SmoothStep(0,1,1-(nightPairDistance-distance)/.65f);
                hung.SetPositionAndRotation(NightPairPosition(center,right,distance,true),Quaternion.Slerp(facing,Quaternion.LookRotation(nightRestHouse.right),arrive));
                lockedPosition=NightPairPosition(center,right,distance,false);
                lockedRotation=Quaternion.Slerp(facing,Quaternion.LookRotation(-nightRestHouse.right),arrive);
                Mission.Player.SetPositionAndRotation(lockedPosition,lockedRotation);
                performanceHung.SlowToIdle(nightHungWalk,distance/1.45f+.18f,namCalmIdle,arrive);
                performanceNam.SlowToIdle(ordinaryWalk,distance/1.25f,namCalmIdle,arrive);
                StraightenNightWalk(performanceNam.actor);
                if(lines[lineIndex].speaker=="Hùng")RotateBone(performanceHung.actor,HumanBodyBones.Head,new Vector3(0,-13*nightHungSide,0),1);
                else RotateBone(performanceNam.actor,HumanBodyBones.Head,new Vector3(0,13*nightHungSide,0),1);
                PerformanceBeat="Vừa đi vừa trò chuyện";
                return;
            }

            LandAtNightRest();
            var outro=nightOutroAt<0?0:sceneClock-nightOutroAt;
            var seatedFacing=Quaternion.LookRotation(-nightRestHouse.forward);
            var turn=Mathf.SmoothStep(0,1,outro/.85f);
            hung.SetPositionAndRotation(nightMatCenter,Quaternion.Slerp(Quaternion.LookRotation(nightRestHouse.right),seatedFacing,turn));
            float seated=Mathf.SmoothStep(0,1,(outro-.50f)/1.32f);
            float lying=Mathf.SmoothStep(0,1,(outro-1.74f)/2.04f);
            var floor=nightMatCenter.y;
            var hungActor=performanceHung.actor;
            var namActor=performanceNam.actor;
            performanceHung.Sample(sceneClock,namCalmIdle,true);
            performanceNam.Sample(sceneClock,namCalmIdle,true);
            PerformanceBeat=outro<=0?"Trò chuyện trong nhà":outro<4?"Hùng nằm xuống chiếu":"Nam đắp chăn cho Hùng";
            if(outro<=0)return;

            float brace=Mathf.SmoothStep(0,1,(outro-.72f)/.64f);
            float openPalm=brace*(1-lying);
            performanceHung.FoldLegs(seated*(1-lying),nightHungSitMuscles,openPalm);
            float forwardLean=19*Mathf.Sin(seated*Mathf.PI)*(1-lying);
            var recline=Quaternion.AngleAxis(90*lying-forwardLean,nightRestHouse.right)*hung.rotation;
            var hungHips=nightMatCenter+nightRestHouse.forward*(.22f*seated*(1-lying))
                +Vector3.up*Mathf.Lerp(Mathf.Lerp(.94f,.25f,seated),.23f,lying);
            performanceHung.PlaceHips(hungHips,recline);
            if(seated<.16f)performanceHung.Ground(floor+.035f,1-seated/.16f);
            if(lying>.84f)performanceHung.SettleBack(floor+.043f,Mathf.SmoothStep(0,1,(lying-.84f)/.16f));
            float planted=Mathf.SmoothStep(0,1,(outro-.48f)/.52f);
            if(planted>0)
            {
                var ankle=nightMatCenter-nightRestHouse.forward*Mathf.Lerp(.13f,.94f,lying)+Vector3.up*.115f;
                SettleNightLeg(hungActor,true,ankle,seatedFacing,lying,planted);
                SettleNightLeg(hungActor,false,ankle,seatedFacing,lying,planted);
            }
            var handLine=nightMatCenter+nightRestHouse.forward*Mathf.Lerp(.34f,-.13f,lying)+Vector3.up*.083f;
            var rightSupport=handLine-nightRestHouse.right*Mathf.Lerp(.32f,.39f,lying);
            Map01ArmContact.Reach(hungActor,false,rightSupport,rightSupport-nightRestHouse.right*.23f+Vector3.up*.16f,brace);
            var leftSupport=handLine+nightRestHouse.right*Mathf.Lerp(.32f,.39f,lying);
            Map01ArmContact.Reach(hungActor,true,leftSupport,leftSupport+nightRestHouse.right*.23f+Vector3.up*.16f,brace);
            OrientNightPalm(hungActor,HumanBodyBones.LeftHand,HumanBodyBones.LeftIndexProximal,HumanBodyBones.LeftLittleProximal,-nightRestHouse.forward,openPalm);
            OrientNightPalm(hungActor,HumanBodyBones.RightHand,HumanBodyBones.RightIndexProximal,HumanBodyBones.RightLittleProximal,-nightRestHouse.forward,openPalm);
            RotateBone(hungActor,HumanBodyBones.Head,new Vector3(5,0,-12),lying);
            RestHeadOnPillow(hungActor,lying);

            float namSeated=Mathf.SmoothStep(0,1,(outro-.65f)/1.7f);
            performanceNam.FoldLegs(namSeated,nightSeatedMuscles);
            float reach=Mathf.SmoothStep(0,1,(outro-3.7f)/.9f)*(1-Mathf.SmoothStep(0,1,(outro-6.7f)/.7f));
            performanceNam.PlaceHips(nightNamSeat+Vector3.up*Mathf.Lerp(.94f,.16f,namSeated),lockedRotation);
            var grip=SampleNightBlanket(outro,hungActor);
            var towardGrip=Vector3.ProjectOnPlane(grip-nightNamSeat,Vector3.up).normalized;
            var bendAxis=Vector3.Cross(Vector3.up,towardGrip);
            float turnToFeet=1-Mathf.SmoothStep(0,1,(outro-4.7f)/1.1f);
            var spine=namActor.GetBoneTransform(HumanBodyBones.Spine);
            spine.rotation=Quaternion.AngleAxis(34*reach,bendAxis)*Quaternion.AngleAxis(-46*reach*turnToFeet,Vector3.up)*spine.rotation;
            var chest=namActor.GetBoneTransform(HumanBodyBones.Chest);
            chest.rotation=Quaternion.AngleAxis(11*reach,bendAxis)*chest.rotation;
            var palm=namActor.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position+Vector3.up*.061f;
            Map01ArmContact.Reach(namActor,true,palm,palm-nightRestHouse.forward*.27f+nightRestHouse.right*.14f,namSeated);
            var knee=namActor.GetBoneTransform(HumanBodyBones.RightLowerLeg).position+Vector3.up*.061f+nightRestHouse.right*.042f;
            Map01ArmContact.Reach(namActor,false,Vector3.Lerp(knee,grip,reach),nightNamSeat+nightRestHouse.forward*.27f+Vector3.up*.33f,namSeated);
            OrientNightPalm(namActor,HumanBodyBones.LeftHand,HumanBodyBones.LeftIndexProximal,HumanBodyBones.LeftLittleProximal,-nightRestHouse.right,namSeated);
            OrientNightPalm(namActor,HumanBodyBones.RightHand,HumanBodyBones.RightIndexProximal,HumanBodyBones.RightLittleProximal,-nightRestHouse.right,namSeated*(1-.6f*reach));
            RotateBone(namActor,HumanBodyBones.Head,new Vector3(14,0,0),namSeated);
        }

        static void OrientNightPalm(Animator actor,HumanBodyBones wrist,HumanBodyBones index,HumanBodyBones little,Vector3 direction,float weight)
        {
            if(weight<=0)return;
            var hand=actor.GetBoneTransform(wrist);
            var indexBase=actor.GetBoneTransform(index);var littleBase=actor.GetBoneTransform(little);
            if(hand==null||indexBase==null||littleBase==null)return;
            var fingers=(indexBase.position+littleBase.position)*.5f-hand.position;
            var across=indexBase.position-littleBase.position;
            var align=Quaternion.FromToRotation(fingers,direction);
            var normal=align*Vector3.Cross(fingers,across);
            if(normal.y<0)normal=-normal;
            float roll=Mathf.Clamp(Vector3.SignedAngle(normal,Vector3.up,direction),-75,75);
            hand.rotation=Quaternion.Slerp(hand.rotation,Quaternion.AngleAxis(roll,direction)*align*hand.rotation,weight);
        }

        void StraightenNightWalk(Animator actor)
        {
            var hips=actor.GetBoneTransform(HumanBodyBones.Hips);
            var spine=actor.GetBoneTransform(HumanBodyBones.Spine);
            var chest=actor.GetBoneTransform(HumanBodyBones.Chest);
            spine.rotation=Quaternion.Slerp(Quaternion.identity,Quaternion.FromToRotation(chest.position-hips.position,Vector3.up),.83f)*spine.rotation;
            var frame=Mission.Player;
            for(int i=0;i<2;i++)
            {
                bool left=i==0;float side=left?-1:1;
                var thigh=actor.GetBoneTransform(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
                var knee=actor.GetBoneTransform(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
                var ankle=actor.GetBoneTransform(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                float inward=.115f-Vector3.Dot(ankle.position-frame.position,frame.right)*side;
                if(inward<=0)continue;
                var footFacing=ankle.rotation;
                Map01ArmContact.ReachLimb(thigh,knee,ankle,ankle.position+frame.right*(side*inward),knee.position+frame.forward*.20f+frame.right*(side*.14f),frame.forward,Vector3.down,1);
                ankle.rotation=footFacing;
            }
        }

        void SettleNightLeg(Animator actor,bool left,Vector3 footCenter,Quaternion facing,float lying,float weight)
        {
            var thigh=actor.GetBoneTransform(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
            var knee=actor.GetBoneTransform(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
            var foot=actor.GetBoneTransform(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
            var original=foot.rotation;
            var outward=nightRestHouse.right*(left?1:-1);
            var bend=Vector3.Slerp(-nightRestHouse.forward,Vector3.up,lying)+outward*.17f;
            Map01ArmContact.ReachLimb(thigh,knee,foot,footCenter+outward*.16f,thigh.position+bend*.65f,-nightRestHouse.forward,Vector3.up,weight);
            var planted=facing*(left?nightHungLeftFoot:nightHungRightFoot);
            var heel=Quaternion.AngleAxis(24*lying,nightRestHouse.right)*planted;
            foot.rotation=Quaternion.Slerp(original,heel,weight);
        }

        void RestHeadOnPillow(Animator actor,float lying)
        {
            float weight=Mathf.SmoothStep(0,1,(lying-.78f)/.22f);
            if(weight<=0||nightPillow==null)return;
            var neck=actor.GetBoneTransform(HumanBodyBones.Neck);
            var head=actor.GetBoneTransform(HumanBodyBones.Head);
            if(neck==null||head==null)return;
            var set=neck.rotation;
            var toward=head.position-neck.position;
            var raised=Vector3.ProjectOnPlane(toward+Vector3.up*.035f,nightRestHouse.right);
            if(raised.sqrMagnitude>.001f)neck.rotation=Quaternion.Slerp(set,Quaternion.FromToRotation(toward,raised)*set,weight);
        }

        void LandAtNightRest()
        {
            if(nightRestHouse==null)return;
            lockedPosition=nightNamSeat;
            lockedRotation=Quaternion.LookRotation(-nightRestHouse.right);
            Mission.Player.SetPositionAndRotation(lockedPosition,lockedRotation);
        }

        Vector3 SampleNightBlanket(float outro,Animator hung)
        {
            float unfold=Mathf.SmoothStep(0,1,(outro-4.65f)/2.05f);
            float lift=Mathf.SmoothStep(0,1,(outro-4.1f)/.55f)*(1-Mathf.SmoothStep(0,1,(outro-6.32f)/.55f));
            var grip=ShapeNightBlanket(unfold,lift,outro>3.75f?hung:null);
            if(outro>=4.65f&&!jacketSoundPlayed)
            {
                jacketSoundPlayed=true;
                performanceSound.Play();
                nightTheme.clip=Layout.hungThemeNightTwo!=null?Layout.hungThemeNightTwo:Resources.Load<AudioClip>("OperationsAudio/hung_theme_night2");
                if(nightTheme.clip!=null)nightTheme.Play();
            }
            return grip;
        }

        void SamplePerformanceCamera()
        {
            if(sceneTask?.id!="N2_05"||nightRestHouse==null)return;
            var y=Vector3.up;
            Vector3 eye,focus;
            if(sceneClock<nightWalkUntil)
            {
                var mid=(Mission.Player.position+roles["Hùng"].transform.position)*.5f;
                var forward=Mission.Player.forward;
                focus=mid+y*1.15f;
                eye=sceneClock<nightWalkUntil-5f?focus+forward*2.4f+Mission.Player.right*2.65f+y*.38f
                    :focus-forward*3.65f+Mission.Player.right*1.25f+y*.55f;
                var ray=eye-focus;
                foreach(var hit in Physics.SphereCastAll(focus,.13f,ray.normalized,ray.magnitude,~0,QueryTriggerInteraction.Ignore))
                    if(hit.distance>.2f&&!hit.transform.IsChildOf(Mission.Player)&&hit.collider.GetComponentInParent<Map02ChapterActor>()==null)
                        eye=focus+ray.normalized*Mathf.Min(Vector3.Distance(eye,focus),Mathf.Max(.65f,hit.distance-.16f));
            }
            else
            {
                focus=Vector3.Lerp(nightMatCenter,nightNamSeat,.48f)+y*(nightOutroAt<0?1.08f:.57f);
                eye=nightOutroAt<0?nightMatCenter+nightRestHouse.right*3.17f-nightRestHouse.forward*1.91f+y*1.62f
                    :nightMatCenter+nightRestHouse.right*2.85f+nightRestHouse.forward*1.65f+y*1.77f;
            }
            rig.SetCinematicView(eye,Quaternion.LookRotation(focus-eye),sceneClock<nightWalkUntil?54:nightOutroAt<0?57:49,1);
        }

        void BuildNightRestProps()
        {
            nightRestProps=new GameObject("Đêm 2 — nhà nghỉ").transform;
            nightRestProps.SetParent(root.transform,false);
            nightRestProps.SetPositionAndRotation(nightMatCenter,Quaternion.LookRotation(nightRestHouse.forward));
            nightMatMaterial=NightRestMaterial("Sợi cói",new Color(.48f,.31f,.15f));
            nightMatTrim=NightRestMaterial("Viền chiếu",new Color(.34f,.17f,.08f));
            Part(nightRestProps,"Chiếu cói",new Vector3(0,.02f,0),new Vector3(1.05f,.028f,2.22f),nightMatMaterial);
            for(int i=0;i<51;i++)
                Part(nightRestProps,"Sợi chiếu",new Vector3(0,.036f,-1.065f+i*.0426f),new Vector3(1.015f,.009f,.012f),nightMatTrim);
            Part(nightRestProps,"Viền dệt",new Vector3(-.5f,.037f,0),new Vector3(.028f,.013f,2.19f),nightMatTrim);
            Part(nightRestProps,"Viền dệt",new Vector3(.5f,.037f,0),new Vector3(.028f,.013f,2.19f),nightMatTrim);
            BuildNightPillow();

            nightLampMetal=NightRestMaterial("Đèn dầu — đồng",new Color(.28f,.17f,.07f));
            nightLampGlow=NightRestMaterial("Đèn dầu — ngọn lửa",new Color(1,.58f,.19f));
            nightLampGlow.EnableKeyword("_EMISSION");
            nightLampGlow.SetColor("_EmissionColor",new Color(1,.44f,.12f)*2.5f);
            var lamp=new GameObject("Đèn dầu treo").transform;
            lamp.SetParent(nightRestProps,false);lamp.localPosition=new Vector3(.59f,1.98f,-.38f);
            Part(lamp,"Dây treo",new Vector3(0,.49f,0),new Vector3(.015f,.74f,.015f),nightLampMetal);
            Part(lamp,"Bình dầu",new Vector3(0,-.1f,0),new Vector3(.23f,.055f,.23f),nightLampMetal,PrimitiveType.Cylinder);
            Part(lamp,"Chụp đèn",new Vector3(0,.13f,0),new Vector3(.22f,.021f,.22f),nightLampMetal,PrimitiveType.Cylinder);
            Part(lamp,"Ngọn đèn",Vector3.zero,new Vector3(.09f,.20f,.09f),nightLampGlow,PrimitiveType.Sphere);
            foreach(float side in new[]{-.095f,.095f})
                Part(lamp,"Gọng đèn",new Vector3(side,.015f,0),new Vector3(.014f,.23f,.014f),nightLampMetal);
            var light=lamp.gameObject.AddComponent<Light>();
            light.type=LightType.Point;light.color=new Color(1,.79f,.57f);light.range=6;light.intensity=5.5f;
            light.shadows=LightShadows.Soft;light.shadowStrength=.70f;light.shadowBias=.04f;light.shadowNormalBias=.18f;

            nightJacket=new GameObject("Đêm 2 — chăn Nam đắp cho Hùng");
            nightJacket.transform.SetParent(nightRestProps,false);
            jacketMaterial=NightRestMaterial("Chăn vải",new Color(.33f,.275f,.185f));
            jacketMaterial.SetFloat("_Cull",0);
            const int rows=BlanketRows,columns=BlanketColumns;
            blanketDraped=new Vector3[(rows+1)*(columns+1)];
            var triangles=new int[rows*columns*6];
            int k=0;
            for(int z=0;z<rows;z++)for(int x=0;x<columns;x++)
            {int a=z*(columns+1)+x,b=a+columns+1;triangles[k++]=a;triangles[k++]=b;triangles[k++]=a+1;triangles[k++]=a+1;triangles[k++]=b;triangles[k++]=b+1;}
            jacketMesh=new Mesh{name="Chăn vải đắp Hùng",vertices=blanketDraped,triangles=triangles};
            jacketMesh.MarkDynamic();
            jacketMesh.RecalculateNormals();jacketMesh.RecalculateBounds();
            nightJacket.AddComponent<MeshFilter>().sharedMesh=jacketMesh;
            nightJacket.AddComponent<MeshRenderer>().sharedMaterial=jacketMaterial;
            nightJacket.transform.SetPositionAndRotation(nightMatCenter,nightRestProps.rotation);
            ShapeNightBlanket(0,0,null);
            EnsurePerformanceAudio();
        }

        void BuildNightPillow()
        {
            nightPillow=new GameObject("Gối tre đan").transform;
            nightPillow.SetParent(nightRestProps,false);
            nightPillow.localPosition=new Vector3(0,.094f,.69f);
            nightPillowReed=NightRestMaterial("Tre đan vàng nhạt",new Color(.68f,.50f,.24f));
            nightPillowBand=NightRestMaterial("Viền tre đỏ đất",new Color(.51f,.20f,.13f));
            const int rings=40,sides=48;
            var vertices=new Vector3[(rings+1)*sides+2];
            var weave=new System.Collections.Generic.List<int>();
            var bands=new System.Collections.Generic.List<int>();
            for(int i=0;i<=rings;i++)
            {
                float u=i/(float)rings*2-1;
                float ends=u*u;
                for(int j=0;j<sides;j++)
                {
                    float angle=j*Mathf.PI*2/sides;
                    float fiber=((i+j)%2==0?.0022f:-.0022f);
                    vertices[i*sides+j]=new Vector3(u*.255f,Mathf.Cos(angle)*(.047f+.018f*ends+fiber),Mathf.Sin(angle)*(.106f+.027f*ends+fiber));
                    if(i==rings)continue;
                    var triangles=i<5||i>=rings-5?bands:weave;
                    int next=(j+1)%sides;
                    int a=i*sides+j,b=i*sides+next,c=(i+1)*sides+j,d=(i+1)*sides+next;
                    triangles.Add(a);triangles.Add(b);triangles.Add(c);triangles.Add(b);triangles.Add(d);triangles.Add(c);
                }
            }
            int firstCap=vertices.Length-2,lastCap=vertices.Length-1;
            vertices[firstCap]=Vector3.left*.255f;vertices[lastCap]=Vector3.right*.255f;
            for(int j=0;j<sides;j++)
            {
                int next=(j+1)%sides;
                bands.Add(firstCap);bands.Add(next);bands.Add(j);
                bands.Add(lastCap);bands.Add(rings*sides+j);bands.Add(rings*sides+next);
            }
            nightPillowMesh=new Mesh{name="Gối tre đan",vertices=vertices,subMeshCount=2};
            nightPillowMesh.SetTriangles(weave,0);nightPillowMesh.SetTriangles(bands,1);
            nightPillowMesh.RecalculateNormals();nightPillowMesh.RecalculateBounds();
            nightPillow.gameObject.AddComponent<MeshFilter>().sharedMesh=nightPillowMesh;
            nightPillow.gameObject.AddComponent<MeshRenderer>().sharedMaterials=new[]{nightPillowReed,nightPillowBand};
        }

        const int BlanketRows=24,BlanketColumns=22;
        const float BlanketFoot=-1.09f,BlanketProfileStep=.036f,BlanketProfileLeft=-.48f,BlanketProfileWidth=.04f;
        readonly float[,] blanketProfile=new float[43,25];
        Vector3[] blanketDraped;
        Vector3 ShapeNightBlanket(float unfold,float lift,Animator hung)
        {
            float length=Mathf.Lerp(.27f,1.45f,unfold);
            if(hung!=null)performanceHung.MeasureBlanketProfile(nightJacket.transform,BlanketFoot,BlanketProfileStep,BlanketProfileLeft,BlanketProfileWidth,blanketProfile);
            for(int z=0;z<=BlanketRows;z++)for(int x=0;x<=BlanketColumns;x++)
            {
                float v=z/(float)BlanketRows,u=x/(float)BlanketColumns*2-1;
                float travel=v*length;
                float profileRow=travel/BlanketProfileStep;
                int row=Mathf.FloorToInt(profileRow);
                float lateral=u*Mathf.Lerp(.38f,.47f,unfold);
                float profileColumn=(lateral-BlanketProfileLeft)/BlanketProfileWidth;
                int column=Mathf.FloorToInt(profileColumn);
                float here=Mathf.Lerp(blanketProfile[row,column],blanketProfile[row,column+1],profileColumn-column);
                float next=Mathf.Lerp(blanketProfile[row+1,column],blanketProfile[row+1,column+1],profileColumn-column);
                float draped=hung!=null?Mathf.Max(.046f,Mathf.Lerp(here,next,profileRow-row)+.022f):.08f;
                float folded=.079f+.018f*Mathf.Sin(v*4*Mathf.PI)*Mathf.Cos(u*Mathf.PI*.5f);
                float top=Mathf.Lerp(folded,draped,Mathf.Max(lift,Mathf.SmoothStep(0,1,unfold*5f)));
                float nearHand=Mathf.Lerp(.19f,1,Mathf.SmoothStep(0,1,(u+1)*.5f));
                top+=lift*v*v*.185f*nearHand+.005f*Mathf.Sin(u*9+v*16)*unfold;
                float gathered=lift*v*v*nearHand*(1-unfold);
                blanketDraped[z*(BlanketColumns+1)+x]=new Vector3(lateral+gathered*.095f,top,BlanketFoot+travel+gathered*.15f);
            }
            jacketMesh.vertices=blanketDraped;
            jacketMesh.RecalculateNormals();
            jacketMesh.RecalculateBounds();
            int corner=BlanketRows*(BlanketColumns+1)+BlanketColumns;
            return nightJacket.transform.TransformPoint(blanketDraped[corner]);
        }

        static Material NightRestMaterial(string name,Color color)
        {
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};
            mat.SetFloat("_Smoothness",.14f);return mat;
        }

        void ReleaseNightRest()
        {
            if(nightRestProps!=null)Destroy(nightRestProps.gameObject);
            if(nightMatMaterial!=null)Destroy(nightMatMaterial);
            if(nightMatTrim!=null)Destroy(nightMatTrim);
            if(nightLampMetal!=null)Destroy(nightLampMetal);
            if(nightLampGlow!=null)Destroy(nightLampGlow);
            if(nightPillowMesh!=null)Destroy(nightPillowMesh);
            if(nightPillowReed!=null)Destroy(nightPillowReed);
            if(nightPillowBand!=null)Destroy(nightPillowBand);
            nightPillow=null;nightPillowMesh=null;nightPillowReed=nightPillowBand=null;
            nightRestProps=nightRestHouse=null;
            nightMatMaterial=nightMatTrim=nightLampMetal=nightLampGlow=null;
            nightPairPath=Array.Empty<Vector3>();nightHungWalk=null;
            blanketDraped=null;nightSeatedMuscles=nightHungSitMuscles=null;
            nightPairDistance=nightWalkUntil=0;
        }
    }
}
