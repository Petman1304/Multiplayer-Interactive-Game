using UnityEngine;
namespace Getaway.Integration
{
    /// <summary>Bounded, decorative render pool. No scene physics or gameplay scripts are copied.</summary>
    public sealed class DowntownEnvironment : MonoBehaviour
    {
        public const int PoolSize = 7;
        sealed class Module
        {
            public GameObject Root;
            public Transform[] Anchors;
            public Vector3[] Positions;
            public Quaternion[] Rotations;
            public bool[] Details;
        }
        sealed class Tile
        {
            public long Index = long.MinValue;
            public Transform Road;
            public DowntownRoadMesh Surface;
            public Module[] Modules = new Module[6];
            public int Active = -1;
        }
        readonly Tile[] tiles = new Tile[PoolSize];
        RouteDefinition route;
        public int ActiveTiles => route != null ? PoolSize : 0;
        public void Initialize(RouteDefinition definition, DowntownEnvironmentDefinition art)
        {
            if (route != null) return;
            route = definition;
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i] = new Tile();
                var road = new GameObject("Curved street " + i, typeof(MeshFilter), typeof(MeshRenderer)); road.transform.SetParent(transform, false);
                tile.Road = road.transform; tile.Surface = new DowntownRoadMesh(); road.GetComponent<MeshFilter>().sharedMesh = tile.Surface.Mesh;
                road.GetComponent<MeshRenderer>().sharedMaterials = art.RoadMaterials;
                for (int variant = 0; variant < 6; variant++)
                {
                    var root = Instantiate(art.Modules[variant], transform, false); root.SetActive(false);
                    var module = new Module { Root = root, Anchors = new Transform[root.transform.childCount], Positions = new Vector3[root.transform.childCount], Rotations = new Quaternion[root.transform.childCount], Details = new bool[root.transform.childCount] };
                    for (int child = 0; child < module.Anchors.Length; child++)
                    { var t = root.transform.GetChild(child); module.Anchors[child] = t; module.Positions[child] = t.localPosition; module.Rotations[child] = t.localRotation; module.Details[child] = t.name.StartsWith("Detail"); }
                    tile.Modules[variant] = module;
                }
            }
        }
        public void Present(double distance, int seed)
        {
            var origin = route.Sample(distance); long first = (long)System.Math.Floor((distance - 80) / 100);
            for (int i = 0; i < tiles.Length; i++)
            {
                long index = first + i; int slot = (int)((index % PoolSize + PoolSize) % PoolSize); var tile = tiles[slot];
                double start = index * 100d;
                int variant = (int)route.SectionAt(start + 50);
                if (variant <= 1) variant = (variant + (EncounterDirector.Lane(seed, 0, (int)(index % int.MaxValue)) == 0 ? 1 : 0)) % 2;
                if (tile.Index != index || tile.Active != variant)
                {
                    if (tile.Active >= 0) tile.Modules[tile.Active].Root.SetActive(false);
                    tile.Active = variant; tile.Index = index; tile.Modules[variant].Root.SetActive(true); tile.Surface.Rebuild(route, start);
                }
                tile.Road.position = route.Sample(start).RelativeTo(origin);
                var module = tile.Modules[variant];
                for (int child = 0; child < module.Anchors.Length; child++)
                {
                    Vector3 local = module.Positions[child]; var anchor = module.Anchors[child];
                    double at = start + local.z; var pose = route.Sample(at);
                    anchor.position = pose.RelativeTo(origin, local.x) + Vector3.up * local.y;
                    anchor.rotation = pose.Rotation * module.Rotations[child];
                    if (module.Details[child]) anchor.gameObject.SetActive(System.Math.Abs(at - distance) < 120);
                }
            }
        }
        void OnDestroy()
        {
            foreach (var tile in tiles) if (tile != null && tile.Surface != null)
            { if (Application.isPlaying) Destroy(tile.Surface.Mesh); else DestroyImmediate(tile.Surface.Mesh); }
        }
    }
}
