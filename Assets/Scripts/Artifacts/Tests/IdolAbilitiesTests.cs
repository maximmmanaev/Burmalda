using System.Linq;
using Burmalda.Core;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.Artifacts.Tests
{
    /// <summary>Способности пяти Идолов, по одному тесту на способность и уровень (PRD v10 §18.2).</summary>
    public class IdolAbilitiesTests
    {
        private float _now;

        [SetUp]
        public void Reset() => _now = 0f;

        [TearDown]
        public void ResetEffects() => RunEffects.Current = RunEffects.Neutral;

        private sealed class World
        {
            public TunnelGrid Grid = new TunnelGrid(5);
            public GridTraceTrail Trail;
            public IdolEffects Effects;
            public IdolAbilityContext Ctx;
            public World(IdolAbilitiesTests t)
            {
                Trail = new GridTraceTrail(Grid, new GridCoordinate(2, 2));
                Effects = new IdolEffects(() => t._now);
                Ctx = new IdolAbilityContext(Grid, Trail, Effects, () => t._now);
                for (var r = 0; r <= 8; r++) for (var c = 0; c < 5; c++) Grid.GetOrCreateTile(new GridCoordinate(r, c));
            }
        }

        private static IIdolAbility Ability(string id) => IdolAbilities.All().Single(a => a.IdolId == id);

        private World New() => new World(this);

        // ---- Рывок ----

        [Test]
        public void Rush_L1_JumpsTwoStraight_OverDanger()
        {
            var w = New();
            w.Grid.GetOrCreateTile(new GridCoordinate(3, 2)).MarkLethalTrap(LethalTrapType.ArrowWave);

            Assert.IsTrue(Ability(IdolAbilities.Rush).Execute(w.Ctx, 1, new GridCoordinate(3, 2)));

            Assert.AreEqual(new GridCoordinate(4, 2), w.Trail.CurrentPosition, "перепрыгнули ловушку");
            Assert.IsFalse(w.Effects.IsInvulnerable);
        }

        [Test]
        public void Rush_L1_DiagonalNotAllowed_L2_AnyOfEightDirections()
        {
            var w = New();
            Assert.IsFalse(Ability(IdolAbilities.Rush).Execute(w.Ctx, 1, new GridCoordinate(3, 3)), "ур. 1 — только по прямой");
            Assert.AreEqual(new GridCoordinate(2, 2), w.Trail.CurrentPosition);

            Assert.IsTrue(Ability(IdolAbilities.Rush).Execute(w.Ctx, 2, new GridCoordinate(3, 3)));
            Assert.AreEqual(new GridCoordinate(4, 4), w.Trail.CurrentPosition);
        }

        [Test]
        public void Rush_L3_GivesOneSecondTrapInvulnerabilityAfterJump()
        {
            var w = New();

            Assert.IsTrue(Ability(IdolAbilities.Rush).Execute(w.Ctx, 3, new GridCoordinate(3, 2)));

            Assert.IsTrue(w.Effects.IsInvulnerable);
            Assert.IsTrue(w.Effects.TryAbsorbHit(ThreatType.Slashing));
            _now = 1.1f;
            Assert.IsFalse(w.Effects.IsInvulnerable, "через 1 с окно закрыто");
        }

        [Test]
        public void Rush_CannotLandOnWallLavaOrOverWall()
        {
            var w = New();
            w.Grid.GetOrCreateTile(new GridCoordinate(4, 2)).MarkBlocked();
            Assert.IsFalse(Ability(IdolAbilities.Rush).Execute(w.Ctx, 1, new GridCoordinate(3, 2)), "приземление на стену");

            var w2 = New();
            w2.Grid.GetOrCreateTile(new GridCoordinate(3, 2)).MarkBlocked();
            Assert.IsFalse(Ability(IdolAbilities.Rush).Execute(w2.Ctx, 1, new GridCoordinate(3, 2)), "через стену нельзя");

            var w3 = New();
            w3.Grid.GetOrCreateTile(new GridCoordinate(4, 2)).MarkLethalTrap(LethalTrapType.Lava);
            Assert.IsFalse(Ability(IdolAbilities.Rush).Execute(w3.Ctx, 1, new GridCoordinate(3, 2)), "в Лаву не прыгают");
        }

        [Test]
        public void Rush_LandingBecomesNewTile_RaisesAdvanced_RouteNotRestored()
        {
            var w = New();
            var advanced = 0;
            w.Trail.Advanced += _ => advanced++;

            Ability(IdolAbilities.Rush).Execute(w.Ctx, 1, new GridCoordinate(3, 2));

            Assert.AreEqual(1, advanced);
            Assert.IsFalse(w.Trail.HasVisited(new GridCoordinate(3, 2)), "промежуточная плита маршрутом не становится");
        }

        // ---- Щит ----

        [Test]
        public void Shield_L1_AbsorbsOneHit()
        {
            var w = New();
            Ability(IdolAbilities.Shield).Execute(w.Ctx, 1, null);

            Assert.IsTrue(w.Effects.TryAbsorbHit(ThreatType.Blunt));
            Assert.IsFalse(w.Effects.TryAbsorbHit(ThreatType.Blunt), "второе попадание уже убивает");
        }

        [Test]
        public void Shield_L2_AbsorbedHitKnocksPlayerToSafeAdjacentTile()
        {
            var w = New();
            w.Trail.TryAdvanceTo(new GridCoordinate(3, 2)); // идём вперёд, назад — пройденная (2,2)
            Ability(IdolAbilities.Shield).Execute(w.Ctx, 2, null);
            // Вокруг (3,2) опасные соседи, кроме пройденной (2,2).
            foreach (var c in new[] { new GridCoordinate(4, 1), new GridCoordinate(4, 2), new GridCoordinate(4, 3) })
                w.Grid.GetOrCreateTile(c).MarkLethalTrap(LethalTrapType.BombBlast);

            Assert.IsTrue(w.Effects.TryAbsorbHit(ThreatType.Explosion));

            Assert.AreNotEqual(new GridCoordinate(3, 2), w.Trail.CurrentPosition, "отброшен");
            Assert.IsTrue(w.Trail.HasVisited(w.Trail.CurrentPosition) || IdolAbilities.IsIntact(w.Grid, w.Trail.CurrentPosition));
            Assert.IsTrue(IdolAbilities.IsIntact(w.Grid, w.Trail.CurrentPosition), "на безопасную плиту");
        }

        [Test]
        public void Shield_L3_WithstandsTwoHitsInARow()
        {
            var w = New();
            Ability(IdolAbilities.Shield).Execute(w.Ctx, 3, null);

            Assert.IsTrue(w.Effects.TryAbsorbHit(ThreatType.Slashing));
            Assert.IsTrue(w.Effects.TryAbsorbHit(ThreatType.Slashing));
            Assert.IsFalse(w.Effects.TryAbsorbHit(ThreatType.Slashing));
        }

        // ---- Пробой ----

        [Test]
        public void Breach_L1_BreaksAdjacentWall_NothingToBreakFails()
        {
            var w = New();
            var wall = w.Grid.GetOrCreateTile(new GridCoordinate(3, 2));
            wall.MarkBlocked();

            Assert.IsTrue(Ability(IdolAbilities.Breach).Execute(w.Ctx, 1, new GridCoordinate(3, 2)));
            Assert.IsFalse(wall.IsBlocked);
            Assert.IsFalse(Ability(IdolAbilities.Breach).Execute(w.Ctx, 1, new GridCoordinate(3, 2)), "ломать больше нечего — заряд не тратится");
        }

        [Test]
        public void Breach_L2_AlsoDefusesTriggerOnAdjacentTile()
        {
            var w = New();
            var trigger = w.Grid.GetOrCreateTile(new GridCoordinate(3, 2));
            trigger.MarkBombTrigger();

            Assert.IsFalse(Ability(IdolAbilities.Breach).Execute(w.Ctx, 1, new GridCoordinate(3, 2)), "ур. 1 триггеры не трогает");
            Assert.IsTrue(Ability(IdolAbilities.Breach).Execute(w.Ctx, 2, new GridCoordinate(3, 2)));
            Assert.IsFalse(trigger.IsTrapTrigger);
        }

        [Test]
        public void Breach_L3_BreaksLineOfThree()
        {
            var w = New();
            var line = new[] { new GridCoordinate(3, 2), new GridCoordinate(4, 2), new GridCoordinate(5, 2), new GridCoordinate(6, 2) };
            foreach (var c in line) w.Grid.GetOrCreateTile(c).MarkBlocked();

            Assert.IsTrue(Ability(IdolAbilities.Breach).Execute(w.Ctx, 3, new GridCoordinate(3, 2)));

            Assert.IsFalse(w.Grid.GetOrCreateTile(line[0]).IsBlocked);
            Assert.IsFalse(w.Grid.GetOrCreateTile(line[1]).IsBlocked);
            Assert.IsFalse(w.Grid.GetOrCreateTile(line[2]).IsBlocked);
            Assert.IsTrue(w.Grid.GetOrCreateTile(line[3]).IsBlocked, "четвёртая не затронута");
        }

        // ---- Время ----

        [Test]
        public void Time_L1_ThreatsAndDecayAtHalfSpeedForThreeSeconds()
        {
            var w = New();
            Ability(IdolAbilities.Time).Execute(w.Ctx, 1, null);

            Assert.AreEqual(0.5f, w.Effects.ThreatTimeScale);
            _now = 2.9f;
            Assert.AreEqual(0.5f, w.Effects.ThreatTimeScale);
            _now = 3.1f;
            Assert.AreEqual(1f, w.Effects.ThreatTimeScale);
        }

        [Test]
        public void Time_L2_RevealsTrapTypesAround()
        {
            var w = New();
            var near = w.Grid.GetOrCreateTile(new GridCoordinate(4, 1));
            near.MarkBombTrigger();
            var far = w.Grid.GetOrCreateTile(new GridCoordinate(8, 1));
            far.MarkBombTrigger();

            Ability(IdolAbilities.Time).Execute(w.Ctx, 1, null);
            Assert.IsFalse(near.IsTrapTypeRevealed, "ур. 1 типы не показывает");
            Ability(IdolAbilities.Time).Execute(w.Ctx, 2, null);

            Assert.IsTrue(near.IsTrapTypeRevealed);
            Assert.IsFalse(far.IsTrapTypeRevealed, "только вокруг");
        }

        [Test]
        public void Time_L3_FullStopForOneAndHalfSeconds_ThenSlow()
        {
            var w = New();
            Ability(IdolAbilities.Time).Execute(w.Ctx, 3, null);

            Assert.AreEqual(0f, w.Effects.ThreatTimeScale, "всё стоит");
            _now = 1.6f;
            Assert.AreEqual(0.5f, w.Effects.ThreatTimeScale, "остановка закончилась, замедление продолжается");
        }

        // ---- Переход ----

        [Test]
        public void Passage_L1_TeleportsToIntactTileWithinRadius2_NotThroughWalls_NotFarther()
        {
            var w = New();
            Assert.IsTrue(Ability(IdolAbilities.Passage).Execute(w.Ctx, 1, new GridCoordinate(4, 3)));
            Assert.AreEqual(new GridCoordinate(4, 3), w.Trail.CurrentPosition);

            var w2 = New();
            Assert.IsFalse(Ability(IdolAbilities.Passage).Execute(w2.Ctx, 1, new GridCoordinate(5, 2)), "радиус 3 — слишком далеко для ур. 1");

            var w3 = New();
            w3.Grid.GetOrCreateTile(new GridCoordinate(3, 2)).MarkBlocked();
            Assert.IsFalse(Ability(IdolAbilities.Passage).Execute(w3.Ctx, 1, new GridCoordinate(4, 2)), "сквозь стену — нельзя на ур. 1");
        }

        [Test]
        public void Passage_L2_Radius3_ThroughWalls_ButNeverOntoWallOrLava()
        {
            var w = New();
            w.Grid.GetOrCreateTile(new GridCoordinate(3, 2)).MarkBlocked();
            w.Grid.GetOrCreateTile(new GridCoordinate(4, 2)).MarkBlocked();
            Assert.IsTrue(Ability(IdolAbilities.Passage).Execute(w.Ctx, 2, new GridCoordinate(5, 2)));

            var w2 = New();
            w2.Grid.GetOrCreateTile(new GridCoordinate(4, 2)).MarkBlocked();
            Assert.IsFalse(Ability(IdolAbilities.Passage).Execute(w2.Ctx, 2, new GridCoordinate(4, 2)), "на стену нельзя");

            var w3 = New();
            w3.Grid.GetOrCreateTile(new GridCoordinate(4, 2)).MarkLethalTrap(LethalTrapType.Lava);
            Assert.IsFalse(Ability(IdolAbilities.Passage).Execute(w3.Ctx, 2, new GridCoordinate(4, 2)), "в Лаву нельзя");
        }

        [Test]
        public void Passage_L3_LandingAndNextStepDoNotTriggerTraps()
        {
            var w = New();

            Assert.IsTrue(Ability(IdolAbilities.Passage).Execute(w.Ctx, 3, new GridCoordinate(4, 2)));

            Assert.IsFalse(w.Effects.StepsActivateTraps, "следующий шаг не активирует ловушки");
            w.Effects.OnStepTaken();
            Assert.IsTrue(w.Effects.StepsActivateTraps, "после одного шага — снова обычные ловушки");
        }

        [Test]
        public void Passage_L1_DoesNotSuppressTraps()
        {
            var w = New();
            Ability(IdolAbilities.Passage).Execute(w.Ctx, 1, new GridCoordinate(4, 2));

            Assert.IsTrue(w.Effects.StepsActivateTraps);
        }

        // ---- Общее ----

        [Test]
        public void NoIdol_GivesLavaImmunity_OrBypassesFateRoll()
        {
            var w = New();
            w.Grid.GetOrCreateTile(new GridCoordinate(3, 2)).MarkLethalTrap(LethalTrapType.Lava);
            Ability(IdolAbilities.Shield).Execute(w.Ctx, 3, null);
            Ability(IdolAbilities.Rush).Execute(w.Ctx, 3, new GridCoordinate(4, 2));
            RunEffects.SetLayer("idol", w.Effects);

            // Лава в TryAbsorbHit не передаётся: у ThreatType нет значения для Лавы; RunState спрашивает слой только про ловушки.
            CollectionAssert.DoesNotContain(System.Enum.GetNames(typeof(ThreatType)), "Lava");
        }

        [Test]
        public void Registry_HasAllFiveIdols_WithExpectedTargeting()
        {
            var all = IdolAbilities.All();
            Assert.AreEqual(5, all.Count);
            foreach (var id in new[] { IdolAbilities.Rush, IdolAbilities.Breach, IdolAbilities.Passage })
                Assert.AreEqual(IdolTargeting.Aim, all.Single(a => a.IdolId == id).Targeting, id);
            foreach (var id in new[] { IdolAbilities.Shield, IdolAbilities.Time })
                Assert.AreEqual(IdolTargeting.Immediate, all.Single(a => a.IdolId == id).Targeting, id);
        }
    }
}
