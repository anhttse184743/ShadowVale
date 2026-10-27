using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ShadowVale.Map01;
using ShadowVale.Gameplay.Player;
using ShadowVale.Gameplay.Combat;
public static class NightStoreLootChecks
{
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static readonly List<string> report=new List<string>();
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,Flags).SetValue(o,v);
 static void Prop(object o,string n,object v)=>o.GetType().GetProperty(n,Flags).SetValue(o,v);
 static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Flags).Invoke(o,args);
 static void Check(bool ok,string m){if(!ok)throw new Exception(m);report.Add("PASS "+m);}
 public static void Run()
 {
  int result=0;
  try
  {
   var pawn=new GameObject("Nam");var motor=pawn.AddComponent<PlayerController>();motor.enabled=false;var hp=pawn.AddComponent<Health>();hp.SetMaxHealth(200);
   var root=new GameObject("Mission");var mission=root.AddComponent<Map02Mission>();mission.enabled=false;Set(mission,"player",motor);Set(mission,"health",hp);Set(mission,"dialogue",new Map02ConversationSession(mission.World.story));Prop(mission,"Ready",true);
   var c=root.AddComponent<Map02ChapterDirector>();c.enabled=false;Prop(c,"Mission",mission);Prop(mission,"Chapter",c);
   var bag=root.AddComponent<Map01Inventory>();Call(bag,"Awake");Prop(mission,"Bag",bag);
   Prop(mission,"CharacterContent",JsonUtility.FromJson<ForestBundle>("{\"items\":[{\"id\":\"ammo_rifle\",\"stack_max\":90},{\"id\":\"ammo_pistol\",\"stack_max\":90},{\"id\":\"medkit_small\",\"stack_max\":10}]}"));
   bag.Changed+=()=>Call(mission,"WriteBagTotals");
   foreach(var t in Map02ChapterDefinition.Load().tasks)if(t.phase<3)c.State.Complete(t.id);
   c.State.Complete("N2_01");c.State.Complete("N2_02");c.State.night2ReachedStore=c.State.night2HatchOpen=c.State.night2InsideStore=true;
   var store=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Story/NightStore/UndergroundStore")).AddComponent<Map02UndergroundStore>();store.enabled=false;Prop(store,"Owner",c);Prop(c,"UndergroundStore",store);
   Check(!c.State.night2StoreLootInitialized,"old saves default to uninitialized unclaimed loot");int originalRenderers=store.GetComponentsInChildren<Renderer>().Length;Call(store,"BuildCollision");Call(store,"BindShelfLoot");Physics.SyncTransforms();
   var crates=(Transform[])store.GetType().GetField("lootCrates",Flags).GetValue(store);
   Check(crates.Length==3&&crates.Select(x=>x.parent.name).SequenceEqual(new[]{"ShelfFront","ShelfBack","ShelfEast"}),"loot binds to the three original shelf meshes");
   Check(store.GetComponentsInChildren<Renderer>().Length==originalRenderers&&crates.All(x=>x.GetComponent<Renderer>()==null),"binding loot adds no new visible crates or floor props");
   Action<int> place=i=>{var at=(Vector3)Call(store,"LootApproachPoint",i);pawn.transform.position=at+(i==0?Vector3.forward:i==1?Vector3.back:Vector3.left)*.8f-Vector3.up*1.11f;Physics.SyncTransforms();};
   var permutations=new HashSet<string>();
   for(int order=0;order<6;order++)
   {
    c.State.night2StoreLootOrder=order;c.State.night2StoreLootMask=0;
    var contents=Enumerable.Range(0,3).Select(i=>store.LootContents(i)).ToArray();
    Check(contents.Single(x=>x.id=="ammo_rifle").count==30&&contents.Single(x=>x.id=="ammo_pistol").count==21&&contents.Single(x=>x.id=="medkit_small").count==2,"permutation "+order+" has exact AK/K54/bandage totals");
    permutations.Add(string.Join(",",contents.Select(x=>x.id)));
   }
   Check(permutations.Count==6,"all six random arrangements are distinct");
   int savedOrder=c.State.night2StoreLootOrder;Call(store,"EnsureLootLayout");Check(c.State.night2StoreLootOrder==savedOrder,"refresh never rerolls an existing layout");
   place(0);
   c.State.night2InsideStore=false;Check(!store.TryLootInteraction(),"cannot loot from outside the store");c.State.night2InsideStore=true;
   Prop(store,"IsClimbing",true);Check(!store.TryLootInteraction(),"cannot loot while using the ladder");Prop(store,"IsClimbing",false);
   Prop(mission,"Paused",true);Check(!store.TryLootInteraction(),"cannot loot while paused");Prop(mission,"Paused",false);
   var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=(pawn.transform.position+crates[0].position)*.5f+Vector3.up*.5f;wall.transform.localScale=new Vector3(2,2,.15f);Physics.SyncTransforms();
   Check(!store.TryLootInteraction(),"shelf or wall blocks looting through it");wall.SetActive(false);Physics.SyncTransforms();
   for(int i=0;i<3;i++)
   {
    place(i);
    Check(store.TryLootInteraction(),"E interaction loots crate "+i);
    Check(!store.TryLootInteraction(),"crate "+i+" cannot grant loot twice");
    var checkpoint=(string)mission.GetType().GetField("checkpoint",Flags).GetValue(mission);
    var restored=Map02Mission.Parse(checkpoint);
    Check(restored.chapter.StoreLootTaken(i)&&restored.chapter.night2StoreLootOrder==savedOrder,"checkpoint persists crate "+i+" claim and layout");
   }
   Check(bag.Count("ammo_rifle")==30&&bag.Count("ammo_pistol")==21&&bag.Count("medkit_small")==2,"looting all three adds exact requested totals to the real inventory");
   var roundtrip=Map02Mission.Parse(JsonUtility.ToJson(mission.Capture()));Prop(c,"State",roundtrip.chapter);
   Check(!store.TryLootInteraction()&&c.State.night2StoreLootMask==7,"restored save keeps every emptied crate claimed");
   c.State.ResetPatrolRoute();Check(c.State.night2StoreLootMask==7&&c.State.night2StoreLootOrder==savedOrder,"route reset does not reroll or replenish warehouse loot");
   c.State.night2StoreLootMask=8;bool rejected=false;try{c.State.Validate();}catch(InvalidDataException){rejected=true;}
   Check(rejected,"invalid loot masks are rejected by save validation");
  }
  catch(Exception e){result=1;report.Add("FAIL "+e);Debug.LogException(e);}
  File.WriteAllLines(Path.GetFullPath(Path.Combine(Application.dataPath,"../../../SourceArt/Map02NightBankFlow/store-loot-checks.txt")),report);EditorApplication.Exit(result);
 }
}
