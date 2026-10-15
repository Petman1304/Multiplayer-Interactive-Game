using System;
using System.Collections.Generic;
using Getaway.Integration;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit editor operations, invoked through Pipeline; never runs a build or starts Play.</summary>
public static class DowntownRouteAuthoring
{
    public const string AssetPath = "Assets/Integration/Environment/DowntownRoute.asset";
    public static RouteDefinition Create()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before authoring assets.");
        if (!AssetDatabase.IsValidFolder("Assets/Integration/Environment")) AssetDatabase.CreateFolder("Assets/Integration", "Environment");
        var route = AssetDatabase.LoadAssetAtPath<RouteDefinition>(AssetPath);
        if (route == null) { route = ScriptableObject.CreateInstance<RouteDefinition>(); AssetDatabase.CreateAsset(route, AssetPath); }
        var curves = new List<RouteCurve>(); var sections = new List<CitySection>();
        Vector3 position = Vector3.zero; float heading = 0; double distance = 0;
        // Ten straight blocks and four eased, 200 m bends: 1,800 m total.
        var kinds = new[] { CitySectionKind.BoulevardA, CitySectionKind.BoulevardB, CitySectionKind.Construction,
            CitySectionKind.LeftBend, CitySectionKind.BoulevardA, CitySectionKind.RightBend,
            CitySectionKind.Landmark, CitySectionKind.BoulevardB, CitySectionKind.Construction,
            CitySectionKind.RightBend, CitySectionKind.BoulevardA, CitySectionKind.LeftBend,
            CitySectionKind.BoulevardB, CitySectionKind.BoulevardA };
        foreach (var kind in kinds)
        {
            bool bend = kind == CitySectionKind.LeftBend || kind == CitySectionKind.RightBend;
            int length = bend ? 200 : 100; float angle = bend ? (kind == CitySectionKind.LeftBend ? -1 : 1) * Mathf.PI / 4 : 0;
            sections.Add(new CitySection { Start = distance, Length = length, Kind = kind });
            float baseHeading = heading;
            for (int metre = 0; metre < length; metre += 10)
            {
                Vector3 start = position; float startHeading = heading;
                // Smooth curvature is zero at both ends; peak radius is approximately 127 m.
                for (int step = 0; step < 10; step++)
                {
                    float u = (metre + step + .5f) / length;
                    float h = baseHeading + angle * (u - Mathf.Sin(2 * Mathf.PI * u) / (2 * Mathf.PI));
                    position += new Vector3(Mathf.Sin(h), 0, Mathf.Cos(h));
                }
                float endU = (metre + 10f) / length;
                heading = baseHeading + angle * (endU - Mathf.Sin(2 * Mathf.PI * endU) / (2 * Mathf.PI));
                var f0 = new Vector3(Mathf.Sin(startHeading), 0, Mathf.Cos(startHeading));
                var f1 = new Vector3(Mathf.Sin(heading), 0, Mathf.Cos(heading));
                curves.Add(new RouteCurve { P0 = start, P1 = start + f0 * (10f / 3), P2 = position - f1 * (10f / 3), P3 = position });
            }
            distance += length;
        }
        route.Curves = curves.ToArray(); route.Sections = sections.ToArray(); Bake(route);
        EditorUtility.SetDirty(route); AssetDatabase.SaveAssets(); return route;
    }
    public static void Bake(RouteDefinition route)
    {
        var dense = new List<RoutePoint>(); double distance = 0; Vector3 previous = route.Curves[0].P0;
        foreach (var curve in route.Curves)
            for (int step = 0; step <= 40; step++)
            {
                if (step == 0 && dense.Count > 0) continue;
                float t = step / 40f, u = 1 - t;
                Vector3 p = u * u * u * curve.P0 + 3 * u * u * t * curve.P1 + 3 * u * t * t * curve.P2 + t * t * t * curve.P3;
                Vector3 d = 3 * u * u * (curve.P1 - curve.P0) + 6 * u * t * (curve.P2 - curve.P1) + 3 * t * t * (curve.P3 - curve.P2);
                Vector3 dd = 6 * u * (curve.P2 - 2 * curve.P1 + curve.P0) + 6 * t * (curve.P3 - 2 * curve.P2 + curve.P1);
                distance += Vector3.Distance(previous, p); previous = p;
                float curvature = (d.z * dd.x - d.x * dd.z) / Mathf.Max(.001f, Mathf.Pow(d.sqrMagnitude, 1.5f));
                dense.Add(new RoutePoint { Distance = distance, X = p.x, Z = p.z, Heading = Mathf.Atan2(d.x, d.z), Curvature = curvature });
            }
        var points = new List<RoutePoint>(); int index = 0;
        for (double at = 0; at < distance; at += 1)
        {
            while (index + 1 < dense.Count - 1 && dense[index + 1].Distance < at) index++;
            var a = dense[index]; var b = dense[index + 1]; float t = (float)((at - a.Distance) / Math.Max(.000001, b.Distance - a.Distance));
            points.Add(new RoutePoint { Distance = at, X = a.X + (b.X - a.X) * t, Z = a.Z + (b.Z - a.Z) * t,
                Heading = Mathf.Lerp(a.Heading, b.Heading, t), Curvature = Mathf.Lerp(a.Curvature, b.Curvature, t) });
        }
        points.Add(dense[dense.Count - 1]); route.Points = points.ToArray();
        // Authored sections are expressed in nominal metres; reconcile tiny bake integration error.
        double ratio = distance / 1800;
        for (int i = 0; i < route.Sections.Length; i++) { var s = route.Sections[i]; s.Start *= ratio; s.Length *= ratio; route.Sections[i] = s; }
    }
}
