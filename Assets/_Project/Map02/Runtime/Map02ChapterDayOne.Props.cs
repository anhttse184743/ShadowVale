using System.Collections.Generic;
using UnityEngine;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        readonly List<Material> dayMaterials=new();
        Material DayMaterial(string name,Color color)
        {var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};mat.SetFloat("_Smoothness",.18f);dayMaterials.Add(mat);return mat;}
        Transform Part(Transform owner,string name,Vector3 pos,Vector3 size,Material mat,PrimitiveType type=PrimitiveType.Cube)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(owner,false);go.transform.localPosition=pos;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;
            // Small visual details never create invisible collision hazards around shared routes.
            Destroy(go.GetComponent<Collider>());return go.transform;
        }
        Transform Group(string name,Vector3 position)
        {var go=new GameObject(name);go.transform.SetParent(dayProps,false);go.transform.position=position;return go.transform;}
        void BuildDayProps()
        {
            dayProps=new GameObject("Day 1–Night 1 · work props").transform;dayProps.SetParent(root.transform,false);
            var wood=DayMaterial("Weathered wood",new Color(.25f,.17f,.085f));var edge=DayMaterial("Crate battens",new Color(.36f,.27f,.14f));var iron=DayMaterial("Iron latch",new Color(.13f,.14f,.12f));var cloth=DayMaterial("Canvas",new Color(.26f,.3f,.16f));var amber=DayMaterial("Lamp shield",new Color(.5f,.37f,.15f));
            for(int i=0;i<3;i++)physicalCrates.Add(BuildCrate(i,sites["arrival"].position+new Vector3(i*1.0f,0,1.8f),wood,edge));   // 3×2, 2×2 and 1×3 cells, as in the store
            var store=Group("Hidden store receiving bay",sites["store"].position);Part(store,"Dry raised floor",new Vector3(1.25f,.025f,.7f),new Vector3(2.7f,.05f,2.2f),wood);
            // Small crate dimensions remain independent of the abstract puzzle grid.
            for(int i=0;i<2;i++)
            {
                var stake=Group("Repair stake "+(i+1),sites["dock"].position+new Vector3(i*1.05f-.525f,0,1.25f));
                Part(stake,"Post",new Vector3(0,.54f,0),new Vector3(.13f,1.08f,.13f),wood);physicalStakes.Add(stake);
            }
            var rails=Group("Dock repair supports",sites["dock"].position+new Vector3(0,0,1.25f));
            foreach(float x in new[]{-.53f,.53f})Part(rails,"Bearing rail",new Vector3(x,.12f,.3f),new Vector3(.14f,.18f,1.05f),wood);
            for(int i=0;i<5;i++)
            {
                var board=Group("Inspection plank "+i,sites["dock"].position+new Vector3(1.8f,.08f+i*.04f,0));
                float length=i==4?.62f:1.28f;Part(board,"Board",Vector3.zero,new Vector3(length,.055f,.27f),edge);
                if(i==3){var crack=Part(board,"Open longitudinal split",new Vector3(.02f,.031f,0),new Vector3(.78f,.012f,.025f),iron);crack.localRotation=Quaternion.Euler(0,7,0);}
                physicalBoards.Add(board);
            }
            PrepareFenceClearance();
            for(int i=0;i<2;i++)BuildNightPost(sites[i==0?"bamboo":"gate"].position,i,wood,cloth);
            BuildNightFencePerimeter();
            CreateStoryProps(wood,cloth,iron);ClearDockGrass();SyncDayProps();
        }
        void SyncDayProps()
        {
            if(dayProps==null)return;EnsureDayState();
            int delivered=State.Count("D1_04");
            for(int i=0;i<physicalCrates.Count;i++)
            {
                var crate=physicalCrates[i];if(State.carry=="D1_04"&&i==delivered)continue; // LateUpdate exclusively owns the held prop.
                var packed=Day.crates[i];crate.gameObject.SetActive(true);
                if(packed.x>=0){crate.position=StoredCrate(i,packed);crate.rotation=Quaternion.Euler(0,packed.rotated?90:0,0);}   // the cell and turn it was given
                else if(i<delivered){crate.position=sites["store"].position+new Vector3(-.7f,0,i*.8f);crate.rotation=Quaternion.identity;}
                else{crate.position=sites["arrival"].position+new Vector3(i*1.0f,0,1.8f);crate.rotation=Quaternion.identity;}
            }
            for(int i=0;i<physicalStakes.Count;i++)physicalStakes[i].position=sites["dock"].position+new Vector3(i*1.05f-.525f,-Mathf.Clamp(Day.stakeHits-i*3,0,3)*.12f,1.25f);
            for(int b=0;b<physicalBoards.Count;b++)
            {
                var board=physicalBoards[b];int slot=System.Array.FindIndex(Day.boards,p=>p.x==b);
                board.rotation=Quaternion.identity;board.position=slot>=0?sites["dock"].position+new Vector3(Day.boards[slot].offset,.27f,1.05f+slot*.3f):sites["dock"].position+new Vector3(1.8f,.08f+System.Array.IndexOf(Day.boardOrder,b)*.07f,0);
            }
            for(int i=0;i<postLamps.Count;i++)SyncNightPost(i);
        }
        void ReleaseDayArt(){nightPropPreview?.Dispose();nightPropPreview=null;foreach(var mat in dayMaterials)if(mat!=null)Destroy(mat);dayMaterials.Clear();foreach(var mesh in dayMeshes)if(mesh!=null)Destroy(mesh);dayMeshes.Clear();}
    }
}
