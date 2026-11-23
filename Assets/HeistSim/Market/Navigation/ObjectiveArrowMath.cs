using UnityEngine;

namespace HeistSim.Market
{
    /// <summary>Resolved screen edge pointer state for the objective arrow.</summary>
    public readonly struct ArrowState
    {
        /// <summary>Creates an arrow state.</summary>
        public ArrowState(bool visible, Vector2 screenPosition, float angleDegrees, float distanceTiles, string label)
        {
            Visible = visible;
            ScreenPosition = screenPosition;
            AngleDegrees = angleDegrees;
            DistanceTiles = distanceTiles;
            Label = label;
        }

        /// <summary>Whether the target is outside the view.</summary>
        public bool Visible { get; }

        /// <summary>Screen point where the arrow is drawn.</summary>
        public Vector2 ScreenPosition { get; }

        /// <summary>Clockwise rotation in degrees from a right pointing arrow.</summary>
        public float AngleDegrees { get; }

        /// <summary>Straight line distance from the player to the target in tiles.</summary>
        public float DistanceTiles { get; }

        /// <summary>Short objective label.</summary>
        public string Label { get; }
    }

    /// <summary>Projects an off screen objective onto the screen edge using the live camera.</summary>
    public static class ObjectiveArrowMath
    {
        /// <summary>Default inset from the screen edge in pixels.</summary>
        public const float EdgeMargin = 56f;

        /// <summary>Computes arrow visibility, edge point, angle and tile distance.</summary>
        public static ArrowState Evaluate(
            Camera camera,
            Vector2 playerTile,
            Vector2 targetTile,
            Vector2 screenSize,
            string label,
            float margin = EdgeMargin)
        {
            var distance = Vector2.Distance(playerTile, targetTile);
            if (camera == null || screenSize.x <= 0f || screenSize.y <= 0f)
            {
                return new ArrowState(false, Vector2.zero, 0f, distance, label);
            }

            var world = new Vector3(targetTile.x, 1f, targetTile.y);
            var viewport = camera.WorldToViewportPoint(world);
            var pixel = new Vector2(viewport.x * screenSize.x, viewport.y * screenSize.y);
            var centre = screenSize * 0.5f;
            var delta = pixel - centre;
            var behind = !camera.orthographic && viewport.z <= 0f;
            if (behind)
            {
                delta = -delta;
            }

            var half = centre - new Vector2(margin, margin);
            if (!behind && Mathf.Abs(delta.x) <= half.x && Mathf.Abs(delta.y) <= half.y)
            {
                return new ArrowState(false, centre, 0f, distance, label);
            }

            if (delta.sqrMagnitude < 1e-6f)
            {
                delta = new Vector2(1f, 0f);
            }

            var scaleX = half.x > 0f ? half.x / Mathf.Abs(delta.x) : 0f;
            var scaleY = half.y > 0f ? half.y / Mathf.Abs(delta.y) : 0f;
            var scale = Mathf.Min(scaleX, scaleY);
            var edge = centre + delta * scale;
            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            return new ArrowState(true, edge, angle, distance, label);
        }
    }
}
