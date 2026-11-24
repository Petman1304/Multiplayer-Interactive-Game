using UnityEngine;

namespace Getaway.Integration
{
    /// <summary>Route-oriented geometry shared by authority, presentation and edit-mode checks.</summary>
    public static class RouteGeometry
    {
        public static float TruckLateralLimit(RouteDefinition route, double distance)
        {
            float k = Mathf.Max(Mathf.Abs(route.Sample(distance - 8).Curvature), Mathf.Abs(route.Sample(distance).Curvature), Mathf.Abs(route.Sample(distance + 4).Curvature));
            // Rear overhang sweeps outside a curved centreline; reserve its sagitta plus a small curb margin.
            return GetawayRules.MaxTruckCenterX - k * 32 - .05f;
        }
        public static float CurveDrift(RouteDefinition route, double distance, float speed) => route.Sample(distance).Curvature * speed * route.CurveDriftMetres;
        public static Vector3 Muzzle(RouteDefinition route, double distance, float truckX, float roofX)
        {
            var pose = route.Sample(distance);
            return pose.Right * (truckX + roofX) + pose.Rotation * new Vector3(0, GetawayRules.DeckHeight + GetawayRules.WeaponHeight, GetawayRules.RearShooterSpawn.z + .7f);
        }
        public static bool Raycast(RouteDefinition route, RoadThreat threat, double originDistance, Ray ray, out float distance)
        {
            var pose = route.Sample(threat.RouteDistance); var origin = route.Sample(originDistance);
            var center = pose.RelativeTo(origin, threat.LaneOffset) + Vector3.up * (GetawayRules.ThreatSize(threat.Kind).y * .5f);
            Quaternion inverse = Quaternion.Inverse(pose.Rotation);
            var localRay = new Ray(inverse * (ray.origin - center), inverse * ray.direction);
            return new Bounds(Vector3.zero, GetawayRules.ThreatSize(threat.Kind)).IntersectRay(localRay, out distance);
        }
        public static bool CrossesTruck(RouteDefinition route, RoadThreat before, RoadThreat after, double previousDistance, double distance, float previousX, float x)
        {
            var previousPose = route.Sample(previousDistance); var currentPose = route.Sample(distance);
            float angle = Quaternion.Angle(previousPose.Rotation, currentPose.Rotation);
            angle = Mathf.Max(angle, Quaternion.Angle(route.Sample(before.RouteDistance).Rotation, route.Sample(after.RouteDistance).Rotation));
            double travel = System.Math.Max(System.Math.Abs(distance - previousDistance) + System.Math.Abs(x - previousX),
                System.Math.Abs(after.RouteDistance - before.RouteDistance) + System.Math.Abs(after.LaneOffset - before.LaneOffset));
            int steps = Mathf.Max(1, Mathf.CeilToInt((float)(travel / .5)), Mathf.CeilToInt(angle / 2));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                var truck = route.Sample(previousDistance + (distance - previousDistance) * t);
                var obstacle = route.Sample(before.RouteDistance + (after.RouteDistance - before.RouteDistance) * t);
                var truckCenter = truck.Right * Mathf.Lerp(previousX, x, t) + truck.Rotation * GetawayRules.TruckCollisionCenter;
                var obstacleCenter = obstacle.RelativeTo(truck, Mathf.Lerp(before.LaneOffset, after.LaneOffset, t));
                var delta = obstacleCenter - truckCenter;
                Vector3 size = GetawayRules.ThreatSize(after.Kind);
                if (Overlap(delta, truck.Right, truck, obstacle, size) && Overlap(delta, truck.Forward, truck, obstacle, size) &&
                    Overlap(delta, obstacle.Right, truck, obstacle, size) && Overlap(delta, obstacle.Forward, truck, obstacle, size)) return true;
            }
            return false;
        }
        static bool Overlap(Vector3 delta, Vector3 axis, RoutePoint truck, RoutePoint threat, Vector3 size)
        {
            float truckRadius = Mathf.Abs(Vector3.Dot(axis, truck.Right)) * GetawayRules.TruckCollisionSize.x * .5f + Mathf.Abs(Vector3.Dot(axis, truck.Forward)) * GetawayRules.TruckCollisionSize.z * .5f;
            float threatRadius = Mathf.Abs(Vector3.Dot(axis, threat.Right)) * size.x * .5f + Mathf.Abs(Vector3.Dot(axis, threat.Forward)) * size.z * .5f;
            return Mathf.Abs(Vector3.Dot(delta, axis)) <= truckRadius + threatRadius;
        }
    }
}
