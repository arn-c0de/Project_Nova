using System.Collections.Generic;
using NUnit.Framework;
using Nova.AI;
using Nova.AI.Data;
using Nova.Simulation.State;

namespace Nova.SimRunner.Tests
{
    /// <summary>
    /// The enemy headquarters as a WEIGHT instead of a short circuit
    /// (behaviour revision 10).
    /// <para>
    /// WHAT THIS SUITE IS FOR. The change is one deleted <c>return</c>, and the
    /// property that matters cannot be seen in the diff: whether the army ever
    /// attacks anything OTHER than the headquarters. Until r10 it effectively
    /// did not — the short circuit took the headquarters on sight, and when
    /// nothing was visible the march destination was the enemy start area, which
    /// on this map is where the headquarters stands. Both roads led to the same
    /// building. The loss column cannot tell that story and the tests below can.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class TargetHqWeightTests
    {
        /// <summary>
        /// 0 is the off setting and it is the OLD CODE, not a large number
        /// pretending to be one: the <c>return</c> is still there and still
        /// takes the first visible headquarters whatever else is on the field.
        /// <para>
        /// Asserted through a scene rather than through the profile value,
        /// because "the branch still exists" is not what anybody needs to know —
        /// "the army still walks onto the headquarters past a softer target" is.
        /// </para>
        /// </summary>
        [Test]
        public void WeightZero_StillTakesTheHeadquartersOnSight()
        {
            AiProfile off = WithHqWeight(0);
            var observer = new TargetObserver();
            SkirmishAiTests.AiHost host = SkirmishAiTests.BuildMatch(
                SkirmishAiTests.Seed, off, goalObserver: observer);

            host.RunUntilDecided(SkirmishAiTests.EndToEndBudgetTicks);

            HashSet<UnitRole> kinds = observer.TargetKinds(host);
            Assert.That(kinds, Does.Contain(UnitRole.HQ),
                "the AI never attacked a headquarters at all — the scene proves nothing either way");
            Assert.That(kinds, Does.Not.Contain(UnitRole.Refinery),
                "with the short circuit in place the AI reached a refinery — the return was removed or "
                + "bypassed, and the off setting is no longer the old behaviour");
        }

        /// <summary>
        /// Switched on, the army picks a DIFFERENT target from the one the short
        /// circuit would have picked, at least once in the match — the rule is
        /// not inert.
        /// <para>
        /// WHAT THIS HARNESS CAN AND CANNOT SHOW, stated rather than papered
        /// over. The number r10 is judged on is how many KINDS of thing the AI
        /// attacks, and that was measured in the lab's canonical two-AI match,
        /// which runs to tick 7.381: three kinds become five, the refinery and
        /// the harvesters joining. This harness decides at 2.761, the enemy
        /// economy is barely standing, and both runs end up aiming at the same
        /// two entities — so asserting "more kinds" here would fail for a reason
        /// that has nothing to do with the rule, and asserting the refinery by
        /// name would pin a scenario rather than a behaviour.
        /// </para>
        /// <para>
        /// What is left is still worth a test and is the strongest claim this
        /// scene supports: the two runs DISAGREE about the target at some
        /// cadence. That is the whole difference between a weight and a short
        /// circuit — the score gets asked — and it is what moved the pinned end
        /// state in <see cref="CanonicalAiOutcomeTests"/>.
        /// </para>
        /// </summary>
        [Test]
        public void TheShippedWeight_ChangesWhichTargetIsPicked()
        {
            var shortCircuit = new TargetObserver();
            SkirmishAiTests.AiHost a = SkirmishAiTests.BuildMatch(
                SkirmishAiTests.Seed, WithHqWeight(0), goalObserver: shortCircuit);
            a.RunUntilDecided(SkirmishAiTests.EndToEndBudgetTicks);

            var shipped = new TargetObserver();
            SkirmishAiTests.AiHost b = SkirmishAiTests.BuildMatch(
                SkirmishAiTests.Seed, AiProfiles.Ms1Canonical, goalObserver: shipped);
            b.RunUntilDecided(SkirmishAiTests.EndToEndBudgetTicks);

            Assert.That(shipped.PerCadence, Is.Not.EqualTo(shortCircuit.PerCadence),
                "the shipped weight chose the same target as the short circuit at every single cadence "
                + "— the score is never actually asked, and the rule is shipped and inert");
            Assert.That(shipped.TargetKinds(b), Does.Contain(UnitRole.HQ),
                "the headquarters stopped being attacked at all — the weight is too low to keep the "
                + "preference, and a preference that never wins is not a preference");
            Assert.That(shipped.TargetKinds(b), Is.SupersetOf(shortCircuit.TargetKinds(a)),
                "the weight did not widen the target choice, it MOVED it — losing a kind the short "
                + "circuit reached is a different rule than the one that was measured");
        }

        /// <summary>
        /// A weight far above the score's own scale is the short circuit again
        /// in every way that shows: the same targets, in the same match.
        /// <para>
        /// This is the assertion the plan's original off setting would have
        /// rested on ("a value so high it outvotes everything"), and it holds —
        /// it is simply not what the SHIPPED off switch rests on, because
        /// "outvotes everything" is an argument about the grid size and the
        /// damage table rather than about the code.
        /// </para>
        /// </summary>
        [Test]
        public void AnEnormousWeight_IsTheShortCircuitAgain()
        {
            var withReturn = new TargetObserver();
            SkirmishAiTests.AiHost a = SkirmishAiTests.BuildMatch(
                SkirmishAiTests.Seed, WithHqWeight(0), goalObserver: withReturn);
            a.RunUntilDecided(SkirmishAiTests.EndToEndBudgetTicks);

            var withWeight = new TargetObserver();
            SkirmishAiTests.AiHost b = SkirmishAiTests.BuildMatch(
                SkirmishAiTests.Seed, WithHqWeight(100_000), goalObserver: withWeight);
            b.RunUntilDecided(SkirmishAiTests.EndToEndBudgetTicks);

            Assert.That(withWeight.TargetKinds(b), Is.EquivalentTo(withReturn.TargetKinds(a)),
                "a weight of 100.000 picked different targets than the short circuit — the score's own "
                + "terms have grown past the scale this assumes");
        }

        /// <summary>The shipped profile carries the measured weight, and 0 stays reachable as the off setting.</summary>
        [Test]
        public void TheShippedProfileCarriesTheMeasuredWeight()
        {
            Assert.That(AiProfiles.Ms1Canonical.TargetHqWeight, Is.EqualTo(100),
                "the shipped headquarters weight moved — 100 is where the measured curve stops costing "
                + "match length and intents; 75 and 150 are both worse, so this is not interpolatable");
            Assert.That(AiProfiles.LegacyDefaults.TargetHqWeight, Is.Zero,
                "the legacy profile has to keep the short circuit — there was no weight before r10");
        }

        private static AiProfile WithHqWeight(int weight)
        {
            AiProfile s = AiProfiles.Ms1Canonical;
            return new AiProfile(
                profileId: $"hq-weight-{weight}-probe",
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
                targetHqWeight: weight);
        }

        /// <summary>
        /// Collects the raw ids the army was ever aimed at, and looks their
        /// roles up afterwards.
        /// <para>
        /// AFTERWARDS IS THE POINT: an entity that dies during the match is gone
        /// from the store by the end, so a role read at the end would silently
        /// drop exactly the targets that were killed — which is most of the
        /// interesting ones. The store keeps dead units addressable by index, so
        /// the lookup walks the raw array instead of asking for a live entity.
        /// </para>
        /// </summary>
        private sealed class TargetObserver : IAiGoalObserver
        {
            private readonly HashSet<uint> _raws = new HashSet<uint>();

            private readonly List<string> _perCadence = new List<string>();

            /// <summary>
            /// The target of every army decision in order, as "tick:raw". A
            /// SEQUENCE and not a set, because the two runs of this scene end up
            /// aiming at the same two entities overall and differ only in which
            /// one they pick when — which is exactly the difference a weight
            /// makes over a short circuit.
            /// </summary>
            public IReadOnlyList<string> PerCadence => _perCadence;

            public void OnArmyGoal(byte slot, uint tick, in AiArmyGoal army)
            {
                if (army.TargetRaw != 0) _raws.Add(army.TargetRaw);
                _perCadence.Add($"{tick}:{army.TargetRaw}");
            }

            public void OnUnitGoal(byte slot, uint tick, in AiUnitGoal goal)
            {
            }

            public HashSet<UnitRole> TargetKinds(SkirmishAiTests.AiHost host)
            {
                var kinds = new HashSet<UnitRole>();
                UnitState[] units = host.Entities.RawUnits;
                for (int i = 0; i < units.Length; i++)
                {
                    ref readonly UnitState u = ref units[i];
                    uint raw = UnitCommandStateView.ToRawEntityId(u.Id);
                    if (raw != 0 && _raws.Contains(raw)) kinds.Add(u.Role);
                }
                return kinds;
            }
        }
    }
}
