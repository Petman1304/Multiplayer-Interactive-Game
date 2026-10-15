using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Getaway.Integration
{
    /// <summary>Scene-scoped daylight and pipeline overrides, restored when the integration scene unloads.</summary>
    public sealed class DowntownRendering : MonoBehaviour
    {
        public static DowntownRendering Instance { get; private set; }
        public string PresetName => selected == 0 ? "LOW" : "BALANCED";
        DowntownEnvironmentDefinition definition;
        RenderPipelineAsset previousPipeline;
        Material previousSky;
        Light previousSun, sun;
        Light[] oldLights;
        bool[] oldEnabled;
        bool previousFog, applied;
        FogMode previousFogMode;
        AmbientMode previousAmbientMode;
        Color previousFogColor, previousSkyColor, previousEquatorColor, previousGroundColor;
        float previousFogStart, previousFogEnd;
        Camera view;
        int selected, previousFrameRate;
        public void Initialize(Camera camera, DowntownEnvironmentDefinition art)
        {
            if (applied) return;
            Instance = this; applied = true; definition = art; view = camera;
            previousPipeline = QualitySettings.renderPipeline; previousFrameRate = Application.targetFrameRate; Application.targetFrameRate = 60;
            previousSky = RenderSettings.skybox; previousSun = RenderSettings.sun;
            previousFog = RenderSettings.fog; previousFogMode = RenderSettings.fogMode; previousFogColor = RenderSettings.fogColor;
            previousFogStart = RenderSettings.fogStartDistance; previousFogEnd = RenderSettings.fogEndDistance;
            previousAmbientMode = RenderSettings.ambientMode; previousSkyColor = RenderSettings.ambientSkyColor; previousEquatorColor = RenderSettings.ambientEquatorColor; previousGroundColor = RenderSettings.ambientGroundColor;
            oldLights = FindObjectsByType<Light>(); oldEnabled = new bool[oldLights.Length];
            for (int i = 0; i < oldLights.Length; i++) { oldEnabled[i] = oldLights[i].enabled; if (oldLights[i].gameObject.scene == gameObject.scene && oldLights[i].type == LightType.Directional) oldLights[i].enabled = false; }
            var lightObject = new GameObject("Downtown daylight"); lightObject.transform.SetParent(transform, false);
            lightObject.transform.rotation = Quaternion.Euler(48, -32, 0); sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional; sun.color = new Color(1, .95f, .85f); sun.intensity = 1.15f;
            RenderSettings.sun = sun; RenderSettings.skybox = art.Skybox;
            RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientSkyColor = new Color(.60f, .69f, .78f);
            RenderSettings.ambientEquatorColor = new Color(.43f, .46f, .47f); RenderSettings.ambientGroundColor = new Color(.25f, .25f, .23f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = new Color(.68f, .76f, .80f);
            RenderSettings.fogStartDistance = 160; RenderSettings.fogEndDistance = 300;
            view.farClipPlane = 380; view.allowHDR = false; view.clearFlags = CameraClearFlags.Skybox;
            view.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            selected = Mathf.Clamp(PlayerPrefs.GetInt("Getaway.CityQuality", 0), 0, 1); Apply();
        }
        void Apply()
        {
            QualitySettings.renderPipeline = selected == 0 ? definition.Low : definition.Balanced;
            view.allowMSAA = selected != 0; sun.shadows = selected == 0 ? LightShadows.Hard : LightShadows.Soft;
        }
        public void Cycle()
        { selected = 1 - selected; Apply(); if (Application.isPlaying) PlayerPrefs.SetInt("Getaway.CityQuality", selected); }
        void OnDestroy() { Restore(); }
        public void Restore()
        {
            if (!applied) return;
            if (QualitySettings.renderPipeline == definition.Low || QualitySettings.renderPipeline == definition.Balanced) QualitySettings.renderPipeline = previousPipeline;
            Application.targetFrameRate = previousFrameRate;
            RenderSettings.skybox = previousSky; RenderSettings.sun = previousSun;
            RenderSettings.fog = previousFog; RenderSettings.fogMode = previousFogMode; RenderSettings.fogColor = previousFogColor;
            RenderSettings.fogStartDistance = previousFogStart; RenderSettings.fogEndDistance = previousFogEnd;
            RenderSettings.ambientMode = previousAmbientMode; RenderSettings.ambientSkyColor = previousSkyColor; RenderSettings.ambientEquatorColor = previousEquatorColor; RenderSettings.ambientGroundColor = previousGroundColor;
            for (int i = 0; i < oldLights.Length; i++) if (oldLights[i] != null && oldLights[i].gameObject.scene == gameObject.scene) oldLights[i].enabled = oldEnabled[i];
            if (Instance == this) Instance = null;
            applied = false;
        }
    }
}
