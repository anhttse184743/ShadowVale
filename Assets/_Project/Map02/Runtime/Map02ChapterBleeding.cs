using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Nam's shoulder wound from the tank's burst (N3_WOUND) to the end of Night 3: he still runs, a little slower, and bleeds
    /// <see cref="BleedPerSecond"/> HP a second until a medkit (H) dresses it. Bleeding alone never takes him below
    /// <see cref="BleedFloor"/>: it wears him down, the soldiers do the killing. While it bleeds, drops fall from the shoulder
    /// and the screen pulses faintly red.
    /// </summary>
    public sealed partial class Map02ChapterDirector
    {
        public const float WoundedPace=.88f,BleedPerSecond=2,BleedFloor=20;
        ParticleSystem bleedDrops;float dropClock;bool bleedHinted,bleedHooked;
        public bool Bleeding=>State.phase==5&&State.wounded&&!State.bandaged&&!State.Done("N3_09");
        void TickBleeding(float dt)
        {
            if(!bleedHooked&&Mission.Bag!=null){Mission.Bag.ItemUsed+=OnItemUsed;bleedHooked=true;}
            if(!Bleeding||sceneTask!=null||Mission.Paused||health==null||health.IsDead||health.CinematicInvulnerable)return;
            if(health.Current>BleedFloor)health.RestoreHealth(Mathf.Max(BleedFloor,health.Current-BleedPerSecond*dt));
            if(!bleedHinted){bleedHinted=true;Mission.Say("Vai trái đang chảy máu — nhấn H dùng băng cứu thương để cầm máu.");}
            dropClock-=dt;if(dropClock<=0){dropClock=.45f;Drip();}
        }
        void OnItemUsed(string id)
        {
            if(id!="medkit_small"||!Bleeding)return;
            State.bandaged=true;Mission.Say("Đã băng vai, máu đã cầm.");
        }
        void Drip()
        {
            var body=Mission.Player.GetComponentInChildren<Animator>();var arm=body!=null&&body.isHuman?body.GetBoneTransform(HumanBodyBones.LeftUpperArm):null;
            var at=arm!=null?arm.position:Mission.Player.position+Vector3.up*1.35f-Mission.Player.right*.2f;
            if(bleedDrops==null)
            {
                bleedDrops=Map01Fx.System(root.transform,"Shoulder bleeding",at,Map01Fx.Unlit,.5f,30);
                var main=bleedDrops.main;main.startLifetime=.8f;main.startSpeed=.15f;main.startSize=.035f;main.gravityModifier=1;main.startColor=new Color(.38f,.02f,.02f,.95f);
                var shape=bleedDrops.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.04f;
            }
            bleedDrops.transform.position=at;bleedDrops.Emit(2);
        }
    }
}
