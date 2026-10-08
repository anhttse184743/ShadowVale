using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ShadowVale.Map01.Editor
{
    public static class ShelterWalkSetup
    {
        [MenuItem("ShadowVale/Rescue/Update shelter walking clips")]
        public static void Build()
        {
            var root=PrefabUtility.LoadPrefabContents("Assets/Resources/Rescue/Map01RescueLayout.prefab");
            try {
                var layout=root.GetComponent<Map01RescueLayout>();
                layout.namWalk=Copy("Assets/_Project/Art/Characters/Animations/Nam/Nam_Rifle_Walk_F.fbx","Nam_Shelter_Walk");
                layout.hungWalk=Copy("Assets/_Project/Art/Characters/Animations/Generated/Walk_Tuned.anim","Hung_Shelter_Walk");
                layout.namKnifeWalk=Copy("Assets/_Project/Art/Characters/Animations/KnifeCarry/Nam_Knife_Walk.anim","Nam_Shelter_Knife_Walk");
                layout.shelterWalkSpeed=1.35f;layout.shelterFollowGap=1.4f;
                layout.interiorCameraOffset=new Vector3(-4,1.8f,-2);
                layout.namWalkCycleSpeed=Speed(layout.namWalk,1.8f);layout.hungWalkCycleSpeed=Speed(layout.hungWalk,1.94f);layout.namKnifeWalkCycleSpeed=Speed(layout.namKnifeWalk,1.94f);
                PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/Rescue/Map01RescueLayout.prefab");
                AssetDatabase.SaveAssets();
                Directory.CreateDirectory("Logs/ShelterWalk");
                File.WriteAllText("Logs/ShelterWalk/clips.txt",$"Walking {layout.shelterWalkSpeed}m/s; gap {layout.shelterFollowGap}m; cycle speeds Nam {layout.namWalkCycleSpeed}, Hung {layout.hungWalkCycleSpeed}, knife {layout.namKnifeWalkCycleSpeed}. Original gameplay clips retained.");
            } finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        static float Speed(AnimationClip clip,float fallback)=>clip.averageSpeed.magnitude>.3f?clip.averageSpeed.magnitude:fallback;
        static AnimationClip Copy(string path,string name)
        {
            var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Resources/Rescue/"+name+".anim");if(existing!=null)return existing;
            var source=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
            var clip=Object.Instantiate(source);clip.name=name;
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);
            AssetDatabase.CreateAsset(clip,"Assets/Resources/Rescue/"+name+".anim");return clip;
        }
    }
}
