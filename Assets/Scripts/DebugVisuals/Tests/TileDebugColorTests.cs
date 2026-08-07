using NUnit.Framework;
using UnityEngine;

namespace Burmalda.DebugVisuals.Tests
{
    public class TileDebugColorTests
    {
        [Test]
        public void Resolve_Destroyed_ReturnsDestroyedColor_RegardlessOfOtherFlags()
        {
            var state = new TileVisualState(isStart: true, isCurrentPosition: true, isDestroyed: true, isBlocked: false, decayProgress01: 0f);

            Assert.AreEqual(TileDebugColor.DestroyedColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_CurrentPosition_NotDestroyed_ReturnsCurrentPositionColor_EvenIfAlsoStart()
        {
            var state = new TileVisualState(isStart: true, isCurrentPosition: true, isDestroyed: false, isBlocked: false, decayProgress01: 0f);

            Assert.AreEqual(TileDebugColor.CurrentPositionColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Start_NotCurrentNotDestroyed_ReturnsStartColor()
        {
            var state = new TileVisualState(isStart: true, isCurrentPosition: false, isDestroyed: false, isBlocked: false, decayProgress01: 0f);

            Assert.AreEqual(TileDebugColor.StartColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Blocked_NotStartNotCurrentNotDestroyed_ReturnsBlockedColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: true, decayProgress01: 0f);

            Assert.AreEqual(TileDebugColor.BlockedColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Blocked_IgnoresDecayProgress()
        {
            // Препятствие (#9) не подвержено распаду — градиент по decayProgress01
            // не должен влиять на его цвет, даже если значение передано ненулевым.
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: true, decayProgress01: 0.9f);

            Assert.AreEqual(TileDebugColor.BlockedColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_RegularTile_ZeroDecayProgress_ReturnsFreshColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, decayProgress01: 0f);

            Assert.AreEqual(TileDebugColor.FreshColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_RegularTile_HalfDecayProgress_ReturnsHalfDecayedColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, decayProgress01: 0.5f);

            Assert.AreEqual(TileDebugColor.HalfDecayedColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_RegularTile_FullDecayProgress_ReturnsAboutToDecayColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, decayProgress01: 1f);

            Assert.AreEqual(TileDebugColor.AboutToDecayColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_RegularTile_QuarterProgress_IsBetweenFreshAndHalfDecayed()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, decayProgress01: 0.25f);

            var color = TileDebugColor.Resolve(state);

            Assert.Greater(color.r, TileDebugColor.FreshColor.r);
            Assert.Less(color.r, TileDebugColor.HalfDecayedColor.r);
        }

        [Test]
        public void Resolve_RegularTile_ThreeQuarterProgress_IsBetweenHalfDecayedAndAboutToDecay()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, decayProgress01: 0.75f);

            var color = TileDebugColor.Resolve(state);

            Assert.Greater(color.g, TileDebugColor.AboutToDecayColor.g);
            Assert.Less(color.g, TileDebugColor.HalfDecayedColor.g);
        }

        [Test]
        public void Resolve_ProgressAboveOne_ClampsToAboutToDecayColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, decayProgress01: 5f);

            Assert.AreEqual(TileDebugColor.AboutToDecayColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_ProgressBelowZero_ClampsToFreshColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, decayProgress01: -1f);

            Assert.AreEqual(TileDebugColor.FreshColor, TileDebugColor.Resolve(state));
        }
    }
}
