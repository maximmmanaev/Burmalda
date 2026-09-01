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
        public void Resolve_Boss_NotStartNotCurrentNotDestroyed_ReturnsBossColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isBoss: true);

            Assert.AreEqual(TileDebugColor.BossColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Boss_AlsoBlocked_BossTakesPriority()
        {
            // Вход в Комнату Босса важнее найти на устройстве, чем обычное
            // препятствие — см. doc-комментарий TileDebugColor.BossColor.
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: true, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isBoss: true);

            Assert.AreEqual(TileDebugColor.BossColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Boss_ButDestroyed_DestroyedTakesPriority()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: true, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isBoss: true);

            Assert.AreEqual(TileDebugColor.DestroyedColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Blocked_IgnoresDecayProgress()
        {
            // Препятствие (#9) не подвержено распаду — градиент по decayProgress01
            // не должен влиять на его цвет, даже если значение передано ненулевым.
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: true, lethalTrap: null, decayProgress01: 0.9f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.BlockedColor, TileDebugColor.Resolve(state));
        }

        // Issue #163: яма больше не видна заранее — общая сигнатура вместо PitColor.
        [Test]
        public void Resolve_Pit_NotStartNotCurrentNotDestroyedNotBlocked_ReturnsHiddenTrapSignatureColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Pit, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.HiddenTrapSignatureColor, TileDebugColor.Resolve(state));
        }

        // Issue #163: лава — единственный LethalTrapType, оставшийся видимым как раньше.
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

            Assert.AreEqual(TileDebugColor.HiddenTrapSignatureColor, TileDebugColor.Resolve(state));
        }

        // Issue #163: взрыв (после активации триггером) тоже скрыт — общая сигнатура вместо ExplosionColor.
        [Test]
        public void Resolve_Explosion_NotStartNotCurrentNotDestroyedNotBlocked_ReturnsHiddenTrapSignatureColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Explosion, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.HiddenTrapSignatureColor, TileDebugColor.Resolve(state));
        }

        // Issue #163: сигнатура НЕ различает яму и взрыв — иначе она бы выдавала точный тип.
        [Test]
        public void Resolve_PitAndExplosion_ProduceIdenticalColor_SignatureDoesNotRevealExactType()
        {
            var pitState = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Pit, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);
            var explosionState = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Explosion, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.Resolve(pitState), TileDebugColor.Resolve(explosionState));
        }

        // Issue #163: сигнатура тоже не путается с лавой — иначе лава выглядела бы "скрытой".
        [Test]
        public void Resolve_HiddenTrapSignatureColor_DiffersFromLavaColor()
        {
            Assert.AreNotEqual(TileDebugColor.LavaColor, TileDebugColor.HiddenTrapSignatureColor);
        }

        [Test]
        public void Resolve_ExplosiveTrapTrigger_NotArmedYet_ReturnsTriggerSignatureColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: true, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.TriggerSignatureColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_ExplosiveTrapTrigger_IgnoresDecayProgress()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0.9f, isExplosiveTrapTrigger: true, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.TriggerSignatureColor, TileDebugColor.Resolve(state));
        }

        // Задача «сделать тоннель играбельным», часть 3: единая сигнатура —
        // игрок не должен читать тип ловушки по цвету триггера, как раньше.
        [Test]
        public void Resolve_ExplosiveTrigger_AndTimedTrapTrigger_ProduceIdenticalColor_SignatureDoesNotRevealExactType()
        {
            var explosiveState = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: true, isTimedTrapTrigger: false, activeTimedTrap: null);
            var timedState = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: true, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.Resolve(explosiveState), TileDebugColor.Resolve(timedState));
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
        public void Resolve_TimedTrapTrigger_NotActiveYet_ReturnsTriggerSignatureColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: true, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.TriggerSignatureColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_TimedTrapTrigger_IgnoresDecayProgress()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0.9f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: true, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.TriggerSignatureColor, TileDebugColor.Resolve(state));
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

        // Задача «сделать тоннель играбельным», часть 1: источники Маны/Ключей
        // видны всегда — маршрутное решение "идти за Маной или за Ключами".
        [Test]
        public void Resolve_ManaSource_NotStartNotCurrentNotDestroyed_ReturnsManaSourceColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isManaSource: true);

            Assert.AreEqual(TileDebugColor.ManaSourceColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_KeySource_NotStartNotCurrentNotDestroyed_ReturnsKeySourceColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isKeySource: true);

            Assert.AreEqual(TileDebugColor.KeySourceColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_ManaSource_IgnoresDecayProgress()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0.9f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isManaSource: true);

            Assert.AreEqual(TileDebugColor.ManaSourceColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_ManaSource_DiffersFromKeySourceColor()
        {
            Assert.AreNotEqual(TileDebugColor.ManaSourceColor, TileDebugColor.KeySourceColor);
        }

        [Test]
        public void Resolve_ManaSource_DiffersFromHiddenTrapSignatureColor()
        {
            Assert.AreNotEqual(TileDebugColor.ManaSourceColor, TileDebugColor.HiddenTrapSignatureColor);
        }

        [Test]
        public void Resolve_KeySource_DiffersFromHiddenTrapSignatureColor()
        {
            Assert.AreNotEqual(TileDebugColor.KeySourceColor, TileDebugColor.HiddenTrapSignatureColor);
        }

        [Test]
        public void Resolve_ManaSource_DiffersFromDecayGradientColors()
        {
            Assert.AreNotEqual(TileDebugColor.ManaSourceColor, TileDebugColor.FreshColor);
            Assert.AreNotEqual(TileDebugColor.ManaSourceColor, TileDebugColor.HalfDecayedColor);
            Assert.AreNotEqual(TileDebugColor.ManaSourceColor, TileDebugColor.AboutToDecayColor);
        }

        [Test]
        public void Resolve_ManaSource_TakesPriorityOverDecayGradient_ButNotOverBlocked()
        {
            // Ни один генератор сейчас не совмещает источник с препятствием
            // (см. doc-комментарий Resolve) — проверяем приоритет ветвления явно.
            var blockedManaSource = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: true, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isManaSource: true);
            var plainManaSource = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0.9f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isManaSource: true);

            Assert.AreEqual(TileDebugColor.BlockedColor, TileDebugColor.Resolve(blockedManaSource));
            Assert.AreEqual(TileDebugColor.ManaSourceColor, TileDebugColor.Resolve(plainManaSource));
        }

        // Задача «рычаг и ворота невидимы»: плейтест владельца — «рычагов
        // поблизости не обнаружил», «недоступные ключи, окружённые стеной».
        [Test]
        public void Resolve_Lever_ReturnsLeverColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isLever: true);

            Assert.AreEqual(TileDebugColor.LeverColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Lever_IgnoresDecayProgress()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0.9f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isLever: true);

            Assert.AreEqual(TileDebugColor.LeverColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_GatedClosed_ReturnsLeverGateClosedColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isGated: true, isLeverGateOpen: false);

            Assert.AreEqual(TileDebugColor.LeverGateClosedColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_GatedOpen_ReturnsLeverGateOpenColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isGated: true, isLeverGateOpen: true);

            Assert.AreEqual(TileDebugColor.LeverGateOpenColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_GatedOpen_DiffersFromGatedClosed()
        {
            // Момент открытия должен быть заметен (задача) — не тот же цвет.
            Assert.AreNotEqual(TileDebugColor.LeverGateClosedColor, TileDebugColor.LeverGateOpenColor);
        }

        [Test]
        public void Resolve_LeverGateClosed_DiffersFromBlockedColor()
        {
            // Blocked — "никогда", ворота — "пока нет" (задача): должны читаться по-разному.
            Assert.AreNotEqual(TileDebugColor.BlockedColor, TileDebugColor.LeverGateClosedColor);
        }

        [Test]
        public void Resolve_Lever_DiffersFromHiddenTrapSignatureAndTriggerSignature()
        {
            // Рычаг — механизм-приглашение, не должен путаться с сигнатурами опасности (задача).
            Assert.AreNotEqual(TileDebugColor.LeverColor, TileDebugColor.HiddenTrapSignatureColor);
            Assert.AreNotEqual(TileDebugColor.LeverColor, TileDebugColor.TriggerSignatureColor);
        }

        [Test]
        public void Resolve_Gated_TakesPriorityOverLever_WhenSomehowBothTrue()
        {
            // Не должно случиться по построению генератора (одна плита — один
            // символ шаблона) — проверяем приоритет ветвления явно, как и для
            // других "не должно, но на всякий" кейсов в этом файле.
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isLever: true, isGated: true, isLeverGateOpen: false);

            Assert.AreEqual(TileDebugColor.LeverGateClosedColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Boss_TakesPriorityOverGatedAndLever()
        {
            var gatedBoss = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isBoss: true, isGated: true);
            var leverBoss = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isBoss: true, isLever: true);

            Assert.AreEqual(TileDebugColor.BossColor, TileDebugColor.Resolve(gatedBoss));
            Assert.AreEqual(TileDebugColor.BossColor, TileDebugColor.Resolve(leverBoss));
        }

        // Задача «тёплый набор плит»: Алтарь — та же приоритетная группа, что Boss.
        [Test]
        public void Resolve_Altar_NotStartNotCurrentNotDestroyedNotBoss_ReturnsAltarColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isAltar: true);

            Assert.AreEqual(TileDebugColor.AltarColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Boss_TakesPriorityOverAltar()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isBoss: true, isAltar: true);

            Assert.AreEqual(TileDebugColor.BossColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Altar_TakesPriorityOverGatedAndLever()
        {
            var gatedAltar = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isAltar: true, isGated: true);
            var leverAltar = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isAltar: true, isLever: true);

            Assert.AreEqual(TileDebugColor.AltarColor, TileDebugColor.Resolve(gatedAltar));
            Assert.AreEqual(TileDebugColor.AltarColor, TileDebugColor.Resolve(leverAltar));
        }

        [Test]
        public void Resolve_Altar_DiffersFromTriggerSignatureColor()
        {
            // Оба золотисто-жёлтые по тону — проверяем, что не совпадают буквально.
            Assert.AreNotEqual(TileDebugColor.AltarColor, TileDebugColor.TriggerSignatureColor);
        }
    }
}
