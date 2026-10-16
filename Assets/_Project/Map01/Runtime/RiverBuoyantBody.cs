using UnityEngine;
namespace ShadowVale.Map01
{
    /// <summary>Four-point displaced-volume buoyancy, water drag and an optional soft horizontal mooring.</summary>
    [RequireComponent(typeof(Rigidbody)), DisallowMultipleComponent]
    public sealed class RiverBuoyantBody : MonoBehaviour
    {
        public RiverWater river;
        public Vector3[] floatPoints = new Vector3[0];
        [Min(.01f)] public float immersionDepth=.45f;
        [Min(.0001f)] public float displacedVolume=.18f;
        [Min(0)] public float waterDrag=2.4f;
        [Min(0)] public float verticalDamping=2.8f;
        public bool moored;
        public Vector3 mooringPoint;
        public float mooringStiffness=1.6f;
        Rigidbody body;
        float rippleTimer;
        void Awake(){body=GetComponent<Rigidbody>();}
        public void ConfigureFromCollider(RiverWater water,Collider shape) {
            river=water;body=GetComponent<Rigidbody>();var b=shape.bounds;float x=b.extents.x*.62f,z=b.extents.z*.62f;
            floatPoints=new Vector3[4];int k=0;foreach(float sx in new[]{-1f,1f})foreach(float sz in new[]{-1f,1f})floatPoints[k++]=transform.InverseTransformPoint(new Vector3(b.center.x+sx*x,b.min.y,b.center.z+sz*z));
            immersionDepth=Mathf.Max(.12f,b.size.y);displacedVolume=Mathf.Max(.001f,b.size.x*b.size.y*b.size.z*.65f);
        }
        void FixedUpdate(){Step(Time.fixedDeltaTime,Time.time);}
        public void Step(float dt,float time) {
            if(body==null)body=GetComponent<Rigidbody>();if(body.isKinematic||river==null||floatPoints.Length==0)return;
            int wet=0;Vector3 averageCurrent=Vector3.zero;float totalSubmersion=0;
            foreach(var local in floatPoints){var p=transform.TransformPoint(local);if(!river.TrySample(p,time,out var water))continue;float sub=Mathf.Clamp01((water.height-p.y)/Mathf.Max(.01f,immersionDepth));if(sub<=0)continue;
                float gravity=Mathf.Abs(Physics.gravity.y);float up=river.density*gravity*displacedVolume*sub/floatPoints.Length;
                up-=body.GetPointVelocity(p).y*verticalDamping*body.mass*sub/floatPoints.Length;
                body.AddForceAtPosition(Vector3.up*up,p,ForceMode.Force);averageCurrent+=water.velocity;wet++;totalSubmersion+=sub;
            }
            if(wet==0)return;
            float fraction=totalSubmersion/floatPoints.Length;averageCurrent/=wet;
            var relative=body.linearVelocity-averageCurrent;relative.y=0;
            body.AddForce(-relative*waterDrag*fraction,ForceMode.Acceleration);
            body.AddTorque(-body.angularVelocity*waterDrag*fraction,ForceMode.Acceleration);
            if(moored){var delta=mooringPoint-body.position;delta.y=0;var velocity=body.linearVelocity;velocity.y=0;body.AddForce(delta*mooringStiffness-velocity*2.2f,ForceMode.Acceleration);}
            rippleTimer-=dt;if(rippleTimer<=0&&body.linearVelocity.sqrMagnitude>.015f){river.EmitRipple(body.worldCenterOfMass,Mathf.Clamp01(body.linearVelocity.magnitude*.7f));rippleTimer=.45f;}
        }
    }
}
