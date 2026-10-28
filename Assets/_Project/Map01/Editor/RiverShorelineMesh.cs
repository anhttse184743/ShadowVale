using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowVale.Map01.Editor
{
    /// <summary>Extends the rendered surface underneath opaque banks so waves cannot expose a seam.</summary>
    public static class RiverShorelineMesh
    {
        public const float BankOverlap = 3f;
        public const int CrossSegments = 16;

        public static Mesh Build(Vector3[] left, Vector3[] right, Matrix4x4 worldToLocal)
        {
            if (left.Length != right.Length || left.Length < 2)
                throw new ArgumentException("Matching river banks are required.");
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int row = 0; row < left.Length; row++)
            {
                var across = right[row] - left[row];
                across.y = 0;
                across.Normalize();
                var l = left[row] - across * BankOverlap;
                var r = right[row] + across * BankOverlap;
                for (int col = 0; col <= CrossSegments; col++)
                {
                    float u = col / (float)CrossSegments;
                    vertices.Add(worldToLocal.MultiplyPoint3x4(Vector3.Lerp(l, r, u)));
                    uvs.Add(new Vector2(u, row - 100));
                    if (row == 0 || col == 0) continue;
                    int k = row * (CrossSegments + 1) + col;
                    triangles.AddRange(new[] { k - CrossSegments - 2, k, k - CrossSegments - 1,
                        k - CrossSegments - 2, k - 1, k });
                }
            }
            var mesh = new Mesh { name = "PhysicalRiver_Surface" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var bounds = mesh.bounds;
            bounds.Expand(new Vector3(0, .4f, 0));
            mesh.bounds = bounds;
            return mesh;
        }
    }
}
