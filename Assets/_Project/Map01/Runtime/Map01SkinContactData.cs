using System;
using UnityEngine;
namespace ShadowVale.Map01
{
    // Editable models remain untouched and non-readable in the player build. The editor
    // caches just the bind-space samples needed to calculate the live skin's floor contact.
    public sealed class Map01SkinContactData:ScriptableObject
    {
        [Serializable]public struct Vertex
        {
            public int a,b,c,d;
            public float wa,wb,wc,wd;
            public Vector3 pa,pb,pc,pd;
        }
        [Serializable]public sealed class Skin
        {
            public Mesh mesh;
            public Vertex[] vertices;
        }
        public Skin[] skins;
    }
}
