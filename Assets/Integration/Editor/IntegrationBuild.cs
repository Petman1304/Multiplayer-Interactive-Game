using System;
using Getaway.Integration;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Netcode;

public static class IntegrationBuild
{
    const string Scene = "Assets/Scenes/IntegratedGetaway.unity";
    public static void Verify()
    {
        Require(IntegratedInput.TryParseSample("0.25,-0.1,0.3,1,0", true, out var angles, out bool aim, out bool shoot) && aim && !shoot && Mathf.Abs(angles.x - .25f) < .001f, "Gyro packet parsing");
        Require(IntegratedInput.TryParseSample("1.57", false, out _, out _, out _), "Steering packet parsing");
        foreach (var bad in new[] { "NaN", "Infinity", "nan,0,0,1,0", "0,0,0,2,0", "0,0,0,1", "boot message" })
            Require(!IntegratedInput.TryParseSample(bad, bad.Contains(","), out _, out _, out _), "Reject invalid packet: " + bad);
        var threat = new RoadThreat { X = 0, Z = -12, Kind = ThreatKind.Traffic, Active = true };
        Require(GetawayRules.CrossesTruck(threat, 12, 0), "Swept collision prevents tunneling");
        Require(!GetawayRules.CrossesTruck(threat, 12, 8), "Clear adjacent lane");
        Require(!GetawayRules.CrossesTruck(threat, -11, 0), "Passed traffic does not collide again");
        threat.Z = 40;
        Require(GetawayRules.ThreatBounds(threat).IntersectRay(new Ray(new Vector3(0, 1, 0), Vector3.forward)), "Shot intersects target geometry");
        Require(!GetawayRules.ThreatBounds(threat).IntersectRay(new Ray(new Vector3(6, 1, 0), Vector3.forward)), "Shot misses adjacent lane");
        Require(GetawayRules.Axis(float.NaN, .1f) == 0 && GetawayRules.Axis(.05f, .1f) == 0 && GetawayRules.Axis(2, .1f) == 1, "Safe input deadzone and clamping");
        var opened = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Additive);
        try
        {
            IntegratedGetaway game = null; NetworkManager manager = null;
            foreach (var root in opened.GetRootGameObjects())
            { game = game != null ? game : root.GetComponentInChildren<IntegratedGetaway>(); manager = manager != null ? manager : root.GetComponentInChildren<NetworkManager>(); }
            Require(game != null && manager != null, "Scene has integration and networking");
            Require(manager.NetworkConfig.PlayerPrefab.GetComponent<IntegratedCrew>() != null, "Scene uses integrated crew prefab");
            Require(game.GetComponent<IntegratedWorld>().TruckArtwork != null && game.GetComponent<IntegratedWorld>().ShooterArtwork != null, "Branch artwork references resolve");
            Require(game.GetComponent<NetworkStartUI>() == null, "Legacy lobby is excluded");
        }
        finally { EditorSceneManager.CloseScene(opened, true); }
        Debug.Log("GETAWAY_RULES_PASS: packet validation, collision, aiming, scene and prefab references");
    }
    public static void VerifyAndBuild()
    {
        Verify();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { Scene }, locationPathName = "/tmp/getaway-integration-build/Getaway.x86_64",
            target = BuildTarget.StandaloneLinux64, options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Integration build failed: " + report.summary.result);
        Debug.Log("GETAWAY_BUILD_PASS");
    }
    static void Require(bool condition, string reason) { if (!condition) throw new Exception("Integration verification failed: " + reason); }
}
