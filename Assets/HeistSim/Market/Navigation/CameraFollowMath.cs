using UnityEngine;

namespace HeistSim.Market
{
    /// <summary>Numerically safe camera smoothing helpers used by the market runner.</summary>
    public static class CameraFollowMath
    {
        /// <summary>Returns a finite non negative delta time clamped to the given maximum step.</summary>
        public static float SafeDelta(float dt, float maxDelta)
        {
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0f)
            {
                return 0f;
            }

            var limit = float.IsNaN(maxDelta) || float.IsInfinity(maxDelta) || maxDelta <= 0f
                ? MarketRunner.MaxDeltaTime
                : maxDelta;
            return Mathf.Min(dt, limit);
        }

        /// <summary>Returns an exponential lerp factor in the range 0 to 1.</summary>
        public static float LerpFactor(float dt, float rate, float maxDelta)
        {
            var step = SafeDelta(dt, maxDelta);
            var safeRate = float.IsNaN(rate) || float.IsInfinity(rate) || rate <= 0f ? 0f : rate;
            return Mathf.Clamp01(1f - Mathf.Exp(-safeRate * step));
        }

        /// <summary>Moves a position toward a target with no overshoot and no non finite result.</summary>
        public static Vector3 Smooth(Vector3 current, Vector3 target, float dt, float rate, float maxDelta)
        {
            var factor = LerpFactor(dt, rate, maxDelta);
            var result = Vector3.Lerp(current, target, factor);
            if (float.IsNaN(result.x) || float.IsNaN(result.y) || float.IsNaN(result.z)
                || float.IsInfinity(result.x) || float.IsInfinity(result.y) || float.IsInfinity(result.z))
            {
                return current;
            }

            return result;
        }
    }
}
