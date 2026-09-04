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

        // Задача «тёплый набор плит»: раньше None (белый цветной фолбэк,
        // самый невнятный объект на экране по плейтесту владельца), теперь
        // tile-current-position.png.
        [Test]
        public void Resolve_CurrentPosition_NotDestroyed_ReturnsCurrentPosition_EvenIfAlsoStart()
        {
            var state = new TileVisualState(isStart: true, isCurrentPosition: true, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileArtKind.CurrentPosition, TileArtKindResolver.Resolve(state));
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

        // Баг с устройства (владелец, 2026-09-04, «вход в Комнату — обязан
        // читаться»): TileArtKind.Boss переиспользует текстуру Алтаря со
        // своим тоном (TunnelDebugVisual.BossArtTint), не None.
        [Test]
        public void Resolve_Boss_NotStartNotCurrentNotDestroyed_ReturnsBoss()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isBoss: true);

            Assert.AreEqual(TileArtKind.Boss, TileArtKindResolver.Resolve(state));
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
        public void Resolve_Pit_Revealed_ReturnsHiddenTrapSignature()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Pit, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isDangerSignatureRevealed: true);

            Assert.AreEqual(TileArtKind.HiddenTrapSignature, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_Lava_ReturnsLava()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Lava, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileArtKind.Lava, TileArtKindResolver.Resolve(state));
        }

        // Задача «разрушение плиты», продолжение (владелец, 2026-09-01):
        // сработавший взрыв — угроза прямо сейчас, БОЛЬШЕ НЕ прячется за
        // сигнатурой ямы (см. Core.TrapSignature) — реюзит TimedTrapActive,
        // видим ВСЕГДА, даже без раскрытия.
        [Test]
        public void Resolve_Explosion_AlwaysReturnsTimedTrapActive_RegardlessOfRevealed()
        {
            var revealed = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Explosion, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isDangerSignatureRevealed: true);
            var notRevealed = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Explosion, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isDangerSignatureRevealed: false);

            Assert.AreEqual(TileArtKind.TimedTrapActive, TileArtKindResolver.Resolve(revealed));
            Assert.AreEqual(TileArtKind.TimedTrapActive, TileArtKindResolver.Resolve(notRevealed));
        }

        // Pit и Explosion теперь РАЗНЫЕ категории — Pit прячется, Explosion нет.
        [Test]
        public void Resolve_PitRevealed_AndExplosion_ProduceDifferentKinds()
        {
            var pitState = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Pit, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isDangerSignatureRevealed: true);
            var explosionState = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Explosion, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isDangerSignatureRevealed: false);

            Assert.AreNotEqual(TileArtKindResolver.Resolve(pitState), TileArtKindResolver.Resolve(explosionState));
        }

        [Test]
        public void Resolve_ExplosiveTrapTrigger_Revealed_ReturnsTriggerSignature()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: true, isTimedTrapTrigger: false, activeTimedTrap: null, isDangerSignatureRevealed: true);

            Assert.AreEqual(TileArtKind.TriggerSignature, TileArtKindResolver.Resolve(state));
        }

        // Задача «сделать тоннель играбельным», часть 3: единая сигнатура —
        // тот же принцип, что уже есть для Pit/Explosion (issue #163).
        [Test]
        public void Resolve_ExplosiveTrigger_AndTimedTrapTrigger_ProduceIdenticalKind()
        {
            var explosiveState = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: true, isTimedTrapTrigger: false, activeTimedTrap: null, isDangerSignatureRevealed: true);
            var timedState = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: true, activeTimedTrap: null, isDangerSignatureRevealed: true);

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
        public void Resolve_TimedTrapTrigger_Revealed_ReturnsTriggerSignature()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: true, activeTimedTrap: null, isDangerSignatureRevealed: true);

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

        // Задача «разрушение плиты»: TileArtKindResolver теперь всегда
        // отдаёт Fresh независимо от прогресса распада — сам распад
        // передаётся непрерывным оверлеем трещин (TunnelDebugVisual), не
        // сменой TileArtKind (см. doc-комментарий ResolveDecayGradient).
        // Эти три теста раньше проверяли переключение на
        // HalfDecayed/AboutToDecay — переписаны на "остаётся Fresh", не
        // удалены, чтобы регрессия ("кто-то вернул дискретные стадии")
        // тоже падала явно.
        [Test]
        public void Resolve_RegularTile_HalfDecayProgress_StaysFresh_OverlayCarriesProgressNow()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0.5f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileArtKind.Fresh, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_RegularTile_FullDecayProgress_StaysFresh_OverlayCarriesProgressNow()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 1f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileArtKind.Fresh, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_ProgressAboveOne_StaysFresh_OverlayCarriesProgressNow()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 5f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileArtKind.Fresh, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_ProgressBelowZero_ClampsToFresh()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: -1f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileArtKind.Fresh, TileArtKindResolver.Resolve(state));
        }

        // Задача «тёплый набор плит»: раньше None (в пакете были только
        // иконки Icons/Currencies/, не тайлы пола) — теперь
        // tile-mana-source.png/tile-key-source.png.
        [Test]
        public void Resolve_ManaSource_ReturnsManaSource_NotDecayGradient()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isManaSource: true);

            Assert.AreEqual(TileArtKind.ManaSource, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_KeySource_ReturnsKeySource_NotDecayGradient()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isKeySource: true);

            Assert.AreEqual(TileArtKind.KeySource, TileArtKindResolver.Resolve(state));
        }

        // Владелец, 2026-09-04 («видимые рычаги, инвариант лавы, размер
        // награды за Воротами») ОТМЕНЯЕТ issue #193: рычаг — механизм, не
        // опасность, видим ВСЕГДА, собственная текстура, не гейтится
        // примериванием.
        [Test]
        public void Resolve_Lever_NotRevealed_ReturnsLever()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isLever: true, isDangerSignatureRevealed: false);

            Assert.AreEqual(TileArtKind.Lever, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_Lever_Revealed_StillReturnsLever()
        {
            // isDangerSignatureRevealed не влияет на рычаг вообще.
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isLever: true, isDangerSignatureRevealed: true);

            Assert.AreEqual(TileArtKind.Lever, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_GatedClosedOrOpen_ReturnsDistinctKinds_NotDecayGradient()
        {
            var closed = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isGated: true, isLeverGateOpen: false);
            var open = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isGated: true, isLeverGateOpen: true);

            Assert.AreEqual(TileArtKind.GateClosed, TileArtKindResolver.Resolve(closed));
            Assert.AreEqual(TileArtKind.GateOpen, TileArtKindResolver.Resolve(open));
        }

        // Задача «тёплый набор плит»: Алтарь — та же приоритетная группа,
        // что Boss (см. TileDebugColorTests.Resolve_Boss_TakesPriorityOverGatedAndLever).
        [Test]
        public void Resolve_Altar_ReturnsAltar()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isAltar: true);

            Assert.AreEqual(TileArtKind.Altar, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_Boss_TakesPriorityOverAltar()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isBoss: true, isAltar: true);

            Assert.AreEqual(TileArtKind.Boss, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_Altar_TakesPriorityOverGatedAndLever()
        {
            var gatedAltar = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isAltar: true, isGated: true);
            var leverAltar = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isAltar: true, isLever: true);

            Assert.AreEqual(TileArtKind.Altar, TileArtKindResolver.Resolve(gatedAltar));
            Assert.AreEqual(TileArtKind.Altar, TileArtKindResolver.Resolve(leverAltar));
        }

        // Задача «двойные флаги на плитах»: та же тройка симптомов, что в
        // TileDebugColorTests — здесь на уровне TileArtKind, не цвета.
        [Test]
        public void Resolve_Boss_AlsoBlocked_BlockedTakesPriority()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: true, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isBoss: true);

            Assert.AreEqual(TileArtKind.Blocked, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_LethalTrap_AlsoBlocked_BlockedTakesPriority()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: true, lethalTrap: LethalTrapType.Pit, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null);

            Assert.AreEqual(TileArtKind.Blocked, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_Lever_AlsoBlocked_BlockedTakesPriority()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: true, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isLever: true);

            Assert.AreEqual(TileArtKind.Blocked, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_ManaSource_AlsoLethalTrap_HiddenTrapSignatureTakesPriority()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Pit, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isManaSource: true, isDangerSignatureRevealed: true);

            Assert.AreEqual(TileArtKind.HiddenTrapSignature, TileArtKindResolver.Resolve(state));
        }

        // Задача «раскрытие опасности при примеривании»: то же правило,
        // что в TileDebugColorTests — до раскрытия плита не отличима от
        // обычного пола (одного и того же градиента распада).
        [Test]
        public void Resolve_Pit_NotRevealed_LooksLikeOrdinaryFreshTile()
        {
            var hidden = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Pit, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isDangerSignatureRevealed: false);

            Assert.AreEqual(TileArtKind.Fresh, TileArtKindResolver.Resolve(hidden));
        }

        [Test]
        public void Resolve_ExplosiveTrigger_NotRevealed_LooksLikeOrdinaryFreshTile()
        {
            var hidden = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: true, isTimedTrapTrigger: false, activeTimedTrap: null, isDangerSignatureRevealed: false);

            Assert.AreEqual(TileArtKind.Fresh, TileArtKindResolver.Resolve(hidden));
        }

        [Test]
        public void Resolve_Lava_NotRevealed_StillReturnsLava()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Lava, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: null, isDangerSignatureRevealed: false);

            Assert.AreEqual(TileArtKind.Lava, TileArtKindResolver.Resolve(state));
        }

        [Test]
        public void Resolve_TimedTrapActive_NotRevealed_StillReturnsTimedTrapActive()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isExplosiveTrapTrigger: false, isTimedTrapTrigger: false, activeTimedTrap: TimedTrapType.Blade, isDangerSignatureRevealed: false);

            Assert.AreEqual(TileArtKind.TimedTrapActive, TileArtKindResolver.Resolve(state));
        }
    }
}
