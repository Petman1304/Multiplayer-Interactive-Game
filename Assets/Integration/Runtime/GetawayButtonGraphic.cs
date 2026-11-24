using UnityEngine;

namespace Getaway.Integration
{
    /// <summary>Scalable, label-free version of the purple/cyan UI Button artwork.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GetawayButtonGraphic : UnityEngine.UI.MaskableGraphic
    {
        // Warna Flat Biru Tua (#1E293B) untuk permukaan tombol:
        public Color Top = new Color32(30, 41, 59, 255);
        public Color Bottom = new Color32(30, 41, 59, 255);
        // Warna Aksen Kuning/Oranye (#F59E0B) untuk bingkai/outline:
        public Color Outline = new Color32(245, 158, 11, 255);


        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float radius = Mathf.Min(15, rect.height * .25f);
            const int segments = 8, count = 4 * (segments + 1);
            for (int ring = 0; ring < 2; ring++)
            {
                float inset = ring == 0 ? 0 : 2;
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector2 center = new Vector2(corner < 2 ? rect.xMax - radius : rect.xMin + radius,
                        corner == 0 || corner == 3 ? rect.yMax - radius : rect.yMin + radius);
                    for (int step = 0; step <= segments; step++)
                    {
                        float angle = (90 - corner * 90 - step * 90f / segments) * Mathf.Deg2Rad;
                        Vector2 position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Mathf.Max(0, radius - inset);
                        Color tint = ring == 0 ? Outline : Color.Lerp(Bottom, Top, Mathf.InverseLerp(rect.yMin, rect.yMax, position.y));
                        mesh.AddVert(position, tint * color, Vector2.zero);
                    }
                }
            }
            mesh.AddVert(rect.center, Color.Lerp(Bottom, Top, .5f) * color, Vector2.zero);
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                mesh.AddTriangle(i, next, count + i);
                mesh.AddTriangle(next, count + next, count + i);
                mesh.AddTriangle(count * 2, count + i, count + next);
            }
        }
    }
}
