using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace ShadowVale.Map01.Editor
{
    public static class Map02PresentationRepair
    {
        public static void Run()
        {
            var data=AssetDatabase.LoadAssetAtPath<Map02DayOneArt>("Assets/_Project/Map01/Resources/Story/Map02DayOneArt.asset");
            var hung=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Characters/NPCs/Animations/Hung_DiKhapKhieng.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
            data.actions.First(a=>a.id=="InjuredWalk").clip=hung;
            foreach(var id in new[]{"CarryWalk","AssistWalk"})
            {
                var clip=data.Find(id);
                // Only normalize the generated body's forward frame; do not touch the source rig or Map 1 clips.
                foreach(var key in new[]{"RootQ.x","RootQ.y","RootQ.z","RootQ.w"})AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),key),AnimationCurve.Constant(0,clip.length,key=="RootQ.w"?1:0));
                clip.EnsureQuaternionContinuity();EditorUtility.SetDirty(clip);
            }
            EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();Debug.Log("MAP02_PRESENTATION_REPAIR_PASS");
        }
    }
}
