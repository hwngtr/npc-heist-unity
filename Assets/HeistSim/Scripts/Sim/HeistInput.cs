namespace HeistSim
{
    /// <summary>One tick of player input for the market game loop.</summary>
    public readonly struct HeistInput
    {
        /// <summary>Creates one tick of input from a move direction and button states.</summary>
        /// <param name="moveI">Normalized move direction along the tile I axis.</param>
        /// <param name="moveJ">Normalized move direction along the tile J axis.</param>
        /// <param name="action">Whether the interact button is held this tick.</param>
        /// <param name="crouch">Whether the crouch button is held this tick.</param>
        /// <param name="sprint">Whether the sprint button is held this tick.</param>
        public HeistInput(float moveI, float moveJ, bool action = false, bool crouch = false, bool sprint = false)
        {
            MoveI = moveI;
            MoveJ = moveJ;
            Action = action;
            Crouch = crouch;
            Sprint = sprint;
        }

        /// <summary>Normalized move direction along the tile I axis.</summary>
        public float MoveI { get; }

        /// <summary>Normalized move direction along the tile J axis.</summary>
        public float MoveJ { get; }

        /// <summary>Whether the interact button is held, mapped to the browser game's E and Space.</summary>
        public bool Action { get; }

        /// <summary>Whether the crouch button is held, mapped to the browser game's C and Control.</summary>
        public bool Crouch { get; }

        /// <summary>Whether the sprint button is held, mapped to the browser game's Shift.</summary>
        public bool Sprint { get; }

        /// <summary>Input with no movement and no buttons held.</summary>
        public static HeistInput None => default;

        /// <summary>Whether the direction has a non zero component.</summary>
        public bool HasDirection => MoveI != 0f || MoveJ != 0f;
    }
}
