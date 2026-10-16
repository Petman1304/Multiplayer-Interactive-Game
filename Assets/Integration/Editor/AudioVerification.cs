using System;
using Getaway.Integration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Edit-mode checks only; does not play audio or start a session.</summary>
public static class AudioVerification
{
    static void Check(bool value, string message) { if (!value) throw new Exception("Audio verification: " + message); }
    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before audio verification.");
        string[] names = { "downtown", "engine", "wind", "siren", "shot", "hit", "impact", "pass", "reload", "ready", "pickup", "countdown", "start", "end" };
        foreach (string name in names)
        {
            string path = GetawayAudioImporter.Folder + name + ".wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            Check(clip != null && clip.channels == 1 && clip.frequency == 22050 && clip.length > .05f, name + " format");
            bool music = name == "downtown";
            Check(importer.defaultSampleSettings.loadType == (music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad), name + " load mode");
            Check(importer.defaultSampleSettings.preloadAudioData != music && importer.loadInBackground == music, name + " loading flags");
        }
        var before = new RoundState { Round = 1, Phase = RoundPhase.Countdown, Hull = 5, Ammo = 5 };
        var after = before; after.Phase = RoundPhase.Playing;
        Check(GetawayAudio.Cues(before, after, true) == GetawayAudioCue.Start, "one start cue");
        before = after;
        Check(GetawayAudio.Cues(before, after, true) == GetawayAudioCue.None, "identical replicated snapshots stay silent");
        after.Hull--;
        Check(GetawayAudio.Cues(before, after, false) == GetawayAudioCue.Impact, "both seats hear damage");
        after = before; after.ReloadTime = 1.8f;
        Check(GetawayAudio.Cues(before, after, true) == GetawayAudioCue.Reload && GetawayAudio.Cues(before, after, false) == GetawayAudioCue.None, "reload is shooter only");
        before = after; after.ReloadTime = 0; after.Ammo = 12;
        Check(GetawayAudio.Cues(before, after, true) == GetawayAudioCue.Ready, "reload completion");
        before = after; after.Phase = RoundPhase.Wrecked;
        Check(GetawayAudio.Cues(before, after, true) == GetawayAudioCue.End, "one defeat cue");
        before = after; after.Phase = RoundPhase.Lobby; after.Ammo = 0; after.Hull = 0;
        Check(GetawayAudio.Cues(before, after, true) == GetawayAudioCue.None, "lobby reset stays silent");
        after.Round++;
        Check(GetawayAudio.Cues(before, after, true) == GetawayAudioCue.None, "new round does not inherit cues");
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Audio verification"); SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            var audio = root.AddComponent<GetawayAudio>(); audio.Initialize(null); audio.Initialize(null);
            var sources = root.GetComponentsInChildren<AudioSource>(true);
            Check(sources.Length == 12, "exactly eight reusable effect voices and four loops, including repeated Initialize");
            int loops = 0;
            foreach (var source in sources)
            {
                Check(!source.isPlaying && !source.playOnAwake && source.dopplerLevel == 0, "initialization never starts playback");
                if (source.loop) { loops++; Check(source.clip != null, "loop reference"); }
            }
            Check(loops == 4, "four loop sources");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        return "PASS: 14 clip imports, cue transitions and role filtering, duplicate snapshot suppression, 12 fixed sources, silent/idempotent initialization. Listening, live networking and frame-time checks remain manual.";
    }
}
