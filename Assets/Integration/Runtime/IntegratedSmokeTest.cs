#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
using System;
using UnityEngine;
using Unity.Netcode;

namespace Getaway.Integration
{
    /// <summary>Opt-in two-process development-build smoke test. Never enabled in release builds.</summary>
    public class IntegratedSmokeTest : MonoBehaviour
    {
        string mode;
        IntegratedGetaway ui;
        float began, playingSince = -1, lastSend;
        bool started, chosen, readied, swapped;
        int shots, hits;
        double distance;
        float roofTravel;
        bool returned;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            foreach (string arg in Environment.GetCommandLineArgs())
                if (arg.StartsWith("--getaway-smoke="))
                {
                    var runner = new GameObject("Opt-in integration smoke test").AddComponent<IntegratedSmokeTest>();
                    runner.mode = arg.Substring("--getaway-smoke=".Length); return;
                }
        }
        void Start() { began = Time.unscaledTime; Application.runInBackground = true; }
        void Update()
        {
            if (ui == null) ui = FindAnyObjectByType<IntegratedGetaway>();
            if (ui == null) return;
            if (!started && Time.unscaledTime - began > .8f)
            {
                started = true; IntegratedGetaway.PlayerName = "Smoke " + mode;
                ui.GetComponent<IntegratedInput>().enabled = false;
                ui.Connect(mode == "host");
            }
            if (mode == "third")
            {
                if (Time.unscaledTime - began > 8)
                {
                    bool rejected = NetworkManager.Singleton != null && !NetworkManager.Singleton.IsConnectedClient && (ui.Status.Contains("occupied") || ui.Status.Contains("progress"));
                    Complete(rejected, "third-client rejection: " + ui.Status);
                }
                return;
            }
            var local = IntegratedCrew.Local; var session = IntegratedCrew.Session;
            if (local != null && session != null)
            {
                var s = session.State.Value;
                if (s.Phase == RoundPhase.Lobby && !returned)
                {
                    if (!chosen) { chosen = true; local.Choose(mode == "host" ? CrewRole.Driver : CrewRole.Shooter); }
                    if (!readied && local.Role.Value != CrewRole.None) { readied = true; local.ToggleReady(); }
                    if (mode == "host" && IntegratedCrew.CanStart()) session.BeginRound();
                }
                if (s.Phase == RoundPhase.Playing)
                {
                    if (playingSince < 0) playingSince = Time.unscaledTime;
                    shots = Mathf.Max(shots, s.Shots); hits = Mathf.Max(hits, s.Hits); distance = Math.Max(distance, s.Distance);
                    roofTravel = Mathf.Max(roofTravel, Mathf.Abs(s.RoofX));
                    if (Time.unscaledTime - lastSend > .04f)
                    {
                        lastSend = Time.unscaledTime;
                        float yaw = 0, pitch = 4;
                        Vector3 origin = GetawayRules.Muzzle(s.TruckX, s.RoofX);
                        float nearest = float.MaxValue;
                        foreach (var t in session.Threats)
                        {
                            if (!t.Active || t.Kind == ThreatKind.Repair || t.Z < 8 || t.Z >= nearest) continue;
                            nearest = t.Z;
                            Vector3 delta = GetawayRules.ThreatBounds(GetawayRules.PresentedThreat(t, s.Distance)).center - origin;
                            yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                            pitch = -Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude) * Mathf.Rad2Deg;
                        }
                        float walk = Mathf.Clamp((Mathf.Sin(Time.unscaledTime * .8f) * .35f - s.RoofX) * 2 - s.RoofVelocity, -1, 1);
                        float steer = Mathf.Clamp((GetawayRules.RoadOffset(s.Distance, s.Speed * .1f) * 10 - s.TruckX) / 8, -1, 1);
                        local.SendInput(steer, walk, yaw, pitch, true);
                    }
                    if (mode == "host" && Time.unscaledTime - playingSince > 28)
                    {
                        returned = true; session.BackToLobby();
                    }
                }
                if (playingSince > 0 && s.Phase == RoundPhase.Lobby && !swapped)
                {
                    returned = true; swapped = true;
                    bool ok = distance > 400 && shots > 20 && hits > 3 && roofTravel > .2f && !local.Ready.Value && IntegratedCrew.Members.Count == 2;
                    Complete(ok, $"distance={distance:0}, shots={shots}, hits={hits}, roofTravel={roofTravel:0.00}, ready={local.Ready.Value}, crew={IntegratedCrew.Members.Count}");
                }
            }
            if (Time.unscaledTime - began > 55) Complete(false, "Timed out: " + ui.Status);
        }
        System.Collections.IEnumerator Exit(bool passed)
        { yield return new WaitForSecondsRealtime(2); Application.Quit(passed ? 0 : 2); }
        void Complete(bool passed, string details)
        {
            Debug.Log((passed ? "GETAWAY_SMOKE_PASS " : "GETAWAY_SMOKE_FAIL ") + mode + " " + details);
            StartCoroutine(Exit(passed)); enabled = false;
        }
    }
}
#endif
