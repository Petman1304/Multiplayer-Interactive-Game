using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Getaway.Integration
{
    /// <summary>Each connection owns one crew member. Only the host's member simulates the shared world.</summary>
    public class IntegratedCrew : NetworkBehaviour
    {
        public NetworkVariable<FixedString64Bytes> DisplayName = new(new FixedString64Bytes("Player"));
        public NetworkVariable<CrewRole> Role = new();
        public NetworkVariable<bool> Ready = new();
        public NetworkVariable<bool> SoloDebug = new();
        public NetworkVariable<RoundState> State = new();
        public NetworkList<RoadThreat> Threats;
        public static readonly List<IntegratedCrew> Members = new();
        public static IntegratedCrew Local { get; private set; }
        public static IntegratedCrew Session
        {
            get
            {
                foreach (var member in Members)
                    if (member.IsSpawned && member.OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId) return member;
                return null;
            }
        }
        float policeAttackIn = 3;
        float steer, walk, yaw, pitch = 4, lastInput = -100, nextShot, nextPacket, spawnIn, repairIn, nextSnapshot;
        bool fire;
        int nextThreat;
        System.Random random;
        RoundState simulation;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Members.Clear(); Local = null; }
        void Awake() { Threats = new NetworkList<RoadThreat>(); }
        public override void OnNetworkSpawn()
        {
            Members.Add(this);
            if (IsServer && OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId)
            {
                simulation = new RoundState { Hull = GetawayRules.MaxHull };
                State.Value = simulation;
                for (int i = 0; i < GetawayRules.ThreatSlots; i++) Threats.Add(default);
            }
            if (IsOwner) { Local = this; NameServerRpc(IntegratedGetaway.PlayerName); }
        }
        public override void OnNetworkDespawn() { Members.Remove(this); if (Local == this) Local = null; }
        [ServerRpc] void NameServerRpc(string name)
        {
            // FixedString is byte limited: accept a short printable name, including non-ASCII names.
            var safe = new System.Text.StringBuilder();
            foreach (char c in (name ?? "Player"))
                if (!char.IsControl(c) && c != '<' && c != '>' && !char.IsSurrogate(c) && safe.Length < 16) safe.Append(c);
            string clean = safe.ToString().Trim();
            DisplayName.Value = new FixedString64Bytes(clean.Length == 0 ? "Player" : clean);
        }
        public void Choose(CrewRole role) { if (IsOwner) ChooseServerRpc(role); }
        [ServerRpc] void ChooseServerRpc(CrewRole role)
        {
            if (Session == null || Session.State.Value.Phase != RoundPhase.Lobby || role < CrewRole.None || role > CrewRole.Shooter) return;
            foreach (var member in Members) if (role != CrewRole.None && member != this && member.IsSpawned && member.Role.Value == role) return;
            Role.Value = role; Ready.Value = false;
        }
        public void ToggleReady() { if (IsOwner) ReadyServerRpc(); }
        [ServerRpc] void ReadyServerRpc()
        {
            if (Session != null && Session.State.Value.Phase == RoundPhase.Lobby && Role.Value != CrewRole.None) Ready.Value = !Ready.Value;
        }
        public static bool CanStart()
        {
            if (Session != null && Session.SoloDebug.Value) return Members.Count == 1 && Local != null && Local.Role.Value != CrewRole.None;
            int count = 0, roles = 0;
            foreach (var m in Members)
            {
                if (!m.IsSpawned) continue;
                if (!m.Ready.Value || m.Role.Value == CrewRole.None) return false;
                count++; roles |= 1 << (int)m.Role.Value;
            }
            return count == 2 && roles == 6;
        }
        public void BeginRound()
        {
            if (!IsServer || Session != this || State.Value.Phase != RoundPhase.Lobby || !CanStart()) return;
            int round = simulation.Round + 1;
            simulation = new RoundState { Phase = RoundPhase.Countdown, Hull = GetawayRules.MaxHull, Countdown = 3, Round = round, RouteSeed = round * 7919, Pitch = 4, Balance = 100, Ammo = GetawayRules.Magazine, Reserve = 72 };
            random = new System.Random(round * 7919);
            policeAttackIn = 3;
            spawnIn = 1.5f; repairIn = 18; nextThreat = 0;
            foreach (var m in Members) { m.fire = false; m.steer = m.walk = m.yaw = 0; m.pitch = 4; m.lastInput = -100; m.nextShot = 0; }
            for (int i = 0; i < Threats.Count; i++) Threats[i] = default;
            State.Value = simulation;
        }
        public void BackToLobby()
        {
            if (!IsServer || Session != this) return;
            simulation.Phase = RoundPhase.Lobby; simulation.Speed = 0;
            State.Value = simulation;
            foreach (var m in Members) { m.Ready.Value = false; m.fire = false; }
            for (int i = 0; i < Threats.Count; i++) Threats[i] = default;
        }
        public void SendInput(float steering, float walking, float aimYaw, float aimPitch, bool shooting)
        {
            if (IsOwner) InputServerRpc(steering, walking, aimYaw, aimPitch, shooting);
        }
        [ServerRpc(Delivery = RpcDelivery.Unreliable)]
        void InputServerRpc(float steering, float walking, float aimYaw, float aimPitch, bool shooting)
        {
            if (Session == null || Session.State.Value.Phase != RoundPhase.Playing || Time.unscaledTime < nextPacket) return;
            if (!GetawayRules.Finite(steering) || !GetawayRules.Finite(walking) || !GetawayRules.Finite(aimYaw) || !GetawayRules.Finite(aimPitch)) return;
            nextPacket = Time.unscaledTime + .015f;
            steer = Mathf.Clamp(steering, -1, 1); walk = Mathf.Clamp(walking, -1, 1);
            yaw = Mathf.Clamp(aimYaw, -75, 75); pitch = Mathf.Clamp(aimPitch, -25, 60);
            fire = shooting; lastInput = Time.unscaledTime;
        }
        public void Reload() { if (IsOwner) ReloadServerRpc(); }
        [ServerRpc] void ReloadServerRpc()
        {
            var session = Session;
            if (session == null || session.simulation.Phase != RoundPhase.Playing || (Role.Value != CrewRole.Shooter && !session.SoloDebug.Value)) return;
            session.StartReload();
        }
        void StartReload()
        {
            if (simulation.ReloadTime <= 0 && simulation.Ammo < GetawayRules.Magazine && simulation.Reserve > 0) simulation.ReloadTime = 1.8f;
        }
        void Defeat(DefeatReason reason)
        { simulation.Defeat = reason; simulation.Phase = RoundPhase.Wrecked; simulation.Speed = 0; }
        void Damage()
        {
            if (simulation.Invulnerability > 0) return;
            simulation.Hull--; simulation.Invulnerability = 1.5f; simulation.Speed *= .8f;
            if (simulation.Hull <= 0) Defeat(DefeatReason.TruckDestroyed);
        }
        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || Session != this) return;
            float dt = Time.fixedDeltaTime;
            if (simulation.Phase == RoundPhase.Countdown)
            {
                simulation.Countdown = Mathf.Max(0, simulation.Countdown - dt);
                if (simulation.Countdown <= 0) simulation.Phase = RoundPhase.Playing;
            }
            else if (simulation.Phase == RoundPhase.Playing) Simulate(dt);
            if (Time.unscaledTime >= nextSnapshot)
            { nextSnapshot = Time.unscaledTime + .05f; State.Value = simulation; }
        }
        void Simulate(float dt)
        {
            IntegratedCrew driver = null, shooter = null;
            foreach (var m in Members)
            {
                if (!m.IsSpawned) continue;
                if (m.Role.Value == CrewRole.Driver) driver = m;
                if (m.Role.Value == CrewRole.Shooter) shooter = m;
            }
            if (SoloDebug.Value) driver = shooter = this;
            if (driver == null || shooter == null) { BackToLobby(); return; }
            bool driverFresh = Time.unscaledTime - driver.lastInput < .35f;
            bool shooterFresh = Time.unscaledTime - shooter.lastInput < .35f;
            simulation.Elapsed += dt;
            simulation.Speed = Mathf.MoveTowards(simulation.Speed, Mathf.Min(42, 22 + simulation.Elapsed * .065f), dt * 5);
            double previousDistance = simulation.Distance;
            var route = GetawayRules.Route;
            float previousTruckX = simulation.TruckX;
            simulation.Steering = driverFresh ? driver.steer : 0;
            float previousLateral = simulation.LateralSpeed;
            simulation.LateralSpeed = Mathf.MoveTowards(simulation.LateralSpeed, simulation.Steering * 8, dt * 22);
            float roadShift = route != null ? RouteGeometry.CurveDrift(route, simulation.Distance, simulation.Speed) * dt : GetawayRules.RoadOffset(simulation.Distance, simulation.Speed * dt);
            simulation.TruckX += simulation.LateralSpeed * dt - roadShift;
            if (Mathf.Abs(simulation.TruckX) > GetawayRules.MaxTruckCenterX)
            {
                simulation.TruckX = Mathf.Clamp(simulation.TruckX, -GetawayRules.MaxTruckCenterX, GetawayRules.MaxTruckCenterX);
                Damage(); simulation.Pursuit += dt * 12;
                if (simulation.Phase == RoundPhase.Wrecked) return;
            }
            float jolt = simulation.LateralSpeed - previousLateral;
            float curvePush = route != null ? -Mathf.Clamp(route.Sample(simulation.Distance).Curvature * simulation.Speed * simulation.Speed, -14, 14) * .075f : 0;
            simulation.RoofVelocity -= jolt * .095f;
            simulation.RoofVelocity = Mathf.MoveTowards(simulation.RoofVelocity, curvePush, dt * 1.4f);
            simulation.RoofX += ((shooterFresh ? shooter.walk : 0) * 2.2f + simulation.RoofVelocity) * dt;
            float strain = Mathf.Abs(simulation.RoofX) > GetawayRules.RoofHalfWidth * .75f ? 20 : 0;
            simulation.Balance = Mathf.Clamp(simulation.Balance + dt * (strain > 0 ? -strain : 12) - Mathf.Abs(jolt) * .8f, 0, 100);
            if (Mathf.Abs(simulation.RoofX) > GetawayRules.RoofHalfWidth + .2f || simulation.Balance <= 0)
            { Defeat(DefeatReason.FellOff); return; }
            if (simulation.ReloadTime > 0)
            {
                simulation.ReloadTime = Mathf.Max(0, simulation.ReloadTime - dt);
                if (simulation.ReloadTime == 0)
                { int rounds = Mathf.Min(GetawayRules.Magazine - simulation.Ammo, simulation.Reserve); simulation.Ammo += rounds; simulation.Reserve -= rounds; }
            }
            simulation.Pursuit = Mathf.Max(0, simulation.Pursuit - dt * .6f);
            simulation.Yaw = shooter.yaw; simulation.Pitch = shooter.pitch;
            simulation.Distance += simulation.Speed * dt;

            simulation.Invulnerability = Mathf.Max(0, simulation.Invulnerability - dt);
            spawnIn -= dt; repairIn -= dt;
            if (spawnIn <= 0) { Spawn(false); spawnIn = Mathf.Max(.95f, 2.4f - simulation.Elapsed * .006f); }
            if (repairIn <= 0) { Spawn(true); repairIn = 20; }
            for (int i = 0; i < Threats.Count; i++)
            {
                var t = Threats[i]; if (!t.Active) continue;
                var previousThreat = t;
                float before = t.Z;
                if (route != null) { t.RouteDistance += t.Speed * dt; t.Z = (float)(t.RouteDistance - simulation.Distance); }
                else t.Z += (t.Speed - simulation.Speed) * dt;
                // Police gradually track the truck, leaving a visible, dodgeable approach.
                if (t.Kind == ThreatKind.Police && t.Z > 12 && t.Z < 80)
                    { if (route != null) t.LaneOffset = Mathf.MoveTowards(t.LaneOffset, simulation.TruckX, dt * .65f); else t.X = Mathf.MoveTowards(t.X, simulation.TruckX, dt * .65f); }
                if (route != null ? RouteGeometry.CrossesTruck(route, previousThreat, t, previousDistance, simulation.Distance, previousTruckX, simulation.TruckX) : GetawayRules.CrossesTruck(GetawayRules.PresentedThreat(t, simulation.Distance), before, simulation.TruckX, previousTruckX))
                {
                    t.Active = false;
                    if (t.Kind == ThreatKind.Repair)
                    { simulation.Hull = Mathf.Min(GetawayRules.MaxHull, simulation.Hull + 1); simulation.Reserve = Mathf.Min(120, simulation.Reserve + 24); simulation.Score += 25; }
                    else { Damage(); simulation.Pursuit += 12; }
                }
                if (t.Z < -25) { t.Active = false; if (t.Kind != ThreatKind.Repair) simulation.Score += 5; }
                Threats[i] = t;
                if (simulation.Phase == RoundPhase.Wrecked) return;
            }
            bool policeClose = false;
            foreach (var t in Threats) if (t.Active && t.Kind == ThreatKind.Police && t.Z > -10 && t.Z < 65) policeClose = true;
            if (policeClose)
            {
                simulation.Pursuit += dt * 5;
                policeAttackIn -= dt;
                if (policeAttackIn <= 0) { Damage(); policeAttackIn = 3; }
            }
            else policeAttackIn = 3;
            if (simulation.Phase == RoundPhase.Playing && simulation.Pursuit >= 100) Defeat(DefeatReason.Captured);
            if (simulation.Phase == RoundPhase.Playing && shooterFresh && shooter.fire && Time.unscaledTime >= shooter.nextShot)
            { shooter.nextShot = Time.unscaledTime + GetawayRules.FireInterval; Shoot(); }
        }
        void Spawn(bool repair)
        {
            int slot = -1;
            for (int i = 0; i < Threats.Count; i++) if (!Threats[i].Active) { slot = i; break; }
            if (slot < 0) return;
            var kind = repair ? ThreatKind.Repair : (ThreatKind)random.Next(simulation.Elapsed > 20 ? 3 : 2);
            float x = (random.Next(3) - 1) * 5.2f;
            float z = repair ? 135 : 155 + (float)random.NextDouble() * 20;
            // Never spawn overlapping threats; every wave leaves at least two lanes open.
            for (int i = 0; i < Threats.Count; i++)
                if (Threats[i].Active && Mathf.Abs(Threats[i].Z - z) < 22) return;
            Threats[slot] = new RoadThreat { Active = true, Id = ++nextThreat, Kind = kind, X = x, Z = z, LaneOffset = x, RouteDistance = simulation.Distance + z,
                Health = kind == ThreatKind.Police ? 3 : kind == ThreatKind.Traffic ? 2 : 1,
                Speed = kind == ThreatKind.Barricade || repair ? 0 : kind == ThreatKind.Police ? 4 : 10 };
        }
        void Shoot()
        {
            if (simulation.ReloadTime > 0) return;
            if (simulation.Ammo <= 0) { StartReload(); return; }
            var route = GetawayRules.Route;
            Vector3 origin = route != null ? RouteGeometry.Muzzle(route, simulation.Distance, simulation.TruckX, simulation.RoofX) : GetawayRules.Muzzle(simulation.TruckX, simulation.RoofX);
            Vector3 direction = GetawayRules.Direction(simulation.Yaw, simulation.Pitch);
            if (route != null) direction = route.Sample(simulation.Distance).Rotation * direction;
            Ray ray = new Ray(origin, direction);
            float nearest = 190; int hit = -1;
            for (int i = 0; i < Threats.Count; i++)
            {
                var threat = Threats[i];
                if (!threat.Active || threat.Kind == ThreatKind.Repair) continue;
                float distance;
                bool intersects = route != null ? RouteGeometry.Raycast(route, threat, simulation.Distance, ray, out distance) : GetawayRules.ThreatBounds(GetawayRules.PresentedThreat(threat, simulation.Distance)).IntersectRay(ray, out distance);
                if (intersects && distance >= 0 && distance < nearest)
                { nearest = distance; hit = i; }
            }
            simulation.Shots++; simulation.Ammo--;
            if (hit >= 0)
            {
                var t = Threats[hit]; t.Health--; simulation.Hits++;
                if (t.Health <= 0) { t.Active = false; if (t.Kind == ThreatKind.Police) { simulation.PoliceDefeated++; simulation.Pursuit = Mathf.Max(0, simulation.Pursuit - 18); } simulation.Score += t.Kind == ThreatKind.Police ? 75 : 30; }
                Threats[hit] = t;
            }
            ShotClientRpc(origin, origin + direction * nearest, hit >= 0, simulation.Distance);
        }
        [ClientRpc] void ShotClientRpc(Vector3 from, Vector3 to, bool hit, double anchorDistance)
        { if (IntegratedWorld.Instance != null) IntegratedWorld.Instance.ShowShot(from, to, hit, anchorDistance); }
    }
}
