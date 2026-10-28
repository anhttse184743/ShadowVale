using UnityEngine;
namespace ShadowVale.Map01
{
    /// <summary>Authored damage presentation; cosmetic smoke never grants stealth cover.</summary>
    public sealed class Map01SabotageProps:MonoBehaviour
    {
        public Renderer[] healthyRadio;
        public GameObject damagedRadio,healthyPower,damagedPower;
        public Transform powerApproach,powerRightContact,powerLeftContact,smokeOrigin;
        ParticleSystem smoke;
        bool smoking;
        public bool Smoking=>smoking&&smoke!=null&&smoke.isPlaying;
        public void Present(bool radioBroken,bool powerBroken){
            foreach(var r in healthyRadio)if(r!=null)r.enabled=!radioBroken;
            if(damagedRadio!=null)damagedRadio.SetActive(radioBroken);
            if(healthyPower!=null)healthyPower.SetActive(!powerBroken);
            if(damagedPower!=null)damagedPower.SetActive(powerBroken);
            if(!Application.isPlaying)return;
            if(powerBroken&&!smoking){
                if(smoke==null){
                    smoke=Map01Fx.System(smokeOrigin,"Burnt power supply — cosmetic smoke",smokeOrigin.position,Map01Fx.Unlit,5,48);
                    var main=smoke.main;main.loop=true;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;main.startLifetime=new ParticleSystem.MinMaxCurve(2.5f,4);main.startSize=new ParticleSystem.MinMaxCurve(.18f,.28f);
                    main.startSpeed=new ParticleSystem.MinMaxCurve(.3f,.55f);main.startColor=new ParticleSystem.MinMaxGradient(new Color(.40f,.41f,.39f,.65f),new Color(.55f,.55f,.52f,.75f));
                    var emission=smoke.emission;emission.rateOverTime=7;
                    var shape=smoke.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=12;shape.radius=.055f;shape.rotation=new Vector3(-90,0,0);
                    Map01Fx.Fade(smoke,.12f,.65f,.65f);Map01Fx.Grow(smoke,.65f,3.6f);Map01Fx.Billow(smoke,.12f,.45f);Map01Fx.Wind(smoke,new Vector3(-.04f,.12f,.02f));
                }
                smoke.Play();smoking=true;
            }else if(!powerBroken&&smoking){smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);smoking=false;}
        }
    }
}
