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

        // Задача «двойные флаги на плитах»: приоритет намеренно ПЕРЕВЁРНУТ
        // относительно старого поведения (раньше Boss побеждал Blocked) —
        // это не должно случаться по построению (Core.TunnelObstacleGenerator
        // не размечает точку Босса), но если конфликт всё же произойдёт,
        // игрок обязан увидеть то, что реально его останавливает
        // (непроходимость), а не безобидный на вид вход в Комнату Босса.
        [Test]
        public void Resolve_Boss_AlsoBlocked_BlockedTakesPriority()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: true, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isBoss: true);

            Assert.AreEqual(TileDebugColor.BlockedColor, TileDebugColor.Resolve(state));
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

        // Issue #163: яма больше не видна заранее — общая сигнатура вместо
        // PitColor. Задача «раскрытие опасности при примеривании»: сигнатура
        // теперь и сама видна только ПОСЛЕ раскрытия (isDangerSignatureRevealed) —
        // см. Resolve_Pit_NotRevealed_LooksLikeOrdinaryTile ниже для состояния до раскрытия.
        [Test]
        public void Resolve_Pit_Revealed_ReturnsHiddenTrapSignatureColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Pit, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isDangerSignatureRevealed: true);

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
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Pit, decayProgress01: 0.9f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isDangerSignatureRevealed: true);

            Assert.AreEqual(TileDebugColor.HiddenTrapSignatureColor, TileDebugColor.Resolve(state));
        }

        // Задача «разрушение плиты», продолжение (владелец, 2026-09-01):
        // сработавший взрыв — угроза прямо сейчас, БОЛЬШЕ НЕ скрыт за общей
        // сигнатурой (см. Core.TrapSignature) — реюзит TimedTrapActiveColor,
        // видим ВСЕГДА, даже без раскрытия.
        [Test]
        public void Resolve_Explosion_AlwaysReturnsTimedTrapActiveColor_RegardlessOfRevealed()
        {
            var revealed = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Explosion, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isDangerSignatureRevealed: true);
            var notRevealed = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Explosion, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isDangerSignatureRevealed: false);

            Assert.AreEqual(TileDebugColor.TimedTrapActiveColor, TileDebugColor.Resolve(revealed));
            Assert.AreEqual(TileDebugColor.TimedTrapActiveColor, TileDebugColor.Resolve(notRevealed));
        }

        // Pit и Explosion теперь РАЗНЫЕ категории — Pit прячется, Explosion нет.
        [Test]
        public void Resolve_PitRevealed_AndExplosion_ProduceDifferentColors()
        {
            var pitState = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Pit, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isDangerSignatureRevealed: true);
            var explosionState = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Explosion, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isDangerSignatureRevealed: false);

            Assert.AreNotEqual(TileDebugColor.Resolve(pitState), TileDebugColor.Resolve(explosionState));
        }

        // Issue #163: сигнатура тоже не путается с лавой — иначе лава выглядела бы "скрытой".
        [Test]
        public void Resolve_HiddenTrapSignatureColor_DiffersFromLavaColor()
        {
            Assert.AreNotEqual(TileDebugColor.LavaColor, TileDebugColor.HiddenTrapSignatureColor);
        }

        [Test]
        public void Resolve_ExplosiveTrapTrigger_Revealed_ReturnsTriggerSignatureColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: true, isTimedTrapTrigger: false, activeTimedTrap: null, isDangerSignatureRevealed: true);

            Assert.AreEqual(TileDebugColor.TriggerSignatureColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_ExplosiveTrapTrigger_IgnoresDecayProgress()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0.9f, isExplosiveTrapTrigger: true, isTimedTrapTrigger: false, activeTimedTrap: null, isDangerSignatureRevealed: true);

            Assert.AreEqual(TileDebugColor.TriggerSignatureColor, TileDebugColor.Resolve(state));
        }

        // Задача «сделать тоннель играбельным», часть 3: единая сигнатура —
        // игрок не должен читать тип ловушки по цвету триггера, как раньше.
        [Test]
        public void Resolve_ExplosiveTrigger_AndTimedTrapTrigger_ProduceIdenticalColor_SignatureDoesNotRevealExactType()
        {
            var explosiveState = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: true, isTimedTrapTrigger: false, activeTimedTrap: null, isDangerSignatureRevealed: true);
            var timedState = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: true, activeTimedTrap: null, isDangerSignatureRevealed: true);

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
        public void Resolve_TimedTrapTrigger_Revealed_ReturnsTriggerSignatureColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: true, activeTimedTrap: null, isDangerSignatureRevealed: true);

            Assert.AreEqual(TileDebugColor.TriggerSignatureColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_TimedTrapTrigger_IgnoresDecayProgress()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0.9f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: true, activeTimedTrap: null, isDangerSignatureRevealed: true);

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

        // Владелец, 2026-09-04 («видимые рычаги, инвариант лавы, размер
        // награды за Воротами») ОТМЕНЯЕТ issue #193: «рычаг — механизм, а
        // не опасность» — видим ВСЕГДА, не гейтится примериванием, не
        // делит сигнатуру с ловушками.
        [Test]
        public void Resolve_Lever_NotRevealed_ReturnsLeverColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isLever: true, isDangerSignatureRevealed: false);

            Assert.AreEqual(TileDebugColor.LeverColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Lever_Revealed_StillReturnsLeverColor()
        {
            // isDangerSignatureRevealed не влияет на рычаг вообще —
            // видимость безусловна в обоих состояниях этого флага.
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isLever: true, isDangerSignatureRevealed: true);

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
        public void Resolve_LeverColor_DiffersFromSignatures()
        {
            // LeverColor снова активно выбирается Resolve() (владелец,
            // 2026-09-04) — дешёвая страховка, что рычаг не читается как
            // одна из сигнатур опасности/триггера.
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

        // Задача «двойные флаги на плитах»: три конкретных симптома
        // плейтеста владельца — не просто общая проверка порядка веток
        // (см. Resolve_Boss_AlsoBlocked_BlockedTakesPriority выше), а именно
        // те формулировки, с которых начиналась задача.
        [Test]
        public void Resolve_LethalTrap_AlsoBlocked_BlockedTakesPriority()
        {
            // "сигнатура опасности отрисовывается вместо стены" — если бы
            // яма/лава победила, игрок видел бы сигнатуру там, где на самом
            // деле стена (Blocked), не пропускающая ход вообще.
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: true, lethalTrap: LethalTrapType.Pit, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.BlockedColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Lever_AlsoBlocked_BlockedTakesPriority()
        {
            // "рычаг иногда непроходим" — плита реально Blocked (GridTraceTrail
            // её не пропускает), но выглядела бы как безобидный рычаг.
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: true, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isLever: true);

            Assert.AreEqual(TileDebugColor.BlockedColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_ManaSource_AlsoLethalTrap_HiddenTrapSignatureTakesPriority()
        {
            // "сигнатура вместо источника Маны" — до этой задачи ManaSource
            // проверялся раньше сигнатуры, теперь наоборот: опасность важнее
            // для выживания, чем то, что под ней ценного (см. комментарий Resolve).
            // isDangerSignatureRevealed: true — иначе сигнатура сама не
            // видна (задача «раскрытие опасности при примеривании»), и
            // проверка приоритета Blocked/сигнатура-vs-роль потеряла бы смысл.
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Pit, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isManaSource: true, isDangerSignatureRevealed: true);

            Assert.AreEqual(TileDebugColor.HiddenTrapSignatureColor, TileDebugColor.Resolve(state));
        }

        // Задача «раскрытие опасности при примеривании» (PRD v9 §4.2
        // заменяется — было "сигнатура видна всегда"): пока плита не
        // раскрыта, она обязана выглядеть НЕОТЛИЧИМО от обычного пола —
        // тем же градиентом распада, что и любая другая плита, не особым
        // цветом/тоном.
        [Test]
        public void Resolve_Pit_NotRevealed_LooksLikeOrdinaryFreshTile()
        {
            var hidden = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Pit, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isDangerSignatureRevealed: false);
            var ordinary = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.Resolve(ordinary), TileDebugColor.Resolve(hidden));
        }

        // Задача «разрушение плиты», продолжение (владелец, 2026-09-01):
        // ИНВЕРСИЯ прежнего теста с тем же именем — сработавший взрыв
        // сознательно НЕ выглядит как обычный пол даже без раскрытия,
        // "сработавшая ловушка — угроза прямо сейчас, скрывать нельзя".
        [Test]
        public void Resolve_Explosion_NotRevealed_DoesNotLookLikeOrdinaryFreshTile()
        {
            var active = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Explosion, decayProgress01: 0.4f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isDangerSignatureRevealed: false);
            var ordinary = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0.4f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreNotEqual(TileDebugColor.Resolve(ordinary), TileDebugColor.Resolve(active));
        }

        [Test]
        public void Resolve_ExplosiveTrigger_NotRevealed_LooksLikeOrdinaryFreshTile()
        {
            var hidden = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: true, isTimedTrapTrigger: false, activeTimedTrap: null, isDangerSignatureRevealed: false);
            var ordinary = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.Resolve(ordinary), TileDebugColor.Resolve(hidden));
        }

        [Test]
        public void Resolve_TimedTrapTrigger_NotRevealed_LooksLikeOrdinaryFreshTile()
        {
            var hidden = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: true, activeTimedTrap: null, isDangerSignatureRevealed: false);
            var ordinary = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileDebugColor.Resolve(ordinary), TileDebugColor.Resolve(hidden));
        }

        // Лава и активная ловушка с таймингом — вне этого правила, остаются
        // видимыми независимо от IsDangerSignatureRevealed (см. TrapSignature).
        [Test]
        public void Resolve_Lava_NotRevealed_StillReturnsLavaColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Lava, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isDangerSignatureRevealed: false);

            Assert.AreEqual(TileDebugColor.LavaColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_TimedTrapActive_NotRevealed_StillReturnsTimedTrapActiveColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: TimedTrapType.Arrow, isDangerSignatureRevealed: false);

            Assert.AreEqual(TileDebugColor.TimedTrapActiveColor, TileDebugColor.Resolve(state));
        }
    }
}
