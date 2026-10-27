using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Audio;
using ShadowVale.Gameplay.Combat;
using UnityEngine;
using UnityEngine.AI;

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

        public GameObject HeldCargo {get;private set;}
        public string HeldCargoKind=>HeldCargo!=null&&HeldCargo.activeSelf?heldKind:null;
        string heldKind;Animator namBody;Vector3 heldSize,heldCentre;readonly List<GameObject> stowedForCarry=new List<GameObject>();
        // What each trip is. Day 1's three crates into the store are exactly what Day 3 takes out of it again:
        // the ammunition and the medicine to the south shelter (D3_01), the grenade crate to the bank hole (D3_02).
        static string TripKind(string task,int trip)=>task=="D3_02"?"grenades":task=="D3_01"?(trip==0?"medkit":"ammo"):trip==0?"ammo":trip==1?"medkit":"grenades";
        static float Depth(string kind)=>kind=="grenades"?.35f:kind=="medkit"?.31f:.15f;   // along the row
        string CargoKind()=>TripKind(State.carry,State.Count(State.carry));
        static readonly string[] PickupTasks={};   // Day 1's crates are the Day 1 props; Day 3's are taken from the store's stock on the cart
        readonly Dictionary<string,(GameObject pile,string key)> pickups=new Dictionary<string,(GameObject,string)>(),delivered=new Dictionary<string,(GameObject,string)>();
        static readonly string[] DeliveryTasks={"D3_01"};
        bool oldPlaceholdersHidden;
        /// <summary>The pre-chapter quest layout's grey blocks (Storage, South stores, Contact table...) mean nothing to the
        /// chapter; only their pictures go, the objects and their logic stay.</summary>
        void HideOldPlaceholders()
        {
            if(oldPlaceholdersHidden)return;oldPlaceholdersHidden=true;
            foreach(var old in FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None))
                if(string.IsNullOrEmpty(old.chapterId))
                    foreach(var f in old.GetComponentsInChildren<MeshFilter>())if(f.sharedMesh!=null&&f.sharedMesh.name=="Cube")
                    {
                        // Out of sight and out of the way: the block's collider would otherwise stop Nam at an invisible box.
                        if(f.TryGetComponent<Renderer>(out var r))r.enabled=false;
                        foreach(var c in f.GetComponents<Collider>())c.enabled=false;
                    }
        }
        static List<string> Trips(string task,int first,int count){var kinds=new List<string>();for(int k=0;k<count;k++)kinds.Add(TripKind(task,first+k));return kinds;}
        /// <summary>A row of cargo, one after another along turn's forward with a hand's gap, centred on spot.</summary>
        GameObject Row(string name,List<string> kinds,Vector3 spot,Quaternion turn)
        {
            const float Gap=.08f;float length=kinds.Sum(Depth)+Gap*Mathf.Max(0,kinds.Count-1),along=-length/2;
            var pile=new GameObject(name);pile.transform.SetParent(root.transform);pile.transform.SetPositionAndRotation(spot,turn);
            for(int k=0;k<kinds.Count;k++)
            {
                var item=CargoArt(kinds[k]);item.transform.SetParent(pile.transform,false);bool box=item.name.EndsWith("art slot");item.name=kinds[k];
                along+=Depth(kinds[k])/2;item.transform.localPosition=Vector3.forward*along;along+=Depth(kinds[k])/2+Gap;
                item.transform.localRotation=Quaternion.Euler(0,5*k-5,0);
                if(box)item.transform.localPosition+=Vector3.up*item.transform.localScale.y/2; // A plain box stands on its base.
            }
            return pile;
        }
        /// <summary>The supplies still waiting to be fetched lie beside the pickup marker, so there is something to see and outline.</summary>
        void RefreshPickups()
        {
            var def=Map02ChapterDefinition.Load();
            foreach(var id in PickupTasks)
            {
                var t=def.Find(id);if(t==null)continue;
                int first=t.kind=="carry"?State.Count(id)+(State.carry==id?1:0):0;
                int left=t.kind=="carry"?t.count-first:State.carry==id||State.introduced.Contains(id)?0:1;
                string key=State.Available(t)&&left>0?first+"/"+left:"";
                pickups.TryGetValue(id,out var shown);
                if(shown.key==key&&(key==""||shown.pile!=null))continue;
                if(shown.pile!=null)Destroy(shown.pile);
                if(key==""){pickups[id]=(null,"");continue;}
                // Beside the marker, not under Nam's feet.
                var spot=sites["arrival"].position+Vector3.right*1.5f;
                if(NavMesh.SamplePosition(spot,out var ground,1.5f,NavMesh.AllAreas))spot=ground.position;   // the ground, never a crate or roof
                pickups[id]=(Row("Waiting to be fetched: "+id,Trips(id,first,left),spot,Quaternion.identity),key);
            }
            RefreshStoreStock(def);
            // What has been delivered stays where it was set down: Day 1's crates in the store, Day 3's at the south shelter.
            foreach(var id in DeliveryTasks)
            {
                var t=def.Find(id);if(t==null)continue;
                int done=State.Done(id)?t.count:Mathf.Min(State.Count(id),t.count);
                string key=done>0?done.ToString():"";
                delivered.TryGetValue(id,out var shown);
                if(shown.key==key&&(key==""||shown.pile!=null))continue;
                if(shown.pile!=null)Destroy(shown.pile);
                if(key==""){delivered[id]=(null,"");continue;}
                var spot=sites[t.site].position+Vector3.back*1.3f;
                if(NavMesh.SamplePosition(spot,out var ground,1.5f,NavMesh.AllAreas))spot=ground.position;
                delivered[id]=(Row("Delivered: "+id,Trips(id,0,done),spot,Quaternion.identity),key);
            }
        }
        GameObject storeStock;string storeStockKey;
        /// <summary>The store's stock lies on the straw cart: what Day 1 brought in from the dock, less what Day 3 has
        /// taken (or is carrying) to the south shelter and the bank hole. Day 3's pickups are fetched from it.</summary>
        void RefreshStoreStock(Map02ChapterDefinition def)
        {
            // Until Day 3 the Day 1 crates themselves stand in the store; from Day 3 the cart holds that stock.
            foreach(var crate in physicalCrates)if(crate!=null&&State.phase>=4)crate.gameObject.SetActive(false);
            if(State.phase<4){if(storeStock!=null)Destroy(storeStock);storeStock=null;storeStockKey=null;return;}
            var bring=def.Find("D1_04");var take=def.Find("D3_01");
            var stock=bring==null?new List<string>():Trips("D1_04",0,State.Done("D1_04")?bring.count:Mathf.Min(State.Count("D1_04"),bring.count));
            if(take!=null)
                foreach(var kind in Trips("D3_01",0,State.Done("D3_01")?take.count:Mathf.Min(State.Count("D3_01")+(State.carry=="D3_01"?1:0),take.count)))
                    stock.Remove(kind);   // the first of its kind goes
            if(State.carry=="D3_02"||State.introduced.Contains("D3_02")||State.Done("D3_02"))stock.Remove("grenades");
            string key=string.Join(",",stock);
            if(key==storeStockKey&&(key==""||storeStock!=null))return;
            if(storeStock!=null)Destroy(storeStock);storeStock=null;storeStockKey=key;
            if(key==""||!StrawCartTop(out var top,out var turn))return;
            storeStock=Row("The store's stock on the cart",stock,top,turn);
        }
        public GameObject StrawCart {get;private set;}
        /// <summary>The straw cart beside the store (where the old placeholder block stood) and the top of its load.</summary>
        bool StrawCartTop(out Vector3 top,out Quaternion turn)
        {
            top=default;turn=Quaternion.identity;
            if(StrawCart==null)
            {
                if(Layout?.strawCartArt==null)return false;
                var at=sites["store"].position+Vector3.right*1.6f;
                if(NavMesh.SamplePosition(at,out var ground,1.5f,NavMesh.AllAreas))at=ground.position;
                StrawCart=Instantiate(Layout.strawCartArt,at,Quaternion.Euler(0,90,0),root.transform);StrawCart.name="Straw cart by the store";
                Physics.SyncTransforms();
            }
            turn=StrawCart.transform.rotation;var ray=new Ray(StrawCart.transform.position+Vector3.up*4,Vector3.down);float best=float.PositiveInfinity;
            foreach(var c in StrawCart.GetComponentsInChildren<Collider>())if(c.Raycast(ray,out var hit,6)&&hit.distance<best){best=hit.distance;top=hit.point;}
            if(float.IsPositiveInfinity(best))top=StrawCart.transform.position+Vector3.up*1.1f;
            return true;
        }
        /// <summary>What the outline should hug for this marker: the waiting supplies, while they wait for Nam.</summary>
        public Transform HighlightFor(Map02ObjectiveObject marker)
        {
            if(marker==null||string.IsNullOrEmpty(marker.chapterId)||State.carry==marker.chapterId)return null;
            // Day 3's pickups: only the item that this E would lift, on the cart.
            string next=marker.chapterId=="D3_01"?TripKind("D3_01",State.Count("D3_01")):marker.chapterId=="D3_02"&&!State.introduced.Contains("D3_02")?"grenades":null;
            if(next!=null)return storeStock!=null?storeStock.transform.Find(next):null;
            // Day 1: the next crate at the landing.
            if(marker.chapterId=="D1_04"){int i=State.Count("D1_04");return i<physicalCrates.Count?physicalCrates[i]:null;}
            return pickups.TryGetValue(marker.chapterId,out var p)&&p.pile!=null?p.pile.transform:null;
        }
        GameObject CargoArt(string kind)
        {
            var art=kind=="grenades"?Layout?.grenadeCrateArt:kind=="medkit"?Layout?.medkitArt:Layout?.ammoCanArt;
            if(art!=null){var go=Instantiate(art,root.transform);go.name=kind+" carried by Nam";return go;}
            var size=kind=="grenades"?new Vector3(.5f,.26f,.35f):kind=="medkit"?new Vector3(.33f,.3f,.12f):new Vector3(.31f,.23f,.15f);
            var box=Placeholder(kind+" carried by Nam — art slot",size,kind=="grenades"?new Color(.36f,.3f,.22f):kind=="medkit"?new Color(.5f,.45f,.3f):new Color(.3f,.22f,.16f),Vector3.zero);
            return box;
        }
        // Whatever Nam is carrying sits against his chest with both hands on its sides (there is no carry clip).
        // Runs after the animator, so the arms are posed on top of the walk.
        void HoldCargo()
        {
            bool show=!string.IsNullOrEmpty(State.carry)&&State.carry!="D1_04"&&sceneTask==null;   // Day 1's crate is carried by the Day 1 pose
            string kind=show?CargoKind():heldKind;
            if(HeldCargo!=null&&(!show||heldKind!=kind))HeldCargo.SetActive(false);
            if(!show)
            {
                // The gun comes back once the load is down; a scene puts away and returns its own.
                if(sceneTask==null&&stowedForCarry.Count>0){foreach(var w in stowedForCarry)if(w!=null)w.SetActive(true);stowedForCarry.Clear();}
                return;
            }
            if(HeldCargo==null||heldKind!=kind)
            {
                if(HeldCargo!=null)Destroy(HeldCargo);heldKind=kind;HeldCargo=CargoArt(kind);
                HeldCargo.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                var renderers=HeldCargo.GetComponentsInChildren<Renderer>();var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
                heldSize=b.size;heldCentre=b.center;
            }
            HeldCargo.SetActive(true);
            // Both hands are on the load (a restored save too): the gun in hand is put out of sight until it is set down.
            foreach(var weapon in Mission.Player.GetComponentsInChildren<Weapon>())if(weapon.gameObject.activeSelf){stowedForCarry.Add(weapon.gameObject);weapon.gameObject.SetActive(false);}
            if(namBody==null)namBody=Mission.Player.GetComponentInChildren<Animator>();
            var chest=namBody!=null&&namBody.isHuman?namBody.GetBoneTransform(HumanBodyBones.Chest):null;
            var body=Mission.Player;var size=heldSize;
            float height=chest!=null?chest.position.y-body.position.y-.12f-size.y/2:1.05f;
            var center=body.position+Vector3.up*height+body.forward*(.2f+size.z/2);
            HeldCargo.transform.SetPositionAndRotation(center-body.rotation*heldCentre,body.rotation);
            if(chest==null)return;
            for(int pass=0;pass<2;pass++)
                foreach(bool left in new[]{true,false})
                {
                    float side=left?-1:1;var shoulder=namBody.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
                    if(shoulder==null)continue;
                    var palm=center+body.right*side*(size.x/2+.035f)-body.up*.03f;
                    Map01ArmContact.Reach(namBody,left,palm,shoulder.position+body.right*side*.3f-body.up*.3f,1);
                }
        }

        void TickDressing(float dt)
        {
            HideOldPlaceholders();RefreshStowedCharge();StrawCartTop(out _,out _);RefreshPickups();TickFlare();TickHouseFire();TickTamCover(dt);
        }
        void RefreshStowedCharge()
        {
            // The crate stays in the hole; the bundle is gone once Hùng pulls it out in the farewell.
            bool placed=State.Done("D3_02")||sceneTask?.id=="D3_02"||State.introduced.Contains("D3_02");
            bool bundleTaken=State.Done("N3_07")||AfterLine("N3_07","m2v3_farewell_06");
            if(placed&&CrateProp==null)
            {
                var hole=sites["ditch"].position+Vector3.right*1.2f+Vector3.back*.6f;
                if(Layout?.grenadeCrateArt!=null){CrateProp=Instantiate(Layout.grenadeCrateArt,root.transform);CrateProp.name="Grenade crate in the bank hole";CrateProp.transform.SetPositionAndRotation(hole,Quaternion.Euler(0,14,0));}
                else CrateProp=Placeholder("Grenade crate in the bank hole",new Vector3(.7f,.45f,.45f),new Color(.24f,.27f,.16f),hole+Vector3.up*.22f);
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
