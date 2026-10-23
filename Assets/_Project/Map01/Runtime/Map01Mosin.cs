using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>Mosin upper body, separate bolt/cartridge, and the selected PU3.5 scope mode.</summary>
    [DefaultExecutionOrder(41)]
    public sealed class Map01Mosin : MonoBehaviour
    {
        CharacterHudSession mission;PlayerCombat combat;Map01NamActions actions;Animator actor;
        Map01PistolAnimationData data;Transform[] bones,parts;Transform rightHand,gun,round;
        ForearmTwist twist;ThirdPersonCamera cameraRig;
        bool ownsTwist,scoped,wasReloading;float weight,aim,clock,shotAt=-100;
        Texture2D scopeMask;
        public Weapon Weapon {get;private set;}
        public float SourceFrame {get;private set;}=1;
        public float PoseWeight=>weight;
        public bool Scoped=>scoped;
        // Insertion presses in the authored five-round take, normalized to its full five seconds.
        public static float[] InsertionTimes=>new[]{.297f,.415f,.533f,.651f,.769f};
        public static Map01Mosin Install(Map01Mission owner)=>Install(CharacterHudSession.For(owner.gameObject));
        public static Map01Mosin Install(CharacterHudSession owner)
        {
            var existing=owner.player.GetComponent<Map01Mosin>();if(existing!=null){existing.mission=owner;existing.combat=owner.ModernCombat;existing.actions=Map01NamActions.For(owner.MapOne);existing.cameraRig=owner.gameCamera.GetComponent<ThirdPersonCamera>();return existing;}
            if(owner.Bundle?.weapons==null||!owner.Bundle.weapons.Any(w=>w!=null&&w.id=="rifle_mosin"))return null;
            var asset=Resources.Load<Map01PistolAnimationData>("Weapons/MosinV13");
            if(asset==null){Debug.LogError("Mosin motion asset is missing. Build ShadowVale/Mosin/Build motion assets.");return null;}
            var result=owner.player.gameObject.AddComponent<Map01Mosin>();result.Bind(owner,asset);return result;
        }
        void Bind(CharacterHudSession owner,Map01PistolAnimationData asset)
        {
            mission=owner;data=asset;combat=owner.ModernCombat;actor=GetComponentInChildren<Animator>();actions=Map01NamActions.For(owner.MapOne);cameraRig=owner.gameCamera.GetComponent<ThirdPersonCamera>();
            var map=actor.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
            bones=data.bones.Select(t=>map[t.name]).ToArray();rightHand=map["RightHand"];
            gun=Instantiate(data.prefab,rightHand,false).transform;gun.name="Mosin PU v13";
            parts=data.parts.Select(t=>gun.Find(t.name)).ToArray();round=parts.FirstOrDefault(t=>t!=null&&t.name=="Cartridge");
            Weapon=gun.GetComponent<Weapon>();var profile=owner.Bundle.weapons.First(w=>w.id=="rifle_mosin");
            Weapon.Configure(WeaponKind.Sniper,profile.damage,profile.range,profile.fire_rate,false,gun.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Mosin_Muzzle"));
            combat.RegisterWeapon(Weapon,new WeaponTuning{magazineCapacity=profile.magazine_size,reloadSeconds=profile.reload_seconds,individualReloadCommits=InsertionTimes});
            twist=GetComponentInChildren<ForearmTwist>();combat.Attacked+=OnShot;
        }
        void OnShot(WeaponKind kind,Vector3 point){if(kind==WeaponKind.Sniper)shotAt=Time.time;}
        bool Available=>mission!=null&&combat!=null&&combat.EquippedKind==WeaponKind.Sniper&&actor!=null&&actor.isActiveAndEnabled&&!mission.Cinematic
            &&!(mission.ModernHealth?.IsDead??false)&&GetComponent<PlayerController>()?.IsProne!=true&&!(mission.Binoculars?.Binoculars??false)&&!(actions!=null&&(actions.Busy||actions.StoneActionPlaying));
        void Update()
        {
            if(data==null||cameraRig==null)return;
            if(mission==null){scoped=false;cameraRig.SetWeaponScope(false,3.5f,0);return;}
            bool wants=Available&&mission.CameraInputEnabled&&!mission.InventoryOpen&&!mission.MapOpen&&!ForestMenu.Visible&&combat.IsAiming&&!combat.IsReloading&&Time.time-shotAt>=Weapon.Cooldown&&aim>.95f;
            scoped=wants;var scouting=mission.GetComponent<Map01Scouting>();float eye=scouting!=null?scouting.Eye.y-transform.position.y:actor.GetBoneTransform(HumanBodyBones.Head).position.y-transform.position.y;
            cameraRig.SetWeaponScope(scoped,3.5f,eye);
        }
        void LateUpdate()
        {
            if(data==null)return;
            if(GetComponent<PlayerController>()?.IsProne==true && GetComponent<NamProneMotion>()?.CanReloadProne==true) {
                weight=0;ReleaseTwist();wasReloading=false;
                if(gun.parent!=rightHand)gun.SetParent(rightHand,false);
                data.weapon.At(101).Apply(gun);
                for(int i=0;i<parts.Length;i++)if(parts[i]!=null)data.parts[i].At(1).Apply(parts[i]);
                if(round!=null)round.gameObject.SetActive(false);
                return;
            }
            bool active=Available,interrupted=combat.EquippedKind==WeaponKind.Sniper&&!active;
            if(interrupted)combat.CancelReload();
            weight=interrupted?0:Mathf.MoveTowards(weight,active?1:0,Time.deltaTime/combat.StanceBlendSeconds);
            if(twist!=null){if(weight>0&&!ownsTwist){ownsTwist=true;twist.BeginAuthoredPose();}else if(weight<=0)ReleaseTwist();}
            if(weight<=0){if(combat.EquippedKind==WeaponKind.Sniper)combat.GunPresentationReady=false;return;}
            clock+=Time.deltaTime;bool levelled=combat.IsAiming||Time.time-shotAt<Weapon.Cooldown;
            aim=Mathf.MoveTowards(aim,levelled?1:0,Time.deltaTime/.4f);
            if(combat.IsReloading)
            {
                float u=combat.FullReloadProgress;int count=combat.ReloadPlannedRounds;
                if(u>=.22f+.118f*count)u+=.118f*(5-count);
                SourceFrame=Mathf.Lerp(251,551,u);
            }
            else if(wasReloading)SourceFrame=551;
            else if(Time.time-shotAt<Weapon.Cooldown)SourceFrame=Mathf.Lerp(163,249,(Time.time-shotAt)/Weapon.Cooldown);
            else if(aim>0&&aim<1)SourceFrame=levelled?Mathf.Lerp(63,99,aim):Mathf.Lerp(553,589,1-aim);
            else SourceFrame=aim>=1?101+Mathf.Repeat(clock*60,60):1+Mathf.Repeat(clock*60,60);
            wasReloading=combat.IsReloading;
            if(combat.EquippedKind==WeaponKind.Sniper)combat.GunPresentationReady=active&&!combat.IsReloading&&(aim>=.95f||Time.time-shotAt<Weapon.Cooldown);
            for(int i=0;i<bones.Length;i++)data.bones[i].At(SourceFrame).Apply(bones[i],weight);
            if(gun.parent!=rightHand)gun.SetParent(rightHand,false);data.weapon.At(SourceFrame).Apply(gun);
            for(int i=0;i<parts.Length;i++)if(parts[i]!=null)data.parts[i].At(SourceFrame).Apply(parts[i]);
            if(round!=null&&data.roundVisible!=null&&data.roundVisible.Length>0)round.gameObject.SetActive(data.roundVisible[Mathf.Clamp(Mathf.RoundToInt(SourceFrame)-1,0,data.roundVisible.Length-1)]);
        }
        void OnGUI()
        {
            if(!scoped)return;
            if(scopeMask==null)
            {
                scopeMask=new Texture2D(512,512,TextureFormat.RGBA32,false){name="PU scope edge",filterMode=FilterMode.Bilinear};var pixels=new Color32[512*512];
                for(int y=0;y<512;y++)for(int x=0;x<512;x++){float radius=Vector2.Distance(new Vector2(x+.5f,y+.5f),Vector2.one*256);pixels[y*512+x]=new Color(0,0,0,Mathf.Clamp01((radius-250)/3));}
                scopeMask.SetPixels32(pixels);scopeMask.Apply(false,true);
            }
            var matrix=GUI.matrix;int depth=GUI.depth;var color=GUI.color;GUI.matrix=Matrix4x4.identity;GUI.depth=0;GUI.color=Color.black;
            float size=Screen.height,x0=(Screen.width-size)*.5f,y0=0,cx=Screen.width*.5f,cy=Screen.height*.5f;
            GUI.DrawTexture(new Rect(0,0,x0,Screen.height),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(x0+size,0,Screen.width-x0-size,Screen.height),Texture2D.whiteTexture);
            GUI.color=Color.white;GUI.DrawTexture(new Rect(x0,y0,size,size),scopeMask);GUI.color=Color.black;
            float gap=size*.035f,line=Mathf.Max(2,size/540);
            GUI.DrawTexture(new Rect(cx-size*.45f,cy-line*.5f,size*.45f-gap,line),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx+gap,cy-line*.5f,size*.45f-gap,line),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx-line*.5f,cy+gap,line,size*.42f-gap),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx-line*.5f,cy-2*line,line,4*line),Texture2D.whiteTexture);
            GUI.matrix=matrix;GUI.depth=depth;GUI.color=color;
        }
        void ReleaseTwist(){if(ownsTwist&&twist!=null)twist.EndAuthoredPose();ownsTwist=false;}
        void OnDisable(){ReleaseTwist();scoped=false;if(cameraRig!=null)cameraRig.SetWeaponScope(false,3.5f,0);}
        void OnDestroy(){ReleaseTwist();if(combat!=null)combat.Attacked-=OnShot;if(scopeMask!=null)Destroy(scopeMask);}
    }
}
