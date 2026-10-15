using UnityEditor;
using UnityEngine;

namespace ShadowVale.Map01.Editor
{
    public static class Map01ForestFloor
    {
        private const string TexturePath = "Assets/_Project/Art/Environment/Map01Detailed/GroundTexture.asset";

        [MenuItem("ShadowVale/Map 1/Remove painted forest trails")]
        public static void RemoveTrails()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            Undo.RecordObject(texture, "Remove painted forest trails");
            var pixels = texture.GetPixels();
            for (int y = 0; y < texture.height; y++)
                for (int x = 0; x < texture.width; x++)
                {
                    float worldX = x * 220f / (texture.width - 1) - 110;
                    float worldZ = y * 200f / (texture.height - 1) - 100;
                    float riverX = Mathf.Sin(worldZ * .036f) * 9;
                    float bankDistance = Mathf.Abs(worldX - riverX);
                    // Keep the channel; rebuild the soft bank transition so old road
                    // junctions cannot remain as short rectangular stubs beside bridges.
                    if (bankDistance <= 6) continue;
                    float grain = Mathf.PerlinNoise(worldX * .6f + 80, worldZ * .6f + 80);
                    float patches = Mathf.PerlinNoise(worldX * .075f + 31, worldZ * .075f + 17);
                    var floor = Color.Lerp(new Color(.215f, .25f, .115f), new Color(.245f, .28f, .13f), patches);
                    floor *= .78f + grain * .4f;
                    int riverPixel = Mathf.Clamp(Mathf.RoundToInt((riverX + 110) / 220 * (texture.width - 1)), 0, texture.width - 1);
                    var soil = pixels[y * texture.width + riverPixel] * (.9f + grain * .2f);
                    pixels[y * texture.width + x] = Color.Lerp(soil, floor, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(6, 11, bankDistance)));
                }
            texture.SetPixels(pixels);
            texture.Apply(true);
            EditorUtility.SetDirty(texture);
            AssetDatabase.SaveAssets();
        }
    }
}
