using UnityEngine;
namespace ShadowVale.Map01
{
    /// <summary>
    /// After the three families (D3_04), Hùng walks Ông Sáu down to the south dock steps and back: a rehearsal of the way out
    /// (script: "Hùng đỡ ông Sáu đi thử xuống bậc bến", the support Night 3 uses again). Hùng plays the Day 1 assist walk, the old
    /// man limps at his side. Nothing is locked; supper, a load or the end of the day cut it short.
    /// </summary>
    public sealed partial class Map02ChapterDirector
    {
        enum StepsWalk{None,Down,AtSteps,Back}
        StepsWalk stepsWalk;float stepsClock;
        const float StepsPace=.9f,StepsHold=4,StepsGiveUp=150;
        public bool StepsRehearsal=>stepsWalk!=StepsWalk.None;
        public Vector3 DockSteps=>sites["dock"].position+Vector3.left*2.4f;   // on the bank by the steps, clear of chú Bảy at the boats
        Vector3 SauYard=>sites["family"].position+Vector3.right*2.4f+Vector3.forward*1.2f;
        static float Flat(Vector3 a,Vector3 b)=>Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));
        void BeginStepsRehearsal()
        {
            var hung=roles["Hùng"];var sau=roles["Ông Sáu"];
            hung.SetLocomotion(DayClip("Assist"),DayClip("AssistWalk"));hung.GoTo(DockSteps,StepsPace);
            sau.SetLocomotion(DayClip("InjuredIdle"),DayClip("InjuredWalk"));sau.Follow(hung.transform,StepsPace+.1f,true);
            stepsWalk=StepsWalk.Down;stepsClock=0;
        }
        void TickStepsRehearsal(float dt)
        {
            if(stepsWalk==StepsWalk.None)return;
            if(State.phase!=4||sceneTask!=null){EndStepsRehearsal(false);return;}
            var hung=roles["Hùng"];stepsClock+=dt;
            if(stepsWalk==StepsWalk.Down&&Flat(hung.transform.position,DockSteps)<.8f){stepsWalk=StepsWalk.AtSteps;stepsClock=0;hung.StopAt(hung.transform.position);}
            else if(stepsWalk==StepsWalk.AtSteps&&stepsClock>StepsHold){stepsWalk=StepsWalk.Back;stepsClock=0;hung.GoTo(HungAtSupper,StepsPace);}
            else if(stepsWalk==StepsWalk.Back&&Flat(hung.transform.position,HungAtSupper)<.8f)EndStepsRehearsal(true);
            if(stepsClock>StepsGiveUp)EndStepsRehearsal(false);   // a route that cannot be walked never keeps them out
        }
        /// <param name="walked">They came home: the old man steps the last bit to his place. Otherwise both are put where the day needs them.</param>
        void EndStepsRehearsal(bool walked)
        {
            if(stepsWalk==StepsWalk.None)return;stepsWalk=StepsWalk.None;
            if(!roles.TryGetValue("Hùng",out var hung)||!roles.TryGetValue("Ông Sáu",out var sau)||hung==null||sau==null)return;
            HungLocomotion();sau.SetLocomotion(null,null);   // back to his Day 3 walk
            if(walked)sau.GoTo(SauYard,StepsPace);else sau.StopAt(SauYard);
            if(sceneTask==null&&!walked)hung.StopAt(HungAtSupper);
        }
    }
}
