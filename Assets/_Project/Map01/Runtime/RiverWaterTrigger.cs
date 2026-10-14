using System.Collections.Generic;
using UnityEngine;
namespace ShadowVale.Map01
{
    /// <summary>Local broad-phase sensors; the river's exact channel test rejects dry banks and bridge users.</summary>
    public sealed class RiverWaterTrigger : MonoBehaviour
    {
        public RiverWater river;
        readonly Dictionary<int,Vector3> previous=new Dictionary<int,Vector3>();
        readonly Dictionary<int,float> lastRipple=new Dictionary<int,float>();
        void OnTriggerEnter(Collider other){Visit(other,true);}
        void OnTriggerStay(Collider other){Visit(other,false);}
        void OnTriggerExit(Collider other){previous.Remove(other.GetInstanceID());lastRipple.Remove(other.GetInstanceID());}
        void OnDisable(){previous.Clear();lastRipple.Clear();}
        void Visit(Collider other,bool entering){
            if(river==null||other.isTrigger)return;var body=other.attachedRigidbody;var character=other.GetComponentInParent<CharacterController>();
            if(body==null&&character==null)return;
            var bounds=other.bounds;var p=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            if(!river.TrySample(p,river.Clock,out var water)||p.y>water.height+.08f||bounds.max.y<water.height-2)return;
            if(body!=null&&!body.isKinematic){var buoy=body.GetComponent<RiverBuoyantBody>();if(buoy==null){buoy=body.gameObject.AddComponent<RiverBuoyantBody>();buoy.ConfigureFromCollider(river,other);}else if(buoy.river==null)buoy.river=river;}
            int id=other.GetInstanceID();bool moved=previous.TryGetValue(id,out var old)&&Vector3.Distance(old,p)>.045f;
            if((entering||moved)&&(!lastRipple.TryGetValue(id,out var t)||Time.time-t>.25f)){river.EmitRipple(p,entering?.8f:.4f);lastRipple[id]=Time.time;previous[id]=p;}else if(!previous.ContainsKey(id))previous[id]=p;
        }
    }
}
