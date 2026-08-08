using System;
using System.Diagnostics;
using System.Globalization;
using Nova.Simulation.State;

namespace Nova.AiLab
{
    /// <summary>
    /// Command line of the AI lab (docs/feature-ideas/AiSimulationEnvironment.md).
    /// LOCAL TOOL, NOT A CONTRIBUTION: it never enters a PR branch, and a green
    /// lab run is DIAGNOSIS, never proof — what was not seen in the running
    /// game is reported as not seen.
    /// <para>
    /// E1 knows one run mode, <c>match</c>. E2 replaces these flags with the
    /// JSON MatchSpec and adds the parallel sweep; the types behind them
    /// already have the spec's shape.
    /// </para>
    /// </summary>
    public static class Program
    {
        private const string Usage =
            "Nova.AiLab — local AI simulation lab (diagnosis only, never proof)\n" +
            "\n" +
            "  match [options]     run one AI-vs-AI match and report outcome and state hash\n" +
            "\n" +
            "Options:\n" +
            "  --seed <ulong>      match seed, decimal or 0x-hex (default 0xA17E57DE57)\n" +
            "  --slots <n>         number of slots, 2..4 seats on the canonical map (default 2)\n" +
            "  --ticks <n>         tick budget (default 27000 = VictorySystem.TimeLimitTick)\n" +
            "  --hash-every <n>    state hash every n ticks (default 0 = end state only)\n" +
            "  --repeat <n>        run the same spec n times and compare the hash chains\n";

        public static int Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "-h" || args[0] == "--help")
            {
                Console.WriteLine(Usage);
                return args.Length == 0 ? 1 : 0;
            }

            if (args[0] != "match")
            {
                Console.Error.WriteLine($"unknown mode '{args[0]}'\n\n{Usage}");
                return 1;
            }

            MatchSpec spec;
            int repeat;
            try
            {
                spec = ParseSpec(args, out repeat);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"{ex.Message}\n\n{Usage}");
                return 1;
            }

            return RunMatch(spec, repeat);
        }

        private static int RunMatch(MatchSpec spec, int repeat)
        {
            MatchRunResult first = null;
            for (int run = 0; run < repeat; run++)
            {
                var watch = Stopwatch.StartNew();
                MatchRunResult result = MatchRun.Execute(spec);
                watch.Stop();

                Report(result, run, watch.ElapsedMilliseconds);

                if (first == null)
                {
                    first = result;
                    continue;
                }

                if (!SameRun(first, result, out string difference))
                {
                    Console.Error.WriteLine($"NON-DETERMINISTIC: run {run} differs from run 0 — {difference}");
                    return 2;
                }
            }

            if (repeat > 1)
            {
                Console.WriteLine($"determinism: {repeat} runs of seed 0x{spec.Seed:X} agree on every hash");
            }
            return 0;
        }

        private static void Report(MatchRunResult r, int run, long elapsedMs)
        {
            Console.WriteLine(
                $"[run {run}] seed 0x{r.Seed:X}  slots {r.SlotCount} ({r.AiSlotCount} AI)  " +
                $"outcome {r.Outcome}  winner slot {r.WinnerSlot}  " +
                $"decided tick {r.DecidedTick}  final tick {r.FinalTick}  " +
                $"state hash 0x{r.FinalStateHash:X16}  definitions 0x{r.DefinitionsHash64:X16}  " +
                $"{elapsedMs} ms");

            if (!r.IsDecided)
            {
                Console.WriteLine($"[run {run}] undecided within the budget of {r.TickBudget} ticks");
            }
            if (r.HashChain.Count > 0)
            {
                Console.WriteLine($"[run {run}] hash chain: {r.HashChain.Count} entries");
            }
        }

        /// <summary>
        /// Two runs of one spec must agree on everything, not just the end
        /// state: a chain that diverges and reconverges is exactly the kind of
        /// shared-state bug the sampling double-runs of plan section 3.7 hunt.
        /// </summary>
        private static bool SameRun(MatchRunResult a, MatchRunResult b, out string difference)
        {
            if (a.FinalStateHash != b.FinalStateHash)
            {
                difference = $"end state 0x{a.FinalStateHash:X16} vs 0x{b.FinalStateHash:X16}";
                return false;
            }
            if (a.DecidedTick != b.DecidedTick || a.Outcome != b.Outcome || a.WinnerSlot != b.WinnerSlot)
            {
                difference = $"decision {a.Outcome}@{a.DecidedTick}/slot {a.WinnerSlot} vs " +
                             $"{b.Outcome}@{b.DecidedTick}/slot {b.WinnerSlot}";
                return false;
            }
            if (a.HashChain.Count != b.HashChain.Count)
            {
                difference = $"chain length {a.HashChain.Count} vs {b.HashChain.Count}";
                return false;
            }
            for (int i = 0; i < a.HashChain.Count; i++)
            {
                if (a.HashChain[i].StateHash == b.HashChain[i].StateHash) continue;
                difference = $"chain diverges at tick {a.HashChain[i].Tick}: " +
                             $"0x{a.HashChain[i].StateHash:X16} vs 0x{b.HashChain[i].StateHash:X16}";
                return false;
            }

            difference = null;
            return true;
        }

        // ----------------------------------------------------------------
        // Argument parsing
        // ----------------------------------------------------------------

        private static MatchSpec ParseSpec(string[] args, out int repeat)
        {
            var spec = new MatchSpec();
            int slots = 2;
            repeat = 1;

            for (int i = 1; i < args.Length; i++)
            {
                string flag = args[i];
                string value = i + 1 < args.Length ? args[i + 1] : null;
                if (value == null) throw new ArgumentException($"option '{flag}' needs a value");
                i++;

                switch (flag)
                {
                    case "--seed": spec.Seed = ParseUInt64(value); break;
                    case "--slots": slots = ParseInt32(value, flag); break;
                    case "--ticks": spec.TickBudget = ParseInt32(value, flag); break;
                    case "--hash-every": spec.HashIntervalTicks = ParseInt32(value, flag); break;
                    case "--repeat": repeat = ParseInt32(value, flag); break;
                    default: throw new ArgumentException($"unknown option '{flag}'");
                }
            }

            if (slots > CanonicalOpening.MaxSeatedSlots)
            {
                throw new ArgumentException(
                    $"--slots {slots}: the canonical map seats {CanonicalOpening.MaxSeatedSlots} bases " +
                    "(more seats are map work, plan E11)");
            }
            if (spec.TickBudget < 1) throw new ArgumentException("--ticks must be positive");
            if (repeat < 1) throw new ArgumentException("--repeat must be positive");

            spec.Slots = MatchSpec.DefaultSlots(slots);
            return spec;
        }

        private static ulong ParseUInt64(string value)
        {
            bool hex = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            string digits = hex ? value.Substring(2) : value;
            NumberStyles style = hex ? NumberStyles.HexNumber : NumberStyles.Integer;
            if (!ulong.TryParse(digits, style, CultureInfo.InvariantCulture, out ulong parsed))
            {
                throw new ArgumentException($"'{value}' is not a valid seed");
            }
            return parsed;
        }

        private static int ParseInt32(string value, string flag)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                throw new ArgumentException($"'{value}' is not a valid value for {flag}");
            }
            return parsed;
        }
    }
}
