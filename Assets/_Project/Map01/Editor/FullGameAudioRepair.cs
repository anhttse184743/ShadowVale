using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace ShadowVale.Map01.Editor
{
    public static class FullGameAudioRepair
    {
        [Serializable] sealed class Bank {public Cue[] entries;}
        [Serializable] sealed class Cue {public string id,resource,cleanResource;}
        [MenuItem("ShadowVale/Audio/Validate and repair imported voice clips")]
        public static void Run()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var bank=JsonUtility.FromJson<Bank>(File.ReadAllText("Assets/Resources/Dialogue/voice-catalog.json"));
            var report=new List<string>();var unresolved=new List<string>();var seen=new HashSet<string>();
            var cues=new List<Cue>(bank.entries);
            foreach(var cue in bank.entries)if(!string.IsNullOrEmpty(cue.cleanResource))cues.Add(new Cue{id=cue.id+" clean",resource=cue.cleanResource});
            foreach(var id in new[]{"crickets","village_crickets","rooster","oars","fire","night_river","bamboo","duck","plane","cough","crate","children","hung_theme","hung_laugh","hung_invitation","tank_tracks"})cues.Add(new Cue{id=id,resource="Map02DayOne/"+id});
            foreach(var cue in cues)
            {
                if(!seen.Add(cue.resource))continue;
                var clip=Resources.Load<AudioClip>(cue.resource);
                string path="Assets/Resources/"+cue.resource+".wav";
                if(clip!=null)path=AssetDatabase.GetAssetPath(clip);
                else if(!File.Exists(path)&&File.Exists(Path.ChangeExtension(path,".mp3")))path=Path.ChangeExtension(path,".mp3");
                var existingImporter=AssetImporter.GetAtPath(path) as AudioImporter;
                if(clip!=null&&existingImporter!=null&&existingImporter.defaultSampleSettings.compressionFormat==AudioCompressionFormat.PCM)continue;
                var before=AssetDatabase.LoadMainAssetAtPath(path);
                report.Add(cue.id+" before: AssetDatabase="+(before!=null?before.GetType().Name:"null")+" Resources=null GUID="+AssetDatabase.AssetPathToGUID(path));
                var importer=AssetImporter.GetAtPath(path) as AudioImporter;
                if(importer!=null)
                {
                    // Short dialogue is small enough to keep PCM. This avoids a failed
                    // compressed import leaving a WAV present on disk but absent at runtime.
                    var settings=importer.defaultSampleSettings;settings.compressionFormat=AudioCompressionFormat.PCM;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;settings.loadType=AudioClipLoadType.DecompressOnLoad;importer.defaultSampleSettings=settings;importer.ClearSampleSettingOverride("Standalone");importer.SaveAndReimport();
                }
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
                clip=Resources.Load<AudioClip>(cue.resource);
                report.Add(cue.id+" after: "+(clip!=null?clip.length+" seconds":"null"));
                if(clip==null)unresolved.Add(cue.id);
            }
            Directory.CreateDirectory("Logs/FullGameAudio_20261023");File.WriteAllLines("Logs/FullGameAudio_20261023/import-repair.txt",report);
            AssetDatabase.SaveAssets();Debug.Log("[Audio audit] "+bank.entries.Length+" cues checked; repaired="+(report.Count/2)+"; unresolved="+unresolved.Count);
            if(unresolved.Count>0)throw new InvalidOperationException("Unresolved audio imports: "+string.Join(",",unresolved));
        }
    }
}
