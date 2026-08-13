using Burmalda.Core;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.DebugVisuals.Tests
{
    public class TileDebugColorTests
    {
        [Test]
        public void Resolve_Destroyed_ReturnsDestroyedColor_RegardlessOfOtherFlags()
        {
            var state = new TileVisualState(isStart: true, isCurrentPosition: true, isDestroyed: true, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.DestroyedColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_CurrentPosition_NotDestroyed_ReturnsCurrentPositionColor_EvenIfAlsoStart()
        {
            var state = new TileVisualState(isStart: true, isCurrentPosition: true, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.CurrentPositionColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Start_NotCurrentNotDestroyed_ReturnsStartColor()
        {
            var state = new TileVisualState(isStart: true, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.StartColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Blocked_NotStartNotCurrentNotDestroyed_ReturnsBlockedColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: true, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.BlockedColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Blocked_IgnoresDecayProgress()
        {
            // Препятствие (#9) не подвержено распаду — градиент по decayProgress01
            // не должен влиять на его цвет, даже если значение передано ненулевым.
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: true, lethalTrap: null, decayProgress01: 0.9f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.BlockedColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Pit_NotStartNotCurrentNotDestroyedNotBlocked_ReturnsPitColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Pit, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.PitColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Lava_NotStartNotCurrentNotDestroyedNotBlocked_ReturnsLavaColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Lava, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.LavaColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_LethalTrap_IgnoresDecayProgress()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Pit, decayProgress01: 0.9f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.PitColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Explosion_NotStartNotCurrentNotDestroyedNotBlocked_ReturnsExplosionColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Explosion, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.ExplosionColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_ExplosiveTrapTrigger_NotArmedYet_ReturnsExplosiveTriggerColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: true, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.ExplosiveTriggerColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_ExplosiveTrapTrigger_IgnoresDecayProgress()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0.9f, isExplosiveTrapTrigger: true, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.ExplosiveTriggerColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_TimedTrapActive_Arrow_ReturnsTimedTrapActiveColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: TimedTrapType.Arrow);

            Assert.AreEqual(TileDebugColor.TimedTrapActiveColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_TimedTrapActive_Blade_ReturnsSameTimedTrapActiveColor()
        {
            // Единый цвет для обоих видов (см. комментарий в TileDebugColor) — не различаем стрелу/лезвие визуально в debug-режиме.
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: TimedTrapType.Blade);

            Assert.AreEqual(TileDebugColor.TimedTrapActiveColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_TimedTrapActive_IgnoresDecayProgress()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0.9f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: TimedTrapType.Arrow);

            Assert.AreEqual(TileDebugColor.TimedTrapActiveColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_TimedTrapTrigger_NotActiveYet_ReturnsTimedTrapTriggerColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: true, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.TimedTrapTriggerColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_TimedTrapTrigger_IgnoresDecayProgress()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0.9f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: true, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.TimedTrapTriggerColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_TimedTrapActive_TakesPriorityOverTimedTrapTrigger()
        {
            // Не должно случиться одновременно по построению генератора, но проверяем приоритет ветвления явно.
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: true, activeTimedTrap: TimedTrapType.Arrow);

            Assert.AreEqual(TileDebugColor.TimedTrapActiveColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_RegularTile_ZeroDecayProgress_ReturnsFreshColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.FreshColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_RegularTile_HalfDecayProgress_ReturnsHalfDecayedColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0.5f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.HalfDecayedColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_RegularTile_FullDecayProgress_ReturnsAboutToDecayColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 1f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.AboutToDecayColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_RegularTile_QuarterProgress_IsBetweenFreshAndHalfDecayed()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0.25f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            var color = TileDebugColor.Resolve(state);

            Assert.Greater(color.r, TileDebugColor.FreshColor.r);
            Assert.Less(color.r, TileDebugColor.HalfDecayedColor.r);
        }

        [Test]
        public void Resolve_RegularTile_ThreeQuarterProgress_IsBetweenHalfDecayedAndAboutToDecay()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0.75f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            var color = TileDebugColor.Resolve(state);

            Assert.Greater(color.g, TileDebugColor.AboutToDecayColor.g);
            Assert.Less(color.g, TileDebugColor.HalfDecayedColor.g);
        }

        [Test]
        public void Resolve_ProgressAboveOne_ClampsToAboutToDecayColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 5f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.AboutToDecayColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_ProgressBelowZero_ClampsToFreshColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: -1f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.FreshColor, TileDebugColor.Resolve(state));
        }
    }
}
