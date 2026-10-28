using System.IO;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace ShadowVale.Map01.Editor
{
    [InitializeOnLoad]
    public static class Map02ConversationValidation
    {
        static Map02ConversationValidation() { EditorApplication.update += Tick; }
        static void Tick()
        {
            const string request = "Temp/Map02ConversationTest.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) return;
            bool skills = File.ReadAllText(request).Trim() == "skills";
            File.Delete(request);
            if (skills) TutorialValidation.RunMap02Skills(); else TutorialValidation.RunMap02Conversation();
        }
    }
}
