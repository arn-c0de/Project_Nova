namespace Nova.AI.Data
{
    /// <summary>
    /// The profiles the game ships with.
    /// <para>
    /// A CODE TABLE, not a JSON file loaded at runtime — the same shape
    /// <c>SimDefinitions</c> already uses for the definition table, and for the
    /// same reasons: <c>static readonly</c> data is thread-safe (so N matches
    /// run on N cores without locks), it cannot fail to parse mid-match, and it
    /// raises no question about what a missing file would mean for the match
    /// fingerprint. The plan sketches profiles as JSON; that shape is the right
    /// one for LAB profiles, which are read by the lab and handed in as values.
    /// A shipped profile is part of the build.
    /// </para>
    /// </summary>
    public static class AiProfiles
    {
        /// <summary>
        /// What MatchRunner ships today, value for value.
        /// <para>
        /// These eight numbers are the whole point of E6 being
        /// behaviour-neutral: four came from <c>MatchRunner</c>'s
        /// <c>AiFactionProfile</c> call (power margin 0, army 12, squad
        /// threshold 6, harvesters 2) and four from <c>const</c> fields inside
        /// <c>SkirmishAiSystem</c> (cadence 20, placement radius 8, both queue
        /// batches 2). Nothing was rounded, retuned or "improved" on the way
        /// across, and <c>AiProfileTests</c> asserts every one of them value
        /// for value.
        /// </para>
        /// <para>
        /// WHAT DOES NOT CHECK THIS: the four determinism baselines. Not one
        /// of them runs the skirmish AI — <c>Determinism10000Scenario</c>
        /// registers Economy, Construction, Production, Pathfinding, Movement,
        /// FogOfWar, Combat and Victory, and the other three pin snapshot
        /// bytes, command serialisation and the RNG. Every number here can
        /// move without turning any of them red. The guard against that is the
        /// end-state pin in <c>SkirmishAiTests</c>.
        /// </para>
        /// </summary>
        public static readonly AiProfile Ms1Canonical = new AiProfile(
            profileId: "ms1-canonical",
            decisionTickInterval: 20,
            placementSearchRadius: 8,
            powerReserve: 0,
            targetHarvesters: 2,
            harvesterQueueBatch: 2,
            // THE ARMY CAP IS THE NUMBER THAT SHOULD MOVE NEXT, and moving it
            // here alone would not move the game. MatchRunner takes fifteen of
            // this profile's nineteen values through the historical
            // AiFactionProfile constructor and OVERRIDES four with literals of
            // its own — this one among them. Change it here and the lab plays
            // differently while the game does not, and the promise above
            // ("what MatchRunner ships today, value for value") quietly stops
            // being true. AiProfileTests.MatchRunnerPassesTheSameFourNumbers...
            // is what keeps the four honest; it reads MatchRunner's source.
            //
            // Why it matters for r6: waveStrengthPoints 1200 are 28 Legion
            // recruits at 44 points each, and the point clause can only decide
            // anything while a head is still free — so it first binds at a cap
            // of 29. Below that the gate does not "fall back to a head count":
            // it degenerates into "gather the entire army cap", which is why
            // caps 22, 24 and 28 were measured as grinds (78, 178 and 154 own
            // losses on the Legion seat). Measured one-sided, 30 is where it
            // works: the Legion decides FASTER than today (5.005 against 5.773
            // ticks) with 23 own losses instead of 51 and an exchange ratio of
            // 139 against 45. Raising the cap WITHOUT the gate goes the other
            // way (own losses 51 -> 64), which is why the gate ships first.
            //
            // The request is written up in the PR; the one line is
            // MatchRunner's, and MatchRunner belongs to the network strand.
            targetArmySize: 12,
            attackSquadThreshold: 6,
            infantryQueueBatch: 2,
            // Target scoring (E7). These four are the first numbers here that
            // are NOT a copy of a shipped constant — there was no scoring
            // before, only "HQ, else the first visible building, else the
            // first visible unit". They come from the plan's sketch
            // (AiSimulationEnvironment.md section 4.6). They change how the AI
            // plays and therefore move the end-state pin in SkirmishAiTests;
            // the four determinism baselines do not see this system and stay
            // green either way.
            targetDamageWeight: 10,
            targetThreatWeight: 6,
            targetFinishWeight: 3,
            targetDistanceWeight: 4,
            // Waves. SINCE r6 THIS NUMBER NO LONGER SETS THE WAVE — the
            // threshold is waveStrengthPoints below, and this value is read
            // for exactly two things: 1 still means "waves off" (and takes the
            // strength gate off with them), and anything above 1 is the
            // threshold of the COUNT path, which now only runs while
            // waveStrengthPoints is 0. It stays at 12 because that is the value
            // the count path was measured with; it is no longer paired to the
            // army cap, and it must not be re-paired to it.
            //
            // What it meant while it did set the wave: attack at full strength,
            // never reinforce piecemeal. A reinforcement waits at the staging
            // cell until the wave is full again. Units already out are never
            // called back — that part is unchanged.
            //
            // Measured one-sided against waveSize 1 over five sizes (4, 6, 8,
            // 10, 12) and both faction seatings; every column improves
            // monotonically with the size, which is why the value sits at the
            // cap and not somewhere in the middle. waveSize 1 turns the rule
            // off and reproduces the previous behaviour exactly — the lab
            // keeps that as the candidate `wave-off`, because a behaviour
            // without an off setting cannot be measured one-sided (M001).
            waveSize: 12,
            stagingDistanceCells: 12,
            stagingToleranceCells: 4,
            // Retreat. A unit under 60 % health with an armed enemy within
            // eight cells walks back to the staging cell instead of dying
            // where it stands; once home it is an ordinary waiting unit and
            // leaves with the next wave. There is no exit percentage and
            // there cannot be one — MS-1 units never heal, see AiProfile.
            //
            // 60 is not a middle value, it is the top of a measured curve.
            // One-sided against retreat off, exchange ratio as Alliance:
            // 25 -> 138, 40 -> 184, 60 -> 252, 75 -> 290, 90 -> 209. 75 buys
            // its higher ratio with a match that runs twice as long and twice
            // the own losses; 90 turns the curve down on both seatings. 0
            // switches the rule off, which is how it was measured (M001).
            retreatHealthPercent: 60,
            retreatDangerCells: 8,
            // Wave strength (r6). 1.200 points is what twelve Alliance
            // riflemen at full health weigh, so on the Alliance seat the
            // threshold reads as "the same wave as before" — and on the Legion
            // seat it reads as "no, twelve recruits are not a full wave", which
            // is the whole correction. The value is one number for both
            // factions on purpose; see AiProfile.WaveStrengthPoints.
            //
            // IT ONLY BITES WHILE THE ARMY CAP ALLOWS MORE STRENGTH TO GATHER
            // THAN IT ASKS FOR — the threshold is capped at what production can
            // still deliver (see ResolveArmyPosture). At the old cap of 12 that
            // ceiling bound first on both seats and the gate decided exactly
            // like the head count, byte for byte. Which is why this value and
            // the cap above ship together and why the cap is derived from this
            // value, not chosen next to it.
            //
            // 0 switches the rule off and restores the head count exactly
            // (finding M001: a behaviour without an off setting cannot be
            // measured one-sided).
            waveStrengthPoints: 1200,
            // Defend home (r8). Ten cells around the own headquarters: inside
            // the base, and short of the staging ring at sixteen, so a skirmish
            // at the gathering point does not summon anybody.
            //
            // THE RULE EXISTS BECAUSE OF A MEASURED HOLE, not a hunch. A unit
            // that has arrived at the staging cell is given no order at all —
            // on purpose, an order per cadence to a standing unit is churn — so
            // it depends entirely on the D-087 auto-acquisition, and that
            // reaches six cells for Legion infantry and seven for an Alliance
            // rifleman. The staging cell sits twelve cells from the base. An
            // attacker at the headquarters is outside all of it. In the
            // canonical match the Legion headquarters takes 327 hits over 766
            // ticks while its own units stand a median of thirteen cells away
            // under Hold, and not one of them attacks.
            //
            // The wave is INTERRUPTED, not released: the destination is the own
            // headquarters, a static cell, so the defenders walk toward the
            // fight rather than away from it, the re-issue suppression swallows
            // the repeats on the way, and a defender that has arrived is not
            // ordered any more at all (the suppression alone cannot carry that
            // — arriving clears the standing order it compares against). That is
            // the correction over the discarded DefendBase (V002), which handed
            // the whole army a moving target every cadence and paid 23 % more
            // intents for it.
            //
            // 0 switches the rule off and restores r7 exactly (M001).
            defendHomeCells: 10,
            // Reinforcement doctrine (r9). 0 IS THE MEASURED ANSWER, not a
            // placeholder and not caution — the rule was built, switched on and
            // measured one-sided over eleven settings and both seatings, and no
            // setting survived.
            //
            // AT THIS CAP ONLY HALF THE RULE EVEN RUNS. The r5 reachability
            // ceiling has already collapsed the wave threshold onto what stands
            // in the ring whenever the army is at its cap, so the gate is open
            // and reinforcements march anyway; the half that RELEASES them has
            // nothing to release, and what the numbers below measure is the half
            // that HOLDS THEM BACK. ReinforcementDoctrineTests asserts that
            // dormancy rather than leaving it to be rediscovered.
            //
            // The two seats want different values and their good ranges overlap
            // in a single point. Own losses, one-sided, Alliance / Legion, off
            // is 33 / 71: 25 -> 43 / 93, 30 -> 55 / 33, 35 -> 35 / 33,
            // 40 -> 21 / 34, 45 -> 21 / 83, 50 -> 51 / 83, 60 -> 33 / 83,
            // 70 -> 28 / 83, 90 -> 58 / 83. The Alliance improves at 40-45 and
            // 70-80, the Legion only at 30-40, and 40 is the whole intersection.
            // One step to either side loses a seat. The lab's seed axis is empty
            // (no system draws from the kernel PRNG), so no further sampling can
            // widen that point — taking it would be hitting a single match, the
            // same mistake the army-cap curve made at 20 in V007.
            //
            // AND ABOVE THE CEILING, WHERE THE WHOLE RULE RUNS, IT IS WORSE ON
            // EVERY SETTING. At a cap of 30 measured against the same cap
            // without the doctrine (Alliance own losses 37): 30 -> 102,
            // 40 -> 63, 50 -> 101, 60 -> 77, 70 -> 102, 80 -> 73 — and at 30,
            // 70 and 80 the Alliance seat LOSES a match it wins without the
            // rule. That is the state the strength strand is heading toward, so
            // it is the number that decides.
            //
            // What the revision buys anyway: the trickle has a NAME and a
            // SWITCH. Until now it was a side effect of the r5 guard, invisible
            // in every report and impossible to turn off. It is now a goal the
            // panel draws and a value the lab varies. Turning it on needs a new
            // measurement, not an edit here.
            reinforceMinStrengthPercent: 0,
            // Headquarters weight (r10). 0 is the short circuit V001 shipped —
            // take the enemy HQ the moment it is seen. 100 lets it compete
            // instead, and 100 is where the curve stops costing anything.
            //
            // THE NUMBER THIS IS JUDGED ON IS NOT THE LOSS COLUMN. It is how
            // many KINDS of target the AI attacks in a match, and it has been
            // effectively one since V001: the HQ short-circuited, and when
            // nothing was visible the march destination was the enemy start
            // area, which on this map is where the HQ stands. Both roads led to
            // the same building, so a player learns the line in two games and
            // parks on it (journal B001). Measured one-sided on the Alliance
            // seat, kinds go 3 -> 5 and the refinery and the harvesters become
            // targets for the first time.
            //
            // THE CURVE IS MONOTONE AND THE PRICE IS AT THE BOTTOM. A small
            // weight lets the HQ lose often, so the AI nibbles at outbuildings
            // and the match drags: at 1 to 75 the kinds rise to 5-7 but the
            // match runs 1,4 to 1,9 times as long and the intents per 1.000
            // ticks go from 43,2 to 53-58. That is the V002 shape and it is the
            // reason none of those ship. At 100: kinds 5, decided 4.767 instead
            // of 7.381, own losses 14 instead of 33, intents 42,4 — below the
            // off setting. Above it the outbuildings drop out again (150 and
            // 200 attack two kinds, fewer than the short circuit), and from 250
            // the HQ always wins and the match is byte-identical to 0.
            //
            // CROSS-CHECKED ON A SECOND REAL AXIS, because the lab's seed axis
            // is empty and one good value on one axis is a single match. At
            // army caps 16, 20 and 30, each against the SAME cap without the
            // weight, the kinds rise every time (3 -> 4, the refinery joining
            // in each) and own losses go 24 -> 27, 147 -> 50, 37 -> 30. Three
            // of four caps better, one marginally worse, and the intents move
            // only at cap 20.
            //
            // TWO HONEST LIMITS. The Legion seat is unchanged at almost every
            // setting because it never lives to see the enemy HQ in this match,
            // so the measurement rests on one seat. And the curve must not be
            // interpolated: 75 and 150 are both worse than 100.
            targetHqWeight: 100);

        /// <summary>
        /// The old <c>AiFactionProfile</c> constructor defaults (power margin
        /// 30, army 15, squad threshold 8, harvesters 2), kept so the
        /// parameterless path behaves as it did. Nothing in the game selects
        /// it: every caller passes its numbers explicitly, which is exactly why
        /// the defaults could drift unnoticed from what actually ships.
        /// </summary>
        public static readonly AiProfile LegacyDefaults = new AiProfile(
            profileId: "legacy-defaults",
            decisionTickInterval: 20,
            placementSearchRadius: 8,
            powerReserve: 30,
            targetHarvesters: 2,
            harvesterQueueBatch: 2,
            targetArmySize: 15,
            attackSquadThreshold: 8,
            infantryQueueBatch: 2,
            // Same weights as the shipped profile: there is no "legacy" value
            // to preserve here, target scoring did not exist before E7.
            targetDamageWeight: 10,
            targetThreatWeight: 6,
            targetFinishWeight: 3,
            targetDistanceWeight: 4,
            // Same reasoning as above: there is no "legacy" wave value to
            // preserve, waves did not exist.
            waveSize: 1,
            stagingDistanceCells: 12,
            stagingToleranceCells: 4,
            retreatHealthPercent: 0,
            retreatDangerCells: 8,
            // Same reasoning again: there is no "legacy" strength value, the
            // wave counted heads. 0 is the off setting.
            waveStrengthPoints: 0,
            // And no legacy defence value either — there was no defence rule.
            defendHomeCells: 0,
            // Nor a legacy reinforcement doctrine. 0 is the off setting.
            reinforceMinStrengthPercent: 0,
            // And no legacy headquarters weight — it was a short circuit, which
            // is what 0 means.
            targetHqWeight: 0);
    }
}
