using UnityEngine;
using ShadowVale.Gameplay.Player;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using System.Reflection;

[DefaultExecutionOrder(9000)]
public sealed class EscortLiveDriver:MonoBehaviour
{
    public Transform player,namKnee,tamKnee,tamArm;
    public PlayerController motor;
    public int frames;public bool done;public string error;
    public float namMotion,tamMotion,travel;
    public float measuredWalk,measuredEscort;
    PlayerController walkProbe,escortProbe;Keyboard keyboard;
    float sampleTime,walkDistance,escortDistance;
    readonly MethodInfo moveTick=typeof(PlayerController).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
    Quaternion namStart,tamStart;Vector3 origin;
    void Start()
    {
        origin=player.position;
        keyboard=InputSystem.AddDevice<Keyboard>();
        var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Controller speed test ground";
        ground.transform.position=new Vector3(15,-.1f,50);ground.transform.localScale=new Vector3(30,.2f,150);
        walkProbe=Probe("Normal walk",10);escortProbe=Probe("Assisted walk",20);
        escortProbe.AssistedWalkSpeed=1.8f;
        Physics.SyncTransforms();
    }
    static PlayerController Probe(string name,float x)
    {
        var obj=new GameObject(name);obj.transform.position=new Vector3(x,1,0);
        var result=obj.AddComponent<PlayerController>();result.enabled=false;return result;
    }
    void Update()
    {
        if(done)return;frames++;
        // Exercise real keyboard input, acceleration and CharacterController.Move, rather than teleporting these probes.
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));InputSystem.Update();
        var beforeWalk=walkProbe.transform.position;var beforeEscort=escortProbe.transform.position;
        moveTick.Invoke(walkProbe,null);moveTick.Invoke(escortProbe,null);
        if(frames>50&&frames<=100)
        {
            sampleTime+=Time.deltaTime;
            walkDistance+=Vector3.ProjectOnPlane(walkProbe.transform.position-beforeWalk,Vector3.up).magnitude;
            escortDistance+=Vector3.ProjectOnPlane(escortProbe.transform.position-beforeEscort,Vector3.up).magnitude;
        }
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
            measuredWalk=walkDistance/sampleTime;measuredEscort=escortDistance/sampleTime;
            if(Mathf.Abs(measuredWalk-walkProbe.WalkSpeed)>.15f||Mathf.Abs(measuredEscort-1.8f)>.15f)
                error="Real controller speed mismatch: walk="+measuredWalk+" escort="+measuredEscort;
            InputSystem.RemoveDevice(keyboard);
            done=true;
        }
    }
}
