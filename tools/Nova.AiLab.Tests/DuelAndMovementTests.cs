using System.Collections.Generic;
using NUnit.Framework;
using Nova.Simulation.State;

namespace Nova.AiLab.Tests
{
    /// <summary>
    /// E5 acceptance suite: the two narrow run modes.
    /// <para>
    /// The condition all three run modes share is the one worth pinning:
    /// IDENTICAL SYSTEM REGISTRATION. The arena registers economy,
    /// construction and production too — they simply tick over empty tables. A
    /// dropped system would be a different tick order and therefore a
    /// different game, and the measurement would describe something that does
    /// not exist.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class DuelAndMovementTests
    {
        private static readonly string[] CanonicalG1Order =
        {
            "Nova.Simulation.Economy.EconomySystem",
            "Nova.Simulation.Construction.ConstructionSystem",
            "Nova.Simulation.Production.ProductionSystem",
            "Nova.Simulation.Pathfinding.PathfindingSystem",
            "Nova.Simulation.Movement.MovementSystem",
            "Nova.Simulation.Vision.FogOfWarSystem",
            "Nova.Simulation.Combat.CombatSystem",
            "Nova.Simulation.Victory.VictorySystem",
        };

        // ================================================================
        // (a) THE SHARED CONDITION
        // ================================================================

        [Test]
        public void AnArenaHostRegistersTheCanonicalSystems_WithoutAnAi()
        {
            var spec = new MatchSpec
            {
                TickBudget = 100,
                Slots = new[]
                {
                    new SlotSpec { Slot = 0, Faction = FactionId.Alliance, Controller = SlotController.Scripted },
                    new SlotSpec { Slot = 1, Faction = FactionId.Legion, Controller = SlotController.Scripted },
                },
            };

            MultiSlotAiHost host = MultiSlotAiHost.Build(spec);

            var names = new List<string>();
            for (int i = 0; i < host.Kernel.Systems.Count; i++) names.Add(host.Kernel.Systems[i].GetType().FullName);

            Assert.That(names, Is.EqualTo(CanonicalG1Order),
                "the arena runs the canonical G1 order — economy, construction and production included, " +
                "ticking over empty tables — and no AI system, because the scenario gives the orders");
            Assert.That(host.AiSlotCount, Is.EqualTo(0));
            foreach (SlotPeer peer in host.Peers)
            {
                Assert.That(peer.System, Is.Null, "a scripted slot decides nothing on its own");
                Assert.That(peer.Ingress, Is.Not.Null, "but it owns a seat to submit through");
            }
        }

        // ================================================================
        // (b) DUEL
        // ================================================================

        [Test]
        public void ADuelIsReproducible()
        {
            DuelSpec Spec() => new DuelSpec
            {
                FactionA = FactionId.Alliance, RoleA = UnitRole.LightTank,
                FactionB = FactionId.Legion, RoleB = UnitRole.BasicInfantry,
                Range = DuelRange.Contact, TickBudget = 1500,
            };

            DuelResult first = DuelArena.Run(Spec());
            DuelResult second = DuelArena.Run(Spec());

            Assert.That(second.FinalStateHash, Is.EqualTo(first.FinalStateHash));
            Assert.That(second.DecidedTick, Is.EqualTo(first.DecidedTick));
            Assert.That(second.Winner, Is.EqualTo(first.Winner));
        }

        [Test]
        public void ParityIsOverCostNotCount()
        {
            // Decision 20: equal counts would be no finding at all. The cheap
            // side must field MORE units for the same AE.
            DuelResult result = DuelArena.Run(new DuelSpec
            {
                FactionA = FactionId.Alliance, RoleA = UnitRole.BasicInfantry,
                FactionB = FactionId.Alliance, RoleB = UnitRole.BattleTank,
                Range = DuelRange.Contact, TickBudget = 1200,
            });

            Assert.That(result.CountA, Is.GreaterThan(result.CountB),
                "the cheaper role must field more units for the same budget");
            Assert.That(result.SpentA, Is.LessThanOrEqualTo(result.BudgetAE));
            Assert.That(result.SpentB, Is.LessThanOrEqualTo(result.BudgetAE));
            Assert.That(result.CountB, Is.GreaterThanOrEqualTo(4),
                "the plan asks for at least four units a side (section 3.9)");
        }

        [Test]
        public void TheSiegeEchelonAttacksExactlyOneBuilding()
        {
            DuelResult result = DuelArena.Run(new DuelSpec
            {
                FactionA = FactionId.Alliance, RoleA = UnitRole.AntiArmorInfantry,
                FactionB = FactionId.Alliance, RoleB = UnitRole.Barracks,
                SiegeEchelon = true, Range = DuelRange.Contact, TickBudget = 1500,
            });

            Assert.That(result.CountB, Is.EqualTo(1),
                "the siege echelon measures ticks to demolition against one building — sizing the " +
                "target by budget made the building count swing from 6 to 12 and the times incomparable");
            Assert.That(result.Decided, Is.True);
            Assert.That(result.SurvivorsB, Is.EqualTo(0), "explosive infantry must bring a Barracks down");

            TestContext.Out.WriteLine(
                $"[duel] {result.CountA} AntiArmorInfantry ({result.SpentA} AE) razed a Barracks " +
                $"in {result.DecidedTick} ticks");
        }

        [Test]
        public void ExplosiveOutDamagesKineticAgainstBuildings()
        {
            // The matrix says kinetic hits Building at 30% and explosive at 75%
            // — a factor of 2,5. The duel shows what reload, range and hit
            // points make of that, which is the whole point of measuring.
            DuelResult kinetic = DuelArena.Run(new DuelSpec
            {
                FactionA = FactionId.Alliance, RoleA = UnitRole.BasicInfantry,
                FactionB = FactionId.Alliance, RoleB = UnitRole.Power,
                SiegeEchelon = true, Range = DuelRange.Contact, TickBudget = 3000,
            });
            DuelResult explosive = DuelArena.Run(new DuelSpec
            {
                FactionA = FactionId.Alliance, RoleA = UnitRole.AntiArmorInfantry,
                FactionB = FactionId.Alliance, RoleB = UnitRole.Power,
                SiegeEchelon = true, Range = DuelRange.Contact, TickBudget = 3000,
            });

            Assert.That(kinetic.Decided && explosive.Decided, Is.True);
            Assert.That(explosive.DecidedTick, Is.LessThan(kinetic.DecidedTick));

            TestContext.Out.WriteLine(
                $"[duel] Power plant razed: kinetic {kinetic.DecidedTick} ticks, " +
                $"explosive {explosive.DecidedTick} ticks — factor " +
                $"{(kinetic.DecidedTick / (explosive.DecidedTick == 0 ? 1 : explosive.DecidedTick))}x " +
                "(the matrix multiplier alone predicts 2,5x)");
        }

        [Test]
        public void TheTablePlansBothDirectionsOfEveryPairing()
        {
            List<DuelSpec> plan = DuelTable.Plan(unitsPerSide: 4, tickBudget: 500);

            var seen = new HashSet<string>();
            foreach (DuelSpec s in plan)
            {
                if (s.SiegeEchelon) continue;
                seen.Add($"{s.FactionA}.{s.RoleA}>{s.FactionB}.{s.RoleB}@{s.Range}");
            }

            foreach (DuelSpec s in plan)
            {
                if (s.SiegeEchelon) continue;
                string mirrored = $"{s.FactionB}.{s.RoleB}>{s.FactionA}.{s.RoleA}@{s.Range}";
                Assert.That(seen, Does.Contain(mirrored),
                    "the duel asymmetry makes A-vs-B and B-vs-A two different measurements");
            }
        }

        // ================================================================
        // (c) MOVEMENT
        // ================================================================

        [Test]
        public void EveryMovementScenarioRunsWithoutARefusedOrder()
        {
            foreach (MovementScenario scenario in new[]
                     {
                         MovementScenario.Arrival, MovementScenario.Blocking,
                         MovementScenario.Standoff, MovementScenario.Detour,
                     })
            {
                MovementResult result = MovementScenarios.Run(new MovementSpec
                {
                    Scenario = scenario,
                    Role = scenario == MovementScenario.Standoff ? UnitRole.Artillery : UnitRole.BasicInfantry,
                    GroupSize = 6,
                    TickBudget = 1200,
                });

                Assert.That(result.RejectedOrders, Is.EqualTo(0),
                    $"{scenario}: a refused order means the scenario did not set up what it thinks it did, " +
                    "and the row is not a measurement");
            }
        }

        [Test]
        public void AGroupOnOpenGroundArrives()
        {
            MovementResult result = MovementScenarios.Run(new MovementSpec
            {
                Scenario = MovementScenario.Arrival, GroupSize = 8, TickBudget = 1500,
            });

            Assert.That(result.Arrived, Is.EqualTo(result.GroupSize), "on an empty field everyone must arrive");
            Assert.That(result.TicksToLastArrival, Is.GreaterThan(0u));
        }

        [Test]
        public void ADetourAroundAWallIsLongerThanTheStraightLine()
        {
            MovementResult result = MovementScenarios.Run(new MovementSpec
            {
                Scenario = MovementScenario.Detour, GroupSize = 6, TickBudget = 2500,
            });

            Assert.That(result.Arrived, Is.GreaterThan(0), "somebody has to find the gap");
            Assert.That(result.TravelledCells, Is.GreaterThan(result.StraightLineCells),
                "a wall with one gap must cost distance — this exercises flow field and CostField directly");
        }

        [Test]
        public void RangedUnitsDoNotKeepTheirDistance()
        {
            // Issue 03's number, measured. Ordered onto an enemy, artillery
            // with a 20-cell gun walks the whole way in. The overshoot IS the
            // finding; if this test ever fails because the overshoot shrank,
            // that is the behaviour work landing.
            MovementResult result = MovementScenarios.Run(new MovementSpec
            {
                Scenario = MovementScenario.Standoff,
                Role = UnitRole.Artillery,
                GroupSize = 6,
                TickBudget = 2000,
            });

            Assert.That(result.AttackRangeCells, Is.GreaterThan(10), "artillery is the long-range case");
            Assert.That(result.StartDistanceCells, Is.GreaterThan(result.AttackRangeCells),
                "the group must start outside its own range, or the closest approach is just the spawn distance");

            TestContext.Out.WriteLine(
                $"[movement] artillery range {result.AttackRangeCells}, started at {result.StartDistanceCells}, " +
                $"closed to {result.ClosestApproachCells} — overshoot {result.OvershootCells} cells");

            Assert.That(result.OvershootCells, Is.GreaterThan(0),
                "today ranged units walk inside their own range; when this stops being true, Issue 03 is done");
        }

        [Test]
        public void MovementScenariosAreReproducible()
        {
            MovementSpec Spec() => new MovementSpec
            {
                Scenario = MovementScenario.Blocking, GroupSize = 10, TickBudget = 1200,
            };

            MovementResult first = MovementScenarios.Run(Spec());
            MovementResult second = MovementScenarios.Run(Spec());

            Assert.That(second.FinalStateHash, Is.EqualTo(first.FinalStateHash));
            Assert.That(second.BlockedUnits, Is.EqualTo(first.BlockedUnits));
            Assert.That(second.TicksToLastArrival, Is.EqualTo(first.TicksToLastArrival));
        }
    }
}
