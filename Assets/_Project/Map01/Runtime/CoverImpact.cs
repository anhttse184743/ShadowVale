using ShadowVale.Gameplay.Audio;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// A rifle round striking cover: a puff of dust and grit off the surface, a couple of sparks, and the smack of it going
    /// in. Pooled like <see cref="ShadowVale.Gameplay.Combat.BloodImpact"/>. Shows Nam that a wall is taking the fire, and where
    /// it comes from.
    /// </summary>
    public sealed class CoverImpact : MonoBehaviour
    {
        static CoverImpact instance;
        static AudioClip smack;
        readonly ParticleSystem[] dust=new ParticleSystem[10],sparks=new ParticleSystem[10];
        int cursor;

        public static void Play(Vector3 point,Vector3 normal)
        {
            if(!Application.isPlaying)return;
            if(instance==null)instance=new GameObject("Cover impacts").AddComponent<CoverImpact>();
            instance.Emit(point,normal);
        }
        void Emit(Vector3 point,Vector3 normal)
        {
            dust[cursor]??=Burst("Grit",Map01Fx.Unlit,new Color(.42f,.36f,.27f,.9f),.25f,.7f,.6f,2.2f,.03f,.12f,.4f,14);
            sparks[cursor]??=Burst("Sparks",Map01Fx.Glow,new Color(1f,.78f,.4f,1f),.05f,.14f,3f,6f,.012f,.025f,.1f,4);
            foreach(var ps in new[]{dust[cursor],sparks[cursor]})
            {
                ps.transform.SetPositionAndRotation(point+normal*.02f,Quaternion.LookRotation(normal.sqrMagnitude>.01f?normal:Vector3.up));
                ps.Clear(true);ps.Emit(ps==dust[cursor]?Random.Range(8,13):Random.Range(1,4));
            }
            cursor=(cursor+1)%dust.Length;
            PooledAudioService.Resolve().PlayOneShot(Smack(),point,.55f,Random.Range(.85f,1.15f));
        }
        ParticleSystem Burst(string name,Material material,Color color,float lifeMin,float lifeMax,float speedMin,float speedMax,float sizeMin,float sizeMax,float gravity,int max)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);var ps=go.AddComponent<ParticleSystem>();
            ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;main.loop=false;main.duration=.5f;main.maxParticles=max;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.startLifetime=new ParticleSystem.MinMaxCurve(lifeMin,lifeMax);main.startSpeed=new ParticleSystem.MinMaxCurve(speedMin,speedMax);
            main.startSize=new ParticleSystem.MinMaxCurve(sizeMin,sizeMax);main.gravityModifier=gravity;main.startColor=color;
            var emission=ps.emission;emission.enabled=false;
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=35;shape.radius=.03f;
            var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,.2f));
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            return ps;
        }
        // The smack of a round into earth or wood: a dull low knock under a short crack, made here rather than shipped.
        static AudioClip Smack()
        {
            if(smack!=null)return smack;
            const int rate=44100;var samples=new float[rate*9/100];var noise=new System.Random(11);
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate;
                samples[i]=Mathf.Exp(-t*70)*.55f*((float)noise.NextDouble()*2-1)+Mathf.Exp(-t*35)*.5f*Mathf.Sin(2*Mathf.PI*120*t);
            }
            smack=AudioClip.Create("Round into cover",samples.Length,1,rate,false);smack.SetData(samples,0);return smack;
        }
    }
}
