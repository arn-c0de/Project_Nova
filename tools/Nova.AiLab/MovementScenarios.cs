using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Nova.Core;
using Nova.Simulation.CommandsV1;
using Nova.Simulation.Definitions;
using Nova.Simulation.Pathfinding;
using Nova.Simulation.State;

namespace Nova.AiLab
{
    /// <summary>The four movement scenarios of plan section 3.9 (decision 24).</summary>
    public enum MovementScenario
    {
        /// <summary>Group, open field, one target order — ticks to arrival, share arrived, spread.</summary>
        Arrival = 0,

        /// <summary>Two crossing groups, one squeezed through a gap between footprints — who blocks whom.</summary>
        Blocking = 1,

        /// <summary>Ranged units with an attack order on a standing target — do they keep their distance?</summary>
        Standoff = 2,

        /// <summary>Target behind a wall of buildings with one gap — path length against straight line.</summary>
        Detour = 3,
    }

    public sealed class MovementSpec
    {
        public ulong Seed = 0xA17E57DE57UL;
        public MovementScenario Scenario = MovementScenario.Arrival;
        public FactionId Faction = FactionId.Alliance;
        public UnitRole Role = UnitRole.BasicInfantry;
        public int GroupSize = 8;
        public int TickBudget = 1500;
        public ushort MapWidth = 128;
        public ushort MapHeight = 128;
        public int EntityCapacity = 256;

        /// <summary>Ticks a moving unit must stand still to count as blocked.</summary>
        public int StuckTicks = 20;
    }

    public sealed class MovementResult
    {
        public MovementScenario Scenario;
        public FactionId Faction;
        public UnitRole Role;
        public int GroupSize;

        public int Arrived;
        public uint TicksToFirstArrival;
        public uint TicksToLastArrival;

        /// <summary>Largest Chebyshev distance from the target centre at the end — the spread.</summary>
        public int SpreadCells;

        /// <summary>Distance the group started at — so an approach measurement is readable as one.</summary>
        public int StartDistanceCells;

        /// <summary>Straight-line and travelled distance in cells (detour).</summary>
        public int StraightLineCells;
        public int TravelledCells;

        /// <summary>Units that stood still while moving for at least StuckTicks (blocking).</summary>
        public int BlockedUnits;
        public int BlockedTicksTotal;
        public int LongestSingleBlockTicks;

        /// <summary>
        /// Smallest centre distance a ranged unit actually reached, against its
        /// own AttackRange. The OVERSHOOT — how far inside its range it walked
        /// — is the number Issue 03 is about (standoff).
        /// </summary>
        public int ClosestApproachCells;
        public int AttackRangeCells;
        public int OvershootCells => AttackRangeCells - ClosestApproachCells;

        public int RejectedOrders;
        public uint FinalTick;
        public ulong FinalStateHash;
    }

    /// <summary>
    /// One group, one order, obstacles placed as DATA rather than code (plan
    /// section 3.9). Runs in seconds, so a movement question is a loop of
    /// seconds instead of a match to evaluate.
    /// <para>
    /// Since v1.1.0 <c>Simulation/Pathfinding/</c> is ours as well — flow field
    /// and <c>CostField</c> included — so the whole way from the order to the
    /// arrival lies in our own scope, and this run mode covers it.
    /// </para>
    /// <para>
    /// <see cref="MovementScenario.Standoff"/> needs combat in the run and is
    /// therefore a mixed scenario. That is intended: keeping distance IS a
    /// combat property, not a pure movement topic.
    /// </para>
    /// </summary>
    public static class MovementScenarios
    {
        /// <summary>A unit counts as arrived within this Chebyshev distance of the target cell.</summary>
        private const int ArrivalToleranceCells = 3;

        public static MovementResult Run(MovementSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));

            var matchSpec = new MatchSpec
            {
                Seed = spec.Seed,
                TickBudget = spec.TickBudget,
                MapWidth = spec.MapWidth,
                MapHeight = spec.MapHeight,
                EntityCapacity = spec.EntityCapacity,
                CountIntents = true,
                Slots = new[]
                {
                    new SlotSpec { Slot = 0, Faction = spec.Faction, Controller = SlotController.Scripted },
                    new SlotSpec { Slot = 1, Faction = Other(spec.Faction), Controller = SlotController.Scripted },
                },
            };

            MultiSlotAiHost host = MultiSlotAiHost.Build(matchSpec);
            var result = new MovementResult
            {
                Scenario = spec.Scenario,
                Faction = spec.Faction,
                Role = spec.Role,
                GroupSize = spec.GroupSize,
            };

            switch (spec.Scenario)
            {
                case MovementScenario.Arrival: RunArrival(host, spec, result); break;
                case MovementScenario.Blocking: RunBlocking(host, spec, result); break;
                case MovementScenario.Standoff: RunStandoff(host, spec, result); break;
                default: RunDetour(host, spec, result); break;
            }

            for (int i = 0; i < host.Peers.Length; i++)
            {
                if (host.Peers[i].IntentCounter != null) result.RejectedOrders += host.Peers[i].IntentCounter.Rejected;
            }
            result.FinalTick = host.Kernel.CurrentTick.Value;
            result.FinalStateHash = host.Kernel.CalculateStateHash();
            return result;
        }

        private static FactionId Other(FactionId faction) =>
            faction == FactionId.Alliance ? FactionId.Legion : FactionId.Alliance;

        // ================================================================
        // arrival — the baseline: does a group get there, and how tidily
        // ================================================================

        private static void RunArrival(MultiSlotAiHost host, MovementSpec spec, MovementResult result)
        {
            var group = SpawnGroup(host, 0, spec.Faction, spec.Role, spec.GroupSize, 20, 64);
            const int targetX = 100, targetY = 64;

            Order(host, 0, group, targetX, targetY);
            WatchUntilArrived(host, spec, group, targetX, targetY, result);
        }

        // ================================================================
        // blocking — the scenario Issue 03 is really about
        // ================================================================

        private static void RunBlocking(MultiSlotAiHost host, MovementSpec spec, MovementResult result)
        {
            // A gap between two footprints: the big group has to file through
            // it while a second group crosses their path.
            // A ONE-CELL gap: three cells wide let eight units walk through
            // abreast and nothing ever queued. The bottleneck has to actually
            // be one, or "no blocking" is a property of the scenario rather
            // than of the movement code.
            PlaceWall(host, 1, spec.Faction, spec.MapHeight, columnX: 64, gapY: 64, gapHeight: 1);

            // The main group is deliberately larger than the caller's default:
            // a queue needs a crowd.
            int mainSize = Math.Max(spec.GroupSize, 16);
            result.GroupSize = mainSize;
            var main = SpawnGroup(host, 0, spec.Faction, spec.Role, mainSize, 50, 64);
            var crossing = SpawnGroup(host, 0, spec.Faction, spec.Role, Math.Max(4, spec.GroupSize / 2), 55, 54);

            const int targetX = 100, targetY = 64;
            Order(host, 0, main, targetX, targetY);
            // Straight across the main group's path to the gap.
            Order(host, 0, crossing, 55, 74);

            var all = new List<uint>(main);
            all.AddRange(crossing);
            all.Sort();

            WatchBlocking(host, spec, all, main, targetX, targetY, result);
            MeasureArrival(host, main, targetX, targetY, result);
        }

        // ================================================================
        // standoff — does a ranged unit stop at its own range?
        // ================================================================

        private static void RunStandoff(MultiSlotAiHost host, MovementSpec spec, MovementResult result)
        {
            if (!SimDefinitions.TryGetUnit(spec.Faction, spec.Role, out SimUnitDefinition def))
            {
                throw new ArgumentException($"unknown unit definition ({spec.Faction}, {spec.Role})");
            }
            result.AttackRangeCells = def.AttackRangeTiles;

            // THE SHOOTERS MUST START WELL OUTSIDE THEIR OWN RANGE, otherwise
            // the "closest approach" is just the spawn distance and the whole
            // scenario measures nothing. The first version spawned them 8 cells
            // from a 20-cell-range target and dutifully reported 8.
            int startDistance = Math.Max(30, def.AttackRangeTiles * 2);
            int targetX = 64;
            int shooterX = targetX - startDistance;
            result.StartDistanceCells = startDistance;

            var shooters = SpawnGroup(host, 0, spec.Faction, spec.Role, spec.GroupSize, shooterX, 64);

            // A standing target of the opposing slot. Huge health, so the
            // measurement is the approach and not how fast the target dies.
            SimDefinitions.TryGetUnit(Other(spec.Faction), UnitRole.BasicInfantry, out SimUnitDefinition targetDef);
            EntityId target = host.Entities.SpawnUnit(
                1, new Transform2D(SimFixed.FromInt(targetX), SimFixed.FromInt(64)),
                targetDef.MoveSpeed, maxHealth: 100000, role: targetDef.Role);

            uint targetRaw = UnitCommandStateView.ToRawEntityId(target);
            SlotPeer peer = host.PeerOf(0);

            // A MOVE order onto the target, plus the attack order. Attack alone
            // does not close the distance — measured: artillery given only
            // AttackTarget on a target 40 cells away never moved a single cell
            // and never fired. That is GB-002 in practice (no attack-move), and
            // it means the approach has to be ordered explicitly, exactly as
            // the AI does it.
            //
            // What this then measures is Issue 03's question: with a 20-cell
            // gun and an order to walk onto the enemy, how far in does the unit
            // actually go before it stops shooting from range?
            Order(host, 0, shooters, targetX, 64);
            peer.Ingress.TrySubmitIntent(
                CommandIntent.Create(new AttackTargetPayload(shooters.ToArray(), targetRaw)), out _);

            // The closest approach is a running minimum: the interesting moment
            // is the deepest point, not where the unit happens to end up.
            result.ClosestApproachCells = int.MaxValue;
            for (int i = 0; i < spec.TickBudget; i++)
            {
                host.Step();
                if (!host.Entities.TryGetUnit(target, out UnitState targetState) || !targetState.IsActive) break;

                int targetCellX = SimFixed.WorldToGrid(targetState.Transform.PositionX);
                int targetCellY = SimFixed.WorldToGrid(targetState.Transform.PositionY);

                foreach (uint raw in shooters)
                {
                    if (!TryReadUnit(host, raw, out UnitState shooter)) continue;
                    int distance = Chebyshev(
                        SimFixed.WorldToGrid(shooter.Transform.PositionX),
                        SimFixed.WorldToGrid(shooter.Transform.PositionY),
                        targetCellX, targetCellY);
                    if (distance < result.ClosestApproachCells) result.ClosestApproachCells = distance;
                }
            }

            if (result.ClosestApproachCells == int.MaxValue) result.ClosestApproachCells = 0;
        }

        // ================================================================
        // detour — flow field and CostField, directly
        // ================================================================

        private static void RunDetour(MultiSlotAiHost host, MovementSpec spec, MovementResult result)
        {
            PlaceWall(host, 1, spec.Faction, spec.MapHeight, columnX: 64, gapY: 20, gapHeight: 3);

            var group = SpawnGroup(host, 0, spec.Faction, spec.Role, spec.GroupSize, 40, 100);
            const int targetX = 90, targetY = 100;

            result.StraightLineCells = Chebyshev(40, 100, targetX, targetY);
            Order(host, 0, group, targetX, targetY);
            WatchUntilArrived(host, spec, group, targetX, targetY, result);
        }

        // ================================================================
        // Shared measurement
        // ================================================================

        private static void WatchUntilArrived(MultiSlotAiHost host, MovementSpec spec, List<uint> group,
            int targetX, int targetY, MovementResult result)
        {
            var lastCell = new Dictionary<uint, (int X, int Y)>();
            long travelled = 0;

            for (int i = 0; i < spec.TickBudget; i++)
            {
                host.Step();

                foreach (uint raw in group)
                {
                    if (!TryReadUnit(host, raw, out UnitState unit)) continue;
                    int x = SimFixed.WorldToGrid(unit.Transform.PositionX);
                    int y = SimFixed.WorldToGrid(unit.Transform.PositionY);

                    if (lastCell.TryGetValue(raw, out (int X, int Y) previous))
                    {
                        travelled += Chebyshev(previous.X, previous.Y, x, y);
                    }
                    lastCell[raw] = (x, y);
                }

                int arrived = CountArrived(host, group, targetX, targetY);
                if (arrived > 0 && result.TicksToFirstArrival == 0)
                {
                    result.TicksToFirstArrival = host.Kernel.CurrentTick.Value;
                }
                if (arrived == group.Count)
                {
                    result.TicksToLastArrival = host.Kernel.CurrentTick.Value;
                    break;
                }
            }

            result.TravelledCells = (int)(group.Count > 0 ? travelled / group.Count : 0);
            MeasureArrival(host, group, targetX, targetY, result);
        }

        private static void MeasureArrival(MultiSlotAiHost host, List<uint> group, int targetX, int targetY,
            MovementResult result)
        {
            result.Arrived = CountArrived(host, group, targetX, targetY);

            int spread = 0;
            foreach (uint raw in group)
            {
                if (!TryReadUnit(host, raw, out UnitState unit)) continue;
                int distance = Chebyshev(
                    SimFixed.WorldToGrid(unit.Transform.PositionX),
                    SimFixed.WorldToGrid(unit.Transform.PositionY),
                    targetX, targetY);
                if (distance > spread) spread = distance;
            }
            result.SpreadCells = spread;
        }

        /// <summary>
        /// Counts units that claim to be moving while their cell does not
        /// change — the operational definition of "blocked" from section 3.9.
        /// </summary>
        private static void WatchBlocking(MultiSlotAiHost host, MovementSpec spec, List<uint> group,
            List<uint> arrivalGroup, int targetX, int targetY, MovementResult result)
        {
            var lastCell = new Dictionary<uint, (int X, int Y)>();
            var stillFor = new Dictionary<uint, int>();
            var everBlocked = new HashSet<uint>();

            for (int i = 0; i < spec.TickBudget; i++)
            {
                host.Step();

                foreach (uint raw in group)
                {
                    if (!TryReadUnit(host, raw, out UnitState unit)) continue;
                    int x = SimFixed.WorldToGrid(unit.Transform.PositionX);
                    int y = SimFixed.WorldToGrid(unit.Transform.PositionY);

                    bool sameCell = lastCell.TryGetValue(raw, out (int X, int Y) previous)
                                    && previous.X == x && previous.Y == y;
                    lastCell[raw] = (x, y);

                    if (unit.IsMoving && sameCell)
                    {
                        int run = stillFor.TryGetValue(raw, out int held) ? held + 1 : 1;
                        stillFor[raw] = run;
                        if (run >= spec.StuckTicks)
                        {
                            everBlocked.Add(raw);
                            result.BlockedTicksTotal++;
                            if (run > result.LongestSingleBlockTicks) result.LongestSingleBlockTicks = run;
                        }
                    }
                    else
                    {
                        stillFor[raw] = 0;
                    }
                }

                // Arrival time is the readable half of this scenario: 16 units
                // through a one-cell gap against 8 across open ground is the
                // comparison that says whether the bottleneck cost anything.
                int arrived = CountArrived(host, arrivalGroup, targetX, targetY);
                if (arrived > 0 && result.TicksToFirstArrival == 0)
                {
                    result.TicksToFirstArrival = host.Kernel.CurrentTick.Value;
                }
                if (arrived == arrivalGroup.Count && result.TicksToLastArrival == 0)
                {
                    result.TicksToLastArrival = host.Kernel.CurrentTick.Value;
                }
            }

            result.BlockedUnits = everBlocked.Count;
        }

        private static int CountArrived(MultiSlotAiHost host, List<uint> group, int targetX, int targetY)
        {
            int arrived = 0;
            foreach (uint raw in group)
            {
                if (!TryReadUnit(host, raw, out UnitState unit)) continue;
                if (Chebyshev(SimFixed.WorldToGrid(unit.Transform.PositionX),
                              SimFixed.WorldToGrid(unit.Transform.PositionY),
                              targetX, targetY) <= ArrivalToleranceCells)
                {
                    arrived++;
                }
            }
            return arrived;
        }

        // ================================================================
        // Setup helpers — obstacles are data, not code
        // ================================================================

        private static List<uint> SpawnGroup(MultiSlotAiHost host, byte slot, FactionId faction, UnitRole role,
            int count, int originX, int originY)
        {
            if (!SimDefinitions.TryGetUnit(faction, role, out SimUnitDefinition def))
            {
                throw new ArgumentException($"unknown unit definition ({faction}, {role})");
            }

            var raws = new List<uint>(count);
            for (int i = 0; i < count; i++)
            {
                EntityId id = host.Entities.SpawnUnit(
                    slot,
                    new Transform2D(SimFixed.FromInt(originX - i / 5), SimFixed.FromInt(originY - 2 + i % 5)),
                    def.MoveSpeed, maxHealth: def.MaxHealth, role: def.Role);
                raws.Add(UnitCommandStateView.ToRawEntityId(id));
            }
            raws.Sort();
            return raws;
        }

        /// <summary>
        /// A wall of completed buildings with one gap. Footprints have been
        /// impassable since the troop-command sprint, so this is a real
        /// obstacle for the cost field — no special terrain needed.
        /// </summary>
        private static void PlaceWall(MultiSlotAiHost host, byte slot, FactionId faction, int mapHeight,
            int columnX, int gapY, int gapHeight)
        {
            // THE WALL SPANS THE WHOLE MAP. A partial wall is not a bottleneck:
            // the first version was 60 cells tall on a 128-cell map and the
            // group simply walked around it, which is why "blocking" reported
            // zero blocked units — a property of the scenario, not of the
            // movement code.
            ushort defId = SimDefinitions.ToDefinitionId(faction, UnitRole.Power);
            int step = SimDefinitions.BuildingFootprintCells;
            int top = mapHeight - step;
            int bottom = 0;

            for (int y = bottom; y <= top; y += step)
            {
                if (y + step > gapY && y < gapY + gapHeight) continue; // the gap
                host.Construction.PlaceCompletedBuilding(slot, defId, columnX, y);
            }
        }

        private static void Order(MultiSlotAiHost host, byte slot, List<uint> raws, int targetX, int targetY)
        {
            SlotPeer peer = host.PeerOf(slot);
            if (peer == null || raws.Count == 0) return;

            const int chunk = CommandLimits.MaxEntityIdsPerCommand;
            for (int start = 0; start < raws.Count; start += chunk)
            {
                int length = Math.Min(chunk, raws.Count - start);
                var ids = new uint[length];
                raws.CopyTo(start, ids, 0, length);
                peer.Ingress.TrySubmitIntent(
                    CommandIntent.Create(new MovePayload(ids, SimFixed.FromInt(targetX), SimFixed.FromInt(targetY))),
                    out _);
            }
        }

        private static bool TryReadUnit(MultiSlotAiHost host, uint raw, out UnitState unit)
        {
            EntityId id = UnitCommandStateView.ToEntityId(raw);
            return host.Entities.TryGetUnit(id, out unit) && unit.IsActive;
        }

        private static int Chebyshev(int ax, int ay, int bx, int by) =>
            Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));

        // ================================================================

        public static string ToNdjson(IReadOnlyList<MovementResult> results)
        {
            var output = new StringBuilder(results.Count * 256);
            foreach (MovementResult r in results)
            {
                output.Append("{\"scenario\":\"").Append(r.Scenario)
                      .Append("\",\"faction\":\"").Append(r.Faction)
                      .Append("\",\"role\":\"").Append(r.Role)
                      .Append("\",\"groupSize\":").Append(r.GroupSize)
                      .Append(",\"arrived\":").Append(r.Arrived)
                      .Append(",\"ticksToFirstArrival\":").Append(r.TicksToFirstArrival)
                      .Append(",\"ticksToLastArrival\":").Append(r.TicksToLastArrival)
                      .Append(",\"spreadCells\":").Append(r.SpreadCells)
                      .Append(",\"startDistanceCells\":").Append(r.StartDistanceCells)
                      .Append(",\"straightLineCells\":").Append(r.StraightLineCells)
                      .Append(",\"travelledCells\":").Append(r.TravelledCells)
                      .Append(",\"blockedUnits\":").Append(r.BlockedUnits)
                      .Append(",\"blockedTicksTotal\":").Append(r.BlockedTicksTotal)
                      .Append(",\"longestSingleBlockTicks\":").Append(r.LongestSingleBlockTicks)
                      .Append(",\"closestApproachCells\":").Append(r.ClosestApproachCells)
                      .Append(",\"attackRangeCells\":").Append(r.AttackRangeCells)
                      .Append(",\"overshootCells\":").Append(r.OvershootCells)
                      .Append(",\"rejectedOrders\":").Append(r.RejectedOrders)
                      .Append(",\"finalTick\":").Append(r.FinalTick)
                      .Append(",\"finalStateHash\":\"0x")
                      .Append(r.FinalStateHash.ToString("X16", CultureInfo.InvariantCulture))
                      .Append("\"}\n");
            }
            return output.ToString();
        }
    }
}
