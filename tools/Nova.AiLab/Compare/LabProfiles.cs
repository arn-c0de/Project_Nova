using System;
using System.Collections.Generic;
using Nova.AI.Data;

namespace Nova.AiLab
{
    /// <summary>
    /// Candidate profiles that exist only in the lab (plan section 4.6:
    /// "Abweichende Profile existieren zunächst nur im Labor").
    /// <para>
    /// These are the second axis of a comparison — and since the first sweep
    /// proved the SEED axis empty (no simulation system draws from the kernel
    /// PRNG), they are currently the ONLY axis with variance in it. A report
    /// over n seeds is one observation; a report over n profiles is n.
    /// </para>
    /// <para>
    /// Every candidate is a deliberate, one-sentence-explainable deviation from
    /// the shipped profile. Rastering all eight values would produce thousands
    /// of runs and no insight: the lab does not rank
    /// (<see cref="ComparisonReport"/>), a human picks, and a human cannot pick
    /// from a thousand rows.
    /// </para>
    /// </summary>
    public static class LabProfiles
    {
        /// <summary>The shipped profile, unchanged — the fixed yardstick every comparison runs against.</summary>
        public static AiProfile Reference => AiProfiles.Ms1Canonical;

        /// <summary>
        /// The named candidates. Ordered, not a dictionary literal iterated by
        /// hash: a report whose row order depends on hashing is a report two
        /// runs disagree about.
        /// </summary>
        public static IReadOnlyList<AiProfile> Candidates { get; } = new[]
        {
            Reference,

            // Attacks earlier with a smaller army. The question it answers:
            // does the shipped threshold of 6 leave the AI standing around?
            Derive("early-push", attackSquadThreshold: 3, targetArmySize: 10),

            // Waits for a bigger army before marching.
            Derive("late-push", attackSquadThreshold: 12, targetArmySize: 20),

            // More harvesters, later army — the economy question.
            Derive("greedy-economy", targetHarvesters: 4, targetArmySize: 16, attackSquadThreshold: 8),

            // Keeps a power reserve instead of building reactively. The shipped
            // value is 0, which means "react when the margin would go negative".
            Derive("power-buffer", powerReserve: 30),

            // Decides twice as often. Costs decision ticks, reacts sooner —
            // and this value was UNREACHABLE before E6, because it was a const.
            Derive("fast-cadence", decisionTickInterval: 10),
        };

        public static bool TryGet(string profileId, out AiProfile profile)
        {
            for (int i = 0; i < Candidates.Count; i++)
            {
                if (!string.Equals(Candidates[i].ProfileId, profileId, StringComparison.Ordinal)) continue;
                profile = Candidates[i];
                return true;
            }
            profile = default;
            return false;
        }

        public static string KnownIds()
        {
            var ids = new List<string>(Candidates.Count);
            for (int i = 0; i < Candidates.Count; i++) ids.Add(Candidates[i].ProfileId);
            return string.Join(", ", ids);
        }

        /// <summary>
        /// A candidate is the shipped profile with named values replaced —
        /// so a candidate differs from the reference in exactly the ways its
        /// definition names, and in no other way that could creep in later.
        /// </summary>
        private static AiProfile Derive(
            string profileId,
            ushort? decisionTickInterval = null,
            int? placementSearchRadius = null,
            int? powerReserve = null,
            int? targetHarvesters = null,
            int? harvesterQueueBatch = null,
            int? targetArmySize = null,
            int? attackSquadThreshold = null,
            int? infantryQueueBatch = null,
            int? targetDamageWeight = null,
            int? targetThreatWeight = null,
            int? targetFinishWeight = null,
            int? targetDistanceWeight = null)
        {
            AiProfile b = Reference;
            return new AiProfile(
                profileId: profileId,
                decisionTickInterval: decisionTickInterval ?? b.DecisionTickInterval,
                placementSearchRadius: placementSearchRadius ?? b.PlacementSearchRadius,
                powerReserve: powerReserve ?? b.PowerReserve,
                targetHarvesters: targetHarvesters ?? b.TargetHarvesters,
                harvesterQueueBatch: harvesterQueueBatch ?? b.HarvesterQueueBatch,
                targetArmySize: targetArmySize ?? b.TargetArmySize,
                attackSquadThreshold: attackSquadThreshold ?? b.AttackSquadThreshold,
                infantryQueueBatch: infantryQueueBatch ?? b.InfantryQueueBatch,
                targetDamageWeight: targetDamageWeight ?? b.TargetDamageWeight,
                targetThreatWeight: targetThreatWeight ?? b.TargetThreatWeight,
                targetFinishWeight: targetFinishWeight ?? b.TargetFinishWeight,
                targetDistanceWeight: targetDistanceWeight ?? b.TargetDistanceWeight);
        }

        /// <summary>Which values a candidate changed against the reference, for the report.</summary>
        public static List<string> DifferencesFromReference(AiProfile candidate)
        {
            AiProfile r = Reference;
            var diffs = new List<string>();
            if (candidate.DecisionTickInterval != r.DecisionTickInterval)
                diffs.Add($"cadence {r.DecisionTickInterval}→{candidate.DecisionTickInterval}");
            if (candidate.PlacementSearchRadius != r.PlacementSearchRadius)
                diffs.Add($"placementRadius {r.PlacementSearchRadius}→{candidate.PlacementSearchRadius}");
            if (candidate.PowerReserve != r.PowerReserve)
                diffs.Add($"powerReserve {r.PowerReserve}→{candidate.PowerReserve}");
            if (candidate.TargetHarvesters != r.TargetHarvesters)
                diffs.Add($"harvesters {r.TargetHarvesters}→{candidate.TargetHarvesters}");
            if (candidate.HarvesterQueueBatch != r.HarvesterQueueBatch)
                diffs.Add($"harvesterBatch {r.HarvesterQueueBatch}→{candidate.HarvesterQueueBatch}");
            if (candidate.TargetArmySize != r.TargetArmySize)
                diffs.Add($"armySize {r.TargetArmySize}→{candidate.TargetArmySize}");
            if (candidate.AttackSquadThreshold != r.AttackSquadThreshold)
                diffs.Add($"squadThreshold {r.AttackSquadThreshold}→{candidate.AttackSquadThreshold}");
            if (candidate.InfantryQueueBatch != r.InfantryQueueBatch)
                diffs.Add($"infantryBatch {r.InfantryQueueBatch}→{candidate.InfantryQueueBatch}");
            // Ohne diese vier meldete der Bericht "geaendert: —" fuer einen
            // Kandidaten, der sich sehr wohl unterscheidet — eine stille
            // Luecke waere schlimmer als eine fehlende Zeile.
            if (candidate.TargetDamageWeight != r.TargetDamageWeight)
                diffs.Add($"targetDmg {r.TargetDamageWeight}→{candidate.TargetDamageWeight}");
            if (candidate.TargetThreatWeight != r.TargetThreatWeight)
                diffs.Add($"targetThreat {r.TargetThreatWeight}→{candidate.TargetThreatWeight}");
            if (candidate.TargetFinishWeight != r.TargetFinishWeight)
                diffs.Add($"targetFinish {r.TargetFinishWeight}→{candidate.TargetFinishWeight}");
            if (candidate.TargetDistanceWeight != r.TargetDistanceWeight)
                diffs.Add($"targetDist {r.TargetDistanceWeight}→{candidate.TargetDistanceWeight}");
            return diffs;
        }
    }
}
