using UnityEngine;

namespace Getaway.Integration
{
    public static class EncounterDirector
    {
        public static int Count(RouteEncounter encounter, long circuit) => encounter.Kind == EncounterKind.Staggered ? 2 : 1;
        public static RoadThreat Activate(RouteDefinition route, int seed, long circuit, int index, int part, double distance, float speed)
        {
            var e = route.Encounters[index]; var t = Threat(route, seed, circuit, index, part);
            t.Speed = t.Kind == ThreatKind.Traffic ? Mathf.Clamp(speed * (e.SpeedFraction > 0 ? e.SpeedFraction : .65f), 14, 28) : 0;
            if (e.Kind == EncounterKind.Staggered) { t.Kind = ThreatKind.Barricade; t.Speed = 0; }
            float halfLength = (GetawayRules.TruckCollisionSize.z + GetawayRules.ThreatSize(t.Kind).z) * .5f;
            t.RouteDistance = distance + EncounterMotion.Travel(speed, 6) - t.Speed * 6 + halfLength + 5 + part * speed * 6;
            t.StartLane = t.TargetLane = t.LaneOffset;
            if (e.Kind == EncounterKind.LaneChange)
            {
                int lane = Mathf.RoundToInt(t.LaneOffset / 5.2f);
                t.TargetLane = (lane == 0 ? (Lane(seed, circuit, index + 31) < 0 ? -1 : 1) : 0) * 5.2f;
                t.Phase = ThreatPhase.Signalling;
                t.SignalSeconds = e.SignalSeconds > 0 ? e.SignalSeconds : EncounterMotion.SignalTime;
                t.ChangeSeconds = e.ChangeSeconds > 0 ? e.ChangeSeconds : EncounterMotion.ChangeTime;
            }
            if (e.Kind == EncounterKind.Police)
            { t.RouteDistance = distance - 60; t.Speed = Mathf.Min(34, speed); t.Health = 6; t.Phase = ThreatPhase.Approach; }
            t.Z = (float)(t.RouteDistance - distance); return t;
        }
        public static int Lane(int seed, long circuit, int index)
        {
            uint h = unchecked((uint)seed ^ (uint)circuit * 747796405u ^ (uint)index * 2891336453u);
            h = (h ^ (h >> 16)) * 2246822519u; return (int)((h ^ (h >> 13)) % 3) - 1;
        }
        public static RoadThreat Threat(RouteDefinition route, int seed, long circuit, int index, int part)
        {
            var encounter = route.Encounters[index]; int lane = Lane(seed, circuit, index);
            if (encounter.Kind == EncounterKind.Staggered) { lane = lane < 0 ? -1 : 1; if (part > 0) lane = -lane; }
            var kind = encounter.Kind == EncounterKind.Recovery ? ThreatKind.Repair : encounter.Kind == EncounterKind.Police ? ThreatKind.Police :
                (encounter.Kind == EncounterKind.Staggered || encounter.Kind == EncounterKind.SlowTraffic || encounter.Kind == EncounterKind.LaneChange) ? ThreatKind.Traffic : ThreatKind.Barricade;
            return new RoadThreat { Active = true, Kind = kind, LaneOffset = lane * 5.2f,
                RouteDistance = circuit * route.Length + encounter.Distance + part * 45,
                Health = kind == ThreatKind.Police ? 3 : kind == ThreatKind.Traffic ? 2 : 1,
                Speed = kind == ThreatKind.Police ? 4 : kind == ThreatKind.Traffic ? 10 : 0 };
        }
        // Conservative path proof at maximum speed, using expanded truck footprint and lane-change time.
        public static bool Validate(RouteDefinition route, int seed, long circuit, out string reason)
        {
            int reachable = 2; double previous = circuit * route.Length;
            float worstDrift = 0;
            foreach (var point in route.Points) worstDrift = Mathf.Max(worstDrift, Mathf.Abs(point.Curvature * 42 * route.CurveDriftMetres));
            float lateralAuthority = Mathf.Max(.1f, 8 - worstDrift);
            for (int i = 0; i < route.Encounters.Length; i++)
                for (int part = 0; part < Count(route.Encounters[i], circuit); part++)
                {
                    var threat = Threat(route, seed, circuit, i, part);
                    if (threat.Kind == ThreatKind.Repair) continue;
                    if (route.NearBendBoundary(threat.RouteDistance)) { reason = "Hazard too near bend entry/exit"; return false; }
                    int next = 0;
                    float halfLength = (GetawayRules.TruckCollisionSize.z + GetawayRules.ThreatSize(threat.Kind).z) * .5f;
                    double availableTime = System.Math.Max(0, (threat.RouteDistance - previous - halfLength * 2) / 42);
                    for (int lane = -1; lane <= 1; lane++)
                    {
                        float x = lane * Mathf.Min(5.2f, RouteGeometry.TruckLateralLimit(route, threat.RouteDistance));
                        float forbidden = (GetawayRules.TruckCollisionSize.x + GetawayRules.ThreatSize(threat.Kind).x) * .5f + .2f;
                        if (Mathf.Abs(x - threat.LaneOffset) < forbidden) continue;
                        for (int from = -1; from <= 1; from++)
                            if ((reachable & (1 << (from + 1))) != 0 && (from == lane || Mathf.Abs(lane - from) * 5.2f / lateralAuthority + .36 <= availableTime)) next |= 1 << (lane + 1);
                    }
                    if (next == 0) { reason = "No reachable lane through encounter " + i; return false; }
                    reachable = next; previous = threat.RouteDistance;
                }
            reason = "Safe at 42 m/s, including full truck width and lateral acceleration allowance"; return true;
        }
    }
}
