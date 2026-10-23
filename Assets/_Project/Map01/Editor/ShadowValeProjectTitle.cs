using System.IO;
using UnityEditor;
using UnityEngine;
namespace ShadowVale.Map01.Editor {
 [InitializeOnLoad] public static class ShadowValeProjectTitle {
  static ShadowValeProjectTitle(){EditorApplication.updateMainWindowTitle+=descriptor=>descriptor.title="ShadowVale | "+Path.GetFullPath(Path.Combine(Application.dataPath,"..")).Replace('/','\\');}
 }
}
