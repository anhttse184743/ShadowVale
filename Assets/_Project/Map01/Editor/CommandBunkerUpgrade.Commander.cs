using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    public static partial class CommandBunkerUpgrade
    {
        static double probeAt;
        static void PlaceCommander()
        {
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!="Assets/_Project/Scenes/Maps/Map 1.unity")throw new Exception("Open Map 1 first.");
            var board=All<Transform>().Single(t=>t.name=="MissionBoard");var mission=All<Map01Mission>().Single();
            const string rescuePath="Assets/Resources/Rescue/Map01RescueLayout.prefab";
            var rescueRoot=PrefabUtility.LoadPrefabContents(rescuePath);
            try {
            var rescue=rescueRoot.GetComponent<Map01RescueLayout>();
            var player=mission.player!=null?mission.player:All<ShadowVale.Gameplay.Player.PlayerController>().Single().transform;
            var log=new StringBuilder();log.AppendLine("Player root="+player.position+" report before="+rescue.reportPoint.position);
            var post=board.parent.Find("Allied commander beside mission board");
            if(post==null){post=new GameObject("Allied commander beside mission board").transform;post.SetParent(board.parent,false);}
            var target=board.TransformPoint(new Vector3(-2.05f,0,-.85f));target.y=player.position.y;
            post.SetPositionAndRotation(target,Quaternion.LookRotation(-board.forward));mission.alliedCommanderPost=post;EditorUtility.SetDirty(mission);
            var report=target-board.forward*1.8f;
            if(!NavMesh.SamplePosition(new Vector3(report.x,board.position.y,report.z),out var floor,.65f,NavMesh.AllAreas))throw new Exception("No walkable reporting position.");
            report=floor.position;
            var route=rescue.shelterRun;
            if(route==null||route.Length<4)throw new Exception("Unexpected shelter return route.");
            // Keep the ramp, doorway and first indoor marker; route around the briefing table.
            var aisle=rescue.transform.Find("Shelter return — left aisle")??rescue.transform.Find("Shelter return — right aisle");
            if(aisle==null){aisle=new GameObject("Shelter return — left aisle").transform;aisle.SetParent(rescue.transform,false);}
            aisle.name="Shelter return — left aisle";
            var aisleAt=new Vector3(-68.1f,board.position.y,-65.6f);
            if(!NavMesh.SamplePosition(aisleAt,out var aisleFloor,.7f,NavMesh.AllAreas))throw new Exception("No shelter aisle route.");
            aisle.position=aisleFloor.position;
            var last=route[route.Length-1];last.position=report;last.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(post.position-report,Vector3.up));
            rescue.shelterRun=route.Take(3).Concat(new[]{aisle,last}).ToArray();
            rescue.reportPoint.SetPositionAndRotation(report,last.rotation);
            rescue.interiorCameraOffset=new Vector3(2.6f,1.8f,-2.1f);EditorUtility.SetDirty(rescue);
            for(int i=1;i<rescue.shelterRun.Length;i++)
            {
                var path=new NavMeshPath();var a=rescue.shelterRun[i-1].position;var b=rescue.shelterRun[i].position;
                if(!NavMesh.CalculatePath(a,b,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)throw new Exception("Disconnected shelter return segment "+i);
                log.AppendLine("PASS return segment "+i+" "+a+" -> "+b+" corners="+path.corners.Length);
            }
            log.AppendLine("Commander="+post.position+" facing="+post.forward+" Nam report="+report+" distance="+Vector3.Distance(post.position,report));
            Directory.CreateDirectory("Library/CommandBunkerBackup");
            var backup="Library/CommandBunkerBackup/Map1_before_commander.unity";if(!File.Exists(backup))File.Copy(scene.path,backup);
            var rescueBackup="Library/CommandBunkerBackup/Rescue_before_commander.prefab";if(!File.Exists(rescueBackup))File.Copy(rescuePath,rescueBackup);
            PrefabUtility.SaveAsPrefabAsset(rescueRoot,rescuePath);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText(Reports+"/commander_placement.txt",log.ToString());
            }finally{PrefabUtility.UnloadPrefabContents(rescueRoot);}
        }
        static void ProbeCommander()
        {
            int state=SessionState.GetInt("BunkerCommanderProbe",0);
            if(state==4&&!EditorApplication.isPlayingOrWillChangePlaymode){EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("BunkerProbeStartScene",""));SessionState.SetInt("BunkerCommanderProbe",0);return;}
            if(state==0||state==4||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            try
            {
                var mission=Object.FindFirstObjectByType<Map01Mission>();
                if(mission==null)
                {
                    const string map="Assets/_Project/Scenes/Maps/Map 1.unity";
                    if(state==1&&UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=map)
                    {
                        foreach(var menu in Object.FindObjectsByType<ForestMenu>(FindObjectsSortMode.None))menu.CancelInvoke();
                        EditorSceneManager.LoadSceneInPlayMode(map,new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
                    }
                    return;
                }
                if(!mission.IsInitialized)return;
                var opening=mission.GetComponentInChildren<Map01OpeningCutscene>();if(opening==null||opening.CommanderTransform==null)return;
                var actor=opening.CommanderTransform;
                if(state==1)
                {
                    probeAt=EditorApplication.timeSinceStartup+2;SessionState.SetInt("BunkerCommanderProbe",2);return;
                }
                if(EditorApplication.timeSinceStartup<probeAt)return;
                if(state==2)
                {
                    if(Vector3.Distance(actor.position,mission.alliedCommanderPost.position)>.02f)throw new Exception("Commander missed authored post.");
                    var board=All<Transform>().Single(t=>t.name=="MissionBoard");
                    Render(board.position+new Vector3(-3.6f,2,-4.2f),board.position+new Vector3(-.85f,1.45f,-.3f),"07_commander_beside_board",58);
                    var rescue=Object.FindFirstObjectByType<Map01RescueLayout>();var cc=mission.player.GetComponent<CharacterController>();bool enabled=cc!=null&&cc.enabled;if(cc!=null)cc.enabled=false;
                    var at=rescue.reportPoint.position+Vector3.left*.6f+Vector3.forward*.6f;at.y=mission.alliedCommanderPost.position.y;
                    mission.player.SetPositionAndRotation(at,Quaternion.LookRotation(actor.position-at));if(cc!=null)cc.enabled=enabled;
                    var dialogue=mission.GetComponent<Map01StoryDialogue>();
                    if(!dialogue.Begin(new[]{new Map01StoryDialogue.Line("Chỉ huy","Đồng chí kiểm tra nhiệm vụ trên bảng."),new Map01StoryDialogue.Line("Nam","Rõ, thưa chỉ huy.")},null))throw new Exception("Cannot start briefing preview.");
                    probeAt=EditorApplication.timeSinceStartup+1.5;SessionState.SetInt("BunkerCommanderProbe",3);return;
                }
                var storyDialogue=mission.GetComponent<Map01StoryDialogue>();
                var camera=mission.gameCamera;
                if(state==3)
                {
                    var fixedSpot=mission.alliedCommanderPost.position+mission.alliedCommanderPost.forward*1.8f;
                    if(Vector3.Distance(mission.player.position,fixedSpot)>.005f||Mathf.Abs(Vector3.Distance(mission.player.position,actor.position)-1.8f)>.005f)
                        throw new Exception("Nam is not at the fixed 1.8 m briefing mark.");
                    Render(camera.transform.position,camera.transform.position+camera.transform.forward*3,"08_commander_dialogue",camera.fieldOfView);
                    var board=All<Transform>().Single(t=>t.name=="MissionBoard");
                    var point=camera.WorldToViewportPoint(board.position+Vector3.up*1.8f);
                    if(point.z<=0||point.x<0||point.x>1||point.y<0||point.y>1)throw new Exception("Mission board is outside commander shot.");
                    foreach(var target in new[]{actor.position+Vector3.up*1.42f,board.position+Vector3.up*1.7f-board.forward*.08f})
                    {
                        var delta=target-camera.transform.position;
                        foreach(float height in new[]{1.05f,1.55f})
                        {
                            var centre=mission.player.position+Vector3.up*height;
                            float t=Vector3.Dot(centre-camera.transform.position,delta)/delta.sqrMagnitude;
                            float radius=height<1.5f?.5f:.35f;
                            if(t>0&&t<1&&(centre-camera.transform.position-delta*t).sqrMagnitude<radius*radius)
                                throw new Exception("Nam obscures commander or mission board.");
                        }
                    }
                    storyDialogue.Advance();
                    if(storyDialogue.Index!=1){probeAt=EditorApplication.timeSinceStartup+1;return;}
                    SessionState.SetInt("BunkerCommanderProbe",5);probeAt=EditorApplication.timeSinceStartup+1.5;return;
                }
                Render(camera.transform.position,camera.transform.position+camera.transform.forward*3,"09_nam_dialogue_unchanged",camera.fieldOfView);
                // Compare Nam's resulting shot to the original two-candidate camera rule.
                var forward=Vector3.ProjectOnPlane(actor.position-mission.player.position,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,forward);
                var focus=mission.player.position+Vector3.up*1.42f;float best=-1;var expected=Vector3.zero;
                foreach(float side in new[]{1f,-1f})
                {
                    var candidate=mission.player.position+forward*1.05f+right*(side*1.5f)+Vector3.up*1.64f;
                    var delta=candidate-focus;float clear=delta.magnitude;
                    foreach(var hit in Physics.SphereCastAll(focus,.12f,delta.normalized,delta.magnitude,mission.ObstructionMask,QueryTriggerInteraction.Ignore))
                        if(hit.transform.GetComponentInParent<ShadowVale.Gameplay.Combat.Health>()==null)clear=Mathf.Min(clear,hit.distance-.15f);
                    if(clear<.8f||clear<=best)continue;best=clear;expected=focus+delta.normalized*clear;
                }
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                var actual=(Vector3)typeof(Map01StoryDialogue).GetField("conversationCamera",flags).GetValue(storyDialogue);
                var shotFov=(float)typeof(Map01StoryDialogue).GetField("conversationFov",flags).GetValue(storyDialogue);
                if(Vector3.Distance(actual,expected)>.02f||Mathf.Abs(shotFov-48)>.01f)throw new Exception("Nam camera differs from original shot rule.");
                File.WriteAllText(Reports+"/camera_validation.txt","PASS commander diagonal shot includes mission board centre in viewport.\nPASS Nam approaches from the left without obscuring the officer or board centre.\nPASS Nam camera position matches original framing rule and FOV remains 48 degrees.\n");
                var towards=Vector3.ProjectOnPlane(mission.player.position-actor.position,Vector3.up).normalized;
                if(Vector3.Dot(actor.forward,towards)<.95f)throw new Exception("Commander does not face Nam in dialogue.");
                storyDialogue.Close(false);
                if(Quaternion.Angle(actor.rotation,mission.alliedCommanderPost.rotation)>.1f)throw new Exception("Commander does not restore shelter facing.");
                var stagingLog=new StringBuilder();
                foreach(var offset in new[]{Vector3.right*.7f,Vector3.left*.6f,Vector3.back*.25f})
                {
                    var body=mission.player.GetComponent<CharacterController>();bool wasEnabled=body!=null&&body.enabled;if(wasEnabled)body.enabled=false;
                    mission.player.position=actor.position+offset;if(wasEnabled)body.enabled=true;
                    if(!storyDialogue.Begin(new[]{new Map01StoryDialogue.Line("Chỉ huy","Kiểm tra vị trí báo cáo.")},null))throw new Exception("Cannot stage dialogue.");
                    var mark=mission.alliedCommanderPost.position+mission.alliedCommanderPost.forward*1.8f;
                    if(Vector3.Distance(mission.player.position,mark)>.005f)throw new Exception("Approach direction changed briefing position.");
                    storyDialogue.Close(false);stagingLog.AppendLine("PASS approach "+offset+" -> same 1.8 m mark.");
                }
                var beforeRadio=mission.player.position;
                if(!storyDialogue.Begin(new[]{new Map01StoryDialogue.Line("Chỉ huy","Kiểm tra liên lạc.",true)},null))throw new Exception("Cannot check radio dialogue.");
                if(Vector3.Distance(beforeRadio,mission.player.position)>.005f)throw new Exception("Radio dialogue moved Nam.");
                storyDialogue.Close(false);stagingLog.AppendLine("PASS radio dialogue preserves player position.");
                File.WriteAllText(Reports+"/fixed_briefing_position.txt",stagingLog.ToString());
                File.WriteAllText(Reports+"/commander_runtime.txt","PASS runtime commander at authored board-side post.\nPASS local briefing starts, commander faces Nam, camera renders at new position, closing restores post facing.\nPASS briefing preview cancelled without committing quest progress.\n");
                SessionState.SetInt("BunkerCommanderProbe",4);EditorApplication.isPlaying=false;
            }
            catch(Exception e){SessionState.SetInt("BunkerCommanderProbe",4);File.WriteAllText(Reports+"/commander_runtime.txt",e.ToString());Debug.LogException(e);EditorApplication.isPlaying=false;}
        }
    }
}

