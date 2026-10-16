using UnityEngine;

namespace ShadowVale.Map01
{
    public sealed class Map01SupplyProps:MonoBehaviour
    {
        public Transform doorHinge,lid,bag,lockBody,doorLock;
        public Transform secondDoorHinge;
        public float doorOpenAngle=-105,secondDoorOpenAngle=105;
        public Collider doorCollider;
        public UnityEngine.AI.NavMeshObstacle doorObstacle;
        public Renderer lockIndicator;
        public int formatVersion;
        Quaternion doorClosed,secondDoorClosed,lidClosed;bool initialized;
        Vector3 doorAxis,secondDoorAxis,lidAxis;
        Transform bagParent;Vector3 bagPosition;Quaternion bagRotation;
        void Ensure(){if(initialized)return;initialized=true;doorClosed=doorHinge!=null?doorHinge.localRotation:Quaternion.identity;secondDoorClosed=secondDoorHinge!=null?secondDoorHinge.localRotation:Quaternion.identity;lidClosed=lid!=null?lid.localRotation:Quaternion.identity;doorAxis=doorHinge!=null?doorHinge.InverseTransformDirection(transform.up):Vector3.up;secondDoorAxis=secondDoorHinge!=null?secondDoorHinge.InverseTransformDirection(transform.up):Vector3.up;lidAxis=lid!=null?lid.InverseTransformDirection(transform.right):Vector3.right;if(bag!=null){bagParent=bag.parent;bagPosition=bag.localPosition;bagRotation=bag.localRotation;}}
        public void Present(bool doorOpen,bool unlocked,bool collected,bool delivered,Transform player)
        {
            Ensure();if(doorHinge!=null)doorHinge.localRotation=doorClosed*Quaternion.AngleAxis(doorOpen?doorOpenAngle:0,doorAxis);
            if(secondDoorHinge!=null)secondDoorHinge.localRotation=secondDoorClosed*Quaternion.AngleAxis(doorOpen?secondDoorOpenAngle:0,secondDoorAxis);
            if(doorCollider!=null)doorCollider.enabled=!doorOpen;
            if(doorObstacle!=null)doorObstacle.enabled=!doorOpen;
            if(doorLock!=null)doorLock.gameObject.SetActive(!doorOpen);
            if(lockBody!=null)lockBody.gameObject.SetActive(!unlocked);
            OpenLid(collected?1:0);
            if(bag==null)return;bag.gameObject.SetActive(!delivered);
            if(delivered)return;
            if(collected&&player!=null)CarryBag(player.GetComponentInChildren<Animator>());
            else {bag.SetParent(bagParent,false);bag.SetLocalPositionAndRotation(bagPosition,bagRotation);}
        }
        public void OpenLid(float amount){Ensure();if(lid!=null)lid.localRotation=lidClosed*Quaternion.AngleAxis(-98*Mathf.Clamp01(amount),lidAxis);}
        static Transform Bone(Animator actor,HumanBodyBones id,string name)=>actor.isHuman?actor.GetBoneTransform(id):System.Array.Find(actor.GetComponentsInChildren<Transform>(),t=>t.name==name);
        public void HoldBag(Animator actor)
        {
            if(bag==null||actor==null)return;var hand=Bone(actor,HumanBodyBones.RightHand,"RightHand");if(hand==null)return;
            var frame=actor.GetComponentInParent<ShadowVale.Gameplay.Player.PlayerController>()?.transform??actor.transform;
            var middle=Bone(actor,HumanBodyBones.RightMiddleProximal,"RightHandMiddle1");
            if(middle==null)foreach(var node in hand.GetComponentsInChildren<Transform>())if(node.name=="RightHandMiddle1"){middle=node;break;}
            var grip=middle!=null?Vector3.Lerp(hand.position,middle.position,.65f):hand.position;
            // The upper arc of the handle is 43 cm above the bag origin, not its centre.
            bag.gameObject.SetActive(true);bag.SetParent(hand,true);bag.SetPositionAndRotation(grip-Vector3.up*.43f+frame.forward*.015f,Quaternion.LookRotation(frame.forward,Vector3.up));
        }
        public void CarryBag(Animator actor)
        {
            if(bag==null||actor==null)return;Ensure();var spine=Bone(actor,HumanBodyBones.Chest,"Spine1");if(spine==null)return;
            var frame=actor.GetComponentInParent<ShadowVale.Gameplay.Player.PlayerController>()?.transform??actor.transform;
            bag.gameObject.SetActive(true);bag.SetParent(spine,true);bag.SetPositionAndRotation(spine.position+frame.right*.24f-frame.forward*.24f-Vector3.up*.10f,Quaternion.LookRotation(-frame.forward,Vector3.up));
        }
        public void PlaceBag(Vector3 point){if(bag==null)return;bag.SetParent(transform,true);bag.SetPositionAndRotation(point,Quaternion.identity);}
        void OnDestroy(){if(bag!=null&&bag.parent!=null&&!bag.IsChildOf(transform))Destroy(bag.gameObject);}
    }
}
