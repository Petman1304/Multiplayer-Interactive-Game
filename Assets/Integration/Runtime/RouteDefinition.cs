using System;
using UnityEngine;

namespace Getaway.Integration
{
    public enum CitySectionKind : byte { BoulevardA, BoulevardB, LeftBend, RightBend, Construction, Landmark }
    public enum EncounterKind : byte { BlockedLane, Staggered, Police, Recovery, SlowTraffic, LaneChange }
    [Serializable] public struct RouteCurve { public Vector3 P0, P1, P2, P3; }
    [Serializable] public struct CitySection { public double Start, Length; public CitySectionKind Kind; }
    [Serializable] public struct RouteEncounter { public double Distance; public EncounterKind Kind; public int Lane; public float SpeedFraction, SignalSeconds, ChangeSeconds; }
    [Serializable] public struct RoutePoint
    {
        public double Distance, X, Z;
        public float Heading, Curvature;
        public Quaternion Rotation => Quaternion.Euler(0, Heading * Mathf.Rad2Deg, 0);
        public Vector3 Forward => new Vector3(Mathf.Sin(Heading), 0, Mathf.Cos(Heading));
        public Vector3 Right => new Vector3(Mathf.Cos(Heading), 0, -Mathf.Sin(Heading));
        public Vector3 RelativeTo(RoutePoint origin, float lateral = 0) =>
            new Vector3((float)(X - origin.X), 0, (float)(Z - origin.Z)) + Right * lateral;
    }
    /// <summary>Editor-baked metre-spaced path. All runtime geometry samples this immutable asset.</summary>
    [CreateAssetMenu(menuName = "Getaway/Route definition")]
    public sealed class RouteDefinition : ScriptableObject
    {
        public string DistrictName = "Downtown";
        public int DistrictCount = 1;
        public float CurveDriftMetres = 18;
        public RouteCurve[] Curves = Array.Empty<RouteCurve>();
        public CitySection[] Sections = Array.Empty<CitySection>();
        public RouteEncounter[] Encounters = Array.Empty<RouteEncounter>();
        public RoutePoint[] Points = Array.Empty<RoutePoint>();
        public double Length => Points.Length > 1 ? Points[Points.Length - 1].Distance : 0;
        public bool IsBaked => Length > 100;
        public RoutePoint Sample(double distance)
        {
            if (!IsBaked) throw new InvalidOperationException("Bake the route before sampling.");
            long circuit = (long)Math.Floor(distance / Length);
            double local = distance - circuit * Length;
            int lo = 0, hi = Points.Length - 1;
            while (hi - lo > 1) { int mid = (lo + hi) / 2; if (Points[mid].Distance <= local) lo = mid; else hi = mid; }
            var a = Points[lo]; var b = Points[hi];
            float t = (float)((local - a.Distance) / Math.Max(.000001, b.Distance - a.Distance));
            var point = new RoutePoint { Distance = distance, X = a.X + (b.X - a.X) * t, Z = a.Z + (b.Z - a.Z) * t,
                Heading = a.Heading + Mathf.DeltaAngle(a.Heading * Mathf.Rad2Deg, b.Heading * Mathf.Rad2Deg) * Mathf.Deg2Rad * t,
                Curvature = Mathf.Lerp(a.Curvature, b.Curvature, t) };
            var end = Points[Points.Length - 1];
            // Exponentiation of a planar rigid transform also supports sampling behind the start.
            double tx = 0, tz = 0, angle = 0, bx = end.X, bz = end.Z, ba = end.Heading;
            if (circuit < 0) { double c = Math.Cos(ba), s = Math.Sin(ba); double x = -c * bx + s * bz; bz = -s * bx - c * bz; bx = x; ba = -ba; circuit = -circuit; }
            while (circuit > 0)
            {
                if ((circuit & 1) != 0) { double c = Math.Cos(angle), s = Math.Sin(angle); tx += c * bx + s * bz; tz += -s * bx + c * bz; angle += ba; }
                double bc = Math.Cos(ba), bs = Math.Sin(ba), nx = bx + bc * bx + bs * bz;
                bz += -bs * bx + bc * bz; bx = nx; ba *= 2; circuit >>= 1;
            }
            double ca = Math.Cos(angle), sa = Math.Sin(angle), px = point.X;
            point.X = tx + ca * px + sa * point.Z; point.Z = tz - sa * px + ca * point.Z;
            point.Heading += (float)angle; return point;
        }
        public int Circuit(double distance) => (int)Math.Floor(Math.Max(0, distance) / Length) + 1;
        public CitySectionKind SectionAt(double distance)
        {
            double local = distance - Math.Floor(distance / Length) * Length;
            for (int i = Sections.Length - 1; i >= 0; i--) if (local >= Sections[i].Start) return Sections[i].Kind;
            return CitySectionKind.BoulevardA;
        }
        public bool NextBend(double distance, out int direction, out float metres)
        {
            long loop = (long)Math.Floor(distance / Length);
            for (int repeat = 0; repeat < 2; repeat++)
                foreach (var section in Sections)
                    if ((section.Kind == CitySectionKind.LeftBend || section.Kind == CitySectionKind.RightBend) &&
                        (loop + repeat) * Length + section.Start + section.Length > distance)
                    { direction = section.Kind == CitySectionKind.LeftBend ? -1 : 1; metres = (float)Math.Max(0, (loop + repeat) * Length + section.Start - distance); return true; }
            direction = 0; metres = 0; return false;
        }
        public bool NearBendBoundary(double distance, double margin = 55)
        {
            double local = distance - Math.Floor(distance / Length) * Length;
            foreach (var section in Sections)
                if ((section.Kind == CitySectionKind.LeftBend || section.Kind == CitySectionKind.RightBend) &&
                    (Math.Abs(local - section.Start) < margin || Math.Abs(local - section.Start - section.Length) < margin)) return true;
            return false;
        }
    }
}
