using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace ShadowVale.Map01.Editor
{
 [InitializeOnLoad] public static class Map01LandmarkCheck
 {
  static int phase;static float began,slideX;static List<string> log=new List<string>();
  static Map01LandmarkCheck(){EditorApplication.update+=Poll;EditorApplication.playModeStateChanged+=State;}
  static void Poll(){if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("Tools/Map01LandmarkCheck.request"))return;File.Delete("Tools/Map01LandmarkCheck.request");SessionState.SetBool("SV.LandmarkCheck",true);EditorApplication.isPlaying=true;}
  static void State(PlayModeStateChange state){if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("SV.LandmarkCheck",false))return;phase=0;began=Time.time;log.Clear();Application.runInBackground=true;EditorApplication.update+=Tick;}
  static void Tick()
  {
   if(!EditorApplication.isPlaying)return;
   try
   {
    var m=UnityEngine.Object.FindFirstObjectByType<ForestMission>();if(m==null||!m.IsInitialized)throw new Exception("Mission not initialized");var cc=m.player.GetComponent<CharacterController>();
    if(phase==0){if(Time.time-began<1)return;if(Vector2.Distance(new Vector2(m.player.position.x,m.player.position.z),new Vector2(-83,-80.3f))>1||!cc.isGrounded)throw new Exception("Bunker spawn is not grounded at its authored position");log.Add("PASS bunker spawn grounded");InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.W));began=Time.time;phase=1;return;}
    if(phase==1){if(Time.time-began<5)return;InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());if(m.player.position.z<-72)throw new Exception("Player blocked leaving bunker: "+m.player.position);log.Add("PASS W moves player out of bunker");cc.enabled=false;m.player.position=new Vector3(7.65f,3.35f,0);cc.enabled=true;began=Time.time;phase=2;return;}
    if(phase==2){if(Time.time-began<.8f)return;if(!cc.isGrounded||m.player.position.y<2.9f)throw new Exception("Bridge deck does not support player");began=Time.time;phase=3;return;}
    if(phase==3){cc.Move(new Vector3(0,0,.035f));if(Time.time-began<2)return;if(m.player.position.z>1.45f||m.player.position.z<.8f)throw new Exception("Bridge railing failed collision: "+m.player.position);log.Add("PASS continuous bridge railing blocks CharacterController");slideX=m.player.position.x;began=Time.time;phase=4;return;}
    if(phase==4){cc.Move(new Vector3(.035f,0,.025f));if(Time.time-began<1.5f)return;if(m.player.position.x-slideX<.8f||m.player.position.z>1.45f)throw new Exception("Character sticks or penetrates while sliding along railing");log.Add("PASS wall slide without railing penetration");float z=25;float x=8+12*Mathf.Sin(z*.041f)+4*Mathf.Sin(z*.105f);cc.enabled=false;m.player.position=new Vector3(x,-.65f,z);cc.enabled=true;began=Time.time;phase=5;return;}
    if(Time.time-began<1)return;if(!cc.isGrounded||!m.IsWading||Mathf.Abs(m.MovementSurfaceMultiplier-.68f)>.001f)throw new Exception("Shallow-water slowdown failed");log.Add("PASS wading speed multiplier 0.68 on riverbed");Finish(null);
   }
   catch(Exception e){Finish("FAIL "+e);}
  }
  static void Finish(string error){if(error!=null)log.Add(error);File.WriteAllLines("Tools/Map01OptimizedReports/landmark-playcheck.txt",log);InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());EditorApplication.update-=Tick;SessionState.SetBool("SV.LandmarkCheck",false);EditorApplication.isPlaying=false;}
 }
}
