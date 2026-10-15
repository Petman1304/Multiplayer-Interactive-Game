using UnityEngine;
namespace Getaway.Integration
{
    /// <summary>Allocation-free movement and conservative time-dependent encounter admission.</summary>
    public static class EncounterMotion
    {
        public const float SignalTime = 2, ChangeTime = 3;
        public static int Budget(long circuit) => circuit == 0 ? 4 : circuit == 1 ? 6 : 8;
        public static float Travel(float speed, float seconds, float ceiling = 42, float acceleration = 5)
        {
            if (acceleration <= 0) return speed * seconds;
            ceiling = Mathf.Max(speed, ceiling);
            float accelerating = Mathf.Min(seconds, Mathf.Max(0, (ceiling - speed) / acceleration));
            return speed * accelerating + .5f * acceleration * accelerating * accelerating + ceiling * (seconds - accelerating);
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
            return SafeProfile(route, distance, truckX, speed, obstacles, count, horizon, previous, next, 0)
                && SafeProfile(route, distance, truckX, speed, obstacles, count, horizon, previous, next, .065f)
                && SafeProfile(route, distance, truckX, speed, obstacles, count, horizon, previous, next, 5);
        }
        // Bounds are projected into the current route frame. Unioned endpoint boxes
        // conservatively cover each steering edge and threat sweep; the gameplay collision
        // system still uses its finer route-oriented SAT substeps.
        static Bounds LocalBox(RoutePoint pose, RoutePoint origin, float lane, Vector3 size, Vector3 center)
        {
            var relative = pose.RelativeTo(origin, lane);
            float c = Mathf.Cos(origin.Heading), sn = Mathf.Sin(origin.Heading);
            float angle = pose.Heading - origin.Heading, ca = Mathf.Cos(angle), sa = Mathf.Sin(angle);
            var position = new Vector3(relative.x*c-relative.z*sn + ca*center.x+sa*center.z, 0,
                relative.x*sn+relative.z*c - sa*center.x+ca*center.z);
            return new Bounds(position, new Vector3(Mathf.Abs(ca)*size.x+Mathf.Abs(sa)*size.z, 2,
                Mathf.Abs(sa)*size.x+Mathf.Abs(ca)*size.z));
        }
        static bool SafeProfile(RouteDefinition route, double distance, float truckX, float speed,
            RoadThreat[] obstacles, int count, float horizon, bool[] previous, bool[] next, float acceleration)
        {
            const float step = .2f, spacing = 5.18f / 21; const int cells = 43; const float left = -5.18f;
            System.Span<Bounds> threats = stackalloc Bounds[20];
            System.Span<Bounds> oldTruck = stackalloc Bounds[cells];
            System.Span<Bounds> newTruck = stackalloc Bounds[cells];
            System.Array.Clear(previous, 0, cells);
            int start = Mathf.Clamp(Mathf.RoundToInt((truckX - left) / spacing), 0, cells - 1); previous[start] = true;
            for (int tick = 1; tick <= Mathf.CeilToInt(horizon / step); tick++)
            {
                float time = tick * step, before = (tick - 1) * step;
                double d = distance + Travel(speed, time, 42, acceleration), oldD = distance + Travel(speed, before, 42, acceleration);
                var origin = route.Sample(d); var oldPose = route.Sample(oldD);
                float limit = RouteGeometry.TruckLateralLimit(route, d) - .1f;
                float drift = Mathf.Abs(RouteGeometry.CurveDrift(route, d, Mathf.Min(42, speed + acceleration * time)));
                float reach = time < .4f ? 0 : Mathf.Max(0, 8 - drift) * step;
                int occupied = 0;
                for (int i=0;i<count;i++)
                {
                    var obstacle=obstacles[i];
                    if (!obstacle.Active || obstacle.Kind==ThreatKind.Repair || obstacle.Kind==ThreatKind.Police) continue;
                    var a=Predict(obstacle,before);var b=Predict(obstacle,time);
                    if (System.Math.Abs(b.RouteDistance-d)>30 && System.Math.Abs(a.RouteDistance-oldD)>30) continue;
                    var box=LocalBox(route.Sample(a.RouteDistance),origin,a.LaneOffset,GetawayRules.ThreatSize(a.Kind),Vector3.zero);
                    box.Encapsulate(LocalBox(route.Sample(b.RouteDistance),origin,b.LaneOffset,GetawayRules.ThreatSize(b.Kind),Vector3.zero));
                    box.Expand(new Vector3(.7f,0,.2f)); threats[occupied++]=box;
                }
                for(int cell=0;cell<cells;cell++)
                {
                    float x=left+cell*spacing;
                    oldTruck[cell]=LocalBox(oldPose,origin,x,GetawayRules.TruckCollisionSize,GetawayRules.TruckCollisionCenter);
                    newTruck[cell]=new Bounds(new Vector3(x+GetawayRules.TruckCollisionCenter.x,0,GetawayRules.TruckCollisionCenter.z),new Vector3(GetawayRules.TruckCollisionSize.x,2,GetawayRules.TruckCollisionSize.z));
                }
                System.Array.Clear(next,0,cells); bool any=false;
                int radius=Mathf.FloorToInt(reach/spacing);
                for(int to=0;to<cells;to++)
                {
                    if(Mathf.Abs(left+to*spacing)>limit)continue;
                    for(int from=Mathf.Max(0,to-radius);from<=Mathf.Min(cells-1,to+radius);from++)
                    {
                        if(!previous[from])continue;
                        var swept=oldTruck[from];swept.Encapsulate(newTruck[to]);swept.Expand(.1f);
                        bool clear=true;for(int i=0;i<occupied;i++)if(swept.Intersects(threats[i])){clear=false;break;}
                        if(clear){next[to]=true;any=true;break;}
                    }
                }
                if(!any)return false;
                var swap=previous;previous=next;next=swap;
            }
            return true;
        }
    }
}
