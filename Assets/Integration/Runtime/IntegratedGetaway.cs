using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Getaway.Integration
{
    /// <summary>LAN session lifecycle and local lobby/settings. Networking decisions stay on the host.</summary>
    [RequireComponent(typeof(IntegratedInput), typeof(IntegratedWorld))]
    public class IntegratedGetaway : MonoBehaviour
    {
        public static string PlayerName = "Player";
        public static bool MenuOpen { get; private set; }
        NetworkManager manager;
        IntegratedInput input;
        IntegratedWorld world;
        string address = "127.0.0.1", status = "Host a game or enter your partner's LAN address.";
        bool connecting, stylesReady;
        float connectDeadline;
        int savedRound = -1;
        readonly HashSet<ulong> reservations = new();
        GUIStyle title, subtitle, body, small, button, field, card, stat;
        Texture2D panelTexture;
        Vector2 panelScroll;
        public string Status => status;
        public bool NativeUI { get; set; }
        bool soloRequested;
        public void SetAddress(string value) { address = value; }
        public void OpenSettings() { MenuOpen = !MenuOpen; }
        public void StartSoloDebug()
        {
            if (manager == null || manager.IsListening || connecting || manager.ShutdownInProgress) return;
            input.ChangeMode(false); soloRequested = true; Connect(true);
        }
        void Start()
        {
            MenuOpen = false;
            manager = NetworkManager.Singleton;
            input = GetComponent<IntegratedInput>(); world = GetComponent<IntegratedWorld>(); world.Initialize(input);
            PlayerName = PlayerPrefs.GetString("Getaway.Name", Environment.UserName);
            input.Port = PlayerPrefs.GetString("Getaway.Port", "");
            input.CustomController = PlayerPrefs.GetInt("Getaway.Custom", 0) == 1;
            input.SteeringFullScale = PlayerPrefs.GetFloat("Getaway.SteeringScale", 1.57f);
            input.InvertSteering = PlayerPrefs.GetInt("Getaway.InvertSteering", 0) == 1;
            Application.runInBackground = true;
            if (manager == null) { status = "NetworkManager is missing from this scene."; return; }
            manager.NetworkConfig.ConnectionApproval = true;
            manager.ConnectionApprovalCallback += Approve;
            manager.OnClientConnectedCallback += Connected;
            manager.OnClientDisconnectCallback += Disconnected;
            manager.OnTransportFailure += TransportFailed;
        }
        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            bool lobby = IntegratedCrew.Session == null || IntegratedCrew.Session.State.Value.Phase == RoundPhase.Lobby;
            bool compatible = System.Text.Encoding.UTF8.GetString(request.Payload ?? Array.Empty<byte>()) == "getaway-integration-v3";
            bool debug = soloRequested || (IntegratedCrew.Session != null && IntegratedCrew.Session.SoloDebug.Value);
            bool available = manager.ConnectedClients.Count + reservations.Count < 2 && lobby && compatible && (!debug || request.ClientNetworkId == NetworkManager.ServerClientId);
            response.Approved = available; response.CreatePlayerObject = available; response.Pending = false;
            response.Reason = available ? "" : !compatible ? "Use the same IntegratedGetaway build as the host." : lobby ? "Both crew slots are occupied." : "A round is in progress. Join when the crew returns to the lobby.";
            if (available) reservations.Add(request.ClientNetworkId);
        }
        void Connected(ulong id)
        {
            reservations.Remove(id);
            if (id == manager.LocalClientId) { connecting = false; status = "Connected. Choose a role and ready up."; }
        }
        void Disconnected(ulong id)
        {
            reservations.Remove(id);
            if (manager.IsServer && IntegratedCrew.Session != null)
            { IntegratedCrew.Session.BackToLobby(); status = "Partner disconnected. Waiting for another crew member."; }
            else
            {
                connecting = false; MenuOpen = false; input.DisconnectDevice();
                status = string.IsNullOrEmpty(manager.DisconnectReason) ? "Disconnected. You can host or join again." : manager.DisconnectReason;
            }
        }
        void TransportFailed() { status = "Network transport failed. Check the host address and UDP port 7777."; Leave(false); }
        public void Connect(bool host)
        {
            if (manager == null || manager.IsListening || connecting || manager.ShutdownInProgress) return;
            var transport = manager.GetComponent<UnityTransport>();
            if (transport == null) { status = "UnityTransport is missing."; return; }
            reservations.Clear(); connecting = true; connectDeadline = Time.unscaledTime + 12;
            status = host ? "Creating lobby…" : "Connecting…";
            manager.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes("getaway-integration-v3");
            transport.SetConnectionData(string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim(), 7777, "0.0.0.0");
            try
            {
                if (!(host ? manager.StartHost() : manager.StartClient())) { connecting = false; status = "Networking could not start. Check whether port 7777 is in use."; }
            }
            catch (Exception error) { status = "Connection failed: " + error.Message; Leave(false); }
        }
        public void Leave(bool message = true)
        {
            if (manager != null) manager.Shutdown();
            soloRequested = false; reservations.Clear(); connecting = false; MenuOpen = false; input.DisconnectDevice();
            if (message) status = "Left the session.";
        }
        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) MenuOpen = !MenuOpen;
            if (connecting && Time.unscaledTime > connectDeadline) { status = "Connection timed out. Check the host IP, same LAN, and UDP 7777 firewall access."; Leave(false); }
            var session = IntegratedCrew.Session;
            if (soloRequested && session != null && session.IsServer && IntegratedCrew.Local != null)
            {
                session.SoloDebug.Value = true;
                if (IntegratedCrew.Local.Role.Value == CrewRole.None) IntegratedCrew.Local.Choose(CrewRole.Shooter);
                else { soloRequested = false; session.BeginRound(); }
            }
            if (session != null && session.State.Value.Phase == RoundPhase.Wrecked && savedRound != session.State.Value.Round)
            {
                savedRound = session.State.Value.Round;
                int score = session.State.Value.Score + (int)session.State.Value.Distance;
                PlayerPrefs.SetInt("Getaway.Best", Mathf.Max(PlayerPrefs.GetInt("Getaway.Best", 0), score));
            }
        }
        void Styles()
        {
            if (stylesReady) return; stylesReady = true;
            panelTexture = new Texture2D(1, 1); panelTexture.SetPixel(0, 0, new Color(.055f, .085f, .115f, .96f)); panelTexture.Apply();
            title = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold };
            title.normal.textColor = new Color(.94f, .96f, .91f);
            subtitle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            subtitle.normal.textColor = new Color(.28f, .86f, .7f);
            body = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, richText = false };
            body.normal.textColor = new Color(.85f, .89f, .9f);
            small = new GUIStyle(body) { fontSize = 12 };
            stat = new GUIStyle(title) { fontSize = 23 };
            button = new GUIStyle(GUI.skin.button) { fontSize = 16, padding = new RectOffset(12, 12, 10, 10), fixedHeight = 42 };
            field = new GUIStyle(GUI.skin.textField) { fontSize = 17, fixedHeight = 34, padding = new RectOffset(9, 9, 6, 6) };
            card = new GUIStyle(GUI.skin.box) { padding = new RectOffset(24, 24, 20, 20) };
            card.normal.background = panelTexture;
        }
        void OnGUI()
        {
            if (NativeUI) return;
            var nativeSession = IntegratedCrew.Session;
            bool live = nativeSession != null && (nativeSession.State.Value.Phase == RoundPhase.Playing || nativeSession.State.Value.Phase == RoundPhase.Countdown);
            if (NativeUI && !MenuOpen && !live) return;
            Styles();
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) * .5f, (Screen.height - 720 * scale) * .5f, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            var session = IntegratedCrew.Session;
            var state = session != null ? session.State.Value : default;
            bool connected = manager != null && manager.IsConnectedClient && session != null;
            if (connected && (state.Phase == RoundPhase.Playing || state.Phase == RoundPhase.Countdown))
            {
                HUD(state);
                if (session.SoloDebug.Value) GUI.Label(new Rect(30, 125, 1100, 36), "SOLO DEBUG · Tab: switch first-person role · A/D or arrows: active role · Mouse: look / aim", body);
                if (MenuOpen)
                {
                    GUILayout.BeginArea(new Rect(355, 95, 570, 535), card);
                    GUILayout.Label("CREW SETTINGS", subtitle); GUILayout.Label("Keep your eyes on the road", stat);
                    GUILayout.Label("The shared game continues while this menu is open. Your controls are released.", small);
                    Controls();
                    if (GUILayout.Button("Resume  ·  Esc", button)) MenuOpen = false;
                    if (GUILayout.Button("Leave session", button)) Leave();
                    GUILayout.EndArea();
                }
            }
            else
            {
                GUILayout.BeginArea(new Rect(40, 38, 565, 646), card);
                GUILayout.Label("CO-OP SURVIVAL  /  INTEGRATION", subtitle);
                GUILayout.Label("BLIND GETAWAY", title);
                GUILayout.Label("One wheelman. One rear shooter. Keep the truck alive.", body);
                GUILayout.Space(14);
                panelScroll = GUILayout.BeginScrollView(panelScroll);
                if (!connected) ConnectionPanel();
                else if (state.Phase == RoundPhase.Wrecked) Results(state);
                else Lobby();
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                GUILayout.BeginArea(new Rect(855, 38, 385, 646), card);
                GUILayout.Label("FIELD MANUAL", subtitle); GUILayout.Label("Stay alive together", stat);
                GUILayout.Label("DRIVER\nA / D or ← / → to steer. The truck accelerates automatically. Leave space to dodge traffic.", body);
                GUILayout.Space(12);
                GUILayout.Label("SHOOTER\nA / D to balance in the cargo bed. Move the mouse to aim through 360 degrees. Hold left click or Space to fire. Hold right click to zoom.", body);
                GUILayout.Space(12);
                GUILayout.Label("Orange barriers: 1 hit\nTraffic: 2 hits\nPolice: 6 hits; rear pursuit raises capture pressure\nGreen packs: collect to repair 1 hull", body);
                GUILayout.Space(12);
                GUILayout.Label("Survive for distance. Earn points by dodging and destroying threats. Traffic builds as speed rises. Five damaging collisions wreck the truck.", body);
                GUILayout.FlexibleSpace(); GUILayout.Label("CUSTOM HARDWARE IS THE PRIMARY CONTROLLER.\nKeyboard / mouse mode is available for testing.\nLAN / direct IP  ·  UDP 7777", small);
                GUILayout.EndArea();
            }
            GUI.matrix = previous;
        }
        void ConnectionPanel()
        {
            GUILayout.Label("CALLSIGN", subtitle); PlayerName = GUILayout.TextField(PlayerName, 16, field);
            GUILayout.Space(10); GUILayout.Label("HOST ADDRESS", subtitle); address = GUILayout.TextField(address, 64, field);
            GUILayout.Label("Same computer: 127.0.0.1  ·  Different computers: host's LAN IPv4", small);
            GUILayout.Space(14);
            GUI.enabled = manager != null && !connecting && !manager.ShutdownInProgress;
            if (GUILayout.Button("Host crew", button)) Connect(true);
            if (GUILayout.Button("Join crew", button)) Connect(false);
            GUI.enabled = true;
            if (connecting && GUILayout.Button("Cancel connection", button)) Leave();
            GUILayout.Space(12); GUILayout.Label(status, body);
            GUILayout.FlexibleSpace(); GUILayout.Label("PERSONAL BEST  " + PlayerPrefs.GetInt("Getaway.Best", 0), subtitle);
            GUILayout.Label("Launch a build and the Editor to test two players on one computer.", small);
        }
        void Lobby()
        {
            foreach (var member in IntegratedCrew.Members)
                if (member.IsSpawned) GUILayout.Label($"{member.DisplayName.Value}   /   {member.Role.Value}   /   {(member.Ready.Value ? "READY" : "NOT READY")}", body);
            if (IntegratedCrew.Members.Count < 2) GUILayout.Label("Waiting for your partner…", small);
            var local = IntegratedCrew.Local;
            if (local != null)
            {
                GUILayout.BeginHorizontal(); RoleButton(local, CrewRole.Driver, "Drive"); RoleButton(local, CrewRole.Shooter, "Shoot"); RoleButton(local, CrewRole.None, "Release"); GUILayout.EndHorizontal();
                GUI.enabled = local.Role.Value != CrewRole.None;
                if (GUILayout.Button(local.Ready.Value ? "Cancel ready" : "Ready up", button)) local.ToggleReady();
                GUI.enabled = true;
            }
            if (manager.IsHost)
            {
                GUI.enabled = IntegratedCrew.CanStart();
                if (GUILayout.Button("Start survival", button)) IntegratedCrew.Session.BeginRound();
                GUI.enabled = true;
            }
            Controls();
            GUILayout.Label(status, small);
            if (GUILayout.Button("Leave crew", button)) Leave();
        }
        void RoleButton(IntegratedCrew local, CrewRole role, string label)
        {
            bool taken = false;
            foreach (var member in IntegratedCrew.Members)
                if (member != local && member.IsSpawned && member.Role.Value == role) taken = true;
            GUI.enabled = !taken;
            if (GUILayout.Button((local.Role.Value == role ? "✓ " : "") + label + (taken ? " (taken)" : ""), button))
            { input.DisconnectDevice(); local.Choose(role); }
            GUI.enabled = true;
        }
        void Controls()
        {
            GUILayout.Space(8); GUILayout.Label("LOCAL OPTIONS", subtitle);
            GUILayout.Label($"Volume {AudioListener.volume:P0}", small);
            AudioListener.volume = GUILayout.HorizontalSlider(AudioListener.volume, 0, 1);
            GUILayout.Label($"Steering sensitivity {input.SteeringSensitivity:0.0}", small);
            input.SteeringSensitivity = GUILayout.HorizontalSlider(input.SteeringSensitivity, .2f, 2);
            string[] quality = QualitySettings.names;
            if (quality.Length > 0 && GUILayout.Button("Graphics: " + quality[QualitySettings.GetQualityLevel()], GUILayout.Height(26)))
                QualitySettings.SetQualityLevel((QualitySettings.GetQualityLevel() + 1) % quality.Length, false);
            GUILayout.Label("LOCAL CONTROLLER", subtitle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(!input.CustomController, "Keyboard / mouse · test", GUI.skin.button, GUILayout.Height(30)) && input.CustomController) input.ChangeMode(false);
            if (GUILayout.Toggle(input.CustomController, "Custom · primary", GUI.skin.button, GUILayout.Height(30)) && !input.CustomController) input.ChangeMode(true);
            GUILayout.EndHorizontal();
            if (input.CustomController)
            {
                GUILayout.BeginHorizontal();
                input.Port = GUILayout.TextField(input.Port, 64, field);
                if (GUILayout.Button("Connect", GUILayout.Height(34), GUILayout.Width(100))) input.ConnectDevice();
                GUILayout.EndHorizontal();
                var local = IntegratedCrew.Local;
                if (local != null && local.Role.Value == CrewRole.Driver)
                {
                    GUILayout.Label($"Full steering range: {input.SteeringFullScale:0.00} (encoder radians / raw input units)", small);
                    input.SteeringFullScale = GUILayout.HorizontalSlider(input.SteeringFullScale, .1f, 6.3f);
                    input.InvertSteering = GUILayout.Toggle(input.InvertSteering, "Invert steering");
                }
                if (GUILayout.Button("Calibrate neutral position  ·  C", GUILayout.Height(30))) input.Calibrate();
                GUILayout.Label(input.DeviceStatus + (input.HasFreshSample ? "" : "  • no fresh input"), small);
                GUILayout.Label("Driver: 9600 baud. Shooter: 115200 baud, angles in radians. Center the device before connecting. C recalibrates.", small);
            }
            else GUILayout.Label("Driver: A/D. Shooter: A/D balance, mouse aim, click / Space fire, R reload, right click zoom. Esc opens settings.", small);
        }
        void Results(RoundState state)
        {
            GUILayout.Label("GETAWAY ENDED / " + state.Defeat, stat);
            GUILayout.Label($"{state.Distance:0} m survived  /  {state.Elapsed:0} seconds\n{state.Score} points  /  {state.PoliceDefeated} police defeated\n{state.Hits} hits from {state.Shots} shots  /  District {GetawayRules.District(state.Distance)}", body);
            GUILayout.Space(16); GUILayout.Label("Swap roles or tune your controllers before the next run.", body);
            if (manager.IsHost && GUILayout.Button("Return crew to lobby", button)) IntegratedCrew.Session.BackToLobby();
            if (!manager.IsHost) GUILayout.Label("Waiting for the host to return to the lobby.", small);
            if (GUILayout.Button("Leave crew", button)) Leave();
        }
        void HUD(RoundState state)
        {
            GUILayout.BeginArea(new Rect(24, 20, 1232, 98), card);
            GUILayout.BeginHorizontal(); GUILayout.Label("BLIND GETAWAY / SURVIVAL", subtitle);
            GUILayout.FlexibleSpace(); GUILayout.Label($"{state.Distance:0} m    {state.Score} PTS    {state.Speed * 3.6f:0} km/h", stat); GUILayout.EndHorizontal();
            string hull = new string('●', Mathf.Max(0, state.Hull)) + new string('○', Mathf.Max(0, GetawayRules.MaxHull - state.Hull));
            string role = input.ViewingDriver ? "DRIVER · FIRST PERSON" : "SHOOTER · FIRST PERSON";
            GUILayout.Label($"HULL  {hull}     /     {role}     /     {(input.CustomController ? "CUSTOM CONTROLLER" : "KEYBOARD + MOUSE · TEST")}     /     Esc · settings", small);
            GUILayout.EndArea();
            bool shooter = !input.ViewingDriver;
            if (shooter)
            {
                int turn = GetawayRules.Turn(state.Distance);
                float until = GetawayRules.ToTurn(state.Distance);
                string direction = turn < 0 ? "LEFT" : turn > 0 ? "RIGHT" : "STRAIGHT";
                GUILayout.BeginArea(new Rect(24, 165, 700, 165), card);
                GUILayout.Label("NAVIGATOR ONLY · Tell your driver", subtitle);
                GUILayout.Label($"District {GetawayRules.District(state.Distance)}/5 · Circuit {GetawayRules.Circuit(state.Distance)} · {direction} {(until > 0 ? $"in {until:0} m" : "NOW")}", body);
                GUILayout.Label($"BALANCE {state.Balance:0}% · Roof {state.RoofX:+0.0;-0.0;0} · Countersteer with A/D or tilt", small);
                GUILayout.Label($"AMMO {state.Ammo}/{state.Reserve} · {(state.ReloadTime > 0 ? $"Reloading {state.ReloadTime:0.0}s" : "R reloads · empty magazine reloads automatically")}", small);
                GUILayout.EndArea();
            }
            else GUI.Label(new Rect(24, 125, 800, 35), "DRIVER · Watch traffic and listen to your navigator for route instructions.", body);
            GUI.Label(new Rect(860, 125, 390, 35), $"POLICE PRESSURE {state.Pursuit:0}%", body);
            if (state.Phase == RoundPhase.Countdown)
            {
                GUI.Label(new Rect(450, 260, 400, 80), "ROLLING IN  " + Mathf.CeilToInt(state.Countdown), title);
            }
            if (input.CustomController && !input.HasFreshSample)
                GUI.Label(new Rect(240, 630, 800, 50), "CONTROLLER INPUT LOST — controls released. Esc to reconnect or select keyboard testing.", body);
            if (world.DamageFeedback > 0)
            {
                GUI.color = new Color(1, .15f, .08f, world.DamageFeedback * .18f);
                GUI.DrawTexture(new Rect(0, 0, 1280, 720), Texture2D.whiteTexture); GUI.color = Color.white;
            }
        }
        void OnDestroy()
        {
            MenuOpen = false;
            PlayerPrefs.SetString("Getaway.Name", PlayerName);
            if (input != null)
            {
                PlayerPrefs.SetString("Getaway.Port", input.Port);
                PlayerPrefs.SetInt("Getaway.Custom", input.CustomController ? 1 : 0);
                PlayerPrefs.SetFloat("Getaway.SteeringScale", input.SteeringFullScale);
                PlayerPrefs.SetInt("Getaway.InvertSteering", input.InvertSteering ? 1 : 0);
            }
            PlayerPrefs.Save();
            if (manager != null)
            {
                manager.ConnectionApprovalCallback -= Approve; manager.OnClientConnectedCallback -= Connected;
                manager.OnClientDisconnectCallback -= Disconnected; manager.OnTransportFailure -= TransportFailed;
            }
            if (panelTexture != null) Destroy(panelTexture);
        }
    }
}
