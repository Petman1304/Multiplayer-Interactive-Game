using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
namespace Getaway.Integration
{
    /// <summary>
    /// Bright cartoon "Blind Getaway" dressing added once to every pooled downtown module:
    /// metropolitan skyline with rooftop billboards, the robbed bank, police roadblocks,
    /// food-market stalls (the getaway truck is an old grocery truck) and bend chevrons.
    /// Decorative only: no colliders, no lights, no scripts. Each prop is combined into one
    /// mesh per material and is a top-level module child so the pool bends it along the route.
    /// Children named "Detail ..." are hidden past 120 route metres by DowntownEnvironment.
    /// </summary>
    public sealed class DowntownSetDressing
    {
        public static readonly Color Ground = new Color(.42f, .56f, .36f);
        static readonly Color[] Towers = {
            new Color(.98f, .80f, .45f), new Color(.55f, .80f, .95f), new Color(.95f, .56f, .50f), new Color(.70f, .86f, .55f),
            new Color(.80f, .70f, .95f), new Color(.98f, .68f, .36f), new Color(.56f, .88f, .82f), new Color(.96f, .93f, .86f) };
        static readonly Color Glass = new Color(.27f, .47f, .72f), GlassDark = new Color(.18f, .28f, .42f), Roof = new Color(.55f, .57f, .62f),
            Steel = new Color(.30f, .32f, .36f), White = new Color(.97f, .97f, .95f), Black = new Color(.08f, .08f, .10f),
            PoliceBlue = new Color(.15f, .46f, .82f), SirenRed = new Color(1f, .12f, .15f), SirenBlue = new Color(.12f, .50f, 1f),
            SirenGlowRed = new Color(1f, .32f, .36f), SirenGlowBlue = new Color(.32f, .68f, 1f),
            Warning = new Color(1f, .82f, .10f), Cone = new Color(1f, .46f, .08f), Leaf = new Color(.36f, .72f, .30f), LeafLight = new Color(.54f, .83f, .36f),
            Bark = new Color(.48f, .32f, .20f), Wood = new Color(.68f, .48f, .28f), WoodLight = new Color(.84f, .66f, .42f),
            AppleRed = new Color(.92f, .16f, .14f), CabbageGreen = new Color(.44f, .82f, .28f), CabbageLight = new Color(.58f, .90f, .38f),
            DebrisGray = new Color(.82f, .80f, .76f), DebrisDark = new Color(.68f, .66f, .62f),
            Stone = new Color(.94f, .89f, .77f), Gold = new Color(1f, .78f, .22f), Teal = new Color(.10f, .62f, .62f), Bag = new Color(.80f, .70f, .50f),
            Cart = new Color(.90f, .28f, .24f), Cloud = new Color(1f, 1f, 1f);
        static readonly Color[] Produce = { new Color(.92f, .20f, .15f), new Color(1f, .55f, .10f), new Color(.45f, .80f, .30f), new Color(.50f, .22f, .58f), new Color(1f, .88f, .20f) };
        static readonly Color[] Awnings = { new Color(.92f, .22f, .25f), new Color(.15f, .55f, .95f), new Color(.25f, .72f, .35f), new Color(1f, .62f, .10f) };
        static readonly string[] Ads = { "ALAT ELEKTRONIK\nDISKON 50%", "TRUK TUA?\nMASIH NGEBUT? CEPAT GANTII", "SIMPAN UANGMU DI BANK\n 100% AMAN*", "BUTUH SAYUR SEGAR?\nKE FRESH MART AJA!",
            "KIRI! MOBIL, KANAN MOTOR \nHATI HATI!!", "AMAN BERKENDARA\nAMAN KELUARGA!", "SIAPKAN ASURANSI\nUNTUK JAGA JAGA", "TIKUNGAN TAJAM? \nAWAS HATI HATI" };
        static readonly Color[] AdPanels = { new Color(.95f, .25f, .35f), new Color(.15f, .55f, .95f), new Color(1f, .82f, .10f), new Color(.30f, .75f, .35f), new Color(.60f, .30f, .85f) };
        static Mesh cube, sphere, cylinder;
        readonly Material template;
        readonly TMP_FontAsset font;
        readonly Dictionary<Color32, Material> materials = new Dictionary<Color32, Material>();
        readonly Dictionary<Color32, List<CombineInstance>> pending = new Dictionary<Color32, List<CombineInstance>>();
        readonly List<Mesh> meshes = new List<Mesh>();
        System.Random random;
        int ad;

        public DowntownSetDressing(Material template)
        {
            this.template = template;
            if (cube == null) { cube = Primitive(PrimitiveType.Cube); sphere = Primitive(PrimitiveType.Sphere); cylinder = Primitive(PrimitiveType.Cylinder); }
            try { font = TMP_Settings.defaultFontAsset; } catch (System.Exception) { font = null; }
            if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }
        static Mesh Primitive(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type); var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go); return mesh;
        }

        /// <summary>Call while the module is still active (TextMeshPro must awaken) and before anchors are captured.</summary>
        public void Apply(Transform module, CitySectionKind kind)
        {
            random = new System.Random(1009 + (int)kind * 7919); ad = (int)kind * 3;
            Skyline(module);
            switch (kind)
            {
                case CitySectionKind.BoulevardA: BoulevardA(module); break;
                case CitySectionKind.BoulevardB: BoulevardB(module); break;
                case CitySectionKind.LeftBend: Bend(module, -1); break;
                case CitySectionKind.RightBend: Bend(module, 1); break;
                case CitySectionKind.Construction: Roadblock(module); break;
                case CitySectionKind.Landmark: Bank(module); break;
            }
        }

        public void Dispose()
        {
            foreach (var material in materials.Values) Release(material);
            foreach (var mesh in meshes) Release(mesh);
            materials.Clear(); meshes.Clear();
        }
        static void Release(Object item) { if (item == null) return; if (Application.isPlaying) Object.Destroy(item); else Object.DestroyImmediate(item); }

        // ---------------------------------------------------------------- building blocks
        float R(float min, float max) => min + (float)random.NextDouble() * (max - min);
        Material Mat(Color color)
        {
            Color32 key = color;
            if (!materials.TryGetValue(key, out var material))
            {
                material = new Material(template) { name = "Getaway dressing " + key };
                material.SetColor("_BaseColor", color); material.color = color; material.SetFloat("_Smoothness", .12f);
                if (key.Equals((Color32)SirenRed) || key.Equals((Color32)SirenBlue) || key.Equals((Color32)SirenGlowRed) || key.Equals((Color32)SirenGlowBlue))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * 2.2f);
                }
                materials[key] = material;
            }
            return material;
        }
        void Add(Mesh mesh, Vector3 position, Vector3 size, Color color, Vector3 euler = default)
        {
            Color32 key = color;
            if (!pending.TryGetValue(key, out var list)) pending[key] = list = new List<CombineInstance>();
            list.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, Quaternion.Euler(euler), size) });
        }
        void Box(Vector3 position, Vector3 size, Color color, Vector3 euler = default) => Add(cube, position, size, color, euler);
        Transform Emit(Transform module, string name, Vector3 position, float yaw, bool shadows)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(module, false); go.transform.localPosition = position; go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var parts = new CombineInstance[pending.Count]; var shared = new Material[pending.Count]; int i = 0;
            foreach (var pair in pending)
            {
                var merged = new Mesh { indexFormat = IndexFormat.UInt32 }; merged.CombineMeshes(pair.Value.ToArray(), true, true);
                parts[i] = new CombineInstance { mesh = merged, transform = Matrix4x4.identity }; shared[i++] = Mat(pair.Key);
            }
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 }; mesh.CombineMeshes(parts, false, false); mesh.UploadMeshData(true);
            foreach (var part in parts) Object.DestroyImmediate(part.mesh);
            pending.Clear(); meshes.Add(mesh);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterials = shared;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go.transform;
        }
        /// <summary>Viewer reads the text while looking along the label's +Z (yaw). Hidden past 120 m like other details.</summary>
        void Label(Transform module, string text, Vector3 position, float yaw, Vector2 size, Color color)
        {
            if (font == null) return;
            var holder = new GameObject("Detail label").transform; holder.SetParent(module, false);
            holder.localPosition = position; holder.localRotation = Quaternion.Euler(0, yaw, 0);
            var go = new GameObject("Text", typeof(RectTransform)); go.transform.SetParent(holder, false);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = font; tmp.fontStyle = FontStyles.Bold; tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap; tmp.enableAutoSizing = true; tmp.fontSizeMin = .1f; tmp.fontSizeMax = 90;
            tmp.rectTransform.sizeDelta = size; tmp.color = color; tmp.text = text;
            var renderer = go.GetComponent<MeshRenderer>(); renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            if (go.activeInHierarchy) tmp.ForceMeshUpdate();
        }
        static Vector3 Place(Vector3 origin, float yaw, Vector3 local) => origin + Quaternion.Euler(0, yaw, 0) * local;

        // ---------------------------------------------------------------- skyline (always visible)
        void Skyline(Transform m)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                // The road mesh ground ends at 55 m; extend it below the skyline (top stays under the street ground).
                for (int i = 0; i < 4; i++) { Box(Vector3.zero, new Vector3(64, .2f, 25.6f), Ground); Emit(m, "Skyline ground", new Vector3(side * 86, -.26f, 12.5f + i * 25), 0, false); }
                for (int i = 0; i < 4; i++)
                {
                    bool back = i >= 2;
                    float w = R(16, 24), d = R(16, 24), h = back ? R(70, 125) : R(34, 62);
                    float x = side * (back ? R(84, 104) : R(58, 66)), z = (i % 2) * 50 + R(14, 36);
                    Tower(m, side, x, z, w, d, h, i == 0);
                }
            }
            for (int c = 0; c < 2; c++)
            {
                float s = R(.8f, 1.4f);
                Add(sphere, Vector3.zero, new Vector3(22, 8, 14) * s, Cloud);
                Add(sphere, new Vector3(9, -1.5f, 2) * s, new Vector3(14, 6, 11) * s, Cloud);
                Add(sphere, new Vector3(-9, -1.8f, -1) * s, new Vector3(15, 6, 10) * s, Cloud);
                Emit(m, "Sky cloud", new Vector3((c == 0 ? -1 : 1) * R(30, 140), R(70, 100), R(0, 100)), R(0, 360), false);
            }
        }
        void Tower(Transform m, int side, float x, float z, float w, float d, float h, bool billboard)
        {
            var body = Towers[random.Next(Towers.Length)]; var glass = random.Next(2) == 0 ? Glass : GlassDark;
            Box(new Vector3(0, h / 2, 0), new Vector3(w, h, d), body);
            int bands = Mathf.Clamp((int)(h / 9), 3, 6); float step = (h - 6) / bands;
            for (int b = 0; b < bands; b++) Box(new Vector3(0, 4 + b * step + step * .3f, 0), new Vector3(w + .3f, step * .45f, d + .3f), glass);
            Box(new Vector3(0, h + 1.2f, 0), new Vector3(w * .62f, 2.4f, d * .62f), Roof);
            if (billboard)
            {
                // Rooftop advert angled toward the road and the approaching truck.
                float yaw = side * 45; var panel = new Vector3(0, h + 7.5f, 0); var color = AdPanels[ad % AdPanels.Length];
                foreach (float post in new[] { -4f, 4f }) Add(cylinder, Place(new Vector3(0, h + 2.4f, 0), yaw, new Vector3(post, 0, 0)) + Vector3.up * 1.2f, new Vector3(.45f, 1.4f, .45f), Steel);
                Box(panel, new Vector3(13.8f, 7, .4f), White, new Vector3(0, yaw, 0));
                Box(Place(panel, yaw, new Vector3(0, 0, -.08f)), new Vector3(13, 6.2f, .4f), color, new Vector3(0, yaw, 0));
                Label(m, Ads[ad % Ads.Length], Place(new Vector3(x, 0, z) + panel, yaw, new Vector3(0, 0, -.32f)), yaw, new Vector2(12, 5.4f), color == AdPanels[2] ? Black : White);
                ad++;
            }
            else if (random.Next(2) == 0)
            {
                Add(cylinder, new Vector3(w * .15f, h + 8.4f, 0), new Vector3(.35f, 6, .35f), Steel);
                Add(sphere, new Vector3(w * .15f, h + 14.6f, 0), Vector3.one * 1.1f, SirenRed);
            }
            else Add(cylinder, new Vector3(-w * .18f, h + 4f, d * .1f), new Vector3(3.4f, 1.6f, 3.4f), Wood); // water tank
            Emit(m, "Skyline tower", new Vector3(x, 0, z), 0, false);
        }

        // ---------------------------------------------------------------- street props (detail)
        void Tree(Transform m, float x, float z)
        {
            float s = R(.85f, 1.2f);
            Add(cylinder, new Vector3(0, .2f, 0), new Vector3(1.4f, .03f, 1.4f), Bark);
            Add(cylinder, new Vector3(0, .18f + 1.5f * s, 0), new Vector3(.38f, 1.5f * s, .38f), Bark);
            Add(sphere, new Vector3(0, 4.3f * s, 0), new Vector3(3.4f, 3f, 3.4f) * s, random.Next(2) == 0 ? Leaf : LeafLight);
            Add(sphere, new Vector3(.6f, 5.6f, .3f) * s, new Vector3(2.2f, 2f, 2.2f) * s, LeafLight);
            Emit(m, "Detail tree", new Vector3(x, 0, z), 0, true);
        }
        void Stall(Transform m, int side, float z)
        {
            const float y = .18f; var awning = Awnings[random.Next(Awnings.Length)];
            Box(new Vector3(0, y + .45f, 0), new Vector3(1.6f, .9f, 3.4f), Wood);
            for (int k = 0; k < 3; k++)
            {
                float cz = -1.1f + k * 1.1f; var produce = Produce[random.Next(Produce.Length)];
                Box(new Vector3(0, y + 1.05f, cz), new Vector3(1.2f, .3f, .95f), WoodLight);
                for (int f = 0; f < 3; f++) Add(sphere, new Vector3(-.3f + f * .3f, y + 1.32f, cz + (f == 1 ? .15f : -.15f)), Vector3.one * .36f, produce);
            }
            foreach (float px in new[] { -.85f, .85f }) foreach (float pz in new[] { -1.65f, 1.65f })
                Add(cylinder, new Vector3(px, y + 1.3f, pz), new Vector3(.07f, 1.3f, .07f), Steel);
            for (int k = 0; k < 4; k++) Box(new Vector3(0, y + 2.7f, -1.275f + k * .85f), new Vector3(2.4f, .1f, .85f), k % 2 == 0 ? awning : White, new Vector3(0, 0, side * 12));
            Emit(m, "Detail market stall", new Vector3(side * 11.4f, 0, z), 0, true);
        }
        void Crates(Transform m, float x, float z)
        {
            const float y = .18f;
            // Upright slatted wooden crate on the curb/shoulder
            SlattedCrate(new Vector3(-.35f, y, -.2f), 14f, 0f, false);
            // Tilted/toppled slatted wooden crate spilling onto the asphalt
            SlattedCrate(new Vector3(.65f, y - .06f, .45f), -25f, 22f, true);
            // Scattered red apples rolling out of the crates
            Vector3[] apples = {
                new Vector3(-.15f, y + .05f, .35f), new Vector3(.25f, .09f, .65f), new Vector3(.95f, .09f, .25f),
                new Vector3(1.35f, .09f, -.15f), new Vector3(-.75f, y + .08f, .15f), new Vector3(.55f, .09f, -.45f)
            };
            foreach (var ap in apples)
            {
                Add(sphere, ap, new Vector3(.24f, .22f, .24f), AppleRed);
                Box(ap + Vector3.up * .12f, new Vector3(.03f, .07f, .03f), Bark);
            }
            Emit(m, "Detail grocery crates", new Vector3(x, 0, z), 0, true);
        }
        void SlattedCrate(Vector3 origin, float yaw, float tiltZ, bool broken)
        {
            var rot = new Vector3(0, yaw, tiltZ);
            // Bottom plank & 4 corner posts
            Box(Place(origin, yaw, new Vector3(0, .06f, 0)), new Vector3(.92f, .06f, .66f), Wood, rot);
            foreach (float px in new[] { -.42f, .42f }) foreach (float pz in new[] { -.29f, .29f })
                Box(Place(origin, yaw, new Vector3(px, .28f, pz)), new Vector3(.07f, .48f, .07f), Wood, rot);
            // 3 horizontal slats per side (leaving visible gaps like a real fruit crate)
            for (int s = 0; s < 3; s++)
            {
                float sy = .14f + s * .14f;
                float tilt = broken && s == 2 ? 14f : 0f;
                foreach (float pz in new[] { -.31f, .31f })
                    Box(Place(origin, yaw, new Vector3(0, sy, pz)), new Vector3(.94f, .08f, .04f), s % 2 == 0 ? WoodLight : Wood, rot + new Vector3(0, 0, tilt));
                foreach (float px in new[] { -.44f, .44f })
                    Box(Place(origin, yaw, new Vector3(px, sy, 0)), new Vector3(.04f, .08f, .64f), s % 2 == 0 ? WoodLight : Wood, rot);
            }
            // Loose broken wooden plank on the ground nearby
            if (broken)
                Box(origin + new Vector3(.45f, .04f, -.45f), new Vector3(.85f, .04f, .10f), WoodLight, new Vector3(0, yaw + 38f, 0));
        }
        void SpilledProduce(Transform m, int side, float z)
        {
            // Tumbled crate on the road shoulder + low-poly green cabbages & red apples scattered across the asphalt edge
            SlattedCrate(new Vector3(side * 7.5f, .05f, 0), side * 28f, -side * 18f, true);
            SlattedCrate(new Vector3(side * 8.6f, .18f, -1.4f), -side * 15f, 0f, false);
            // Low-poly green cabbages rolling along the road shoulder (safe visual props, no colliders)
            Vector3[] cabbages = {
                new Vector3(side * 6.3f, .20f, 1.2f),
                new Vector3(side * 5.2f, .20f, 3.4f),
                new Vector3(side * 6.8f, .20f, -2.1f),
                new Vector3(side * 4.4f, .20f, -0.6f)
            };
            for (int i = 0; i < cabbages.Length; i++)
            {
                var cp = cabbages[i];
                var col = i % 2 == 0 ? CabbageGreen : CabbageLight;
                // Faceted low-poly look by combining angled chunky boxes + sphere core
                Add(sphere, cp, new Vector3(.46f, .42f, .46f), col);
                Box(cp, new Vector3(.38f, .34f, .38f), CabbageLight, new Vector3(25f, i * 40f, 18f));
                Box(cp, new Vector3(.36f, .36f, .36f), col, new Vector3(-20f, i * 55f + 30f, -25f));
            }
            // Red apples scattered around the crate
            Vector3[] apples = {
                new Vector3(side * 7.0f, .11f, .6f), new Vector3(side * 6.5f, .11f, -.3f),
                new Vector3(side * 7.8f, .11f, .9f), new Vector3(side * 5.9f, .11f, 2.1f),
                new Vector3(side * 8.2f, .26f, -.7f)
            };
            foreach (var ap in apples) Add(sphere, ap, new Vector3(.23f, .21f, .23f), AppleRed);
            Emit(m, "Detail spilled produce", new Vector3(0, 0, z), 0, true);
        }
        void CrashDebris(Transform m, float x, float z, int side)
        {
            const float y = .18f;
            // Orange jersey barrier on the sidewalk/curb
            Box(new Vector3(side * 1.4f, y + .48f, -1.2f), new Vector3(.75f, .95f, 2.6f), Cone, new Vector3(0, -side * 10f, 0));
            // Flying / scattered low-poly concrete chunks & dust puff like in the reference image
            Vector3[] chunks = {
                new Vector3(0, y + .35f, 0), new Vector3(-side * .6f, y + .75f, .4f),
                new Vector3(-side * 1.1f, y + .45f, -.5f), new Vector3(side * .4f, y + .95f, .7f),
                new Vector3(-side * .3f, y + 1.15f, -.2f), new Vector3(-side * 1.4f, y + .22f, .8f)
            };
            float[] scales = { .65f, .52f, .44f, .38f, .32f, .28f };
            for (int i = 0; i < chunks.Length; i++)
            {
                float s = scales[i];
                Box(chunks[i], new Vector3(s * 1.2f, s * .85f, s), i % 2 == 0 ? DebrisGray : DebrisDark, new Vector3(i * 23f, i * 47f, i * 31f));
            }
            Emit(m, "Detail crash debris", new Vector3(x, 0, z), 0, true);
        }
        void Sign(Transform m, int side, float z, string text, Color board, Color ink)
        {
            const float y = .18f;
            foreach (float pz in new[] { -2.6f, 2.6f }) Add(cylinder, new Vector3(0, y + 1.6f, pz), new Vector3(.14f, 1.6f, .14f), Steel);
            Box(new Vector3(0, y + 3.6f, 0), new Vector3(.2f, 1.5f, 6.2f), board);
            Emit(m, "Detail shop sign", new Vector3(side * 13.4f, 0, z), 0, true);
            Label(m, text, new Vector3(side * 13.4f - side * .13f, y + 3.6f, z), side * 90, new Vector2(5.8f, 1.3f), ink);
        }
        void Barricade(Transform m, float x, float z, float yaw = 0f)
        {
            const float y = .18f;
            // A-frame legs (two angled struts on left and right)
            foreach (float lx in new[] { -.95f, .95f })
            {
                Box(new Vector3(lx, y + .52f, -.22f), new Vector3(.10f, 1.10f, .10f), White, new Vector3(16f, 0, 0));
                Box(new Vector3(lx, y + .52f, .22f), new Vector3(.10f, 1.10f, .10f), White, new Vector3(-16f, 0, 0));
            }
            // Top & middle horizontal boards with diagonal orange-and-white hazard stripes
            foreach (float by in new[] { y + .88f, y + .48f })
            {
                Box(new Vector3(0, by, 0), new Vector3(2.2f, .24f, .10f), White);
                for (int k = -3; k <= 3; k++)
                    if ((k & 1) == 0)
                        Box(new Vector3(k * .28f, by, 0), new Vector3(.20f, .25f, .12f), Cone, new Vector3(0, 0, 22f));
            }
            // Flanking traffic cones next to the barricade
            ConeSingle(new Vector3(-1.35f, y, .25f));
            ConeSingle(new Vector3(1.35f, y, -.20f));
            Emit(m, "Detail police barricade", new Vector3(x, 0, z), yaw, true);
        }
        void ConeSingle(Vector3 pos)
        {
            Add(cylinder, pos + new Vector3(0, .03f, 0), new Vector3(.56f, .03f, .56f), Black);
            Add(cylinder, pos + new Vector3(0, .28f, 0), new Vector3(.38f, .26f, .38f), Cone);
            Add(cylinder, pos + new Vector3(0, .44f, 0), new Vector3(.30f, .07f, .30f), White);
            Add(cylinder, pos + new Vector3(0, .62f, 0), new Vector3(.20f, .14f, .20f), Cone);
        }
        void Cones(Transform m, float x, float z, int count)
        {
            const float y = .18f;
            for (int k = 0; k < count; k++)
            {
                float cz = (k - (count - 1) * .5f) * 3.0f;
                ConeSingle(new Vector3(0, y, cz));
            }
            Emit(m, "Detail traffic cones", new Vector3(x, 0, z), 0, true);
        }
        /// <param name="roadSide">+1 when the road lies along the car's local +X.</param>
        void PoliceCar(Transform m, float x, float z, float yaw, int roadSide)
        {
            const float y = .18f;
            // Classic blue & white sedan body like in the reference image
            Box(new Vector3(0, y + .42f, 0), new Vector3(2.02f, .22f, 4.65f), PoliceBlue);
            Box(new Vector3(0, y + .68f, 0), new Vector3(1.98f, .38f, 4.50f), White);
            // Blue quarter panels (front hood & rear trunk accents)
            Box(new Vector3(0, y + .70f, 1.45f), new Vector3(2.00f, .36f, 1.55f), PoliceBlue);
            Box(new Vector3(0, y + .70f, -1.45f), new Vector3(2.00f, .36f, 1.55f), PoliceBlue);
            // Bumpers
            Box(new Vector3(0, y + .32f, 2.34f), new Vector3(2.06f, .18f, .18f), Steel);
            Box(new Vector3(0, y + .32f, -2.34f), new Vector3(2.06f, .18f, .18f), Steel);
            // Taillights (red & orange) and headlights
            foreach (float lx in new[] { -.68f, .68f })
            {
                Box(new Vector3(lx, y + .68f, -2.26f), new Vector3(.42f, .18f, .06f), SirenRed);
                Box(new Vector3(lx * .72f, y + .68f, -2.27f), new Vector3(.22f, .16f, .06f), Warning);
                Box(new Vector3(lx, y + .68f, 2.26f), new Vector3(.38f, .18f, .06f), White);
            }
            // White upper cabin with dark windows
            Box(new Vector3(0, y + 1.18f, -.15f), new Vector3(1.74f, .64f, 2.35f), White);
            Box(new Vector3(0, y + 1.16f, -.15f), new Vector3(1.78f, .44f, 2.10f), GlassDark);
            Box(new Vector3(0, y + 1.16f, -.15f), new Vector3(1.56f, .44f, 2.40f), GlassDark);
            // Glowing red & blue roof lightbar + bright halo spheres
            Box(new Vector3(0, y + 1.54f, -.15f), new Vector3(1.18f, .10f, .32f), Steel);
            Box(new Vector3(-.34f, y + 1.62f, -.15f), new Vector3(.52f, .18f, .36f), SirenBlue);
            Box(new Vector3(.34f, y + 1.62f, -.15f), new Vector3(.52f, .18f, .36f), SirenRed);
            Add(sphere, new Vector3(-.42f, y + 1.66f, -.15f), new Vector3(.95f, .52f, .95f), SirenGlowBlue);
            Add(sphere, new Vector3(.42f, y + 1.66f, -.15f), new Vector3(.95f, .52f, .95f), SirenGlowRed);
            // Gold police shield on doors
            Box(new Vector3(roadSide * 1.00f, y + .68f, .15f), new Vector3(.04f, .26f, .24f), Gold);
            // Wheels
            foreach (float wx in new[] { -.96f, .96f }) foreach (float wz in new[] { -1.42f, 1.42f })
                Add(cylinder, new Vector3(wx, y + .36f, wz), new Vector3(.74f, .14f, .74f), Black, new Vector3(0, 0, 90));
            Emit(m, "Detail police car", new Vector3(x, 0, z), yaw, true);
        }
        void Chevron(Transform m, float x, float z, int direction)
        {
            const float y = .18f;
            Add(cylinder, new Vector3(0, y + .8f, .1f), new Vector3(.1f, .8f, .1f), Steel);
            Box(new Vector3(0, y + 1.9f, 0), new Vector3(1.9f, 1.2f, .1f), Warning);
            foreach (float ax in new[] { -.4f, .4f })
            {
                Box(new Vector3(ax, y + 1.9f + .17f, -.07f), new Vector3(.62f, .16f, .04f), Black, new Vector3(0, 0, -direction * 35));
                Box(new Vector3(ax, y + 1.9f - .17f, -.07f), new Vector3(.62f, .16f, .04f), Black, new Vector3(0, 0, direction * 35));
            }
            Emit(m, "Detail bend chevron", new Vector3(x, 0, z), 0, true);
        }

        // ---------------------------------------------------------------- module themes
        void BoulevardA(Transform m)
        {
            foreach (int side in new[] { -1, 1 }) foreach (float z in new[] { 8f, 46f, 92f }) Tree(m, side * 12.6f, z);
            // Bus stop (right) and a street food cart (left) facing the road.
            const float y = .18f;
            Box(new Vector3(0, y + 2.6f, 0), new Vector3(2.3f, .14f, 4.4f), Teal);
            Box(new Vector3(.95f, y + 1.3f, 0), new Vector3(.08f, 2.3f, 4.1f), Glass);
            foreach (float pz in new[] { -2f, 2f }) Add(cylinder, new Vector3(-.9f, y + 1.3f, pz), new Vector3(.1f, 1.3f, .1f), Steel);
            Box(new Vector3(.45f, y + .45f, 0), new Vector3(.55f, .1f, 3.2f), Wood);
            Emit(m, "Detail bus stop", new Vector3(12, 0, 30), 0, true);
            Label(m, "HALTE", new Vector3(12 - 1.22f, y + 2.6f, 30), 90, new Vector2(3.6f, .5f), White);
            Box(new Vector3(0, y + .85f, 0), new Vector3(1.2f, .9f, 2.2f), Cart);
            Box(new Vector3(0, y + 1.33f, 0), new Vector3(1.3f, .06f, 2.3f), White);
            foreach (float wz in new[] { -.6f, .6f }) Add(cylinder, new Vector3(.64f, y + .38f, wz), new Vector3(.75f, .06f, .75f), Black, new Vector3(0, 0, 90));
            Add(cylinder, new Vector3(-.3f, y + 2.0f, 0), new Vector3(.06f, .7f, .06f), Steel);
            Add(cylinder, new Vector3(-.3f, y + 2.7f, 0), new Vector3(2.6f, .06f, 2.6f), Warning);
            Add(sphere, new Vector3(-.3f, y + 2.75f, 0), new Vector3(2.4f, .5f, 2.4f), SirenRed);
            Emit(m, "Detail food cart", new Vector3(-11.3f, 0, 32), 0, true);
            Label(m, "BAKSO", new Vector3(-11.3f + .62f, y + 1.0f, 32), -90, new Vector2(2f, .5f), White);
            Crates(m, -8.8f, 22);
            SpilledProduce(m, -1, 44);
            SpilledProduce(m, 1, 74);
            Barricade(m, -4.5f, 18, 18f);
            Barricade(m, -4.5f, 68, -22f);
            PoliceCar(m, -3.5f, 38, 22f, 1);
            CrashDebris(m, -8.8f, 35, -1);
            Add(cylinder, new Vector3(0, .18f + .35f, 0), new Vector3(.32f, .35f, .32f), SirenRed); Add(sphere, new Vector3(0, .18f + .72f, 0), Vector3.one * .3f, SirenRed);
            Emit(m, "Detail hydrant", new Vector3(9.2f, 0, 60), 0, true);
        }
        void BoulevardB(Transform m)
        {
            foreach (int side in new[] { -1, 1 }) foreach (float z in new[] { 8f, 92f }) Tree(m, side * 12.6f, z);
            foreach (float z in new[] { 26f, 30f, 34f }) Stall(m, -1, z);
            foreach (float z in new[] { 56f, 60f, 64f }) Stall(m, 1, z);
            Sign(m, -1, 40, "PASAR SEGAR", Warning, Black);
            Sign(m, 1, 50, "SAYUR & BUAH", Teal, White);
            Crates(m, -8.9f, 45); Crates(m, 8.9f, 70);
            SpilledProduce(m, -1, 18);
            SpilledProduce(m, 1, 82);
            Barricade(m, 9.2f, 32, -16f);
            PoliceCar(m, 8.5f, 24, -20f, -1);
            CrashDebris(m, 8.9f, 28, 1);
        }
        void Bend(Transform m, int direction)
        {
            int outer = -direction;
            for (int i = 0; i < 4; i++) Chevron(m, outer * 9.4f, 12 + i * 25, direction);
            const float y = .18f;
            foreach (float px in new[] { -2.2f, 2.2f }) Add(cylinder, new Vector3(px, y + 1.4f, .1f), new Vector3(.14f, 1.4f, .14f), Steel);
            Box(new Vector3(0, y + 3.4f, 0), new Vector3(5.2f, 1.9f, .14f), Black);
            Box(new Vector3(0, y + 3.4f, -.03f), new Vector3(5f, 1.7f, .14f), Warning);
            Emit(m, "Detail bend warning", new Vector3(outer * 12.2f, 0, 3), 0, true);
            Label(m, direction < 0 ? "<<  TIKUNGAN TAJAM" : "TIKUNGAN TAJAM  >>", new Vector3(outer * 12.2f, y + 3.4f, 3 - .12f), 0, new Vector2(4.7f, 1.4f), Black);
            foreach (float z in new[] { 30f, 70f }) Tree(m, -outer * 12.6f, z);
            SpilledProduce(m, outer, 36);
            Barricade(m, outer * 9.0f, 52, -outer * 20f);
            PoliceCar(m, outer * 8.6f, 64, outer * 24f, -outer);
        }
        void Roadblock(Transform m)
        {
            foreach (float z in new[] { 38f, 54f, 70f })
            {
                Barricade(m, -3.5f, z, 20f);
                Barricade(m, 3.5f, z + 4, -20f);
            }
            Cones(m, 8.6f, 86, 5);
            PoliceCar(m, -4.0f, 46, 24f, 1);
            PoliceCar(m, 4.0f, 64, -22f, -1);
            CrashDebris(m, -8.8f, 42, -1);
            CrashDebris(m, 8.8f, 60, 1);
            SpilledProduce(m, -1, 24);
            SpilledProduce(m, 1, 78);
            const float y = .18f;
            Box(new Vector3(0, y + 1.3f, 0), new Vector3(2.2f, 2.6f, 2.2f), White);
            Box(new Vector3(0, y + 1.7f, 0), new Vector3(2.26f, .8f, 2.26f), GlassDark);
            Box(new Vector3(0, y + 2.7f, 0), new Vector3(2.7f, .22f, 2.7f), PoliceBlue);
            Add(sphere, new Vector3(0, y + 2.95f, 0), Vector3.one * .35f, SirenBlue);
            Emit(m, "Detail police post", new Vector3(-12, 0, 82), 0, true);
            Label(m, "POS POLISI", new Vector3(-12 + 1.4f, y + 2.7f, 82), -90, new Vector2(2.6f, .3f), White);
        }
        void Bank(Transform m)
        {
            // The heist began here: remove the right-hand middle frontage and stand the bank in its place.
            if (m.childCount > 4 && m.GetChild(4).name == "Petrus frontage") m.GetChild(4).gameObject.SetActive(false);
            var origin = new Vector3(26, 0, 48);
            Box(new Vector3(0, .5f, 0), new Vector3(19, 1, 26), Stone);
            Box(new Vector3(-10, .35f, 0), new Vector3(1, .7f, 14), Stone);
            Box(new Vector3(-11, .2f, 0), new Vector3(1, .4f, 14), Stone);
            Box(new Vector3(1, 6.5f, 0), new Vector3(17, 11, 24), Stone);
            Box(new Vector3(-.25f, 12.6f, 0), new Vector3(19.5f, 1.2f, 26), White);
            for (int i = 0; i < 6; i++) Add(cylinder, new Vector3(-9, 6.5f, -10 + i * 4), new Vector3(1.1f, 5.5f, 1.1f), White);
            float[] widths = { 22, 16, 10, 4 };
            for (int i = 0; i < widths.Length; i++) Box(new Vector3(-9.4f, 13.6f + i * .8f, 0), new Vector3(1.2f, .8f, widths[i]), White);
            Add(sphere, new Vector3(3, 13.2f, 0), new Vector3(9, 7, 9), Gold);
            Box(new Vector3(-7.55f, 4, 0), new Vector3(.12f, 6, 4.4f), Wood);
            foreach (float wz in new[] { -7f, 7f }) Box(new Vector3(-7.55f, 6.5f, wz), new Vector3(.12f, 4, 2.6f), GlassDark);
            foreach (float az in new[] { -12.5f, 12.5f }) Add(sphere, new Vector3(-9.8f, 13.7f, az), Vector3.one * .9f, SirenRed);
            Emit(m, "Getaway bank", origin, 0, true);
            Label(m, "BANK KOTA", origin + new Vector3(-10.08f, 12.6f, 0), 90, new Vector2(14, 1.1f), PoliceBlue);
            // Spilled loot and the police who arrived too early.
            const float y = .18f;
            Add(sphere, new Vector3(0, y + .45f, 0), new Vector3(.9f, 1f, .9f), Bag); Add(sphere, new Vector3(0, y + 1.0f, 0), Vector3.one * .3f, Bag);
            Add(sphere, new Vector3(.3f, y + .35f, 1.1f), new Vector3(.7f, .75f, .7f), Bag); Add(sphere, new Vector3(-.2f, y + .3f, -1f), new Vector3(.65f, .65f, .65f), Bag);
            Emit(m, "Detail loot bags", new Vector3(12.8f, 0, 48), 0, true);
            Label(m, "$", new Vector3(12.8f - .47f, y + .5f, 48), 90, new Vector2(.6f, .6f), new Color(.15f, .45f, .15f));
            PoliceCar(m, 8.6f, 37, 22, -1); PoliceCar(m, 8.6f, 60, -20, -1);
            Barricade(m, 9.2f, 44, -18f); Barricade(m, 9.2f, 52, 18f);
            CrashDebris(m, 9.0f, 33, 1);
            SpilledProduce(m, -1, 52);
            foreach (float z in new[] { 10f, 90f }) Tree(m, -12.6f, z);
        }
    }
}
