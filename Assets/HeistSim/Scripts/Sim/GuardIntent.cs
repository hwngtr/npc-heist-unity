using UnityEngine;

namespace HeistSim
{
    /// <summary>Discrete motor command and FSM state emitted by a guard controller.</summary>
    public enum GuardAction
    {
        /// <summary>Stand still and wait.</summary>
        Idle,
        /// <summary>Move along the patrol route.</summary>
        Patrol,
        /// <summary>Move to a sound or last known position.</summary>
        Investigate,
        /// <summary>Move toward the player.</summary>
        Chase,
        /// <summary>Attack the player in range.</summary>
        Attack,
        /// <summary>Scan the area around the last known position.</summary>
        Search,
        /// <summary>Move back to the route start.</summary>
        Return,
    }

    /// <summary>Immutable command returned by a guard controller.</summary>
    public readonly struct GuardIntent
    {
        /// <summary>Creates an intent with a target and speed.</summary>
        public GuardIntent(GuardAction action, Vector2? target = null, float speed = 1f)
        {
            Action = action;
            Target = target;
            Speed = speed;
        }

        /// <summary>Requested action or state.</summary>
        public GuardAction Action { get; }

        /// <summary>World target in tiles, if any.</summary>
        public Vector2? Target { get; }

        /// <summary>Movement speed in tiles per tick.</summary>
        public float Speed { get; }
    }
}
