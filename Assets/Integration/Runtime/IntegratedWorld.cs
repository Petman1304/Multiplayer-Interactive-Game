using System.Collections.Generic;
using UnityEngine;

namespace Getaway.Integration
{
    /// <summary>Presentation only: legacy artwork is copied without its local input, physics or cameras.</summary>
    public class IntegratedWorld : MonoBehaviour
    {
        public GameObject TruckArtwork, ShooterArtwork, TrafficArtwork, BuildingArtwork, SectionArtwork;
        public RouteDefinition Route;
        public DowntownEnvironmentDefinition CityArtwork;
        DowntownEnvironment downtown;
        public bool UseDowntownRoute;
        public static IntegratedWorld Instance { get; private set; }
        public Camera View { get; private set; }
        public Vector3 AimPoint { get; private set; }
        [Tooltip("First-person seat position, in integration world units relative to the truck.")]
        public Vector3 DriverEye = new Vector3(1.02f, 2.48f, 1.89f);
        public Vector3 ShooterEye = new Vector3(0, GetawayRules.ShooterEyeHeight, .1f);
        Renderer[] shooterRenderers;
        TruckViewPart[] truckViewParts;
        bool previousDriverView;
        float fallingTime;
        public Vector3 MuzzlePosition => roof == null ? Vector3.zero : roof.position + truck.rotation * (Vector3.up * GetawayRules.WeaponHeight + Vector3.forward * .7f);
        Transform truck, roof, gun, steeringWheel;
        Quaternion steeringRest;
        float steeringAngle;
        readonly Dictionary<Material, Material> insideTruckMaterials = new();
        readonly Dictionary<Material, Material> truckGlassMaterials = new();
        readonly Transform[] threatViews = new Transform[GetawayRules.ThreatSlots];
        readonly int[] threatIds = new int[GetawayRules.ThreatSlots];
        readonly ThreatKind[] threatKinds = new ThreatKind[GetawayRules.ThreatSlots];
        readonly Transform[] chunks = new Transform[5];
        readonly Dictionary<Color, Material> materials = new();
        readonly Transform[,] threatPool = new Transform[GetawayRules.ThreatSlots, 4];
        readonly Dictionary<GameObject, GameObject> artworkCache = new();
        readonly Dictionary<GameObject, Bounds> artworkBounds = new();
        readonly LineRenderer[] tracers = new LineRenderer[8], tracerCores = new LineRenderer[8];
        readonly LineRenderer[,] impactSparks = new LineRenderer[8,3];
        readonly bool[] tracerHits = new bool[8];
        readonly float[] tracerBorn = new float[8], tracerArrival = new float[8];
        ShooterWeaponView weaponView;
        readonly float[] tracerExpires = new float[8];
        readonly double[] tracerAnchors = new double[8];
        readonly Vector3[] tracerStarts = new Vector3[8], tracerEnds = new Vector3[8];
        int tracerIndex, lastRound = -1, lastHull = GetawayRules.MaxHull;
        float hitUntil, damageUntil;
        double renderedDistance;
        float renderedTruckX;
        readonly double[] renderedThreatDistances = new double[GetawayRules.ThreatSlots];
        readonly float[] renderedThreatLanes = new float[GetawayRules.ThreatSlots];
        IntegratedInput input;
        AudioSource audioSource;
        AudioClip shotSound, impactSound, sirenSound;
        float heardPoliceEventUntil = -1;
        readonly Transform[,] trafficIndicators = new Transform[GetawayRules.ThreatSlots, 2];
        public float HitFeedback => Mathf.Clamp01((hitUntil - Time.unscaledTime) * 6);
        public float DamageFeedback => Mathf.Clamp01((damageUntil - Time.unscaledTime) * 2);
        public void Initialize(IntegratedInput controls)
        {
            Instance = this; input = controls;
            View = Camera.main;
            if (View == null) { View = new GameObject("Integration Camera").AddComponent<Camera>(); View.gameObject.AddComponent<AudioListener>(); }
            View.nearClipPlane = .03f; View.farClipPlane = 420;
            View.clearFlags = CameraClearFlags.Skybox;
            View.transform.SetPositionAndRotation(new Vector3(0, 10.4f, -8), Quaternion.Euler(14, 0, 0));
            truck = new GameObject("Shared truck (presentation)").transform;
            Artwork(TruckArtwork, truck, Vector3.zero);
            roof = new GameObject("Roof shooter").transform; roof.SetParent(truck, false); roof.localPosition = GetawayRules.RearShooterSpawn;
            CharacterArtwork(roof);
            // Use the actual weapon hierarchy; no replacement cube weapon or platform.
            foreach (Transform item in roof.GetComponentsInChildren<Transform>(true)) if (item.name == "Gun") { gun = item; break; }
            if (gun == null) { gun = new GameObject("Replicated aim pivot").transform; gun.SetParent(roof, false); }
            foreach (Transform mount in truck.GetComponentsInChildren<Transform>(true))
                if (mount.name == "DriverEyeMount") { DriverEye = truck.InverseTransformPoint(mount.position); break; }
            CreateSteeringWheel();
            truckViewParts = truck.GetComponentsInChildren<TruckViewPart>(true);
            shooterRenderers = roof.GetComponentsInChildren<Renderer>(true);
            var weaponObject=new GameObject("Shooter weapon presentation");weaponObject.transform.SetParent(View.transform,false);
            weaponView=weaponObject.AddComponent<ShooterWeaponView>();weaponView.Initialize();weaponView.CreateWorld(roof);
            if (UseDowntownRoute && Route != null && CityArtwork != null)
            { downtown = new GameObject("Downtown district").AddComponent<DowntownEnvironment>(); downtown.Initialize(Route, CityArtwork); var lighting = gameObject.AddComponent<DowntownRendering>(); lighting.Initialize(View, CityArtwork);
            }
            else for (int i = 0; i < chunks.Length; i++) BuildChunk(i);
            for (int i = 0; i < tracers.Length; i++)
            {
                tracers[i]=TraceLine("Amber shot streak "+i,new Color(1,.56f,.12f),.035f);
                tracerCores[i]=TraceLine("Shot core "+i,new Color(1,.97f,.79f),.011f);
                for(int arm=0;arm<3;arm++) impactSparks[i,arm]=TraceLine("Impact spark "+i+"/"+arm,new Color(1,.78f,.32f),.018f);
            }
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.spatialBlend = 0; audioSource.volume = .22f;
            shotSound = Tone("Shot", 130, .075f); impactSound = Tone("Impact", 65, .14f); sirenSound = Tone("Siren", 650, 1.2f);
        }
        LineRenderer TraceLine(string name,Color color,float width)
        {
            var line=new GameObject(name).AddComponent<LineRenderer>();line.sharedMaterial=Material(color,true);
            line.positionCount=2;line.startWidth=width;line.endWidth=width*.25f;line.numCapVertices=2;
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;return line;
        }
        AudioClip Tone(string name, float frequency, float duration)
        {
            int count = (int)(22050 * duration); var data = new float[count];
            for (int i = 0; i < count; i++) data[i] = Mathf.Sin(i * (name == "Siren" ? frequency + 180 * Mathf.Sin(i * Mathf.PI * 4 / 22050) : frequency) * Mathf.PI * 2 / 22050) * Mathf.Pow(1 - i / (float)count, 3);
            var clip = AudioClip.Create(name, count, 1, 22050, false); clip.SetData(data, 0); return clip;
        }
        Material Material(Color color, bool unlit = false)
        {
            if (materials.TryGetValue(color, out var cached)) return cached;
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var result = new Material(shader) { color = color }; materials.Add(color, result); return result;
        }
        Transform Box(string name, Transform parent, Vector3 position, Vector3 scale, Color color)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube); item.name = name;
            item.transform.SetParent(parent, false); item.transform.localPosition = position; item.transform.localScale = scale;
            Destroy(item.GetComponent<Collider>()); item.GetComponent<Renderer>().sharedMaterial = Material(color);
            return item.transform;
        }
        // Copy only render geometry. Instantiating the old player prefabs would run serial readers and local physics.
        bool Artwork(GameObject source, Transform parent, Vector3 targetSize)
        {
            if (source == null) return false;
            if (!artworkCache.TryGetValue(source, out var template))
            {
                template = new GameObject(source.name + " render template"); template.SetActive(false);
                var map = new Dictionary<Transform, Transform>();
                CopyTransforms(source.transform, template.transform, map);
                bool found = false; Bounds total = default;
                foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
                {
                    bool bodyPart = source == TruckArtwork && InModel(renderer.transform, "Truck ver 1");
                    bool cockpitPart = source == TruckArtwork && InModel(renderer.transform, "kemudi");
                    if ((!renderer.enabled && !bodyPart && !cockpitPart) || !renderer.gameObject.activeSelf) continue;
                    Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null) continue;
                    Transform target = map[renderer.transform];
                    Renderer copy;
                    if (renderer is SkinnedMeshRenderer skin)
                    {
                        var newSkin = target.gameObject.AddComponent<SkinnedMeshRenderer>();
                        newSkin.sharedMesh = mesh;
                        var bones = skin.bones;
                        for (int i = 0; i < bones.Length; i++) bones[i] = bones[i] != null && map.TryGetValue(bones[i], out var bone) ? bone : null;
                        newSkin.bones = bones;
                        if (skin.rootBone != null && map.TryGetValue(skin.rootBone, out var rootBone)) newSkin.rootBone = rootBone;
                        newSkin.localBounds = skin.localBounds; newSkin.updateWhenOffscreen = true;
                        copy = newSkin;
                    }
                    else
                    {
                        target.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                        copy = target.gameObject.AddComponent<MeshRenderer>();
                    }
                    copy.sharedMaterials = renderer.sharedMaterials;
                    if (bodyPart || cockpitPart)
                    {
                        // Plano is the windshield; Plano.001 contains the side/rear glazing.
                        // Keep both separate from opaque cab panels.
                        bool windscreen = renderer.name == "Plano" || renderer.name == "Plano.001";
                        var materialCache = windscreen ? truckGlassMaterials : insideTruckMaterials;
                        var twoSided = copy.sharedMaterials;
                        for (int materialIndex = 0; materialIndex < twoSided.Length; materialIndex++)
                        {
                            var original = twoSided[materialIndex]; if (original == null) continue;
                            if (!materialCache.TryGetValue(original, out var interiorSafe))
                            {
                                interiorSafe = CreateTruckMaterial(original, windscreen);
                                materialCache.Add(original, interiorSafe);
                            }
                            twoSided[materialIndex] = interiorSafe;
                        }
                        copy.sharedMaterials = twoSided;
                        if (windscreen) copy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                    if (bodyPart || cockpitPart)
                    {
                        var viewPart = target.gameObject.AddComponent<TruckViewPart>();
                        viewPart.Cockpit = cockpitPart;
                        if (cockpitPart)
                            foreach (var interior in source.GetComponentsInChildren<Renderer>(true))
                                if (InModel(interior.transform, "Truck ver 1") && renderer.name == interior.name &&
                                    (renderer.bounds.center - interior.bounds.center).sqrMagnitude < .001f &&
                                    (renderer.bounds.size - interior.bounds.size).sqrMagnitude < .001f)
                                { viewPart.InteriorDuplicate = true; break; }
                    }
                    Matrix4x4 matrix = source.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                    Bounds bounds = mesh.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        Vector3 point = matrix.MultiplyPoint3x4(corner);
                        if (!found) { total = new Bounds(point, Vector3.zero); found = true; } else total.Encapsulate(point);
                    }
                }
                if (!found) { Destroy(template); return false; }
                foreach (var animator in source.GetComponentsInChildren<Animator>(true))
                {
                    var copy = map[animator.transform].gameObject.AddComponent<Animator>();
                    copy.avatar = animator.avatar; copy.runtimeAnimatorController = animator.runtimeAnimatorController;
                    copy.applyRootMotion = false; copy.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
                artworkCache.Add(source, template); artworkBounds.Add(source, total);
            }
            var root = Instantiate(template, parent, false).transform;
            Bounds box = artworkBounds[source];
            float scale = targetSize == Vector3.zero ? .4f : Mathf.Min(targetSize.x / Mathf.Max(.001f, box.size.x), targetSize.y / Mathf.Max(.001f, box.size.y), targetSize.z / Mathf.Max(.001f, box.size.z));
            root.localScale = Vector3.one * scale;
            root.localPosition = targetSize == Vector3.zero ? Vector3.zero : new Vector3(-box.center.x, -box.min.y, -box.center.z) * scale;
            if (targetSize == Vector3.zero) root.localRotation = source.transform.localRotation;
            root.gameObject.SetActive(true);
            return true;
        }
        void CreateSteeringWheel()
        {
            Transform rim = null;
            var movingParts = new List<Transform>();
            foreach (var item in truck.GetComponentsInChildren<Transform>(true))
            {
                if (!InModel(item, "kemudi")) continue;
                if (item.name == "pPipe1") rim = item;
                if (item.name == "pPipe1" || item.name == "pCube1" || item.name == "pCylinder23") movingParts.Add(item);
            }
            if (rim == null || !rim.TryGetComponent<Renderer>(out var renderer)) return;
            steeringWheel = new GameObject("Steering wheel hub").transform;
            steeringWheel.SetParent(rim.parent, false);
            steeringWheel.position = renderer.bounds.center; steeringWheel.rotation = rim.rotation;
            steeringRest = steeringWheel.localRotation;
            foreach (var item in movingParts) item.SetParent(steeringWheel, true);
        }
        static Material CreateTruckMaterial(Material original, bool windscreen)
        {
            var result = new Material(original) { name = original.name + (windscreen ? " (clear truck glass)" : " (truck interior visible)"), doubleSidedGI = true };
            if (result.HasProperty("_Cull")) result.SetFloat("_Cull", 0);
            if (result.HasProperty("_CullMode")) result.SetFloat("_CullMode", 0);
            if (!windscreen) return result;
            result.SetFloat("_Surface", 1);
            result.SetFloat("_Blend", 0);
            result.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            result.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            result.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            result.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            result.SetFloat("_ZWrite", 0);
            result.SetFloat("_AlphaClip", 0);
            result.SetFloat("_BlendModePreserveSpecular", 0);
            result.SetFloat("_Smoothness", .15f);
            result.SetFloat("_Metallic", 0);
            result.SetColor("_BaseColor", new Color(.8f, .9f, .95f, .035f));
            result.SetOverrideTag("RenderType", "Transparent");
            result.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            result.DisableKeyword("_ALPHATEST_ON");
            result.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            result.DisableKeyword("_ALPHAMODULATE_ON");
            result.SetShaderPassEnabled("DepthOnly", false);
            result.SetShaderPassEnabled("ShadowCaster", false);
            result.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return result;
        }
        static bool InModel(Transform item, string model)
        {
            while (item != null) { if (item.name == model) return true; item = item.parent; }
            return false;
        }
        void CharacterArtwork(Transform parent)
        {
            if (!Artwork(ShooterArtwork, parent, Vector3.zero)) return;
            Transform character = parent.GetChild(parent.childCount - 1);
            Bounds bounds = artworkBounds[ShooterArtwork];
            float scale = GetawayRules.ShooterHeight / Mathf.Max(.01f, bounds.size.y);
            character.localScale = Vector3.one * scale;
            character.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z) * scale;
        }
        static void CopyTransforms(Transform source, Transform target, Dictionary<Transform, Transform> map)
        {
            map.Add(source, target);
            foreach (Transform child in source)
            {
                var copy = new GameObject(child.name).transform; copy.SetParent(target, false);
                copy.localPosition = child.localPosition; copy.localRotation = child.localRotation; copy.localScale = child.localScale;
                copy.gameObject.SetActive(child.gameObject.activeSelf);
                CopyTransforms(child, copy, map);
            }
        }
        void BuildChunk(int index)
        {
            var chunk = new GameObject("Petrus road section " + index).transform; chunks[index] = chunk;
            if (!Artwork(SectionArtwork, chunk, Vector3.zero)) Debug.LogError("Assign PetrusRoadLayout to SectionArtwork.");
        }
        void BuildThreat(int slot, RoadThreat data)
        {
            if (threatViews[slot] != null) threatViews[slot].gameObject.SetActive(false);
            threatIds[slot] = data.Id; threatKinds[slot] = data.Kind;
            if (threatPool[slot, (int)data.Kind] != null) { threatViews[slot] = threatPool[slot, (int)data.Kind]; return; }
            var root = new GameObject(data.Kind + " pooled slot " + slot).transform; threatViews[slot] = root;
            threatPool[slot, (int)data.Kind] = root;
            Vector3 size = GetawayRules.ThreatSize(data.Kind);
            if (data.Kind == ThreatKind.Repair)
            {
                Box("Repair pack", root, Vector3.up * .8f, size, new Color(.04f, .7f, .47f));
                Box("Cross vertical", root, new Vector3(0, .85f, -.77f), new Vector3(.2f, .8f, .04f), Color.white);
                Box("Cross horizontal", root, new Vector3(0, .85f, -.8f), new Vector3(.8f, .2f, .04f), Color.white);
            }
            else if (data.Kind == ThreatKind.Barricade)
            {
                Box("Road barrier", root, Vector3.up * 1.1f, size, new Color(.96f, .39f, .12f));
                for (int x = -1; x <= 1; x++) Box("Reflector", root, new Vector3(x * .8f, 1.3f, -.82f), new Vector3(.35f, .7f, .04f), new Color(1, .88f, .5f));
            }
            else
            {
                if (!Artwork(TrafficArtwork, root, size))
                    Box("Vehicle", root, new Vector3(0, .9f, 0), new Vector3(2.3f, 1.8f, 4.5f), new Color(.55f, .21f, .18f));
                if (data.Kind == ThreatKind.Traffic)
                    for (int side = 0; side < 2; side++)
                    {
                        var indicator = new GameObject(side == 0 ? "Left indicators" : "Right indicators").transform;
                        indicator.SetParent(root, false); trafficIndicators[slot, side] = indicator;
                        for (int end = -1; end <= 1; end += 2)
                            Box("Amber signal", indicator, new Vector3(side == 0 ? -1.7f : 1.7f, 1, end * 2.05f), new Vector3(.35f,.3f,.12f), new Color(1,.55f,.02f));
                        indicator.gameObject.SetActive(false);
                    }
                if (data.Kind == ThreatKind.Police)
                {
                    Box("Red beacon", root, new Vector3(-.5f, 2.2f, 0), new Vector3(.65f, .25f, .5f), new Color(1, .08f, .12f));
                    Box("Blue beacon", root, new Vector3(.5f, 2.2f, 0), new Vector3(.65f, .25f, .5f), new Color(.1f, .45f, 1));
                }
            }
        }
        void LateUpdate()
        {
            if (truck == null) return;
            var session = IntegratedCrew.Session;
            RoundState state = session != null ? session.State.Value : new RoundState { Hull = GetawayRules.MaxHull };
            if (state.Round != lastRound) heardPoliceEventUntil = -1;
            if (state.PoliceEvent == PursuitEvent.Approaching && state.PoliceEventUntil > state.Elapsed && state.PoliceEventUntil != heardPoliceEventUntil)
            { heardPoliceEventUntil = state.PoliceEventUntil; audioSource.PlayOneShot(sirenSound, .5f); }
            bool snap = state.Round != lastRound || state.Phase == RoundPhase.Lobby;
            lastRound = state.Round;
            float blend = snap ? 1 : 1 - Mathf.Exp(-18 * Time.deltaTime);
            renderedDistance = snap ? state.Distance : renderedDistance + (state.Distance - renderedDistance) * blend;
            var route = GetawayRules.Route;
            var routePose = route != null ? route.Sample(renderedDistance) : default;
            renderedTruckX = snap ? state.TruckX : Mathf.Lerp(renderedTruckX, state.TruckX, blend);
            truck.position = route != null ? routePose.Right * renderedTruckX : new Vector3(renderedTruckX, 0, 0);
            truck.rotation = route != null ? routePose.Rotation : Quaternion.identity;
            Vector3 shooterPosition = GetawayRules.RearShooterSpawn + Vector3.right * state.RoofX;
            roof.localPosition = Vector3.Lerp(roof.localPosition, shooterPosition, blend);
            if (steeringWheel != null)
            {
                steeringAngle = Mathf.Lerp(steeringAngle, state.Steering * 180, 1 - Mathf.Exp(-12 * Time.deltaTime));
                steeringWheel.localRotation = steeringRest * Quaternion.AngleAxis(steeringAngle, Vector3.up);
            }
            gun.localRotation = Quaternion.Euler(state.Pitch, 0, 0);
            roof.localRotation = Quaternion.Euler(0, state.Yaw, -state.RoofVelocity * 10);
            if (state.Defeat == DefeatReason.FellOff)
            {
                fallingTime += Time.deltaTime;
                roof.localPosition = shooterPosition + new Vector3(Mathf.Sign(state.RoofX) * fallingTime * 2, -4.9f * fallingTime * fallingTime, 0);
            }
            else fallingTime = 0;
            float scroll = (float)(renderedDistance % 200);
            if (downtown != null) downtown.Present(renderedDistance, state.RouteSeed);
            else for (int i = 0; i < chunks.Length; i++)
            {
                float ahead = (i - 1) * 200 - scroll;
                float from = GetawayRules.RoadOffset(renderedDistance, ahead - 100);
                float to = GetawayRules.RoadOffset(renderedDistance, ahead + 100);
                chunks[i].position = new Vector3((from + to) * .5f, 0, ahead);
                chunks[i].rotation = Quaternion.Euler(0, Mathf.Atan2(to - from, 200) * Mathf.Rad2Deg, 0);
                chunks[i].localScale = new Vector3(1, 1, Mathf.Sqrt(40000 + (to - from) * (to - from)) / 200);
            }
            for (int i = 0; i < threatViews.Length; i++)
            {
                bool active = session != null && i < session.Threats.Count && session.Threats[i].Active;
                if (!active) { if (threatViews[i] != null) threatViews[i].gameObject.SetActive(false); continue; }
                var threat = session.Threats[i]; bool fresh = threatViews[i] == null || threatIds[i] != threat.Id || threatKinds[i] != threat.Kind;
                if (fresh) BuildThreat(i, threat);
                threatViews[i].gameObject.SetActive(true);
                if (threat.Kind == ThreatKind.Traffic)
                    for (int side = 0; side < 2; side++) if (trafficIndicators[i, side] != null)
                        trafficIndicators[i, side].gameObject.SetActive((threat.Phase == ThreatPhase.Signalling || threat.Phase == ThreatPhase.Changing)
                            && (threat.TargetLane < threat.StartLane ? side == 0 : side == 1) && (int)(state.Elapsed * 3) % 2 == 0);
                renderedThreatDistances[i] = fresh || snap ? threat.RouteDistance : renderedThreatDistances[i] + (threat.RouteDistance-renderedThreatDistances[i])*blend;
                renderedThreatLanes[i] = fresh || snap ? threat.LaneOffset : Mathf.Lerp(renderedThreatLanes[i],threat.LaneOffset,blend);
                var threatPose = route != null ? route.Sample(renderedThreatDistances[i]) : default;
                Vector3 target = route != null ? threatPose.RelativeTo(routePose, renderedThreatLanes[i]) : new Vector3(threat.X + GetawayRules.RoadOffset(state.Distance, threat.Z), 0, threat.Z);
                threatViews[i].rotation = route != null ? threatPose.Rotation : Quaternion.identity;
                threatViews[i].position = route != null || fresh || snap ? target : Vector3.Lerp(threatViews[i].position, target, blend);
            }
            bool driverView = input != null && input.ViewingDriver;
            bool aim = !driverView && input != null && input.Aiming;
            bool live = session != null && (state.Phase == RoundPhase.Playing || state.Phase == RoundPhase.Countdown || state.Phase == RoundPhase.Wrecked);
            if (live)
            {
                // Camera snaps to the selected player's eyes, never interpolates through the vehicle.
                View.transform.position = driverView ? truck.TransformPoint(DriverEye) : roof.position + truck.rotation * ShooterEye;
                float yaw = driverView ? input.DriverLookYaw : input.AimYaw;
                float pitch = driverView ? input.DriverLookPitch : input.AimPitch;
                View.transform.rotation = truck.rotation * Quaternion.Euler(pitch, yaw, 0);
            }
            else
            {
                View.transform.position = new Vector3(0, 10.4f, -8);
                View.transform.LookAt(truck.position + Vector3.forward * 10);
            }
            View.fieldOfView = driverView != previousDriverView ? 75 : Mathf.Lerp(View.fieldOfView, aim ? 48 : 75, 1 - Mathf.Exp(-10 * Time.deltaTime));
            previousDriverView = driverView;
            // Exterior stays complete for the robber. Only driver view uses the cabin assembly.
            foreach (var part in truckViewParts)
            {
                bool cockpitView = live && driverView;
                part.GetComponent<Renderer>().forceRenderingOff = part.Cockpit && (!cockpitView || part.InteriorDuplicate);
            }
            foreach (var renderer in shooterRenderers)
                renderer.forceRenderingOff = (live && !driverView) || renderer.transform.IsChildOf(gun);
            AimPoint=live && !driverView?ResolveAimPoint():View.transform.position+View.transform.forward*190;
            weaponView.Pose(live && !driverView,state.Phase==RoundPhase.Playing && aim,state.ReloadTime,input!=null?input.AimYaw:0,input!=null?input.AimPitch:4,Time.deltaTime,View.transform.InverseTransformPoint(AimPoint));
            weaponView.WorldWeapon.gameObject.SetActive(!live || driverView);
            weaponView.WorldWeapon.localRotation=Quaternion.Euler(state.Pitch,0,0);
            if (state.Hull < lastHull) { damageUntil = Time.unscaledTime + .5f; audioSource.PlayOneShot(impactSound); }
            lastHull = state.Hull;
            for (int i = 0; i < tracers.Length; i++)
            {
                float age=Time.unscaledTime-tracerBorn[i];bool active=Time.unscaledTime<tracerExpires[i];
                if(!active && (!tracerHits[i] || age>tracerArrival[i]+.11f))
                {tracers[i].enabled=tracerCores[i].enabled=false;for(int arm=0;arm<3;arm++)impactSparks[i,arm].enabled=false;continue;}
                Vector3 shift=route!=null?route.Sample(tracerAnchors[i]).RelativeTo(routePose):Vector3.zero;
                ShotTrace.Sample(tracerStarts[i],tracerEnds[i],age,out var tail,out var head,out float width);
                tracers[i].enabled=tracerCores[i].enabled=active && (head-tail).sqrMagnitude>.0001f;
                if(tracers[i].enabled)
                {
                    tracers[i].SetPosition(0,tail+shift);tracers[i].SetPosition(1,head+shift);tracers[i].startWidth=.035f*width;tracers[i].endWidth=.005f*width;
                    tracerCores[i].SetPosition(0,tail+shift);tracerCores[i].SetPosition(1,head+shift);tracerCores[i].startWidth=.011f*width;tracerCores[i].endWidth=.003f*width;
                }
                float arrival=tracerArrival[i];
                float burst=age-arrival;
                for(int arm=0;arm<3;arm++)
                {
                    var spark=impactSparks[i,arm];spark.enabled=tracerHits[i] && burst>=0 && burst<.11f;
                    if(!spark.enabled)continue;
                    var back=(tracerStarts[i]-tracerEnds[i]).normalized;
                    var right=Vector3.Cross(back,Mathf.Abs(back.y)<.9f?Vector3.up:Vector3.right).normalized;
                    var up=Vector3.Cross(back,right);float angle=arm*Mathf.PI*2/3+tracerBorn[i];
                    var direction=right*Mathf.Cos(angle)+up*Mathf.Sin(angle);
                    var at=tracerEnds[i]+shift+back*.03f;float radius=(.16f+arrival*1.2f)*(1-burst/.11f);
                    spark.SetPosition(0,at+direction*radius*.15f);spark.SetPosition(1,at+direction*radius);spark.widthMultiplier=.018f*(1-burst/.11f);
                }
            }
        }
        public void ShowShot(Vector3 from, Vector3 to, bool hit, double anchorDistance = 0)
        {
            int i = tracerIndex++ % tracers.Length;
            if (tracers[i] == null) return;
            var route=GetawayRules.Route;
            Vector3 shift=route!=null?route.Sample(anchorDistance).RelativeTo(route.Sample(renderedDistance)):Vector3.zero;
            if(weaponView!=null)
            {
                from=(weaponView.Visible?weaponView.Muzzle.position:weaponView.WorldMuzzle.position)-shift;
                weaponView.Fire();
            }
            tracerAnchors[i]=anchorDistance;tracerStarts[i]=from;tracerEnds[i]=to;tracerHits[i]=hit;tracerBorn[i]=Time.unscaledTime;
            tracerArrival[i]=Vector3.Distance(from,to)/ShotTrace.Speed;
            tracerExpires[i]=Time.unscaledTime+ShotTrace.Lifetime(from,to);
            audioSource.PlayOneShot(shotSound);
            if (hit) hitUntil = Time.unscaledTime + .16f;
        }
        Vector3 ResolveAimPoint()
        {
            if(input==null)return View.transform.position+View.transform.forward*190;
            var session=IntegratedCrew.Session;
            Ray aimRay = new Ray(MuzzlePosition, truck.rotation * GetawayRules.Direction(input.AimYaw, input.AimPitch));
            float range = 190;
            if (new Plane(Vector3.up, new Vector3(0, 1.1f, 0)).Raycast(aimRay, out float ground)) range = Mathf.Min(range, ground);
            for (int i = 0; i < (session!=null?session.Threats.Count:0); i++)
            {
                var threat = session.Threats[i];
                if (GetawayRules.Route != null) { threat.RouteDistance = renderedThreatDistances[i]; threat.LaneOffset = renderedThreatLanes[i]; }
                if (threat.Active && threat.Kind != ThreatKind.Repair)
                {
                    float hit; var route = GetawayRules.Route;
                    bool intersects = route != null ? RouteGeometry.Raycast(route, threat, renderedDistance, aimRay, out hit) : GetawayRules.ThreatBounds(GetawayRules.PresentedThreat(threat, session.State.Value.Distance)).IntersectRay(aimRay, out hit);
                    if (intersects && hit > 0) range = Mathf.Min(range, hit);
                }
            }
            return aimRay.GetPoint(range);
        }
        void OnGUI()
        {
            if (input == null || input.ViewingDriver || IntegratedCrew.Local == null ||
                IntegratedCrew.Session == null || IntegratedCrew.Session.State.Value.Phase != RoundPhase.Playing) return;
            Vector3 point = View.WorldToScreenPoint(AimPoint);
            if (point.z <= 0) return;
            var previousColor=GUI.color;var previousMatrix=GUI.matrix;
            var centre=new Vector2(point.x,Screen.height-point.y);
            GUI.color=HitFeedback>0?new Color(1,.65f,.2f):Color.white;
            if(HitFeedback>0)GUIUtility.RotateAroundPivot(45,centre);
            // Symmetric strokes have no font bearing/padding offset from the aim point.
            const float gap=3,length=5,width=1.5f;
            GUI.DrawTexture(new Rect(centre.x-gap-length,centre.y-width*.5f,length,width),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(centre.x+gap,centre.y-width*.5f,length,width),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(centre.x-width*.5f,centre.y-gap-length,width,length),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(centre.x-width*.5f,centre.y+gap,width,length),Texture2D.whiteTexture);
            GUI.matrix=previousMatrix;GUI.color=previousColor;
        }
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if(weaponView!=null)Destroy(weaponView.gameObject);
            foreach (var m in materials.Values) if (m != null) Destroy(m);
            foreach (var material in insideTruckMaterials.Values) if (material != null) Destroy(material);
            foreach (var material in truckGlassMaterials.Values) if (material != null) Destroy(material);
            foreach (var template in artworkCache.Values) if (template != null) Destroy(template);
            if (sirenSound != null) Destroy(sirenSound); if (shotSound != null) Destroy(shotSound); if (impactSound != null) Destroy(impactSound);
        }
    }
}
