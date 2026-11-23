using UnityEngine;
namespace ShadowVale.Map01
{
    public sealed class Map02NightWatchVisual : MonoBehaviour
    {
        public Transform gateLeaf, bolt, hood;
        public Vector3 seatedBolt, boltRetractDirection = Vector3.right, gateAxis = Vector3.up, hoodAxis = Vector3.up;
        public Quaternion gateRest = Quaternion.identity, hoodRest = Quaternion.identity;
        public Light beam, glow;
        public bool gate;
        bool initialized, closed, latched;
        float lampTarget, lampCurrent, lampVelocity, leafCurrent;
        Map02Mission mission;
        public Vector3 InteractionPosition=>transform.position-transform.forward*(gate?1.1f:.85f);
        public float LeafAngle => leafCurrent;
        public float LampAngle => lampCurrent;
        public bool Settled => !gate || Mathf.Abs(leafCurrent - (closed ? 0 : 58)) < .5f && Vector3.Distance(bolt.localPosition, seatedBolt + boltRetractDirection * (latched ? 0 : .25f)) < .002f;
        public void CaptureReferencePose()
        {
            if(gateLeaf!=null){gateRest=gateLeaf.localRotation;gateAxis=gateLeaf.parent.InverseTransformDirection(transform.up).normalized;seatedBolt=bolt.localPosition;boltRetractDirection=bolt.parent.InverseTransformDirection(Vector3.ProjectOnPlane(gateLeaf.position-bolt.position,transform.up)).normalized;}
            if(hood!=null){hoodRest=hood.localRotation;hoodAxis=hood.parent.InverseTransformDirection(transform.up).normalized;}
        }
        public void SetGate(bool isClosed,bool isLatched){closed=isClosed;latched=isClosed&&isLatched;if(!initialized){initialized=true;Snap();}}
        public void SetLamp(float angle,bool lit){lampTarget=angle;if(!initialized){initialized=true;Snap();}if(beam!=null)beam.enabled=lit;if(glow!=null)glow.enabled=lit;}
        void Snap(){leafCurrent=closed?0:58;lampCurrent=lampTarget;lampVelocity=0;ApplyPose();if(bolt!=null)bolt.localPosition=seatedBolt+boltRetractDirection*(latched?0:.25f);}
        void ApplyPose(){if(gateLeaf!=null)gateLeaf.localRotation=Quaternion.AngleAxis(leafCurrent,gateAxis)*gateRest;if(hood!=null)hood.localRotation=Quaternion.AngleAxis(lampCurrent,hoodAxis)*hoodRest;}
        void Update()
        {
            mission??=FindFirstObjectByType<Map02Mission>();if(mission!=null&&(mission.Paused||ForestMenu.Visible))return;
            if(gateLeaf!=null){leafCurrent=Mathf.MoveTowards(leafCurrent,closed?0:58,135*Time.deltaTime);bool engage=latched&&Mathf.Abs(leafCurrent)<.4f;bolt.localPosition=Vector3.MoveTowards(bolt.localPosition,seatedBolt+boltRetractDirection*(engage?0:.25f),.55f*Time.deltaTime);}
            if(hood!=null)lampCurrent=Mathf.SmoothDampAngle(lampCurrent,lampTarget,ref lampVelocity,.16f,240,Mathf.Min(Time.deltaTime,1f/30));
            ApplyPose();
        }
    }
}
