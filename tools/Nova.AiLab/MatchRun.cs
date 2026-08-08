using System;
using System.Collections.Generic;
using Nova.Simulation.Definitions;
using Nova.Simulation.Victory;

namespace Nova.AiLab
{
    /// <summary>One entry of the state hash chain (plan section 3.5, proof 3).</summary>
    public readonly struct HashChainEntry
    {
        public readonly uint Tick;
        public readonly ulong StateHash;

        public HashChainEntry(uint tick, ulong stateHash)
        {
            Tick = tick;
            StateHash = stateHash;
        }
    }

    /// <summary>
    /// Output contract of one lab run (plan section 3.2). Integers only — no
    /// float ever leaves the simulation, otherwise comparing two runs is luck
    /// instead of arithmetic.
    /// </summary>
    public sealed class MatchRunResult
    {
        public int SpecVersion = MatchSpec.SpecVersion;
        public ulong Seed;
        public int SlotCount;
        public int AiSlotCount;
        public int TickBudget;

        public MatchOutcome Outcome;
        public byte WinnerSlot;
        public uint DecidedTick;
        public uint FinalTick;

        public ulong FinalStateHash;
        public ulong DefinitionsHash64;

        /// <summary>Empty unless <see cref="MatchSpec.HashIntervalTicks"/> is set.</summary>
        public List<HashChainEntry> HashChain = new List<HashChainEntry>();

        public bool IsDecided => Outcome != MatchOutcome.Undecided;
    }

    /// <summary>
    /// Drives one match to its end and collects the E1 result: outcome, winner,
    /// deciding tick and end-state hash — plus the hash chain when the spec
    /// asks for one.
    /// <para>
    /// The chain is the diagnostic that section 8 of the plan wants from the
    /// lab: when a behaviour change turns a baseline red it shows the TICK at
    /// which two builds diverge, not just THAT they do.
    /// </para>
    /// </summary>
    public static class MatchRun
    {
        public static MatchRunResult Execute(MatchSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));

            MultiSlotAiHost host = MultiSlotAiHost.BuildMatch(spec);
            var result = new MatchRunResult
            {
                Seed = spec.Seed,
                SlotCount = host.SlotCount,
                AiSlotCount = host.AiPeers.Length,
                TickBudget = spec.TickBudget,
                DefinitionsHash64 = SimDefinitions.ComputeDefinitionsHash64(),
            };

            bool chained = spec.HashIntervalTicks > 0;
            if (chained)
            {
                result.HashChain.Add(new HashChainEntry(host.Kernel.CurrentTick.Value, host.Kernel.CalculateStateHash()));
            }

            for (int i = 0; i < spec.TickBudget && !host.Victory.IsDecided; i++)
            {
                host.Step();

                uint tick = host.Kernel.CurrentTick.Value;
                if (chained && tick % (uint)spec.HashIntervalTicks == 0)
                {
                    result.HashChain.Add(new HashChainEntry(tick, host.Kernel.CalculateStateHash()));
                }
            }

            result.Outcome = host.Victory.Outcome;
            result.WinnerSlot = host.Victory.WinnerSlot;
            result.DecidedTick = host.Victory.DecidedTick;
            result.FinalTick = host.Kernel.CurrentTick.Value;
            result.FinalStateHash = host.Kernel.CalculateStateHash();
            return result;
        }
    }
}
