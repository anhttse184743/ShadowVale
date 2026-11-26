using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The charge in Hùng's arms (decided 26/10/2026): he carries the bundle the way Nam carries a load (Day 1's "Carry" on
    /// the upper body over the walk), along the ditch while Nam covers him (N3_08) and in the run to the tracks (N3_09). At
    /// the tracks he sets it, then steps back and turns to look at Nam, and the blast whites the screen out.
    /// </summary>
    public sealed partial class Map02ChapterDirector
    {
        const float LookBackFrom=PlantEnd+1.2f,CrouchStride=1.1f;   // m/s the crouched walk is authored at
        // Nam's authored crouch clips are also used by Hùng while he carries the charge.
        AnimationClip crouchWalk,crouchIdle;
        Map02UpperBodyPose hungCarry;float hungCarryClock;Vector3 hungLastAt;GameObject heldCharge;
        /// <summary>Hùng has the charge in his arms.</summary>
        public bool HungCarriesCharge=>CoverFireActive&&sceneTask==null||sceneTask?.id=="N3_09"&&sceneClock<RiseEnd;
        public bool ChargeSet=>sceneTask?.id=="N3_09"&&sceneClock>=PlantEnd-.6f&&sceneClock<BlastAt;
        public GameObject HeldCharge=>heldCharge;

        /// <summary>After the animators: the carry over his walk, the bundle at his chest; the bundle on the tracks once set.</summary>
        void HungChargeLate()
        {
            if(!roles.TryGetValue("Hùng",out var hung)||hung==null||hung.visual==null){DropCharge();return;}
            var animator=hung.visual.GetComponentInChildren<Animator>();
            if(HungCarriesCharge&&animator!=null&&animator.isHuman&&DayClip("Carry")!=null)
            {
                float dt=Mathf.Max(Time.deltaTime,.0001f);float pace=Vector3.ProjectOnPlane(hung.transform.position-hungLastAt,Vector3.up).magnitude/dt;
                if(pace>6)pace=0;   // a placement, not a step
                hungLastAt=hung.transform.position;
                hungCarry??=new Map02UpperBodyPose(animator,DayClip("Carry"),true);   // arms round the bundle, the rest crouched
                hungCarryClock+=dt*pace/CrouchStride;
                var lower=pace>.1f?crouchWalk??ordinaryWalk:crouchIdle??namCalmIdle;
                if(lower!=null)hungCarry.Sample(lower,pace>.1f?hungCarryClock:Time.time);
                var chest=animator.GetBoneTransform(HumanBodyBones.Chest);var body=hung.transform;
                Charge().SetActive(true);
                var at=(chest!=null?chest.position-Vector3.up*.2f:body.position+Vector3.up*1.05f)+body.forward*.32f;
                heldCharge.transform.SetPositionAndRotation(at,body.rotation);
                return;
            }
            hungCarry?.Dispose();hungCarry=null;hungLastAt=hung.transform.position;
            if(ChargeSet&&tank!=null)
            {
                // On the track by the road wheels, on Hùng's side of the hull.
                var toTank=Vector3.ProjectOnPlane(tank.transform.position-ChargePoint,Vector3.up).normalized;
                Charge().SetActive(true);heldCharge.transform.SetPositionAndRotation(ChargePoint+toTank*1.1f+Vector3.up*.45f,Quaternion.LookRotation(toTank));
                return;
            }
            if(heldCharge!=null)heldCharge.SetActive(false);
        }
        GameObject Charge()
        {
            if(heldCharge!=null)return heldCharge;
            heldCharge=Layout?.explosivesArt!=null?Instantiate(Layout.explosivesArt):Placeholder("Hùng's demolition charge",new Vector3(.38f,.24f,.26f),new Color(.36f,.28f,.18f),Vector3.zero);
            heldCharge.name="Hùng's demolition charge";heldCharge.transform.SetParent(root.transform,true);
            foreach(var c in heldCharge.GetComponentsInChildren<Collider>())c.enabled=false;
            return heldCharge;
        }
        void DropCharge(){hungCarry?.Dispose();hungCarry=null;if(heldCharge!=null)heldCharge.SetActive(false);}
        /// <summary>Set, he steps back from the tracks towards the bank and turns to Nam.</summary>
        Vector3 LookBackSpot=>Vector3.Lerp(ChargePoint,RetreatPoint,.5f);
    }
}
