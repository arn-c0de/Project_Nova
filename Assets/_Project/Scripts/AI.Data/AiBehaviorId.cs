using Nova.Core;

namespace Nova.AI.Data
{
    /// <summary>
    /// One short identifier for "which AI is this". It changes whenever the
    /// skirmish AI behaves differently, and it is meant to be read off a
    /// screenshot: the debug HUD shows it, every lab report carries it, and the
    /// behaviour journal keys its entries to it.
    /// <para>
    /// WHY TWO PARTS. AI behaviour changes in two ways that no single mechanism
    /// catches:
    /// </para>
    /// <list type="number">
    /// <item><b>Numbers</b> — a value in <see cref="AiProfiles.Ms1Canonical"/>
    /// moves. Caught automatically: <see cref="ProfileHash"/> is a hash over
    /// every field of the shipped profile, so an edit cannot be silent.</item>
    /// <item><b>Code</b> — the same numbers, a different rule (score targeting
    /// replacing list order, say). NOTHING can derive that from data, so
    /// <see cref="Revision"/> is bumped BY HAND. The safety net is a test that
    /// pins the identifier together with the canonical match's end-state hash:
    /// change behaviour without bumping, and it goes red.</item>
    /// </list>
    /// <para>
    /// The identifier is NOT part of the match fingerprint and deliberately so.
    /// Whether an AI profile is fingerprint-relevant is an owner decision
    /// (Simulation/Replays), and nothing here may quietly answer it.
    /// </para>
    /// </summary>
    public static class AiBehaviorId
    {
        /// <summary>
        /// Bumped BY HAND whenever the AI's behaviour code changes — a new
        /// rule, a changed formula, a different order of decisions. Not for
        /// comments, renames or refactors that leave the end-state hash alone.
        /// <para>
        /// History, so the number means something:
        /// </para>
        /// <list type="bullet">
        /// <item><b>1</b> — MS-1 skirmish AI as shipped: build order, economy,
        /// army, attack at the squad threshold, targets chosen as "HQ, else the
        /// first visible building, else the first visible unit".</item>
        /// <item><b>2</b> — target choice by integer score (counter table,
        /// threat, missing health, distance) instead of the order of the
        /// visibility list. The enemy HQ stays a short-circuit.</item>
        /// <item><b>3</b> — waves. The army marches at full strength and
        /// reinforcements wait at a staging cell between the own base and the
        /// enemy start area instead of walking to the front one at a time.
        /// Units already out are never called back. Off setting:
        /// <c>waveSize</c> 1.</item>
        /// <item><b>4</b> — retreat. A unit under a health percentage with an
        /// armed enemy nearby walks back to the staging cell instead of
        /// fighting to the last point, and rejoins with the next wave. No
        /// health hysteresis: MS-1 units never heal, so an exit percentage
        /// would never be reached. Off setting:
        /// <c>retreatHealthPercent</c> 0.</item>
        /// </list>
        /// <para>
        /// NOT bumped for the posture/assignment refactor of the army step:
        /// it reproduces the canonical match tick for tick, which is what
        /// "leaves the end-state hash alone" means. Aiming below the squad
        /// threshold WOULD have bumped it and was measured back out again —
        /// behaviour journal V003.
        /// </para>
        /// <para>
        /// NOT bumped either for naming those branches <see cref="GoalKind"/>.
        /// Same four conditions, same order, same orders out; the canonical
        /// match decides on the same tick with the same end state and a lab
        /// run's artifacts came out byte-identical bar the measured runtime.
        /// The refactor deliberately adds NO profile field — priorities in the
        /// profile would move <see cref="ProfileHash"/>, and with it this very
        /// identifier, which would have made the neutrality unprovable in the
        /// artifacts it is printed into. A goal module that earns an off switch
        /// brings one in the pull request that gives it a rule.
        /// </para>
        /// <para>
        /// r8 IS THAT PULL REQUEST, and the identifier moves twice over: the
        /// revision here because decisions change, and
        /// <see cref="ProfileHash"/> because the rule ships with the off switch
        /// that makes it measurable one-sided at all (finding M001).
        /// <c>DefendHome</c> is the first goal to carry a rule of its own — the
        /// units still waiting in the staging ring break off and walk home when
        /// a visible armed enemy comes within
        /// <see cref="AiProfile.DefendHomeCells"/> of their headquarters. What
        /// it fixes is a defect as old as the staging cell itself (r3), not one
        /// r6 introduced: a unit that has arrived is deliberately given no
        /// order, so it hangs entirely on an auto-acquisition that reaches six
        /// or seven cells while the staging cell sits twelve from the base.
        /// </para>
        /// <para>
        /// TWO CORRECTIONS WENT INTO r8 BEFORE IT SHIPPED, both found by
        /// reviewing the claim rather than the code, and neither moves
        /// <see cref="ProfileHash"/> because neither adds a number. First: the
        /// argument "the destination is static, so the suppression swallows
        /// every repeat" only holds WHILE the defender walks — arriving clears
        /// the standing order the suppression compares against, so the
        /// headquarters cell went out again every cadence, one intent per
        /// cadence for the whole siege, with the standing defenders flipped back
        /// into movement each time. <c>DefendHome</c> now falls silent once
        /// home, the way <c>Hold</c> does at the staging cell. Second: the goal
        /// asked not to be retreating, which could only ever exclude a wounded
        /// unit that had ALREADY ARRIVED — and arriving ends the retreat by the
        /// rule right above it. Those units fell to <c>Hold</c> and stood twelve
        /// cells out while the base they had run to burned.
        /// </para>
        /// <para>
        /// r5 fixes two defects found in the review of r3/r4, both of which
        /// change decisions and therefore the end state: the wave now waits for
        /// what production can still deliver instead of a fixed cap (a single
        /// survivor outside the ring used to stall it for the rest of the
        /// match), and a retreating unit is pointed at its pursuer instead of
        /// carrying a march target it can no longer reach.
        /// </para>
        /// <para>
        /// r6 gives the wave a unit of measure. It marched on a COUNT of
        /// gathered units, and a count cannot tell an Alliance rifleman (100
        /// points of damage times health per firing interval) from a Legion
        /// recruit (44): twelve of each were both "a full wave", one of them at
        /// 44 % of the attack strength of the other. The gate now sums
        /// <c>CombatStrength</c> over the units inside the staging ring and
        /// compares it against <c>waveStrengthPoints</c>. The r5 rule survives
        /// translated into points — the threshold is still capped at what
        /// production can actually still deliver, so a survivor standing
        /// outside the ring cannot stall the next wave. Off setting:
        /// <c>waveStrengthPoints</c> 0, which restores the count.
        /// </para>
        /// <para>
        /// IT SHIPS DORMANT, and that is the point rather than an oversight.
        /// The threshold is capped at what production can still deliver, so the
        /// point clause can only decide anything while at least one head of the
        /// army cap is free — at the shipped cap of 12 that means eleven
        /// riflemen, 1.100 points, against a threshold of 1.200. The gate
        /// therefore decides exactly like the count it replaces and the
        /// canonical match is byte-identical. What the revision buys is the
        /// DECOUPLING: the wave's threshold is no longer a head count paired to
        /// the production cap, which is the precondition for moving that cap.
        /// Measured one-sided, moving it WITHOUT the gate makes the Legion
        /// worse (own losses 51 to 64); with the gate and a cap of 30 the same
        /// seat decides faster than today with 23 own losses. The cap itself is
        /// one of four values <c>MatchRunner</c> overrides with literals of its
        /// own, so it is not this strand's to move.
        /// <para>
        /// THE MARGIN IS NINE POINTS PER RIFLEMAN, and weapon numbers are this
        /// strand's own work: one more damage on the Alliance rifleman wakes
        /// the gate without anyone touching the cap.
        /// <c>AiProfileTests.TheStrengthGateIsDormantAtTheShippedArmyCap</c>
        /// computes that margin from <c>CombatStrength</c> rather than trusting
        /// a copied number, and goes red either way.
        /// </para>
        /// </para>
        /// <para>
        /// r7 keeps the D-077 strategic opening but makes its prerequisite
        /// handoff explicit: after the Refinery, the AI completes the Power
        /// plant required by D-103 before attempting its Barracks. The old
        /// margin-only rule happened to do that for Alliance, but Legion's
        /// 15-point margin covered the Barracks' 10-point draw and therefore
        /// retried an illegal placement forever once the all-of gate shipped.
        /// </para>
        /// <para>
        /// r9 gives the trickle a condition. Since r5 the wave threshold is
        /// capped at what production can still deliver, and with the army
        /// standing at its cap that ceiling collapses onto the strength already
        /// in the ring — so every single replacement marched off alone,
        /// whatever it was walking towards. That was a side effect of a guard,
        /// never a decision, and it is half the reinforcement doctrine with
        /// nothing under it. Now the summed strength of the units already
        /// outside decides: at or above
        /// <c>reinforceMinStrengthPercent</c> of the full threshold the wave out
        /// there is intact and every gathering unit follows it under the new
        /// <see cref="GoalKind.Reinforce"/>; below it the wave counts as broken
        /// and the ring is held to the FULL threshold instead, so nobody
        /// trickles after a remnant. Off setting:
        /// <c>reinforceMinStrengthPercent</c> 0.
        /// <para>
        /// A LEVEL, NOT A RATE, and the difference is stated rather than
        /// glossed: "the attack collapses" is a rate, a rate needs two points
        /// in time, and this AI has no memory to hold the first one. What is
        /// compared is a level, which a collapsing wave crosses within a few
        /// cadences.
        /// </para>
        /// <para>
        /// IT SHIPS OFF, and unlike r6 that is a verdict rather than a
        /// dormancy. Measured one-sided over eleven settings and both seatings,
        /// the two faction seats agree on exactly one percentage (40) and
        /// disagree one step to either side; and at a raised army cap of 30 —
        /// where the r5 ceiling stops binding and the whole rule finally runs —
        /// every setting measured worse than the same cap without it, twice
        /// turning a won match into a lost one. Numbers in
        /// <c>AiProfiles.Ms1Canonical</c> and the journal. What the revision
        /// buys regardless: the trickle now has a name a panel can draw and a
        /// switch a report can vary, where before it was an invisible side
        /// effect of the r5 guard.
        /// </para>
        /// <para>
        /// THE KNOWN RISK IS THE r4 BLOCKADE IN A NEW COAT. Holding the ring to
        /// the full threshold asks it for a strength the army cap cannot supply
        /// while the remnant outside is still alive and still holding a head.
        /// A remnant that refuses to die therefore stalls the next wave — the
        /// V006 failure mode. There is no clause against it that does not also
        /// remove the rule; the answer is the off setting and the measurement.
        /// </para>
        /// </para>
        /// <para>
        /// r10 turns the enemy headquarters from a short circuit into a weight.
        /// V001 argued that losing it decides the match (D-077), so it is a win
        /// condition rather than a preference, and took the first visible one on
        /// sight. Right about the rule, wrong about the behaviour — and for a
        /// reason that is a property of the map rather than of the argument: the
        /// fallback march destination is the enemy start area, and the enemy
        /// start area is where the headquarters stands, so both roads led to the
        /// same building and the army walked the same line onto it every single
        /// match (journal B001). <c>targetHqWeight</c> (shipped 100, <b>0 is the
        /// short circuit</b>) adds the preference to the ordinary score instead,
        /// so a defended headquarters can lose to a soft target standing off to
        /// one side — which the score has been able to rate since V001 and never
        /// once got the chance to, because the method returned first.
        /// <para>
        /// THE NUMBER IT IS JUDGED ON is how many kinds of target the AI
        /// attacks, effectively one until now: measured one-sided it goes from
        /// three to five, with the refinery and the harvesters joining. Own
        /// losses fall from 33 to 14 and the match decides 35 % earlier, and
        /// the same rise in kinds holds at army caps 16, 20 and 30 — the second
        /// axis, because the lab's seed axis is empty and one value on one axis
        /// is one match.
        /// </para>
        /// </para>
        /// </summary>
        public const int Revision = 10;

        /// <summary>
        /// Hash over every value of the shipped profile. Domain-separated like
        /// the rest of the project's hashes, so it can never collide with a
        /// state or definitions hash that happens to hold the same bytes.
        /// </summary>
        public static readonly ulong ProfileHash = ComputeProfileHash(AiProfiles.Ms1Canonical);

        /// <summary>
        /// What the HUD shows and what a report prints: <c>r2.4F1C08A9</c>.
        /// Short enough for a status line, unambiguous enough to grep for.
        /// </summary>
        public static readonly string Value = $"r{Revision}.{(uint)(ProfileHash >> 32):X8}";

        /// <summary>
        /// The same identifier with the full profile hash, for artefacts that
        /// have room and want to be exact rather than short.
        /// </summary>
        public static string Full => $"r{Revision}.0x{ProfileHash:X16}";

        /// <summary>
        /// Hash of an arbitrary profile — the lab tunes profiles that never
        /// ship, and a report that says which one played needs this.
        /// </summary>
        public static ulong ComputeProfileHash(AiProfile profile)
        {
            // Field order IS the hash. Appending a new field at the end keeps
            // old hashes comparable in the sense that matters here: they were
            // computed from a different set, and the value says so by changing.
            SimHashWriter writer = SimHashWriter.ForDefinitions();
            writer.WriteLengthPrefixedString(profile.ProfileId ?? string.Empty);
            writer.WriteInt32(AiProfile.SchemaVersion);
            writer.WriteUInt16(profile.DecisionTickInterval);
            writer.WriteInt32(profile.PlacementSearchRadius);
            writer.WriteInt32(profile.PowerReserve);
            writer.WriteInt32(profile.TargetHarvesters);
            writer.WriteInt32(profile.HarvesterQueueBatch);
            writer.WriteInt32(profile.TargetArmySize);
            writer.WriteInt32(profile.AttackSquadThreshold);
            writer.WriteInt32(profile.InfantryQueueBatch);
            writer.WriteInt32(profile.TargetDamageWeight);
            writer.WriteInt32(profile.TargetThreatWeight);
            writer.WriteInt32(profile.TargetFinishWeight);
            writer.WriteInt32(profile.TargetDistanceWeight);
            writer.WriteInt32(profile.WaveSize);
            writer.WriteInt32(profile.StagingDistanceCells);
            writer.WriteInt32(profile.StagingToleranceCells);
            writer.WriteInt32(profile.RetreatHealthPercent);
            writer.WriteInt32(profile.RetreatDangerCells);
            writer.WriteInt32(profile.WaveStrengthPoints);
            writer.WriteInt32(profile.DefendHomeCells);
            writer.WriteInt32(profile.ReinforceMinStrengthPercent);
            writer.WriteInt32(profile.TargetHqWeight);
            return writer.Digest();
        }
    }
}
