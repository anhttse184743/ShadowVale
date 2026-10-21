using ShadowVale.Gameplay.Audio;
using ShadowVale.Gameplay.Combat;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Smaller staging around Day 3 and Night 3: the grenade crate Nam carries to the bank and the bundle Hùng
    /// takes from it, the flare that opens the attack, Ông Sáu's burning roof, and Tâm firing from the bamboo.
    /// Like the rest of the chapter, what is shown follows from the story state.
    /// </summary>
    public sealed partial class Map02ChapterDirector
    {
        public GameObject CrateProp {get;private set;}
        public GameObject BundleProp {get;private set;}
        public Light FlareLight {get;private set;}
        public Light HouseFire {get;private set;}
        public Light TamFlash {get;private set;}
        ParticleSystem houseSmoke;float flareStarted=-1,tamClock;

        // D3_02: fetch the crate from the closed store, carry it to the bank, then the scene and the stowing.
        bool CarryCrate(Map02ChapterTask t)
        {
            if(t.kind!="demolition"||State.introduced.Contains(t.id))return false;
            if(State.carry!=t.id){State.carry=t.id;combat.Equip(WeaponKind.Unarmed);Mission.Say("Đã nhấc thùng lựu đạn. Mang ra hốc bờ đất cạnh bến.");UpdateMarkers();return true;}
            State.carry=null;UpdateMarkers();return false; // Set down at the bank: the scene follows.
        }
        Vector3 CrateMarker(Map02ChapterTask t)=>sites[State.carry==t.id||State.introduced.Contains(t.id)?"ditch":"store"].position;
        string CratePrompt(Map02ChapterTask t)=>State.carry==t.id?"Đặt thùng lựu đạn vào hốc bờ đất":State.introduced.Contains(t.id)?t.title:"Nhận thùng lựu đạn tại kho kín";
        string CrateInstruction(Map02ChapterTask t)=>State.carry==t.id?"Đang mang thùng lựu đạn. Tới hốc bờ đất cạnh bến, nhấn E đặt xuống.":State.introduced.Contains(t.id)?"E bắt đầu · giữ Space để cất, Esc hủy.":"Tới kho kín, nhấn E nhận thùng lựu đạn.";

        void TickDressing(float dt)
        {
            RefreshStowedCharge();TickFlare();TickHouseFire();TickTamCover(dt);
        }
        void RefreshStowedCharge()
        {
            // The crate stays in the hole; the bundle is gone once Hùng pulls it out in the farewell.
            bool placed=State.Done("D3_02")||sceneTask?.id=="D3_02"||State.introduced.Contains("D3_02");
            bool bundleTaken=State.Done("N3_07")||AfterLine("N3_07","m2v3_farewell_06");
            if(placed&&CrateProp==null)
            {
                var hole=sites["ditch"].position+Vector3.right*1.2f+Vector3.back*.6f;
                CrateProp=Placeholder("Grenade crate in the bank hole",new Vector3(.7f,.45f,.45f),new Color(.24f,.27f,.16f),hole+Vector3.up*.22f);
                BundleProp=Layout?.explosivesArt!=null?Instantiate(Layout.explosivesArt):Placeholder("Last militia demolition bundle — art slot",new Vector3(.38f,.24f,.26f),new Color(.36f,.28f,.18f),Vector3.zero);
                BundleProp.transform.SetParent(root.transform);BundleProp.transform.position=hole+Vector3.right*.6f+Vector3.up*.13f;
            }
            if(CrateProp!=null)CrateProp.SetActive(placed);
            if(BundleProp!=null)BundleProp.SetActive(placed&&!bundleTaken);
        }
        GameObject Placeholder(string name,Vector3 size,Color color,Vector3 at)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;Destroy(go.GetComponent<Collider>());go.transform.SetParent(root.transform);
            go.transform.localScale=size;go.transform.position=at;var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);go.GetComponent<Renderer>().SetPropertyBlock(block);return go;
        }
        void TickFlare()
        {
            // Nhịp 1: one second of black, then an enemy flare over the roofs.
            if(sceneTask?.id=="N3_00"&&sceneClock>=1&&flareStarted<0)flareStarted=Time.time;
            if(flareStarted<0)return;
            if(FlareLight==null)
            {
                var go=new GameObject("Enemy flare");go.transform.SetParent(root.transform);go.transform.position=sites["rest"].position+Vector3.up*26+Vector3.forward*10;
                FlareLight=go.AddComponent<Light>();FlareLight.type=LightType.Point;FlareLight.range=90;FlareLight.color=new Color(.92f,.95f,1);FlareLight.shadows=LightShadows.None;
            }
            float age=Time.time-flareStarted;FlareLight.intensity=age<12?Mathf.Lerp(6,0,age/12)*(.9f+Mathf.PerlinNoise(Time.time*4,1)*.2f):0;
            if(age>=12||State.phase!=5){FlareLight.intensity=0;flareStarted=-1;}
        }
        void TickHouseFire()
        {
            // Nhịp 2: Ông Sáu's back roof burns from the attack through the night.
            bool burning=State.phase==5&&(State.Done("N3_00")||sceneTask?.id=="N3_00"&&sceneClock>=1);
            if(burning&&HouseFire==null)
            {
                var roof=sites["family"].position+Vector3.up*4.2f+Vector3.back*1.5f;
                var go=new GameObject("Ông Sáu's burning roof");go.transform.SetParent(root.transform);go.transform.position=roof;
                HouseFire=go.AddComponent<Light>();HouseFire.type=LightType.Point;HouseFire.range=18;HouseFire.color=new Color(1,.45f,.16f);HouseFire.shadows=LightShadows.None;
                houseSmoke=Map01Fx.System(root.transform,"Roof smoke",roof,Map01Fx.Lit,3,90);var main=houseSmoke.main;main.loop=true;main.startLifetime=4;main.startSpeed=1.6f;main.startSize=2.4f;main.startColor=new Color(.16f,.14f,.13f,.7f);
                var emission=houseSmoke.emission;emission.rateOverTime=10;houseSmoke.Play();
            }
            if(HouseFire==null)return;
            HouseFire.enabled=burning;HouseFire.intensity=2.6f+Mathf.PerlinNoise(Time.time*3.5f,7)*1.8f;
            if(burning&&!houseSmoke.isPlaying)houseSmoke.Play();else if(!burning&&houseSmoke.isPlaying)houseSmoke.Stop();
        }
        void TickTamCover(float dt)
        {
            // Nhịp 4: Tâm fires from the bamboo while Nam clears the fallen tree.
            bool covering=State.phase==5&&State.Done("N3_CRAWL")&&!State.Done("N3_06");
            if(!covering){if(TamFlash!=null)TamFlash.enabled=false;return;}
            var tam=roles["Tâm"].transform;
            if(TamFlash==null){var go=new GameObject("Tâm's muzzle flash");go.transform.SetParent(tam,false);go.transform.localPosition=new Vector3(0,1.4f,.7f);TamFlash=go.AddComponent<Light>();TamFlash.type=LightType.Point;TamFlash.range=7;TamFlash.intensity=3.5f;TamFlash.color=new Color(1,.8f,.45f);TamFlash.shadows=LightShadows.None;}
            var field=Vector3.ProjectOnPlane(sites["lane3"].position-tam.position,Vector3.up);if(field.sqrMagnitude>.01f)tam.rotation=Quaternion.LookRotation(field);
            float before=tamClock;tamClock+=dt;TamFlash.enabled=Mathf.Repeat(tamClock,1.6f)<.06f;
            if(Mathf.Floor(tamClock/1.6f)>Mathf.Floor(before/1.6f))
            {var sound=Mission.Player.GetComponent<CombatAudio>();if(sound!=null&&sound.Gunshot.IsValid){var clip=sound.Gunshot;PooledAudioService.Resolve().PlayOneShot(clip.clip,tam.position,.4f*clip.EffectiveGain,1.1f,0,.85f,clip.startTime,clip.duration);}}
        }
        /// <summary>One-off sounds tied to a spoken line.</summary>
        void LineCue(string id)
        {
            // Đêm 2, "Tiếng gì thế?": tank tracks far out in the fields — the sound Night 3 brings back.
            if(id=="m2v3_lights_06"&&Layout?.tankTracksClip!=null)AudioSource.PlayClipAtPoint(Layout.tankTracksClip,sites["bridge"].position+Vector3.forward*70,.6f);
        }
    }
}
