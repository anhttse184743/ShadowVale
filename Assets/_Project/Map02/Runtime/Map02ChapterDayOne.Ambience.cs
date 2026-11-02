using System.Collections.Generic;
using UnityEngine;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        // Plan P8 (2026-10-25): Map 2 had no sound of its own. Day 1: bamboo creaking in the wind everywhere, children by the
        // houses, oars at the jetty, a rooster far off now and then, one aeroplane very far away about eight minutes in.
        // Night 1: frogs and crickets everywhere, the stove fire in chị Lành's kitchen. No music in phases 0–1 except Hùng's
        // theme inside "Lời hứa". CC0 recordings, sources and hashes: SourceArt/Audio/Map02_DayOne_20261025.
        sealed class AmbienceLayer{public AudioSource source;}
        readonly Dictionary<string,AmbienceLayer> ambience=new();
        float roosterIn=25,dayAmbienceClock;bool planeFlown,ambiencePaused;
        public const float PlaneAt=480;   // seconds of Day 1 play
        AmbienceLayer Layer(string clip,Vector3? at=null,float near=3,float far=30,bool loop=true)
        {
            if(ambience.TryGetValue(clip,out var layer))return layer;
            var sound=Resources.Load<AudioClip>("Map02DayOne/"+clip);var host=new GameObject("Ambience — "+clip);host.transform.SetParent(transform,false);if(at!=null)host.transform.position=at.Value;
            var source=host.AddComponent<AudioSource>();source.clip=sound;source.loop=loop;source.playOnAwake=false;source.volume=0;
            source.spatialBlend=at!=null?1:0;source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=near;source.maxDistance=far;source.dopplerLevel=0;
            if(loop&&sound!=null){source.time=Random.Range(0,sound.length*.9f);source.Play();}
            layer=new AmbienceLayer{source=source};ambience[clip]=layer;return layer;
        }
        void TickAmbience(float dt,bool paused)
        {
            if(paused!=ambiencePaused){foreach(var layer in ambience.Values){if(paused)layer.source.Pause();else layer.source.UnPause();}ambiencePaused=paused;}
            if(paused||!ready)return;
            float duck=DialogueVoice.AnySpeech?.25f:1;
            bool day=State.phase==0,night=State.phase==1;var dock=sites.TryGetValue("dock",out var d)?d.position:DayFamily;
            Fade(Layer("d1_bamboo_loop"),day?.12f*duck:0,dt);Fade(Layer("d1_children_loop",DayFamily+Vector3.forward*4,4,30),day?.10f*duck:0,dt);Fade(Layer("d1_oars_loop",dock,3,18),day?.065f*duck:0,dt);
            Fade(Layer("n1_frogs_loop"),night?.10f*duck:0,dt);Fade(Layer("n1_fire_loop",porchDoor-porchOut*2.6f+Vector3.up*.4f,1.5f,14),night?.07f*duck:0,dt);
            if(!day)return;
            State.villageDaySeconds+=dt;dayAmbienceClock=State.villageDaySeconds;roosterIn-=dt;
            if(roosterIn<=0){roosterIn=Random.Range(45f,110f);var crow=Resources.Load<AudioClip>("Map02DayOne/d1_rooster");if(crow!=null)Layer("d1_bamboo_loop").source.PlayOneShot(crow,Random.Range(.4f,.6f));}   // scaled by the bed's own volume: far off
            if(!planeFlown&&!State.villagePlanePlayed&&dayAmbienceClock>=PlaneAt){planeFlown=State.villagePlanePlayed=true;var plane=Layer("d1_plane_far",null,3,30,false);plane.source.volume=.035f*duck;plane.source.Play();}
        }
        static void Fade(AmbienceLayer layer,float target,float dt){layer.source.volume=Mathf.MoveTowards(layer.source.volume,target,dt/3);}   // phase changes cross over ~2 s
        public float AmbienceVolume(string clip)=>ambience.TryGetValue(clip,out var layer)?layer.source.volume:0;

        // "Lời hứa": Hùng's theme, solo, from Nam's "Tôi hả... cũng chưa biết" to the held end; his laugh after the punch.
        const float SupperMusicAt=49.7f,SupperLaughAt=43.1f;
        AudioSource supperMusic;bool supperLaughed;
        void TickSupperSound()
        {
            if(sceneTask?.id!="N1_01"||!mealBegan)return;float t=MealTime;
            if(supperMusic==null){var host=new GameObject("Hùng's theme — Lời hứa");host.transform.SetParent(transform,false);supperMusic=host.AddComponent<AudioSource>();supperMusic.playOnAwake=false;supperMusic.spatialBlend=0;supperMusic.volume=.12f;}
            if(!supperLaughed&&t>=SupperLaughAt){supperLaughed=true;var laugh=Resources.Load<AudioClip>("Map02DayOne/hung_laugh");if(laugh!=null)supperMusic.PlayOneShot(laugh,.45f);}
            if(t>=SupperMusicAt&&supperMusic.clip==null){supperMusic.clip=Resources.Load<AudioClip>("Map02DayOne/hung_theme");supperMusic.volume=.08f;if(supperMusic.clip!=null){supperMusic.time=Mathf.Min(t-SupperMusicAt,supperMusic.clip.length-.01f);supperMusic.Play();}}
        }
        void StopSupperSound(){if(supperMusic!=null){supperMusic.Stop();supperMusic.clip=null;}supperLaughed=false;}
        void PauseSupperSound(bool paused){if(supperMusic==null)return;if(paused)supperMusic.Pause();else supperMusic.UnPause();}

        // Ông Sáu coughs in the inner room before he speaks (P5): his first line waits for it.
        const float CoughLead=1.6f;string pendingVoice;bool sceneCoughed;
        bool CoughFirst(Map02DialogueCatalog.Entry line)
        {
            if(sceneTask?.id!="D1_03"||line.speaker!="Ông Sáu"||sceneCoughed)return false;sceneCoughed=true;
            var cough=Resources.Load<AudioClip>("Map02DayOne/cough");if(cough==null)return false;
            AudioSource.PlayClipAtPoint(cough,porchDoor-porchOut*2.5f+Vector3.up*1.2f,.35f);return true;   // from inside the house
        }
        void TickPendingVoice(){if(pendingVoice!=null&&lineClock>=0){voice.Play(pendingVoice);pendingVoice=null;}}
    }
}
