using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

/// <summary>Self-contained LAN lobby and presentation for the cooperative getaway.</summary>
public class NetworkStartUI : MonoBehaviour
{
    public static string PlayerName = "Player";
    private string address = "127.0.0.1", status = "Host a lobby or join your driver's / robber's IP address.";
    private NetworkManager manager;
    private Transform car, robber, gun, road;
    private readonly Transform[] obstacles = new Transform[PlayerNetwork.ObstacleCount];
    private Camera view;
    private LineRenderer tracer;
    private float nextInput, tracerUntil;
    private int lastShot;
    private bool connecting;
    private GUIStyle title, text;

    private void Start()
    {
        manager = NetworkManager.Singleton;
        if (manager == null) { status = "Scene needs a NetworkManager and UnityTransport."; return; }
        manager.NetworkConfig.ConnectionApproval = true;
        manager.ConnectionApprovalCallback = Approve;
        manager.OnClientConnectedCallback += Connected;
        manager.OnClientDisconnectCallback += Disconnected;
        manager.OnTransportFailure += TransportFailed;
        Application.runInBackground = true;
        BuildWorld();
    }
    private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        bool available = manager.ConnectedClients.Count < 2 && (PlayerNetwork.Session == null || PlayerNetwork.Session.phase.Value == 0);
        response.Approved = available;
        response.CreatePlayerObject = available;
        response.Reason = available ? "" : "Lobby is full or the getaway has already started.";
        response.Pending = false;
    }
    private void Connected(ulong id)
    {
        if (id != manager.LocalClientId) return;
        connecting = false;
        status = "Connected. Choose a role and ready up.";
    }
    private void Disconnected(ulong id)
    {
        if (id == manager.LocalClientId || !manager.IsClient)
        {
            connecting = false;
            status = string.IsNullOrEmpty(manager.DisconnectReason) ? "Disconnected. You can host or join again." : manager.DisconnectReason;
        }
        if (manager.IsServer && PlayerNetwork.Session != null) PlayerNetwork.Session.ReturnToLobby();
    }
    private void TransportFailed() { connecting = false; status = "Connection failed. Check the host address and UDP port 7777."; manager.Shutdown(); }
    private void OnDestroy()
    {
        if (manager == null) return;
        manager.OnClientConnectedCallback -= Connected;
        manager.OnClientDisconnectCallback -= Disconnected;
        manager.OnTransportFailure -= TransportFailed;
        manager.ConnectionApprovalCallback -= Approve;
    }
    private void Connect(bool host)
    {
        var transport = manager.GetComponent<UnityTransport>();
        if (transport == null) { status = "UnityTransport is missing."; return; }
        transport.SetConnectionData(string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim(), 7777, "0.0.0.0");
        connecting = true;
        status = host ? "Creating lobby..." : "Connecting...";
        try
        {
            if (!(host ? manager.StartHost() : manager.StartClient())) { connecting = false; status = "Could not start networking."; }
        }
        catch (System.Exception error) { connecting = false; status = error.Message; manager.Shutdown(); }
    }
    private void Update()
    {
        var session = PlayerNetwork.Session;
        if (session == null || car == null) return;
        car.position = Vector3.Lerp(car.position, session.carPosition.Value, 1 - Mathf.Exp(-15 * Time.deltaTime));
        robber.localPosition = new Vector3(session.robberOffset.Value, 3.1f, 0);
        gun.localRotation = Quaternion.Euler(0, session.aimYaw.Value, 0);
        road.position = new Vector3(0, -.25f, car.position.z + 100);
        for (int i = 0; i < obstacles.Length; i++) obstacles[i].gameObject.SetActive((session.clearedObstacles.Value & (1UL << i)) == 0);
        bool isRobber = PlayerNetwork.Local != null && PlayerNetwork.Local.playerRole.Value == 2;
        Vector3 cameraOffset = isRobber ? new Vector3(0, 7, -11) : new Vector3(0, 8, -15);
        view.transform.position = car.position + cameraOffset;
        view.transform.LookAt(car.position + new Vector3(0, 1.5f, 18));
        if (session.shotSequence.Value != lastShot)
        {
            lastShot = session.shotSequence.Value;
            tracer.SetPosition(0, robber.position + Vector3.forward);
            tracer.SetPosition(1, session.shotEnd.Value);
            tracerUntil = Time.time + .12f;
        }
        tracer.enabled = Time.time < tracerUntil;
        if (session.phase.Value != 1 || PlayerNetwork.Local == null || Time.unscaledTime < nextInput) return;
        nextInput = Time.unscaledTime + .05f;
        var keyboard = Keyboard.current;
        float horizontal = 0;
        if (Application.isFocused && keyboard != null)
            horizontal = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
        float yaw = Mouse.current == null ? 0 : Mathf.Clamp((Mouse.current.position.ReadValue().x / Screen.width - .5f) * 140, -70, 70);
        bool fire = Application.isFocused && ((Mouse.current != null && Mouse.current.leftButton.isPressed) || (keyboard != null && keyboard.spaceKey.isPressed));
        PlayerNetwork.Local.SubmitInput(isRobber ? 0 : horizontal, isRobber ? horizontal : 0, yaw, fire);
    }
    private void OnGUI()
    {
        title ??= new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
        text ??= new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
        float width = Mathf.Min(540, Screen.width - 24);
        var session = PlayerNetwork.Session;
        bool running = session != null && session.phase.Value == 1;
        GUILayout.BeginArea(new Rect(12, 12, width, running ? 155 : Screen.height - 24), GUI.skin.box);
        GUILayout.Label("ROOFTOP GETAWAY", title);
        if (running)
        {
            GUILayout.Label($"Hull {session.health.Value}/3   •   {session.carPosition.Value.z:0} / {PlayerNetwork.FinishDistance:0} m", text);
            GUILayout.Label(PlayerNetwork.Local != null && PlayerNetwork.Local.playerRole.Value == 1 ? "DRIVER • A/D or ←/→ to steer. Avoid the red roadblocks." : "ROBBER • A/D to move on the roof. Mouse aims, hold click / Space to fire.", text);
        }
        else if (manager != null && manager.IsConnectedClient && session != null)
        {
            GUILayout.Label("Two players. One driver. One robber on the roof.", text);
            foreach (var p in PlayerNetwork.Players)
                if (p.IsSpawned) GUILayout.Label($"{p.playerName.Value}  •  {Role(p.playerRole.Value)}  •  {(p.isReady.Value ? "READY" : "Not ready")}", text);
            if (session.phase.Value == 0)
            {
                GUILayout.Space(12);
                if (PlayerNetwork.Local != null)
                {
                    GUILayout.BeginHorizontal();
                    GUI.enabled = RoleAvailable(1);
                    if (GUILayout.Button("Choose Driver", GUILayout.Height(40))) PlayerNetwork.Local.SetRole(1);
                    GUI.enabled = RoleAvailable(2);
                    if (GUILayout.Button("Choose Robber", GUILayout.Height(40))) PlayerNetwork.Local.SetRole(2);
                    GUILayout.EndHorizontal();
                    GUI.enabled = PlayerNetwork.Local.playerRole.Value != 0;
                    if (GUILayout.Button(PlayerNetwork.Local.isReady.Value ? "Cancel Ready" : "Ready", GUILayout.Height(40))) PlayerNetwork.Local.ToggleReady();
                    GUI.enabled = true;
                }
                if (manager.IsHost)
                {
                    GUI.enabled = CanStart();
                    if (GUILayout.Button("Start Getaway", GUILayout.Height(44))) session.StartRound();
                    GUI.enabled = true;
                }
                GUILayout.Label("Both players must choose different roles and be ready. Host starts the getaway.", text);
                GUILayout.Label("LAN • UDP 7777 • Give your partner this computer's LAN IP.", text);
            }
            else
            {
                GUILayout.Label(session.phase.Value == 3 ? "ESCAPED! You made it together." : "GETAWAY FAILED • The car was wrecked.", title);
                if (manager.IsHost && GUILayout.Button("Back to lobby / Play again", GUILayout.Height(42))) session.ReturnToLobby();
            }
        }
        else
        {
            GUILayout.Label("Cooperative LAN multiplayer", text);
            GUILayout.Label("Your name");
            PlayerName = GUILayout.TextField(PlayerName, 16, GUILayout.Height(30));
            GUILayout.Label("Host IP (127.0.0.1 for two instances on this PC)");
            address = GUILayout.TextField(address, 64, GUILayout.Height(30));
            GUI.enabled = manager != null && !connecting && !manager.ShutdownInProgress;
            if (GUILayout.Button("Host Lobby", GUILayout.Height(42))) Connect(true);
            if (GUILayout.Button("Join Lobby", GUILayout.Height(42))) Connect(false);
            GUI.enabled = true;
            GUILayout.Label(status, text);
        }
        if (manager != null && (manager.IsListening || connecting) && GUILayout.Button("Leave / Disconnect", GUILayout.Height(30)))
        { manager.Shutdown(); connecting = false; status = "Left lobby."; }
        GUILayout.EndArea();
    }
    private bool RoleAvailable(int role)
    {
        foreach (var p in PlayerNetwork.Players)
            if (p.IsSpawned && p != PlayerNetwork.Local && p.playerRole.Value == role) return false;
        return true;
    }
    private bool CanStart()
    {
        int count = 0, roles = 0;
        foreach (var p in PlayerNetwork.Players)
        {
            if (!p.IsSpawned) continue;
            if (!p.isReady.Value) return false;
            count++; roles |= 1 << p.playerRole.Value;
        }
        return count == 2 && roles == 6;
    }
    private static string Role(int value) => value == 1 ? "Driver" : value == 2 ? "Robber" : "Choose a role";
    private Transform Box(string name, Vector3 position, Vector3 scale, Color color, Transform parent = null, PrimitiveType shape = PrimitiveType.Cube)
    {
        var item = GameObject.CreatePrimitive(shape);
        item.name = name;
        item.transform.SetParent(parent, false);
        item.transform.localPosition = position;
        item.transform.localScale = scale;
        Destroy(item.GetComponent<Collider>());
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = color;
        item.GetComponent<Renderer>().material = material;
        return item.transform;
    }
    private void BuildWorld()
    {
        view = Camera.main;
        if (view == null) view = new GameObject("Getaway Camera").AddComponent<Camera>();
        view.farClipPlane = 1800;
        view.backgroundColor = new Color(.08f, .12f, .19f);
        view.clearFlags = CameraClearFlags.SolidColor;
        car = new GameObject("Shared Getaway Car").transform;
        Box("Car body", new Vector3(0, 1, 0), new Vector3(2.8f, 1.2f, 5), new Color(.05f, .5f, .6f), car);
        Box("Cabin", new Vector3(0, 2, -.3f), new Vector3(2.5f, 1, 2.8f), new Color(.1f, .22f, .3f), car);
        for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
                Box("Wheel", new Vector3(x * 1.45f, .6f, z * 1.5f), new Vector3(.45f, 1, 1), Color.black, car);
        robber = Box("Roof Robber", new Vector3(0, 3.1f, 0), new Vector3(.65f, .65f, .65f), new Color(1, .65f, .15f), car, PrimitiveType.Capsule);
        gun = Box("Robber Gun", new Vector3(0, .3f, .8f), new Vector3(.25f, .25f, 1.6f), Color.black, robber);
        road = Box("Highway", new Vector3(0, -.25f, 100), new Vector3(15, .3f, 500), new Color(.12f, .14f, .18f));
        for (int i = 0; i < PlayerNetwork.ObstacleCount; i++)
            obstacles[i] = Box("Roadblock " + i, PlayerNetwork.ObstaclePosition(i), new Vector3(2.4f, 2, 2), new Color(.9f, .18f, .12f));
        for (int i = 0; i < 100; i++)
            Box("Lane marker", new Vector3(0, -.08f, i * 16), new Vector3(.12f, .05f, 6), Color.white);
        Box("Finish", new Vector3(0, 5, PlayerNetwork.FinishDistance), new Vector3(15, 1, 1), Color.green);
        tracer = new GameObject("Shot tracer").AddComponent<LineRenderer>();
        tracer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        tracer.material.color = Color.yellow;
        tracer.positionCount = 2;
        tracer.startWidth = tracer.endWidth = .09f;
        tracer.enabled = false;
        view.transform.position = new Vector3(0, 8, -15);
        view.transform.LookAt(new Vector3(0, 1.5f, 18));
    }
}
