using NUnit.Framework;
using Nova.AI;
using Nova.AI.Data;

namespace Nova.SimRunner.Tests
{
    /// <summary>
    /// The arithmetic of the reinforcement doctrine (behaviour revision 9):
    /// three situations, one comparison, and the boundary between them.
    /// <para>
    /// WHY THESE ARE UNIT TESTS AND NOT A MATCH. A match cannot produce the
    /// states that matter here — a remnant worth exactly the threshold, a
    /// percentage whose product truncates, a wave outside worth a single point.
    /// The same argument that pulled <see cref="WaveStrengthGate"/> out of the
    /// AI system pulls this one out, and it was earned there rather than
    /// assumed: with the gate's arithmetic buried in a private method, deleting
    /// its clamp left the entire suite green.
    /// </para>
    /// <para>
    /// WHAT THESE TESTS DELIBERATELY DO NOT CLAIM. That the rule is a good idea.
    /// Measured one-sided in the lab it is not, at the shipped army cap or at a
    /// raised one, which is why it ships with
    /// <c>reinforceMinStrengthPercent: 0</c>. These tests hold the arithmetic to
    /// its stated meaning so that the next person to reach for the switch turns
    /// on the rule that was measured and not a different one.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class ReinforcementDoctrineTests
    {
        private const int WavePoints = 1200;

        // ----------------------------------------------------------------
        // The off setting — the one behaviour this suite must never let slip
        // ----------------------------------------------------------------

        /// <summary>
        /// 0 means off, and off means the doctrine does not look at all: no
        /// stance other than <see cref="ReinforcementStance.Off"/>, whatever is
        /// standing outside.
        /// <para>
        /// This is the test the whole delivery rests on. A rule without a
        /// working off switch cannot be measured one-sided (finding M001) — the
        /// same binary reaches both seats of a self-play match — and this rule
        /// SHIPS off, so a leak here would ship a behaviour change nobody
        /// measured under the name of a neutral one.
        /// </para>
        /// </summary>
        [TestCase(0L)]
        [TestCase(1L)]
        [TestCase(599L)]
        [TestCase(1200L)]
        [TestCase(long.MaxValue)]
        public void PercentZero_NeverLooks(long committedStrength)
        {
            ReinforcementStance stance = ReinforcementDoctrine.Resolve(
                percent: 0, WavePoints, committedStrength, out long threshold);

            Assert.That(stance, Is.EqualTo(ReinforcementStance.Off),
                "the doctrine answered while it is switched off");
            Assert.That(threshold, Is.Zero, "a switched-off rule reported a threshold it never applied");
        }

        /// <summary>
        /// The count path has no points to take a percentage of, so the doctrine
        /// stays out of it — a share of zero would be zero, and every remnant
        /// would then count as an intact wave.
        /// </summary>
        [Test]
        public void WithoutAStrengthThreshold_TheDoctrineStaysOut()
        {
            ReinforcementStance stance = ReinforcementDoctrine.Resolve(
                percent: 50, wavePoints: 0, committedStrength: 1, out long threshold);

            Assert.That(stance, Is.EqualTo(ReinforcementStance.Off),
                "the doctrine judged a wave measured in heads, where it has no unit of measure");
            Assert.That(threshold, Is.Zero);
        }

        // ----------------------------------------------------------------
        // The three situations
        // ----------------------------------------------------------------

        /// <summary>
        /// NOTHING OUTSIDE IS NOT A BROKEN WAVE. The distinction is the one that
        /// could most easily be lost while writing this rule, and losing it does
        /// not fail quietly: a threshold of anything is above a committed
        /// strength of nothing, so a first strike classified as "broken" would
        /// hold the opening wave to the full threshold forever and the AI would
        /// never attack at all.
        /// </summary>
        [Test]
        public void NothingOutside_IsAFirstStrikeAndNotABrokenWave()
        {
            ReinforcementStance stance = ReinforcementDoctrine.Resolve(
                percent: 50, WavePoints, committedStrength: 0, out long threshold);

            Assert.That(stance, Is.EqualTo(ReinforcementStance.FirstStrike),
                "an empty field was read as a broken wave — the opening wave would never march");
            Assert.That(threshold, Is.Zero,
                "a first strike was compared against a threshold it is not subject to");
        }

        /// <summary>An intact wave outside releases the ring; a remnant does not.</summary>
        [TestCase(600L, ReinforcementStance.Reinforce, TestName = "exactly the threshold still counts as intact")]
        [TestCase(601L, ReinforcementStance.Reinforce)]
        [TestCase(1200L, ReinforcementStance.Reinforce)]
        [TestCase(599L, ReinforcementStance.WaveBroken, TestName = "one point below the threshold is broken")]
        [TestCase(1L, ReinforcementStance.WaveBroken)]
        public void TheLevelDecides(long committedStrength, ReinforcementStance expected)
        {
            ReinforcementStance stance = ReinforcementDoctrine.Resolve(
                percent: 50, WavePoints, committedStrength, out long threshold);

            Assert.That(stance, Is.EqualTo(expected));
            Assert.That(threshold, Is.EqualTo(600L),
                "50 % of 1.200 is 600 — the reported threshold has to be the one that was applied");
        }

        // ----------------------------------------------------------------
        // The arithmetic itself
        // ----------------------------------------------------------------

        /// <summary>
        /// The share truncates, and the truncation is part of the value rather
        /// than an accident of it: 1.200 at 33 % is 396, on every machine and in
        /// both directions of a lockstep match.
        /// </summary>
        [TestCase(1200, 50, 600L)]
        [TestCase(1200, 40, 480L)]
        [TestCase(1200, 33, 396L)]
        [TestCase(1200, 1, 12L)]
        [TestCase(7, 33, 2L)]
        [TestCase(1200, 100, 1200L)]
        [TestCase(1200, 150, 1800L)]
        public void TheShareIsIntegerAndTruncates(int wavePoints, int percent, long expected)
        {
            Assert.That(ReinforcementDoctrine.BrokenThreshold(wavePoints, percent), Is.EqualTo(expected));
        }

        /// <summary>
        /// A threshold large enough to overflow an <c>int</c> product still
        /// comes out right. Not reachable from a shipped profile, and that is
        /// exactly why it is asserted here — the guard is invisible from any
        /// match, so nothing else would notice it being removed.
        /// </summary>
        [Test]
        public void ALargeThresholdDoesNotWrap()
        {
            Assert.That(ReinforcementDoctrine.BrokenThreshold(int.MaxValue, 100),
                Is.EqualTo((long)int.MaxValue));
        }

        /// <summary>
        /// Above 100 % the rule reads as "nothing outside is ever intact", which
        /// is a coherent setting and not an error: it turns every wave that has
        /// left into a remnant and holds every reinforcement at home. It is
        /// allowed through rather than clamped, because clamping would silently
        /// give a profile a behaviour it did not ask for.
        /// </summary>
        [Test]
        public void AboveFullStrengthNothingCountsAsIntact()
        {
            ReinforcementStance stance = ReinforcementDoctrine.Resolve(
                percent: 150, WavePoints, committedStrength: WavePoints, out long threshold);

            Assert.That(stance, Is.EqualTo(ReinforcementStance.WaveBroken));
            Assert.That(threshold, Is.EqualTo(1800L));
        }

        // ----------------------------------------------------------------
        // What ships
        // ----------------------------------------------------------------

        /// <summary>
        /// The shipped profile carries the OFF setting, and this is the
        /// assertion that keeps that honest.
        /// <para>
        /// WHY IT SHIPS OFF, so that nobody flips it back on from the value
        /// alone. Measured one-sided in the lab over eleven settings and both
        /// faction seatings, the rule has no value that is good on both: the
        /// Alliance seat improves at 40–45 and 70–80, the Legion seat only at
        /// 30–40, and their overlap is the single point 40 — a knife edge, not a
        /// plateau, and the lab's seed axis is empty so no further sampling can
        /// widen it. At a raised army cap of 30, where the r5 reachability
        /// ceiling no longer binds and the rule is supposed to matter most, every
        /// setting measured worse than the same cap without it, and two of them
        /// turned a won match into a lost one. The rule is built, switchable and
        /// recorded; turning it on needs a reason this measurement does not
        /// supply.
        /// </para>
        /// </summary>
        [Test]
        public void TheShippedProfileKeepsTheDoctrineOff()
        {
            Assert.That(AiProfiles.Ms1Canonical.ReinforceMinStrengthPercent, Is.Zero,
                "the reinforcement doctrine was switched on in the shipped profile — the lab measured "
                + "every setting as worse at a raised army cap; switching it on needs a new measurement, "
                + "not an edit here");
            Assert.That(AiProfiles.LegacyDefaults.ReinforceMinStrengthPercent, Is.Zero);
        }

        // ----------------------------------------------------------------
        // The goal, in a whole match
        // ----------------------------------------------------------------

        /// <summary>
        /// AT THE SHIPPED ARMY CAP THE GOAL CANNOT FIRE, and that is a finding
        /// about the cap rather than a defect in the rule. <c>Reinforce</c> names
        /// a unit that marches WHILE THE GATE IS SHUT — but with the cap at
        /// twelve and a wave of twelve outside, the r5 reachability ceiling has
        /// already collapsed the threshold onto what stands in the ring, so the
        /// gate is open and every replacement is under <c>Attack</c> anyway. The
        /// half of the doctrine that releases reinforcements therefore has
        /// nothing left to release here; only the half that HOLDS THEM BACK can
        /// change anything, and it does — that is what the lab measured at this
        /// cap.
        /// <para>
        /// This is asserted rather than merely known, because the alternative is
        /// somebody reading the lab numbers for this cap as evidence about a
        /// rule half of which never ran. The identical shape sank r6: a gate
        /// that ships dormant behind the same ceiling, with no test going red
        /// about it.
        /// </para>
        /// </summary>
        [Test]
        public void AtTheShippedArmyCap_TheGateIsAlreadyOpenAndTheGoalNeverFires()
        {
            var observer = new CountingObserver();
            SkirmishAiTests.AiHost host = SkirmishAiTests.BuildMatch(
                SkirmishAiTests.Seed, WithPercent(40), goalObserver: observer);

            host.RunUntilDecided(SkirmishAiTests.EndToEndBudgetTicks);

            Assert.That(observer.StanceCount, Is.GreaterThan(0),
                "not even the stance was reached — this test would then be asserting the wrong dormancy");
            Assert.That(observer.ReinforceCount, Is.Zero,
                "the goal fired at the shipped army cap, so the reachability ceiling no longer collapses "
                + "the threshold — re-read the lab numbers for this cap, they measure something else now");
        }

        /// <summary>
        /// Above the ceiling the goal is reached, and it means what the
        /// catalogue says: only for units still inside the ring, only while the
        /// wave gate is SHUT, and only while the army report calls the wave
        /// outside intact.
        /// <para>
        /// A RULE THAT NEVER FIRES IS NOT A RULE, and this is the test that says
        /// the difference. The unit tests above pin the arithmetic; none of them
        /// would notice a stance computed correctly and then dropped before the
        /// goal chain.
        /// </para>
        /// <para>
        /// THE CAP OF 30 IS NOT A PROPOSAL. It is the smallest round setting at
        /// which the ceiling stops binding, so it is what this test needs to
        /// reach the rule at all; the shipped cap is <c>MatchRunner</c>'s and not
        /// this strand's to move.
        /// </para>
        /// </summary>
        [Test]
        public void AboveTheCeiling_TheGoalIsReachedAndMeansWhatItSays()
        {
            var observer = new CountingObserver();
            SkirmishAiTests.AiHost host = SkirmishAiTests.BuildMatch(
                SkirmishAiTests.Seed, WithPercent(40, armyCap: 30), goalObserver: observer);

            host.RunUntilDecided(SkirmishAiTests.EndToEndBudgetTicks);

            Assert.That(observer.ReinforceCount, Is.GreaterThan(0),
                "the doctrine was switched on and no unit was ever put under Reinforce — the stance is "
                + "computed and then dropped somewhere before the goal chain");

            Assert.That(observer.ReinforceWithGateOpen, Is.Zero,
                "a unit was called a reinforcement while the wave gate was open — that is a wave "
                + "launch and belongs to Attack");
            Assert.That(observer.ReinforceWithoutStance, Is.Zero,
                "a unit was put under Reinforce in a decision whose army report does not say the wave "
                + "outside is intact");
            Assert.That(observer.ReinforceOutsideTheRing, Is.Zero,
                "a unit already outside the ring was called a reinforcement — it is attacking, and "
                + "calling it back is the failure r3 exists to prevent");
        }

        /// <summary>
        /// Switched off, the goal never appears — the whole-match counterpart to
        /// <see cref="PercentZero_NeverLooks"/>, and the reason the shipped build
        /// can carry this code at all.
        /// </summary>
        [Test]
        public void SwitchedOff_TheGoalNeverAppears()
        {
            var observer = new CountingObserver();
            SkirmishAiTests.AiHost host = SkirmishAiTests.BuildMatch(
                SkirmishAiTests.Seed, AiProfiles.Ms1Canonical, goalObserver: observer);

            host.RunUntilDecided(SkirmishAiTests.EndToEndBudgetTicks);

            Assert.That(observer.Judged, Is.GreaterThan(0), "no unit was judged at all — the run proves nothing");
            Assert.That(observer.ReinforceCount, Is.Zero,
                "the shipped profile has the doctrine off and a unit was still put under Reinforce");
            Assert.That(observer.StanceCount, Is.Zero,
                "the shipped profile has the doctrine off and the army report still claims an intact wave");
        }

        /// <summary>The shipped profile with one or two values replaced, and nothing else.</summary>
        private static AiProfile WithPercent(int percent, int armyCap = 0)
        {
            AiProfile s = AiProfiles.Ms1Canonical;
            return new AiProfile(
                profileId: $"reinforce-{percent}-probe",
                decisionTickInterval: s.DecisionTickInterval,
                placementSearchRadius: s.PlacementSearchRadius,
                powerReserve: s.PowerReserve,
                targetHarvesters: s.TargetHarvesters,
                harvesterQueueBatch: s.HarvesterQueueBatch,
                targetArmySize: armyCap > 0 ? armyCap : s.TargetArmySize,
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
                reinforceMinStrengthPercent: percent,
                targetHqWeight: s.TargetHqWeight);
        }

        /// <summary>
        /// Counts the decisions instead of keeping them. A whole match reports
        /// tens of thousands of unit decisions and none of the questions here
        /// need to look at one twice; keeping them all would make the test slow
        /// for no answer it could not give this way.
        /// <para>
        /// It reads the army decision of the SAME tick, which is why the army
        /// report is stored rather than counted: unit decisions of a cadence
        /// always follow their army decision, so the last one seen is the one
        /// they belong to.
        /// </para>
        /// </summary>
        private sealed class CountingObserver : IAiGoalObserver
        {
            private readonly int _ringCells;
            private AiArmyGoal _army;
            private bool _haveArmy;

            public CountingObserver()
            {
                AiProfile p = AiProfiles.Ms1Canonical;
                _ringCells = p.StagingDistanceCells + p.StagingToleranceCells;
            }

            public int Judged;
            public int StanceCount;
            public int ReinforceCount;
            public int ReinforceWithGateOpen;
            public int ReinforceWithoutStance;
            public int ReinforceOutsideTheRing;

            public void OnArmyGoal(byte slot, uint tick, in AiArmyGoal army)
            {
                _army = army;
                _haveArmy = true;
                if (army.Reinforces) StanceCount++;
            }

            public void OnUnitGoal(byte slot, uint tick, in AiUnitGoal goal)
            {
                Judged++;
                if (goal.Goal != GoalKind.Reinforce) return;

                ReinforceCount++;
                if (!_haveArmy) return;
                if (_army.WaveReady) ReinforceWithGateOpen++;
                if (!_army.Reinforces) ReinforceWithoutStance++;

                // The ring is StagingDistanceCells + StagingToleranceCells
                // around the own HQ, and the unit report carries exactly that
                // distance — so the "still gathering" half of the condition is
                // checkable here without re-implementing the predicate.
                if (goal.HomeDistanceCells > _ringCells)
                {
                    ReinforceOutsideTheRing++;
                }
            }
        }
    }
}
