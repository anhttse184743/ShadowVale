using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Audio;
using ShadowVale.Gameplay.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Nhịp 6, the counter-attack (N3_09 after "Rạch Tre. Bắn."): the soldiers still in the village hold a lane; the militia
    /// pour in from the fields as riflemen, close on them and shoot them down, seen wide and from the side, far off. Then a
    /// shell from Rạch Tre comes down out of the sky onto the burning tank: followed alongside through the sky, then seen
    /// from the shell itself as it dives onto the hull, and the wreck goes up a second time.
    /// </summary>
    public sealed partial class Map02ChapterDirector
    {
        public const float PocketAt=FireOrderAt,ShellAt=42,ShellFlight=3.2f,ShellPov=.7f,SafeBlastDistance=22;
        const int PocketSize=5;
        readonly List<Map02ChapterEnemy> pocket=new List<Map02ChapterEnemy>();
        readonly Dictionary<Map02ChapterEnemy,int> pocketHits=new Dictionary<Map02ChapterEnemy,int>();
        readonly List<float> militiaNextShot=new List<float>();
        Light militiaFlash;float militiaFlashUntil;
        GameObject shell;bool shellLanded;
        Vector3 pocketCenter,shellFrom,shellBend;
        public int PocketAlive=>pocket.Count(e=>e!=null&&!e.Dead);
        public bool ShellLanded=>shellLanded;

        /// <summary>The blast must not be on top of Nam: if covering Hùng took him close, he is back at the cover point.</summary>
        void KeepNamClearOfTheBlast()
        {
            var tankAt=tank!=null?tank.transform.position:sites["tankDock"].position;
            if(Flat(Mission.Player.position,tankAt)>=SafeBlastDistance)return;
            var spot=markers.TryGetValue("N3_08",out var cover)?cover.transform.position:Mission.Player.position;
            if(Flat(spot,tankAt)<SafeBlastDistance)
            {
                var away=Vector3.ProjectOnPlane(spot-tankAt,Vector3.up);away=away.sqrMagnitude>.01f?away.normalized:(sites["ditch"].position-tankAt).normalized;
                spot=tankAt+away*SafeBlastDistance;if(NavMesh.SamplePosition(spot,out var hit,4,NavMesh.AllAreas))spot=hit.position;
            }
            var cc=Mission.Player.GetComponent<CharacterController>();if(cc!=null)cc.enabled=false;Mission.Player.position=spot;if(cc!=null)cc.enabled=true;Physics.SyncTransforms();
        }
        /// <summary>The tank goes up: the bang in 3D, loud and untouched by Nam's deafness for its first moment, and the fireball.</summary>
        void BlowTank()
        {
            var at=tank!=null?tank.transform.position:sites["tankDock"].position;
            var blast=Map01Blast.Spawn(at,Vector3.up,null,7.5f);
            if(blast!=null&&blast.Sound!=null){blast.Sound.bypassListenerEffects=true;blast.Sound.volume=1;blast.Sound.maxDistance=Mathf.Max(blast.Sound.maxDistance,160);}
            Fireball(at+Vector3.up,2.2f);
        }
        /// <summary>The soldiers left in the village: a lane in sight of the militia's road, rifles up, waiting.</summary>
        void SpawnPocket()
        {
            if(pocket.Count>0)return;
            var leg=Leg("tankBreach","tankHide");float run=Length(leg);
            var spots=new[]{.32f,.38f,.44f,.5f,.56f}.Select((f,i)=>{var p=Along(leg,run*f);var across=Vector3.Cross(Vector3.up,(Along(leg,run*f+1)-p).normalized);return p+across*((i%2==0?1:-1)*1.8f);}).ToArray();
            pocketCenter=spots.Aggregate(Vector3.zero,(a,b)=>a+b)/spots.Length;
            for(int i=0;i<PocketSize;i++)
            {
                var at=NavMesh.SamplePosition(spots[i],out var hit,3,NavMesh.AllAreas)?hit.position:spots[i];
                SpawnEnemy("pocket_"+i,"pocket",at,100,true);var e=Enemies[Enemies.Count-1];pocket.Add(e);pocketHits[e]=0;
                var toward=Vector3.ProjectOnPlane(sites["militiaEntry"].position-at,Vector3.up);if(toward.sqrMagnitude>.01f)e.transform.rotation=Quaternion.LookRotation(toward);
            }
        }
        /// <summary>Each militiaman runs in along the road, stops in range of a soldier still standing and fires on him until he falls.</summary>
        void TickMilitiaFight(float t)
        {
            if(militiaFlash!=null)militiaFlash.enabled=Time.time<militiaFlashUntil;
            for(int i=0;i<militia.Count;i++)
            {
                if(militia[i]==null)continue;
                while(militiaNextShot.Count<=i)militiaNextShot.Add(0);
                var at=militia[i].position;var target=pocket.Where(e=>e!=null&&!e.Dead).OrderBy(e=>Flat(e.transform.position,at)).FirstOrDefault();
                bool inRange=target!=null&&Flat(target.transform.position,at)<14;
                if(!inRange)
                {
                    float d=Mathf.Max(0,(t-MilitiaAt-i*.4f)*4.2f);var ahead=Along(militiaRoad,d+1)-Along(militiaRoad,d);
                    var side=Vector3.Cross(Vector3.up,ahead.sqrMagnitude>.01f?ahead.normalized:Vector3.forward)*((i%3-1)*1.6f);
                    militiaRoadAt[i]=Mathf.Max(militiaRoadAt[i],d);militia[i].position=Along(militiaRoad,militiaRoadAt[i])+side;
                    if(ahead.sqrMagnitude>.01f)militia[i].rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(ahead,Vector3.up));
                }
                var animator=militia[i].GetComponentInChildren<Animator>();
                if(animator!=null&&animator.runtimeAnimatorController!=null){animator.SetFloat("Speed",inRange||target==null?0:4.2f);animator.SetBool("Engaged",true);}
                if(!inRange||t<militiaNextShot[i])continue;
                militiaNextShot[i]=t+Random.Range(.5f,.9f);
                var face=Vector3.ProjectOnPlane(target.transform.position-at,Vector3.up);if(face.sqrMagnitude>.01f)militia[i].rotation=Quaternion.LookRotation(face);
                if(animator!=null&&animator.runtimeAnimatorController!=null)animator.SetTrigger(PlayerCombat.AnimatorParams.Attack);
                MilitiaShot(at+Vector3.up*1.45f+militia[i].forward*.6f);
                if(++pocketHits[target]>=3)target.HitPoints.TakeDamage(1000,target.transform.position+Vector3.up,militia[i].gameObject);
            }
        }
        readonly List<float> militiaRoadAt=new List<float>();
        void MilitiaShot(Vector3 muzzle)
        {
            if(militiaFlash==null)
            {
                var go=new GameObject("Militia muzzle flash");go.transform.SetParent(root.transform);militiaFlash=go.AddComponent<Light>();
                militiaFlash.type=LightType.Point;militiaFlash.range=6;militiaFlash.intensity=6;militiaFlash.color=new Color(1,.78f,.45f);militiaFlash.shadows=LightShadows.None;
            }
            militiaFlash.transform.position=muzzle;militiaFlash.enabled=true;militiaFlashUntil=Time.time+.06f;
            var sound=Mission.Player.GetComponent<CombatAudio>();
            if(sound!=null&&sound.Gunshot.IsValid){var a=sound.Gunshot;PooledAudioService.Resolve().PlayOneShot(a.clip,muzzle,.55f*a.EffectiveGain,Random.Range(.92f,1.06f),0,.85f,a.startTime,a.duration);}
        }
        /// <summary>The militia on the guards' controller: rifles shouldered, the run.</summary>
        void ArmMilitiaman(Transform body)
        {
            var animator=body.GetComponentInChildren<Animator>();var rifleman=Map01ActorAssets.Load()?.enemyController;
            if(animator==null||rifleman==null||!animator.isHuman)return;
            animator.runtimeAnimatorController=rifleman;animator.applyRootMotion=false;animator.Rebind();animator.Update(0);Map01EnemyGrip.BindStandalone(animator);
        }

        // ---- the shell from Rạch Tre
        Vector3 ShellPosition(float u){var end=ShellTarget;var a=Vector3.Lerp(shellFrom,shellBend,u);var b=Vector3.Lerp(shellBend,end,u);return Vector3.Lerp(a,b,u);}
        Vector3 ShellTarget=>(tank!=null?tank.transform.position:sites["tankDock"].position)+Vector3.up*1.6f;
        void TickShell(float t)
        {
            if(t<ShellAt||shellLanded)return;
            if(shell==null)
            {
                var end=ShellTarget;var outward=Vector3.ProjectOnPlane(sites["militiaEntry"].position-end,Vector3.up);outward=outward.sqrMagnitude>.01f?outward.normalized:Vector3.forward;
                shellFrom=end+outward*140+Vector3.up*95;shellBend=Vector3.Lerp(shellFrom,end,.45f)+Vector3.up*28;
                shell=GameObject.CreatePrimitive(PrimitiveType.Capsule);shell.name="Rạch Tre's shell";Destroy(shell.GetComponent<Collider>());shell.transform.SetParent(root.transform);shell.transform.localScale=new Vector3(.22f,.5f,.22f);
                var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",new Color(.12f,.12f,.1f));shell.GetComponent<Renderer>().SetPropertyBlock(block);
                var trail=shell.AddComponent<TrailRenderer>();trail.material=Map01Fx.Glow;trail.time=.45f;trail.startWidth=.35f;trail.endWidth=0;trail.startColor=new Color(1,.62f,.25f,.9f);trail.endColor=new Color(1,.4f,.1f,0);
                var glow=new GameObject("Shell glow").AddComponent<Light>();glow.transform.SetParent(shell.transform,false);glow.type=LightType.Point;glow.range=8;glow.intensity=3;glow.color=new Color(1,.6f,.25f);glow.shadows=LightShadows.None;
            }
            float u=Mathf.Clamp01((t-ShellAt)/ShellFlight);var at=ShellPosition(u);var next=ShellPosition(Mathf.Min(1,u+.01f));
            shell.transform.position=at;var along=next-at;if(along.sqrMagnitude>1e-6f)shell.transform.rotation=Quaternion.FromToRotation(Vector3.up,along.normalized);
            if(u<1)return;
            shellLanded=true;Destroy(shell);shell=null;
            var hitAt=ShellTarget-Vector3.up*1.6f;var blast=Map01Blast.Spawn(hitAt,Vector3.up,null,7.5f);
            if(blast!=null&&blast.Sound!=null){blast.Sound.bypassListenerEffects=true;blast.Sound.volume=1;}
            Fireball(hitAt+Vector3.up,2.6f);rig.Shake(.45f,1);
        }
        /// <summary>The counter-attack and the shell, as seen: wide from the side as the militia go in; alongside the shell in the
        /// sky; from the shell as it dives on the tank. False outside those beats.</summary>
        bool CounterAttackCamera(float t,out Vector3 eye,out Vector3 focus)
        {
            eye=focus=default;
            if(t>=ShellAt&&t<ShellAt+ShellFlight&&shell!=null)
            {
                float u=Mathf.Clamp01((t-ShellAt)/ShellFlight);var at=ShellPosition(u);var dir=(ShellPosition(Mathf.Min(1,u+.01f))-at).normalized;
                if(t<ShellAt+ShellFlight-ShellPov)
                {
                    var side=Vector3.Cross(Vector3.up,dir);if(side.sqrMagnitude<.01f)side=Vector3.right;side.Normalize();
                    eye=at+side*12-dir*4+Vector3.up*1.5f;focus=at+dir*6;stagedFov=50;return true;
                }
                eye=at+dir*.5f;focus=ShellTarget;stagedFov=72;return true;   // the shell's own view, the tank rushing up
            }
            if(t>=MilitiaAt&&t<ShellAt&&militia.Count>0)
            {
                var alive=militia.Where(m=>m!=null).ToList();if(alive.Count==0)return false;
                var group=alive.Aggregate(Vector3.zero,(a,m)=>a+m.position)/alive.Count;var mid=Vector3.Lerp(group,pocketCenter,.5f);
                var axis=Vector3.ProjectOnPlane(pocketCenter-group,Vector3.up);axis=axis.sqrMagnitude>.01f?axis.normalized:Vector3.forward;
                var side=Vector3.Cross(Vector3.up,axis);if(Vector3.Dot(Mission.Player.position-mid,side)>0)side=-side;   // the far side from Nam: nothing of the bank in shot
                eye=mid+side*26+Vector3.up*6;focus=mid+Vector3.up*1.5f;stagedFov=42;return true;
            }
            return false;
        }
        void ClearCounterAttack()
        {
            foreach(var e in pocket)if(e!=null){Enemies.Remove(e);Destroy(e.gameObject);}
            pocket.Clear();pocketHits.Clear();militiaNextShot.Clear();militiaRoadAt.Clear();
            if(shell!=null)Destroy(shell);shell=null;shellLanded=false;if(militiaFlash!=null)militiaFlash.enabled=false;
        }
    }
}
