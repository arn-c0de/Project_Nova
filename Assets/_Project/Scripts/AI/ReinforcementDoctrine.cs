namespace Nova.AI
{
    /// <summary>Which of the three situations the reinforcement doctrine sees.</summary>
    public enum ReinforcementStance : byte
    {
        /// <summary>
        /// The doctrine is switched off (<c>AiProfile.ReinforceMinStrengthPercent</c>
        /// 0) or has nothing to compare (the count path carries no points).
        /// The wave gate keeps its answer untouched.
        /// </summary>
        Off = 0,

        /// <summary>
        /// Nothing is outside the ring: this is the first strike, and it is the
        /// wave gate's own business. The doctrine changes nothing.
        /// </summary>
        FirstStrike = 1,

        /// <summary>
        /// A wave is out there and still worth at least the configured share of
        /// a full one. Every unit in the ring follows it now
        /// (<see cref="Nova.AI.Data.GoalKind.Reinforce"/>) — a single unit joining
        /// a fight that is still going is reinforcement.
        /// </summary>
        Reinforce = 2,

        /// <summary>
        /// What is outside is a remnant. Nobody follows it; the ring is held to
        /// the full threshold and gathers a new wave instead.
        /// </summary>
        WaveBroken = 3,
    }

    /// <summary>
    /// "Is the wave that already marched still worth following?", as a pure
    /// function (behaviour revision 9).
    /// <para>
    /// IT LIVES IN ITS OWN TYPE FOR THE REASON <see cref="WaveStrengthGate"/>
    /// does, and that reason was earned rather than assumed: buried as a private
    /// method it could only be reached through a whole match, and a match cannot
    /// produce the states this has to be right about — the exact boundary, a
    /// percentage that truncates, a remnant worth a single point. Mutation
    /// testing made the same point about the gate: with the arithmetic buried,
    /// deleting its clamp left the whole suite green.
    /// </para>
    /// <para>
    /// WHAT IT DOES NOT DO is decide anything about a unit. It classifies the
    /// situation and hands back the number the classification was reached
    /// against; turning that into a wave verdict and a goal is the caller's
    /// business, and keeping the two apart is what lets this be tested without
    /// a world.
    /// </para>
    /// <para>
    /// No state, no fields, integer only — the same rules the simulation runs
    /// under, because two machines have to reach this answer identically.
    /// </para>
    /// </summary>
    public static class ReinforcementDoctrine
    {
        /// <summary>
        /// The strength below which the wave outside counts as broken:
        /// <paramref name="percent"/> of <paramref name="wavePoints"/>.
        /// <para>
        /// ONE TRUNCATION, PINNED. 1.200 points at 40 % is 480 and at 33 % is
        /// 396 — integer division, identical on every machine, which is the
        /// only property the netcode cares about. <c>long</c> for the product
        /// because a profile is free to carry a large threshold and the
        /// intermediate must not wrap where the result would not.
        /// </para>
        /// </summary>
        public static long BrokenThreshold(int wavePoints, int percent)
        {
            if (wavePoints <= 0 || percent <= 0) return 0;
            return (long)wavePoints * percent / 100;
        }

        /// <summary>
        /// Which situation the army is in, and the threshold it was judged
        /// against.
        /// <para>
        /// THE ORDER OF THE TESTS IS THE RULE. Off first, because a switched-off
        /// rule may not even look; then "nothing outside", because a first
        /// strike is not a broken wave and must not be treated as one — that
        /// confusion would hold the opening wave back forever, since a threshold
        /// of anything is above a committed strength of nothing.
        /// </para>
        /// <para>
        /// THE BOUNDARY IS INCLUSIVE: exactly the threshold still counts as
        /// intact. Stated here rather than at the call site so there is one
        /// comparison operator and not two that can drift.
        /// </para>
        /// </summary>
        /// <param name="percent">Profile share of the full threshold; 0 switches the doctrine off.</param>
        /// <param name="wavePoints">The full wave threshold in combat points; 0 means the count path, which has none.</param>
        /// <param name="committedStrength">Summed combat points of the units already outside the ring.</param>
        /// <param name="brokenThreshold">The strength the wave outside was compared against; 0 when the doctrine did not look.</param>
        public static ReinforcementStance Resolve(
            int percent, int wavePoints, long committedStrength, out long brokenThreshold)
        {
            brokenThreshold = 0;
            if (percent <= 0 || wavePoints <= 0) return ReinforcementStance.Off;
            if (committedStrength <= 0) return ReinforcementStance.FirstStrike;

            brokenThreshold = BrokenThreshold(wavePoints, percent);
            return committedStrength >= brokenThreshold
                ? ReinforcementStance.Reinforce
                : ReinforcementStance.WaveBroken;
        }
    }
}
