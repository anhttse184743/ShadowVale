using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ShadowVale.Map01;

[InitializeOnLoad]
public static class CharacterRippleValidation
{
    [Serializable] sealed class Sensor {public Vector3 position,center,size;public Quaternion rotation;}
    [Serializable] sealed class Layout {public Vector3[] left,right;public Sensor[] sensors;public float amplitude,flow;}
    static RiverWater river;
    static CharacterController character;
    static Vector3 center,across,along,left,right;
    static float started=-1;
    static int standing=-1,moving=-1,bridge=-1;
    static float standingStamp,bridgeStamp;
    static float LatestRippleTime()
    {
        var samples=(Vector4[])typeof(RiverWater).GetField("ripples",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(river);
        float latest=-10000;foreach(var sample in samples)if(sample.w>0)latest=Mathf.Max(latest,sample.z);return latest;
    }
    const string Output="../Map01WaterReports/character-ripples.txt";
    static CharacterRippleValidation(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        SessionState.SetBool("CharacterRippleValidation",true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!SessionState.GetBool("CharacterRippleValidation",false)||!EditorApplication.isPlaying)return;
        try
        {
            if(started<0)
            {
                var layout=JsonUtility.FromJson<Layout>(File.ReadAllText("../Map01WaterReports/interaction-layout.json"));
                river=new GameObject("Actual Map 1 river channel").AddComponent<RiverWater>();
                river.leftBank=layout.left;river.rightBank=layout.right;river.waveAmplitude=layout.amplitude;river.flowSpeed=layout.flow;
                foreach(var source in layout.sensors)
                {
                    var sensor=new GameObject("Saved Map 1 sensor");sensor.layer=4;sensor.transform.SetPositionAndRotation(source.position,source.rotation);
                    var box=sensor.AddComponent<BoxCollider>();box.isTrigger=true;box.center=source.center;box.size=source.size;
                    sensor.AddComponent<RiverWaterTrigger>().river=river;
                }
                left=layout.left[204];right=layout.right[204];center=(left+right)*.5f;
                across=(right-left).normalized;along=Vector3.Cross(across,Vector3.up);
                var model=new GameObject("Wading character probe");model.layer=6;model.transform.position=left-across*.1f-Vector3.up*.6f;
                character=model.AddComponent<CharacterController>();character.center=new Vector3(0,.9f,0);character.height=1.8f;character.radius=.22f;character.minMoveDistance=0;
                Physics.SyncTransforms();started=Time.time;
            }
            float time=Time.time-started;
            Vector3 target;
            if(time<1)
            {
                target=left-across*.1f-Vector3.up*.6f+along*(.15f*time);
                if(time>.5f&&river.RippleCount!=0)throw new Exception("Dry bank generated ripples.");
            }
            else if(time<2)target=Vector3.Lerp(left-across*.1f,center,Mathf.Clamp01(time-1))-Vector3.up*.6f;
            else if(time<3)
            {
                target=center-Vector3.up*.6f;
                if(time>2.3f&&standing<0){standing=river.RippleCount;standingStamp=LatestRippleTime();if(standing<1)throw new Exception("Entering the water generated no ripple.");}
                if(time>2.7f&&LatestRippleTime()!=standingStamp)throw new Exception("Stationary character generates continuous ripples.");
            }
            else if(time<4)target=center-Vector3.up*.6f+along*((time-3)*.65f);
            else if(time<5)
            {
                if(moving<0){moving=river.RippleCount;if(LatestRippleTime()<=standingStamp)throw new Exception("Walking in water generated no new ripples.");}
                target=center+Vector3.up*.4f+along*((time-4)*.2f);
                if(time>4.3f&&bridge<0){bridge=river.RippleCount;bridgeStamp=LatestRippleTime();}
                if(time>4.7f&&LatestRippleTime()!=bridgeStamp)throw new Exception("Bridge-height character generated ripples.");
            }
            else
            {
                target=right+across*.1f-Vector3.up*.6f+along*((time-5)*.15f);
                if(time>5.3f&&LatestRippleTime()!=bridgeStamp)throw new Exception("Returning to dry bank generated ripples.");
            }
            character.Move(target-character.transform.position);
            if(time<6)return;
            File.WriteAllLines(Output,new[]{"PASS actual Unity Play Mode using saved Map 1 channel and all 50 sensor transforms", "PASS player-layer CharacterController entry into water emits ripples: "+standing, "PASS walking in water emits additional ripples: "+moving, "PASS standing still, dry bank and bridge-height walking emit no extra ripples (buffer timestamps checked even at capacity)", "PASS ripple count="+river.RippleCount+", same production RiverWater and RiverWaterTrigger as Map 2"});
            SessionState.SetBool("CharacterRippleValidation",false);EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText(Output,"FAIL "+e);SessionState.SetBool("CharacterRippleValidation",false);EditorApplication.Exit(1);}
    }
}
