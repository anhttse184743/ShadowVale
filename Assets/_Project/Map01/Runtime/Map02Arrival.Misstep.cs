using System.Linq;
using UnityEngine;
namespace ShadowVale.Map01
{
    public sealed partial class Map02Arrival
    {
        // C1 (doc: "Hùng bước lên, hụt chân. Nam đặt ba lô xuống trước rồi mới đỡ", 2026-10-25), made on Mixamo mocap in
        // chuyen_dong/blender_scripts/map2_c1_hutchan_mixamo.py. Nam, up first, steps aside at the top of the stairs and turns
        // to watch Hùng come up; on the last stair Hùng's toe catches the riser and he goes down on his right knee and left
        // hand; Nam slips his pack off and sets it down at his side as he kneels by him, they get up together, Nam takes the
        // pack up again and both walk on to the village. Without the two takes the landing plays as before.
        // The C1 frame (Blender): origin at the top riser on Hùng's lane, -Y up onto the bank, +X Hùng's left, z up. The takes
        // play in place; C1Travel.json moves each actor's root as the body travels, so the walk on starts under the body.
        const float C1RiserX=-1.85f,C1Bank=1.01f,C1Lane=-.45f,C1StairCycle=4.6f,C1HungWalk=8.5f,C1NamWalk=7.5f,C1NamToSpot=1.2f,C1NamTurn=.7f;
        static readonly Vector3 C1Hung0=new(0,.10f,-.21f),C1Nam0=new(.78f,-.42f,0);
        AnimationClip c1Hung,c1Nam;float c1Start=-1;Vector3 c1HungFrom,c1NamFrom;float[] c1HungTravel,c1NamTravel;C1Track c1Track;
        Transform c1Pack;SkinnedMeshRenderer c1WornPack;Transform c1Chest;Vector3 c1PackInChest;Quaternion c1PackRotInChest,c1PackRot0,c1Blender0;Vector3 c1GroundAt;Quaternion c1GroundRot;bool c1Grounded,c1Thud,c1SetDown;
        [System.Serializable] sealed class C1Track{public float fps=30;public C1Frame[] pack;}
        [System.Serializable] sealed class C1Frame{public float t;public string mode;public float[] pos,rot,hand,chest;}
        [System.Serializable] sealed class C1TravelData{public float[] hung,nam;}
        bool Misstep=>c1Hung!=null&&c1Nam!=null&&c1Track?.pack?.Length>0&&c1HungTravel!=null&&c1NamTravel!=null;
        public bool MisstepPlayed=>c1Start>=0;
        public float MisstepClock=>c1Start<0?-1:clock-c1Start;
        bool c1PackWorn;
        public Transform MisstepPack=>c1PackWorn?null:c1Pack;
        public SkinnedMeshRenderer WornPack=>c1WornPack;
        public bool WornPackVisible=>c1PackWorn&&c1Pack!=null&&c1Pack.gameObject.activeInHierarchy||c1WornPack!=null&&c1WornPack.enabled;
        public Bounds MisstepPackBounds=>c1Pack!=null?c1Pack.GetComponentInChildren<Renderer>().bounds:c1WornPack!=null?c1WornPack.bounds:default;
        static Quaternion C1ChestBasis(Animator actor)
        {
            var right=actor.GetBoneTransform(HumanBodyBones.RightUpperArm).position-actor.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
            var up=actor.GetBoneTransform(HumanBodyBones.Head).position-actor.GetBoneTransform(HumanBodyBones.Hips).position;
            return Quaternion.LookRotation(Vector3.Cross(right,up).normalized,up.normalized);
        }
        public float LaneOf(int index)=>Lane(index);
        public bool MisstepOwns(int index)=>MisstepOwnsFeet(index);
        public static Vector3 MisstepPoint(Vector3 blender)=>C1Point(blender);
        float Lane(int index)=>index==0?0:index==1?-.8f:Misstep?C1Lane:.8f;
        // Hùng reaches the top tread at his stair cycle C1StairCycle; both then play their takes and walk on.
        float ExitLength=>Misstep?2*PassengerDelay+StandDuration+LaneDuration+C1StairCycle/StairX.Length*StairDuration+Mathf.Max(c1Hung.length+C1HungWalk,c1Nam.length+C1NamWalk)+.8f:ExitDuration;
        void LoadMisstep()
        {
            approvedC1=Resources.Load<Map02ArrivalC1Data>("Map02ArrivalC1/Approved");
            if(approvedC1!=null){c1Hung=approvedC1.hungMotion;c1Nam=approvedC1.namMotion;c1Track=JsonUtility.FromJson<C1Track>(approvedC1.pack.text);var t=JsonUtility.FromJson<C1TravelData>(approvedC1.travel.text);c1HungTravel=t.hung;c1NamTravel=t.nam;return;}
            var art=Resources.Load<Map02DayOneArt>("Story/Map02DayOneArt");c1Hung=art?.Find("HungHutChan");c1Nam=art?.Find("NamDoHung");
            var pack=Resources.Load<TextAsset>("Map02DayOne/C1Pack");if(pack!=null)c1Track=JsonUtility.FromJson<C1Track>(pack.text);
            var travel=Resources.Load<TextAsset>("Map02DayOne/C1Travel");if(travel!=null){var t=JsonUtility.FromJson<C1TravelData>(travel.text);c1HungTravel=t.hung;c1NamTravel=t.nam;}
        }
        static Vector3 C1Dir(Vector3 f)=>DockRotation*new Vector3(f.y,f.z,-f.x);
        static Vector3 C1Point(Vector3 f)=>DockPoint(new Vector3(C1RiserX+f.y,C1Bank+f.z,C1Lane-f.x));
        static Vector3 C1Vector(float[] a,int i)=>new(a[i],a[i+1],a[i+2]);
        /// <summary>A rotation from the Blender frame, as a world rotation (x axis to forward, z axis to up).</summary>
        static Quaternion C1Rotation(float[] q)
        {
            var b=new Quaternion(q[0],q[1],q[2],q[3]);
            return Quaternion.LookRotation(C1Dir(b*Vector3.right),C1Dir(b*new Vector3(0,0,1)));
        }
        static Vector3 C1Travel(float[] track,float seconds)
        {
            int n=track.Length/2;float f=Mathf.Clamp(seconds*30,0,n-1);int i=Mathf.Min((int)f,n-2);float k=f-i;
            return new Vector3(Mathf.Lerp(track[i*2],track[i*2+2],k),Mathf.Lerp(track[i*2+1],track[i*2+3],k),0);
        }
        Quaternion HungFacing=>DockRotation*Quaternion.LookRotation(Vector3.left);   // up the stairs
        Quaternion NamFacing=>DockRotation*Quaternion.LookRotation(Vector3.forward); // across the top, toward Hùng's lane
        /// <summary>The landing for Nam (index 0) and Hùng (index 2) when C1 plays; false hands back to the usual steps.</summary>
        bool MisstepPassenger(int index,float t)
        {
            if(!Misstep||index==1)return false;
            var passenger=Passengers[index];var pose=poses[index];
            if(index==2)
            {
                float at=StandDuration+LaneDuration+C1StairCycle/StairX.Length*StairDuration;
                if(t<at)return false;
                if(c1Start<0)BeginMisstep();
                float c=clock-c1Start;
                if(c<c1Hung.length)
                {
                    var root=C1Point(C1Hung0+C1Travel(c1HungTravel,c));
                    passenger.SetPositionAndRotation(Vector3.Lerp(c1HungFrom,root,Mathf.SmoothStep(0,1,c/.3f)),HungFacing);
                    if(!c1Thud&&c>=.85f){c1Thud=true;Thud(actors[2].GetBoneTransform(HumanBodyBones.RightLowerLeg).position,.55f);}
                    return true;
                }
                WalkOn(2,C1Point(C1Hung0+C1Travel(c1HungTravel,c1Hung.length)),c-c1Hung.length,C1HungWalk);return true;
            }
            float stairEnd=StandDuration+StairDuration;if(t<stairEnd)return false;
            float w=t-stairEnd;var top=DockPoint(new Vector3(-2.35f,1.25f-WaterlineRoot,0));var spot=C1Point(C1Nam0);
            if(c1Start<0||clock<c1Start)
            {
                if(w<C1NamToSpot)
                {   // a few steps aside along the top of the stairs
                    pose.Play(walk[0]);pose.Rate(Vector3.Distance(top,spot)/C1NamToSpot*walk[0].length/WalkStride);
                    passenger.SetPositionAndRotation(Vector3.Lerp(top,spot,w/C1NamToSpot),Quaternion.LookRotation(Vector3.ProjectOnPlane(spot-top,Vector3.up)));return true;
                }
                if(w<C1NamToSpot+C1NamTurn)
                {   // and round to face across the stairs, Hùng's way
                    pose.Play(turn[0]);pose.Rate(turn[0].length/C1NamTurn);if(w>C1NamToSpot+C1NamTurn*.5f)SlingRifle();
                    passenger.SetPositionAndRotation(spot,Quaternion.Slerp(Quaternion.LookRotation(Vector3.ProjectOnPlane(spot-top,Vector3.up)),NamFacing,Mathf.SmoothStep(0,1,(w-C1NamToSpot)/C1NamTurn)));return true;
                }
                pose.Play(approvedC1!=null?stand[0]:c1Nam);pose.Rate(0);passenger.SetPositionAndRotation(spot,NamFacing);StartApprovedVisual(0);return true;   // waiting, watching him climb
            }
            float n=clock-c1Start;
            if(n<c1Nam.length){passenger.SetPositionAndRotation(C1Point(C1Nam0+C1Travel(c1NamTravel,n)),NamFacing);if(approvedC1==null)TickPack(n);return true;}
            EndPack();WalkOn(0,C1Point(C1Nam0+C1Travel(c1NamTravel,c1Nam.length)),n-c1Nam.length,C1NamWalk);return true;
        }
        void BeginMisstep()
        {
            c1Start=clock;c1HungFrom=Passengers[2].position;poses[2].Play(approvedC1!=null?stand[2]:c1Hung,true);poses[2].Rate(approvedC1!=null?0:1);
            poses[0].Play(approvedC1!=null?stand[0]:c1Nam);poses[0].Rate(approvedC1!=null?0:1);StartApprovedVisual(0);StartApprovedVisual(2);
        }
        /// <summary>The walk on from where the scene left him to his place on the bank (the usual shore walk, from there).</summary>
        void WalkOn(int index,Vector3 start,float elapsed,float seconds)
        {
            RestoreApprovedVisual(index);
            var passenger=Passengers[index];var pose=poses[index];
            pose.Play(walk[index]);float p=Mathf.Clamp01(elapsed/seconds);
            pose.Rate(Vector3.Distance(start,SpawnPoints[index])/seconds*walk[index].length/WalkStride);
            passenger.position=Vector3.Lerp(start,SpawnPoints[index],p);
            var way=Vector3.ProjectOnPlane(SpawnPoints[index]-start,Vector3.up);
            var from=index==2?HungFacing:NamFacing;
            passenger.rotation=Quaternion.Slerp(from,Quaternion.LookRotation(way),Mathf.SmoothStep(0,1,elapsed/.6f));
            if(Physics.Raycast(passenger.position+Vector3.up*2,Vector3.down,out var hit,5,~0,QueryTriggerInteraction.Ignore)&&!hit.transform.IsChildOf(transform)&&hit.point.y>.5f)
                passenger.position=new Vector3(passenger.position.x,hit.point.y,passenger.position.z);
            if(p>=1){pose.Play(Resources.Load<AnimationClip>("Cutscenes/Map02/"+(index==2?"Hung":"Nam")+"_Standing_Guard"));GroundSkin(actors[index],SpawnPoints[index].y);ashore[index]=true;}
        }
        /// <summary>True while the C1 takes own this actor's feet (their contacts are authored; no stair or shore planting).</summary>
        bool MisstepOwnsFeet(int index)
        {
            if(!Misstep||index==1)return false;float t=clock-index*PassengerDelay;
            if(index==2)return c1Start>=0&&clock-c1Start<c1Hung.length;
            return t>=StandDuration+StairDuration+C1NamToSpot&&(c1Start<0||clock-c1Start<c1Nam.length);
        }
        /// <summary>The landing as it happens: side on from the bank, both of them in frame. It comes in as Hùng starts up the
        /// stairs and stays until the village reveal takes the camera on from it.</summary>
        bool MisstepCamera(out Vector3 eye,out Vector3 focus,out float weight)
        {
            eye=focus=Vector3.zero;weight=0;if(!Misstep)return false;
            float hungStair=2*PassengerDelay+StandDuration+LaneDuration;
            weight=Mathf.SmoothStep(0,1,(clock-hungStair+1)/1.5f);if(weight<=0)return false;
            // Shoot from the other side of Thành's shore lane, so he cannot walk through the lens. Follow the pair's
            // horizontal travel as they rise/walk on; a fixed eye here would cut their heads off and then meet them.
            focus=C1Point(new Vector3(.3f,-.4f,.65f));eye=C1Point(new Vector3(2.5f,-3.2f,1.75f));
            if(c1Start>=0){var travel=Vector3.ProjectOnPlane((Passengers[0].position+Passengers[2].position)*.5f-(C1Point(C1Nam0)+C1Point(C1Hung0))*.5f,Vector3.up);focus+=travel;eye+=travel;}
            // Nam stays to pick the pack up while Hùng starts walking. Pull back enough for their current span and
            // their hands/helmets, including the letterbox-safe height, instead of letting the first walker leave frame.
            var offset=eye-focus;float distance=offset.magnitude;var view=Quaternion.Inverse(Quaternion.LookRotation(-offset));
            float tan=Mathf.Tan(48*.5f*Mathf.Deg2Rad)*.8f,aspect=camera!=null?camera.aspect:16f/9f;
            foreach(int index in new[]{0,2})foreach(var bone in new[]{HumanBodyBones.Head,HumanBodyBones.Hips,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,HumanBodyBones.LeftHand,HumanBodyBones.RightHand})
            {
                var point=MisstepActor(index).GetBoneTransform(bone).position+(bone==HumanBodyBones.Head?Vector3.up*.3f:Vector3.zero);var p=view*(point-focus);
                distance=Mathf.Max(distance,Mathf.Abs(p.x)/(tan*aspect)-p.z,Mathf.Abs(p.y)/tan-p.z);
            }
            eye=focus+offset.normalized*distance;
            return true;
        }
        static float Ease(float t,float a,float b)=>Mathf.SmoothStep(0,1,(t-a)/(b-a));
        /// <summary>Unity's humanoid retarget leaves the hands some centimetres off where the takes put them; the contacts are
        /// set again here, at the takes' own times: his left hand on the boards and then on Nam's forearm, Nam's hands on his
        /// left arm, then on his shoulder.</summary>
        void MisstepContacts()
        {
            // Approved v4 includes its contacts and twists. Do not add the old Humanoid IK correction on top.
            if(approvedC1!=null)return;
            if(!Misstep||c1Start<0)return;float c=clock-c1Start;var hung=actors[2];var nam=actors[0];
            Vector3 B(Animator a,HumanBodyBones b)=>a.GetBoneTransform(b).position;
            if(c<c1Hung.length)
            {
                var boards=C1Point(new Vector3(.22f,-.78f,.03f))+Vector3.up*.05f;
                float plant=Ease(c,.55f,.82f)*(1-Ease(c,3.25f,3.35f));
                float brace=Ease(c,.55f,.85f)*(1-Ease(c,3.3f,4f));
                FitMisstepTorso(hung,true,boards,brace,true);
                C1Reach(hung,true,boards,plant);
                C1Reach(hung,true,Vector3.Lerp(B(nam,HumanBodyBones.RightLowerArm),B(nam,HumanBodyBones.RightHand),.55f)+Vector3.up*.04f,Ease(c,3.3f,3.6f)*(1-Ease(c,4.6f,4.9f)));
            }
            if(c<c1Nam.length)
            {
                var up=B(hung,HumanBodyBones.LeftUpperArm);var lo=B(hung,HumanBodyBones.LeftLowerArm);float hold=Ease(c,2.2f,2.75f)*(1-Ease(c,4.65f,4.95f));
                var support=Vector3.Lerp(up,lo,.6f)+Vector3.up*.03f;
                // The authored assisted rise takes over the torso after the low supporting kneel. Release this extra
                // incline ahead of it instead of making an upright shoulder keep chasing the old low arm contact.
                FitMisstepTorso(nam,false,support,hold*(1-Ease(c,3.05f,3.55f)),false);
                C1Reach(nam,false,support,hold);
                C1Reach(nam,true,Vector3.Lerp(up,B(hung,HumanBodyBones.LeftShoulder),.2f)-Vector3.up*.07f,hold*Ease(c,2.4f,2.9f));
                C1Reach(nam,true,Vector3.Lerp(B(hung,HumanBodyBones.LeftShoulder),B(hung,HumanBodyBones.Neck),.25f)+Vector3.up*.07f,Ease(c,4.75f,5.05f)*(1-Ease(c,5.45f,5.75f)));
            }
        }
        /// <summary>The retargeted kneel leaves his chest upright: the authored board contact can be farther from his
        /// shoulder than the whole arm. Incline the existing spine chain toward the supporting hand before solving that
        /// hand. Use the smallest extra bend that makes the target reachable, without stretching the arm; the supporting
        /// kneel keeps its authored ankle contacts and knee heights.</summary>
        static void FitMisstepTorso(Animator body,bool left,Vector3 target,float weight,bool lowerPelvis)
        {
            if(weight<=0)return;
            var upper=body.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);var lower=body.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);var hand=body.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            float reach=(Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,hand.position))*.98f;
            if(Vector3.Distance(upper.position,target)<=reach)return;
            var hips=body.GetBoneTransform(HumanBodyBones.Hips);
            if(lowerPelvis)
            {
                // Settle back into the kneel instead of forcing the whole correction into his waist. Preserve the
                // take's ankle contacts and each knee's height using the same fixed-length two-bone construction.
                var leftKnee=body.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position;var rightKnee=body.GetBoneTransform(HumanBodyBones.RightLowerLeg).position;
                var leftFoot=body.GetBoneTransform(HumanBodyBones.LeftFoot);var rightFoot=body.GetBoneTransform(HumanBodyBones.RightFoot);
                var lf=leftFoot.position;var rf=rightFoot.position;var lr=leftFoot.rotation;var rr=rightFoot.rotation;
                hips.position-=Vector3.up*.14f*weight;
                KeepMisstepLeg(body,true,lf,leftKnee);KeepMisstepLeg(body,false,rf,rightKnee);
                leftFoot.rotation=lr;rightFoot.rotation=rr;
            }
            var axis=Vector3.Cross(Vector3.up,Vector3.ProjectOnPlane(target-hips.position,Vector3.up)).normalized;
            if(axis.sqrMagnitude<.001f)return;
            var spine=body.GetBoneTransform(HumanBodyBones.Spine);var chest=body.GetBoneTransform(HumanBodyBones.Chest);var top=body.GetBoneTransform(HumanBodyBones.UpperChest);
            if(spine==null)return;
            var q0=spine.localRotation;var q1=chest!=null?chest.localRotation:Quaternion.identity;var q2=top!=null?top.localRotation:Quaternion.identity;
            void Bend(float angle)
            {
                spine.localRotation=q0;if(chest!=null)chest.localRotation=q1;if(top!=null)top.localRotation=q2;
                float total=.65f+(chest!=null?.25f:0)+(top!=null?.1f:0);
                spine.rotation=Quaternion.AngleAxis(angle*.65f/total,axis)*spine.rotation;
                if(chest!=null)chest.rotation=Quaternion.AngleAxis(angle*.25f/total,axis)*chest.rotation;
                if(top!=null)top.rotation=Quaternion.AngleAxis(angle*.1f/total,axis)*top.rotation;
            }
            // The distance is not monotonic through a full bow. A target beside the upper chest may be reachable at
            // 10 degrees but farther away again at 80. Find the first reachable interval, or the closest feasible bend;
            // forcing the maximum when the far endpoint misses would snap Nam's spine during the assisted rise.
            float lo=0,hi=0,best=0,bestDistance=Vector3.Distance(upper.position,target);bool found=false;
            for(float angle=5;angle<=80;angle+=5)
            {
                Bend(angle);float distance=Vector3.Distance(upper.position,target);
                if(distance<bestDistance){bestDistance=distance;best=angle;}
                if(distance<=reach){lo=angle-5;hi=angle;found=true;break;}
            }
            if(found)for(int i=0;i<8;i++){float mid=(lo+hi)*.5f;Bend(mid);if(Vector3.Distance(upper.position,target)>reach)lo=mid;else hi=mid;}
            Bend((found?hi:best)*weight);
        }
        static void KeepMisstepLeg(Animator body,bool left,Vector3 target,Vector3 kneeHint)
        {
            var upper=body.GetBoneTransform(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);var knee=body.GetBoneTransform(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);var foot=body.GetBoneTransform(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
            var origin=upper.position;float l1=Vector3.Distance(origin,knee.position),l2=Vector3.Distance(knee.position,foot.position);
            var axis=(target-origin).normalized;float d=Mathf.Clamp(Vector3.Distance(origin,target),Mathf.Abs(l1-l2)+.001f,(l1+l2)*.995f);
            float along=(l1*l1-l2*l2+d*d)/(2*d),height=Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));var centre=origin+axis*along;
            var bend=Vector3.ProjectOnPlane(kneeHint-centre,axis).normalized;var up=Vector3.ProjectOnPlane(Vector3.up,axis);
            if(height>.0001f&&up.magnitude>.0001f)
            {
                float y=(kneeHint.y-centre.y)/(height*up.magnitude);
                if(Mathf.Abs(y)<=1)
                {
                    up.Normalize();var across=Vector3.Cross(axis,up);float side=Mathf.Sqrt(Mathf.Max(0,1-y*y));
                    var a=up*y+across*side;var b=up*y-across*side;bend=Vector3.Dot(a,bend)>=Vector3.Dot(b,bend)?a:b;
                }
            }
            var wanted=centre+bend*height;
            upper.rotation=Quaternion.FromToRotation(knee.position-origin,wanted-origin)*upper.rotation;
            knee.rotation=Quaternion.FromToRotation(foot.position-knee.position,origin+axis*d-knee.position)*knee.rotation;
        }
        float RevealStart=>Misstep?(c1Start>=0?c1Start+c1Nam.length-.5f:float.MaxValue):7;
        // One sling follows Nam's current body through the model swap.
        void SlingRifle()=>Map01BoatCarry.For(actors[0]).Stow();
        void EndMisstep(){EndPack();RestoreApprovedVisual(0);RestoreApprovedVisual(2);}
        // ---- the pack: Nam's own (baked from his skinned pack), on his back, in his hand, on the ground and back on ------
        void TickPack(float n)
        {
            var f=c1Track.pack[Mathf.Clamp(Mathf.RoundToInt(n*c1Track.fps),0,c1Track.pack.Length-1)];
            if(f.mode=="worn"&&c1Pack==null&&approvedC1==null)return;
            if(c1Pack==null&&!BakePack())return;
            var nam=MisstepActor(0);var hand=nam.GetBoneTransform(HumanBodyBones.RightHand).position;
            var chestBasis=C1ChestBasis(nam);var wornAt=c1Chest.position+chestBasis*c1PackInChest;var wornRot=chestBasis*c1PackRotInChest;
            if(approvedC1!=null&&f.mode=="worn"){c1PackWorn=true;c1Pack.SetPositionAndRotation(wornAt,wornRot);return;}
            c1PackWorn=false;
            var handAt=C1Point(C1Vector(f.pos,0))+(hand-C1Point(C1Vector(f.hand,0)));
            var handRot=C1Rotation(f.rot)*Quaternion.Inverse(c1Blender0)*c1PackRot0;
            Vector3 at;Quaternion rot;
            if(f.mode=="ground")
            {
                if(!c1Grounded){c1Grounded=true;c1GroundAt=handAt;c1GroundRot=handRot;
                    c1Pack.SetPositionAndRotation(handAt,handRot);var hit=WalkSurfaceBelow(handAt);
                    if(hit.collider!=null)c1GroundAt.y+=hit.point.y+.003f-c1Pack.GetComponentInChildren<Renderer>().bounds.min.y;}
                at=c1GroundAt;rot=c1GroundRot;if(!c1SetDown){c1SetDown=true;Thud(at,.3f);}
            }
            else if(n<1.2f){float k=Mathf.SmoothStep(0,1,(n-.95f)/.25f);at=Vector3.Lerp(wornAt,handAt,k);rot=Quaternion.Slerp(wornRot,handRot,k);}
            else if(n>=7.4f){float k=Mathf.SmoothStep(0,1,(n-7.4f)/.6f);at=Vector3.Lerp(handAt,wornAt,k);rot=Quaternion.Slerp(handRot,wornRot,k);}
            else{at=handAt;rot=handRot;if(c1Grounded&&n<6.75f){float k=Mathf.SmoothStep(0,1,(n-6.5f)/.25f);at=Vector3.Lerp(c1GroundAt,handAt,k);rot=Quaternion.Slerp(c1GroundRot,handRot,k);}}
            if(f.mode=="worn"&&n>=8f){EndPack();return;}
            c1Pack.SetPositionAndRotation(at,rot);
        }
        bool BakePack()
        {
            c1WornPack=Passengers[0].GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r=>r.name=="Nam_BaloRanRi");
            var originalChest=actors[0].GetBoneTransform(HumanBodyBones.UpperChest)??actors[0].GetBoneTransform(HumanBodyBones.Chest);
            var visual=MisstepActor(0);c1Chest=visual.GetBoneTransform(HumanBodyBones.UpperChest)??visual.GetBoneTransform(HumanBodyBones.Chest);
            if(c1WornPack==null||c1Chest==null||originalChest==null){c1Track=null;return false;}
            var mesh=BakeC1Prop(c1WornPack,out var centre,approvedC1?.backpackMesh);boatMeshes.Add(mesh);
            c1Pack=new GameObject("Nam's pack — set down at the landing").transform;c1Pack.SetParent(transform.parent,true);
            c1Pack.SetPositionAndRotation(centre,c1WornPack.transform.rotation);
            var art=new GameObject("Mesh");art.transform.SetParent(c1Pack,false);
            art.AddComponent<MeshFilter>().sharedMesh=mesh;art.AddComponent<MeshRenderer>().sharedMaterials=c1WornPack.sharedMaterials;
            // The gameplay and approved rigs have different chest bone rolls. Transfer the pack in an
            // anatomical frame (shoulders/head), not the raw bone's axes or import scale.
            var originalBasis=C1ChestBasis(actors[0]);c1PackInChest=Quaternion.Inverse(originalBasis)*(centre-originalChest.position);c1PackRotInChest=Quaternion.Inverse(originalBasis)*c1Pack.rotation;
            c1PackRot0=c1Pack.rotation;c1Blender0=C1Rotation(c1Track.pack[0].rot);
            c1WornPack.enabled=false;return true;
        }
        // Snapshot in world units directly from skin weights/bind poses. This avoids the imported
        // renderer's 100x local scale and 0.01 parent introducing a different pivot/size in BakeMesh.
        public static Mesh BakeC1Prop(SkinnedMeshRenderer skin,out Vector3 centre,Mesh readable=null)
        {
            var source=readable!=null?readable:skin.sharedMesh;
            if(!source.isReadable)throw new System.InvalidOperationException("C1 backpack CPU source is missing. Run Map02ArrivalC1Import.BuildReadablePack.");
            var mesh=Instantiate(source);mesh.name="Nam pack (carried)";
            var vertices=source.vertices;var bind=source.bindposes;var matrices=new Matrix4x4[skin.bones.Length];
            for(int i=0;i<matrices.Length;i++)matrices[i]=skin.bones[i].localToWorldMatrix*bind[i];
            using var counts=source.GetBonesPerVertex();using var weights=source.GetAllBoneWeights();int cursor=0;
            var bounds=new Bounds();
            for(int i=0;i<vertices.Length;i++)
            {
                Vector3 world=Vector3.zero;for(int j=0;j<counts[i];j++){var w=weights[cursor++];world+=matrices[w.boneIndex].MultiplyPoint3x4(vertices[i])*w.weight;}
                vertices[i]=world;if(i==0)bounds=new Bounds(world,Vector3.zero);else bounds.Encapsulate(world);
            }
            centre=bounds.center;var inverse=Quaternion.Inverse(skin.transform.rotation);
            for(int i=0;i<vertices.Length;i++)vertices[i]=inverse*(vertices[i]-centre);
            mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
        void EndPack(){if(c1Pack!=null)Destroy(c1Pack.gameObject);c1Pack=null;c1PackWorn=false;if(c1WornPack!=null)c1WornPack.enabled=true;}
        void Thud(Vector3 at,float volume){var clip=Resources.Load<AudioClip>("Map02DayOne/crate_place");if(clip!=null)AudioSource.PlayClipAtPoint(clip,at,volume);}
    }
}
