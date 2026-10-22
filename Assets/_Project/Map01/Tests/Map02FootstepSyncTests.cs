using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// Nam's footsteps sound as a foot comes down, walking freely and carrying a load (which also walks faster than it did).
    /// </summary>
    public sealed class Map02FootstepSyncTests:ForestSceneTestBase
    {
        static Keyboard keyboard;static Key[] keys=System.Array.Empty<Key>();
        static void Pump(){keyboard.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}

        [UnityTest,Timeout(300000)]public IEnumerator StepsSoundAsAFootComesDownAndALoadIsCarriedAtAWalk()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            yield return InPlayMode();
            yield return new ExitPlayMode();
        }
        static IEnumerator Wait(float seconds){float end=Time.time+seconds;while(Time.time<end)yield return null;}
        static Vector3 OpenPoint()
        {
            for(int x=-20;x<=60;x+=10)for(int z=20;z<=60;z+=10)
            {
                if(!NavMesh.SamplePosition(new Vector3(x,1,z),out var hit,3,NavMesh.AllAreas))continue;
                if(NavMesh.FindClosestEdge(hit.position,out var edge,NavMesh.AllAreas)&&edge.distance<4)continue;
                var from=hit.position+Vector3.up;
                if(new[]{Vector3.forward,Vector3.back,Vector3.left,Vector3.right}.Any(d=>Physics.SphereCast(from,.35f,d,out _,3.5f,~0,QueryTriggerInteraction.Ignore)))continue;
                return hit.position;
            }
            Assert.Fail("No open ground to walk on.");return default;
        }
        static IEnumerator InPlayMode()
        {
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Chapter!=null&&m.Ready)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);m.Player.GetComponent<Health>().CinematicInvulnerable=true;
            var steps=m.Player.GetComponent<PlayerFootsteps>();Assert.NotNull(steps);steps.enabled=true;
            var body=m.Player.GetComponentsInChildren<Animator>().First(a=>a.isHuman);
            Transform left=body.GetBoneTransform(HumanBodyBones.LeftFoot),right=body.GetBoneTransform(HumanBodyBones.RightFoot);
            var heard=new List<(float time,bool left,float height)>();var trace=new List<string>();var frames=new List<(float t,float l,float r)>();
            void OnStep(PlayerFootsteps.Footstep s){if(!s.Landing)heard.Add((Time.time,s.LeftFoot,(s.LeftFoot?left:right).position.y-m.Player.position.y));}
            var routing=RouteInputToGame();keyboard=InputSystem.AddDevice<Keyboard>();keys=System.Array.Empty<Key>();InputSystem.onBeforeUpdate+=Pump;steps.Stepped+=OnStep;
            try
            {
                foreach(var carry in new[]{(string)null,"D3_01"})
                {
                    var snap=m.Capture();var s=new Map02ChapterState();
                    foreach(var t in Map02ChapterDefinition.Load().tasks){if(t.id=="D3_01")break;s.Complete(t.id);}
                    s.carry=carry;snap.chapter=s;m.Restore(snap);yield return Wait(1.5f);   // past the day's fade
                    var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.position=OpenPoint();cc.enabled=true;Physics.SyncTransforms();
                    var controller=m.Player.GetComponent<PlayerController>();
                    if(carry!=null)Assert.AreEqual(Map02ChapterDirector.CarryPace,controller.SurfaceSpeedMultiplier,1e-4f,"A load is carried at a brisk walk.");
                    // Each foot's lowest height over the walk is where it stands on the ground.
                    float lowL=float.PositiveInfinity,lowR=float.PositiveInfinity;heard.Clear();frames.Clear();float start=Time.time;
                    foreach(var key in new[]{Key.W,Key.S,Key.W})
                    {
                        keys=new[]{key};float end=Time.time+1.6f;
                        while(Time.time<end){yield return null;float hl=left.position.y-m.Player.position.y,hr=right.position.y-m.Player.position.y;lowL=Mathf.Min(lowL,hl);lowR=Mathf.Min(lowR,hr);frames.Add((Time.time,hl,hr));trace.Add($"{Time.time-start:0.000},{hl:0.000},{hr:0.000},{m.Player.position.x:0.00},{m.Player.position.z:0.00},{heard.Count},{body.GetFloat("Speed"):0.00},{body.GetInteger("Weapon")},{string.Join("+",body.GetCurrentAnimatorClipInfo(0).Select(c=>c.clip.name+":"+c.weight.ToString("0.00")))},{body.speed:0.00},{(body.playableGraph.IsValid()?body.playableGraph.GetOutputCount():0)}");}
                    }
                    keys=System.Array.Empty<Key>();yield return Wait(.3f);
                    System.IO.Directory.CreateDirectory("Logs/Footsteps");System.IO.File.WriteAllLines($"Logs/Footsteps/{(carry==null?"walk":"carry")}.csv",trace.Concat(heard.Select(h=>$"STEP,{h.time-start:0.000},{(h.left?"L":"R")},{h.height:0.000}")));trace.Clear();
                    string what=carry==null?"walking":"carrying";
                    var walked=heard.Where(h=>h.time>start+.4f).ToArray();   // after the first stride from standing
                    Assert.GreaterOrEqual(walked.Length,4,what+": steps are heard");
                    // Down, not mid-swing: in the lower part of that foot's range as it sounds, and still down a moment later
                    // (a heel coming down, not a foot lifting off).
                    float highL=frames.Max(f=>f.l),highR=frames.Max(f=>f.r);
                    foreach(var h in walked)
                    {
                        float low=h.left?lowL:lowR,high=h.left?highL:highR,limit=low+.55f*(high-low);   // a heel strikes with the ankle part-way up
                        var after=frames.FirstOrDefault(f=>f.t>=h.time+.15f);float later=h.left?after.l:after.r;
                        string foot=h.left?"left":"right";
                        Assert.Less(h.height,limit,$"{what}: the {foot} step sounds as that foot is down ({h.height-low:0.000} m above its lowest, swing {high-low:0.000} m).");
                        if(after.t>0)Assert.Less(later,limit,$"{what}: the {foot} foot stays down after its step ({later-low:0.000} m).");
                    }
                    Assert.Greater(walked.Count(h=>h.left),0,what+": left foot heard");Assert.Greater(walked.Count(h=>!h.left),0,what+": right foot heard");
                }
            }
            finally{steps.Stepped-=OnStep;keys=System.Array.Empty<Key>();InputSystem.onBeforeUpdate-=Pump;InputSystem.RemoveDevice(keyboard);RestoreInputRouting(routing);}
        }
    }
}
