using UnityEngine;

namespace Getaway.Integration
{
    [System.Flags]
    public enum GetawayAudioCue : byte { None = 0, Start = 1, End = 2, Impact = 4, Reload = 8, Ready = 16 }

    /// <summary>Local presentation of replicated gameplay; fixed voices, imported clips, no audio RPCs.</summary>
    public sealed class GetawayAudio : MonoBehaviour
    {
        static readonly string[] Names = { "shot", "hit", "impact", "pass", "reload", "ready", "pickup", "countdown", "start", "end" };
        readonly AudioClip[] effects = new AudioClip[10];
        readonly AudioSource[] voices = new AudioSource[8];
        readonly int[] passingIds = new int[GetawayRules.ThreatSlots];
        readonly float[] passingDistances = new float[GetawayRules.ThreatSlots];
        AudioSource music, engine, wind, siren;
        IntegratedInput input;
        RoundState previous;
        bool initialized, observed;
        int voiceIndex, countdown = -1;

        public void Initialize(IntegratedInput controls)
        {
            if (initialized) return;
            initialized = true; input = controls;
            for (int i = 0; i < effects.Length; i++) effects[i] = Load(Names[i]);
            for (int i = 0; i < voices.Length; i++) voices[i] = Source("Effect voice " + i, false);
            music = Loop("Downtown music", "downtown", 128);
            engine = Loop("Truck engine", "engine", 64);
            wind = Loop("Road wind", "wind", 96);
            siren = Loop("Rear police siren", "siren", 32);
            siren.spatialBlend = 1; siren.minDistance = 12; siren.maxDistance = 110;
            siren.rolloffMode = AudioRolloffMode.Linear;
        }

        static AudioClip Load(string name)
        {
            var clip = Resources.Load<AudioClip>("GetawayAudio/" + name);
            if (clip == null) Debug.LogWarning("Getaway audio clip missing: " + name);
            return clip;
        }
        AudioSource Source(string label, bool loop)
        {
            var child = new GameObject(label); child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = loop; source.spatialBlend = 0;
            source.dopplerLevel = 0; source.volume = 0; source.reverbZoneMix = 0;
            return source;
        }
        AudioSource Loop(string label, string clip, int priority)
        {
            var source = Source(label, true); source.clip = Load(clip); source.priority = priority;
            return source;
        }
        void Effect(int index, float volume)
        {
            if (!initialized || effects[index] == null || !Application.isFocused) return;
            var source = voices[voiceIndex++ % voices.Length];
            source.Stop(); source.clip = effects[index]; source.volume = volume; source.pitch = 1; source.Play();
        }
        static void Fade(AudioSource source, float target, float dt)
        {
            source.volume = Mathf.MoveTowards(source.volume, target, dt * .8f);
            if (target > 0 && !source.isPlaying && source.clip != null) source.Play();
            else if (target <= 0 && source.volume <= 0 && source.isPlaying) source.Stop();
        }
        public void Shot(bool hit)
        {
            bool shooter = input != null && !input.ViewingDriver;
            Effect(0, shooter ? .5f : .22f);
            if (hit && shooter) Effect(1, .2f);
        }
        public void Pickup() => Effect(6, .35f);

        public static GetawayAudioCue Cues(RoundState before, RoundState after, bool shooter)
        {
            if (before.Round != after.Round) return GetawayAudioCue.None;
            var cues = GetawayAudioCue.None;
            if (after.Phase == RoundPhase.Playing && before.Phase != RoundPhase.Playing) cues |= GetawayAudioCue.Start;
            if (after.Phase == RoundPhase.Wrecked && before.Phase != RoundPhase.Wrecked) cues |= GetawayAudioCue.End;
            if (before.Phase != RoundPhase.Playing || after.Phase != RoundPhase.Playing) return cues;
            if (after.Hull < before.Hull) cues |= GetawayAudioCue.Impact;
            if (shooter && after.ReloadTime > 0 && before.ReloadTime <= 0) cues |= GetawayAudioCue.Reload;
            if (shooter && after.ReloadTime <= 0 && before.ReloadTime > 0 && after.Ammo > before.Ammo) cues |= GetawayAudioCue.Ready;
            return cues;
        }

        public void Tick(RoundState state, IntegratedCrew session, Transform truck, double presentationDistance)
        {
            if (!initialized) return;
            bool playing = session != null && state.Phase == RoundPhase.Playing;
            bool connected = session != null;
            bool focused = Application.isFocused;
            bool menu = IntegratedGetaway.MenuOpen;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            bool sameRound = observed && previous.Round == state.Round;
            if (!sameRound) { countdown = -1; System.Array.Clear(passingIds, 0, passingIds.Length); }
            bool shooter = input != null && !input.ViewingDriver;
            if (sameRound && connected)
            {
                var cues = Cues(previous, state, shooter);
                if ((cues & GetawayAudioCue.Start) != 0) Effect(8, .45f);
                if ((cues & GetawayAudioCue.End) != 0) Effect(9, .45f);
                if ((cues & GetawayAudioCue.Impact) != 0) Effect(2, .55f);
                if ((cues & GetawayAudioCue.Reload) != 0) Effect(4, .4f);
                if ((cues & GetawayAudioCue.Ready) != 0) Effect(5, .35f);
            }
            if (connected && state.Phase == RoundPhase.Countdown)
            {
                int second = Mathf.CeilToInt(state.Countdown);
                if (second > 0 && second != countdown) { countdown = second; Effect(7, .35f); }
            }
            float policeGap = 1000;
            if (playing)
                for (int i = 0; i < session.Threats.Count && i < passingIds.Length; i++)
                {
                    var threat = session.Threats[i];
                    if (!threat.Active) { passingIds[i] = 0; continue; }
                    float relative = (float)(threat.RouteDistance - state.Distance);
                    if (threat.Kind == ThreatKind.Police)
                    {
                        policeGap = Mathf.Min(policeGap, Mathf.Abs(relative));
                        var route = GetawayRules.Route;
                        siren.transform.position = route != null
                            ? route.Sample(threat.RouteDistance).RelativeTo(route.Sample(presentationDistance), threat.LaneOffset) + Vector3.up * 2
                            : truck.TransformPoint(threat.X - state.TruckX, 2, relative);
                    }
                    if (threat.Kind == ThreatKind.Traffic && passingIds[i] == threat.Id && passingDistances[i] > 0 && relative <= 0
                        && Mathf.Abs(threat.LaneOffset - state.TruckX) < 8) Effect(3, .3f);
                    passingIds[i] = threat.Id; passingDistances[i] = relative;
                }
            float musicVolume = playing ? (policeGap < 90 ? .24f : .17f) : connected && state.Phase == RoundPhase.Wrecked ? .06f : .1f;
            Fade(music, focused ? musicVolume * (menu ? .55f : 1) : 0, dt);
            Fade(engine, playing && focused ? .22f + Mathf.Clamp01(state.Speed / 42) * .12f : 0, dt);
            engine.pitch = Mathf.Lerp(.7f, 1.65f, Mathf.Clamp01(state.Speed / 42));
            Fade(wind, playing && focused ? Mathf.Clamp01(state.Speed / 42) * .24f : 0, dt);
            Fade(siren, playing && focused && policeGap < 100 ? .48f : 0, dt);
            previous = state; observed = connected;
        }
        void OnApplicationFocus(bool focused)
        {
            if (!focused) foreach (var voice in voices) if (voice != null) voice.Stop();
        }
        void OnDisable()
        {
            foreach (var voice in voices) if (voice != null) voice.Stop();
            if (music != null) music.Stop(); if (engine != null) engine.Stop();
            if (wind != null) wind.Stop(); if (siren != null) siren.Stop();
        }
    }
}
