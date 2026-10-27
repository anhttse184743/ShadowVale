using System;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.AI.Perception;
using ShadowVale.Gameplay.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    public sealed partial class Map02Mission
    {
        public Map01Inventory Bag {get;private set;}
        public Map01Hud CharacterHud {get;private set;}
        public ForestSettings CharacterSettings {get;private set;}
        public ForestBundle CharacterContent {get;private set;}
        public Camera GameCamera=>cameraRig!=null?cameraRig.GetComponent<Camera>():Camera.main;
        public float Stamina {get;private set;}
        public bool CharacterGearLocked=>!string.IsNullOrEmpty(Chapter?.State.carry)||Chapter?.CompanionLocksGear==true||Chapter?.DayActivityActive==true||Chapter?.MovementLocked==true;
        public bool CharacterCanHeal=>!CharacterGearLocked||Chapter?.ScoutEscortActive==true;
        public bool CharacterCanSprint=>Chapter?.MovementLocked!=true&&Stamina>2&&Chapter?.State.wounded!=true;
        // The player's panel still blocks controls, but field crafting must leave enemies active.
        public bool CharacterWorldBlocked=>!Ready||Paused||Cinematic||Chapter?.LocksInput==true||World.story.failed
            ||(Panel!=0&&!(Panel==1&&CharacterHud?.FieldCraftingOpen==true));
        public string HudMessage=>message;
        public bool HudMessageVisible=>Time.unscaledTime<messageUntil;
        public readonly List<ForestPoint> CharacterWorkpoints=new List<ForestPoint>();
        bool syncingBag;readonly Dictionary<string,int> bagOverflow=new Dictionary<string,int>();
        readonly List<Map02GroundItem> groundItems=new List<Map02GroundItem>();
        public IReadOnlyList<Map02GroundItem> GroundItems=>groundItems;
        public MapExploration Exploration {get;private set;}
        public Material CharacterTrail {get;private set;}
        [Serializable] public sealed class GroundSnapshot {public string id;public int count;public float condition;public Vector3 position;}
        public bool IsExplored(Vector3 at)=>Exploration!=null&&Exploration.IsExplored(at);

        void InitializeCharacter()
        {
            var data=Resources.Load<CharacterHudData>("Hud/CharacterHudData");
            if(data==null||data.balance==null||data.content==null)throw new InvalidOperationException("Missing shared character HUD data.");
            CharacterSettings=JsonUtility.FromJson<ForestSettings>(data.balance.text);
            CharacterContent=JsonUtility.FromJson<ForestBundle>(data.content.text);
            Stamina=arrival?.CharacterBag!=null?arrival.CharacterStamina:CharacterSettings.stamina;
            Exploration=new MapExploration(Rect.MinMaxRect(-Map02Navigation.HalfSize,-Map02Navigation.HalfSize,Map02Navigation.HalfSize,Map02Navigation.HalfSize));
            Bag=gameObject.AddComponent<Map01Inventory>();
            var skills=player.GetComponent<ShadowVale.Gameplay.Skills.PlayerSkills>();if(skills==null)player.gameObject.AddComponent<ShadowVale.Gameplay.Skills.PlayerSkills>();
            InitializeSkills();
            CharacterTrail=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            Map01Pistol.Install(CharacterHudSession.For(gameObject));Map01Mosin.Install(CharacterHudSession.For(gameObject));
            gameObject.AddComponent<Map01StoneThrow>();
            SyncBagFromTotals(true,arrival?.CharacterBag);
            Bag.Changed+=WriteBagTotals;
            Bag.RestoreFieldToolkit(arrival?.CharacterBag?.fieldKitVersion ?? 0);
            CharacterHud=gameObject.AddComponent<Map01Hud>();
            InitializeCharacterEffects();
            Map01NamActions.For(CharacterHudSession.For(gameObject));
            NamProneMotion.Install(CharacterHudSession.For(gameObject));
            combat.SlotPressed=slot=>{if(CharacterGearLocked){Say("Hoàn tất việc đang mang trước khi đổi trang bị.");return true;}return Bag.OnSlotKey(slot);};
            combat.ReserveRounds=()=>Bag.Count(Bag.HeldAmmoId);
            combat.DrawRounds=requested=>{int n=Mathf.Min(requested,Bag.Count(Bag.HeldAmmoId));Bag.Spend(Bag.HeldAmmoId,n);return n;};
            combat.GunUsable=()=>!Bag.HeldGunBroken();
            combat.InputAllowed=()=>!InputBlocked&&!Bag.SuppressFire&&!Bag.ThrowableEquipped;
            combat.Attacked+=CharacterAttack;
            player.SprintAllowed=()=>CharacterCanSprint;
            var bench=new GameObject("Map 2 shared workbench access");bench.transform.SetParent(transform);
            bench.transform.position=Chapter.Sites["store"].position;
            var point=bench.AddComponent<ForestPoint>();point.kind=ForestPointKind.Workbench;point.label="Bàn chế tạo / sửa súng";CharacterWorkpoints.Add(point);
        }
        void CharacterAttack(WeaponKind kind,Vector3 at)
        {
            var held=Bag.HeldGun();if(held!=null)Bag.Wear(held.Value,Bag.WearPerShot(Bag.IdOf(Map01Inventory.Gun.InSlot(held.Value))));
        }
        public void SyncBagFromTotals(bool initial=false,Map01Inventory.Layout layout=null)
        {
            if(Bag==null||syncingBag)return;
            var totals=Bag.Items;
            if(!initial&&Inventory.All(p=>p.Value==((totals.TryGetValue(p.Key,out int n)?n:0)+(bagOverflow.TryGetValue(p.Key,out int extra)?extra:0)))&&totals.All(p=>Inventory.TryGetValue(p.Key,out int n)&&n>=p.Value))return;
            syncingBag=true;
            try
            {
                var previous=layout??Bag.CaptureLayout();
                var items=Inventory.Select(p=>new ForestIngredient{item_id=p.Key,count=p.Value}).ToList();
                // Legacy Map 2 snapshots did not list carried weapons. Preserve the actual rifle ID.
                if(!items.Any(i=>Map01Inventory.SlotOf(i.item_id)==Map01Inventory.Slot.Primary))items.Add(new ForestIngredient{item_id=RifleItemId,count=initial&&layout==null?1:0});
                Bag.RestoreFromSave(items.ToArray(),Stones,null,0);
                if(!initial||layout!=null)Bag.RestoreLayout(previous);
                bagOverflow.Clear();foreach(var item in items){int extra=item.count-Bag.Count(item.item_id);if(extra>0)bagOverflow[item.item_id]=extra;}
            }
            finally{syncingBag=false;}
            WriteBagTotals();
        }
        void WriteBagTotals()
        {
            if(syncingBag||Bag==null)return;
            Inventory.Clear();foreach(var item in Bag.Items)Inventory[item.Key]=item.Value;
            foreach(var extra in bagOverflow)Inventory[extra.Key]=(Inventory.TryGetValue(extra.Key,out int n)?n:0)+extra.Value;
            Stones=Bag.Stones;
        }
        Item[] CaptureCharacterItems()
        {
            if(Bag==null)return Inventory.Select(p=>new Item{id=p.Key,count=p.Value}).ToArray();
            var items=Bag.CaptureItems().ToDictionary(i=>i.item_id,i=>i.count);
            foreach(var extra in bagOverflow)items[extra.Key]=(items.TryGetValue(extra.Key,out int n)?n:0)+extra.Value;
            return items.OrderBy(i=>i.Key).Select(i=>new Item{id=i.Key,count=i.Value}).ToArray();
        }
        void TickCharacter()
        {
            SyncBagFromTotals();
            if(Exploration.Reveal(Player.position,14))RefreshExplorationTexture();
            if(InputBlocked)return;
            bool running=player.IsSprinting&&player.IsMoving;
            Stamina=Mathf.Clamp(Stamina+(running?-CharacterSettings.staminaDrain:CharacterSettings.staminaRecovery)*Time.deltaTime,0,CharacterSettings.stamina);
            var kb=Keyboard.current;if(kb==null)return;
            if(Bag.ThrowableEquipped&&!CharacterGearLocked)
            {if(kb.digit1Key.wasPressedThisFrame)Bag.OnSlotKey(0);else if(kb.digit2Key.wasPressedThisFrame)Bag.OnSlotKey(1);else if(kb.digit3Key.wasPressedThisFrame)Bag.OnSlotKey(2);else if(kb.digit4Key.wasPressedThisFrame)Bag.OnSlotKey(3);}
            if(Mouse.current==null||!Mouse.current.leftButton.isPressed)Bag.SuppressFire=false;
            if(kb.hKey.wasPressedThisFrame&&CharacterCanHeal)Bag.UseItem("medkit_small");
            if(kb.backquoteKey.wasPressedThisFrame)CharacterHud.ToggleObjective();
            var throwing=GetComponent<Map01StoneThrow>();
            if(Bag.ThrowableEquipped&&!CharacterGearLocked&&!Bag.SuppressFire&&Mouse.current!=null){if(Mouse.current.rightButton.wasPressedThisFrame)throwing.Cancel();else if(Mouse.current.leftButton.wasPressedThisFrame)throwing.BeginAim();else if(Mouse.current.leftButton.wasReleasedThisFrame)throwing.Release();}
        }
        Map02GroundItem NearestGroundItem()=>groundItems.Where(g=>g!=null&&Vector3.Distance(Player.position,g.transform.position)<3).OrderBy(g=>(g.transform.position-Player.position).sqrMagnitude).FirstOrDefault();
        void TryCharacterInteraction()
        {
            var item=NearestGroundItem();
            if(item!=null){PickupCharacterItem(item);return;}
            if(!CharacterGearLocked&&Bag.NearWorkbench())SetCharacterPanel(4);
        }
        void RestoreCharacter(Snapshot data)
        {
            if(Bag==null)return;
            Stamina=data.hasCharacterState?Mathf.Clamp(data.stamina,0,CharacterSettings.stamina):CharacterSettings.stamina;
            var layout=data.bagLayout;
            // rifleItemId remains authoritative for old saves and existing weapon-switch callers.
            if(layout!=null&&(layout.primary=="rifle_standard"||layout.primary==Map01M16.Id)&&layout.primary!=RifleItemId)
            {var old=layout.primary;if(Inventory.TryGetValue(old,out int count)&&count>0)Inventory[old]=count-1;Inventory[RifleItemId]=(Inventory.TryGetValue(RifleItemId,out int n)?n:0)+1;layout=JsonUtility.FromJson<Map01Inventory.Layout>(JsonUtility.ToJson(layout));layout.primary=RifleItemId;}
            SyncBagFromTotals(true,layout);
            if(!string.IsNullOrEmpty(data.crafting)){var savedLayout=Bag.CaptureLayout();Bag.RestoreFromSave(Bag.CaptureItems(),Stones,data.crafting,data.craftRemaining);Bag.RestoreLayout(savedLayout);}
            Exploration.Restore(data.exploration);Exploration.Reveal(Player.position,14);RefreshExplorationTexture();
            foreach(var item in groundItems)if(item!=null)Destroy(item.gameObject);groundItems.Clear();
            foreach(var saved in data.ground??Array.Empty<GroundSnapshot>()){DropCharacterItem(saved.id,saved.count,saved.condition);groundItems[groundItems.Count-1].transform.position=saved.position;}
            Bag.RestoreFieldToolkit(data.bagLayout?.fieldKitVersion ?? 0);
            player.SprintAllowed=()=>CharacterCanSprint;
            GetComponent<Map01StoneThrow>()?.Cancel();
        }
        public int CharacterNoise(Vector3 point,float radius,NoiseKind kind)
        {
            int count=0;foreach(var enemy in Chapter.Enemies)if(enemy!=null&&!enemy.Dead&&enemy.gameObject.activeInHierarchy&&Vector3.Distance(enemy.transform.position,point)<radius){enemy.Hear(point,kind!=NoiseKind.Stone);count++;}return count;
        }
        public (int killed,int wounded) CharacterExplosion(Vector3 point)
        {
            int killed=0,wounded=0;foreach(var enemy in Chapter.Enemies.Where(e=>e!=null&&!e.Dead&&e.gameObject.activeInHierarchy))
            {float d=Vector3.Distance(enemy.transform.position,point);if(d>Map01StoneThrow.WoundRadius)continue;enemy.HitPoints.TakeDamage(enemy.HitPoints.Max*(d<=Map01StoneThrow.KillRadius?4:.6f),point,Player.gameObject);if(d<=Map01StoneThrow.KillRadius)killed++;else wounded++;}return(killed,wounded);
        }
        public void SetCharacterPanel(int panel)
        {
            Panel=panel;player.RestoreMotion(false);Bag.SuppressFire=true;
            UpdatePointerMode();
        }
        public void DropCharacterItem(string id,int count,float condition)
        {
            var go=new GameObject("Dropped "+id);go.transform.position=Player.position+Player.forward+Vector3.up*.2f;
            var item=go.AddComponent<Map02GroundItem>();item.id=id;item.count=count;item.condition=condition;
            var visual=GameObject.CreatePrimitive(PrimitiveType.Cube);visual.transform.SetParent(go.transform,false);visual.transform.localScale=Vector3.one*.2f;
            Destroy(visual.GetComponent<Collider>());groundItems.Add(item);
        }
        public bool PickupCharacterItem(Map02GroundItem item)
        {
            if(item==null||!groundItems.Contains(item)||Vector3.Distance(Player.position,item.transform.position)>3)return false;
            groundItems.Remove(item);Bag.Receive(item.id,item.count,item.condition);Destroy(item.gameObject);return true;
        }
        void DestroyCharacter()
        {
            if(combat!=null)combat.Attacked-=CharacterAttack;
            foreach(var item in groundItems)if(item!=null)Destroy(item.gameObject);
            foreach(var texture in new[]{explorationFog,characterMapRim,characterMapDisk,characterAlly,characterEnemy})if(texture!=null)Destroy(texture);
            if(CharacterTrail!=null)Destroy(CharacterTrail);
        }
    }
    public sealed class Map02GroundItem:MonoBehaviour {public string id;public int count;public float condition;}
}
