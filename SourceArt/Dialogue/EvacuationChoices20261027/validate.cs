using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShadowVale.Map01;
using UnityEngine;

public static class EvacuationVoiceChecks
{
    public static void Run()
    {
        var report=new List<string>();
        try
        {
            var lines=Map02ChapterDefinition.Load().entries.Where(e=>e.id.StartsWith("m2v3_evac_")).ToArray();
            if(lines.Length!=8)throw new Exception("Expected eight evacuation radio lines");
            foreach(var line in lines)
            {
                if(!Map02DialogueCatalog.HasApprovedRecording(line))throw new Exception("Missing/mismatched voice: "+line.id);
                var recording=DialogueVoice.Find(line.id);var clip=recording.clip;
                if(!recording.radio||clip.channels!=1||clip.frequency!=48000||Mathf.Abs(clip.length-recording.duration)>.025f)throw new Exception("Invalid import/metadata: "+line.id);
                var samples=new float[clip.samples];if(!clip.GetData(samples,0)||!samples.Any(x=>Mathf.Abs(x)>.001f)||samples.Any(x=>!float.IsFinite(x)))throw new Exception("Silent/undecodable clip: "+line.id);
                report.Add("PASS "+line.id+" caption/radio match; decoded mono 48 kHz; "+clip.length.ToString("F3")+" seconds");
            }
        }
        catch(Exception error){report.Add("FAIL "+error);throw;}
        finally{File.WriteAllLines("evacuation-voice-checks.txt",report);}
    }
}
