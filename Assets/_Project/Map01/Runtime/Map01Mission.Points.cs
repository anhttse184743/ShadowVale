using System;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.AI.Contracts;
using ShadowVale.AI.Coordination;
using ShadowVale.AI.Perception;
using ShadowVale.AI.Tactical;
using ShadowVale.Core.Events;
using ShadowVale.Core.Services;
using ShadowVale.Gameplay.Audio;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using ShadowVale.Telemetry;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The map's points (ForestPoint) and guards as they register, and the hide spots that keep Nam hidden.
    /// </summary>
    public sealed partial class Map01Mission
    {
        public void RegisterPoint(ForestPoint point)
        {
            // Retired delivery/exit markers remain serialized in old scenes and saves.
            // Current extraction is owned by Map01Extraction after the C3 radio report.
            if (point.kind == ForestPointKind.Exit) { point.enabled = false; return; }
            Points.Add(point);
            if (point.kind == ForestPointKind.Hide) {
                hideSpots.Add(point);
                var p=point.transform.position;
                for(int x=Mathf.FloorToInt((p.x-point.radius)/HideCell);x<=Mathf.FloorToInt((p.x+point.radius)/HideCell);x++)
                for(int z=Mathf.FloorToInt((p.z-point.radius)/HideCell);z<=Mathf.FloorToInt((p.z+point.radius)/HideCell);z++){
                    var key=new Vector2Int(x,z);if(!hideGrid.TryGetValue(key,out var list))hideGrid[key]=list=new();list.Add(point);
                }
            }
            else if (point.kind == ForestPointKind.Cover) coverPoints.Add(point);
            else Interactables.Add(point);
        }
        public void UnregisterPoint(ForestPoint point)
        {
            Points.Remove(point); hideSpots.Remove(point); coverPoints.Remove(point); Interactables.Remove(point);
            if(point.kind==ForestPointKind.Hide)foreach(var list in hideGrid.Values)list.Remove(point);
        }
        public void AddEnemy(Map01EnemyController enemy) { if (enemy != null && !Enemies.Contains(enemy)) Enemies = Enemies.Append(enemy).ToArray(); }
        public void RemoveEnemy(Map01EnemyController enemy) => Enemies=Enemies.Where(e=>e!=null&&e!=enemy).ToArray();

        public void UpdateHiddenState()
        {
            Hidden = false;
            if (!Crouched) return;
            Vector3 at = player.position;
            Hidden=TryGetHideSpot(at,out _);
        }
        public bool TryGetHideSpot(Vector3 at,out ForestPoint found) {
            found=null;float best=float.PositiveInfinity;
            var key=new Vector2Int(Mathf.FloorToInt(at.x/HideCell),Mathf.FloorToInt(at.z/HideCell));
            if(!hideGrid.TryGetValue(key,out var nearby))return false;
            foreach(var spot in nearby)if(spot!=null){float distance=(spot.transform.position-at).sqrMagnitude;
                if(distance<spot.radius*spot.radius&&distance<best){best=distance;found=spot;}}
            return found!=null;
        }
    }
}
