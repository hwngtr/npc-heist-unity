using UnityEngine;

namespace HeistSim.Market2D
{
    /// <summary>
    /// Stamina fatigue and loot haul-weight encumbrance model for Market2D.
    /// Carrying more stolen goods gently slows movement (never sluggish, max ~25% at 6/6 items)
    /// and increases sprint stamina drain so the player must pace sprints on a heavy haul.
    /// </summary>
    public sealed class Market2DStamina
    {
        /// <summary>Maximum stamina pool.</summary>
        public const float MaxStamina = 100f;

        /// <summary>Recovery threshold required to exit the Winded state.</summary>
        public const float WindedRecoveryThreshold = 25f;

        /// <summary>Current stamina (0..100).</summary>
        public float Stamina { get; private set; } = MaxStamina;

        /// <summary>True after hitting 0 stamina until recovering to WindedRecoveryThreshold.</summary>
        public bool IsWinded { get; private set; }

        /// <summary>Normalized stamina (0..1).</summary>
        public float Normalized => Mathf.Clamp01(Stamina / MaxStamina);

        /// <summary>Resets stamina to full for a new shift.</summary>
        public void Reset()
        {
            Stamina = MaxStamina;
            IsWinded = false;
        }

        /// <summary>
        /// Gentle haul-weight speed multiplier: 100% empty, ~87% at 3 items, ~75% at full 6/6 bag.
        /// </summary>
        public static float HaulSpeedMultiplier(int bagCount)
        {
            var clamped = Mathf.Clamp(bagCount, 0, 6);
            return Mathf.Clamp(1f - clamped * 0.042f, 0.72f, 1f);
        }

        /// <summary>
        /// Sprint stamina drain per second, scaling up with carried items (+22% drain per item).
        /// </summary>
        public static float SprintDrainPerSecond(int bagCount)
        {
            var clamped = Mathf.Clamp(bagCount, 0, 6);
            return 26f * (1f + clamped * 0.22f);
        }

        /// <summary>
        /// Steps stamina and returns whether sprint is allowed, the effective movement speed multiplier,
        /// and whether the thief just became Winded on this frame.
        /// </summary>
        public (bool AllowSprint, float SpeedScale, bool JustWinded) Step(
            bool wantsSprint,
            bool moving,
            bool crouching,
            int bagCount,
            float dt)
        {
            dt = Mathf.Max(0f, dt);
            var justWinded = false;
            var allowSprint = wantsSprint && moving && !crouching && !IsWinded && Stamina > 0.5f;

            if (allowSprint)
            {
                Stamina = Mathf.Max(0f, Stamina - SprintDrainPerSecond(bagCount) * dt);
                if (Stamina <= 0.01f)
                {
                    Stamina = 0f;
                    IsWinded = true;
                    allowSprint = false;
                    justWinded = true;
                }
            }
            else
            {
                // Standing still recovers fastest, crouching second, walking slowest
                var regen = !moving ? 34f : (crouching ? 20f : 14f);
                // Heavy bag slightly slows breath recovery
                var weightFactor = 1f - Mathf.Clamp(bagCount, 0, 6) * 0.05f;
                Stamina = Mathf.Min(MaxStamina, Stamina + regen * weightFactor * dt);
                if (IsWinded && Stamina >= WindedRecoveryThreshold)
                {
                    IsWinded = false;
                }
            }

            var speedScale = HaulSpeedMultiplier(bagCount);
            if (IsWinded)
            {
                speedScale *= 0.88f;
            }

            return (allowSprint, speedScale, justWinded);
        }
    }
}
