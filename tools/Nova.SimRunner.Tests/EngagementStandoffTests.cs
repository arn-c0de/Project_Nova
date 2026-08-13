using System.Collections.Generic;
using NUnit.Framework;
using Nova.AI;
using Nova.AI.Data;
using Nova.Core;
using Nova.Simulation.State;

namespace Nova.SimRunner.Tests
{
    /// <summary>
    /// Behaviour revision 11: an attacking unit is sent to a point on ITS OWN
    /// weapon range around the army's target instead of onto the target's cell
    /// (<see cref="AiProfile.EngagementStandoffPercent"/>).
    /// <para>
    /// WHY THIS SUITE EXISTS NEXT TO THE PINNED OUTCOME. The canonical match
    /// moved by one tick when the rule shipped, and a one-tick shift reads like
    /// "almost nothing happened" to anybody who only looks at
    /// <see cref="CanonicalAiOutcomeTests"/>. It is not: the match is decided by
    /// the same base collapsing at roughly the same time, while WHERE the army
    /// stands to do it changed completely. An outcome pin cannot tell those two
    /// apart, so the rule is measured here directly — through the goal observer,
    /// which reports the destination each unit actually received.
    /// </para>
    /// <para>
    /// THE OBSERVER IS THE ONLY INSTRUMENT AVAILABLE, and it bounds what can be
    /// asserted. <see cref="AiUnitGoal"/> carries the destination but not the
    /// unit's position or its weapon, so no test here can recompute the ring and
    /// compare. What it CAN do is the three statements the rule is actually
    /// made of: off reproduces the previous behaviour exactly, on makes units
    /// stop before the target, and on makes an army fan out instead of
    /// converging on one cell. That is the rule, stated three ways, without a
    /// single number copied out of the implementation.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class EngagementStandoffTests
    {
        /// <summary>Every attack destination the AI handed out, with the army decision it belonged to.</summary>
        private sealed class AttackDestinationObserver : IAiGoalObserver
        {
            private readonly Dictionary<uint, AiArmyGoal> _army = new Dictionary<uint, AiArmyGoal>();

            /// <summary>Attack/Reinforce destinations per tick, in report order.</summary>
            public readonly List<(uint Tick, int CellX, int CellY)> Destinations =
                new List<(uint, int, int)>();

            /// <summary>How often a unit under Attack was told nothing about movement.</summary>
            public int HeldInRange;

            /// <summary>How often a unit under Attack received a destination.</summary>
            public int SentSomewhere;

            public void OnArmyGoal(byte slot, uint tick, in AiArmyGoal army)
            {
                _army[tick] = army;
            }

            public void OnUnitGoal(byte slot, uint tick, in AiUnitGoal goal)
            {
                if (goal.Goal != GoalKind.Attack && goal.Goal != GoalKind.Reinforce) return;
                // Only decisions taken against a real target say anything about
                // a stand-off: with no visible enemy the destination is the
                // enemy START AREA, which the rule leaves alone on purpose.
                if (!_army.TryGetValue(tick, out AiArmyGoal army) || army.TargetRaw == 0) return;

                if (goal.MoveCellX < 0)
                {
                    HeldInRange++;
                    return;
                }

                SentSomewhere++;
                Destinations.Add((tick, goal.MoveCellX, goal.MoveCellY));
            }

            /// <summary>The largest number of DISTINCT attack destinations handed out in a single decision.</summary>
            public int WidestFanOut()
            {
                var perTick = new Dictionary<uint, HashSet<long>>();
                foreach ((uint tick, int cellX, int cellY) in Destinations)
                {
                    if (!perTick.TryGetValue(tick, out HashSet<long> cells))
                    {
                        cells = new HashSet<long>();
                        perTick[tick] = cells;
                    }
                    cells.Add(((long)cellY << 32) | (uint)cellX);
                }

                int widest = 0;
                foreach (KeyValuePair<uint, HashSet<long>> entry in perTick)
                {
                    if (entry.Value.Count > widest) widest = entry.Value.Count;
                }
                return widest;
            }
        }

        /// <summary>
        /// THE OFF SETTING IS THE PREVIOUS BEHAVIOUR, not an approximation of
        /// it. Asserted against the revision-10 values rather than against a
        /// second run of the same binary, because a comparison of the binary
        /// with itself would still pass if both sides had moved together
        /// (methodology finding M001).
        /// </summary>
        [Test]
        public void StandoffZero_ReproducesTheBehaviourBeforeTheRule()
        {
            SkirmishAiTests.AiHost host = SkirmishAiTests.BuildMatch(
                SkirmishAiTests.Seed, WithStandoff(0));
            uint decided = host.RunUntilDecided(SkirmishAiTests.EndToEndBudgetTicks);

            Assert.Multiple(() =>
            {
                Assert.That(decided, Is.EqualTo(2761u),
                    "with the stand-off off the canonical match must be decided on the revision-10 tick");
                Assert.That($"0x{host.Kernel.CalculateStateHash():X16}", Is.EqualTo("0xF68C050A84B900F4"),
                    "with the stand-off off the canonical match must end in the revision-10 state");
            });
        }

        /// <summary>
        /// THE RULE BITES: units stop short of the target and say so. Before
        /// revision 11 a unit under Attack ALWAYS carried a destination — the
        /// target's cell — because there was no condition under which it had
        /// arrived. A single "nothing to say" is therefore proof the ring is
        /// reached and held, and the count is asserted as a share so the test
        /// does not turn into a second outcome pin.
        /// </summary>
        [Test]
        public void StandoffOn_LetsUnitsStopBeforeTheTarget()
        {
            var observer = new AttackDestinationObserver();
            SkirmishAiTests.AiHost host = SkirmishAiTests.BuildMatch(
                SkirmishAiTests.Seed, goalObserver: observer);
            host.RunUntilDecided(SkirmishAiTests.EndToEndBudgetTicks);

            int judged = observer.HeldInRange + observer.SentSomewhere;
            Assert.That(judged, Is.GreaterThan(0),
                "no unit ever attacked a visible target — the run proves nothing either way");
            Assert.That(observer.HeldInRange, Is.GreaterThan(0),
                "no attacking unit ever reached its stand-off ring: every one of them was still being "
                + "walked toward the target, which is the behaviour revision 11 replaces");
        }

        /// <summary>
        /// THE OFF SETTING CONVERGES, THE RULE FANS OUT — the same run, the
        /// same seed, one profile value apart. Every unit's ring is centred on
        /// the same target cell but entered from where that unit stands, so an
        /// army arriving from several directions receives several destinations.
        /// <para>
        /// This is the closest a test gets to the thing the change is for. The
        /// spacing a player sees is produced by the formation spread in
        /// <c>ApplyMove</c>, which is maintainer ground (D-088) and untouched
        /// here; what this strand contributes is that the spread no longer
        /// starts from one shared point on top of the enemy.
        /// </para>
        /// </summary>
        [Test]
        public void StandoffOn_GivesAnArmySeveralDestinationsWhereOffGivesItOne()
        {
            var off = new AttackDestinationObserver();
            SkirmishAiTests.BuildMatch(SkirmishAiTests.Seed, WithStandoff(0), off)
                .RunUntilDecided(SkirmishAiTests.EndToEndBudgetTicks);

            var on = new AttackDestinationObserver();
            SkirmishAiTests.BuildMatch(SkirmishAiTests.Seed, goalObserver: on)
                .RunUntilDecided(SkirmishAiTests.EndToEndBudgetTicks);

            Assert.That(off.Destinations, Is.Not.Empty, "the off run never attacked — it proves nothing");
            Assert.Multiple(() =>
            {
                Assert.That(off.WidestFanOut(), Is.EqualTo(1),
                    "with the stand-off off every attacker of a decision must share ONE destination — "
                    + "that shared cell is exactly what the rule removes");
                Assert.That(on.WidestFanOut(), Is.GreaterThan(1),
                    "with the stand-off on the attackers of at least one decision must be sent to "
                    + "different cells, or the ring is not being entered from different directions");
            });
        }

        /// <summary>
        /// The identifier moves with the value, so a tuning run cannot be
        /// mistaken for the run before it. Cheap, and it closes the one gap the
        /// three behaviour tests leave: a profile value that reaches no hash.
        /// </summary>
        [Test]
        public void TheStandoffReachesTheBehaviourIdentifier()
        {
            Assert.That(AiBehaviorId.ComputeProfileHash(WithStandoff(0)),
                Is.Not.EqualTo(AiBehaviorId.ComputeProfileHash(WithStandoff(80))),
                "two profiles that differ only in the stand-off must not share an identifier");
        }

        /// <summary>
        /// THE COMPLAINT THIS RULE ANSWERS, measured on the board instead of on
        /// the order: how close does an attacker ever get to something hostile?
        /// <para>
        /// Everything else in this file reads the ORDERS the AI handed out, and
        /// orders were exactly where the first version of this rule looked
        /// correct while the game still showed units in contact. The gap was
        /// <c>MoveCell -1</c>: it means "no new order", not "halt", so a unit
        /// marching on the enemy start area — the destination whenever nothing
        /// is visible — met its target already INSIDE the ring, was told
        /// nothing, and walked on into contact. A cadence is 20 ticks and
        /// infantry covers eight cells in them, so meeting the target inside
        /// the ring is the normal case, not the corner one.
        /// </para>
        /// <para>
        /// Hence a positional assertion, and a relative one: the closest
        /// approach over a whole match, with the rule on against the same
        /// match with it off. An absolute floor would encode the ring radius,
        /// the sight radius and the cadence into one number and go stale on
        /// the first tuning pass; "closer without it than with it" is the claim
        /// itself.
        /// </para>
        /// </summary>
        [Test]
        public void StandoffOn_KeepsAttackersFurtherFromTheEnemyThanOffDoes()
        {
            SimFixed withRule = ClosestApproach(WithStandoff(80));
            SimFixed withoutRule = ClosestApproach(WithStandoff(0));

            TestContext.Out.WriteLine(
                $"[EngagementStandoffTests] closest approach: {withRule} cells with the stand-off, "
                + $"{withoutRule} without it");

            Assert.That(withRule, Is.GreaterThan(withoutRule),
                $"closest approach was {withRule} with the stand-off and {withoutRule} without it — "
                + "the rule has to keep attackers further from the enemy than its own off setting, "
                + "or it is not doing the one thing it exists for");
        }

        /// <summary>
        /// The smallest centre distance between two units of different players
        /// at any point of the canonical match, sampled every tick. Buildings
        /// count: they are units in the store and they are what an army walks
        /// into.
        /// </summary>
        private static SimFixed ClosestApproach(AiProfile profile)
        {
            SkirmishAiTests.AiHost host = SkirmishAiTests.BuildMatch(SkirmishAiTests.Seed, profile);
            SimFixed closestSquared = SimFixed.FromInt(0x3FFF);

            for (int tick = 0; tick < SkirmishAiTests.EndToEndBudgetTicks && !host.Victory.IsDecided; tick++)
            {
                host.Step();

                UnitState[] units = host.Entities.RawUnits;
                int capacity = host.Entities.Capacity;
                for (int i = 0; i < capacity; i++)
                {
                    ref readonly UnitState a = ref units[i];
                    if (!a.IsActive) continue;
                    for (int k = i + 1; k < capacity; k++)
                    {
                        ref readonly UnitState b = ref units[k];
                        if (!b.IsActive || b.PlayerId == a.PlayerId) continue;

                        // Cell distance, for the same overflow reason
                        // ResolveEngagementCell computes in cells: a squared
                        // WORLD distance across a 128-cell map steps over the
                        // Q16.16 ceiling.
                        int dx = SimFixed.WorldToGrid(a.Transform.PositionX)
                            - SimFixed.WorldToGrid(b.Transform.PositionX);
                        int dy = SimFixed.WorldToGrid(a.Transform.PositionY)
                            - SimFixed.WorldToGrid(b.Transform.PositionY);
                        SimFixed distanceSquared = SimFixed.FromInt(dx * dx + dy * dy);
                        if (distanceSquared < closestSquared) closestSquared = distanceSquared;
                    }
                }
            }

            return SimTrig.Sqrt(closestSquared);
        }

        /// <summary>The shipped profile with the stand-off replaced, and nothing else.</summary>
        private static AiProfile WithStandoff(int percent)
        {
            AiProfile s = AiProfiles.Ms1Canonical;
            return new AiProfile(
                profileId: $"standoff-{percent}-probe",
                decisionTickInterval: s.DecisionTickInterval,
                placementSearchRadius: s.PlacementSearchRadius,
                powerReserve: s.PowerReserve,
                targetHarvesters: s.TargetHarvesters,
                harvesterQueueBatch: s.HarvesterQueueBatch,
                targetArmySize: s.TargetArmySize,
                attackSquadThreshold: s.AttackSquadThreshold,
                infantryQueueBatch: s.InfantryQueueBatch,
                targetDamageWeight: s.TargetDamageWeight,
                targetThreatWeight: s.TargetThreatWeight,
                targetFinishWeight: s.TargetFinishWeight,
                targetDistanceWeight: s.TargetDistanceWeight,
                waveSize: s.WaveSize,
                stagingDistanceCells: s.StagingDistanceCells,
                stagingToleranceCells: s.StagingToleranceCells,
                retreatHealthPercent: s.RetreatHealthPercent,
                retreatDangerCells: s.RetreatDangerCells,
                waveStrengthPoints: s.WaveStrengthPoints,
                defendHomeCells: s.DefendHomeCells,
                reinforceMinStrengthPercent: s.ReinforceMinStrengthPercent,
                targetHqWeight: s.TargetHqWeight,
                engagementStandoffPercent: percent);
        }
    }
}
