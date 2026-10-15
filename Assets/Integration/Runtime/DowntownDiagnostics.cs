#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;
using Unity.Profiling;
namespace Getaway.Integration
{
    /// <summary>Manual F8 ten-minute capture; no automatic profiling, builds, or extra processes.</summary>
    public sealed class DowntownDiagnostics : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var world = FindAnyObjectByType<IntegratedWorld>();
            if (world != null && world.UseDowntownRoute && world.GetComponent<DowntownDiagnostics>() == null) world.gameObject.AddComponent<DowntownDiagnostics>();
        }
        readonly FrameTiming[] timing = new FrameTiming[1];
        StreamWriter output;
        ProfilerRecorder mainThread;
        float began, lastRow;
        int frames, cpuCount, gpuCount;
        double cpuTotal, gpuTotal;
        DowntownEnvironment environment;
        IntegratedInput controls;
        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
            { if (output == null) Begin(); else End("stopped manually"); }
            if (output == null) return;
            FrameTimingManager.CaptureFrameTimings(); frames++;
            bool hasMainTiming = false;
            if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
            {
                if (timing[0].cpuMainThreadFrameTime > 0) { cpuTotal += timing[0].cpuMainThreadFrameTime; cpuCount++; hasMainTiming = true; }
                if (timing[0].gpuFrameTime > 0) { gpuTotal += timing[0].gpuFrameTime; gpuCount++; }
            }
            if (!hasMainTiming && mainThread.Valid && mainThread.LastValue > 0) { cpuTotal += mainThread.LastValue / 1000000d; cpuCount++; }
            float now = Time.unscaledTime;
            if (now - lastRow >= 1)
            {
                var session = IntegratedCrew.Session; var state = session != null ? session.State.Value : default;
                string cpu = cpuCount > 0 ? (cpuTotal / cpuCount).ToString("F3", CultureInfo.InvariantCulture) : "";
                string gpu = gpuCount > 0 ? (gpuTotal / gpuCount).ToString("F3", CultureInfo.InvariantCulture) : "";
                try
                {
                output.WriteLine(string.Format(CultureInfo.InvariantCulture,"{0:F1},{1},{2:F1},{3:F1},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13}",
                    now-began, state.Phase, state.Distance, frames/(now-lastRow), cpu, gpu,
                    GC.GetTotalMemory(false), Profiler.GetTotalAllocatedMemoryLong(), environment != null ? environment.ActiveTiles : 0, DowntownRendering.Instance != null ? DowntownRendering.Instance.PresetName : "unknown", Screen.width, Screen.height, IntegratedGetaway.MenuOpen, controls != null && controls.ViewingDriver ? "Driver" : "Shooter"));
                output.Flush();
                }
                catch (IOException error) { End("write failed: "+error.Message); return; }
                lastRow = now; frames = cpuCount = gpuCount = 0; cpuTotal = gpuTotal = 0;
            }
            if (now - began >= 600) End("completed 600 seconds");
        }
        void Begin()
        {
            environment = FindAnyObjectByType<DowntownEnvironment>(); controls = GetComponent<IntegratedInput>();
            if (environment == null) { Debug.Log("Downtown capture requires the integrated downtown scene."); return; }
            try
            {
                string path = Path.Combine(Application.persistentDataPath,"downtown-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".csv");
                output = new StreamWriter(path); mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1); output.WriteLine("seconds,phase,distance,fps,cpu_main_ms,gpu_ms,managed_bytes,unity_allocated_bytes,tiles,preset,width,height,menu_open,role");
                began = lastRow = Time.unscaledTime; frames = cpuCount = gpuCount = 0; cpuTotal = gpuTotal = 0;
                Debug.Log("Downtown capture started: "+path+". Missing backend timings are blank, not zero. F8 stops.");
            }
            catch (Exception error) { mainThread.Dispose(); output?.Dispose(); output = null; Debug.LogWarning("Capture unavailable: "+error.Message); }
        }
        void End(string reason)
        {
            mainThread.Dispose(); var writer=output; output=null;
            try { writer?.Dispose(); } catch(IOException error) { Debug.LogWarning("Capture close failed: "+error.Message); }
            Debug.Log("Downtown capture "+reason);
        }
        void OnDisable() { if (output != null) End("scene closed"); }
    }
}
#endif
