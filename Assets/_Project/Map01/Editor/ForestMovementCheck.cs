using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace ShadowVale.Map01.Editor
{
    [InitializeOnLoad] public static class ForestMovementCheck
    {
        static float startY,maxY,startTime;static int phase;static bool airborne;static Vector3 original;
        static ForestMovementCheck(){EditorApplication.update+=Poll;EditorApplication.playModeStateChanged+=State;}
        static void Poll()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Tools/Map01MovementCheck.request"))return;
            File.Delete("Tools/Map01MovementCheck.request");SessionState.SetBool("SV.MovementCheck",true);EditorApplication.isPlaying=true;
        }
        static void State(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("SV.MovementCheck",false))return;
            phase=0;startTime=Time.time;airborne=false;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                var m=UnityEngine.Object.FindFirstObjectByType<ForestMission>();if(m==null||!m.IsInitialized)throw new Exception("Mission initialization failed");
                var cc=m.player.GetComponent<CharacterController>();
                if(phase==0)
                {
                    if(Time.time-startTime<.6f)return;
                    if(!cc.isGrounded)throw new Exception("Player spawn not grounded");
                    original=m.player.position;startY=maxY=original.y;
                    InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Space));phase=1;startTime=Time.time;return;
                }
                if(phase==1)
                {
                    if(Time.time-startTime<.1f)return;
                    InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());phase=2;return;
                }
                if(phase==2)
                {
                    maxY=Mathf.Max(maxY,m.player.position.y);airborne|=!cc.isGrounded;
                    if(Time.time-startTime<1.5f)return;
                    if(!airborne||maxY-startY<.6f||!cc.isGrounded)throw new Exception("Space jump/landing failed; rise="+(maxY-startY));
                    float z=25;float x=8+12*Mathf.Sin(z*.041f)+4*Mathf.Sin(z*.105f);
                    cc.enabled=false;m.player.position=new Vector3(x-10,5,z);cc.enabled=true;startTime=Time.time;phase=3;return;
                }
                if(phase==3)
                {
                    if(Time.time-startTime<1.5f)return;
                    startTime=Time.time;phase=4;return;
                }
                if(phase==4)
                {
                    float z=25;float x=8+12*Mathf.Sin(z*.041f)+4*Mathf.Sin(z*.105f);
                    // Drive the real controller across the bank; runtime gravity remains active.
                    if(m.player.position.x<x-.2f){cc.Move(Vector3.right*.055f);if(Time.time-startTime>15)throw new Exception("Bank collider blocked entry to stream");return;}
                    phase=5;startTime=Time.time;return;
                }
                if(Time.time-startTime<.8f)return;
                if(m.player.position.y>.2f||!cc.isGrounded)throw new Exception("Player did not reach shallow riverbed: y="+m.player.position.y);
                Finish("PASS Space key raises player "+(maxY-startY).ToString("F2")+"m then lands; CharacterController enters shallow river and stands on bed y="+m.player.position.y.ToString("F2"));
            }
            catch(Exception e){Finish("FAIL "+e);}
        }
        static void Finish(string result)
        {
            File.WriteAllText("Tools/Map01OptimizedReports/movement-check.txt",result);EditorApplication.update-=Tick;SessionState.SetBool("SV.MovementCheck",false);InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());EditorApplication.isPlaying=false;
        }
    }
}
