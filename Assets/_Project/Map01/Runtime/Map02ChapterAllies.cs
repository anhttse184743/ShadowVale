using System.Collections.Generic;
using ShadowVale.Gameplay.Audio;
using ShadowVale.Gameplay.Combat;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Night 3: Nam does not hold the village alone. Anh Lực, Tâm and anh Thành fire at the soldiers they can see from where
    /// they stand: a round every second and a half or two, rarely a hit (more often up close), flash and report every time,
    /// dust where a round strikes cover. The soldier shot at keeps his head down a moment and answers towards the shooter.
    /// They leave the tank's escort to Nam (the cover fire for Hùng is his), and they are never hurt: the story needs them.
    /// </summary>
    public sealed partial class Map02ChapterDirector
    {
        static readonly string[] AllyRoles={"Anh Lực","Tâm","Anh Thành"};
        const float AllyRange=32,AllyDamage=34;
        readonly Dictionary<string,float> allyNextShot=new Dictionary<string,float>(),allyFlashUntil=new Dictionary<string,float>();
        readonly Dictionary<string,Light> allyFlash=new Dictionary<string,Light>();
        bool AlliesFighting=>State.phase==5&&State.Done("N3_00")&&!State.Done("N3_09")&&!State.finished&&!State.hungDead;
        public int AllyShotsFired {get;private set;}
        public int AllyHits {get;private set;}

        void TickAllies()
        {
            foreach(var pair in allyFlash)if(pair.Value!=null)pair.Value.enabled=Time.time<allyFlashUntil[pair.Key];
            if(!AlliesFighting||sceneTask!=null||Mission.Paused)return;
            foreach(var role in AllyRoles)
            {
                if(!roles.TryGetValue(role,out var ally)||ally==null||ally.visual==null||!ally.visual.gameObject.activeInHierarchy)continue;
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
        void AllyFire(string role,Map02ChapterActor ally,Vector3 eye,Map02ChapterEnemy target,float distance)
        {
            var aim=target.transform.position+Vector3.up*1.1f;
            var facing=Vector3.ProjectOnPlane(aim-ally.transform.position,Vector3.up);if(facing.sqrMagnitude>.01f)ally.transform.rotation=Quaternion.LookRotation(facing);
            ArmAlly(role,ally);AllyShotsFired++;allyFlashUntil[role]=Time.time+.06f;
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
