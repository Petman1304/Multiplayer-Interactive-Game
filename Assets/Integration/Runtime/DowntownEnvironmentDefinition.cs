using UnityEngine;
namespace Getaway.Integration
{
    [CreateAssetMenu(menuName = "Getaway/Downtown environment")]
    public sealed class DowntownEnvironmentDefinition : ScriptableObject
    {
        public GameObject[] Modules = new GameObject[6];
        public Material[] RoadMaterials = new Material[5];
        public Material Skybox;
    }
}
