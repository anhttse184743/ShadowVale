using System.Collections.Generic;
using ShadowVale.AI.Perception;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Skills;
using UnityEngine;

namespace ShadowVale.Map01
{
    // Adapts the shared character views/inventory without creating a Map 1 mission in Map 2.
    public sealed class CharacterHudSession
    {
        public readonly Map01Mission MapOne;
        public readonly Map02Mission MapTwo;
        CharacterHudSession(GameObject owner){MapOne=owner.GetComponent<Map01Mission>();MapTwo=owner.GetComponent<Map02Mission>();}
        public static CharacterHudSession For(GameObject owner)=>new CharacterHudSession(owner);
        public bool IsInitialized=>MapOne!=null?MapOne.IsInitialized:MapTwo?.Ready==true;
        public bool Stopped=>MapOne!=null?MapOne.Stopped:MapTwo==null||MapTwo.Paused||MapTwo.Cinematic||MapTwo.World.story.failed||ModernHealth.IsDead;
        public bool Cinematic=>MapOne!=null?MapOne.Cinematic:MapTwo==null||MapTwo.Cinematic;
        public bool InventoryOpen=>MapOne!=null?MapOne.InventoryOpen:MapTwo.Panel==1;
        public bool MapOpen=>MapOne!=null?MapOne.MapOpen:MapTwo.Panel==3;
        public bool BenchOpen=>MapOne!=null?MapOne.BenchOpen:MapTwo.Panel==4;
        public Transform player=>MapOne!=null?MapOne.player:MapTwo!=null?MapTwo.Player:null;
        public Camera gameCamera=>MapOne!=null?MapOne.gameCamera:MapTwo!=null?MapTwo.GameCamera:null;
        public Health ModernHealth=>MapOne!=null?MapOne.ModernHealth:player?.GetComponent<Health>();
        public IBinoculars Binoculars=>MapOne!=null?MapOne.Binoculars:MapTwo!=null?MapTwo.Chapter:null;
        public bool CameraInputEnabled=>MapOne!=null?MapOne.CameraInputEnabled:MapTwo!=null&&!MapTwo.InputBlocked;
        public T GetComponent<T>() where T:Component=>(MapOne!=null?MapOne.gameObject:MapTwo.gameObject).GetComponent<T>();
        public PlayerCombat ModernCombat=>MapOne!=null?MapOne.ModernCombat:player?.GetComponent<PlayerCombat>();
        public PlayerSkills Skills=>MapOne!=null?MapOne.Skills:player?.GetComponent<PlayerSkills>();
        public ForestSettings Settings=>MapOne!=null?MapOne.Settings:MapTwo?.CharacterSettings;
        public ForestBundle Bundle=>MapOne!=null?MapOne.Bundle:MapTwo?.CharacterContent;
        public float PlayerHealth=>MapOne!=null?MapOne.PlayerHealth:player.GetComponent<Health>().Current;
        public float Stamina=>MapOne!=null?MapOne.Stamina:MapTwo.Stamina;
        public bool Crouched=>MapOne!=null?MapOne.Crouched:player.GetComponent<ShadowVale.Gameplay.Player.PlayerController>().IsSneaking;
        public bool Hidden=>MapOne!=null?MapOne.Hidden:player!=null&&Map01Smoke.Blocks(player.position+Vector3.up,player.position+Vector3.up);
        public string Dialogue=>MapOne!=null?MapOne.Dialogue:MapTwo.HudMessage;
        public float DialogueUntil=>MapOne!=null?MapOne.DialogueUntil:Time.time+(MapTwo.HudMessageVisible?1:0);
        public Vector2 mapMin=>MapOne!=null?MapOne.mapMin:Vector2.one*-Map02Navigation.HalfSize;
        public Vector2 mapMax=>MapOne!=null?MapOne.mapMax:Vector2.one*Map02Navigation.HalfSize;
        public Map01EnemyController[] Enemies=>MapOne!=null?MapOne.Enemies:System.Array.Empty<Map01EnemyController>();
        public List<ForestPoint> Interactables=>MapOne!=null?MapOne.Interactables:MapTwo.CharacterWorkpoints;
        public void Say(string text,float seconds=4){if(MapOne!=null)MapOne.Say(text,seconds);else MapTwo?.Say(text);}
        public void RestoreHealthFromSave(float value){if(MapOne!=null)MapOne.RestoreHealthFromSave(value);else player.GetComponent<Health>().RestoreHealth(value);}
        public void SetBenchOpen(bool value){if(MapOne!=null)MapOne.SetBenchOpen(value);else MapTwo.SetCharacterPanel(value?4:0);}
        public bool GetTakedownHint(float range,out Map01EnemyController target,out bool ready){target=null;ready=false;return MapOne!=null&&MapOne.GetTakedownHint(range,out target,out ready);}
        public void Drop(string id,int count,float condition){if(MapOne!=null)Map01Drops.For(MapOne)?.Put(id,count,condition);else MapTwo?.DropCharacterItem(id,count,condition);}
        public int ObstructionMask=>MapOne!=null?MapOne.ObstructionMask:~0;
        public Material trailMaterial=>MapOne!=null?MapOne.trailMaterial:MapTwo.CharacterTrail;
        public void Damage(float amount){if(MapOne!=null)MapOne.Damage(amount);else ModernHealth.TakeDamage(amount,player.position,MapTwo.gameObject);}
        public int EmitNoise(Vector3 point,float radius,NoiseKind kind)=>MapOne!=null?MapOne.EmitNoise(point,radius,kind):MapTwo.CharacterNoise(point,radius,kind);
    }
}
