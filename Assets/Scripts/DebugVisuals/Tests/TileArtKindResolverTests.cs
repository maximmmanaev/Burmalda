using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.DebugVisuals.Tests
{
    /// <summary>
    /// Issue "Завести арт в игру": та же матрица приоритетов, что и
    /// <see cref="TileDebugColorTests"/> (см. её комментарии по каждому
    /// кейсу) — <see cref="TileArtKindResolver"/> обязан воспроизводить
    /// ветвление <see cref="TileDebugColor.Resolve"/> один в один, просто
    /// возвращая категорию арта вместо цвета.
    /// </summary>
    public class TileArtKindResolverTests
    {
        [Test]
        public void Resolve_Destroyed_ReturnsDestroyed_RegardlessOfOtherFlags()
        {
            var state = new TileVisualState(isStart: true, isCurrentPosition: true, isDestroyed: true, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileArtKind.Destroyed, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_CurrentPosition_NotDestroyed_ReturnsNone_EvenIfAlsoStart()
        {
            // Готового арта под текущую позицию в пакете нет — TunnelDebugVisual
            // сам падает на TileDebugColor.CurrentPositionColor для None.
            var state = new TileVisualState(isStart: true, isCurrentPosition: true, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileArtKind.None, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_Start_NotCurrentNotDestroyed_ReturnsStart()
        {
            var state = new TileVisualState(isStart: true, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileArtKind.Start, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_Blocked_NotStartNotCurrentNotDestroyed_ReturnsBlocked()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: true, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileArtKind.Blocked, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_Boss_NotStartNotCurrentNotDestroyed_ReturnsNone()
        {
            // Categорий Boss/ в пакете нет (Комната Босса ещё не реализована) —
            // остаётся на цветном fallback (TileDebugColor.BossColor).
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isBoss: true);

            Assert.AreEqual(TileArtKind.None, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_Boss_ButDestroyed_DestroyedTakesPriority()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: true, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isBoss: true);

            Assert.AreEqual(TileArtKind.Destroyed, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_Blocked_IgnoresDecayProgress()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: true, lethalTrap: null, decayProgress01: 0.9f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileArtKind.Blocked, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_Pit_ReturnsHiddenTrapSignature()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Pit, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileArtKind.HiddenTrapSignature, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_Lava_ReturnsLava()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Lava, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileArtKind.Lava, TileArtKindResolver.Resolve(state));
        }

        // Issue #163: сигнатура не должна различать яму и взрыв — иначе выдаёт точный тип.
        [Test]
        public void Resolve_PitAndExplosion_ProduceIdenticalKind()
        {
            var pitState = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Pit, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);
            var explosionState = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Explosion, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileArtKindResolver.Resolve(pitState), TileArtKindResolver.Resolve(explosionState));
        }

        [Test]
        public void Resolve_ExplosiveTrapTrigger_NotArmedYet_ReturnsTriggerSignature()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: true, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileArtKind.TriggerSignature, TileArtKindResolver.Resolve(state));
        }

        // Задача «сделать тоннель играбельным», часть 3: единая сигнатура —
        // тот же принцип, что уже есть для Pit/Explosion (issue #163).
        [Test]
        public void Resolve_ExplosiveTrigger_AndTimedTrapTrigger_ProduceIdenticalKind()
        {
            var explosiveState = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: true, isTimedTrapTrigger: false, activeTimedTrap: null);
            var timedState = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: true, activeTimedTrap: null);

            Assert.AreEqual(TileArtKindResolver.Resolve(explosiveState), TileArtKindResolver.Resolve(timedState));
        }

        [Test]
        public void Resolve_TimedTrapActive_Arrow_ReturnsTimedTrapActive()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: TimedTrapType.Arrow);

            Assert.AreEqual(TileArtKind.TimedTrapActive, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_TimedTrapActive_Blade_ReturnsSameKindAsArrow()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: TimedTrapType.Blade);

            Assert.AreEqual(TileArtKind.TimedTrapActive, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_TimedTrapTrigger_NotActiveYet_ReturnsTriggerSignature()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: true, activeTimedTrap: null);

            Assert.AreEqual(TileArtKind.TriggerSignature, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_TimedTrapActive_TakesPriorityOverTimedTrapTrigger()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: true, activeTimedTrap: TimedTrapType.Arrow);

            Assert.AreEqual(TileArtKind.TimedTrapActive, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_RegularTile_ZeroDecayProgress_ReturnsFresh()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileArtKind.Fresh, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_RegularTile_HalfDecayProgress_ReturnsHalfDecayed()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0.5f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileArtKind.HalfDecayed, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_RegularTile_FullDecayProgress_ReturnsAboutToDecay()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 1f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileArtKind.AboutToDecay, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_ProgressAboveOne_ClampsToAboutToDecay()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 5f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileArtKind.AboutToDecay, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_ProgressBelowZero_ClampsToFresh()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: -1f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileArtKind.Fresh, TileArtKindResolver.Resolve(state));
        }

        // Задача «сделать тоннель играбельным», часть 1: нет тайл-текстур под
        // источники валюты в текущем пакете — None, TunnelDebugVisual падает
        // на цветной фолбэк (TileDebugColor.ManaSourceColor/KeySourceColor),
        // не на градиент распада.
        [Test]
        public void Resolve_ManaSource_ReturnsNone_NotDecayGradient()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isManaSource: true);

            Assert.AreEqual(TileArtKind.None, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_KeySource_ReturnsNone_NotDecayGradient()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isKeySource: true);

            Assert.AreEqual(TileArtKind.None, TileArtKindResolver.Resolve(state));
        }
    }
}
