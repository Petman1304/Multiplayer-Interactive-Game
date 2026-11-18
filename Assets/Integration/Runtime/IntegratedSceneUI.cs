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
        [Tooltip("Opsional: Tarik Font Asset (misal Montserrat/Poppins/Rajdhani SDF) ke sini, atau simpan di Assets/Resources/GameFont SDF")]
        public TMP_FontAsset CustomFont;
        IntegratedGetaway game;
        IntegratedInput input;
        GameObject main, join, room, levels, settings, hud;
        TMP_InputField nameField, addressField, portField;
        TMP_Text status, heading, roomInstructions, resultSummary, settingsStatus, modeLabel, volumeLabel, sensitivityLabel, rangeLabel;
        TMP_Text hudStats, hudRole, hudHint, navigator, countdown, deviceWarning;
                GameObject turnBox;       // <-- Wadah kotak panah
        TMP_Text turnIndicator;   // <-- Teks simbol panah besar

        // --- BAR POLICE & BALANCE ---
        GameObject policeContainer, balanceContainer;
        Image policeBarFill, balanceBarFill;
        TMP_Text policePercentText, balancePercentText;

        Button ready, start, back, driver, shooter, solo, mainHost, mainClient, connect, keyboard, custom, invert, graphics, leaveSettings;
        // --- LOBBY ROLE CARDS ---
        TMP_Text driverCardName, driverCardStatus;
        TMP_Text shooterCardName, shooterCardStatus;
        GameObject hardware;
        Image damage;
        Image sirenOverlay;       // <-- Pantulan lampu sirene merah-biru
        TMP_Text emergencyAlert;  // <-- Teks darurat tengah layar
        Image[] hullBlocks; // untuk menampung kotak darah

        // --- PRIORITAS 1: SUB-TAHAP A FIELDS ---
        Image truckHealthFill;
        TMP_Text truckHealthPercentText, truckHealthStatusText;
        int lastObservedHull = -1;
        float damageFlashUntil;

        TMP_Text balanceStatusText, balanceHintText;

        GameObject navInstructionBox;
        TMP_Text navStatusBadge, navDirectionText, navDistanceText, navEstTimeText, navCommHintText;

        GameObject ammoBox, ammoReloadBarContainer;
        TMP_Text ammoMagText, ammoReserveText, ammoStatusBadgeText, ammoHintText;
        Image ammoReloadProgressFill;
        float reloadCompleteUntil;
        bool wasReloading;

        TMP_Text damageToastText;
        float damageToastUntil;

        TMP_FontAsset font;
        bool joining, built;
        float nextRefresh;
        // Palet Warna UI Profesional (Bebas Cyan Silau, Kontras Tinggi & Aman di Mata)
        static readonly Color TextPrimary = new Color32(248, 250, 252, 255); // Putih Bersih (#F8FAFC)
        static readonly Color TextMuted = new Color32(148, 163, 184, 255);   // Muted Slate (#94A3B8)
        static readonly Color AmberAccent = new Color32(245, 158, 11, 255);  // Amber Gold (#F59E0B)
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
            font = CustomFont != null ? CustomFont : Resources.Load<TMP_FontAsset>("GameFont SDF");
            if (font == null) font = main.GetComponentInChildren<TMP_Text>(true)?.font;
            foreach (var screen in new[] { main, join, room })
            {
                foreach (var existingText in screen.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (font != null) existingText.font = font;
                    // Amankan semua teks bawaan Canvas yang sebelumnya menggunakan warna cyan/biru muda cerah:
                    if (existingText.color.g > 0.8f && existingText.color.b > 0.85f && existingText.color.r < 0.65f)
                    {
                        existingText.color = TextPrimary;
                    }
                }
            }
            var mainBg = BackgroundSprite(main);
            foreach (var screen in new[] { main, join, room }) FullScreen(screen, mainBg);
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
            // --- 1. KARTU SLOT DRIVER (KIRI) ---
            driver = Existing(room, "driver", "DRIVER", new Vector2(-180, 85), () => Choose(CrewRole.Driver));
            Place((RectTransform)driver.transform, new Vector2(-180, 85), new Vector2(320, 130));
            var driverBtnText = driver.GetComponentInChildren<TMP_Text>();
            if (driverBtnText != null)
            {
                driverBtnText.alignment = TextAlignmentOptions.Top;
                driverBtnText.rectTransform.offsetMax = new Vector2(-12, -10);
            }
            driverCardName = Text(driver.transform, "DriverName", 18);
            Place(driverCardName.rectTransform, new Vector2(0, -8), new Vector2(290, 36));
            driverCardStatus = Text(driver.transform, "DriverStatus", 16);
            Place(driverCardStatus.rectTransform, new Vector2(0, -42), new Vector2(290, 28));

            // --- 2. KARTU SLOT SHOOTER (KANAN) ---
            shooter = Existing(room, "shooter", "SHOOTER", new Vector2(180, 85), () => Choose(CrewRole.Shooter));
            Place((RectTransform)shooter.transform, new Vector2(180, 85), new Vector2(320, 130));
            var shooterBtnText = shooter.GetComponentInChildren<TMP_Text>();
            if (shooterBtnText != null)
            {
                shooterBtnText.alignment = TextAlignmentOptions.Top;
                shooterBtnText.rectTransform.offsetMax = new Vector2(-12, -10);
            }
            shooterCardName = Text(shooter.transform, "ShooterName", 18);
            Place(shooterCardName.rectTransform, new Vector2(0, -8), new Vector2(290, 36));
            shooterCardStatus = Text(shooter.transform, "ShooterStatus", 16);
            Place(shooterCardStatus.rectTransform, new Vector2(0, -42), new Vector2(290, 28));

            foreach (var item in room.GetComponentsInChildren<RectTransform>(true))
                if (item.name == "roles" || item.name == "room" || item.name == "playerscroll" || item.name.StartsWith("level")) item.gameObject.SetActive(false);

            // --- 3. TOMBOL AKSI LOBBY DENGAN POSISI BARU ---
            ready = Action(room.transform, "READY", new Vector2(0, -20), () => IntegratedCrew.Local?.ToggleReady(), 380);
            start = Action(room.transform, "START SURVIVAL", new Vector2(0, -88), () =>
            {
                var session = IntegratedCrew.Session;
                if (session == null) return;
                if (session.State.Value.Phase == RoundPhase.Wrecked) session.BackToLobby(); else session.BeginRound();
            }, 380);
            Action(room.transform, "SETTINGS", new Vector2(-170, -165), () => game.OpenSettings(), 230);
            back = Action(room.transform, "LEAVE CREW", new Vector2(170, -165), () => game.Leave(), 230);
            var backGraphic = back.GetComponentInChildren<GetawayButtonGraphic>();
            if (backGraphic != null) { backGraphic.Outline = new Color(1f, 0.4f, 0.4f); backGraphic.SetVerticesDirty(); }
            var overlay = Canvas("Integration overlay", 100);
            status = Text(overlay.transform, "Session status", 19);
            Place(status.rectTransform, new Vector2(0, -315), new Vector2(1000, 60));
            settings = Screen(overlay.transform, "Settings", null);
            var setImg = settings.GetComponent<Image>();
            if (setImg != null)
            {
                setImg.color = new Color32(5, 8, 16, 215); // Backdrop Dimmer gelap (85% opacity) agar scene 3D tidak silau
                setImg.raycastTarget = true; // Tangkap klik agar tidak tembus ke game
            }
            BuildSettings();
            hud = new GameObject("Survival HUD", typeof(RectTransform)); hud.transform.SetParent(overlay.transform, false); Stretch((RectTransform)hud.transform);
            BuildHUD();
            settings.SetActive(false); hud.SetActive(false);
        }
        void FullScreen(GameObject screen, Sprite sharedBackground = null)
        {
            var canvas = screen.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = screen.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            var background = Find<Image>(screen, "Background");
            if (background != null)
            {
                if (sharedBackground != null) background.sprite = sharedBackground;
                background.color = Color.white;
                Stretch(background.rectTransform); background.preserveAspect = false; background.raycastTarget = false; background.transform.SetAsFirstSibling();
            }

            // Di dalam FullScreen(GameObject screen), tepat setelah mengatur background:
            var dimmer = new GameObject("Dimmer", typeof(RectTransform), typeof(Image));
            dimmer.transform.SetParent(screen.transform, false);
            Stretch((RectTransform)dimmer.transform);
            var dimImg = dimmer.GetComponent<Image>();
            dimImg.color = new Color32(0, 0, 0, 115); // Hitam dengan alpha 115 (antara 100-130)
            dimImg.raycastTarget = false;
            dimmer.transform.SetSiblingIndex(1); // Tepat di atas Background, di bawah teks & tombol
        }
        void BuildSettings()
        {
            Color32 modalBgColor = new Color32(15, 23, 42, 245);    // Slate-900 gelap elegan (#0F172A)
            Color32 slateLabel = new Color32(148, 163, 184, 255);    // Muted Slate (#94A3B8)
            Color32 crispWhite = new Color32(248, 250, 252, 255);    // Putih bersih tajam (#F8FAFC)
            Color32 amberAccent = new Color32(245, 158, 11, 255);    // Amber gold (#F59E0B)

            // Modal Card Panel di tengah layar
            var modalBox = new GameObject("SettingsModal", typeof(RectTransform), typeof(Image));
            modalBox.transform.SetParent(settings.transform, false);
            Place((RectTransform)modalBox.transform, Vector2.zero, new Vector2(940, 560));
            var mbImg = modalBox.GetComponent<Image>();
            mbImg.color = modalBgColor;
            mbImg.raycastTarget = false;

            // Border outline tipis modal card
            var borderGO = new GameObject("Border", typeof(RectTransform), typeof(GetawayButtonGraphic));
            borderGO.transform.SetParent(modalBox.transform, false);
            Stretch((RectTransform)borderGO.transform);
            var bGraphic = borderGO.GetComponent<GetawayButtonGraphic>();
            bGraphic.Top = bGraphic.Bottom = Color.clear;
            bGraphic.Outline = new Color32(51, 65, 85, 200); // Slate-700
            bGraphic.raycastTarget = false;

            // Header Judul & Subtitle
            var title = Text(modalBox.transform, "CREW SETTINGS", 26);
            Place(title.rectTransform, new Vector2(0, 230), new Vector2(600, 36));
            title.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            title.color = crispWhite;

            var subtitle = Text(modalBox.transform, "CUSTOM HARDWARE IS THE PRIMARY CONTROLLER  |  ESC TO RETURN", 12);
            Place(subtitle.rectTransform, new Vector2(0, 200), new Vector2(750, 24));
            subtitle.color = slateLabel;

            // Mode Selector Buttons
            keyboard = Action(modalBox.transform, "KEYBOARD / MOUSE", new Vector2(-190, 140), () => input.ChangeMode(false), 340);
            custom = Action(modalBox.transform, "CUSTOM CONTROLLER", new Vector2(190, 140), () => input.ChangeMode(true), 340);

            modeLabel = Text(modalBox.transform, "Controller mode", 13);
            Place(modeLabel.rectTransform, new Vector2(0, 92), new Vector2(880, 24));
            modeLabel.color = new Color32(203, 213, 225, 255); // Slate-300

            // Audio & Steering Sensitivity Section
            volumeLabel = Text(modalBox.transform, "Volume", 14);
            Place(volumeLabel.rectTransform, new Vector2(-190, 48), new Vector2(340, 24));
            volumeLabel.fontStyle = FontStyles.Bold;
            volumeLabel.color = crispWhite;
            MakeSlider(modalBox.transform, new Vector2(-190, 20), 0, 1, AudioListener.volume, v => AudioListener.volume = v);

            sensitivityLabel = Text(modalBox.transform, "Sensitivity", 14);
            Place(sensitivityLabel.rectTransform, new Vector2(190, 48), new Vector2(340, 24));
            sensitivityLabel.fontStyle = FontStyles.Bold;
            sensitivityLabel.color = crispWhite;
            MakeSlider(modalBox.transform, new Vector2(190, 20), .2f, 2, input != null ? input.SteeringSensitivity : 1, v => input.SteeringSensitivity = v);

            // Hardware Controls Sub-container
            hardware = new GameObject("Hardware controls", typeof(RectTransform));
            hardware.transform.SetParent(modalBox.transform, false);
            Stretch((RectTransform)hardware.transform);

            portField = Instantiate(nameField, hardware.transform);
            portField.name = "Serial port";
            StyleField(portField, new Vector2(-190, -50), "COM3 or /dev/ttyACM0");
            portField.SetTextWithoutNotify(input != null ? input.Port : "");
            portField.onValueChanged.RemoveAllListeners();
            portField.onValueChanged.AddListener(v => input.Port = v);

            Action(hardware.transform, "CONNECT DEVICE", new Vector2(190, -50), () => { input.Port = portField.text; input.ConnectDevice(); }, 340);

            rangeLabel = Text(hardware.transform, "Steering range", 14);
            Place(rangeLabel.rectTransform, new Vector2(-190, -105), new Vector2(340, 24));
            rangeLabel.fontStyle = FontStyles.Bold;
            rangeLabel.color = crispWhite;
            MakeSlider(hardware.transform, new Vector2(-190, -135), .1f, 6.3f, input != null ? input.SteeringFullScale : 1.57f, v => input.SteeringFullScale = v);

            invert = Action(hardware.transform, "INVERT STEERING: OFF", new Vector2(190, -118), () => input.InvertSteering = !input.InvertSteering, 340);
            Action(hardware.transform, "CALIBRATE / C", new Vector2(-190, -190), () => input.Calibrate(), 340);

            // Graphics button
            graphics = Action(modalBox.transform, "GRAPHICS", new Vector2(190, -190), () =>
            {
                if (DowntownRendering.Instance != null) { DowntownRendering.Instance.Cycle(); return; }
                if (QualitySettings.names.Length > 0) QualitySettings.SetQualityLevel((QualitySettings.GetQualityLevel() + 1) % QualitySettings.names.Length, false);
            }, 340);

            // Footer Status & Hints
            settingsStatus = Text(modalBox.transform, "Controller status", 12);
            Place(settingsStatus.rectTransform, new Vector2(0, -242), new Vector2(880, 42));
            settingsStatus.color = slateLabel;

            // Tombol Navigasi Modal (Back & Leave)
            var resume = Action(modalBox.transform, "BACK / ESC", new Vector2(380, 230), () => game.OpenSettings(), 140);
            Place((RectTransform)resume.transform, new Vector2(380, 230), new Vector2(130, 40));
            resume.GetComponentInChildren<TMP_Text>().fontSize = 14;

            leaveSettings = Action(modalBox.transform, "LEAVE SESSION", new Vector2(-380, 230), () => game.Leave(), 140);
            Place((RectTransform)leaveSettings.transform, new Vector2(-380, 230), new Vector2(130, 40));
            var lsText = leaveSettings.GetComponentInChildren<TMP_Text>();
            lsText.fontSize = 14;
            var lsGraphic = leaveSettings.GetComponentInChildren<GetawayButtonGraphic>();
            if (lsGraphic != null)
            {
                lsGraphic.Outline = new Color32(239, 68, 68, 200); // Soft red outline
                lsGraphic.SetVerticesDirty();
            }
        }
        void BuildHUD()
        {
            Color32 hudPanelColor = new Color32(15, 23, 42, 232);   // Slate-900 gelap (#0F172A)
            Color32 hudTrackColor = new Color32(30, 41, 59, 240);   // Slate-800 (#1E293B)
            Color32 amberAccent = new Color32(245, 158, 11, 255);   // Kuning/Oranye (#F59E0B)
            Color32 slateLabel = new Color32(148, 163, 184, 255);   // Abu-biru bersih (#94A3B8)
            Color32 crispWhite = new Color32(248, 250, 252, 255);   // Putih tajam (#F8FAFC)

            var panel = new GameObject("Instruments", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(hud.transform, false);
            var rect = (RectTransform)panel.transform; rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1); rect.sizeDelta = new Vector2(-40, 88); rect.anchoredPosition = new Vector2(0, -16);
            panel.GetComponent<Image>().color = hudPanelColor; panel.GetComponent<Image>().raycastTarget = false;

            // =========================================================================
            // REVISI PRIORITAS 1 - SUB-TAHAP A: BUILD HUD (TIPOGRAFI & TEMA BIRU TUA)
            // =========================================================================

            // --- 1. TRUCK HEALTH (LABEL, FILL BAR, PERSENTASE, STATUS) ---
            var healthLabel = Text(panel.transform, "TRUCK HEALTH", 13);
            var hlRect = healthLabel.rectTransform;
            hlRect.anchorMin = hlRect.anchorMax = hlRect.pivot = new Vector2(0, 1);
            hlRect.anchoredPosition = new Vector2(20, -10);
            hlRect.sizeDelta = new Vector2(125, 20);
            healthLabel.alignment = TextAlignmentOptions.Left;
            healthLabel.fontStyle = FontStyles.Bold;
            healthLabel.characterSpacing = 1.5f;
            healthLabel.color = amberAccent;

            truckHealthPercentText = Text(panel.transform, "100%", 15);
            var thpRect = truckHealthPercentText.rectTransform;
            thpRect.anchorMin = thpRect.anchorMax = thpRect.pivot = new Vector2(0, 1);
            thpRect.anchoredPosition = new Vector2(145, -9);
            thpRect.sizeDelta = new Vector2(50, 20);
            truckHealthPercentText.alignment = TextAlignmentOptions.Left;
            truckHealthPercentText.fontStyle = FontStyles.Bold;
            truckHealthPercentText.color = crispWhite;

            truckHealthStatusText = Text(panel.transform, "HEALTHY", 12);
            var thsRect = truckHealthStatusText.rectTransform;
            thsRect.anchorMin = thsRect.anchorMax = thsRect.pivot = new Vector2(0, 1);
            thsRect.anchoredPosition = new Vector2(198, -10);
            thsRect.sizeDelta = new Vector2(85, 20);
            truckHealthStatusText.alignment = TextAlignmentOptions.Left;
            truckHealthStatusText.fontStyle = FontStyles.Bold;
            truckHealthStatusText.characterSpacing = 1f;

            // Health Bar Background & Fill
            var healthBg = new GameObject("HealthBarBg", typeof(RectTransform), typeof(Image));
            healthBg.transform.SetParent(panel.transform, false);
            var hbgRect = (RectTransform)healthBg.transform;
            hbgRect.anchorMin = hbgRect.anchorMax = hbgRect.pivot = new Vector2(0, 1);
            hbgRect.anchoredPosition = new Vector2(20, -32);
            hbgRect.sizeDelta = new Vector2(245, 12);
            healthBg.GetComponent<Image>().color = hudTrackColor;

            var healthFillGO = new GameObject("HealthFill", typeof(RectTransform), typeof(Image));
            healthFillGO.transform.SetParent(healthBg.transform, false);
            var hfRect = (RectTransform)healthFillGO.transform;
            hfRect.anchorMin = new Vector2(0, 0); hfRect.anchorMax = new Vector2(0, 1); hfRect.pivot = new Vector2(0, 0.5f);
            hfRect.anchoredPosition = Vector2.zero;
            hfRect.sizeDelta = new Vector2(245, 0);
            truckHealthFill = healthFillGO.GetComponent<Image>();
            truckHealthFill.color = new Color32(56, 217, 133, 255);

            // --- 2. STATS INSTRUMEN (DISTANCE, SCORE, SPEED) ---
            hudStats = Text(panel.transform, "Distance and score", 22);
            Stretch(hudStats.rectTransform);
            hudStats.rectTransform.offsetMin = new Vector2(290, 36);
            hudStats.rectTransform.offsetMax = new Vector2(-240, -6);
            hudStats.alignment = TextAlignmentOptions.Left;
            hudStats.fontStyle = FontStyles.Bold;
            hudStats.characterSpacing = 1f;
            hudStats.color = crispWhite;

            // --- POLICE PRESSURE GAUGE (KANAN ATAS) ---
            policeContainer = new GameObject("PoliceGauge", typeof(RectTransform));
            policeContainer.transform.SetParent(panel.transform, false);
            var pgRect = (RectTransform)policeContainer.transform;
            pgRect.anchorMin = pgRect.anchorMax = pgRect.pivot = new Vector2(1, 1);
            pgRect.anchoredPosition = new Vector2(-20, -10);
            pgRect.sizeDelta = new Vector2(215, 26);

            var pLabel = Text(policeContainer.transform, "POLICE", 13);
            var plRect = pLabel.rectTransform;
            plRect.anchorMin = plRect.anchorMax = plRect.pivot = new Vector2(0, 0.5f);
            plRect.anchoredPosition = new Vector2(0, 0);
            plRect.sizeDelta = new Vector2(62, 24);
            pLabel.fontStyle = FontStyles.Bold;
            pLabel.characterSpacing = 1.5f;
            pLabel.color = amberAccent;

            var pBg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            pBg.transform.SetParent(policeContainer.transform, false);
            var pbgRect = (RectTransform)pBg.transform;
            pbgRect.anchorMin = pbgRect.anchorMax = pbgRect.pivot = new Vector2(0, 0.5f);
            pbgRect.anchoredPosition = new Vector2(65, 0);
            pbgRect.sizeDelta = new Vector2(95, 14);
            pBg.GetComponent<Image>().color = hudTrackColor;

            var pFill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            pFill.transform.SetParent(pBg.transform, false);
            var pfRect = (RectTransform)pFill.transform;
            pfRect.anchorMin = new Vector2(0, 0); pfRect.anchorMax = new Vector2(0, 1); pfRect.pivot = new Vector2(0, 0.5f);
            pfRect.anchoredPosition = Vector2.zero;
            pfRect.sizeDelta = new Vector2(0, 0);
            policeBarFill = pFill.GetComponent<Image>();
            policeBarFill.color = new Color(1f, 0.2f, 0.45f);

            policePercentText = Text(policeContainer.transform, "0%", 15);
            var pptRect = policePercentText.rectTransform;
            pptRect.anchorMin = pptRect.anchorMax = pptRect.pivot = new Vector2(0, 0.5f);
            pptRect.anchoredPosition = new Vector2(165, 0);
            pptRect.sizeDelta = new Vector2(48, 20);
            policePercentText.fontStyle = FontStyles.Bold;
            policePercentText.color = crispWhite;

            hudRole = Text(panel.transform, "Role", 14); Stretch(hudRole.rectTransform); hudRole.rectTransform.offsetMin = new Vector2(20, 6); hudRole.rectTransform.offsetMax = new Vector2(-18, -48); hudRole.alignment = TextAlignmentOptions.Left;
            hudRole.fontStyle = FontStyles.Bold;
            hudRole.characterSpacing = 1.2f;
            hudRole.color = slateLabel;

            // --- 3. PANEL NAVIGATION INSTRUCTION (PROMINENT SHOOTER HUD) ---
            navInstructionBox = new GameObject("NavInstructionPanel", typeof(RectTransform), typeof(Image));
            navInstructionBox.transform.SetParent(hud.transform, false);
            var nibRect = (RectTransform)navInstructionBox.transform;
            nibRect.anchorMin = nibRect.anchorMax = nibRect.pivot = new Vector2(0, 1);
            nibRect.anchoredPosition = new Vector2(25, -112);
            nibRect.sizeDelta = new Vector2(330, 140);
            navInstructionBox.GetComponent<Image>().color = hudPanelColor;
            navInstructionBox.GetComponent<Image>().raycastTarget = false;

            var navTitle = Text(navInstructionBox.transform, "NAVIGATION INSTRUCTION", 12);
            var ntRect = navTitle.rectTransform;
            ntRect.anchorMin = ntRect.anchorMax = ntRect.pivot = new Vector2(0, 1);
            ntRect.anchoredPosition = new Vector2(12, -8);
            ntRect.sizeDelta = new Vector2(195, 20);
            navTitle.alignment = TextAlignmentOptions.Left;
            navTitle.fontStyle = FontStyles.Bold;
            navTitle.characterSpacing = 1.2f;
            navTitle.color = amberAccent;

            navStatusBadge = Text(navInstructionBox.transform, "[ UPCOMING ]", 13);
            var nsbRect = navStatusBadge.rectTransform;
            nsbRect.anchorMin = nsbRect.anchorMax = nsbRect.pivot = new Vector2(1, 1);
            nsbRect.anchoredPosition = new Vector2(-12, -8);
            nsbRect.sizeDelta = new Vector2(120, 20);
            navStatusBadge.alignment = TextAlignmentOptions.Right;
            navStatusBadge.fontStyle = FontStyles.Bold;

            navDirectionText = Text(navInstructionBox.transform, "<<<  TURN LEFT", 25);
            Place(navDirectionText.rectTransform, new Vector2(0, 18), new Vector2(310, 32));
            navDirectionText.fontStyle = FontStyles.Bold;
            navDirectionText.characterSpacing = 1.5f;
            navDirectionText.alignment = TextAlignmentOptions.Center;

            navDistanceText = Text(navInstructionBox.transform, "201 m", 21);
            Place(navDistanceText.rectTransform, new Vector2(0, -12), new Vector2(310, 26));
            navDistanceText.fontStyle = FontStyles.Bold;
            navDistanceText.alignment = TextAlignmentOptions.Center;
            navDistanceText.color = crispWhite;

            navEstTimeText = Text(navInstructionBox.transform, "EST. 9.0 SECONDS", 13);
            Place(navEstTimeText.rectTransform, new Vector2(0, -36), new Vector2(310, 20));
            navEstTimeText.alignment = TextAlignmentOptions.Center;
            navEstTimeText.fontStyle = FontStyles.Bold;
            navEstTimeText.color = new Color32(203, 213, 225, 255);

            navCommHintText = Text(navInstructionBox.transform, "Communicate with your Driver!", 11);
            Place(navCommHintText.rectTransform, new Vector2(0, -56), new Vector2(310, 18));
            navCommHintText.alignment = TextAlignmentOptions.Center;
            navCommHintText.color = slateLabel;

            // --- 4. PANEL CREW BALANCE (PROMINENT SHOOTER HUD) ---
            balanceContainer = new GameObject("BalancePanel", typeof(RectTransform), typeof(Image));
            balanceContainer.transform.SetParent(hud.transform, false);
            var bcRect = (RectTransform)balanceContainer.transform;
            bcRect.anchorMin = bcRect.anchorMax = bcRect.pivot = new Vector2(0, 1);
            bcRect.anchoredPosition = new Vector2(365, -112);
            bcRect.sizeDelta = new Vector2(270, 140);
            balanceContainer.GetComponent<Image>().color = hudPanelColor;
            balanceContainer.GetComponent<Image>().raycastTarget = false;

            var balTitle = Text(balanceContainer.transform, "CREW BALANCE", 12);
            var btRect = balTitle.rectTransform;
            btRect.anchorMin = btRect.anchorMax = btRect.pivot = new Vector2(0, 1);
            btRect.anchoredPosition = new Vector2(12, -8);
            btRect.sizeDelta = new Vector2(140, 20);
            balTitle.alignment = TextAlignmentOptions.Left;
            balTitle.fontStyle = FontStyles.Bold;
            balTitle.characterSpacing = 1.2f;
            balTitle.color = amberAccent;

            balanceStatusText = Text(balanceContainer.transform, "[ STABLE ]", 13);
            var bstRect = balanceStatusText.rectTransform;
            bstRect.anchorMin = bstRect.anchorMax = bstRect.pivot = new Vector2(1, 1);
            bstRect.anchoredPosition = new Vector2(-12, -8);
            bstRect.sizeDelta = new Vector2(110, 20);
            balanceStatusText.alignment = TextAlignmentOptions.Right;
            balanceStatusText.fontStyle = FontStyles.Bold;

            balancePercentText = Text(balanceContainer.transform, "100%", 28);
            Place(balancePercentText.rectTransform, new Vector2(0, 12), new Vector2(240, 36));
            balancePercentText.fontStyle = FontStyles.Bold;
            balancePercentText.alignment = TextAlignmentOptions.Center;
            balancePercentText.color = crispWhite;

            var bBg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            bBg.transform.SetParent(balanceContainer.transform, false);
            var bbgRect = (RectTransform)bBg.transform;
            bbgRect.anchorMin = bbgRect.anchorMax = bbgRect.pivot = new Vector2(0.5f, 0);
            bbgRect.anchoredPosition = new Vector2(0, 42);
            bbgRect.sizeDelta = new Vector2(230, 14);
            bBg.GetComponent<Image>().color = hudTrackColor;

            var bFill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            bFill.transform.SetParent(bBg.transform, false);
            var bfRect = (RectTransform)bFill.transform;
            bfRect.anchorMin = new Vector2(0, 0); bfRect.anchorMax = new Vector2(0, 1); bfRect.pivot = new Vector2(0, 0.5f);
            bfRect.anchoredPosition = Vector2.zero;
            bfRect.sizeDelta = new Vector2(230, 0);
            balanceBarFill = bFill.GetComponent<Image>();
            balanceBarFill.color = new Color32(56, 217, 133, 255);

            balanceHintText = Text(balanceContainer.transform, "Maintain your balance!", 12);
            var bhRect = balanceHintText.rectTransform;
            bhRect.anchorMin = bhRect.anchorMax = bhRect.pivot = new Vector2(0.5f, 0);
            bhRect.anchoredPosition = new Vector2(0, 12);
            bhRect.sizeDelta = new Vector2(240, 20);
            balanceHintText.alignment = TextAlignmentOptions.Center;
            balanceHintText.color = slateLabel;

            // --- 5. PANEL AMMUNITION & RELOAD (PROMINENT SHOOTER HUD) ---
            ammoBox = new GameObject("AmmoPanel", typeof(RectTransform), typeof(Image));
            ammoBox.transform.SetParent(hud.transform, false);
            var abRect = (RectTransform)ammoBox.transform;
            abRect.anchorMin = abRect.anchorMax = abRect.pivot = new Vector2(0, 1);
            abRect.anchoredPosition = new Vector2(25, -260);
            abRect.sizeDelta = new Vector2(330, 105);
            ammoBox.GetComponent<Image>().color = hudPanelColor;
            ammoBox.GetComponent<Image>().raycastTarget = false;

            var ammoTitle = Text(ammoBox.transform, "AMMUNITION", 12);
            var atRect = ammoTitle.rectTransform;
            atRect.anchorMin = atRect.anchorMax = atRect.pivot = new Vector2(0, 1);
            atRect.anchoredPosition = new Vector2(12, -8);
            atRect.sizeDelta = new Vector2(130, 20);
            ammoTitle.alignment = TextAlignmentOptions.Left;
            ammoTitle.fontStyle = FontStyles.Bold;
            ammoTitle.characterSpacing = 1.2f;
            ammoTitle.color = amberAccent;

            ammoStatusBadgeText = Text(ammoBox.transform, "READY", 13);
            var asbRect = ammoStatusBadgeText.rectTransform;
            asbRect.anchorMin = asbRect.anchorMax = asbRect.pivot = new Vector2(1, 1);
            asbRect.anchoredPosition = new Vector2(-12, -8);
            asbRect.sizeDelta = new Vector2(160, 20);
            ammoStatusBadgeText.alignment = TextAlignmentOptions.Right;
            ammoStatusBadgeText.fontStyle = FontStyles.Bold;

            ammoMagText = Text(ammoBox.transform, "12", 30);
            var amtRect = ammoMagText.rectTransform;
            amtRect.anchorMin = amtRect.anchorMax = amtRect.pivot = new Vector2(0, 1);
            amtRect.anchoredPosition = new Vector2(25, -42);
            amtRect.sizeDelta = new Vector2(60, 34);
            ammoMagText.alignment = TextAlignmentOptions.Right;
            ammoMagText.fontStyle = FontStyles.Bold;
            ammoMagText.color = crispWhite;

            ammoReserveText = Text(ammoBox.transform, "/ 72  RESERVE", 16);
            var artRect = ammoReserveText.rectTransform;
            artRect.anchorMin = artRect.anchorMax = artRect.pivot = new Vector2(0, 1);
            artRect.anchoredPosition = new Vector2(95, -44);
            artRect.sizeDelta = new Vector2(140, 30);
            ammoReserveText.alignment = TextAlignmentOptions.Left;
            ammoReserveText.fontStyle = FontStyles.Bold;
            ammoReserveText.color = slateLabel;

            ammoHintText = Text(ammoBox.transform, "[R] RELOAD", 12);
            var ahRect = ammoHintText.rectTransform;
            ahRect.anchorMin = ahRect.anchorMax = ahRect.pivot = new Vector2(1, 1);
            ahRect.anchoredPosition = new Vector2(-12, -44);
            ahRect.sizeDelta = new Vector2(85, 24);
            ammoHintText.alignment = TextAlignmentOptions.Right;
            ammoHintText.fontStyle = FontStyles.Bold;
            ammoHintText.color = amberAccent;

            // Reload progress bar container
            ammoReloadBarContainer = new GameObject("ReloadBarContainer", typeof(RectTransform), typeof(Image));
            ammoReloadBarContainer.transform.SetParent(ammoBox.transform, false);
            var rbcRect = (RectTransform)ammoReloadBarContainer.transform;
            rbcRect.anchorMin = rbcRect.anchorMax = rbcRect.pivot = new Vector2(0.5f, 0);
            rbcRect.anchoredPosition = new Vector2(0, 12);
            rbcRect.sizeDelta = new Vector2(300, 8);
            ammoReloadBarContainer.GetComponent<Image>().color = hudTrackColor;

            var rFillGO = new GameObject("ReloadFill", typeof(RectTransform), typeof(Image));
            rFillGO.transform.SetParent(ammoReloadBarContainer.transform, false);
            var rfRect = (RectTransform)rFillGO.transform;
            rfRect.anchorMin = new Vector2(0, 0); rfRect.anchorMax = new Vector2(0, 1); rfRect.pivot = new Vector2(0, 0.5f);
            rfRect.anchoredPosition = Vector2.zero;
            rfRect.sizeDelta = new Vector2(300, 0);
            ammoReloadProgressFill = rFillGO.GetComponent<Image>();
            ammoReloadProgressFill.color = amberAccent;
            ammoReloadBarContainer.SetActive(false);

            // --- 6. ACTION FEEDBACK: DAMAGE TOAST ---
            var toastBox = new GameObject("DamageToastBox", typeof(RectTransform), typeof(Image));
            toastBox.transform.SetParent(hud.transform, false);
            var tbRect = (RectTransform)toastBox.transform;
            tbRect.anchorMin = tbRect.anchorMax = tbRect.pivot = new Vector2(0, 1);
            tbRect.anchoredPosition = new Vector2(650, -112);
            tbRect.sizeDelta = new Vector2(340, 60);
            var tbImg = toastBox.GetComponent<Image>();
            tbImg.color = new Color32(69, 10, 10, 235);
            tbImg.raycastTarget = false;

            damageToastText = Text(toastBox.transform, "DamageToast", 15);
            Stretch(damageToastText.rectTransform);
            damageToastText.fontStyle = FontStyles.Bold;
            damageToastText.alignment = TextAlignmentOptions.Center;
            toastBox.SetActive(false);

            // Standby elements for backward compatibility
            navigator = Text(hud.transform, "NavigatorHidden", 1); navigator.gameObject.SetActive(false);
            turnBox = new GameObject("TurnBoxStub", typeof(RectTransform)); turnBox.transform.SetParent(hud.transform, false); turnBox.SetActive(false);

            // Hint bawah dengan bar latar tipis supaya terbaca jelas di atas dashboard & aspal
            var hintBg = new GameObject("HintBg", typeof(RectTransform), typeof(Image));
            hintBg.transform.SetParent(hud.transform, false);
            var hbgBottom = (RectTransform)hintBg.transform;
            hbgBottom.anchorMin = new Vector2(.5f, 0); hbgBottom.anchorMax = new Vector2(.5f, 0); hbgBottom.pivot = new Vector2(.5f, 0);
            hbgBottom.sizeDelta = new Vector2(760, 32); hbgBottom.anchoredPosition = new Vector2(0, 58);
            hintBg.GetComponent<Image>().color = new Color32(15, 23, 42, 185);
            hintBg.GetComponent<Image>().raycastTarget = false;

            hudHint = Text(hud.transform, "Controls", 15); var hint = hudHint.rectTransform; hint.anchorMin = new Vector2(0, 0); hint.anchorMax = new Vector2(1, 0); hint.pivot = new Vector2(.5f, 0); hint.sizeDelta = new Vector2(-50, 32); hint.anchoredPosition = new Vector2(0, 58);
            hudHint.fontStyle = FontStyles.Bold;
            hudHint.characterSpacing = 0.8f;
            hudHint.color = crispWhite;

            countdown = Text(hud.transform, "Countdown", 44); Place(countdown.rectTransform, Vector2.zero, new Vector2(650, 90));
            countdown.fontStyle = FontStyles.Bold;
            countdown.characterSpacing = 3f;
            countdown.color = amberAccent;
            deviceWarning = Text(hud.transform, "Device warning", 18); var warning = deviceWarning.rectTransform; warning.anchorMin = new Vector2(0, 0); warning.anchorMax = new Vector2(1, 0); warning.pivot = new Vector2(.5f, 0); warning.sizeDelta = new Vector2(-40, 65); warning.anchoredPosition = new Vector2(0, 15);
            var flash = new GameObject("Damage feedback", typeof(RectTransform), typeof(Image)); flash.transform.SetParent(hud.transform, false); damage = flash.GetComponent<Image>(); Stretch(damage.rectTransform); damage.raycastTarget = false; damage.color = Color.clear;

            // --- OVERLAY LAMPU SIRENE POLISI (MERAH-BIRU) ---
            var sirenGO = new GameObject("Siren Overlay", typeof(RectTransform), typeof(Image));
            sirenGO.transform.SetParent(hud.transform, false);
            sirenOverlay = sirenGO.GetComponent<Image>();
            Stretch(sirenOverlay.rectTransform);
            sirenOverlay.raycastTarget = false;
            sirenOverlay.color = Color.clear;

            // --- TEKS PERINGATAN DARURAT TENGAH LAYAR ---
            emergencyAlert = Text(hud.transform, "Emergency Alert", 28);
            Place(emergencyAlert.rectTransform, new Vector2(0, 0), new Vector2(850, 70));
            emergencyAlert.fontStyle = FontStyles.Bold;
            emergencyAlert.gameObject.SetActive(false);
        
        }
        void MakeSlider(Transform parent, Vector2 position, float min, float max, float value, UnityAction<float> changed)
        {
            var go = new GameObject("Slider", typeof(RectTransform), typeof(Image), typeof(Slider)); go.transform.SetParent(parent, false); Place((RectTransform)go.transform, position, new Vector2(350, 22));
            go.GetComponent<Image>().color = new Color32(30, 41, 59, 255); // Flat dark blue (#1E293B)
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)); handle.transform.SetParent(go.transform, false); var r = (RectTransform)handle.transform; r.sizeDelta = new Vector2(20, 30); handle.GetComponent<Image>().color = new Color32(245, 158, 11, 255); // Amber accent (#F59E0B)
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
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false); Stretch((RectTransform)go.transform); go.GetComponent<Image>().sprite = sprite;
            var dimmer = new GameObject("Dimmer", typeof(RectTransform), typeof(Image));
            dimmer.transform.SetParent(go.transform, false);
            Stretch((RectTransform)dimmer.transform);
            var dimImg = dimmer.GetComponent<Image>();
            dimImg.color = new Color32(0, 0, 0, 120);
            dimImg.raycastTarget = false;
            return go;
        }
        static Sprite BackgroundSprite(GameObject root) => Find<Image>(root, "Background")?.sprite;
        TMP_Text Title(Transform parent, string title, string subtitle)
        {
            // Teks bayangan halus (Drop Shadow) tepat di belakang judul utama (kompatibel penuh dengan TextMeshPro)
            var shadowLabel = Text(parent, title + " Shadow", 46);
            var sRect = shadowLabel.rectTransform;
            sRect.anchorMin = new Vector2(0, 1); sRect.anchorMax = Vector2.one; sRect.pivot = new Vector2(.5f, 1);
            sRect.sizeDelta = new Vector2(-60, 60); sRect.anchoredPosition = new Vector2(3f, -72f);
            shadowLabel.text = title;
            shadowLabel.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            shadowLabel.characterSpacing = 4f;
            shadowLabel.color = new Color32(0, 0, 0, 190);

            var label = Text(parent, title, 46);
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
            rect.sizeDelta = new Vector2(-60, 60); rect.anchoredPosition = new Vector2(0, -68);
            label.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            label.characterSpacing = 4f; // Spasi huruf tegas ala judul game action
            label.color = Color.white;

            var sub = Text(parent, subtitle, 17);
            var r = sub.rectTransform;
            r.anchorMin = new Vector2(0, 1); r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, 1);
            r.sizeDelta = new Vector2(-60, 36); r.anchoredPosition = new Vector2(0, -128);
            sub.fontStyle = FontStyles.Bold;
            sub.color = new Color32(245, 158, 11, 255); // Aksen kuning/oranye (#F59E0B)
            return label;
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

            // Warna Flat Biru Tua (#1E293B) dengan pembeda Tombol Utama (Aksen Kuning/Oranye #F59E0B) vs Tombol Sekunder
            Color32 darkSlate = new Color32(30, 41, 59, 255);     // #1E293B
            Color32 amberAccent = new Color32(245, 158, 11, 255); // #F59E0B
            Color32 slateBorder = new Color32(71, 85, 105, 255);  // #475569
            Color32 lightText = new Color32(248, 250, 252, 255);  // #F8FAFC

            bool isPrimary = label.Contains("HOST") || label.Contains("CONNECT") || label.Contains("START") || label.Contains("READY");
            graphic.Top = darkSlate;
            graphic.Bottom = darkSlate;
            graphic.Outline = isPrimary ? (Color)amberAccent : (Color)slateBorder;
            graphic.SetVerticesDirty();

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f);
            colors.pressedColor = new Color(.78f, .78f, .85f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(.45f, .45f, .5f, .6f);
            button.colors = colors;

            var text = button.GetComponentInChildren<TMP_Text>(true);
            if (text == null) { var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(button.transform, false); text = go.GetComponent<TextMeshProUGUI>(); }
            Stretch(text.rectTransform); text.rectTransform.offsetMin = new Vector2(12, 4); text.rectTransform.offsetMax = new Vector2(-12, -4);
            var sourceText = style != null ? style.GetComponentInChildren<TMP_Text>(true) : null;
            if (sourceText != null && sourceText.font != null) text.font = sourceText.font;
            text.text = label;
            text.color = isPrimary ? (Color)amberAccent : (Color)lightText;
            text.fontSize = 22; text.fontStyle = FontStyles.Bold; text.alignment = TextAlignmentOptions.Center; text.enableAutoSizing = false; text.raycastTarget = false;
        }
        static void SetLabel(Button button, string text) { button.GetComponentInChildren<TMP_Text>(true).text = text; }
        TMP_Text Text(Transform parent, string name, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); var text = go.GetComponent<TextMeshProUGUI>();
            text.font = font; text.text = name; text.color = TextPrimary; text.fontSize = size; text.enableAutoSizing = false; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; return text;
        }
        static void StyleField(TMP_InputField field, Vector2 position, string placeholder)
        {
            Place((RectTransform)field.transform, position, new Vector2(370, 50));
            var bg = field.GetComponent<Image>();
            if (bg != null) bg.color = new Color32(15, 23, 42, 230); // Slate-900 gelap rapi
            field.textComponent.fontSize = 22; field.textComponent.color = Color.white; field.textComponent.enableAutoSizing = false;
            if (field.placeholder is TMP_Text label) { label.text = placeholder; label.fontSize = 20; label.color = new Color(.65f, .74f, .84f); }
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

            // Cari siapa yang menempati slot Driver dan Shooter:
            IntegratedCrew driverMember = null;
            IntegratedCrew shooterMember = null;
            foreach (var member in IntegratedCrew.Members)
            {
                if (!member.IsSpawned) continue;
                if (member.Role.Value == CrewRole.Driver) driverMember = member;
                if (member.Role.Value == CrewRole.Shooter) shooterMember = member;
            }

            // Kunci kartu jika sudah diambil oleh partner:
            if (driverMember != null && driverMember != local) driver.interactable = false;
            if (shooterMember != null && shooterMember != local) shooter.interactable = false;

            // Update Tampilan Kartu Driver:
            bool isLocalDriver = local != null && local.Role.Value == CrewRole.Driver;
            SetLabel(driver, isLocalDriver ? "DRIVER (YOUR ROLE)" : "DRIVER");
            if (driverCardName != null && driverCardStatus != null)
            {
                if (driverMember == null)
                {
                    driverCardName.text = "<color=#8888aa>SLOT OPEN\n<size=15>Click to select Driver</size></color>";
                    driverCardStatus.text = "<color=#666677>[ VACANT ]</color>";
                }
                else
                {
                    string tag = (driverMember == local) ? " <color=#f59e0b><b>(YOU)</b></color>" : "";
                    driverCardName.text = $"Callsign: <b>{driverMember.DisplayName.Value}</b>{tag}";
                    driverCardStatus.text = driverMember.Ready.Value
                        ? "<color=#33ff77><b>[ READY ]</b></color>"
                        : "<color=#ffcc33><b>[ WAITING... ]</b></color>";
                }
            }

            // Update Tampilan Kartu Shooter:
            bool isLocalShooter = local != null && local.Role.Value == CrewRole.Shooter;
            SetLabel(shooter, isLocalShooter ? "SHOOTER (YOUR ROLE)" : "SHOOTER / NAVIGATOR");
            if (shooterCardName != null && shooterCardStatus != null)
            {
                if (shooterMember == null)
                {
                    shooterCardName.text = "<color=#8888aa>SLOT OPEN\n<size=15>Click to select Shooter</size></color>";
                    shooterCardStatus.text = "<color=#666677>[ VACANT ]</color>";
                }
                else
                {
                    string tag = (shooterMember == local) ? " <color=#f59e0b><b>(YOU)</b></color>" : "";
                    shooterCardName.text = $"Callsign: <b>{shooterMember.DisplayName.Value}</b>{tag}";
                    shooterCardStatus.text = shooterMember.Ready.Value
                        ? "<color=#33ff77><b>[ READY ]</b></color>"
                        : "<color=#ffcc33><b>[ WAITING... ]</b></color>";
                }
            }

            // --- HIRARKI CTA: TOMBOL READY ---
            bool isReady = local != null && local.Ready.Value;
            ready.interactable = connected && lobby && local != null && local.Role.Value != CrewRole.None;
            SetLabel(ready, isReady ? "[ READY ] (CLICK TO CANCEL)" : "CONFIRM READY");
            var rGraphic = ready.GetComponentInChildren<GetawayButtonGraphic>();
            var rText = ready.GetComponentInChildren<TMP_Text>();
            if (rGraphic != null && rText != null)
            {
                Color readyColor = isReady ? new Color(0.25f, 1f, 0.5f) : (Color)new Color32(245, 158, 11, 255);
                rGraphic.Outline = readyColor;
                rText.color = readyColor;
                rGraphic.SetVerticesDirty();
            }

            // --- HIRARKI CTA: TOMBOL START SURVIVAL (PRIMARY CTA) ---
            bool canStart = IntegratedCrew.CanStart();
            start.interactable = connected && session.IsServer && (phase == RoundPhase.Wrecked || canStart);
            var sGraphic = start.GetComponentInChildren<GetawayButtonGraphic>();
            var sText = start.GetComponentInChildren<TMP_Text>();

            if (phase == RoundPhase.Wrecked)
            {
                SetLabel(start, "RETURN TO LOBBY");
                start.transform.localScale = Vector3.one;
                if (sGraphic != null && sText != null)
                {
                    Color accent = new Color32(245, 158, 11, 255);
                    sGraphic.Outline = accent;
                    sText.color = accent;
                    sGraphic.SetVerticesDirty();
                }
            }
            else if (canStart)
            {
                SetLabel(start, ">>>  START SURVIVAL NOW!  <<<");

                // Efek Denyut / Breathing Pulse saat kru sudah siap (Mengundang untuk diklik):
                float pulse = Mathf.PingPong(Time.unscaledTime * 3.5f, 1f);
                start.transform.localScale = Vector3.one * Mathf.Lerp(1.0f, 1.05f, pulse);

                if (sGraphic != null && sText != null)
                {
                    Color highlight = Color.Lerp(new Color(0.25f, 1f, 0.55f), new Color(1f, 0.9f, 0.2f), pulse);
                    sGraphic.Outline = highlight;
                    sText.color = highlight;
                    sGraphic.SetVerticesDirty();
                }
            }
            else
            {
                // Kondisi belum siap: tombol redup dan statis
                start.transform.localScale = Vector3.one;
                SetLabel(start, session != null && session.IsServer ? "WAITING FOR CREW TO READY..." : "WAITING FOR HOST TO START...");
                if (sGraphic != null && sText != null)
                {
                    sGraphic.Outline = new Color(0.35f, 0.35f, 0.45f);
                    sText.color = new Color(0.6f, 0.6f, 0.7f);
                    sGraphic.SetVerticesDirty();
                }
            }
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
            bool isCustom = input.CustomController;
            keyboard.interactable = isCustom;
            custom.interactable = !isCustom;
            hardware.SetActive(isCustom);

            // Indikator tombol aktif yang jelas:
            SetLabel(keyboard, isCustom ? "KEYBOARD / MOUSE" : "[ ACTIVE ] KEYBOARD / MOUSE");
            SetLabel(custom, isCustom ? "[ ACTIVE ] CUSTOM CONTROLLER" : "CUSTOM CONTROLLER");

            var kbGraphic = keyboard.GetComponentInChildren<GetawayButtonGraphic>();
            var kbText = keyboard.GetComponentInChildren<TMP_Text>();
            if (kbGraphic != null && kbText != null)
            {
                Color c = !isCustom ? new Color32(245, 158, 11, 255) : new Color32(100, 116, 139, 200);
                kbGraphic.Outline = c; kbText.color = c; kbGraphic.SetVerticesDirty();
            }

            var cuGraphic = custom.GetComponentInChildren<GetawayButtonGraphic>();
            var cuText = custom.GetComponentInChildren<TMP_Text>();
            if (cuGraphic != null && cuText != null)
            {
                Color c = isCustom ? new Color32(245, 158, 11, 255) : new Color32(100, 116, 139, 200);
                cuGraphic.Outline = c; cuText.color = c; cuGraphic.SetVerticesDirty();
            }

            // Jika Keyboard mode aktif, posisikan tombol Graphics di tengah agar tidak sendirian di pojok:
            var gRect = (RectTransform)graphics.transform;
            if (!isCustom)
            {
                Place(gRect, new Vector2(0, -60), new Vector2(340, 50));
            }
            else
            {
                Place(gRect, new Vector2(190, -190), new Vector2(340, 50));
            }

            modeLabel.text = isCustom
                ? "CUSTOM HARDWARE MODE: Calibrate your wheel/gyro before connecting to a session."
                : "KEYBOARD TEST:  A / D: steer or balance  |  Mouse: aim 360 DEG  |  Click / Space: fire  |  R: reload";

            volumeLabel.text = $"VOLUME: {AudioListener.volume:P0}";
            sensitivityLabel.text = $"STEERING SENSITIVITY: {input.SteeringSensitivity:0.0}";
            rangeLabel.text = $"STEERING RANGE: {input.SteeringFullScale:0.00}";
            SetLabel(invert, "INVERT STEERING: " + (input.InvertSteering ? "ON" : "OFF"));

            leaveSettings.gameObject.SetActive(NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);
            if (DowntownRendering.Instance != null) SetLabel(graphics, "GRAPHICS: " + DowntownRendering.Instance.PresetName);
            else if (QualitySettings.names.Length > 0) SetLabel(graphics, "GRAPHICS: " + QualitySettings.names[QualitySettings.GetQualityLevel()].ToUpperInvariant());

            settingsStatus.text = isCustom
                ? input.DeviceStatus + "\nDriver: 9600 baud / Shooter: 115200 baud  |  Center the device, then calibrate"
                : "Tab: switch roles in Solo Debug  |  Esc: return to game\nThe multiplayer match continues in the background while settings are open.";
        }
        void RefreshHUD(RoundState state, bool debug)
        {
            string role = input.ViewingDriver ? "DRIVER" : "SHOOTER";

            // =========================================================================
            // REVISI PRIORITAS 1 - SUB-TAHAP A: REFRESH HUD
            // =========================================================================

            // --- 1. TRUCK HEALTH (PERSENTASE, STATUS HEALTHY/DAMAGED/CRITICAL, FILL BAR) ---
            float hpPercentage = Mathf.Clamp01(state.Hull / (float)GetawayRules.MaxHull);
            if (truckHealthPercentText != null) truckHealthPercentText.text = Mathf.RoundToInt(hpPercentage * 100f) + "%";

            // Cek apakah baru saja terkena damage tabrakan:
            if (lastObservedHull >= 0 && state.Hull < lastObservedHull)
            {
                damageFlashUntil = Time.unscaledTime + 0.45f;
                damageToastUntil = Time.unscaledTime + 1.8f;
            }
            lastObservedHull = state.Hull;

            Color32 healthColor;
            string healthStatus;
            if (hpPercentage >= 0.70f)
            {
                healthColor = new Color32(56, 217, 133, 255); // Hijau
                healthStatus = "HEALTHY";
            }
            else if (hpPercentage >= 0.31f)
            {
                healthColor = new Color32(255, 194, 71, 255); // Kuning
                healthStatus = "DAMAGED";
            }
            else
            {
                healthColor = new Color32(255, 80, 95, 255);  // Merah
                healthStatus = "CRITICAL";
            }

            if (truckHealthStatusText != null)
            {
                truckHealthStatusText.text = healthStatus;
                truckHealthStatusText.color = healthColor;
            }

            if (truckHealthFill != null)
            {
                ((RectTransform)truckHealthFill.transform).sizeDelta = new Vector2(hpPercentage * 245f, 0);
                // Flash putih sesaat saat terkena damage:
                bool isFlashing = Time.unscaledTime < damageFlashUntil;
                truckHealthFill.color = isFlashing ? Color.white : (Color)healthColor;
            }

            // --- 2. STATS INSTRUMEN LAINNYA ---
            hudStats.text = $"{state.Distance:0} m       {state.Score} PTS       {state.Speed * 3.6f:0} km/h";

            // Update Police Pressure Bar:
            if (policeBarFill != null)
            {
                float pNorm = Mathf.Clamp01(state.Pursuit / 100f);
                ((RectTransform)policeBarFill.transform).sizeDelta = new Vector2(pNorm * 95f, 0);

                if (state.Pursuit >= 80f)
                    policeBarFill.color = (Mathf.Sin(Time.unscaledTime * 12f) > 0) ? Color.red : Color.white;
                else if (state.Pursuit >= 50f)
                    policeBarFill.color = new Color(1f, 0.35f, 0.1f);
                else
                    policeBarFill.color = new Color(1f, 0.2f, 0.45f);

                policePercentText.text = $"{state.Pursuit:0}%";
            }

            hudRole.text = $"{role} / FIRST PERSON     {(input.CustomController ? "CUSTOM CONTROLLER" : "KEYBOARD + MOUSE TEST")}     ESC: SETTINGS";
            hudHint.text = debug ? "SOLO DEBUG / Tab: switch role / A-D: active role / Mouse: look or aim" : input.ViewingDriver ? "Watch traffic and listen to your navigator." : "A-D: balance / Mouse: aim 360 DEG / Click or Space: fire / R: reload";

            bool isShooter = !input.ViewingDriver;

            // --- 3. PANEL NAVIGATION INSTRUCTION (PROMINENT SHOOTER HUD) ---
            if (navInstructionBox != null) navInstructionBox.SetActive(isShooter);
            var route = GetawayRules.Route;
            int turn; float until;
            if (route != null) route.NextBend(state.Distance, out turn, out until);
            else { turn = GetawayRules.Turn(state.Distance); until = GetawayRules.ToTurn(state.Distance); }

            if (isShooter && navDirectionText != null)
            {
                string arrowSymbol = turn < 0 ? "<<<  TURN LEFT" : turn > 0 ? "TURN RIGHT  >>>" : "^^^  GO STRAIGHT";
                string distString = until > 0 ? $"{until:0} m" : "NOW (0 m)";
                float estTime = until / Mathf.Max(1f, state.Speed);
                string estTimeString = until > 0 ? $"EST. {estTime:0.0} SECONDS" : "TURNING NOW";

                string navStatus;
                Color navColor;
                if (until <= 30f)
                {
                    navStatus = "TURN NOW";
                    navColor = (Mathf.Sin(Time.unscaledTime * 14f) > 0) ? Color.red : Color.yellow;
                }
                else if (until <= 100f)
                {
                    navStatus = "PREPARE";
                    navColor = new Color32(255, 194, 71, 255); // Kuning
                }
                else
                {
                    navStatus = "UPCOMING";
                    navColor = new Color32(248, 250, 252, 255); // Putih bersih (#F8FAFC)
                }

                navStatusBadge.text = $"[ {navStatus} ]";
                navStatusBadge.color = navColor;
                navDirectionText.text = arrowSymbol;
                navDirectionText.color = navColor;
                navDistanceText.text = distString;
                navEstTimeText.text = estTimeString;
            }

            // --- 4. PANEL CREW BALANCE (PROMINENT SHOOTER HUD) ---
            if (balanceContainer != null) balanceContainer.SetActive(isShooter);
            if (isShooter && balanceBarFill != null)
            {
                float bNorm = Mathf.Clamp01(state.Balance / 100f);
                ((RectTransform)balanceBarFill.transform).sizeDelta = new Vector2(bNorm * 230f, 0);
                balancePercentText.text = $"{state.Balance:0}%";

                Color32 bColor;
                string bStatus;
                string bHint;
                if (state.Balance >= 75f)
                {
                    bColor = new Color32(56, 217, 133, 255); // Hijau
                    bStatus = "STABLE";
                    bHint = "Maintain your balance!";
                }
                else if (state.Balance >= 31f)
                {
                    bColor = new Color32(255, 194, 71, 255); // Kuning
                    bStatus = "UNSTABLE";
                    bHint = "Countersteer with A / D!";
                }
                else
                {
                    bColor = new Color32(255, 80, 95, 255);  // Merah
                    bStatus = "CRITICAL";
                    bHint = "BRACE YOURSELF! FALLING!";
                }

                balanceStatusText.text = $"[ {bStatus} ]";
                balanceStatusText.color = bColor;
                balanceHintText.text = bHint;
                balanceBarFill.color = (state.Balance <= 30f && Mathf.Sin(Time.unscaledTime * 12f) > 0) ? Color.white : (Color)bColor;
            }

            // --- 5. PANEL AMMUNITION & RELOAD FEEDBACK (PROMINENT SHOOTER HUD) ---
            if (ammoBox != null) ammoBox.SetActive(isShooter);
            if (isShooter && ammoMagText != null)
            {
                ammoMagText.text = $"{state.Ammo}";
                ammoReserveText.text = $"/ {state.Reserve} <size=12>RESERVE</size>";

                if (state.ReloadTime > 0)
                {
                    wasReloading = true;
                    ammoStatusBadgeText.text = "RELOADING...";
                    ammoStatusBadgeText.color = new Color32(255, 194, 71, 255);
                    ammoReloadBarContainer.SetActive(true);
                    float reloadProgress = Mathf.Clamp01((1.8f - state.ReloadTime) / 1.8f);
                    ((RectTransform)ammoReloadProgressFill.transform).sizeDelta = new Vector2(reloadProgress * 300f, 0);
                    ammoMagText.color = new Color(0.7f, 0.7f, 0.7f);
                }
                else
                {
                    if (wasReloading)
                    {
                        wasReloading = false;
                        reloadCompleteUntil = Time.unscaledTime + 1.2f;
                    }

                    if (Time.unscaledTime < reloadCompleteUntil)
                    {
                        ammoStatusBadgeText.text = "RELOAD COMPLETE";
                        ammoStatusBadgeText.color = new Color32(56, 217, 133, 255);
                        ammoReloadBarContainer.SetActive(false);
                        ammoMagText.color = Color.white;
                    }
                    else
                    {
                        ammoReloadBarContainer.SetActive(false);
                        if (state.Ammo <= 0)
                        {
                            ammoStatusBadgeText.text = "RELOAD!";
                            ammoStatusBadgeText.color = new Color32(255, 80, 95, 255);
                            ammoMagText.color = Color.red;
                        }
                        else if (state.Ammo <= 3)
                        {
                            ammoStatusBadgeText.text = "LOW AMMO";
                            ammoStatusBadgeText.color = new Color32(255, 194, 71, 255);
                            ammoMagText.color = Color.yellow;
                        }
                        else
                        {
                            ammoStatusBadgeText.text = "READY";
                            ammoStatusBadgeText.color = new Color32(56, 217, 133, 255);
                            ammoMagText.color = Color.white;
                        }
                    }
                }
            }

            // --- 6. ACTION FEEDBACK: DAMAGE TOAST ---
            if (damageToastText != null && damageToastText.transform.parent != null)
            {
                bool showToast = Time.unscaledTime < damageToastUntil;
                damageToastText.transform.parent.gameObject.SetActive(showToast);
                if (showToast)
                {
                    damageToastText.text = $"[!] TRUCK DAMAGED! [!]\n-1 HULL ({state.Hull}/{GetawayRules.MaxHull})";
                    damageToastText.color = (Mathf.Sin(Time.unscaledTime * 14f) > 0) ? new Color(1f, 0.35f, 0.15f) : Color.yellow;
                }
            }

            countdown.text = state.Phase == RoundPhase.Countdown ? "ROLLING IN " + Mathf.CeilToInt(state.Countdown) : "";
            deviceWarning.text = input.CustomController && !input.HasFreshSample ? "CONTROLLER INPUT LOST / Esc to reconnect or select keyboard testing" : "";

            // Efek Darah Kritis (Denyut merah jika nyawa truk sisa 1):
            float damageAlpha = GetComponent<IntegratedWorld>().DamageFeedback * .22f;
            if (state.Hull == 1)
            {
                float criticalPulse = Mathf.PingPong(Time.unscaledTime * 3f, 1f) * 0.14f;
                damageAlpha = Mathf.Max(damageAlpha, criticalPulse);
            }
            damage.color = new Color(1, .12f, .08f, damageAlpha);

            // --- OVERLAY LAMPU SIRENE POLISI (STROBE MERAH-BIRU) ---
            if (sirenOverlay != null)
            {
                if (state.Pursuit >= 45f)
                {
                    float pursuitNorm = Mathf.InverseLerp(45f, 100f, state.Pursuit);
                    bool isBlue = Mathf.Sin(Time.unscaledTime * Mathf.Lerp(10f, 20f, pursuitNorm)) > 0;
                    Color sirenColor = isBlue ? new Color(0.1f, 0.4f, 1f) : new Color(1f, 0.15f, 0.2f);
                    float sirenAlpha = Mathf.Lerp(0.04f, 0.16f, pursuitNorm);
                    sirenOverlay.color = new Color(sirenColor.r, sirenColor.g, sirenColor.b, sirenAlpha);
                }
                else
                {
                    sirenOverlay.color = Color.clear;
                }
            }

            // --- TEKS PERINGATAN DARURAT TENGAH LAYAR ---
            if (emergencyAlert != null)
            {
                if (!input.ViewingDriver && state.Balance <= 30f)
                {
                    emergencyAlert.gameObject.SetActive(true);
                    bool flashToggle = Mathf.Sin(Time.unscaledTime * 14f) > 0;
                    emergencyAlert.color = flashToggle ? Color.red : Color.yellow;
                    emergencyAlert.text = $"CRITICAL WARNING\nLOSING BALANCE! ({state.Balance:0}%)\nBRACE YOURSELF! (A / D)";
                }
                else if (state.Pursuit >= 85f)
                {
                    emergencyAlert.gameObject.SetActive(true);
                    bool flashToggle = Mathf.Sin(Time.unscaledTime * 14f) > 0;
                    emergencyAlert.color = flashToggle ? Color.red : Color.white;
                    emergencyAlert.text = "[!] CRITICAL WARNING [!]\nPOLICE IN RANGE! CAPTURE IMMINENT!";
                }
                else
                {
                    emergencyAlert.gameObject.SetActive(false);
                }
            }
        }
    }
    
}
