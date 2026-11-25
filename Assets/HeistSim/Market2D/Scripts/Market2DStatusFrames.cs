using UnityEngine;

namespace HeistSim.Market2D
{
    /// <summary>All 13 visual invincibility/status-frame looks from SpriteCook Technique 14.</summary>
    public enum StatusFrameLook
    {
        Off = 0,
        Blink = 1,
        SeeThrough = 2,
        FadePulse = 3,
        FlashRed = 4,
        FlashWhite = 5,
        DarkFlicker = 6,
        Rainbow = 7,
        Jitter = 8,
        SquashPulse = 9,
        Outline = 10,
        EchoRing = 11,
        ShieldBubble = 12,
        Sparkles = 13,
    }

    /// <summary>
    /// 60 Hz tick-driven status / invincibility-frame visual controller (SpriteCook Technique 14).
    /// Runs for 90 ticks (1.5 s) without restarting on duplicate triggers inside the window,
    /// and restores every modified SpriteRenderer property losslessly on tick 90.
    /// </summary>
    public sealed class Market2DStatusFrames
    {
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        private static readonly int StatusTintColorId = Shader.PropertyToID("_StatusTintColor");
        private static readonly int StatusTintAmountId = Shader.PropertyToID("_StatusTintAmount");
        private static readonly int HighlightColorId = Shader.PropertyToID("_HighlightColor");
        private static readonly int HighlightAmountId = Shader.PropertyToID("_HighlightAmount");

        private static readonly int[] JitterPattern = { -1, 1, 0, 1, -1, 0 };

        private static readonly Color[] RainbowColors =
        {
            new(255f / 255f,  70f / 255f,  70f / 255f, 1f),
            new(255f / 255f, 170f / 255f,  50f / 255f, 1f),
            new(250f / 255f, 230f / 255f,  60f / 255f, 1f),
            new( 90f / 255f, 220f / 255f,  90f / 255f, 1f),
            new( 70f / 255f, 170f / 255f, 255f / 255f, 1f),
            new(190f / 255f, 110f / 255f, 255f / 255f, 1f),
        };

        /// <summary>Window duration in 60 Hz ticks (90 ticks = 1.5 s).</summary>
        public int DurationTicks { get; set; } = 90;

        /// <summary>Active visual look.</summary>
        public StatusFrameLook Look { get; set; } = StatusFrameLook.FlashRed;

        /// <summary>Current tick (-1 when inactive, 0..DurationTicks-1 while active).</summary>
        public int Tick => _k;

        /// <summary>True while inside the active status/i-frame window.</summary>
        public bool IsActive => _k >= 0;

        /// <summary>Current visual jitter X offset in art pixels (0 when inactive).</summary>
        public int JitterOffsetPx { get; private set; }

        /// <summary>Current squash-pulse scale factor (1,1 when inactive).</summary>
        public Vector2 ScaleFactor { get; private set; } = Vector2.one;

        private MaterialPropertyBlock _mpb;
        private SpriteRenderer _target;
        private bool _savedEnabled = true;
        private Color _savedColor = Color.white;
        private int _k = -1;
        private float _accum;

        /// <summary>
        /// Triggers the status window. Returns false (and does not restart) if already active.
        /// </summary>
        public bool Trigger(SpriteRenderer target, StatusFrameLook? lookOverride = null)
        {
            if (lookOverride.HasValue)
            {
                Look = lookOverride.Value;
            }

            if (Look == StatusFrameLook.Off)
            {
                return true;
            }

            if (_k >= 0)
            {
                return false;
            }

            _target = target;
            if (_target != null)
            {
                _savedEnabled = _target.enabled;
                _savedColor = _target.color;
            }

            _k = 0;
            _accum = 0f;
            ApplyLook(0);
            return true;
        }

        /// <summary>Advances the 60 Hz clock by dt seconds.</summary>
        public void Advance(float dt)
        {
            if (_k < 0)
            {
                return;
            }

            _accum += Mathf.Min(dt, 0.1f);
            const float step = 1f / 60f;
            while (_accum >= step && _k >= 0)
            {
                _accum -= step;
                StepTick();
            }
        }

        /// <summary>Advances the window by exactly one 60 Hz tick (public for automated unit tests).</summary>
        public void StepTick()
        {
            if (_k < 0)
            {
                return;
            }

            RestoreSaved();
            _k++;
            if (_k >= DurationTicks)
            {
                _k = -1;
                return;
            }

            ApplyLook(_k);
        }

        /// <summary>Immediately cancels the window and restores all sprite properties.</summary>
        public void Reset()
        {
            if (_k >= 0)
            {
                RestoreSaved();
                _k = -1;
            }

            _accum = 0f;
        }

        /// <summary>Pure visibility rule for Blink mode: hidden when (k / 4) % 2 == 1.</summary>
        public static bool IsBlinkVisible(int k) => ((k / 4) % 2) == 0;

        /// <summary>Pure alpha rule for FadePulse mode: 0.25 + 0.75 * (0.5 + 0.5 * cos(2*pi*k/15)).</summary>
        public static float FadePulseAlpha(int k) => 0.25f + 0.75f * (0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * k / 15f));

        /// <summary>Pure X jitter rule in art pixels: (-1, 1, 0, 1, -1, 0)[(k / 2) % 6].</summary>
        public static int JitterPx(int k) => JitterPattern[(k / 2) % JitterPattern.Length];

        private void RestoreSaved()
        {
            JitterOffsetPx = 0;
            ScaleFactor = Vector2.one;

            if (_target == null)
            {
                return;
            }

            _target.enabled = _savedEnabled;
            _target.color = _savedColor;
            _mpb ??= new MaterialPropertyBlock();
            _target.GetPropertyBlock(_mpb);
            _mpb.SetFloat(FlashAmountId, 0f);
            _mpb.SetFloat(StatusTintAmountId, 0f);
            _mpb.SetFloat(HighlightAmountId, 0f);
            _target.SetPropertyBlock(_mpb);
        }

        private void ApplyLook(int k)
        {
            JitterOffsetPx = 0;
            ScaleFactor = Vector2.one;

            if (_target == null)
            {
                return;
            }

            switch (Look)
            {
                case StatusFrameLook.Blink:
                    _target.enabled = _savedEnabled && IsBlinkVisible(k);
                    break;

                case StatusFrameLook.SeeThrough:
                    _target.color = new Color(_savedColor.r, _savedColor.g, _savedColor.b, _savedColor.a * 0.45f);
                    break;

                case StatusFrameLook.FadePulse:
                    _target.color = new Color(_savedColor.r, _savedColor.g, _savedColor.b, _savedColor.a * FadePulseAlpha(k));
                    break;

                case StatusFrameLook.FlashRed:
                    if ((k / 5) % 2 == 0)
                    {
                        SetStatusTint(new Color(240f / 255f, 46f / 255f, 46f / 255f, 1f), 0.6f);
                    }
                    break;

                case StatusFrameLook.FlashWhite:
                    if ((k / 5) % 2 == 0)
                    {
                        SetFlash(1f);
                    }
                    break;

                case StatusFrameLook.DarkFlicker:
                    if ((k / 5) % 2 == 0)
                    {
                        SetStatusTint(new Color(28f / 255f, 22f / 255f, 44f / 255f, 1f), 0.85f);
                    }
                    break;

                case StatusFrameLook.Rainbow:
                {
                    var col = RainbowColors[(k / 3) % RainbowColors.Length];
                    SetStatusTint(col, 0.6f);
                    break;
                }

                case StatusFrameLook.Jitter:
                    JitterOffsetPx = JitterPx(k);
                    break;

                case StatusFrameLook.SquashPulse:
                {
                    var s = Mathf.Round(2f * Mathf.Sin(2f * Mathf.PI * k / 20f)) * 0.03f;
                    ScaleFactor = new Vector2(1f + s, 1f - s);
                    break;
                }

                case StatusFrameLook.Outline:
                {
                    var col = ((k / 5) % 2 == 0)
                        ? new Color(236f / 255f, 52f / 255f, 52f / 255f, 1f)
                        : new Color(255f / 255f, 210f / 255f, 64f / 255f, 1f);
                    SetOutline(col, 1f);
                    break;
                }

                case StatusFrameLook.EchoRing:
                {
                    var a = k % 12;
                    var alpha = Mathf.Pow(1f - a / 12f, 0.7f);
                    SetOutline(new Color(1f, 1f, 0.94f, 1f), alpha);
                    break;
                }

                case StatusFrameLook.ShieldBubble:
                {
                    var visible = k < DurationTicks - 24 || ((k / 4) % 2 == 0);
                    if (visible)
                    {
                        SetOutline(new Color(0.28f, 0.76f, 0.96f, 1f), 0.85f);
                    }
                    break;
                }

                case StatusFrameLook.Sparkles:
                    SetOutline(new Color(1f, 0.85f, 0.25f, 1f), 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * k / 16f));
                    break;
            }
        }

        private void SetFlash(float amount)
        {
            _mpb ??= new MaterialPropertyBlock();
            _target.GetPropertyBlock(_mpb);
            _mpb.SetFloat(FlashAmountId, amount);
            _target.SetPropertyBlock(_mpb);
        }

        private void SetStatusTint(Color color, float amount)
        {
            _mpb ??= new MaterialPropertyBlock();
            _target.GetPropertyBlock(_mpb);
            _mpb.SetColor(StatusTintColorId, color);
            _mpb.SetFloat(StatusTintAmountId, amount);
            _target.SetPropertyBlock(_mpb);
        }

        private void SetOutline(Color color, float amount)
        {
            _mpb ??= new MaterialPropertyBlock();
            _target.GetPropertyBlock(_mpb);
            _mpb.SetColor(HighlightColorId, color);
            _mpb.SetFloat(HighlightAmountId, amount);
            _target.SetPropertyBlock(_mpb);
        }
    }
}
