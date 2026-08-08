using System;
using Nova.AI;
using Nova.Simulation.CommandsV1;
using Nova.Simulation.Economy;
using Nova.Simulation.State;
using Nova.Simulation.Victory;

namespace Nova.AiLab
{
    /// <summary>
    /// What one slot is: its faction, and whether a skirmish AI plays it.
    /// A slot with <see cref="IsAi"/> = false is the passive fixture of
    /// SkirmishAiTests — it exists, it owns the canonical opening position,
    /// and nobody ever issues a command for it.
    /// </summary>
    public sealed class SlotSpec
    {
        public byte Slot;
        public FactionId Faction;
        public bool IsAi = true;

        /// <summary>
        /// The AI profile of this slot. The canonical default mirrors
        /// <c>MatchRunner.InitializeMatch</c> exactly (power margin 0, army 12,
        /// squad threshold 6, harvesters 2) — E6 moves these numbers into
        /// <c>AI.Data/</c>, until then they live here as they live in the game.
        /// </summary>
        public AiFactionProfile Profile;

        public static AiFactionProfile CanonicalProfile(FactionId faction) =>
            new AiFactionProfile(faction.ToString(),
                targetPowerMargin: 0,
                targetArmySize: 12,
                attackSquadThreshold: 6,
                targetHarvesterCount: 2);
    }

    /// <summary>
    /// Input contract of one lab run (plan section 3.2). E1 fills it from the
    /// command line; E2 reads it from JSON — the shape is already the one the
    /// spec file describes, so that step adds a reader, not a rewrite.
    /// </summary>
    public sealed class MatchSpec
    {
        public const int SpecVersion = 1;

        public ulong Seed = 0xA17E57DE57UL;

        /// <summary>
        /// Default 27.000 = <see cref="VictorySystem.TimeLimitTick"/>, the
        /// game's own time limit (plan decision 18): a lab result carries no
        /// footnote only while the budget is the game's. Shortening it biases
        /// toward fast strategies.
        /// </summary>
        public int TickBudget = (int)VictorySystem.TimeLimitTick;

        public ushort MapWidth = 128;
        public ushort MapHeight = 128;
        public int EntityCapacity = 1024;
        public long StartingCreditsAE = EconomySystem.CanonicalMatchStartingCreditsAE;

        /// <summary>State hash every n ticks (0 = only the end state).</summary>
        public int HashIntervalTicks;

        public SlotSpec[] Slots = DefaultSlots(2);

        /// <summary>
        /// The canonical seating: slot 0 Alliance, slot 1 Legion — the pairing
        /// MatchRunner, MatchBootstrap and the determinism scenario all use.
        /// Further slots alternate, so a 4-slot free-for-all stays 2v2 by
        /// faction without inventing a third one.
        /// </summary>
        public static SlotSpec[] DefaultSlots(int slotCount)
        {
            if (slotCount < 2 || slotCount > CommandLimits.ReservedPlayerSlots)
            {
                throw new ArgumentOutOfRangeException(nameof(slotCount), slotCount,
                    $"slot count must be in [2, {CommandLimits.ReservedPlayerSlots}]");
            }

            var slots = new SlotSpec[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                FactionId faction = (i % 2) == 0 ? FactionId.Alliance : FactionId.Legion;
                slots[i] = new SlotSpec
                {
                    Slot = (byte)i,
                    Faction = faction,
                    IsAi = true,
                    Profile = SlotSpec.CanonicalProfile(faction),
                };
            }
            return slots;
        }
    }
}
