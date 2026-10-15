using System;
using System.Collections.Generic;
using System.Reflection;
using Getaway.Integration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Small, explicitly invoked render; never enters Play or creates a second Editor.</summary>
public static class DowntownPreview
{
    public static string Render(double distance, bool driver, string output, bool overhead = false)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before rendering a preview.");
        var route = AssetDatabase.LoadAssetAtPath<RouteDefinition>(DowntownRouteAuthoring.AssetPath);
        var art = AssetDatabase.LoadAssetAtPath<DowntownEnvironmentDefinition>(DowntownArtAuthoring.Root + "/DowntownEnvironment.asset");
        var scene = EditorSceneManager.NewPreviewScene(); var root = new GameObject("Downtown preview"); SceneManager.MoveGameObjectToScene(root, scene);
        var temporaryMaterials = new List<Material>(); var materialCache = new Dictionary<(Material, bool), Material>();
        RenderTexture target = null; Texture2D pixels = null; var previous = RenderTexture.active; DowntownRendering lighting = null;
        var settingsObject = (UnityEngine.Object)typeof(RenderSettings).GetMethod("GetRenderSettings",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
        var serializedSettings = new SerializedObject(settingsObject); var serializedSun = serializedSettings.FindProperty("m_Sun").objectReferenceValue;
        var previousPipeline = QualitySettings.renderPipeline; var previousSky = RenderSettings.skybox; bool previousFog = RenderSettings.fog;
        try
        {
            var environment = root.AddComponent<DowntownEnvironment>(); environment.Initialize(route, art); environment.Present(distance, 7919);
            var truck = new GameObject("Truck view").transform; truck.SetParent(root.transform, false);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/TruckPrefab.prefab");
            var materialFactory = typeof(IntegratedWorld).GetMethod("CreateTruckMaterial", BindingFlags.Static | BindingFlags.NonPublic);
            var renderers = source.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var renderer in renderers)
            {
                bool body = false, cockpit = false;
                for (var t = renderer.transform; t != null; t = t.parent) { body |= t.name == "Truck ver 1"; cockpit |= t.name == "kemudi"; }
                if (!body && !cockpit || cockpit && !driver) continue;
                if (cockpit)
                {
                    bool duplicate = false;
                    foreach (var b in renderers) if (b.transform.parent.name == "Truck ver 1" && b.name == renderer.name && (b.bounds.center-renderer.bounds.center).sqrMagnitude<.001f && (b.bounds.size-renderer.bounds.size).sqrMagnitude<.001f) { duplicate = true; break; }
                    if (duplicate) continue;
                }
                var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh; if (mesh == null) continue;
                var copy = new GameObject(renderer.name, typeof(MeshFilter), typeof(MeshRenderer)); copy.transform.SetParent(truck, false);
                copy.transform.localPosition = renderer.transform.position * .4f; copy.transform.localRotation = renderer.transform.rotation; copy.transform.localScale = renderer.transform.lossyScale * .4f;
                copy.GetComponent<MeshFilter>().sharedMesh = mesh; var materials = renderer.sharedMaterials;
                bool glazing = renderer.name == "Plano" || renderer.name == "Plano.001";
                for (int i = 0; i < materials.Length; i++)
                {
                    if (!materialCache.TryGetValue((materials[i], glazing), out var material))
                    { material = (Material)materialFactory.Invoke(null, new object[] { materials[i], glazing }); materialCache.Add((materials[i], glazing), material); temporaryMaterials.Add(material); }
                    materials[i] = material;
                }
                copy.GetComponent<MeshRenderer>().sharedMaterials = materials;
                if (glazing) copy.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var pose = route.Sample(distance); truck.rotation = pose.Rotation;
            var cameraObject = new GameObject("Player perspective", typeof(Camera)); cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.GetComponent<Camera>(); camera.scene = scene; camera.nearClipPlane = .03f; camera.fieldOfView = 75;
            lighting = root.AddComponent<DowntownRendering>(); lighting.Initialize(camera, art);
            // Explicit sky component also works when this isolated preview scene is not the active scene.
            cameraObject.AddComponent<Skybox>().material = art.Skybox;
            Vector3 eye = driver ? new Vector3(1.02f,2.48f,1.89f) : GetawayRules.RearShooterSpawn + new Vector3(0,GetawayRules.ShooterEyeHeight,.1f);
            camera.transform.SetPositionAndRotation(pose.Rotation * eye, pose.Rotation * Quaternion.Euler(driver ? 0 : 4,0,0));
            if (overhead) { camera.transform.position = new Vector3(0,80,-55); camera.transform.LookAt(pose.Forward*90); }
            target = new RenderTexture(960,540,16); camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            pixels = new Texture2D(960,540,TextureFormat.RGB24,false); pixels.ReadPixels(new Rect(0,0,960,540),0,0); pixels.Apply();
            System.IO.File.WriteAllBytes(output,pixels.EncodeToPNG()); return output;
        }
        finally
        {
            RenderTexture.active = previous; if (lighting != null) lighting.Restore();
            UnityEngine.Object.DestroyImmediate(root); foreach (var material in temporaryMaterials) UnityEngine.Object.DestroyImmediate(material);
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels); if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            EditorSceneManager.ClosePreviewScene(scene);
            serializedSettings.Update(); serializedSettings.FindProperty("m_Sun").objectReferenceValue = serializedSun; serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            if (QualitySettings.renderPipeline != previousPipeline || RenderSettings.skybox != previousSky || RenderSettings.fog != previousFog) throw new Exception("Preview did not restore rendering state");
        }
    }
}
