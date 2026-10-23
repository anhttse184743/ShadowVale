using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>The catalog prop held by the throwing hand. Never owns gameplay, inventory or fuse timing.</summary>
    public static class Map01ThrowableVisual
    {
        public static GameObject Create(string item, Transform hand, out Material ownedMaterial)
        {
            ownedMaterial=null;
            var catalog=Map01ItemCatalog.Load();
            var model=item!="stone"&&catalog!=null?catalog.Model(item):null;
            GameObject go;
            if(model!=null){go=Object.Instantiate(model,hand,false);go.transform.localScale=Vector3.one*catalog.Scale(item);}
            else {
                go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.transform.SetParent(hand,false);
                go.transform.localScale=Vector3.one*.10f;
                var renderer=go.GetComponent<Renderer>();ownedMaterial=new Material(renderer.sharedMaterial);
                ownedMaterial.color=new Color(.46f,.44f,.39f);renderer.sharedMaterial=ownedMaterial;
            }
            go.name="Throwable in hand — "+item;
            foreach(var collider in go.GetComponentsInChildren<Collider>()){
                if(Application.isPlaying)Object.Destroy(collider);else Object.DestroyImmediate(collider);}
            // Props are authored with a floor origin. Centre the visible body in the palm, rather than
            // putting the floor origin at the wrist and leaving a canister hanging outside the fist.
            go.transform.localPosition=Vector3.zero;
            go.transform.localRotation=item=="grenade_f1"?Quaternion.FromToRotation(Vector3.left,Vector3.up):Quaternion.identity;
            var renderers=go.GetComponentsInChildren<Renderer>();
            if(renderers.Length>0){var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                go.transform.position+=hand.TransformPoint(Vector3.up*.06f)-bounds.center;}
            return go;
        }
    }
}
