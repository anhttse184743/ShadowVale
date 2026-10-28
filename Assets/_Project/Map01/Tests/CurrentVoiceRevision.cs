using System;
using System.IO;
using System.Linq;
using UnityEngine;
namespace ShadowVale.Map01.Tests {
 internal static class CurrentVoiceRevision {
  [Serializable] internal sealed class Row {public string id,voice;public float duration;public bool radio;}
  [Serializable] sealed class Bank {public Row[] lines;}
  internal static Row[] All=>JsonUtility.FromJson<Bank>(File.ReadAllText("SourceArt/Dialogue/FullGameFree20261023/integrated-manifest.json")).lines;
  internal static Row For(string id)=>All.Single(e=>e.id==id);
 }
}
