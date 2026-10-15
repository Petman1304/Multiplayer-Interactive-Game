using System;
using Unity.Netcode;
using UnityEngine;

namespace Getaway.Integration
{
    public enum RoundPhase : byte { Lobby, Countdown, Playing, Wrecked }
    public enum CrewRole : byte { None, Driver, Shooter }
    public enum ThreatKind : byte { Traffic, Barricade, Police, Repair }

    public enum DefeatReason : byte { None, TruckDestroyed, Captured, FellOff }

    [Serializable]
    public struct RoundState : INetworkSerializable, IEquatable<RoundState>
    {
        public RoundPhase Phase;
        public float Steering;
        public float TruckX, LateralSpeed, Speed, RoofX, Yaw, Pitch, Countdown, Invulnerability, Elapsed;
        public double Distance;
        public int Hull, Score, Shots, Hits, Round, Ammo, Reserve, PoliceDefeated;
        public float Balance, RoofVelocity, Pursuit, ReloadTime;
        public DefeatReason Defeat;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Steering);
            s.SerializeValue(ref Phase); s.SerializeValue(ref TruckX); s.SerializeValue(ref LateralSpeed);
            s.SerializeValue(ref Speed); s.SerializeValue(ref RoofX); s.SerializeValue(ref Yaw);
            s.SerializeValue(ref Pitch); s.SerializeValue(ref Countdown); s.SerializeValue(ref Invulnerability);
            s.SerializeValue(ref Elapsed); s.SerializeValue(ref Distance); s.SerializeValue(ref Hull);
            s.SerializeValue(ref Ammo); s.SerializeValue(ref Reserve); s.SerializeValue(ref PoliceDefeated);
            s.SerializeValue(ref Balance); s.SerializeValue(ref RoofVelocity); s.SerializeValue(ref Pursuit);
            s.SerializeValue(ref ReloadTime); s.SerializeValue(ref Defeat);
            s.SerializeValue(ref Score); s.SerializeValue(ref Shots); s.SerializeValue(ref Hits); s.SerializeValue(ref Round);
        }
        public bool Equals(RoundState o) => Steering == o.Steering && Phase == o.Phase && TruckX == o.TruckX && LateralSpeed == o.LateralSpeed &&
            Speed == o.Speed && RoofX == o.RoofX && Yaw == o.Yaw && Pitch == o.Pitch && Countdown == o.Countdown &&
            Invulnerability == o.Invulnerability && Elapsed == o.Elapsed && Distance == o.Distance && Hull == o.Hull &&
            Ammo == o.Ammo && Reserve == o.Reserve && PoliceDefeated == o.PoliceDefeated && Balance == o.Balance && RoofVelocity == o.RoofVelocity && Pursuit == o.Pursuit && ReloadTime == o.ReloadTime && Defeat == o.Defeat && Score == o.Score && Shots == o.Shots && Hits == o.Hits && Round == o.Round;
    }

    [Serializable]
    public struct RoadThreat : INetworkSerializable, IEquatable<RoadThreat>
    {
        public int Id, Health;
        public ThreatKind Kind;
        public float X, Z, Speed;
        public bool Active;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Id); s.SerializeValue(ref Health); s.SerializeValue(ref Kind);
            s.SerializeValue(ref X); s.SerializeValue(ref Z); s.SerializeValue(ref Speed); s.SerializeValue(ref Active);
        }
        public bool Equals(RoadThreat o) => Id == o.Id && Health == o.Health && Kind == o.Kind &&
            X == o.X && Z == o.Z && Speed == o.Speed && Active == o.Active;
    }

    /// <summary>Shared geometry used by server collision, shooting, input and presentation.</summary>
    public static class GetawayRules
    {
        public const int MaxHull = 5, ThreatSlots = 18;
        public const float RoadHalfWidth = 8f, RoofHalfWidth = 2.05f, FireInterval = .22f;
        public const int Magazine = 12;
        // Complete Truck ver 1 bounds measured through the Editor, after uniform 0.4 conversion.
        public static readonly Vector3 TruckCollisionCenter = new Vector3(0, 1.893f, -2);
        public static readonly Vector3 TruckCollisionSize = new Vector3(5.312f, 3.786f, 12);
        public static float MaxTruckCenterX => RoadHalfWidth - TruckCollisionSize.x * .5f;
        public const float ShooterHeight = 2.4f, ShooterEyeHeight = 2.2f, WeaponHeight = 2.1f;
        // Cargo-deck top measured from Truck ver 1 / Cube.002 bounds, in integration units.
        public const float DeckHeight = 1.732f;
        public static readonly Vector3 RearShooterSpawn = new Vector3(0, DeckHeight, .65f);
        public const float DistrictLength = 500;
        static readonly int[] turns = { -1, 1, 1, -1, 0 };
        public static int District(double distance) => (int)(distance / DistrictLength) % 5 + 1;
        public static int Circuit(double distance) => (int)(distance / (DistrictLength * 5)) + 1;
        public static int Turn(double distance) => turns[(int)(distance / DistrictLength) % 5];
        public static float ToTurn(double distance) => Mathf.Max(0, 360 - (float)(distance % DistrictLength));
        public static double RoadCenter(double distance)
        {
            distance = System.Math.Max(0, distance);
            int section = (int)(distance / DistrictLength);
            double center = 0;
            for (int i = 0; i < section % 5; i++) center += turns[i] * 16;
            float t = Mathf.Clamp01(((float)(distance % DistrictLength) - 360) / 140);
            return center + turns[section % 5] * 16 * t * t * (3 - 2 * t);
        }
        public static float RoadOffset(double distance, float ahead) => (float)(RoadCenter(distance + ahead) - RoadCenter(distance));
        public static RoadThreat PresentedThreat(RoadThreat threat, double distance)
        { threat.X += RoadOffset(distance, threat.Z); return threat; }
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static Vector3 Muzzle(float truckX, float roofX) => new Vector3(truckX + roofX, DeckHeight + WeaponHeight, RearShooterSpawn.z + .7f);
        public static Vector3 Direction(float yaw, float pitch) => Quaternion.Euler(pitch, yaw, 0) * Vector3.forward;
        public static Vector3 ThreatSize(ThreatKind kind) => kind == ThreatKind.Barricade ? new Vector3(2.7f, 2.2f, 1.6f) :
            kind == ThreatKind.Repair ? new Vector3(1.5f, 1.5f, 1.5f) : new Vector3(4, 2, 4);
        public static Bounds ThreatBounds(RoadThreat threat) => new Bounds(new Vector3(threat.X,
            ThreatSize(threat.Kind).y * .5f, threat.Z), ThreatSize(threat.Kind));
        public static bool CrossesTruck(RoadThreat threat, float previousZ, float truckX) => CrossesTruck(threat, previousZ, truckX, truckX);
        public static bool CrossesTruck(RoadThreat threat, float previousZ, float truckX, float previousTruckX)
        {
            Vector3 size = ThreatSize(threat.Kind);
            var expanded = new Bounds(new Vector3(TruckCollisionCenter.x, 0, TruckCollisionCenter.z),
                new Vector3(TruckCollisionSize.x + size.x, 2, TruckCollisionSize.z + size.z));
            Vector3 from = new Vector3(threat.X - previousTruckX, 0, previousZ);
            Vector3 to = new Vector3(threat.X - truckX, 0, threat.Z);
            if (expanded.Contains(from) || expanded.Contains(to)) return true;
            Vector3 movement = to - from;
            return movement.sqrMagnitude > .000001f && expanded.IntersectRay(new Ray(from, movement.normalized), out float distance) && distance <= movement.magnitude;
        }
        public static float Axis(float value, float deadzone)
        {
            if (!Finite(value) || Mathf.Abs(value) <= deadzone) return 0;
            return Mathf.Sign(value) * Mathf.Clamp01((Mathf.Abs(value) - deadzone) / (1 - deadzone));
        }
    }
}
