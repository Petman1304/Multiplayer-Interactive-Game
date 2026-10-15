using UnityEngine;
namespace Getaway.Integration
{
    /// <summary>Allocation-free movement and conservative time-dependent encounter admission.</summary>
    public static class EncounterMotion
    {
        public const float SignalTime = 2, ChangeTime = 3;
        public static int Budget(long circuit) => circuit == 0 ? 4 : circuit == 1 ? 6 : 8;
        public static float Travel(float speed, float seconds)
        {
            float accelerating = Mathf.Min(seconds, Mathf.Max(0, (42 - speed) / 5));
            return speed * accelerating + 2.5f * accelerating * accelerating + 42 * (seconds - accelerating);
        }
        public static RoadThreat Predict(RoadThreat t, float seconds)
        {
            t.RouteDistance += t.Speed * seconds;
            if (t.Phase == ThreatPhase.Signalling || t.Phase == ThreatPhase.Changing)
            {
                float elapsed = t.PhaseTime + seconds - (t.Phase == ThreatPhase.Signalling ? t.SignalSeconds : 0);
                float u = Mathf.Clamp01(elapsed / Mathf.Max(.1f, t.ChangeSeconds));
                t.LaneOffset = Mathf.Lerp(t.StartLane, t.TargetLane, u * u * (3 - 2 * u));
            }
            return t;
        }
        public static void TrafficStep(ref RoadThreat t, float dt)
        {
            var next = Predict(t, dt); t.RouteDistance = next.RouteDistance; t.LaneOffset = next.LaneOffset;
            if (t.Phase != ThreatPhase.Signalling && t.Phase != ThreatPhase.Changing) return;
            t.PhaseTime += dt;
            if (t.Phase == ThreatPhase.Signalling && t.PhaseTime >= t.SignalSeconds)
            { t.PhaseTime -= t.SignalSeconds; t.Phase = ThreatPhase.Changing; }
            if (t.Phase == ThreatPhase.Changing && t.PhaseTime >= t.ChangeSeconds)
            { t.Phase = ThreatPhase.Cruise; t.PhaseTime = 0; t.LaneOffset = t.TargetLane; }
        }
        public static PursuitEvent PoliceStep(RouteDefinition route, ref RoadThreat t, double distance, float truckX, float speed, float dt)
        {
            t.Age += dt;
            t.Speed = Mathf.MoveTowards(t.Speed, Mathf.Min(34, speed + 4), 6 * dt);
            t.RouteDistance = System.Math.Min(t.RouteDistance + t.Speed * dt, distance - 22);
            t.LaneOffset = Mathf.MoveTowards(t.LaneOffset, truckX, .65f * dt);
            t.Z = (float)(t.RouteDistance - distance);
            t.EscapeTime = t.Z < -75 ? t.EscapeTime + dt : 0;
            if (t.EscapeTime >= 5 || t.Age >= 30)
            { t.Active = false; return t.EscapeTime >= 5 ? PursuitEvent.Escaped : PursuitEvent.Withdrawn; }
            if (t.Phase == ThreatPhase.Approach && t.Age >= 2 && t.Z >= -45 && !route.NearBendBoundary(distance))
            { t.Phase = ThreatPhase.Pursuing; return PursuitEvent.Pursuing; }
            return PursuitEvent.None;
        }
        // Scratch arrays belong to the caller, never to shared static state.
        // Conservative reachable positions reserve acceleration time and expand every obstacle.
        public static bool SafeWave(RouteDefinition route, double distance, float truckX, float speed,
            RoadThreat[] obstacles, int count, float horizon, bool[] previous, bool[] next)
        {
            const float step = .2f, spacing = .25f; const int cells = 43; const float left = -5.25f;
            System.Array.Clear(previous, 0, cells);
            int start = Mathf.Clamp(Mathf.RoundToInt((truckX - left) / spacing), 0, cells - 1); previous[start] = true;
            for (int tick = 1; tick <= Mathf.CeilToInt(horizon / step); tick++)
            {
                float time = tick * step, before = (tick - 1) * step;
                double d = distance + Travel(speed, time), oldD = distance + Travel(speed, before);
                float limit = RouteGeometry.TruckLateralLimit(route, d) - .1f;
                float drift = Mathf.Abs(RouteGeometry.CurveDrift(route, d, Mathf.Min(42, speed + 5 * time)));
                float reach = time < .4f ? 0 : Mathf.Max(0, 8 - drift) * step;
                System.Array.Clear(next, 0, cells); bool any = false;
                for (int to = 0; to < cells; to++)
                {
                    float x = left + to * spacing; if (Mathf.Abs(x) > limit) continue;
                    for (int from = 0; from < cells; from++)
                    {
                        if (!previous[from] || Mathf.Abs(to - from) * spacing > reach + .0001f) continue;
                        bool clear = true;
                        for (int i = 0; i < count && clear; i++)
                        {
                            var obstacle = obstacles[i];
                            if (!obstacle.Active || obstacle.Kind == ThreatKind.Repair || obstacle.Kind == ThreatKind.Police) continue;
                            var a = Predict(obstacle, before); var b = Predict(obstacle, time);
                            // Inflate lateral footprint for quantization and steering uncertainty.
                            float forbidden = (GetawayRules.TruckCollisionSize.x + GetawayRules.ThreatSize(obstacle.Kind).x) * .5f + .35f;
                            double z = b.RouteDistance - d;
                            float halfLength = (GetawayRules.TruckCollisionSize.z + GetawayRules.ThreatSize(obstacle.Kind).z) * .5f + 1;
                            if (System.Math.Abs(z - GetawayRules.TruckCollisionCenter.z) < halfLength && Mathf.Abs(x - b.LaneOffset) < forbidden) clear = false;
                            else if (RouteGeometry.CrossesTruck(route, a, b, oldD, d, left + from * spacing, x)) clear = false;
                        }
                        if (clear) { next[to] = true; any = true; break; }
                    }
                }
                if (!any) return false;
                var swap = previous; previous = next; next = swap;
            }
            return true;
        }
    }
}
