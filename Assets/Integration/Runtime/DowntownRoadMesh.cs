using UnityEngine;
namespace Getaway.Integration
{
    /// <summary>Fixed-size road buffers; rebuild only when a pooled tile is recycled.</summary>
    public sealed class DowntownRoadMesh
    {
        const int Steps = 50, Strips = 10;
        readonly Vector3[] vertices = new Vector3[Steps * Strips * 4];
        readonly Vector3[] normals = new Vector3[Steps * Strips * 4];
        readonly Vector2[] uv = new Vector2[Steps * Strips * 4];
        public readonly Mesh Mesh;
        static readonly float[] Left = { -55, -8, -14, 8, -8.2f, 8, -2.66f, 2.54f, -7.8f, 7.7f };
        static readonly float[] Right = { 55, 8, -8, 14, -8, 8.2f, -2.54f, 2.66f, -7.7f, 7.8f };
        static readonly float[] Height = { -.12f, 0, .18f, .18f, .22f, .22f, .012f, .012f, .012f, .012f };
        static readonly int[] Material = { 0, 1, 2, 2, 3, 3, 4, 4, 4, 4 };
        public DowntownRoadMesh()
        {
            Mesh = new Mesh { name = "Pooled curved street" }; Mesh.MarkDynamic();
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            Mesh.vertices = vertices; Mesh.normals = normals; Mesh.subMeshCount = 5;
            for (int material = 0; material < 5; material++)
            {
                int strips = 0; for (int s = 0; s < Strips; s++) if (Material[s] == material) strips++;
                var indices = new int[Steps * strips * 6]; int n = 0;
                for (int step = 0; step < Steps; step++) for (int strip = 0; strip < Strips; strip++)
                    if (Material[strip] == material)
                    { int v = (step * Strips + strip) * 4; indices[n++] = v; indices[n++] = v + 2; indices[n++] = v + 1; indices[n++] = v; indices[n++] = v + 3; indices[n++] = v + 2; }
                Mesh.SetTriangles(indices, material, false);
            }
        }
        public void Rebuild(RouteDefinition route, double start)
        {
            var origin = route.Sample(start);
            for (int step = 0; step < Steps; step++)
            {
                var a = route.Sample(start + step * 2); var b = route.Sample(start + (step + 1) * 2);
                for (int strip = 0; strip < Strips; strip++)
                {
                    float left = Left[strip], right = Right[strip];
                    if ((strip == 6 || strip == 7) && ((long)System.Math.Floor((start + step * 2) / 6) & 1) != 0) right = left;
                    int v = (step * Strips + strip) * 4;
                    vertices[v] = a.RelativeTo(origin, left) + Vector3.up * Height[strip];
                    vertices[v + 1] = a.RelativeTo(origin, right) + Vector3.up * Height[strip];
                    vertices[v + 2] = b.RelativeTo(origin, right) + Vector3.up * Height[strip];
                    vertices[v + 3] = b.RelativeTo(origin, left) + Vector3.up * Height[strip];
                    uv[v] = new Vector2(left, step * 2); uv[v + 1] = new Vector2(right, step * 2);
                    uv[v + 2] = new Vector2(right, (step + 1) * 2); uv[v + 3] = new Vector2(left, (step + 1) * 2);
                }
            }
            Mesh.SetVertices(vertices); Mesh.SetUVs(0, uv); Mesh.RecalculateBounds();
        }
    }
}
