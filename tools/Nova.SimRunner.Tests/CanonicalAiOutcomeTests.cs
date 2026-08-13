using NUnit.Framework;
using Nova.AI.Data;

namespace Nova.SimRunner.Tests
{
    /// <summary>
    /// Pins the OUTCOME of the canonical AI match: the tick it is decided on
    /// and the end-state hash. Owned by the maintainer strand (D-101).
    /// <para>
    /// Why this is not in <see cref="SkirmishAiTests"/> any more: these two
    /// numbers move on ANY change to the simulation the AI plays in — economy,
    /// construction, production, vision — not only on a change to the AI
    /// itself. They sat in the AI suite until 2026-08-09, where every package
    /// of Sprint 16 tripped them and the failure message sent the maintainer
    /// strand to the AI behaviour journal, which is the wrong book. The
    /// identifier pin — "which AI is this" — stayed where it belongs, in
    /// <see cref="SkirmishAiTests.AiBehaviorId_TracksWhichAiThisIs"/>.
    /// </para>
    /// <para>
    /// The split does not lose the diagnosis, because this test reads the
    /// identifier too. Read the two results together:
    /// </para>
    /// <list type="table">
    /// <item><term>outcome moved, identifier moved</term><description>the AI
    /// changed. The AI strand bumps <c>AiBehaviorId.Revision</c>, writes the
    /// journal entry in <c>tools/Nova.AiLab/reports/behavior-log.md</c>, and
    /// updates the numbers below in the same commit.</description></item>
    /// <item><term>outcome moved, identifier unchanged</term><description>the
    /// simulation under the AI changed. The strand that changed it updates the
    /// numbers below and says so in its PR. No journal entry, no revision
    /// bump — nothing about the AI moved.</description></item>
    /// <item><term>outcome UNDECIDED (tick 0)</term><description>not a moved
    /// pin. The AI no longer finishes the match inside the budget, which means
    /// something broke its loop. Fix the cause; do not update the
    /// numbers.</description></item>
    /// </list>
    /// <para>
    /// This is NOT one of the four determinism baselines: those live in their
    /// own files and force a behaviour PR and a baseline PR apart. This pin
    /// belongs WITH the change that moved it and is updated in the same commit.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class CanonicalAiOutcomeTests
    {
        /// <summary>Decided tick of the canonical AI match, last moved by: the AI strand, behaviour revision 10 (the enemy HQ became a weight instead of a short circuit). Previous value: 2726 (Sprint 16.9 / D-104, D-107).</summary>
        private const uint PinnedDecidedTick = 2761u;

        /// <summary>
        /// End-state hash of the canonical AI match, last moved by: the AI
        /// strand, behaviour revision 10. The enemy headquarters stopped
        /// short-circuiting the target score and became a weight on it
        /// (targetHqWeight 100), so a defended headquarters can now lose to a
        /// softer target — the AI attacks five kinds of thing instead of three.
        /// Identifier moved with it, which is the "the AI changed" case above:
        /// revision bumped, journal entry V010 written, this pin updated in the
        /// same commit.
        /// AiBehaviorId is r10.E75CB19D.
        /// Previous value: 0x10B83E94F86F2E55 (Sprint 16.9 / D-104, D-107).
        /// </summary>
        private const string PinnedEndState = "0xF68C050A84B900F4";

        [Test]
        public void CanonicalAiMatch_DecidesOnThePinnedTick_WithThePinnedEndState()
        {
            SkirmishAiTests.AiHost host = SkirmishAiTests.BuildMatch(SkirmishAiTests.Seed);
            uint decided = host.RunUntilDecided(SkirmishAiTests.EndToEndBudgetTicks);
            ulong endState = host.Kernel.CalculateStateHash();

            // Undecided is a defect, not a moved pin — it is asserted first and
            // on its own so the failure says which of the two happened.
            Assert.That(decided, Is.Not.Zero,
                $"the AI did not decide the canonical match within {SkirmishAiTests.EndToEndBudgetTicks} ticks — "
                + "its loop is broken, not merely moved. Fix the cause instead of updating the pin.");

            Assert.Multiple(() =>
            {
                Assert.That(decided, Is.EqualTo(PinnedDecidedTick),
                    "the canonical AI match is decided on a different tick than the pinned one — "
                    + $"AI identifier is {AiBehaviorId.Value}; if that string is unchanged, the simulation moved, not the AI");
                Assert.That($"0x{endState:X16}", Is.EqualTo(PinnedEndState),
                    "the canonical AI match ends in a different state than the pinned one — "
                    + $"AI identifier is {AiBehaviorId.Value}; if that string is unchanged, the simulation moved, not the AI");
            });
        }
    }
}
