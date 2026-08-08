using System;
using System.Collections.Generic;
using System.IO;
using Nova.Simulation.State;

namespace Nova.AiLab
{
    /// <summary>
    /// <c>movement</c> — the four scenarios (arrival, blocking, standoff,
    /// detour) for both factions. A row whose orders were refused is printed
    /// to stderr and is not a measurement.
    /// </summary>
    internal static class MovementCommand
    {
        public static int Run(Options options)
        {
            var results = new List<MovementResult>();
            foreach (MovementScenario scenario in new[]
                     {
                         MovementScenario.Arrival, MovementScenario.Blocking,
                         MovementScenario.Standoff, MovementScenario.Detour,
                     })
            foreach (FactionId faction in DuelTable.Factions)
            {
                // Standoff only makes sense for a unit that HAS a range worth
                // keeping; running it on melee-range infantry would measure
                // nothing and read like a result.
                UnitRole role = scenario == MovementScenario.Standoff ? UnitRole.Artillery : UnitRole.BasicInfantry;
                results.Add(MovementScenarios.Run(new MovementSpec
                {
                    Scenario = scenario,
                    Faction = faction,
                    Role = role,
                    GroupSize = options.GroupSize,
                    TickBudget = options.Spec.TickBudget,
                }));
            }

            foreach (MovementResult r in results)
            {
                Console.WriteLine($"{r.Scenario,-9} {r.Faction,-8} {r.Role,-14} " +
                                  $"arrived {r.Arrived}/{r.GroupSize}  spread {r.SpreadCells,3}  " +
                                  $"first/last {r.TicksToFirstArrival,5}/{r.TicksToLastArrival,5}  " +
                                  Detail(r));
                if (r.RejectedOrders > 0)
                {
                    Console.Error.WriteLine($"  {r.RejectedOrders} orders refused — this row is not a measurement");
                }
            }

            if (options.OutputDirectory != null)
            {
                Directory.CreateDirectory(options.OutputDirectory);
                string path = Path.Combine(options.OutputDirectory, "movement.ndjson");
                File.WriteAllText(path, MovementScenarios.ToNdjson(results));
                Console.WriteLine($"results written to {path}");
            }
            return 0;
        }

        private static string Detail(MovementResult r) => r.Scenario switch
        {
            MovementScenario.Blocking =>
                $"blocked {r.BlockedUnits} units, {r.BlockedTicksTotal} tick-units, longest {r.LongestSingleBlockTicks}",
            MovementScenario.Standoff =>
                $"from {r.StartDistanceCells} in to {r.ClosestApproachCells}, range {r.AttackRangeCells} — overshoot {r.OvershootCells}",
            MovementScenario.Detour =>
                $"straight {r.StraightLineCells}, travelled {r.TravelledCells}",
            _ => $"travelled {r.TravelledCells}",
        };
    }
}
