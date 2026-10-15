using UnityEngine;

namespace Getaway.Integration
{
    /// <summary>Scalable, label-free version of the purple/cyan UI Button artwork.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GetawayButtonGraphic : UnityEngine.UI.MaskableGraphic
    {
        public Color Top = new Color(.63f, .035f, .76f);
        public Color Bottom = new Color(.13f, .16f, .69f);
        public Color Outline = new Color(.46f, .97f, 1);
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
