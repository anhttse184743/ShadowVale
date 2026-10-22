using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ShadowVale.Map01
{
    /// <summary>Solid sides on the village bridges; the two ends stay open.</summary>
    public static class Map02BridgeRails
    {
        public const string Left="Bridge side barrier L",Right="Bridge side barrier R";
        public static List<BoxCollider> Install(Scene scene)
        {
            var rails=new List<BoxCollider>();
            foreach(var root in scene.GetRootGameObjects())foreach(var deck in root.GetComponentsInChildren<BoxCollider>(true))
            {
                if(deck.name!="Walkable bridge deck"&&deck.name!="Bridge deck")continue;
                foreach(int side in new[]{-1,1})
                {
                    string name=side<0?Left:Right;var child=deck.transform.Find(name);
                    if(child==null){child=new GameObject(name).transform;child.SetParent(deck.transform,false);}
                    child.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);child.localScale=Vector3.one;
                    child.gameObject.layer=deck.gameObject.layer;
                    var rail=child.GetComponent<BoxCollider>();if(rail==null)rail=child.gameObject.AddComponent<BoxCollider>();
                    rail.enabled=true;rail.isTrigger=false;
                    rail.center=new Vector3(deck.center.x,deck.center.y+deck.size.y*.5f+.6f,deck.center.z+side*(deck.size.z*.5f-.11f));
                    rail.size=new Vector3(deck.size.x,1.2f,.22f);rails.Add(rail);
                }
            }
            return rails;
        }
    }
}
