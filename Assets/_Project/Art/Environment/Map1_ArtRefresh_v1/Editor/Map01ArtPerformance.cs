using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Object=UnityEngine.Object;
namespace ShadowVale.EnvironmentReview
{
    public static class Map01ArtPerformance
    {
        public static void TuneStage()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit mode only.");
            EditorSceneManager.OpenScene(Map01ArtStage.Stage);
            if(Object.FindFirstObjectByType<Map01ArtNavigationReady>()!=null)throw new Exception("Stage already tuned; do not multiply LOD thresholds twice.");
            File.Copy(Map01ArtStage.Stage,Map01ArtStage.Work+"/Backup/Map1_ArtStage.before_performance.unity",true);
            ApplyToActive();Map01LandscapeStage.Recapture();
        }
        public static void ApplyToGameplay()
        {
            var scene=EditorSceneManager.OpenScene(Map01EnvironmentReview.MapPath);
            if(Object.FindFirstObjectByType<Map01ArtNavigationReady>()!=null)throw new Exception("Gameplay scene already tuned.");
            File.Copy(Map01EnvironmentReview.MapPath,Map01ArtStage.Work+"/Backup/Map1.before_performance.unity",true);
            ApplyToActive();
        }
        static void ApplyToActive()
        {
            int groups=0,shadowRenderers=0;
            foreach(var group in Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(!PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(group.gameObject).Contains("/ForestV2/"))continue;
                bool small=group.name.StartsWith("GroundGrass")||group.name.StartsWith("Fern");
                bool medium=group.name.StartsWith("CoverShrub")||group.name.StartsWith("TallGrass")||group.name.StartsWith("Sapling");
                float distance=group.name.StartsWith("GroundGrass")?35:group.name.StartsWith("Fern")?50:medium?90:220;
                var lods=group.GetLODs();if(lods.Length==0)continue;
                for(int i=0;i<lods.Length;i++)lods[i].screenRelativeTransitionHeight=Mathf.Min(.95f,lods[i].screenRelativeTransitionHeight*(small?1.5f:1.45f));
                float cull=group.size*QualitySettings.lodBias/(2*Mathf.Tan(30*Mathf.Deg2Rad)*distance);
                lods[lods.Length-1].screenRelativeTransitionHeight=Mathf.Max(lods[lods.Length-1].screenRelativeTransitionHeight,Mathf.Min(cull,lods.Length>1?lods[lods.Length-2].screenRelativeTransitionHeight*.8f:.9f));
                group.SetLODs(lods);PrefabUtility.RecordPrefabInstancePropertyModifications(group);groups++;
                foreach(var r in (small?lods.SelectMany(l=>l.renderers):lods[lods.Length-1].renderers).Distinct())
                {if(r==null)continue;r.shadowCastingMode=ShadowCastingMode.Off;PrefabUtility.RecordPrefabInstancePropertyModifications(r);shadowRenderers++;}
            }
            var root=new GameObject("Map 1 / navigation startup").AddComponent<Map01ArtNavigationReady>();root.surface=Object.FindFirstObjectByType<NavMeshSurface>();
            root.initiallyEnabled=Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None).Where(a=>a.enabled).ToArray();
            foreach(var agent in root.initiallyEnabled){agent.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(agent);}
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            File.WriteAllText(Map01ArtStage.Work+"/Audit/performance_tuning.txt",$"Scene {EditorSceneManager.GetActiveScene().path}; {groups} forest LOD groups; {shadowRenderers} small/far shadow renderers; {root.initiallyEnabled.Length} authored agents enabled after surface registration. Full render resolution retained. TreeLOD1.45x; grass35m, fern50m, shrub/sapling90m, tree220m. Shared prefabs/pipeline unchanged.");
        }
    }
}
