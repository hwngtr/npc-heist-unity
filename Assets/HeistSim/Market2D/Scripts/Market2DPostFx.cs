using System;
using UnityEngine;

namespace HeistSim.Market2D
{
    /// <summary>All 8 SpriteCook pixel-art screen transition styles (Technique 19).</summary>
    public enum ScreenTransitionStyle
    {
        Fade = 0,
        SteppedFade = 1,
        BayerDither = 2,
        Pixelate = 3,
        Iris = 4,
        Wipe = 5,
        Diamonds = 6,
        Shutters = 7,
    }

    /// <summary>
    /// Unified post-processing controller on the Market2D camera combining:
    /// - Technique 09: 4-row Heat-Tier / Shift Mood Color LUT (Calm -> Alert -> Chase -> Lockdown)
    /// - Technique 19: 8 Screen Transitions with exact 84-tick timing (33 cover, 9 black, 33 uncover, 9 hold)
    /// - Technique 22: Toggleable CRT / Retro Print-Monitor Shader pass (F8)
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public sealed class Market2DPostFx : MonoBehaviour
    {
        private static readonly int MoodLutId = Shader.PropertyToID("_MoodLut");
        private static readonly int MoodPhaseId = Shader.PropertyToID("_MoodPhase");
        private static readonly int MoodStrengthId = Shader.PropertyToID("_MoodStrength");
        private static readonly int CrtEnabledId = Shader.PropertyToID("_CrtEnabled");
        private static readonly int TransStyleId = Shader.PropertyToID("_TransStyle");
        private static readonly int TransProgressId = Shader.PropertyToID("_TransProgress");
        private static readonly int TransReverseId = Shader.PropertyToID("_TransReverse");
        private static readonly int TransCenterId = Shader.PropertyToID("_TransCenter");
        private static readonly int TransPixelSizeId = Shader.PropertyToID("_TransPixelSize");
        private static readonly int TransColorId = Shader.PropertyToID("_TransColor");

        /// <summary>Cover duration in 60 Hz ticks (0.55 s).</summary>
        public const int CoverTicks = 33;

        /// <summary>Full-black hold ticks while swapping/resetting shift (0.15 s).</summary>
        public const int HoldBlackTicks = 9;

        /// <summary>Uncover duration in 60 Hz ticks (0.55 s).</summary>
        public const int UncoverTicks = 33;

        /// <summary>Final settle ticks before input resumes (0.15 s). Total paused = 84 ticks.</summary>
        public const int PostHoldTicks = 9;

        /// <summary>Total ticks in one full transition cycle.</summary>
        public const int TotalTransitionTicks = CoverTicks + HoldBlackTicks + UncoverTicks + PostHoldTicks;

        /// <summary>Serialized material using HeistSim/Market2DPostFx.</summary>
        public Material postMaterial;

        /// <summary>Active screen transition style (default: BayerDither).</summary>
        public ScreenTransitionStyle transitionStyle = ScreenTransitionStyle.BayerDither;

        /// <summary>Enable/disable the retro CRT monitor pass (default false to keep crisp riso print).</summary>
        public bool crtEnabled;

        /// <summary>Strength of the Heat-Tier Mood Color LUT (0 = off, 0.55 = subtle editorial atmosphere).</summary>
        [Range(0f, 1f)]
        public float moodLutStrength = 0.45f;

        private Texture2D _moodLutTex;
        private float _moodPhase;
        private float _targetMoodPhase;

        private int _transTick = -1;
        private float _tickAccum;
        private float _transProgress;
        private bool _transReverse;
        private Vector2 _transCenterUv = new(0.37f, 0.5f);
        private Action _onMidpointSwap;

        /// <summary>True while a screen transition is actively covering/uncovering.</summary>
        public bool IsTransitioning => _transTick >= 0;

        /// <summary>Current normalized transition cover progress (0 = clear, 1 = fully covered).</summary>
        public float TransitionProgress => _transProgress;

        /// <summary>Current transition tick (-1 when idle, 0..84 while running).</summary>
        public int TransitionTick => _transTick;

        /// <summary>Current mood LUT row phase (0 = Calm, 1 = Suspicious, 2 = Alert, 3 = Lockdown).</summary>
        public float MoodPhase => _moodPhase;

        private void OnEnable()
        {
            EnsureResources();
        }

        private void OnDisable()
        {
            if (_moodLutTex != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_moodLutTex);
                }
                else
                {
                    DestroyImmediate(_moodLutTex);
                }

                _moodLutTex = null;
            }
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            var dt = Time.unscaledDeltaTime;
            _moodPhase = Mathf.Lerp(_moodPhase, _targetMoodPhase, 1f - Mathf.Exp(-3.5f * dt));

            if (_transTick >= 0)
            {
                _tickAccum += dt;
                const float tickStep = 1f / 60f;
                while (_tickAccum >= tickStep && _transTick >= 0)
                {
                    _tickAccum -= tickStep;
                    StepTransitionTick();
                }
            }
        }

        /// <summary>Sets target Heat-Tier Mood LUT phase from simulation heat (0..100) and guard state.</summary>
        public void SetHeatMood(float heat, string guardState)
        {
            if (guardState == "chase" || guardState == "attack")
            {
                _targetMoodPhase = Mathf.Clamp(2.2f + (heat / 100f) * 0.8f, 2.0f, 3.0f);
            }
            else if (guardState == "investigate" || guardState == "search")
            {
                _targetMoodPhase = Mathf.Clamp(0.8f + (heat / 100f) * 0.9f, 0.7f, 1.8f);
            }
            else
            {
                // At calm start (heat <= 10), keep phase at 0.0 so daylight riso colors are 100% pure
                _targetMoodPhase = heat <= 12f ? 0f : Mathf.Clamp((heat - 12f) / 40f, 0f, 1.5f);
            }
        }

        /// <summary>
        /// Starts an 84-tick screen transition (ignores duplicate requests while active).
        /// Invokes onMidpointSwap on tick 34 while the screen is 100% covered.
        /// </summary>
        public bool BeginTransition(Action onMidpointSwap, Vector2? centerUv = null, ScreenTransitionStyle? styleOverride = null)
        {
            if (_transTick >= 0)
            {
                return false;
            }

            if (styleOverride.HasValue)
            {
                transitionStyle = styleOverride.Value;
            }

            _transCenterUv = centerUv ?? new Vector2(0.37f, 0.5f);
            _onMidpointSwap = onMidpointSwap;
            _transTick = 0;
            _tickAccum = 0f;
            _transProgress = 0f;
            _transReverse = false;
            return true;
        }

        /// <summary>Advances the screen transition state machine by exactly one 60 Hz tick (public for unit tests).</summary>
        public void StepTransitionTick()
        {
            if (_transTick < 0)
            {
                return;
            }

            _transTick++;
            if (_transTick <= CoverTicks)
            {
                _transProgress = _transTick / (float)CoverTicks;
                _transReverse = false;
            }
            else if (_transTick <= CoverTicks + HoldBlackTicks)
            {
                _transProgress = 1f;
                _transReverse = false;
                if (_transTick == CoverTicks + 1)
                {
                    var cb = _onMidpointSwap;
                    _onMidpointSwap = null;
                    cb?.Invoke();
                }
            }
            else if (_transTick <= CoverTicks + HoldBlackTicks + UncoverTicks)
            {
                var u = (_transTick - (CoverTicks + HoldBlackTicks)) / (float)UncoverTicks;
                _transProgress = Mathf.Clamp01(1f - u);
                _transReverse = true;
            }
            else if (_transTick >= TotalTransitionTicks)
            {
                _transTick = -1;
                _transProgress = 0f;
                _transReverse = false;
            }
            else
            {
                _transProgress = 0f;
                _transReverse = false;
            }
        }

        /// <summary>Toggles the CRT post-process pass and returns the new state.</summary>
        public bool ToggleCrt()
        {
            crtEnabled = !crtEnabled;
            return crtEnabled;
        }

        /// <summary>Cycles through the 8 transition styles and returns the new style name.</summary>
        public string CycleTransitionStyle()
        {
            transitionStyle = (ScreenTransitionStyle)(((int)transitionStyle + 1) % 8);
            return transitionStyle.ToString();
        }

        /// <summary>Ensures shader material and 4-row Riso Mood Color LUT texture are initialized.</summary>
        public void EnsureResources()
        {
            if (postMaterial == null)
            {
                var shader = Shader.Find("HeistSim/Market2DPostFx");
                if (shader != null)
                {
                    postMaterial = new Material(shader) { name = "Market2DPostFxMat" };
                }
            }

            if (_moodLutTex == null)
            {
                _moodLutTex = BuildRisoMoodLutTexture();
            }
        }

        /// <summary>
        /// Builds the 32x4 palette-faithful Riso Mood LUT texture (T09):
        /// Row 0: Calm Daylight Cream (identity neutral)
        /// Row 1: Suspicious Warm Ochre / Afternoon Tension
        /// Row 2: Alert Coral / High Suspicion
        /// Row 3: Lockdown Cool Navy-Indigo Shadows + Warm Highlight Warning
        /// </summary>
        public static Texture2D BuildRisoMoodLutTexture()
        {
            const int width = 32;
            const int rows = 4;
            var tex = new Texture2D(width, rows, TextureFormat.RGBA32, mipChain: false, linear: false)
            {
                name = "RisoMoodLut_32x4",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            for (var x = 0; x < width; x++)
            {
                var luma = x / (float)(width - 1);

                // Row 0: Identity daylight (neutral white multiplier)
                tex.SetPixel(x, 0, Color.white);

                // Row 1: Suspicious warm amber paper shift
                var r1 = Color.Lerp(new Color(0.88f, 0.85f, 0.94f), new Color(1.03f, 0.96f, 0.84f), luma);
                tex.SetPixel(x, 1, r1);

                // Row 2: Alert coral-rose tension
                var r2 = Color.Lerp(new Color(0.78f, 0.72f, 0.86f), new Color(1.06f, 0.86f, 0.82f), luma);
                tex.SetPixel(x, 2, r2);

                // Row 3: Lockdown cool indigo shadows with warm siren highlights (OKLab-inspired night shift)
                var r3 = Color.Lerp(new Color(0.56f, 0.60f, 0.80f), new Color(1.04f, 0.82f, 0.78f), luma);
                tex.SetPixel(x, 3, r3);
            }

            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return tex;
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            EnsureResources();

            var needsPass = postMaterial != null && (crtEnabled || _transProgress > 0.001f || (_moodPhase > 0.001f && moodLutStrength > 0.001f));
            if (!needsPass)
            {
                Graphics.Blit(source, destination);
                return;
            }

            postMaterial.SetTexture(MoodLutId, _moodLutTex);
            postMaterial.SetFloat(MoodPhaseId, _moodPhase);
            postMaterial.SetFloat(MoodStrengthId, moodLutStrength);
            postMaterial.SetFloat(CrtEnabledId, crtEnabled ? 1f : 0f);
            postMaterial.SetFloat(TransStyleId, (float)transitionStyle);
            postMaterial.SetFloat(TransProgressId, _transProgress);
            postMaterial.SetFloat(TransReverseId, _transReverse ? 1f : 0f);
            postMaterial.SetVector(TransCenterId, new Vector4(_transCenterUv.x, _transCenterUv.y, 0f, 0f));
            postMaterial.SetFloat(TransPixelSizeId, 3f);
            postMaterial.SetColor(TransColorId, RisoPalette.InkBlue);

            Graphics.Blit(source, destination, postMaterial);
        }
    }
}
