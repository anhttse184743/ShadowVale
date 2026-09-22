using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.AI.Navigation;

namespace ShadowVale.Map01.Editor
{
    // Reference blockout: rectangular wetland plots, winding canal, forest and earth shelter.
    // All geometry remains individually editable. No downloaded assets are required.
    public static class ReferenceWetlandDressing
    {
        static Transform solid, decor;
        static Material earth, timber, thatch, water, duckweed, reed, chart;
        static GameObject Box(string name, Vector3 p, Vector3 size, Material m, bool collision = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(collision ? solid : decor);
            go.transform.position = p; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (!collision) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            else go.layer = LayerMask.NameToLayer("Obstacle");
            return go;
        }
        static Material Mat(string name, Color c)
        {
            string path = "Assets/_Project/Map01/ReferenceBlockout/Generated/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path); }
            m.SetColor("_BaseColor",c); m.SetFloat("_Smoothness",.05f); EditorUtility.SetDirty(m); return m;
        }
        static Vector3 P(float x, float y, float z) => new Vector3(x,y,z);
        static float CanalX(float z) => 36 + Mathf.Sin((z + 30) * .045f) * 3;
        public static void Build(Transform collision, Transform visuals)
        {
            solid = collision; decor = visuals;
            earth=Mat("Clay earth",new Color(.43f,.31f,.19f));
            timber=Mat("Bamboo timber",new Color(.34f,.24f,.12f));
            thatch=Mat("Dry palm roof",new Color(.47f,.43f,.24f));
            water=Mat("Canal green",new Color(.19f,.36f,.29f));
            duckweed=Mat("Duckweed",new Color(.39f,.49f,.19f));
            reed=Mat("Bamboo leaves",new Color(.25f,.37f,.13f));
            chart=Mat("Paper chart",new Color(.83f,.75f,.53f));
            // Clear trees only inside new water and architecture footprints.
            var remove = new List<GameObject>();
            foreach(var parent in new[]{solid,decor}) foreach(Transform t in parent)
            {
                var p=t.position;
                if ((t.name.Contains("trunk") || t.name.Contains("canopy") || t.name.Contains("fern") || t.name.Contains("rock")) &&
                    (p.x > 29 || (p.x < -28 && p.z < 29) || (p.x > -18 && p.x < 18 && p.z > 46 && p.z < 72))) remove.Add(t.gameObject);
            }
            foreach(var go in remove) UnityEngine.Object.DestroyImmediate(go);
            // Keep physical level boundaries but blend their appearance into earthen banks.
            foreach(Transform t in solid) if(t.name.Contains("boundary") || t.name == "North river bank")
            {
                t.GetComponent<Renderer>().sharedMaterial=earth;
                if(t.name=="North river bank") t.GetComponent<Renderer>().enabled=false;
            }
            Canal(); Plots(); Shelter();
        }
        static void Canal()
        {
            var root = new GameObject("03 • Kenh rach va beo nuoc").transform; root.SetParent(decor);
            for(int i=0;i<70;i++)
            {
                float z=-55+i*2; float x=CanalX(z);
                var tile=Box("Canal water",P(x,.065f,z),P(8,.10f,2.25f),water,false);tile.transform.SetParent(root);
                // Invisible blocker prevents walking into deep water; navmesh excludes it.
                var barrier=Box("Deep canal collision",P(x,1,z),P(7.6f,2,2.1f),water);
                barrier.GetComponent<Renderer>().enabled=false;
                var mod=barrier.AddComponent<NavMeshModifier>(); mod.overrideArea=true;mod.area=1;
                if(i%2==0) foreach(float side in new[]{-1f,1f})
                {
                    Box("Duckweed bank",P(x+side*3.2f,.13f,z),P(1.3f,.025f,1.8f),duckweed,false).transform.SetParent(root);
                    for(int k=0;k<3;k++)
                        Box("Reed",P(x+side*4.3f+k*.15f,.65f,z),P(.07f,1.3f,.07f),reed,false).transform.SetParent(root);
                }
            }
            // Two narrow sampans, open hulls rather than solid boat cubes.
            foreach(float z in new[]{12f,20f})
            {
                float x=CanalX(z);
                Box("Sampan bottom",P(x,.2f,z),P(1.5f,.16f,5),timber,false);
                foreach(float side in new[]{-1f,1f}) Box("Sampan gunwale",P(x+side*.72f,.43f,z),P(.12f,.45f,5),timber,false);
                foreach(float end in new[]{-2.4f,2.4f}) Box("Sampan end",P(x,.4f,z+end),P(1.5f,.4f,.15f),timber,false);
                for(int i=-1;i<=1;i++) Box("Sampan seat",P(x,.48f,z+i*1.3f),P(1.5f,.12f,.35f),timber,false);
            }
        }
        static void Plots()
        {
            for(int i=0;i<5;i++)
            {
                float z=-43+i*14;
                var pond=Box("West wetland plot "+(i+1),P(-36,.09f,z),P(12,.12f,11),i%2==0?water:duckweed,false);
                var stop=Box("Pond collision",P(-36,1,z),P(11.5f,2,10.5f),water);
                stop.GetComponent<Renderer>().enabled=false;
                var mod=stop.AddComponent<NavMeshModifier>();mod.overrideArea=true;mod.area=1;
                foreach(float side in new[]{-1f,1f})
                {
                    Box("Raised pond dike",P(-36+side*6.4f,.15f,z),P(.8f,.3f,13),earth);
                    Box("Cross dike",P(-36,.15f,z+side*6),P(13,.3f,.8f),earth);
                }
            }
        }
        static void Shelter()
        {
            Box("Base clearing",P(0,.03f,58),P(30,.06f,24),earth,false);
            // Earth embankments imply a semi-buried shelter; floor stays flush for prototype traversal.
            Box("Earth shelter back",P(-5,1.5f,66),P(17,3,2),earth);
            Box("Earth shelter west",P(-13,1.2f,60),P(2,2.4f,12),earth);
            Box("Earth shelter east",P(1.8f,1.2f,62),P(1.2f,2.4f,8),earth);
            Box("Packed earth floor",P(-5,.06f,60),P(15,.12f,11),earth,false);
            // Doorway width 3m: visible dark passage to the room.
            Box("Door left earth",P(-10,1.1f,54),P(6,2.2f,.65f),earth);
            Box("Door right earth",P(.5f,1.1f,54),P(5,2.2f,.65f),earth);
            Box("Door lintel",P(-4.75f,2.9f,54),P(4.5f,.35f,.7f),timber);
            // Rear half of gabled thatch roof; foreground cut away to expose the meeting room.
            var roof=Box("Thatch roof • rear half cutaway",P(-5,3.4f,63),P(18,.25f,7.5f),thatch,false);
            roof.transform.rotation=Quaternion.Euler(27,0,0);
            var porch=Box("Low thatch porch left",P(-10,2.6f,51.5f),P(7,.22f,6),thatch,false);
            porch.transform.rotation=Quaternion.Euler(0,0,25);
            for(int i=0;i<6;i++)
            {
                float x=-12+i*3;
                Box("Timber roof beam",P(x,2.9f,60),P(.16f,.2f,11),timber,false);
                if(i==0 || i==5) Box("Porch post",P(x,1.5f,50),P(.25f,3,.25f),timber);
            }
            Box("Long meeting table",P(-5,.92f,61),P(7,.22f,1.8f),timber);
            foreach(float x in new[]{-8f,-2f}) foreach(float z in new[]{60.4f,61.6f}) Box("Table leg",P(x,.44f,z),P(.18f,.88f,.18f),timber);
            foreach(float z in new[]{59.3f,62.7f})
            {
                Box("Meeting bench",P(-5,.5f,z),P(7,.16f,.45f),timber);
                foreach(float x in new[]{-8f,-2f}) Box("Bench support",P(x,.24f,z),P(.2f,.48f,.4f),timber);
            }
            Box("Secret river chart",P(-5,1.045f,61),P(2.6f,.025f,1.4f),chart,false);
            for(int i=0;i<5;i++) Box("Chart river markings",P(-5.8f+i*.4f,1.065f,61),P(.06f,.015f,.95f),water,false).transform.rotation=Quaternion.Euler(0,i*17,0);
            var point=new GameObject("documents • Ban do ben song");point.transform.SetParent(decor);point.transform.position=P(-5,0,61);
            var fp=point.AddComponent<ForestPoint>();fp.id="documents";fp.label="Thu thập bản đồ và ghi chép bến sông";fp.kind=ForestPointKind.Documents;
            for(int i=0;i<3;i++)
            {
                var lamp=new GameObject("Warm bunker lamp");lamp.transform.SetParent(decor);lamp.transform.position=P(-10+i*5,2.4f,62);
                var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.64f,.26f);light.range=8;light.intensity=2;
            }
            Box("Wall map",P(-5,1.8f,64.95f),P(3.6f,1.6f,.035f),chart,false);
            for(int i=0;i<4;i++) Box("Abandoned supply crate",P(8+(i%2)*2,.5f,57+i/2*2),P(1.5f,1,1.2f),timber);
            // Bamboo clumps frame the shelter without blocking the main route.
            foreach(float x in new[]{-18f,17f}) for(int i=0;i<22;i++)
            {
                float z=48+i;float xx=x+Mathf.Sin(i*2.3f)*1.7f;float h=4+i%4*.5f;
                Box("Bamboo stem",P(xx,h/2,z),P(.14f,h,.14f),timber,false);
                Box("Bamboo foliage",P(xx,h-.6f,z),P(1.7f,1.1f,1.4f),reed,false);
            }
        }
    }
}

