using System;
using System.Collections.Generic;
using Getaway.Integration;
using UnityEditor;
using UnityEngine;

public static class DowntownArtAuthoring
{
    public const string Root = "Assets/Integration/Environment";
    static Material MakeMaterial(string name, Color color)
    {
        string path = Root + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
        material.color = color; material.SetFloat("_Smoothness", .08f); EditorUtility.SetDirty(material); return material;
    }
    static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = material; return go;
    }
    static Material[] BuildingMaterials(MeshRenderer renderer, bool twoSided)
    {
        var materials = renderer.sharedMaterials;
        if (!twoSided) return materials;
        for (int i = 0; i < materials.Length; i++)
        {
            var original = materials[i];
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(original, out string guid, out long localId))
                throw new InvalidOperationException("Building material has no asset identity.");
            string path = Root + "/Building_" + guid + "_" + localId + ".mat";
            var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (copy == null)
            {
                copy = new Material(original); copy.name = original.name + " (two-sided building)";
                AssetDatabase.CreateAsset(copy, path);
            }
            // This source model contains inward-facing exterior polygons. Preserve the source
            // asset and make its existing surfaces visible from both sides in the integration.
            copy.SetFloat("_Cull", 0); copy.doubleSidedGI = true; EditorUtility.SetDirty(copy);
            materials[i] = copy;
        }
        return materials;
    }
    public static DowntownEnvironmentDefinition Create()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before authoring.");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Integration/Prefabs/PetrusRoadLayout.prefab");
        var buildingSources = new List<Transform>();
        foreach (var t in source.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("Amaryllis_") || t.name.StartsWith("Lycoris_")) buildingSources.Add(t);
        if (buildingSources.Count == 0) throw new Exception("Petrus building groups not found.");
        var materials = new[] {
            MakeMaterial("CityGround", new Color(.24f,.27f,.23f)), MakeMaterial("CityAsphalt", new Color(.16f,.18f,.19f)),
            MakeMaterial("CitySidewalk", new Color(.49f,.48f,.44f)), MakeMaterial("CityCurb", new Color(.68f,.67f,.61f)),
            MakeMaterial("CityMarkings", new Color(.88f,.87f,.76f)) };
        var orange = MakeMaterial("CityConstruction", new Color(.85f,.39f,.13f)); var dark = MakeMaterial("CityStreetFurniture", new Color(.12f,.16f,.18f));
        var sign = MakeMaterial("CitySign", new Color(.08f,.32f,.29f));
        string definitionPath = Root + "/DowntownEnvironment.asset";
        var definition = AssetDatabase.LoadAssetAtPath<DowntownEnvironmentDefinition>(definitionPath);
        if (definition == null) { definition = ScriptableObject.CreateInstance<DowntownEnvironmentDefinition>(); AssetDatabase.CreateAsset(definition, definitionPath); }
        definition.RoadMaterials = materials;
        for (int variant = 0; variant < 6; variant++)
        {
            var root = new GameObject(((CitySectionKind)variant).ToString());
            try
            {
                for (int side = -1; side <= 1; side += 2) for (int row = 0; row < 3; row++)
                {
                    var src = buildingSources[(row + variant + (side > 0 ? 1 : 0)) % buildingSources.Count];
                    var group = new GameObject("Petrus frontage").transform; group.SetParent(root.transform, false);
                    float setback = variant == 2 || variant == 3 ? 34 : 25;
                    group.localPosition = new Vector3(side * setback, 0, 16 + row * 32); group.localRotation = Quaternion.Euler(0, side > 0 ? -90 : 90, 0);
                    var renderers = src.GetComponentsInChildren<MeshRenderer>(true); Bounds total = renderers[0].bounds;
                    foreach (var renderer in renderers) total.Encapsulate(renderer.bounds);
                    float scale = .4f; // The same authored-unit conversion as the truck and Petrus road.
                    var near = new GameObject("Original Petrus artwork").transform; near.SetParent(group, false);
                    foreach (var renderer in renderers)
                    {
                        var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh; if (mesh == null) continue;
                        var child = new GameObject(renderer.name, typeof(MeshFilter), typeof(MeshRenderer)); child.transform.SetParent(near, false);
                        child.transform.localPosition = (renderer.transform.position - new Vector3(total.center.x, total.min.y, total.center.z)) * scale;
                        child.transform.localRotation = renderer.transform.rotation; child.transform.localScale = renderer.transform.lossyScale * scale;
                        child.GetComponent<MeshFilter>().sharedMesh = mesh; child.GetComponent<MeshRenderer>().sharedMaterials = BuildingMaterials(renderer, src.name.StartsWith("Amaryllis_City_Pok_mon_Center"));
                    }
                    // Keep complete facades throughout the visible range. Screen-relative box LODs
                    // replaced small buildings well before the fog hid them. The tile pool bounds scenery.
                }
                for (int side = -1; side <= 1; side += 2) for (int row = 0; row < 2; row++)
                {
                    float z = 20 + row * 55;
                    var pole = new GameObject("Detail street light").transform; pole.SetParent(root.transform, false); pole.localPosition = new Vector3(side * 12.5f, 0, z);
                    Box("Pole", pole, new Vector3(0,3.7f,0), new Vector3(.16f,7.4f,.16f), dark);
                    Box("Lamp housing", pole, new Vector3(-side*.65f,7.3f,0), new Vector3(1.6f,.2f,.6f), materials[3]);
                }
                if (variant == 4)
                    for (int i = 0; i < 4; i++) Box("Detail sidewalk construction", root.transform, new Vector3(11, .6f, 20+i*5), new Vector3(1.2f,1.2f,2), orange);
                if (variant == 5)
                {
                    var landmark = new GameObject("Municipal square landmark").transform; landmark.SetParent(root.transform, false); landmark.localPosition = new Vector3(-17,0,50);
                    Box("Clock tower", landmark, new Vector3(0,12,0), new Vector3(3.5f,24,3.5f), materials[3]);
                    Box("Clock face", landmark, new Vector3(0,19,-1.8f), new Vector3(2.5f,2.5f,.1f), dark);
                    Box("Hour hand", landmark, new Vector3(.4f,19.2f,-1.87f), new Vector3(.9f,.12f,.08f), materials[4]);
                    Box("Square sign", landmark, new Vector3(0,5,-2), new Vector3(7,1.5f,.15f), sign);
                }
                definition.Modules[variant] = PrefabUtility.SaveAsPrefabAsset(root, Root + "/City" + ((CitySectionKind)variant) + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets(); return definition;
    }
}
