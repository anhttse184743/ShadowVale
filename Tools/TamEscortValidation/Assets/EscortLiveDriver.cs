using UnityEngine;
using ShadowVale.Gameplay.Player;

[DefaultExecutionOrder(9000)]
public sealed class EscortLiveDriver:MonoBehaviour
{
    public Transform player,namKnee,tamKnee,tamArm;
    public PlayerController motor;
    public int frames;public bool done;public string error;
    public float namMotion,tamMotion,travel;
    Quaternion namStart,tamStart;Vector3 origin;
    void Start(){origin=player.position;}
    void Update()
    {
        if(done)return;frames++;
        if(frames>20&&frames<=100)player.position+=Vector3.forward*motor.WalkSpeed*Time.deltaTime;
        // Simulate the earlier animation layers writing a conflicting pose every frame.
        tamArm.localRotation=Quaternion.identity;
    }
    void LateUpdate()
    {
        if(done)return;
        if(frames==20){namStart=namKnee.localRotation;tamStart=tamKnee.localRotation;}
        if(frames>20&&frames<=100){namMotion=Mathf.Max(namMotion,Quaternion.Angle(namStart,namKnee.localRotation));tamMotion=Mathf.Max(tamMotion,Quaternion.Angle(tamStart,tamKnee.localRotation));}
        if(frames>=110)
        {
            travel=Vector3.Distance(origin,player.position);
            if(namMotion<10||tamMotion<10||travel<1)error="Gait did not advance: Nam="+namMotion+" Tam="+tamMotion+" distance="+travel;
            if(Quaternion.Angle(tamArm.localRotation,Quaternion.identity)<10)error="Earlier animation layer replaced the approved shoulder pose.";
            done=true;
        }
    }
}
