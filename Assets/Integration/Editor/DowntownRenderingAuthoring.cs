using Getaway.Integration;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
public static class DowntownRenderingAuthoring
{
    public static void Create()
    {
        var environment = AssetDatabase.LoadAssetAtPath<DowntownEnvironmentDefinition>(DowntownArtAuthoring.Root + "/DowntownEnvironment.asset");
        string rendererPath = DowntownArtAuthoring.Root + "/CityRenderer.asset";
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (renderer == null)
        {
            renderer = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset"));
            renderer.name = "CityRenderer"; renderer.rendererFeatures.Clear(); AssetDatabase.CreateAsset(renderer, rendererPath);
        }
        var rendererSettings = new SerializedObject(renderer); rendererSettings.FindProperty("m_RenderingMode").intValue = 0; rendererSettings.ApplyModifiedPropertiesWithoutUndo();
        for (int preset = 0; preset < 2; preset++)
        {
            string name = preset == 0 ? "CityLow" : "CityBalanced", path = DowntownArtAuthoring.Root + "/" + name + ".asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (pipeline == null) { pipeline = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset")); pipeline.name = name; AssetDatabase.CreateAsset(pipeline, path); }
            pipeline.supportsCameraDepthTexture = false; pipeline.supportsCameraOpaqueTexture = false; pipeline.supportsHDR = false;
            pipeline.msaaSampleCount = preset == 0 ? 1 : 2; pipeline.renderScale = 1; pipeline.shadowCascadeCount = 2;
            pipeline.shadowDistance = preset == 0 ? 40 : 70; pipeline.mainLightShadowmapResolution = preset == 0 ? 1024 : 2048;
            var serialized = new SerializedObject(pipeline);
            serialized.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            serialized.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
            serialized.FindProperty("m_AdditionalLightsRenderingMode").intValue = 0;
            serialized.FindProperty("m_SoftShadowsSupported").boolValue = preset != 0;
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(pipeline);
            if (preset == 0) environment.Low = pipeline; else environment.Balanced = pipeline;
        }
        string skyPath = DowntownArtAuthoring.Root + "/CityDaylight.mat";
        var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        if (sky == null) { sky = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(sky, skyPath); }
        sky.SetColor("_SkyTint", new Color(.5f,.5f,.5f)); sky.SetColor("_GroundColor", new Color(.45f,.46f,.42f));
        sky.SetFloat("_AtmosphereThickness", 1); sky.SetFloat("_Exposure", 1f); environment.Skybox = sky;
        EditorUtility.SetDirty(sky); EditorUtility.SetDirty(environment); AssetDatabase.SaveAssets();
    }
}
