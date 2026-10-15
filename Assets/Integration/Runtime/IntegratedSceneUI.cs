using TMPro;
using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;
using Slider = UnityEngine.UI.Slider;

namespace Getaway.Integration
{
    /// <summary>Responsive menus using the UI Button scene artwork and one reusable button prefab.</summary>
    public class IntegratedSceneUI : MonoBehaviour
    {
        public Button ButtonPrefab;
        IntegratedGetaway game;
        IntegratedInput input;
        GameObject main, join, room, levels, settings, hud;
        TMP_InputField nameField, addressField, portField;
        TMP_Text status, heading, roomInstructions, resultSummary, settingsStatus, modeLabel, volumeLabel, sensitivityLabel, rangeLabel;
        TMP_Text hudStats, hudRole, hudHint, navigator, countdown, deviceWarning;
        Button ready, start, back, driver, shooter, solo, mainHost, mainClient, connect, keyboard, custom, invert, graphics, leaveSettings;
        GameObject hardware;
        Image damage;
        TMP_FontAsset font;
        bool joining, built;
        float nextRefresh;
        static readonly Color Cyan = new Color(.46f, .97f, 1);
        void Awake() { game = GetComponent<IntegratedGetaway>(); game.NativeUI = true; }
        void Start() { BuildUI(); Refresh(); }

        // Also used by the edit-mode layout preview; no network or gameplay is started here.
        public void BuildUI()
        {
            if (built) return;
            built = true;
            game = GetComponent<IntegratedGetaway>(); input = GetComponent<IntegratedInput>();
            main = Root("Canvas"); join = Root("Canvas (1)"); room = Root("Canvas (2)"); levels = Root("Canvas (3)");
            if (main == null || join == null || room == null) { enabled = false; return; }
            font = main.GetComponentInChildren<TMP_Text>(true)?.font;
            foreach (var screen in new[] { main, join, room }) FullScreen(screen);
            if (levels != null) levels.SetActive(false);
            nameField = Find<TMP_InputField>(main, "inputusername");
            addressField = Find<TMP_InputField>(join, "inputip");
            StyleField(nameField, new Vector2(0, 95), "Callsign");
            StyleField(addressField, new Vector2(0, 55), "Host LAN address");
            nameField.SetTextWithoutNotify(IntegratedGetaway.PlayerName);
            if (string.IsNullOrWhiteSpace(addressField.text)) addressField.SetTextWithoutNotify("127.0.0.1");
            Title(main.transform, "BLIND GETAWAY", "TWO PLAYERS / ENDLESS SURVIVAL");
            Title(join.transform, "JOIN YOUR CREW", "SAME LAN / UDP 7777");
            heading = Title(room.transform, "CREW LOBBY", "CHOOSE A ROLE, THEN READY UP");
            roomInstructions = Find<TMP_Text>(room, "CHOOSE A ROLE, THEN READY UP");
            resultSummary = Text(room.transform, "Round results", 24); Place(resultSummary.rectTransform, new Vector2(0, 65), new Vector2(1000, 135)); resultSummary.gameObject.SetActive(false);
            mainHost = Existing(main, "host", "HOST CREW", new Vector2(0, 10), () => { ReadFields(); game.Connect(true); });
            mainClient = Existing(main, "client", "JOIN CREW", new Vector2(0, -70), () => joining = true);
            solo = Action(main.transform, "SOLO DEBUG", new Vector2(0, -150), () => { ReadFields(); game.StartSoloDebug(); });
            Action(main.transform, "SETTINGS", new Vector2(0, -230), () => game.OpenSettings());
            connect = Existing(join, "connect", "CONNECT", new Vector2(0, -35), () => { ReadFields(); game.Connect(false); });
            Action(join.transform, "BACK / CANCEL", new Vector2(0, -115), () => { if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) game.Leave(); joining = false; });
            Action(join.transform, "SETTINGS", new Vector2(0, -195), () => game.OpenSettings());
            driver = Existing(room, "driver", "DRIVER", new Vector2(-170, 110), () => Choose(CrewRole.Driver));
            shooter = Existing(room, "shooter", "SHOOTER", new Vector2(170, 110), () => Choose(CrewRole.Shooter));
            foreach (var item in room.GetComponentsInChildren<RectTransform>(true))
                if (item.name == "roles" || item.name == "room" || item.name == "playerscroll" || item.name.StartsWith("level")) item.gameObject.SetActive(false);
            ready = Action(room.transform, "READY", new Vector2(0, 10), () => IntegratedCrew.Local?.ToggleReady());
            start = Action(room.transform, "START SURVIVAL", new Vector2(0, -70), () =>
            {
                var session = IntegratedCrew.Session;
                if (session == null) return;
                if (session.State.Value.Phase == RoundPhase.Wrecked) session.BackToLobby(); else session.BeginRound();
            });
            Action(room.transform, "SETTINGS", new Vector2(-170, -170), () => game.OpenSettings());
            back = Action(room.transform, "LEAVE CREW", new Vector2(170, -170), () => game.Leave());
            var overlay = Canvas("Integration overlay", 100);
            status = Text(overlay.transform, "Session status", 19);
            Place(status.rectTransform, new Vector2(0, -315), new Vector2(1000, 60));
            settings = Screen(overlay.transform, "Settings", BackgroundSprite(main));
            BuildSettings();
            hud = new GameObject("Survival HUD", typeof(RectTransform)); hud.transform.SetParent(overlay.transform, false); Stretch((RectTransform)hud.transform);
            BuildHUD();
            settings.SetActive(false); hud.SetActive(false);
        }
        void FullScreen(GameObject screen)
        {
            var canvas = screen.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = screen.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            var background = Find<Image>(screen, "Background");
            if (background != null) { Stretch(background.rectTransform); background.preserveAspect = false; background.raycastTarget = false; background.transform.SetAsFirstSibling(); }
        }
        void BuildSettings()
        {
            Title(settings.transform, "CREW SETTINGS", "CUSTOM HARDWARE IS THE PRIMARY CONTROLLER / ESC TO RETURN");
            keyboard = Action(settings.transform, "KEYBOARD / MOUSE", new Vector2(-210, 140), () => input.ChangeMode(false), 370);
            custom = Action(settings.transform, "CUSTOM CONTROLLER", new Vector2(210, 140), () => input.ChangeMode(true), 370);
            modeLabel = Text(settings.transform, "Controller mode", 18); Place(modeLabel.rectTransform, new Vector2(0, 90), new Vector2(900, 30));
            volumeLabel = Text(settings.transform, "Volume", 18); Place(volumeLabel.rectTransform, new Vector2(-210, 45), new Vector2(370, 30));
            MakeSlider(settings.transform, new Vector2(-210, 10), 0, 1, AudioListener.volume, v => AudioListener.volume = v);
            sensitivityLabel = Text(settings.transform, "Sensitivity", 18); Place(sensitivityLabel.rectTransform, new Vector2(210, 45), new Vector2(370, 30));
            MakeSlider(settings.transform, new Vector2(210, 10), .2f, 2, input != null ? input.SteeringSensitivity : 1, v => input.SteeringSensitivity = v);
            hardware = new GameObject("Hardware controls", typeof(RectTransform)); hardware.transform.SetParent(settings.transform, false); Stretch((RectTransform)hardware.transform);
            portField = Instantiate(nameField, hardware.transform); portField.name = "Serial port";
            StyleField(portField, new Vector2(-210, -65), "COM3 or /dev/ttyACM0"); portField.SetTextWithoutNotify(input != null ? input.Port : "");
            portField.onValueChanged.RemoveAllListeners(); portField.onValueChanged.AddListener(v => input.Port = v);
            Action(hardware.transform, "CONNECT DEVICE", new Vector2(210, -65), () => { input.Port = portField.text; input.ConnectDevice(); }, 370);
            rangeLabel = Text(hardware.transform, "Steering range", 18); Place(rangeLabel.rectTransform, new Vector2(-210, -115), new Vector2(370, 30));
            MakeSlider(hardware.transform, new Vector2(-210, -145), .1f, 6.3f, input != null ? input.SteeringFullScale : 1.57f, v => input.SteeringFullScale = v);
            invert = Action(hardware.transform, "INVERT STEERING: OFF", new Vector2(210, -145), () => input.InvertSteering = !input.InvertSteering, 370);
            Action(hardware.transform, "CALIBRATE / C", new Vector2(-210, -220), () => input.Calibrate(), 370);
            graphics = Action(settings.transform, "GRAPHICS", new Vector2(210, -220), () =>
            { if (DowntownRendering.Instance != null) { DowntownRendering.Instance.Cycle(); return; } if (QualitySettings.names.Length > 0) QualitySettings.SetQualityLevel((QualitySettings.GetQualityLevel() + 1) % QualitySettings.names.Length, false); }, 370);
            settingsStatus = Text(settings.transform, "Controller status", 17); Place(settingsStatus.rectTransform, new Vector2(0, -285), new Vector2(1040, 70));
            var resume = Action(settings.transform, "BACK / ESC", Vector2.zero, () => game.OpenSettings(), 240);
            var rect = (RectTransform)resume.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 1); rect.anchoredPosition = new Vector2(-25, -22); rect.sizeDelta = new Vector2(210, 46);
            leaveSettings = Action(settings.transform, "LEAVE SESSION", Vector2.zero, () => game.Leave(), 240);
            var leaveRect = (RectTransform)leaveSettings.transform; leaveRect.anchorMin = leaveRect.anchorMax = leaveRect.pivot = new Vector2(0, 1); leaveRect.anchoredPosition = new Vector2(25, -22); leaveRect.sizeDelta = new Vector2(210, 46);
            leaveSettings.GetComponentInChildren<TMP_Text>().fontSize = 18;
        }
        void BuildHUD()
        {
            var panel = new GameObject("Instruments", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(hud.transform, false);
            var rect = (RectTransform)panel.transform; rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1); rect.sizeDelta = new Vector2(-40, 88); rect.anchoredPosition = new Vector2(0, -16);
            panel.GetComponent<Image>().color = new Color(.09f, .025f, .19f, .88f); panel.GetComponent<Image>().raycastTarget = false;
            hudStats = Text(panel.transform, "Distance and hull", 25); Stretch(hudStats.rectTransform); hudStats.rectTransform.offsetMin = new Vector2(18, 38); hudStats.rectTransform.offsetMax = new Vector2(-18, -7); hudStats.alignment = TextAlignmentOptions.Left;
            hudRole = Text(panel.transform, "Role", 17); Stretch(hudRole.rectTransform); hudRole.rectTransform.offsetMin = new Vector2(18, 6); hudRole.rectTransform.offsetMax = new Vector2(-18, -47); hudRole.alignment = TextAlignmentOptions.Left;
            hudHint = Text(hud.transform, "Controls", 17); var hint = hudHint.rectTransform; hint.anchorMin = new Vector2(0, 1); hint.anchorMax = Vector2.one; hint.pivot = new Vector2(.5f, 1); hint.sizeDelta = new Vector2(-50, 44); hint.anchoredPosition = new Vector2(0, -110);
            navigator = Text(hud.transform, "Navigator", 19); var nav = navigator.rectTransform; nav.anchorMin = nav.anchorMax = nav.pivot = new Vector2(0, 1); nav.sizeDelta = new Vector2(740, 130); nav.anchoredPosition = new Vector2(30, -170); navigator.alignment = TextAlignmentOptions.TopLeft;
            countdown = Text(hud.transform, "Countdown", 42); Place(countdown.rectTransform, Vector2.zero, new Vector2(650, 90));
            deviceWarning = Text(hud.transform, "Device warning", 18); var warning = deviceWarning.rectTransform; warning.anchorMin = new Vector2(0, 0); warning.anchorMax = new Vector2(1, 0); warning.pivot = new Vector2(.5f, 0); warning.sizeDelta = new Vector2(-40, 65); warning.anchoredPosition = new Vector2(0, 15);
            var flash = new GameObject("Damage feedback", typeof(RectTransform), typeof(Image)); flash.transform.SetParent(hud.transform, false); damage = flash.GetComponent<Image>(); Stretch(damage.rectTransform); damage.raycastTarget = false; damage.color = Color.clear;
        }
        void MakeSlider(Transform parent, Vector2 position, float min, float max, float value, UnityAction<float> changed)
        {
            var go = new GameObject("Slider", typeof(RectTransform), typeof(Image), typeof(Slider)); go.transform.SetParent(parent, false); Place((RectTransform)go.transform, position, new Vector2(350, 22));
            go.GetComponent<Image>().color = new Color(.2f, .08f, .4f);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)); handle.transform.SetParent(go.transform, false); var r = (RectTransform)handle.transform; r.sizeDelta = new Vector2(20, 30); handle.GetComponent<Image>().color = Cyan;
            var slider = go.GetComponent<Slider>(); slider.handleRect = r; slider.targetGraphic = handle.GetComponent<Image>(); slider.minValue = min; slider.maxValue = max; slider.SetValueWithoutNotify(value); slider.onValueChanged.AddListener(changed);
        }
        GameObject Canvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster)); go.transform.SetParent(transform, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; go.GetComponent<Canvas>().sortingOrder = order;
            var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f; return go;
        }
        static GameObject Screen(Transform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false); Stretch((RectTransform)go.transform); go.GetComponent<Image>().sprite = sprite; return go;
        }
        static Sprite BackgroundSprite(GameObject root) => Find<Image>(root, "Background")?.sprite;
        TMP_Text Title(Transform parent, string title, string subtitle)
        {
            var label = Text(parent, title, 38); var rect = label.rectTransform; rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1); rect.sizeDelta = new Vector2(-60, 55); rect.anchoredPosition = new Vector2(0, -72); label.fontStyle = FontStyles.Bold;
            var sub = Text(parent, subtitle, 17); var r = sub.rectTransform; r.anchorMin = new Vector2(0, 1); r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, 1); r.sizeDelta = new Vector2(-60, 36); r.anchoredPosition = new Vector2(0, -128); return label;
        }
        void ReadFields()
        {
            if (!string.IsNullOrWhiteSpace(nameField.text)) IntegratedGetaway.PlayerName = nameField.text;
            game.SetAddress(addressField.text);
        }
        void Choose(CrewRole role)
        {
            input.DisconnectDevice(); var local = IntegratedCrew.Local;
            if (local != null) local.Choose(local.Role.Value == role ? CrewRole.None : role);
        }
        GameObject Root(string name)
        { foreach (var root in gameObject.scene.GetRootGameObjects()) if (root.name == name) return root; return null; }
        static T Find<T>(GameObject root, string name) where T : Component
        { if (root != null) foreach (var item in root.GetComponentsInChildren<T>(true)) if (item.name == name) return item; return null; }
        Button Existing(GameObject root, string name, string label, Vector2 position, UnityAction clicked)
        { var result = Find<Button>(root, name); StyleButton(result, label, ButtonPrefab); Place((RectTransform)result.transform, position, new Vector2(310, 62)); result.onClick.RemoveAllListeners(); result.onClick.AddListener(clicked); return result; }
        Button Action(Transform parent, string label, Vector2 position, UnityAction clicked, float width = 310)
        {
            Button result;
            if (ButtonPrefab != null) result = Instantiate(ButtonPrefab, parent, false);
            else { var go = new GameObject(label, typeof(RectTransform), typeof(GetawayButtonGraphic), typeof(Button)); go.transform.SetParent(parent, false); result = go.GetComponent<Button>(); }
            result.name = label; StyleButton(result, label, ButtonPrefab); Place((RectTransform)result.transform, position, new Vector2(width, 62)); result.onClick.RemoveAllListeners(); result.onClick.AddListener(clicked); return result;
        }
        public static void StyleButton(Button button, string label, Button style = null)
        {
            var image = button.GetComponent<Image>(); if (image != null) image.enabled = false;
            var graphic = button.GetComponentInChildren<GetawayButtonGraphic>(true);
            if (graphic == null)
            {
                var surface = new GameObject("Button surface", typeof(RectTransform), typeof(GetawayButtonGraphic));
                surface.transform.SetParent(button.transform, false); surface.transform.SetAsFirstSibling();
                Stretch((RectTransform)surface.transform); graphic = surface.GetComponent<GetawayButtonGraphic>();
            }
            graphic.raycastTarget = true; button.targetGraphic = graphic;
            button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
            if (style != null)
            {
                var sourceGraphic = style.GetComponentInChildren<GetawayButtonGraphic>(true);
                if (sourceGraphic != null) { graphic.Top = sourceGraphic.Top; graphic.Bottom = sourceGraphic.Bottom; graphic.Outline = sourceGraphic.Outline; graphic.SetVerticesDirty(); }
                button.colors = style.colors;
            }
            else
            {
                var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f); colors.pressedColor = new Color(.65f, .65f, .85f); colors.selectedColor = Color.white; colors.disabledColor = new Color(.4f, .4f, .4f, .6f); button.colors = colors;
            }
            var text = button.GetComponentInChildren<TMP_Text>(true);
            if (text == null) { var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(button.transform, false); text = go.GetComponent<TextMeshProUGUI>(); }
            Stretch(text.rectTransform); text.rectTransform.offsetMin = new Vector2(12, 4); text.rectTransform.offsetMax = new Vector2(-12, -4);
            text.text = label; text.color = Cyan; text.fontSize = 22; text.fontStyle = FontStyles.Bold; text.alignment = TextAlignmentOptions.Center; text.enableAutoSizing = false; text.raycastTarget = false;
            var sourceText = style != null ? style.GetComponentInChildren<TMP_Text>(true) : null;
            if (sourceText != null) { text.font = sourceText.font; text.fontSize = sourceText.fontSize; text.fontStyle = sourceText.fontStyle; text.color = sourceText.color; }
        }
        static void SetLabel(Button button, string text) { button.GetComponentInChildren<TMP_Text>(true).text = text; }
        TMP_Text Text(Transform parent, string name, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); var text = go.GetComponent<TextMeshProUGUI>();
            text.font = font; text.text = name; text.color = Cyan; text.fontSize = size; text.enableAutoSizing = false; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; return text;
        }
        static void StyleField(TMP_InputField field, Vector2 position, string placeholder)
        {
            Place((RectTransform)field.transform, position, new Vector2(370, 50));
            field.textComponent.fontSize = 22; field.textComponent.color = Color.white; field.textComponent.enableAutoSizing = false;
            if (field.placeholder is TMP_Text label) { label.text = placeholder; label.fontSize = 20; label.color = new Color(.7f, .8f, .9f); }
        }
        static void Place(RectTransform rect, Vector2 position, Vector2 size)
        { rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size; }
        static void Stretch(RectTransform rect)
        { rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        void Update()
        {
            if (!built || status == null || game == null) return;
            if (Time.unscaledTime < nextRefresh) return; nextRefresh = Time.unscaledTime + .1f;
            Refresh();
        }
        void Refresh()
        {
            var session = IntegratedCrew.Session; var manager = NetworkManager.Singleton;
            bool connected = session != null && manager != null && manager.IsConnectedClient;
            var phase = connected ? session.State.Value.Phase : RoundPhase.Lobby;
            bool live = connected && (phase == RoundPhase.Playing || phase == RoundPhase.Countdown);
            bool visible = !live && !IntegratedGetaway.MenuOpen;
            main.SetActive(visible && !connected && !joining); join.SetActive(visible && !connected && joining); room.SetActive(visible && connected);
            settings.SetActive(IntegratedGetaway.MenuOpen); hud.SetActive(live && !IntegratedGetaway.MenuOpen); status.gameObject.SetActive(visible);
            bool idle = manager != null && !manager.IsListening && !manager.ShutdownInProgress;
            mainHost.interactable = mainClient.interactable = solo.interactable = connect.interactable = idle;
            var local = IntegratedCrew.Local;
            bool lobby = phase == RoundPhase.Lobby;
            driver.gameObject.SetActive(lobby); shooter.gameObject.SetActive(lobby); ready.gameObject.SetActive(lobby);
            driver.interactable = shooter.interactable = connected && lobby;
            foreach (var member in IntegratedCrew.Members)
            { if (member == local || !member.IsSpawned) continue; if (member.Role.Value == CrewRole.Driver) driver.interactable = false; if (member.Role.Value == CrewRole.Shooter) shooter.interactable = false; }
            SetLabel(driver, local != null && local.Role.Value == CrewRole.Driver ? "DRIVER / SELECTED" : "DRIVER");
            SetLabel(shooter, local != null && local.Role.Value == CrewRole.Shooter ? "SHOOTER / SELECTED" : "SHOOTER");
            ready.interactable = connected && lobby && local != null && local.Role.Value != CrewRole.None;
            SetLabel(ready, local != null && local.Ready.Value ? "CANCEL READY" : "READY");
            start.interactable = connected && session.IsServer && (phase == RoundPhase.Wrecked || IntegratedCrew.CanStart());
            SetLabel(start, phase == RoundPhase.Wrecked ? "RETURN TO LOBBY" : "START SURVIVAL");
            heading.text = phase == RoundPhase.Wrecked ? "GETAWAY ENDED" : "CREW LOBBY";
            roomInstructions.text = phase == RoundPhase.Wrecked ? "SWAP ROLES OR TUNE YOUR CONTROLLERS BEFORE THE NEXT RUN" : "CHOOSE A ROLE, THEN READY UP";
            resultSummary.gameObject.SetActive(phase == RoundPhase.Wrecked);
            if (phase == RoundPhase.Wrecked)
            {
                var result = session.State.Value;
                resultSummary.text = $"{result.Defeat}\n{result.Distance:0} m / {result.Elapsed:0} seconds / {result.Score} PTS\n{result.PoliceDefeated} police defeated / {result.Hits} hits from {result.Shots} shots";
            }
            string crew = "";
            foreach (var member in IntegratedCrew.Members) if (member.IsSpawned) crew += $"{member.DisplayName.Value}: {member.Role.Value} {(member.Ready.Value ? "READY" : "NOT READY")}   ";
            status.text = connected ? crew : game.Status;
            if (IntegratedGetaway.MenuOpen) RefreshSettings();
            if (live) RefreshHUD(session.State.Value, session.SoloDebug.Value);
        }
        void RefreshSettings()
        {
            keyboard.interactable = input.CustomController; custom.interactable = !input.CustomController; hardware.SetActive(input.CustomController);
            modeLabel.text = input.CustomController ? "CUSTOM HARDWARE / choose your role before connecting" : "KEYBOARD TEST / A-D: steer or balance / Mouse: aim / Click: fire / R: reload";
            volumeLabel.text = $"VOLUME {AudioListener.volume:P0}"; sensitivityLabel.text = $"STEERING SENSITIVITY {input.SteeringSensitivity:0.0}";
            rangeLabel.text = $"FULL STEERING RANGE {input.SteeringFullScale:0.00}"; SetLabel(invert, "INVERT STEERING: " + (input.InvertSteering ? "ON" : "OFF"));
            leaveSettings.gameObject.SetActive(NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);
            if (DowntownRendering.Instance != null) SetLabel(graphics, "GRAPHICS: " + DowntownRendering.Instance.PresetName);
            else if (QualitySettings.names.Length > 0) SetLabel(graphics, "GRAPHICS: " + QualitySettings.names[QualitySettings.GetQualityLevel()].ToUpperInvariant());
            settingsStatus.text = input.CustomController ? input.DeviceStatus + "\nDriver: 9600 baud / Shooter: 115200 baud / center the device, then calibrate" : "Tab switches first-person roles in Solo Debug. Esc returns to the game.\nThe multiplayer game continues while settings are open.";
        }
        void RefreshHUD(RoundState state, bool debug)
        {
            string role = input.ViewingDriver ? "DRIVER" : "SHOOTER";
            hudStats.text = $"HULL {state.Hull}/{GetawayRules.MaxHull}     {state.Distance:0} m     {state.Score} PTS     {state.Speed * 3.6f:0} km/h     POLICE {state.Pursuit:0}%";
            hudRole.text = $"{role} / FIRST PERSON     {(input.CustomController ? "CUSTOM CONTROLLER" : "KEYBOARD + MOUSE TEST")}     ESC: SETTINGS";
            hudHint.text = debug ? "SOLO DEBUG / Tab: switch role / A-D: active role / Mouse: look or aim" : input.ViewingDriver ? "Watch traffic and listen to your navigator." : "A-D: balance / Mouse: aim 360° / Click or Space: fire / R: reload";
            navigator.gameObject.SetActive(!input.ViewingDriver);
            var route = GetawayRules.Route;
            int turn; float until;
            if (route != null) route.NextBend(state.Distance, out turn, out until);
            else { turn = GetawayRules.Turn(state.Distance); until = GetawayRules.ToTurn(state.Distance); }
            navigator.text = $"NAVIGATOR / TELL YOUR DRIVER\n{(route != null ? route.DistrictName + " / 1 of " + route.DistrictCount : "District " + GetawayRules.District(state.Distance) + "/5")} / Circuit {(route != null ? route.Circuit(state.Distance) : GetawayRules.Circuit(state.Distance))} / {(turn < 0 ? "LEFT" : turn > 0 ? "RIGHT" : "STRAIGHT")} {(until > 0 ? $"in {until:0} m / {until / Mathf.Max(1, state.Speed):0.0}s" : "NOW")}\nBALANCE {state.Balance:0}% / AMMO {state.Ammo}/{state.Reserve}" + (state.ReloadTime > 0 ? $" / RELOADING {state.ReloadTime:0.0}s" : "");
            if (!input.ViewingDriver)
            {
                string pursuit = ""; var session = IntegratedCrew.Session;
                if (state.PoliceEventUntil > state.Elapsed)
                    pursuit = state.PoliceEvent == PursuitEvent.Approaching ? "POLICE APPROACHING FROM BEHIND" :
                        state.PoliceEvent == PursuitEvent.Escaped ? "POLICE LOST / ESCAPED" :
                        state.PoliceEvent == PursuitEvent.Defeated ? "POLICE DEFEATED" :
                        state.PoliceEvent == PursuitEvent.Withdrawn ? "POLICE FELL BACK" : "REAR PURSUIT / CAPTURE PRESSURE";
                if (session != null) foreach (var t in session.Threats) if (t.Active && t.Kind == ThreatKind.Police)
                    pursuit += $" / REAR {state.Distance - t.RouteDistance:0} m / {(t.Phase == ThreatPhase.Approach ? "APPROACH" : "PURSUIT")}";
                if (pursuit.Length > 0) navigator.text += "\n" + pursuit;
            }
            countdown.text = state.Phase == RoundPhase.Countdown ? "ROLLING IN " + Mathf.CeilToInt(state.Countdown) : "";
            deviceWarning.text = input.CustomController && !input.HasFreshSample ? "CONTROLLER INPUT LOST / Esc to reconnect or select keyboard testing" : "";
            damage.color = new Color(1, .15f, .08f, GetComponent<IntegratedWorld>().DamageFeedback * .18f);
        }
    }
}
