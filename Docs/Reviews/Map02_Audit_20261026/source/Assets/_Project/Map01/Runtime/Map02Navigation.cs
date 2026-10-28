using UnityEngine;
namespace ShadowVale.Map01
{
    /// <summary>One north-up projection for the survey, minimap and full map. Never camera-relative.</summary>
    public static class Map02Navigation
    {
        public const float HalfSize=110;
        public static Vector2 ChartUv(Vector3 world)=>new Vector2((world.x+HalfSize)/(HalfSize*2),(world.z+HalfSize)/(HalfSize*2));
        public static Vector2 Plot(Vector3 world,Rect area){var uv=ChartUv(world);return new Vector2(area.x+uv.x*area.width,area.y+(1-uv.y)*area.height);}
        public static Vector2 Disc(Vector3 world,Vector2 center,float radius)=>center+Vector2.ClampMagnitude(new Vector2(world.x,-world.z)/HalfSize*radius,radius-8);
        public static Matrix4x4 RotateUi(Matrix4x4 canvas,Vector2 pivot,float degrees)=>canvas*Matrix4x4.Translate(new Vector3(pivot.x,pivot.y,0))*Matrix4x4.Rotate(Quaternion.Euler(0,0,degrees))*Matrix4x4.Translate(new Vector3(-pivot.x,-pivot.y,0));
    }
}
