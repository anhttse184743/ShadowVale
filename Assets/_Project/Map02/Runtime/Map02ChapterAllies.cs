using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Audio;
using ShadowVale.Gameplay.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Night 3: Nam does not hold the village alone. Anh Lực, Tâm and anh Thành fight as riflemen: on the guards' controller
    /// (rifle at the shoulder, the run, the recoil), each fires at the soldiers he can see from where he stands, a round every
    /// second and a half or two, rarely a hit (more often up close), flash and report every time, dust where a round strikes
    /// cover, and every few seconds runs to another firing spot a couple of metres from his post. The soldier shot at keeps
    /// his head down a moment and answers towards the shooter. They leave the tank's escort to Nam (the cover fire for Hùng is
    /// his), and they are never hurt: the story needs them. Once the fighting is over they get their own controller back.
    /// </summary>
    public sealed partial class Map02ChapterDirector
    {
        static readonly string[] AllyRoles={"Anh Lực","Tâm","Anh Thành"};
        const float AllyRange=32,AllyDamage=34,AllyDrift=2.2f,AllyRun=3;
        readonly Dictionary<string,float> allyNextShot=new Dictionary<string,float>(),allyFlashUntil=new Dictionary<string,float>(),allyMoveAt=new Dictionary<string,float>();
        readonly Dictionary<string,Light> allyFlash=new Dictionary<string,Light>();
        readonly Dictionary<string,RuntimeAnimatorController> allyOwnController=new Dictionary<string,RuntimeAnimatorController>();
        readonly Dictionary<string,Vector3> allyHome=new Dictionary<string,Vector3>(),allySpot=new Dictionary<string,Vector3>();
        bool AlliesFighting=>State.phase==5&&State.Done("N3_00")&&!State.Done("N3_09")&&!State.finished&&!State.hungDead;
        public int AllyShotsFired {get;private set;}
        public int AllyHits {get;private set;}
        /// <summary>The ally is on the riflemen's controller (Night 3's fighting).</summary>
        public bool AllyInCombatPose(string role)=>allyOwnController.ContainsKey(role);

        void TickAllies()
        {
            foreach(var pair in allyFlash)if(pair.Value!=null)pair.Value.enabled=allyFlashUntil.TryGetValue(pair.Key,out var until)&&Time.time<until;
            bool fighting=AlliesFighting;
            foreach(var role in AllyRoles)
                if(roles.TryGetValue(role,out var someone)&&someone!=null&&someone.visual!=null)CombatPose(role,someone,fighting&&someone.visual.gameObject.activeInHierarchy);
            if(!fighting||sceneTask!=null||Mission.Paused)return;
            foreach(var role in AllyRoles)
            {
                if(!roles.TryGetValue(role,out var ally)||ally==null||ally.visual==null||!ally.visual.gameObject.activeInHierarchy)continue;
                ShiftAlly(role,ally);
                if(ally.Travelling)continue;   // no shooting on the run
                if(allyNextShot.TryGetValue(role,out var next)&&Time.time<next)continue;
                allyNextShot[role]=Time.time+Random.Range(1.4f,2.2f);
                var eye=ally.transform.position+Vector3.up*1.45f;Map02ChapterEnemy target=null;float best=AllyRange;
                foreach(var enemy in Enemies)
                {
                    if(enemy==null||enemy.Dead||enemy.HoldPosition||!enemy.gameObject.activeInHierarchy)continue;
                    var aim=enemy.transform.position+Vector3.up*1.1f;float d=Vector3.Distance(eye,aim);if(d>=best)continue;
                    if(Physics.Raycast(eye,(aim-eye).normalized,out var seen,d+.3f,~0,QueryTriggerInteraction.Ignore)&&!seen.transform.IsChildOf(ally.transform)&&seen.transform.GetComponentInParent<Map02ChapterEnemy>()!=enemy)continue;
                    target=enemy;best=d;
                }
                if(target==null)continue;
                AllyFire(role,ally,eye,target,best);
            }
        }
        /// <summary>Every 7–12 s he runs to another spot within <see cref="AllyDrift"/> m of his post. His post is wherever
        /// the story last put him, so the story's own moves (out to the outer post, back to the tamarind) carry it along.</summary>
        void ShiftAlly(string role,Map02ChapterActor ally)
        {
            var at=ally.transform.position;
            if(!allyHome.TryGetValue(role,out var home))allyHome[role]=home=at;
            bool ownSpot=allySpot.TryGetValue(role,out var spot)&&Flat(at,spot)<1;
            if(!ally.Travelling&&!ownSpot&&Flat(at,home)>.5f){allyHome[role]=home=at;allySpot.Remove(role);}   // placed or walked there by the story
            if(ally.Travelling)return;
            bool scheduled=allyMoveAt.TryGetValue(role,out var due);
            if(scheduled&&Time.time<due)return;
            allyMoveAt[role]=Time.time+Random.Range(7f,12f);
            if(!scheduled)return;   // first sight of the fight: hold a moment before moving
            for(int i=0;i<8;i++)
            {
                var p=home+Quaternion.Euler(0,Random.Range(0f,360f),0)*Vector3.forward*Random.Range(.8f,AllyDrift);
                if(!NavMesh.SamplePosition(p,out var hit,.6f,NavMesh.AllAreas)||Flat(hit.position,at)<.7f||Flat(hit.position,Mission.Player.position)<1.4f)continue;
                if(TankLoose&&HiddenScore(hit.position)<TankThreats.Length)continue;   // with the tank in the lanes, only behind walls
                allySpot[role]=hit.position;ally.GoTo(hit.position,AllyRun);return;
            }
        }
        /// <summary>On the guards' controller (rifle pose, run, recoil) while the fight lasts; his own controller after it.</summary>
        void CombatPose(string role,Map02ChapterActor ally,bool on)
        {
            var animator=ally.visual.GetComponentInChildren<Animator>();if(animator==null)return;
            bool armed=allyOwnController.ContainsKey(role);
            if(on&&!armed)
            {
                var rifleman=Map01ActorAssets.Load()?.enemyController;if(rifleman==null||!animator.isHuman)return;
                allyOwnController[role]=animator.runtimeAnimatorController;ally.SetLocomotion(null,null);
                animator.runtimeAnimatorController=rifleman;animator.applyRootMotion=false;animator.Rebind();animator.Update(0);
                Map01EnemyGrip.BindStandalone(animator);ArmAlly(role,ally);
            }
            else if(!on&&armed)
            {
                var grip=animator.GetComponent<Map01EnemyGrip>();if(grip!=null)Destroy(grip);
                animator.runtimeAnimatorController=allyOwnController[role];allyOwnController.Remove(role);animator.Rebind();
                allyHome.Remove(role);allySpot.Remove(role);allyMoveAt.Remove(role);
            }
            if(on&&animator.runtimeAnimatorController!=null)animator.SetBool("Engaged",true);   // the shouldered rifle, not the patrol carry
        }
        void AllyFire(string role,Map02ChapterActor ally,Vector3 eye,Map02ChapterEnemy target,float distance)
        {
            var aim=target.transform.position+Vector3.up*1.1f;
            var facing=Vector3.ProjectOnPlane(aim-ally.transform.position,Vector3.up);if(facing.sqrMagnitude>.01f)ally.transform.rotation=Quaternion.LookRotation(facing);
            ArmAlly(role,ally);AllyShotsFired++;allyFlashUntil[role]=Time.time+.06f;
            var animator=ally.visual.GetComponentInChildren<Animator>();if(animator!=null&&AllyInCombatPose(role))animator.SetTrigger(PlayerCombat.AnimatorParams.Attack);
            var sound=Mission.Player.GetComponent<CombatAudio>();
            if(sound!=null&&sound.Gunshot.IsValid){var a=sound.Gunshot;PooledAudioService.Resolve().PlayOneShot(a.clip,eye,.5f*a.EffectiveGain,Random.Range(.95f,1.05f),0,1,a.startTime,a.duration);}
            bool hit=Random.value<Mathf.Lerp(.35f,.12f,distance/AllyRange);
            if(!hit)aim+=Vector3.Cross(Vector3.up,(aim-eye).normalized)*(Random.value<.5f?-1:1)*Random.Range(.4f,1.1f)+Vector3.up*Random.Range(-.5f,.6f);
            target.Suppress(1.4f);target.UnderFireFrom(ally.transform.position);
            if(!Physics.Raycast(eye,(aim-eye).normalized,out var struck,distance+6,~0,QueryTriggerInteraction.Ignore)||struck.transform.IsChildOf(ally.transform))return;
            var health=struck.transform.GetComponentInParent<Health>();
            if(health!=null){if(hit&&health==target.HitPoints){health.TakeDamage(AllyDamage,struck.point,ally.gameObject);AllyHits++;}return;}
            if(!(struck.collider is CharacterController))CoverImpact.Play(struck.point,struck.normal);
        }
        readonly Dictionary<string,Vector3> retreats=new Dictionary<string,Vector3>();bool retreatStarted;
        /// <summary>N3_WOUND: whoever stands near the breach falls back to cover as the tank comes in (anh Lực to the tamarind
        /// Nam then reaches, Tâm to the bamboo, anh Thành to the command post).</summary>
        void PlanRetreats()
        {
            retreats.Clear();retreatStarted=false;var breach=sites["tankBreach"].position;
            foreach(var role in AllyRoles)
            {
                if(!roles.TryGetValue(role,out var ally)||ally==null||ally.visual==null||!ally.visual.gameObject.activeInHierarchy||Flat(ally.transform.position,breach)>30)continue;
                // Anh Lực into the cover Nam then makes for; the others well back from the breach, towards the bank.
                var back=Vector3.ProjectOnPlane(sites["ditch"].position-breach,Vector3.up).normalized;
                var to=role=="Anh Lực"?LucCover():breach+back*20+Vector3.Cross(Vector3.up,back)*(role=="Tâm"?4:-4);
                if(NavMesh.SamplePosition(to,out var hit,4,NavMesh.AllAreas))to=hit.position;retreats[role]=role=="Anh Lực"?to:HiddenFromTank(to,9);
            }
        }
        /// <summary>They break at the first sound of the tracks, before the tank is in, running for cover.</summary>
        void TickRetreats()
        {
            if(retreatStarted||sceneClock<.3f)return;retreatStarted=true;   // at the first sound of the tracks, before it is in
            foreach(var pair in retreats){var ally=roles[pair.Key];ally.SceneDriven=true;ally.GoTo(pair.Value,NightRun);}
        }
        public IReadOnlyDictionary<string,Vector3> Retreats=>retreats;
        /// <summary>The cover Nam makes for after the hit (the crawl's mark, out of the tank's sight).</summary>
        public Vector3 CrawlCover=>markers.TryGetValue("N3_CRAWL",out var mark)?mark.transform.position:sites["cover"].position;
        /// <summary>Anh Lực's place in that cover: beside Nam's, out of the tank's sight too.</summary>
        Vector3 LucCover()
        {
            var c=CrawlCover;var across=Vector3.Cross(Vector3.up,Vector3.ProjectOnPlane(c-sites["tankBreach"].position,Vector3.up).normalized);
            var p=HiddenFromTank(c+across*1.5f,3);return Flat(p,c)<1.1f?c+across*1.5f:p;
        }
        /// <summary>From the crawl until it burns, the tank is loose in the village: Nam's comrades keep out of its sight.</summary>
        bool TankLoose=>State.phase==5&&State.Done("N3_CRAWL")&&!State.tankDestroyed&&!State.Done("N3_09");
        Vector3[] tankThreats;
        /// <summary>Where the tank looks from: the breach, and the middle and far end of the lanes it roams.</summary>
        Vector3[] TankThreats
        {
            get
            {
                if(tankThreats!=null)return tankThreats;
                var leg=Leg("tankBreach","tankHide");float run=Length(leg);
                return tankThreats=new[]{sites["tankBreach"].position,Along(leg,run*RoamMid),Along(leg,run*RoamFar)}.Select(p=>p+Vector3.up*2.2f).ToArray();
            }
        }
        List<Bounds> houses;
        /// <summary>The village's houses as solid blocks. They stand on stilts, so a ray at chest height slips under their
        /// floors between the posts; the whole house is what hides a man from the tank.</summary>
        List<Bounds> Houses
        {
            get
            {
                if(houses!=null)return houses;houses=new List<Bounds>();
                foreach(var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if(t.name.IndexOf("house",System.StringComparison.OrdinalIgnoreCase)<0||t.GetComponentInParent<Map02ChapterActor>()!=null)continue;
                    var parts=t.GetComponentsInChildren<Renderer>();if(parts.Length==0)continue;
                    var b=parts[0].bounds;foreach(var r in parts)b.Encapsulate(r.bounds);
                    if(b.size.y<2||b.size.x<2.5f&&b.size.z<2.5f||b.size.x>22||b.size.z>22)continue;   // a house, not a prop or the whole village
                    houses.Add(b);
                }
                return houses;
            }
        }
        public int HouseCount=>Houses.Count;
        /// <summary>How many of the tank's vantage points a man standing at <paramref name="p"/> is hidden from (-1: inside or
        /// under a house).</summary>
        int HiddenScore(Vector3 p)
        {
            int hidden=0;var chest=p+Vector3.up*1.2f;
            foreach(var house in Houses){var inside=house;inside.Expand(.4f);if(inside.Contains(chest))return -1;}
            foreach(var from in TankThreats)
            {
                var line=chest-from;var ray=new Ray(from,line.normalized);bool behindHouse=false;
                foreach(var house in Houses)if(house.IntersectRay(ray,out var enter)&&enter<line.magnitude-.5f){behindHouse=true;break;}
                if(behindHouse){hidden++;continue;}
                if(!Physics.Linecast(from,chest,out var block,~0,QueryTriggerInteraction.Ignore))continue;
                if(block.collider is CharacterController||block.collider.GetComponentInParent<Map02ChapterActor>()!=null||block.collider.GetComponentInParent<Health>()!=null||(tank!=null&&block.transform.IsChildOf(tank.transform)))continue;
                hidden++;
            }
            return hidden;
        }
        /// <summary>A spot near <paramref name="near"/> behind a house, a wall or a bank from the tank: the nearest one hidden
        /// from most of its vantage points.</summary>
        public Vector3 HiddenFromTank(Vector3 near,float reach=10)
        {
            int bestHidden=HiddenScore(near);if(bestHidden==TankThreats.Length)return near;
            Vector3 best=near;float bestDistance=0;
            for(int ring=1;ring<=5;ring++)for(int i=0;i<16;i++)
            {
                var p=near+Quaternion.Euler(0,i*22.5f+ring*11,0)*Vector3.forward*(reach*ring/5f);
                if(!NavMesh.SamplePosition(p,out var hit,.8f,NavMesh.AllAreas))continue;
                int h=HiddenScore(hit.position);float d=Flat(hit.position,near);
                if(h>bestHidden||h==bestHidden&&h>0&&d<bestDistance){bestHidden=h;best=hit.position;bestDistance=d;}
            }
            return best;
        }
        /// <summary>A rifle in hand for Night 3 (Thành carries his already) and a muzzle flash at its tip.</summary>
        void ArmAlly(string role,Map02ChapterActor ally)
        {
            if(allyFlash.ContainsKey(role))return;
            var animator=ally.visual.GetComponentInChildren<Animator>();Map01Rifle rifle=null;
            if(animator!=null){rifle=animator.GetComponent<Map01Rifle>();if(rifle==null)rifle=Map01Rifle.Attach(animator);}
            var tip=rifle!=null&&rifle.weapon!=null?rifle.weapon.Muzzle:ally.transform;
            var go=new GameObject(role+"'s muzzle flash");go.transform.SetParent(tip,false);if(tip==ally.transform)go.transform.localPosition=new Vector3(.25f,1.35f,.6f);
            var light=go.AddComponent<Light>();light.type=LightType.Point;light.range=4;light.intensity=6;light.color=new Color(1,.78f,.45f);light.enabled=false;
            allyFlash[role]=light;
        }
    }
}
