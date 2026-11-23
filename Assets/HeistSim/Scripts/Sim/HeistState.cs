using System;
using System.Collections.Generic;

namespace HeistSim
{
    /// <summary>Alert tier on the heat scale.</summary>
    public readonly struct HeatTier
    {
        /// <summary>Creates a tier with its threshold and display text.</summary>
        public HeatTier(float min, string label, string note, string css)
        {
            Min = min;
            Label = label;
            Note = note;
            Css = css;
        }

        /// <summary>Minimum heat at which this tier applies.</summary>
        public float Min { get; }

        /// <summary>Human readable tier name.</summary>
        public string Label { get; }

        /// <summary>One line description of guard behaviour at this tier.</summary>
        public string Note { get; }

        /// <summary>Style key used by the browser game.</summary>
        public string Css { get; }
    }

    /// <summary>Objective tracked by a heist, with a stable id and completion state.</summary>
    public sealed class HeistObjective
    {
        /// <summary>Creates an incomplete objective with the given id.</summary>
        public HeistObjective(string id)
        {
            Id = id;
            Done = false;
        }

        /// <summary>Stable objective identifier.</summary>
        public string Id { get; }

        /// <summary>Whether the objective is complete.</summary>
        public bool Done { get; internal set; }
    }

    /// <summary>Timed interaction in progress.</summary>
    public sealed class ChannelState
    {
        /// <summary>Creates a channel at zero progress.</summary>
        public ChannelState(string kind, string id, float hold)
        {
            Kind = kind;
            Id = id;
            Hold = hold;
            Progress = 0f;
        }

        /// <summary>Action kind, for example steal or disable.</summary>
        public string Kind { get; }

        /// <summary>Target object identifier.</summary>
        public string Id { get; }

        /// <summary>Seconds of hold accumulated so far.</summary>
        public float Progress { get; internal set; }

        /// <summary>Seconds of hold needed to finish.</summary>
        public float Hold { get; }
    }

    /// <summary>Result of advancing the active channel by one tick.</summary>
    public enum ChannelResult
    {
        /// <summary>No channel was active.</summary>
        None,
        /// <summary>The channel gained progress and is still running.</summary>
        Running,
        /// <summary>The channel reached its hold time and cleared.</summary>
        Done,
        /// <summary>The channel was interrupted and cleared without progress.</summary>
        Cancelled,
    }

    /// <summary>Mutable heist state: heat, alert tier, bag, objectives and the timed channel.</summary>
    public sealed class HeistState
    {
        /// <summary>Minimum hold time for any channel, in seconds.</summary>
        public const float MinChannelHold = 0.2f;

        /// <summary>Heat decay per second while standing and unseen.</summary>
        public const float StandingDecayRate = 0.07f;

        /// <summary>Heat decay per second while hidden or crouched and unseen.</summary>
        public const float HiddenDecayRate = 0.12f;

        /// <summary>Alert tiers in ascending threshold order.</summary>
        public static readonly HeatTier[] Tiers =
        {
            new HeatTier(0.00f, "Calm", "routine shelf patrol", "calm"),
            new HeatTier(0.12f, "Curious", "a noise caught their ear", "curious"),
            new HeatTier(0.32f, "Suspicious", "cameras tracking your route", "suspicious"),
            new HeatTier(0.52f, "Searching", "detective sweeping the zone", "searching"),
            new HeatTier(0.72f, "Lockdown", "security on full alert", "lockdown"),
            new HeatTier(0.90f, "Exposure", "one more sighting ends it", "exposure"),
        };

        private readonly List<string> _bag = new();
        private readonly List<HeistObjective> _objectives = new();

        /// <summary>Creates a heist at the browser game starting heat with the given bag capacity and objectives.</summary>
        public HeistState(int bagCap = 3, IEnumerable<string> objectiveIds = null)
        {
            Heat = 0.12f;
            Tier = 1;
            Phase = "case";
            BagCap = bagCap != 0 ? bagCap : 3;
            CamerasDisabled = false;
            VitrineOpen = false;
            HadTags = false;
            Elapsed = 0f;
            Channel = null;

            if (objectiveIds == null)
            {
                return;
            }

            foreach (var id in objectiveIds)
            {
                _objectives.Add(new HeistObjective(id));
            }
        }

        /// <summary>Current heat in the range 0 to 1.</summary>
        public float Heat { get; private set; }

        /// <summary>Alert tier index that matches the current heat.</summary>
        public int Tier { get; private set; }

        /// <summary>Bag capacity in item slots.</summary>
        public int BagCap { get; }

        /// <summary>Ids of the goods currently in the bag.</summary>
        public IReadOnlyList<string> Bag => _bag;

        /// <summary>Objectives for this heist in declaration order.</summary>
        public IReadOnlyList<HeistObjective> Objectives => _objectives;

        /// <summary>Whether the CCTV cameras are disabled.</summary>
        public bool CamerasDisabled { get; set; }

        /// <summary>Whether the display case is open.</summary>
        public bool VitrineOpen { get; set; }

        /// <summary>Whether the thief has carried an RF tagged good.</summary>
        public bool HadTags { get; set; }

        /// <summary>Coarse phase label used by the browser game.</summary>
        public string Phase { get; set; }

        /// <summary>Elapsed heist time in seconds.</summary>
        public float Elapsed { get; set; }

        /// <summary>Active timed interaction, or null when idle.</summary>
        public ChannelState Channel { get; private set; }

        /// <summary>Returns the tier index for a heat value.</summary>
        public static int TierOf(float heat)
        {
            var tier = 0;
            for (var i = 1; i < Tiers.Length; i++)
            {
                if (heat >= Tiers[i].Min)
                {
                    tier = i;
                }
            }

            return tier;
        }

        /// <summary>Sets heat, clamps it to 0 to 1, and updates the tier.</summary>
        public float SetHeat(float value)
        {
            var clean = float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
            Heat = Math.Clamp(clean, 0f, 1f);
            Tier = TierOf(Heat);
            return Heat;
        }

        /// <summary>Adds a heat delta, clamps the result, and updates the tier.</summary>
        public float AddHeat(float amount)
        {
            return SetHeat(Heat + amount);
        }

        /// <summary>Decays heat at the hidden rate when hidden, otherwise at the standing rate.</summary>
        public float Decay(float dt, bool hidden)
        {
            var rate = hidden ? HiddenDecayRate : StandingDecayRate;
            return AddHeat(-rate * dt);
        }

        /// <summary>Number of goods in the bag.</summary>
        public int BagCount => _bag.Count;

        /// <summary>Whether the bag has room for another good.</summary>
        public bool CanCarry => _bag.Count < BagCap;

        /// <summary>Adds a good to the bag when there is room and reports success.</summary>
        public bool PutInBag(string id)
        {
            if (!CanCarry)
            {
                return false;
            }

            _bag.Add(id);
            return true;
        }

        /// <summary>Finds an objective by id, or null when absent.</summary>
        public HeistObjective Objective(string id)
        {
            return _objectives.Find(o => o.Id == id);
        }

        /// <summary>Marks an incomplete objective done and reports whether this call changed it.</summary>
        public bool CompleteObjective(string id)
        {
            var objective = Objective(id);
            if (objective == null || objective.Done)
            {
                return false;
            }

            objective.Done = true;
            return true;
        }

        /// <summary>Whether every objective except escape is complete.</summary>
        public bool RequiredMet()
        {
            foreach (var objective in _objectives)
            {
                if (objective.Id != "escape" && !objective.Done)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Starts a timed channel, clamping the hold to the minimum.</summary>
        public ChannelState StartChannel(string kind, string id, float holdSeconds)
        {
            Channel = new ChannelState(kind, id, Math.Max(MinChannelHold, holdSeconds));
            return Channel;
        }

        /// <summary>Clears the active channel without completing it.</summary>
        public void CancelChannel()
        {
            Channel = null;
        }

        /// <summary>Advances the active channel, honouring interruption before progress.</summary>
        public ChannelResult TickChannel(float dt, bool interrupted)
        {
            var channel = Channel;
            if (channel == null)
            {
                return ChannelResult.None;
            }

            if (interrupted)
            {
                Channel = null;
                return ChannelResult.Cancelled;
            }

            channel.Progress += dt;
            if (channel.Progress >= channel.Hold)
            {
                Channel = null;
                return ChannelResult.Done;
            }

            return ChannelResult.Running;
        }
    }
}
