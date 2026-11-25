using UnityEngine;

namespace HeistSim.Market2D
{
    /// <summary>
    /// Attaches directly to a loot-bearing fixture GameObject so its stealable highlight,
    /// unlocked vitrine look, and raided/empty sprite swap travel with the real object
    /// when moved, rotated, or deleted in the scene (no detached floating star markers).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class LootableFixture2D : MonoBehaviour
    {
        private static readonly int HighlightColorId = Shader.PropertyToID("_HighlightColor");
        private static readonly int HighlightAmountId = Shader.PropertyToID("_HighlightAmount");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

        /// <summary>Stable MarketData fixture and loot id (e.g. m_aktion1).</summary>
        public string fixtureId;

        /// <summary>True when this loot carries an active RF security tag.</summary>
        public bool isRfTagged;

        /// <summary>True when the spirits vitrine must be forced before stealing.</summary>
        public bool isVitrineGated;

        /// <summary>Stocked fixture sprite shown before stealing.</summary>
        public Sprite stockedSprite;

        /// <summary>Optional unlocked sprite shown after forcing the vitrine but before stealing.</summary>
        public Sprite unlockedSprite;

        /// <summary>Raided/empty fixture sprite shown after the goods have been stolen.</summary>
        public Sprite emptySprite;

        /// <summary>SpriteRenderer on this fixture GameObject.</summary>
        public SpriteRenderer spriteRenderer;

        private MaterialPropertyBlock _mpb;
        private Vector3 _baseScale = Vector3.one;
        private bool _initialized;
        private bool _wasStolen;
        private bool _wasUnlocked;
        private float _popTimer;
        private float _popDuration = 0.22f;
        private float _shaderAccum;
        private bool _hasSyncedOnce;
        private bool _lastStolenParam;
        private bool _lastVitrineOpenParam;
        private bool _lastInRangeParam;

        /// <summary>True once this fixture's loot has been stolen in the current shift.</summary>
        public bool IsStolen => _wasStolen;

        private void Awake()
        {
            EnsureInit();
        }

        private void EnsureInit()
        {
            if (_initialized)
            {
                return;
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            _baseScale = transform.localScale;
            if (_baseScale.sqrMagnitude < 1e-4f)
            {
                _baseScale = Vector3.one;
            }

            _mpb = new MaterialPropertyBlock();
            _initialized = true;
        }

        /// <summary>Resets visual state back to stocked and highlighted for a new shift.</summary>
        public void ResetForShift()
        {
            EnsureInit();
            _wasStolen = false;
            _wasUnlocked = false;
            _popTimer = 0f;
            _shaderAccum = 0f;
            _hasSyncedOnce = false;
            _lastStolenParam = false;
            _lastVitrineOpenParam = false;
            _lastInRangeParam = false;
            transform.localScale = _baseScale;
            if (spriteRenderer != null && stockedSprite != null)
            {
                spriteRenderer.sprite = stockedSprite;
            }

            ApplyShader(0f, RisoPalette.Sun, 3.0f);
        }

        /// <summary>
        /// Synchronizes sprite state (stocked / unlocked / empty), silhouette highlight pulse,
        /// and pickup scale-pop directly on this fixture. Returns true on the exact frame stolen.
        /// </summary>
        public bool SyncState(bool stolen, bool vitrineOpen, bool inRange, float clock, float dt)
        {
            EnsureInit();
            if (spriteRenderer == null)
            {
                return false;
            }

            var justStolen = stolen && !_wasStolen;
            var justUnlocked = isVitrineGated && vitrineOpen && !_wasUnlocked && !stolen;

            if (justStolen)
            {
                _wasStolen = true;
                _popTimer = _popDuration;
            }
            else if (!stolen && _wasStolen)
            {
                _wasStolen = false;
            }

            if (justUnlocked)
            {
                _wasUnlocked = true;
                _popTimer = _popDuration;
            }
            else if (!vitrineOpen)
            {
                _wasUnlocked = false;
            }

            // Pick the exact sprite for stocked / unlocked / stolen-empty state
            if (stolen)
            {
                if (emptySprite != null && spriteRenderer.sprite != emptySprite)
                {
                    spriteRenderer.sprite = emptySprite;
                }
            }
            else if (isVitrineGated && vitrineOpen && unlockedSprite != null)
            {
                if (spriteRenderer.sprite != unlockedSprite)
                {
                    spriteRenderer.sprite = unlockedSprite;
                }
            }
            else if (stockedSprite != null && spriteRenderer.sprite != stockedSprite)
            {
                spriteRenderer.sprite = stockedSprite;
            }

            // Scale-pop feedback directly on the fixture when stolen or unlocked
            if (_popTimer > 0f)
            {
                _popTimer = Mathf.Max(0f, _popTimer - dt);
                var progress = 1f - (_popTimer / _popDuration);
                var pulse = Mathf.Sin(progress * Mathf.PI) * 0.085f;
                transform.localScale = _baseScale * (1f + pulse);
            }
            else if (transform.localScale != _baseScale)
            {
                transform.localScale = _baseScale;
            }

            // Silhouette highlight shader parameters (zero highlight once stolen!)
            _shaderAccum += dt;
            var stateChanged = !_hasSyncedOnce
                || stolen != _lastStolenParam
                || vitrineOpen != _lastVitrineOpenParam
                || inRange != _lastInRangeParam;
            if (stateChanged || (!stolen && _shaderAccum >= 0.05f))
            {
                _hasSyncedOnce = true;
                _lastStolenParam = stolen;
                _lastVitrineOpenParam = vitrineOpen;
                _lastInRangeParam = inRange;
                _shaderAccum = 0f;

                if (stolen)
                {
                    ApplyShader(0f, RisoPalette.Sun, 3.0f);
                }
                else
                {
                    var phase = 0.5f + 0.5f * Mathf.Sin(clock * (inRange ? 6.2f : 2.8f) + (fixtureId?.GetHashCode() ?? 0) * 0.001f);
                    Color col;
                    if (isVitrineGated && !vitrineOpen)
                    {
                        // Locked vitrine gleams between Coral and Sun to signal force-open required
                        col = Color.Lerp(RisoPalette.Coral, RisoPalette.Sun, 0.35f + 0.35f * phase);
                    }
                    else if (isRfTagged)
                    {
                        // RF-tagged high-value loot alternates Sun gold and Coral accent rim
                        col = Color.Lerp(RisoPalette.Sun, RisoPalette.Coral, 0.25f + 0.35f * phase);
                    }
                    else
                    {
                        // Untagged stealable loot glows in crisp warm riso Sun gold
                        col = RisoPalette.Sun;
                    }

                    var amount = inRange
                        ? Mathf.Lerp(0.95f, 1.0f, phase)
                        : Mathf.Lerp(0.84f, 0.98f, phase);
                    var width = inRange ? 7.5f : 5.5f;
                    ApplyShader(amount, col, width);
                }
            }

            return justStolen;
        }

        private void ApplyShader(float amount, Color color, float outlineTexels)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(HighlightAmountId, amount);
            _mpb.SetColor(HighlightColorId, color);
            _mpb.SetFloat(OutlineWidthId, outlineTexels);
            spriteRenderer.SetPropertyBlock(_mpb);
        }
    }
}
