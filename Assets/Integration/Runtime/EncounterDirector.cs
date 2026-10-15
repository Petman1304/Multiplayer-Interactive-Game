using UnityEngine;

namespace Getaway.Integration
{
    public enum WaveAdmission : byte { Pending, Ready, Skip }
    public static class EncounterDirector
    {
        public static WaveAdmission Schedule(RouteDefinition route, RoundState state, long circuit, int index,
            RoadThreat[] admission, int existingCount, RoadThreat[] wave, bool[] reachable, bool[] reachableNext,
            float policeCooldownUntil, out int count)
        {
            count = 0;
            if (route.Encounters.Length == 0) return WaveAdmission.Pending;
            var entry = route.Encounters[index];
            double anchor = circuit * route.Length + entry.Distance;
            bool police = entry.Kind == EncounterKind.Police;
            if (state.Distance < anchor - (police ? 0 : state.Speed * 6)) return WaveAdmission.Pending;
            double following = circuit * route.Length + (index + 1 < route.Encounters.Length
                ? route.Encounters[index + 1].Distance : route.Length + route.Encounters[0].Distance);
            double local = state.Distance - circuit * route.Length;
            bool recovery = entry.Kind == EncounterKind.Recovery;
            bool expired = state.Distance > System.Math.Min(anchor + 100, following - 55)
                || (!recovery && local >= 1500);
            if (!expired)
            {
                int active = existingCount, obstacles = existingCount, trafficAhead = 0; bool chasing = false;
                for (int i = 0; i < existingCount; i++)
                {
                    var t = admission[i]; chasing |= t.Kind == ThreatKind.Police;
                    if (t.Kind == ThreatKind.Traffic && t.RouteDistance > state.Distance) trafficAhead++;
                }
                bool suppressed = !recovery && route.NearBendBoundary(state.Distance);
                suppressed |= !recovery && !police && Mathf.Abs(state.LateralSpeed - RouteGeometry.CurveDrift(route, state.Distance, state.Speed)) > 1;
                suppressed |= police && (chasing || state.Elapsed < policeCooldownUntil || trafficAhead > 1);
                suppressed |= chasing && (entry.Kind == EncounterKind.Staggered || entry.Kind == EncounterKind.LaneChange
                    || (entry.Kind == EncounterKind.SlowTraffic && trafficAhead >= 1) || entry.Kind == EncounterKind.BlockedLane);
                count = chasing && entry.Kind == EncounterKind.SlowTraffic ? 1 : EncounterDirector.Count(entry, circuit);
                if (!suppressed && active + count <= EncounterMotion.Budget(circuit))
                {
                    for (int part = 0; part < count; part++)
                    {
                        wave[part] = EncounterDirector.Activate(route, state.RouteSeed, circuit, index, part,
                            state.Distance, state.Speed, Mathf.Min(42, 22 + state.Elapsed * .065f + .39f));
                        admission[obstacles + part] = wave[part];
                    }
                    bool reserved = true;
                    // Reserve the entire lateral change corridor against other vehicles for its duration.
                    if (entry.Kind == EncounterKind.LaneChange)
                        for (int i = 0; i < obstacles; i++)
                            if (admission[i].Kind != ThreatKind.Repair && admission[i].Kind != ThreatKind.Police
                                && System.Math.Abs(admission[i].RouteDistance - wave[0].RouteDistance) < 60)
                                reserved = false;
                    if (entry.Kind == EncounterKind.LaneChange && (route.NearBendBoundary(state.Distance + EncounterMotion.Travel(state.Speed, wave[0].SignalSeconds)))) reserved = false;
                    if (!reserved) { wave[0].Phase = ThreatPhase.Cruise; wave[0].TargetLane = wave[0].StartLane; admission[obstacles] = wave[0]; }
                    // A staggered wave must finish before the recovery stretch or following encounter.
                    bool fits = entry.Kind != EncounterKind.Staggered || wave[count - 1].RouteDistance + 10 <
                        System.Math.Min(circuit * route.Length + 1500, following - 25);
                    bool safe = recovery || police || (fits && EncounterMotion.SafeWave(route, state.Distance,
                        state.TruckX, state.Speed, admission, obstacles + count, 36, reachable, reachableNext));
                    if (safe && entry.Kind == EncounterKind.LaneChange && wave[0].Phase == ThreatPhase.Signalling)
                    {
                        var planned = admission[obstacles]; admission[obstacles].Phase = ThreatPhase.Cruise;
                        bool stationaryLaneSafe = EncounterMotion.SafeWave(route, state.Distance, state.TruckX, state.Speed,
                            admission, obstacles + count, 36, reachable, reachableNext);
                        admission[obstacles] = planned; safe = stationaryLaneSafe;
                    }
                    if (!safe && entry.Kind == EncounterKind.LaneChange)
                    {
                        wave[0].Phase = ThreatPhase.Cruise; wave[0].TargetLane = wave[0].StartLane; admission[obstacles] = wave[0];
                        safe = EncounterMotion.SafeWave(route, state.Distance, state.TruckX, state.Speed,
                            admission, obstacles + count, 36, reachable, reachableNext);
                    }
                    if (safe) return WaveAdmission.Ready;
                }
                return WaveAdmission.Pending; // Retry until expiration; never force an unsafe wave.
            }
            return WaveAdmission.Skip;
        }
        public static int Count(RouteEncounter encounter, long circuit) => (encounter.Kind == EncounterKind.Staggered || (encounter.Kind == EncounterKind.SlowTraffic && circuit > 0)) ? 2 : 1;
        public static RoadThreat Activate(RouteDefinition route, int seed, long circuit, int index, int part, double distance, float speed, float cruiseSpeed = 0)
        {
            var e = route.Encounters[index]; var t = Threat(route, seed, circuit, index, part);
            if (e.Kind == EncounterKind.LaneChange) t.LaneOffset = 0;
            if (e.Kind == EncounterKind.SlowTraffic && part > 0) t.LaneOffset = (t.LaneOffset < 0 ? 1 : -1) * 5.2f;
            t.Speed = t.Kind == ThreatKind.Traffic ? Mathf.Clamp(speed * (e.SpeedFraction > 0 ? e.SpeedFraction : .65f), 14, 28) : 0;
            if (e.Kind == EncounterKind.Staggered) { t.Kind = ThreatKind.Barricade; t.Speed = 0; t.Health = 1; }
            float halfLength = (GetawayRules.TruckCollisionSize.z + GetawayRules.ThreatSize(t.Kind).z) * .5f;
            float intendedLead = EncounterMotion.Travel(speed, 6, cruiseSpeed > 0 ? cruiseSpeed : speed + .39f) - t.Speed * 6;
            float minimumWarningLead = EncounterMotion.Travel(speed, 4) - t.Speed * 4;
            t.RouteDistance = distance + Mathf.Max(intendedLead, minimumWarningLead) + halfLength + 5 + part * speed * 6;
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
        // Authoring contract only. Runtime admission validates complete moving waves, including
        // existing threats and current truck position; unsafe choices may be deferred or skipped.
        public static bool Validate(RouteDefinition route, int seed, long circuit, out string reason)
        {
            double previous = -1;
            foreach (var e in route.Encounters)
            {
                if (double.IsNaN(e.Distance) || double.IsInfinity(e.Distance) || e.Distance < 0 || e.Distance >= route.Length || e.Distance <= previous
                    || e.Kind < EncounterKind.BlockedLane || e.Kind > EncounterKind.LaneChange
                    || !GetawayRules.Finite(e.SpeedFraction) || e.SpeedFraction < 0 || e.SpeedFraction > 1
                    || !GetawayRules.Finite(e.SignalSeconds) || e.SignalSeconds < 0
                    || !GetawayRules.Finite(e.ChangeSeconds) || e.ChangeSeconds < 0)
                { reason = "Invalid encounter authoring data"; return false; }
                previous = e.Distance;
            }
            reason = "Ordered encounter anchors; moving corridors require runtime admission"; return true;
        }
    }
}
