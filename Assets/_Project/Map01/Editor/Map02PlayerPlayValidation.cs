using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    [InitializeOnLoad]
    public static class Map02PlayerPlayValidation
    {
        const string Flag="ShadowVale.Map02PlayerValidation";
        static float start=-1,groundY,highest;
        static Vector3 original;
        static Keyboard keyboard;
        static int stage,resetCount;
        static Key[] held=Array.Empty<Key>();
        static string runtimeError;
        static Map02PlayerPlayValidation(){InputSystem.onAfterUpdate+=()=>{if(InputState.currentUpdateType==InputUpdateType.Dynamic)Tick();};Application.logMessageReceived+=(text,stack,type)=>{if(SessionState.GetBool(Flag,false)&&(type==LogType.Error||type==LogType.Exception)&&!stack.Contains("UnityEditor.Search."))runtimeError=text;};}
        public static void Run(){SessionState.SetBool(Flag,true);Map02PlayerSetup.Play();}
        static void Tick()
        {
            if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
            try
            {
                if(Time.time<1)return;
                if(runtimeError!=null)throw new Exception("Runtime error: "+runtimeError);
                var rig=Object.FindFirstObjectByType<Map02Exploration>();
                if(rig==null||SceneManager.GetActiveScene().name!="Map 2")throw new Exception("Did not start Map 2 directly");
                if(ForestMenu.Visible)throw new Exception("Menu obscures exploration");
                var player=rig.player;var controller=player.GetComponent<CharacterController>();
                if(start<0){keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;InputSystem.onBeforeUpdate+=FeedInput;start=Time.time;groundY=player.transform.position.y;highest=groundY;original=player.transform.position;if(!controller.isGrounded)throw new Exception("Spawn not grounded");}
                float elapsed=Time.time-start;
                highest=Mathf.Max(highest,player.transform.position.y);
                if(stage==0){held=elapsed<.3f?new[]{Key.Space}:Array.Empty<Key>();if(elapsed<1.5f)return;if(highest-groundY<.5f)throw new Exception("Jump did not work: ground="+groundY+" peak="+highest+" keyboard="+(Keyboard.current==keyboard));stage=1;start=Time.time;original=player.transform.position;}
                if(stage==1){held=new[]{Key.W};if(Time.time-start<2)return;if(Vector3.Distance(player.transform.position,original)<2)throw new Exception("WASD movement blocked");if(player.GetComponentInChildren<Animator>().GetFloat("Speed")<.5f)throw new Exception("Locomotion animation not driven");stage=2;start=Time.time;held=new[]{Key.R};return;}
                if(stage==2){held=Array.Empty<Key>();if(Time.time-start<.4f)return;if(Vector3.Distance(player.transform.position,rig.boatLandingSpawn.position)>.3f)throw new Exception("R did not return to landing");resetCount=rig.ResetCount;controller.enabled=false;player.transform.position=Vector3.down*3;controller.enabled=true;stage=3;start=Time.time;return;}
                if(stage==3){if(Time.time-start<.4f)return;if(rig.ResetCount<=resetCount||Vector3.Distance(player.transform.position,rig.boatLandingSpawn.position)>.3f)throw new Exception("Fall recovery failed");var cameras=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c=>c.enabled).ToArray();if(cameras.Length!=1||Camera.main==null||Vector3.Distance(Camera.main.transform.position,player.transform.position)>17)throw new Exception("Follow camera invalid");if(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(a=>a.enabled)!=1)throw new Exception("Multiple listeners");Capture(Camera.main);File.WriteAllLines("Tools/Map02Reports/player-play-checks.txt",new[]{"PASS actual Unity Play Mode starts Map 2 without title menu","PASS landing spawn grounded, animated player model present","PASS Space jump above 0.5 m and W movement above 2 m","PASS R resets to landing; falling returns safely to landing","PASS one active follow camera and audio listener; no gameplay runtime errors"});Finish(0);}
            }
            catch(Exception e){if(Camera.main!=null)Capture(Camera.main);File.WriteAllText("Tools/Map02Reports/player-play-checks.txt","FAIL "+e);Finish(1);}
        }
        static void FeedInput(){if(keyboard!=null&&InputState.currentUpdateType==InputUpdateType.Dynamic)InputSystem.QueueStateEvent(keyboard,new KeyboardState(held));}
        static void Finish(int code){SessionState.SetBool(Flag,false);InputSystem.onBeforeUpdate-=FeedInput;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);EditorApplication.Exit(code);}
        static void Capture(Camera camera)
        {
            var rt=new RenderTexture(1600,1000,24);var img=new Texture2D(1600,1000,TextureFormat.RGB24,false);var old=RenderTexture.active;var target=camera.targetTexture;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;img.ReadPixels(new Rect(0,0,1600,1000),0,0);img.Apply();File.WriteAllBytes("Tools/Map02Reports/map02-player-boat-spawn.png",img.EncodeToPNG());}
            finally{camera.targetTexture=target;RenderTexture.active=old;Object.DestroyImmediate(rt);Object.DestroyImmediate(img);}
        }
    }
}
