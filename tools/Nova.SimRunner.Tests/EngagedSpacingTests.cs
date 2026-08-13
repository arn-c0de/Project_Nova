using NUnit.Framework;
using Nova.Core;
using Nova.Simulation;
using Nova.Simulation.Movement;
using Nova.Simulation.Pathfinding;
using Nova.Simulation.State;

namespace Nova.SimRunner.Tests
{
    /// <summary>
    /// The separation rule of <see cref="MovementSystem"/>: two units of the
    /// SAME player that are BOTH engaged keep more than contact distance, so a
    /// firing line is a line instead of a blob.
    /// <para>
    /// FOUR CASES, ONE GEOMETRY. The same pair is placed at contact distance
    /// four times and only the engagement and ownership flags differ, so what
    /// the assertions compare is the rule and nothing else — not a spawn, not a
    /// path, not a tick budget. Three of the four must NOT spread: the rule is
    /// as much about where it stays silent as about where it fires, and the
    /// silent cases are the ones a later change would break by accident.
    /// </para>
    /// <para>
    /// WHY THE ASYMMETRIC CASE HAS A TEST OF ITS OWN. Each unit computes the
    /// pair's minimum distance for itself, in a different iteration of the same
    /// sweep. A rule that fired for one of them and not the other would have
    /// one unit pushing away from a neighbour that does not yield and does not
    /// follow — a slow one-sided drift that no assertion on a single position
    /// would catch, and that on two machines would still be identical, so the
    /// determinism suites would stay green while the formation walked off the
    /// map. Hence: one engaged, one not, and nobody moves.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class EngagedSpacingTests
    {
        private const ushort MapSize = 32;

        /// <summary>Contact distance of two default units: 0.5 m radius each.</summary>
        private static readonly SimFixed ContactDistance = SimFixed.FromInt(1);

        /// <summary>Ticks given to a standing pair to settle — the correction is damped and capped.</summary>
        private const int SettleTicks = 60;

        [Test]
        public void TwoEngagedUnitsOfTheSamePlayer_SpreadBeyondContactDistance()
        {
            SimFixed spread = Settle(engagedA: true, engagedB: true, samePlayer: true);

            Assert.That(spread, Is.GreaterThan(ContactDistance),
                "two engaged units of the same player must hold more than the bare contact distance — "
                + "that extra room IS the rule");
        }

        [Test]
        public void TwoIdleUnits_StayAtContactDistance()
        {
            SimFixed spread = Settle(engagedA: false, engagedB: false, samePlayer: true);

            Assert.That(spread, Is.LessThanOrEqualTo(ContactDistance),
                "a travelling pair must keep travelling at contact distance — a column that walks "
                + "wider takes longer through every gap the flow field routes it through");
        }

        [Test]
        public void OneEngagedOneIdle_StayAtContactDistance()
        {
            SimFixed spread = Settle(engagedA: true, engagedB: false, samePlayer: true);

            Assert.That(spread, Is.LessThanOrEqualTo(ContactDistance),
                "the rule must be symmetric: one engaged neighbour is not enough, or the two units "
                + "disagree about how far apart they belong and one drifts away from the other");
        }

        [Test]
        public void TwoEngagedEnemies_StayAtContactDistance()
        {
            SimFixed spread = Settle(engagedA: true, engagedB: true, samePlayer: false);

            Assert.That(spread, Is.LessThanOrEqualTo(ContactDistance),
                "spacing is formation, not a force field — an enemy is pushed no further away than "
                + "its body requires");
        }

        /// <summary>
        /// Places two standing units exactly at contact distance, lets the
        /// separation steering settle, and returns the distance they ended up
        /// holding. Nothing here moves under its own orders: both units are
        /// standing, so the only thing that can change the gap is the rule.
        /// </summary>
        private static SimFixed Settle(bool engagedA, bool engagedB, bool samePlayer)
        {
            var entities = new EntityManager(8);
            var pathfinding = new PathfindingSystem(MapSize, MapSize);
            var movement = new MovementSystem(entities, pathfinding);

            SimFixed midX = SimFixed.FromInt(MapSize / 2);
            SimFixed midY = SimFixed.FromInt(MapSize / 2);

            EntityId a = entities.SpawnUnit(
                playerId: 0,
                new Transform2D(midX, midY, SimAngle.Zero),
                moveSpeed: SimFixed.FromInt(4));
            EntityId b = entities.SpawnUnit(
                playerId: samePlayer ? (byte)0 : (byte)1,
                new Transform2D(midX + ContactDistance, midY, SimAngle.Zero),
                moveSpeed: SimFixed.FromInt(4));

            // A target id never has to exist: the movement system reads the
            // FLAG on the order, not the entity behind it, and combat is not
            // registered here. Two different ids so nothing can pass by
            // accidentally comparing them.
            if (engagedA) entities.GetUnitRef(a).AttackTarget = new EntityId(90, 1);
            if (engagedB) entities.GetUnitRef(b).AttackTarget = new EntityId(91, 1);

            for (int tick = 1; tick <= SettleTicks; tick++)
            {
                movement.ExecuteTick(new Tick((uint)tick));
            }

            return SimTrig.Sqrt(
                entities.GetUnitRef(a).Transform.DistanceToSquared(entities.GetUnitRef(b).Transform));
        }
    }
}
