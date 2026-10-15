using System;
using System.Globalization;
using System.IO.Ports;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Getaway.Integration
{
    /// <summary>One local device feeds the same validated network input path as keyboard testing.</summary>
    public class IntegratedInput : MonoBehaviour
    {
        public float SteeringSensitivity = 1;
        public bool CustomController;
        public bool DebugDriverCamera { get; private set; } = true;
        public bool ViewingDriver => IntegratedCrew.Session != null && IntegratedCrew.Session.SoloDebug.Value
            ? DebugDriverCamera : IntegratedCrew.Local != null && IntegratedCrew.Local.Role.Value == CrewRole.Driver;
        public float DriverLookYaw { get; private set; }
        public float DriverLookPitch { get; private set; }
        public float MouseSensitivity = .12f;
        public string Port = "";
        public float SteeringFullScale = 1.57f;
        public bool InvertSteering;
        public string DeviceStatus { get { lock (gate) return deviceStatus; } }
        public bool Aiming { get; private set; }
        public float AimYaw { get; private set; }
        public float AimPitch { get; private set; } = 4;
        readonly object gate = new();
        Thread reader;
        volatile bool stop;
        string deviceStatus = "Not connected";
        Vector3 sample, neutral;
        bool aimButton, shootButton;
        long sampleTime;
        float nextSend;
        bool calibrateNext;
        public bool HasFreshSample
        {
            get { lock (gate) return sampleTime != 0 && Age(sampleTime) < .5; }
        }
        static double Age(long timestamp) => (System.Diagnostics.Stopwatch.GetTimestamp() - timestamp) / (double)System.Diagnostics.Stopwatch.Frequency;
        public void ConnectDevice()
        {
            DisconnectDevice();
            if (reader != null) return;
            var crew = IntegratedCrew.Local;
            if (crew == null || crew.Role.Value == CrewRole.None) { SetStatus("Choose your role before connecting hardware."); return; }
            if (string.IsNullOrWhiteSpace(Port)) { SetStatus("Enter a serial port, e.g. COM3 or /dev/ttyACM0."); return; }
            bool shooter = crew.Role.Value == CrewRole.Shooter;
            string port = Port.Trim();
            stop = false; calibrateNext = true;
            reader = new Thread(() => ReadDevice(port, shooter)) { IsBackground = true, Name = "Getaway custom controller" };
            reader.Start();
        }
        public void DisconnectDevice()
        {
            stop = true;
            if (reader != null && !reader.Join(300))
            {
                // Keep its cancellation flag set until the worker exits; never start a second reader.
                SetStatus("Serial device is still closing. Try again shortly.");
                return;
            }
            reader = null;
            lock (gate) { sampleTime = 0; deviceStatus = "Not connected"; }
        }
        void SetStatus(string value) { lock (gate) deviceStatus = value; }
        void ReadDevice(string port, bool shooter)
        {
            try
            {
                using var serial = new SerialPort(port, shooter ? 115200 : 9600) { ReadTimeout = 100, WriteTimeout = 100 };
                serial.Open(); SetStatus("Connected • waiting for controller data");
                while (!stop)
                {
                    string line;
                    try { line = serial.ReadLine(); } catch (TimeoutException) { continue; }
                    if (line.Length > 160) continue;
                    if (!TryParseSample(line, shooter, out Vector3 value, out bool aim, out bool shoot)) continue;
                    lock (gate)
                    {
                        sample = value; aimButton = aim; shootButton = shoot;
                        sampleTime = System.Diagnostics.Stopwatch.GetTimestamp();
                        if (calibrateNext) { neutral = sample; calibrateNext = false; }
                        deviceStatus = shooter ? "Gyro connected • 115200 baud" : "Steering connected • 9600 baud";
                    }
                }
            }
            catch (Exception error) { SetStatus("Controller unavailable: " + error.Message); }
            finally { lock (gate) sampleTime = 0; }
        }
        public static bool TryParseSample(string line, bool shooter, out Vector3 value, out bool aim, out bool shoot)
        {
            value = Vector3.zero; aim = shoot = false;
            string[] fields = line.Trim().Split(',');
            if (fields.Length != (shooter ? 5 : 1)) return false;
            if (!float.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) || !GetawayRules.Finite(x)) return false;
            value.x = x;
            if (!shooter) return true;
            if (!float.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) || !GetawayRules.Finite(y) ||
                !float.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z) || !GetawayRules.Finite(z) ||
                !int.TryParse(fields[3], out int a) || (a != 0 && a != 1) ||
                !int.TryParse(fields[4], out int s) || (s != 0 && s != 1)) return false;
            value = new Vector3(x, y, z); aim = a == 1; shoot = s == 1; return true;
        }
        public void Calibrate() { lock (gate) { if (sampleTime != 0) neutral = sample; else calibrateNext = true; } }
        public void ChangeMode(bool custom) { DisconnectDevice(); CustomController = custom; Aiming = false; }
        void Update()
        {
            var crew = IntegratedCrew.Local; var session = IntegratedCrew.Session;
            if (crew == null || session == null) return;
            var keys = Keyboard.current;
            if (session.SoloDebug.Value && Application.isFocused && keys != null && keys.tabKey.wasPressedThisFrame)
            {
                DebugDriverCamera = !DebugDriverCamera;
                crew.SendInput(0, 0, AimYaw, AimPitch, false);
                nextSend = 0; Aiming = false;
            }
            bool active = session.State.Value.Phase == RoundPhase.Playing && Application.isFocused && !IntegratedGetaway.MenuOpen;
            Cursor.lockState = active ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !active;
            if (keys != null && keys.cKey.wasPressedThisFrame && Application.isFocused) Calibrate();
            if (session.State.Value.Phase != RoundPhase.Playing) return;
            if (Application.isFocused && !IntegratedGetaway.MenuOpen && keys != null && !ViewingDriver && keys.rKey.wasPressedThisFrame) crew.Reload();
            float horizontal = 0; bool fire = false;
            Aiming = false;
            if (Application.isFocused && !IntegratedGetaway.MenuOpen)
            {
                if (CustomController)
                {
                    lock (gate)
                    {
                        if (sampleTime != 0 && Age(sampleTime) < .5)
                        {
                            Vector3 centered = sample - neutral;
                            horizontal = ViewingDriver
                                ? GetawayRules.Axis(centered.x / Mathf.Max(.01f, SteeringFullScale) * (InvertSteering ? -1 : 1), .05f)
                                : GetawayRules.Axis(-centered.x / .45f, .1f);
                            AimYaw = Mathf.Clamp(centered.z * Mathf.Rad2Deg, -75, 75);
                            AimPitch = Mathf.Clamp(-centered.y * Mathf.Rad2Deg + 4, -25, 60);
                            Aiming = aimButton; fire = shootButton;
                        }
                    }
                }
                else
                {
                    if (keys != null) horizontal = (keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1 : 0) - (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1 : 0);
                    var mouse = Mouse.current;
                    Aiming = !ViewingDriver && mouse != null && mouse.rightButton.isPressed;
                    fire = !ViewingDriver && ((mouse != null && mouse.leftButton.isPressed) || (keys != null && keys.spaceKey.isPressed));
                    if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
                    {
                        Vector2 delta = mouse.delta.ReadValue() * MouseSensitivity * (Aiming ? .55f : 1);
                        if (ViewingDriver)
                        {
                            DriverLookYaw = Mathf.Clamp(DriverLookYaw + delta.x, -85, 85);
                            DriverLookPitch = Mathf.Clamp(DriverLookPitch - delta.y, -55, 55);
                        }
                        else
                        {
                            AimYaw = Mathf.Clamp(AimYaw + delta.x, -75, 75);
                            AimPitch = Mathf.Clamp(AimPitch - delta.y, -25, 60);
                        }
                    }
                }
            }
            if (Time.unscaledTime < nextSend) return;
            nextSend = Time.unscaledTime + .04f;
            bool driver = crew.Role.Value == CrewRole.Driver;
            if (session.SoloDebug.Value)
            {
                // Exactly one role receives controls, matching its normal multiplayer input mapping.
                float steering = ViewingDriver ? Mathf.Clamp(horizontal * SteeringSensitivity, -1, 1) : 0;
                float walking = ViewingDriver ? 0 : horizontal;
                crew.SendInput(steering, walking, AimYaw, AimPitch, !ViewingDriver && fire);
            }
            else crew.SendInput(driver ? Mathf.Clamp(horizontal * SteeringSensitivity, -1, 1) : 0, driver ? 0 : horizontal, AimYaw, AimPitch, fire);
        }
        void OnDisable() { DisconnectDevice(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
