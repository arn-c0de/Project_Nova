using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Nova.AiLab
{
    /// <summary>
    /// Command line of the AI lab (docs/feature-ideas/AiSimulationEnvironment.md).
    /// LOCAL TOOL, NOT A CONTRIBUTION: it never enters a PR branch, and a green
    /// lab run is DIAGNOSIS, never proof — what was not seen in the running
    /// game is reported as not seen.
    /// </summary>
    public static class Program
    {
        private const string Usage =
            "Nova.AiLab — local AI simulation lab (diagnosis only, never proof)\n" +
            "\n" +
            "  match [options]        run one AI-vs-AI match\n" +
            "  sweep [options]        run a seed matrix across all cores\n" +
            "\n" +
            "Spec:\n" +
            "  --spec <file>          JSON MatchSpec (plan section 3.2); flags below override it\n" +
            "  --seed <ulong>         match seed, decimal or 0x-hex (default 0xA17E57DE57)\n" +
            "  --slots <n>            slot count, 2..4 seats on the canonical map (default 2)\n" +
            "  --ticks <n>            tick budget (default 27000 = VictorySystem.TimeLimitTick)\n" +
            "  --trace-every <n>      metric sample every n ticks (default 0 = off)\n" +
            "  --hash-every <n>       state hash every n ticks (default 0 = end state only)\n" +
            "  --view-every <n>       view frame every n ticks (default 0 = off)\n" +
            "  --fog                  record the fog layer with each view frame\n" +
            "\n" +
            "match:\n" +
            "  --repeat <n>           run the same spec n times and compare the hash chains\n" +
            "  --watch                draw the running match in the terminal (implies --view-every 20)\n" +
            "  --out <dir>            write result.json, trace.ndjson, hashchain.json, view.ndjson, player.html\n" +
            "\n" +
            "sweep:\n" +
            "  --seeds <n>            number of seeds, derived from --seed (default 8)\n" +
            "  --out <dir>            one subdirectory per seed\n" +
            "  --parallel <n>         max concurrent matches (default: processor count)\n";

        public static int Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "-h" || args[0] == "--help")
            {
                Console.WriteLine(Usage);
                return args.Length == 0 ? 1 : 0;
            }

            Options options;
            try
            {
                options = Options.Parse(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"{ex.Message}\n\n{Usage}");
                return 1;
            }

            return args[0] switch
            {
                "match" => RunMatch(options),
                "sweep" => RunSweep(options),
                _ => Fail($"unknown mode '{args[0]}'"),
            };
        }

        private static int Fail(string message)
        {
            Console.Error.WriteLine($"{message}\n\n{Usage}");
            return 1;
        }

        // ----------------------------------------------------------------
        // match
        // ----------------------------------------------------------------

        private static int RunMatch(Options options)
        {
            TerminalView live = null;
            if (options.Watch)
            {
                live = new TerminalView(options.Spec.MapWidth, options.Spec.MapHeight);
                Console.WriteLine(TerminalView.Legend);
            }

            MatchRunResult first = null;
            for (int run = 0; run < options.Repeat; run++)
            {
                // Only the first run is watched: two overlapping live views in
                // one terminal would be unreadable, and the point of --repeat
                // is the hash comparison, not the picture.
                Action<ViewFrame> onFrame = (live != null && run == 0) ? live.Draw : (Action<ViewFrame>)null;
                MatchRunResult result = MatchRun.Execute(options.Spec, onFrame);
                Report(result, run);

                if (options.OutputDirectory != null && run == 0)
                {
                    RunArtifacts.Write(options.OutputDirectory, options.Spec, result);
                    Console.WriteLine($"[run {run}] artifacts written to {options.OutputDirectory}");
                }

                if (first == null)
                {
                    first = result;
                    continue;
                }

                string difference = SweepRunner.Compare(first, result);
                if (difference != null)
                {
                    Console.Error.WriteLine($"NON-DETERMINISTIC: run {run} differs from run 0 — {difference}");
                    return 2;
                }
            }

            if (options.Repeat > 1)
            {
                Console.WriteLine($"determinism: {options.Repeat} runs of seed 0x{options.Spec.Seed:X} agree on every hash");
            }
            return 0;
        }

        private static void Report(MatchRunResult r, int run)
        {
            Console.WriteLine(
                $"[run {run}] seed 0x{r.Seed:X}  slots {r.SlotCount} ({r.AiSlotCount} AI)  " +
                $"outcome {r.Outcome}  winner slot {r.WinnerSlot}  " +
                $"decided tick {r.DecidedTick}  final tick {r.FinalTick}  " +
                $"state hash 0x{r.FinalStateHash:X16}  {r.ElapsedMilliseconds} ms");

            if (!r.IsDecided)
            {
                Console.WriteLine($"[run {run}] undecided within the budget of {r.TickBudget} ticks");
            }
            if (r.Trace.Count > 0 || r.HashChain.Count > 0 || r.View.Count > 0)
            {
                Console.WriteLine($"[run {run}] {r.Trace.Count} metric samples, " +
                                  $"{r.HashChain.Count} hash chain entries, {r.View.Count} view frames");
            }
        }

        // ----------------------------------------------------------------
        // sweep
        // ----------------------------------------------------------------

        private static int RunSweep(Options options)
        {
            ulong[] seeds = SeedSeries.Derive(options.Spec.Seed, options.SeedCount);
            Console.WriteLine(
                $"sweep: {seeds.Length} seeds, {options.Spec.Slots.Length} slots, budget {options.Spec.TickBudget}, " +
                $"parallelism {(options.Parallelism > 0 ? options.Parallelism : Environment.ProcessorCount)}");

            SweepResult sweep = SweepRunner.Run(
                options.Spec, seeds, options.OutputDirectory, options.Parallelism);

            for (int i = 0; i < sweep.Runs.Length; i++)
            {
                MatchRunResult r = sweep.Runs[i];
                Console.WriteLine(
                    $"  seed 0x{r.Seed:X}  {r.Outcome}  winner slot {r.WinnerSlot}  " +
                    $"tick {r.FinalTick}  state hash 0x{r.FinalStateHash:X16}");
            }

            Console.WriteLine(
                $"throughput: {sweep.TotalTicks} ticks in {sweep.WallClockMilliseconds} ms " +
                $"= {sweep.TicksPerSecond} ticks/s across all cores");
            Console.WriteLine(
                $"self-check: {sweep.DoubleCheckedRuns} of {seeds.Length} runs played twice " +
                $"(every {SweepRunner.DoubleCheckEveryNthRun}th), {sweep.Mismatches.Count} mismatches");

            if (sweep.DistinctDecisions == 1 && seeds.Length > 1)
            {
                // Said plainly, because the table above looks like evidence and
                // is not: no simulation system draws from the kernel PRNG, so
                // the seed moves the state hash and nothing else.
                Console.WriteLine(
                    $"NOTE: all {seeds.Length} seeds produced the SAME decision — the seed axis is empty. " +
                    "No simulation system draws from the kernel PRNG today, so a seed changes the state " +
                    "hash but not the match. Variance has to come from profiles (E6) or starting " +
                    "positions, not from seeds.");
            }
            else
            {
                Console.WriteLine($"variance: {sweep.DistinctDecisions} distinct decisions across {seeds.Length} seeds");
            }

            if (sweep.Mismatches.Count > 0)
            {
                // Not a warning. A sweep with a mismatch measured nothing:
                // shared state between parallel matches disguises itself as
                // spread in the numbers, and every result here is suspect.
                Console.Error.WriteLine("SWEEP INVALID — parallel runs disagreed with themselves:");
                foreach (string mismatch in sweep.Mismatches) Console.Error.WriteLine($"  {mismatch}");
                return 2;
            }

            if (options.OutputDirectory != null)
            {
                Console.WriteLine($"artifacts written to {options.OutputDirectory}");
            }
            return 0;
        }

        // ----------------------------------------------------------------
        // Argument parsing
        // ----------------------------------------------------------------

        private sealed class Options
        {
            public MatchSpec Spec;
            public int Repeat = 1;
            public int SeedCount = 8;
            public int Parallelism;
            public string OutputDirectory;
            public bool Watch;

            public static Options Parse(string[] args)
            {
                var options = new Options();
                var flags = new Dictionary<string, string>();
                var switches = new HashSet<string> { "--fog", "--watch" };

                for (int i = 1; i < args.Length; i++)
                {
                    string flag = args[i];
                    if (switches.Contains(flag))
                    {
                        flags[flag] = "true";
                        continue;
                    }
                    if (i + 1 >= args.Length) throw new ArgumentException($"option '{flag}' needs a value");
                    flags[flag] = args[++i];
                }

                // The spec file is the base; explicit flags override it, so a
                // saved spec can be re-run with one number changed without
                // editing the file.
                options.Spec = flags.TryGetValue("--spec", out string specPath)
                    ? SpecFile.Load(specPath)
                    : new MatchSpec();

                int? slots = null;
                foreach (KeyValuePair<string, string> flag in flags)
                {
                    switch (flag.Key)
                    {
                        case "--spec": break;
                        case "--seed": options.Spec.Seed = ParseSeed(flag.Value); break;
                        case "--slots": slots = ParseInt(flag.Value, flag.Key); break;
                        case "--ticks": options.Spec.TickBudget = ParseInt(flag.Value, flag.Key); break;
                        case "--trace-every": options.Spec.TraceIntervalTicks = ParseInt(flag.Value, flag.Key); break;
                        case "--hash-every": options.Spec.HashIntervalTicks = ParseInt(flag.Value, flag.Key); break;
                        case "--view-every": options.Spec.ViewIntervalTicks = ParseInt(flag.Value, flag.Key); break;
                        case "--fog": options.Spec.RecordFog = true; break;
                        case "--watch": options.Watch = true; break;
                        case "--repeat": options.Repeat = ParseInt(flag.Value, flag.Key); break;
                        case "--seeds": options.SeedCount = ParseInt(flag.Value, flag.Key); break;
                        case "--parallel": options.Parallelism = ParseInt(flag.Value, flag.Key); break;
                        case "--out": options.OutputDirectory = flag.Value; break;
                        default: throw new ArgumentException($"unknown option '{flag.Key}'");
                    }
                }

                if (slots.HasValue) options.Spec.Slots = MatchSpec.DefaultSlots(slots.Value);

                // Watching needs frames; 20 ticks = 2 s of simulated time, the
                // AI's own decision cadence, so every frame can differ.
                if (options.Watch && options.Spec.ViewIntervalTicks <= 0) options.Spec.ViewIntervalTicks = 20;

                if (options.Spec.Slots.Length > CanonicalOpening.MaxSeatedSlots)
                {
                    throw new ArgumentException(
                        $"{options.Spec.Slots.Length} slots: the canonical map seats " +
                        $"{CanonicalOpening.MaxSeatedSlots} bases (more seats are map work, plan E11)");
                }
                if (options.Spec.TickBudget < 1) throw new ArgumentException("--ticks must be positive");
                if (options.Repeat < 1) throw new ArgumentException("--repeat must be positive");
                if (options.SeedCount < 1) throw new ArgumentException("--seeds must be positive");

                return options;
            }
        }

        private static ulong ParseSeed(string value)
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

        private static int ParseInt(string value, string flag)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                throw new ArgumentException($"'{value}' is not a valid value for {flag}");
            }
            return parsed;
        }
    }
}
