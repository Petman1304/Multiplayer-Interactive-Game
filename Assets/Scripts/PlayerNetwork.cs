using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>Server-owned lobby membership and shared getaway simulation.</summary>
public class PlayerNetwork : NetworkBehaviour
{
    public const float FinishDistance = 1500f;
    public const int ObstacleCount = 32;
    public NetworkVariable<FixedString64Bytes> playerName = new(new FixedString64Bytes("Player"));
    public NetworkVariable<int> playerRole = new(); // 0: unassigned, 1: driver, 2: robber
    public NetworkVariable<bool> isReady = new();
    public NetworkVariable<int> phase = new(); // 0: lobby, 1: running, 2: crashed, 3: escaped
    public NetworkVariable<Vector3> carPosition = new(new Vector3(0, 0, 0));
    public NetworkVariable<float> robberOffset = new();
    public NetworkVariable<float> aimYaw = new();
    public NetworkVariable<int> health = new(3);
    public NetworkVariable<ulong> clearedObstacles = new();
    public NetworkVariable<int> shotSequence = new();
    public NetworkVariable<Vector3> shotEnd = new();
    private float steering, walking, yaw, lastInput, nextShot;
    private static readonly List<PlayerNetwork> players = new();
    public static IReadOnlyList<PlayerNetwork> Players => players;
    public static PlayerNetwork Local { get; private set; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { players.Clear(); Local = null; }
    public static PlayerNetwork Session
    {
        get
        {
            foreach (var player in Players)
                if (player.IsSpawned && player.OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId) return player;
            return null;
        }
    }

    public static Vector3 ObstaclePosition(int index) => new(((index * 7 + 1) % 3 - 1) * 3.5f, 1, 55 + index * 43);
    public static Vector3 RoofPosition(Vector3 car, float offset) => car + new Vector3(offset, 3.1f, 0);

    public override void OnNetworkSpawn()
    {
        players.Add(this);
        if (!IsOwner) return;
        Local = this;
        SetNameServerRpc(NetworkStartUI.PlayerName);
        var ui = FindAnyObjectByType<UIManager>();
        if (ui != null) ui.SetLocalPlayer(this);
    }
    public override void OnNetworkDespawn() { players.Remove(this); if (Local == this) Local = null; }
    [ServerRpc]
    private void SetNameServerRpc(string value)
    {
        value = string.IsNullOrWhiteSpace(value) ? "Player" : value.Trim();
        playerName.Value = new FixedString64Bytes(value.Substring(0, Mathf.Min(value.Length, 16)));
    }
    public void SetRole(int role) { if (IsOwner) SetRoleServerRpc(role); }
    [ServerRpc]
    private void SetRoleServerRpc(int role)
    {
        if (Session == null || Session.phase.Value != 0 || role < 1 || role > 2) return;
        foreach (var other in Players)
            if (other != this && other.IsSpawned && other.playerRole.Value == role) return;
        playerRole.Value = role;
        isReady.Value = false;
    }
    public void ToggleReady() { if (IsOwner) ToggleReadyServerRpc(); }
    [ServerRpc]
    private void ToggleReadyServerRpc()
    {
        if (Session != null && Session.phase.Value == 0 && playerRole.Value != 0) isReady.Value = !isReady.Value;
    }
    public void StartRound() { if (IsOwner && IsServer) StartRoundServerRpc(); }
    [ServerRpc]
    private void StartRoundServerRpc()
    {
        if (OwnerClientId != Unity.Netcode.NetworkManager.ServerClientId || phase.Value != 0) return;
        var players = Players;
        int roles = 0, count = 0;
        foreach (var p in players)
        {
            if (!p.IsSpawned) continue;
            if (!p.isReady.Value || p.playerRole.Value < 1 || p.playerRole.Value > 2) return;
            roles |= 1 << p.playerRole.Value;
            count++;
        }
        if (count != 2 || roles != 6) return;
        carPosition.Value = Vector3.zero;
        robberOffset.Value = 0;
        health.Value = 3;
        clearedObstacles.Value = 0;
        phase.Value = 1;
    }
    public void ReturnToLobby()
    {
        if (!IsServer || Session != this) return;
        phase.Value = 0;
        foreach (var p in Players) p.isReady.Value = false;
    }
    public void SubmitInput(float steer, float walk, float aim, bool fire)
    {
        if (IsOwner) InputServerRpc(steer, walk, aim, fire);
    }
    [ServerRpc]
    private void InputServerRpc(float steer, float walk, float aim, bool fire)
    {
        var session = Session;
        if (session == null || session.phase.Value != 1) return;
        if (!float.IsFinite(steer) || !float.IsFinite(walk) || !float.IsFinite(aim)) return;
        steering = Mathf.Clamp(steer, -1, 1);
        walking = Mathf.Clamp(walk, -1, 1);
        yaw = Mathf.Clamp(aim, -70, 70);
        lastInput = Time.unscaledTime;
        if (playerRole.Value == 2 && fire && Time.unscaledTime >= nextShot)
        {
            nextShot = Time.unscaledTime + .3f;
            Vector3 origin = RoofPosition(session.carPosition.Value, session.robberOffset.Value);
            origin.y = 1;
            Vector3 direction = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            float nearest = 180;
            int target = -1;
            for (int i = 0; i < ObstacleCount; i++)
            {
                if ((session.clearedObstacles.Value & (1UL << i)) != 0) continue;
                Vector3 delta = ObstaclePosition(i) - origin;
                float along = Vector3.Dot(delta, direction);
                if (along > 0 && along < nearest && (delta - along * direction).sqrMagnitude < 3.5f)
                { nearest = along; target = i; }
            }
            if (target >= 0) session.clearedObstacles.Value |= 1UL << target;
            session.shotEnd.Value = origin + direction * nearest;
            session.shotSequence.Value++;
        }
    }
    private void FixedUpdate()
    {
        if (!IsServer || Session != this || phase.Value != 1) return;
        PlayerNetwork driver = null, robber = null;
        foreach (var p in Players)
        {
            if (!p.IsSpawned) continue;
            if (p.playerRole.Value == 1) driver = p;
            if (p.playerRole.Value == 2) robber = p;
        }
        if (driver == null || robber == null) { ReturnToLobby(); return; }
        Vector3 pos = carPosition.Value;
        float oldZ = pos.z;
        pos.x = Mathf.Clamp(pos.x + (Time.unscaledTime - driver.lastInput < .5f ? driver.steering : 0) * 7 * Time.fixedDeltaTime, -5.2f, 5.2f);
        pos.z += 26 * Time.fixedDeltaTime;
        carPosition.Value = pos;
        robberOffset.Value = Mathf.Clamp(robberOffset.Value + (Time.unscaledTime - robber.lastInput < .5f ? robber.walking : 0) * 2 * Time.fixedDeltaTime, -.9f, .9f);
        aimYaw.Value = robber.yaw;
        for (int i = 0; i < ObstacleCount; i++)
        {
            if ((clearedObstacles.Value & (1UL << i)) != 0) continue;
            Vector3 obstacle = ObstaclePosition(i);
            if (oldZ < obstacle.z && pos.z >= obstacle.z && Mathf.Abs(pos.x - obstacle.x) < 2.3f)
            { clearedObstacles.Value |= 1UL << i; health.Value--; }
        }
        if (health.Value <= 0) phase.Value = 2;
        else if (pos.z >= FinishDistance) phase.Value = 3;
    }
}
