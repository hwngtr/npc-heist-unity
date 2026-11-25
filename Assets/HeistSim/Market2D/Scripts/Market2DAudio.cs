using UnityEngine;

namespace HeistSim.Market2D
{
    /// <summary>
    /// Procedural synthesized audio floor for Frischemarkt Market2D (no external audio assets).
    /// Synthesizes PCM clips with 5ms fade envelopes via AudioClip.Create for pickup blips,
    /// unlock chimes, alert stings, footsteps, UI clicks, milestone triads, and quiet room tone.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Market2DAudio : MonoBehaviour
    {
        private const int SampleRate = 22050;

        /// <summary>Master SFX gain scalar (0..1).</summary>
        public float sfxGain = 0.65f;

        /// <summary>Ambient room tone gain scalar (0..1).</summary>
        public float ambientGain = 0.04f;

        private AudioSource _sfxSource;
        private AudioSource _ambientSource;
        private AudioClip _pickupClip;
        private AudioClip _rfPickupClip;
        private AudioClip _unlockClip;
        private AudioClip _alertClip;
        private AudioClip _refusalClip;
        private AudioClip _stepClip;
        private AudioClip _uiClickClip;
        private AudioClip _escapeClip;
        private AudioClip _bustedClip;
        private AudioClip _ambientClip;
        private bool _muted;
        private bool _initialized;

        /// <summary>True when audio output is muted by the player.</summary>
        public bool Muted
        {
            get => _muted;
            set
            {
                _muted = value;
                if (_ambientSource != null)
                {
                    _ambientSource.volume = _muted ? 0f : ambientGain;
                }
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>Builds AudioSources and synthesized PCM clips on demand.</summary>
        public void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.spatialBlend = 0f;

            _ambientSource = gameObject.AddComponent<AudioSource>();
            _ambientSource.playOnAwake = false;
            _ambientSource.loop = true;
            _ambientSource.spatialBlend = 0f;

            _pickupClip = BuildSweepClip("PickupBlip", 0.095f, 880f, 1320f, 0.42f);
            _rfPickupClip = BuildDualBlipClip("RfPickupBlip", 0.145f, 840f, 1280f, 1560f, 0.40f);
            _unlockClip = BuildTriadClip("UnlockChime", new[] { 587f, 880f }, 0.085f, 0.36f);
            _alertClip = BuildTriadClip("AlertSting", new[] { 466f, 622f }, 0.10f, 0.44f);
            _refusalClip = BuildSweepClip("RefusalBuzz", 0.11f, 240f, 175f, 0.34f);
            _stepClip = BuildNoiseBurstClip("StepTap", 0.028f, 0.16f, lowPass: 0.25f);
            _uiClickClip = BuildNoiseBurstClip("UiClick", 0.020f, 0.22f, lowPass: 0.55f);
            _escapeClip = BuildTriadClip("EscapeSting", new[] { 523.25f, 659.25f, 783.99f }, 0.125f, 0.44f);
            _bustedClip = BuildTriadClip("BustedSting", new[] { 293.66f, 220.0f }, 0.19f, 0.42f);
            _ambientClip = BuildBrownNoiseLoop("RoomTone", 4.5f, 0.32f);

            _ambientSource.clip = _ambientClip;
            _ambientSource.volume = _muted ? 0f : ambientGain;
            if (Application.isPlaying)
            {
                _ambientSource.Play();
            }
        }

        /// <summary>Plays a bright loot pickup blip with subtle pitch variation.</summary>
        public void PlayPickup(bool rfTagged)
        {
            PlayOneShot(rfTagged ? _rfPickupClip : _pickupClip, 0.85f, Random.Range(0.93f, 1.07f));
        }

        /// <summary>Plays an objective/device unlock chime (vitrine, CCTV panel, de-tagger).</summary>
        public void PlayUnlock()
        {
            PlayOneShot(_unlockClip, 0.90f, 1.0f);
        }

        /// <summary>Plays a detective sighting or EAS alarm warning pulse.</summary>
        public void PlayAlert()
        {
            PlayOneShot(_alertClip, 0.92f, 1.0f);
        }

        /// <summary>Plays a brief low refusal tone.</summary>
        public void PlayRefusal()
        {
            PlayOneShot(_refusalClip, 0.65f, 1.0f);
        }

        /// <summary>Plays a subtle footstep tap.</summary>
        public void PlayStep(bool sprint)
        {
            PlayOneShot(_stepClip, sprint ? 0.55f : 0.24f, Random.Range(0.88f, 1.12f));
        }

        /// <summary>Plays a crisp UI button click.</summary>
        public void PlayUiClick()
        {
            PlayOneShot(_uiClickClip, 0.55f, Random.Range(0.96f, 1.04f));
        }

        /// <summary>Plays the escape victory triad or busted chord.</summary>
        public void PlayOutcome(bool escaped)
        {
            PlayOneShot(escaped ? _escapeClip : _bustedClip, 0.95f, 1.0f);
        }

        private void PlayOneShot(AudioClip clip, float scale, float pitch)
        {
            if (_muted || !Application.isPlaying)
            {
                return;
            }

            EnsureInitialized();
            if (_sfxSource == null || clip == null)
            {
                return;
            }

            _sfxSource.pitch = pitch;
            _sfxSource.PlayOneShot(clip, Mathf.Clamp01(sfxGain * scale));
        }

        private static AudioClip BuildSweepClip(string name, float duration, float f0, float f1, float amp)
        {
            var count = Mathf.Max(64, Mathf.RoundToInt(duration * SampleRate));
            var data = new float[count];
            var phase = 0f;
            for (var i = 0; i < count; i++)
            {
                var u = i / (float)count;
                var freq = Mathf.Lerp(f0, f1, u);
                phase += (2f * Mathf.PI * freq) / SampleRate;
                var env = EdgeEnvelope(i, count) * Mathf.Pow(1f - u, 0.8f);
                data[i] = Mathf.Sin(phase) * amp * env;
            }

            return MakeClip(name, data);
        }

        private static AudioClip BuildDualBlipClip(string name, float duration, float f0, float f1, float f2, float amp)
        {
            var count = Mathf.Max(64, Mathf.RoundToInt(duration * SampleRate));
            var data = new float[count];
            var phase = 0f;
            for (var i = 0; i < count; i++)
            {
                var u = i / (float)count;
                var freq = u < 0.55f ? Mathf.Lerp(f0, f1, u / 0.55f) : f2;
                phase += (2f * Mathf.PI * freq) / SampleRate;
                var env = EdgeEnvelope(i, count) * (1f - 0.55f * u);
                data[i] = Mathf.Sin(phase) * amp * env;
            }

            return MakeClip(name, data);
        }

        private static AudioClip BuildTriadClip(string name, float[] notes, float noteDuration, float amp)
        {
            var stepSamples = Mathf.Max(64, Mathf.RoundToInt(noteDuration * 0.85f * SampleRate));
            var noteSamples = Mathf.Max(64, Mathf.RoundToInt(noteDuration * SampleRate));
            var total = stepSamples * (notes.Length - 1) + noteSamples;
            var data = new float[total];

            for (var n = 0; n < notes.Length; n++)
            {
                var start = n * stepSamples;
                var freq = notes[n];
                for (var i = 0; i < noteSamples && start + i < total; i++)
                {
                    var u = i / (float)noteSamples;
                    var env = EdgeEnvelope(i, noteSamples) * Mathf.Pow(1f - u, 0.65f);
                    var s = Mathf.Sin(2f * Mathf.PI * freq * (i / (float)SampleRate));
                    var warm = s + 0.18f * Mathf.Sin(4f * Mathf.PI * freq * (i / (float)SampleRate));
                    data[start + i] = Mathf.Clamp(data[start + i] + warm * amp * env, -0.95f, 0.95f);
                }
            }

            return MakeClip(name, data);
        }

        private static AudioClip BuildNoiseBurstClip(string name, float duration, float amp, float lowPass)
        {
            var count = Mathf.Max(32, Mathf.RoundToInt(duration * SampleRate));
            var data = new float[count];
            var state = 0f;
            var seed = 1337u;
            for (var i = 0; i < count; i++)
            {
                seed = seed * 1664525u + 1013904223u;
                var white = ((seed & 0xFFFFu) / 32768f) - 1f;
                state = Mathf.Lerp(state, white, lowPass);
                var u = i / (float)count;
                data[i] = state * amp * EdgeEnvelope(i, count) * (1f - u);
            }

            return MakeClip(name, data);
        }

        private static AudioClip BuildBrownNoiseLoop(string name, float duration, float amp)
        {
            var count = Mathf.Max(256, Mathf.RoundToInt(duration * SampleRate));
            var data = new float[count];
            var acc = 0f;
            var seed = 424242u;
            for (var i = 0; i < count; i++)
            {
                seed = seed * 1664525u + 1013904223u;
                var white = ((seed & 0xFFFFu) / 32768f) - 1f;
                acc = Mathf.Clamp(acc * 0.992f + white * 0.04f, -1f, 1f);
                // Smooth crossfade at loop seam
                var fade = EdgeEnvelope(i, count, 0.04f);
                data[i] = acc * amp * fade;
            }

            return MakeClip(name, data);
        }

        private static float EdgeEnvelope(int index, int count, float edgeSec = 0.005f)
        {
            var edge = Mathf.Max(8, Mathf.RoundToInt(edgeSec * SampleRate));
            if (index < edge)
            {
                return index / (float)edge;
            }

            if (index > count - edge)
            {
                return Mathf.Max(0f, (count - index) / (float)edge);
            }

            return 1f;
        }

        private static AudioClip MakeClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
