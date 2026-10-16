using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowVale.ArtPreview
{
    /// <summary>Consumes an explicit local art-capture request after script import.</summary>
    [InitializeOnLoad]
    static class MissionOutpostsDemoRequest
    {
        const string Request = "SourceArt/MissionOutposts_Demo/capture.request";
        const string Status = "SourceArt/MissionOutposts_Demo/capture_status.txt";

        static MissionOutpostsDemoRequest()
        {
            if (File.Exists(Request)) EditorApplication.delayCall += Run;
        }

        static void Run()
        {
            if (!File.Exists(Request)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Run;
                return;
            }
            // Never interrupt gameplay or replace an unrelated/unsaved scene.
            for (int i = 0; i < SceneManager.sceneCount; ++i)
            {
                var scene = SceneManager.GetSceneAt(i);
                bool art = scene.path.StartsWith("Assets/_Project/Art/Environment/ResistancePalmRoof_Demo/", StringComparison.Ordinal)
                    || scene.path.StartsWith("Assets/_Project/Art/Environment/MissionOutposts_Demo/", StringComparison.Ordinal);
                if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || !art)
                {
                    File.WriteAllText(Status, "WAITING: editor is not in a clean art preview scene.");
                    return;
                }
            }
            File.Move(Request, Request + ".consumed");
            try
            {
                File.WriteAllText(Status, "RUNNING");
                MissionOutpostsPreview.BuildAndRender();
                File.WriteAllText(Status, "COMPLETE " + DateTime.UtcNow.ToString("O"));
            }
            catch (Exception e)
            {
                File.WriteAllText(Status, "FAILED " + e);
                Debug.LogException(e);
            }
        }
    }
}
