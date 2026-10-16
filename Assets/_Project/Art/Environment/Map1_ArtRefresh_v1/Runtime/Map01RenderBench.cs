using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using Unity.Profiling;
using ShadowVale.Map01;
using ShadowVale.Gameplay.Player;
namespace ShadowVale.EnvironmentReview
{
    /// <summary>Only attached to a separate local benchmark scene. Never attached to gameplay Map 1.</summary>
    [DefaultExecutionOrder(-30000)]
    public sealed class Map01RenderBench:MonoBehaviour
    {
        public string outputRoot;
        [Serializable] public class ViewResult {public string name;public bool missionStopped,focused,diagnosticNoAI;public int frames,nearbyEnemies,alertedEnemies,enabledAgents,onNavMeshAgents;public float meanMs,p50Ms,p95Ms,p99Ms,averageFps,overBudgetPercent,cpuTimingMs,gpuTimingMs,cpuMainMs,cpuRenderMs,presentWaitMs,drawCalls,triangles;}
        [Serializable] public class Report {public string gpu,cpu,unity,quality,method;public int width,height,vramMB,ramMB;public ViewResult[] views;}
        readonly Vector3[] eyes={new Vector3(25,6,-72),new Vector3(54,10,-23),new Vector3(68,15,34),new Vector3(67,11,59),new Vector3(-18,8,74),new Vector3(24,8,24)};
        readonly Vector3[] targets={new Vector3(39,5,-79),new Vector3(64,8,-13),new Vector3(68,10,62),new Vector3(68,10,67),new Vector3(-3,1.4f,87),new Vector3(34,5,36)};
        readonly string[] names={"C1 warehouse","C2 signal station","C3 arrival","C3 courtyard","B jetty","Forest route"};
        ProfilerRecorder drawRecorder,triangleRecorder;
        void OnDestroy(){drawRecorder.Dispose();triangleRecorder.Dispose();}
        void Awake()
        {
            Directory.CreateDirectory(outputRoot);
            typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.Static|BindingFlags.NonPublic)?.SetValue(null,Path.Combine(outputRoot,"isolated-saves"));
            Map01OpeningCutscene.CancelPending();QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
            Application.runInBackground=true;Screen.SetResolution(1920,1080,FullScreenMode.FullScreenWindow);
        }
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(4);
            var mission=FindFirstObjectByType<Map01Mission>();if(mission==null||!mission.IsInitialized)throw new Exception("Benchmark mission did not initialize.");
            var camera=mission.gameCamera;var rig=camera.GetComponent<ThirdPersonCamera>();if(rig!=null)rig.enabled=false;
            mission.ModernHealth.CinematicInvulnerable=true;mission.ModernPlayer.InputAllowed=()=>false;mission.ModernCombat.InputAllowed=()=>false;
            mission.GetComponent<Map01Rescue>().MarkIntroSeen();
            foreach(var health in mission.hung.GetComponentsInChildren<ShadowVale.Gameplay.Combat.Health>())health.CinematicInvulnerable=true;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;var results=new List<ViewResult>();
            drawRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count");triangleRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count");
            for(int n=0;n<eyes.Length+2;n++)
            {
                int viewIndex=n<eyes.Length?n:3;string viewName=n<eyes.Length?names[n]:n==eyes.Length?"C3 courtyard repeat":"C3 courtyard diagnostic AI paused";
                bool diagnosticNoAI=n==eyes.Length+1;
                if(diagnosticNoAI)
                {
                    foreach(var enemy in mission.Enemies)enemy.enabled=false;
                    foreach(var agent in FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))agent.enabled=false;
                }
                if(NavMesh.SamplePosition(targets[viewIndex],out var stand,5,NavMesh.AllAreas))
                {var controller=mission.player.GetComponent<CharacterController>();controller.enabled=false;mission.player.position=stand.position;controller.enabled=true;mission.ModernPlayer.RestoreMotion(true);Physics.SyncTransforms();}
                camera.transform.SetPositionAndRotation(eyes[viewIndex],Quaternion.LookRotation(targets[viewIndex]-eyes[viewIndex]));camera.fieldOfView=60;
                File.WriteAllText(Path.Combine(outputRoot,"status.txt"),"RUNNING "+viewName);
                yield return new WaitForSecondsRealtime(3);
                var durations=new List<float>();var cpu=new List<float>();var gpu=new List<float>();var main=new List<float>();var render=new List<float>();var present=new List<float>();var draws=new List<float>();var tris=new List<float>();var timings=new FrameTiming[1];double until=Time.realtimeSinceStartupAsDouble+8;
                while(Time.realtimeSinceStartupAsDouble<until){FrameTimingManager.CaptureFrameTimings();yield return null;durations.Add(Time.unscaledDeltaTime*1000);if(FrameTimingManager.GetLatestTimings(1,timings)>0){if(timings[0].cpuFrameTime>0)cpu.Add((float)timings[0].cpuFrameTime);if(timings[0].gpuFrameTime>0)gpu.Add((float)timings[0].gpuFrameTime);main.Add((float)timings[0].cpuMainThreadFrameTime);render.Add((float)timings[0].cpuRenderThreadFrameTime);present.Add((float)timings[0].cpuMainThreadPresentWaitTime);}if(drawRecorder.Valid)draws.Add(drawRecorder.LastValue);if(triangleRecorder.Valid)tris.Add(triangleRecorder.LastValue);}
                durations.Sort();float Percentile(float p)=>durations[Mathf.Min(durations.Count-1,Mathf.FloorToInt((durations.Count-1)*p))];
                var r=new ViewResult{name=viewName,diagnosticNoAI=diagnosticNoAI,missionStopped=mission.Stopped,frames=durations.Count,nearbyEnemies=mission.Enemies.Count(e=>e.Alive&&Vector3.Distance(e.transform.position,mission.player.position)<45),alertedEnemies=mission.Enemies.Count(e=>e.Alive&&e.Alerted),meanMs=durations.Average(),p50Ms=Percentile(.5f),p95Ms=Percentile(.95f),p99Ms=Percentile(.99f),overBudgetPercent=100f*durations.Count(t=>t>16.667f)/durations.Count};r.averageFps=1000/r.meanMs;results.Add(r);
                var agents=FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None).Where(a=>a.enabled).ToArray();r.enabledAgents=agents.Length;r.onNavMeshAgents=agents.Count(a=>a.isOnNavMesh);r.focused=Application.isFocused;r.cpuTimingMs=cpu.Count>0?cpu.Average():-1;r.gpuTimingMs=gpu.Count>0?gpu.Average():-1;r.drawCalls=draws.Count>0?draws.Average():-1;r.triangles=tris.Count>0?tris.Average():-1;
                r.cpuMainMs=main.Count>0?main.Average():-1;r.cpuRenderMs=render.Count>0?render.Average():-1;r.presentWaitMs=present.Count>0?present.Average():-1;
                ScreenCapture.CaptureScreenshot(Path.Combine(outputRoot,"view_"+(n+1)+".png"));yield return null;
            }
            var report=new Report{gpu=SystemInfo.graphicsDeviceName,cpu=SystemInfo.processorType,unity=Application.unityVersion,quality=QualitySettings.names[QualitySettings.GetQualityLevel()],width=Screen.width,height=Screen.height,vramMB=SystemInfo.graphicsMemorySize,ramMB=SystemInfo.systemMemorySize,views=results.ToArray(),method="Standalone non-development player, 1920x1080, VSync off, uncapped, 3s warmup +8s each fixed camera. Player relocated to each sampled area and invulnerable; original nearby AI and HUD active for the six route views and C3 repeat. Final view ONLY pauses enemy scripts/agents for diagnosis and must not be used as a gameplay FPS result. Captive intro suppressed; player/companion deaths prevented only in benchmark. Includes naturally alerted guards, without scripted combat load. Excludes full escort/extraction stress and controlled worst-case combat. Check focused flag; other desktop workload is not controlled."};
            File.WriteAllText(Path.Combine(outputRoot,"results.json"),JsonUtility.ToJson(report,true));File.WriteAllText(Path.Combine(outputRoot,"status.txt"),"COMPLETE");Application.Quit();
        }
    }
}
