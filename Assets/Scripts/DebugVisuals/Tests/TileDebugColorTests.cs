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
            var state = new TileVisualState(isStart: true, isCurrentPosition: true, isDestroyed: true, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isTrapTrigger: false);

            Assert.AreEqual(TileDebugColor.DestroyedColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_CurrentPosition_NotDestroyed_ReturnsCurrentPositionColor_EvenIfAlsoStart()
        {
            var state = new TileVisualState(isStart: true, isCurrentPosition: true, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isTrapTrigger: false);

            Assert.AreEqual(TileDebugColor.CurrentPositionColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Start_NotCurrentNotDestroyed_ReturnsStartColor()
        {
            var state = new TileVisualState(isStart: true, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isTrapTrigger: false);

            Assert.AreEqual(TileDebugColor.StartColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Blocked_NotStartNotCurrentNotDestroyed_ReturnsBlockedColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: true, lethalTrap: null, decayProgress01: 0f, isTrapTrigger: false);

            Assert.AreEqual(TileDebugColor.BlockedColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Boss_NotStartNotCurrentNotDestroyed_ReturnsBossColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isTrapTrigger: false, isBoss: true);

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
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: true, lethalTrap: null, decayProgress01: 0f, isTrapTrigger: false, isBoss: true);

            Assert.AreEqual(TileDebugColor.BlockedColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Boss_ButDestroyed_DestroyedTakesPriority()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: true, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isTrapTrigger: false, isBoss: true);

            Assert.AreEqual(TileDebugColor.DestroyedColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Blocked_IgnoresDecayProgress()
        {
            // Препятствие (#9) не подвержено распаду — градиент по decayProgress01
            // не должен влиять на его цвет, даже если значение передано ненулевым.
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: true, lethalTrap: null, decayProgress01: 0.9f, isTrapTrigger: false);

            Assert.AreEqual(TileDebugColor.BlockedColor, TileDebugColor.Resolve(state));
        }

        // Issue #163: лава — единственный статичный LethalTrapType, видимый
        // всегда (никогда не был "скрытым"). Владелец, 2026-09-05 («оставить
        // только пять новых ловушек»): Pit — единственный тип, который
        // раньше прятался за общей сигнатурой (Core.TrapSignature) — удалён
        // вместе со всей идеей "скрытый LethalTrapType". Ниже — единственный
        // оставшийся кейс этой категории.
        [Test]
        public void Resolve_Lava_NotStartNotCurrentNotDestroyedNotBlocked_ReturnsLavaColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Lava, decayProgress01: 0f, isTrapTrigger: false);

            Assert.AreEqual(TileDebugColor.LavaColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_LethalTrap_IgnoresDecayProgress()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Lava, decayProgress01: 0.9f, isTrapTrigger: false);

            Assert.AreEqual(TileDebugColor.LavaColor, TileDebugColor.Resolve(state));
        }

        // Баг с устройства (владелец, 2026-09-05, «стрела остаётся
        // смертельной навсегда... выглядит как непроходимая стена»):
        // настоящая причина — у этих четырёх LethalTrapType не было ветки
        // здесь вовсе, армированная плита выглядела обычным полом (падала
        // до градиента распада) — игрок видел необъяснимую преграду, а не
        // "перестал гаситься тайл". Реюзит TimedTrapActiveColor — активная
        // угроза прямо сейчас, видна ВСЕГДА, без гейта примериванием (тот же
        // принцип, что у Лавы выше).
        [TestCase(LethalTrapType.ArrowWave)]
        [TestCase(LethalTrapType.BombBlast)]
        [TestCase(LethalTrapType.BladeTact)]
        [TestCase(LethalTrapType.LavaWave)]
        public void Resolve_NewTurnBasedTrapTypes_ReturnTimedTrapActiveColor(LethalTrapType trapType)
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: trapType, decayProgress01: 0f, isTrapTrigger: false);

            Assert.AreEqual(TileDebugColor.TimedTrapActiveColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_TrapTrigger_Revealed_ReturnsTriggerSignatureColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isTrapTrigger: true, isDangerSignatureRevealed: true);

            Assert.AreEqual(TileDebugColor.TriggerSignatureColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_TrapTrigger_IgnoresDecayProgress()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0.9f, isTrapTrigger: true, isDangerSignatureRevealed: true);

            Assert.AreEqual(TileDebugColor.TriggerSignatureColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_LethalTrap_TakesPriorityOverTrapTrigger()
        {
            // Не должно случиться одновременно по построению генератора, но
            // проверяем приоритет ветвления явно.
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.ArrowWave, decayProgress01: 0f, isTrapTrigger: true);

            Assert.AreEqual(TileDebugColor.TimedTrapActiveColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_RegularTile_ZeroDecayProgress_ReturnsFreshColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isTrapTrigger: false);

            Assert.AreEqual(TileDebugColor.FreshColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_RegularTile_HalfDecayProgress_ReturnsHalfDecayedColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0.5f, isTrapTrigger: false);

            Assert.AreEqual(TileDebugColor.HalfDecayedColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_RegularTile_FullDecayProgress_ReturnsAboutToDecayColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 1f, isTrapTrigger: false);

            Assert.AreEqual(TileDebugColor.AboutToDecayColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_RegularTile_QuarterProgress_IsBetweenFreshAndHalfDecayed()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0.25f, isTrapTrigger: false);

            var color = TileDebugColor.Resolve(state);

            Assert.Greater(color.r, TileDebugColor.FreshColor.r);
            Assert.Less(color.r, TileDebugColor.HalfDecayedColor.r);
        }

        [Test]
        public void Resolve_RegularTile_ThreeQuarterProgress_IsBetweenHalfDecayedAndAboutToDecay()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0.75f, isTrapTrigger: false);

            var color = TileDebugColor.Resolve(state);

            Assert.Greater(color.g, TileDebugColor.AboutToDecayColor.g);
            Assert.Less(color.g, TileDebugColor.HalfDecayedColor.g);
        }

        [Test]
        public void Resolve_ProgressAboveOne_ClampsToAboutToDecayColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 5f, isTrapTrigger: false);

            Assert.AreEqual(TileDebugColor.AboutToDecayColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_ProgressBelowZero_ClampsToFreshColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: -1f, isTrapTrigger: false);

            Assert.AreEqual(TileDebugColor.FreshColor, TileDebugColor.Resolve(state));
        }

        // Задача «сделать тоннель играбельным», часть 1: источники Маны/Ключей
        // видны всегда — маршрутное решение "идти за Маной или за Ключами".
        [Test]
        public void Resolve_ManaSource_NotStartNotCurrentNotDestroyed_ReturnsManaSourceColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isTrapTrigger: false, isManaSource: true);

            Assert.AreEqual(TileDebugColor.ManaSourceColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_KeySource_NotStartNotCurrentNotDestroyed_ReturnsKeySourceColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isTrapTrigger: false, isKeySource: true);

            Assert.AreEqual(TileDebugColor.KeySourceColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_ManaSource_IgnoresDecayProgress()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0.9f, isTrapTrigger: false, isManaSource: true);

            Assert.AreEqual(TileDebugColor.ManaSourceColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_ManaSource_DiffersFromKeySourceColor()
        {
            Assert.AreNotEqual(TileDebugColor.ManaSourceColor, TileDebugColor.KeySourceColor);
        }

        [Test]
        public void Resolve_ManaSource_DiffersFromTriggerSignatureColor()
        {
            Assert.AreNotEqual(TileDebugColor.ManaSourceColor, TileDebugColor.TriggerSignatureColor);
        }

        [Test]
        public void Resolve_KeySource_DiffersFromTriggerSignatureColor()
        {
            Assert.AreNotEqual(TileDebugColor.KeySourceColor, TileDebugColor.TriggerSignatureColor);
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
            var blockedManaSource = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: true, lethalTrap: null, decayProgress01: 0f, isTrapTrigger: false, isManaSource: true);
            var plainManaSource = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0.9f, isTrapTrigger: false, isManaSource: true);

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
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isTrapTrigger: false, isLever: true, isDangerSignatureRevealed: false);

            Assert.AreEqual(TileDebugColor.LeverColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Lever_Revealed_StillReturnsLeverColor()
        {
            // isDangerSignatureRevealed не влияет на рычаг вообще —
            // видимость безусловна в обоих состояниях этого флага.
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isTrapTrigger: false, isLever: true, isDangerSignatureRevealed: true);

            Assert.AreEqual(TileDebugColor.LeverColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_GatedClosed_ReturnsLeverGateClosedColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isTrapTrigger: false, isGated: true, isLeverGateOpen: false);

            Assert.AreEqual(TileDebugColor.LeverGateClosedColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_GatedOpen_ReturnsLeverGateOpenColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isTrapTrigger: false, isGated: true, isLeverGateOpen: true);

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
            // сигнатура триггера.
            Assert.AreNotEqual(TileDebugColor.LeverColor, TileDebugColor.TriggerSignatureColor);
        }

        [Test]
        public void Resolve_Gated_TakesPriorityOverLever_WhenSomehowBothTrue()
        {
            // Не должно случиться по построению генератора (одна плита — один
            // символ шаблона) — проверяем приоритет ветвления явно, как и для
            // других "не должно, но на всякий" кейсов в этом файле.
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isTrapTrigger: false, isLever: true, isGated: true, isLeverGateOpen: false);

            Assert.AreEqual(TileDebugColor.LeverGateClosedColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Boss_TakesPriorityOverGatedAndLever()
        {
            var gatedBoss = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isTrapTrigger: false, isBoss: true, isGated: true);
            var leverBoss = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isTrapTrigger: false, isBoss: true, isLever: true);

            Assert.AreEqual(TileDebugColor.BossColor, TileDebugColor.Resolve(gatedBoss));
            Assert.AreEqual(TileDebugColor.BossColor, TileDebugColor.Resolve(leverBoss));
        }

        // Задача «тёплый набор плит»: Алтарь — та же приоритетная группа, что Boss.
        [Test]
        public void Resolve_Altar_NotStartNotCurrentNotDestroyedNotBoss_ReturnsAltarColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isTrapTrigger: false, isAltar: true);

            Assert.AreEqual(TileDebugColor.AltarColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Boss_TakesPriorityOverAltar()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isTrapTrigger: false, isBoss: true, isAltar: true);

            Assert.AreEqual(TileDebugColor.BossColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Altar_TakesPriorityOverGatedAndLever()
        {
            var gatedAltar = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isTrapTrigger: false, isAltar: true, isGated: true);
            var leverAltar = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isTrapTrigger: false, isAltar: true, isLever: true);

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
            // лава победила, игрок видел бы сигнатуру там, где на самом
            // деле стена (Blocked), не пропускающая ход вообще.
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: true, lethalTrap: LethalTrapType.Lava, decayProgress01: 0f, isTrapTrigger: false);

            Assert.AreEqual(TileDebugColor.BlockedColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_Lever_AlsoBlocked_BlockedTakesPriority()
        {
            // "рычаг иногда непроходим" — плита реально Blocked (GridTraceTrail
            // её не пропускает), но выглядела бы как безобидный рычаг.
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: true, lethalTrap: null, decayProgress01: 0f, isTrapTrigger: false, isLever: true);

            Assert.AreEqual(TileDebugColor.BlockedColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_ManaSource_AlsoLethalTrap_LethalTrapTakesPriority()
        {
            // "сигнатура вместо источника Маны" — ManaSource проверяется
            // ПОСЛЕ LethalTrap (см. комментарий Resolve): опасность важнее
            // для выживания, чем то, что под ней ценного. Владелец,
            // 2026-09-05 («оставить только пять новых ловушек»): раньше этот
            // тест демонстрировался через Pit+HiddenTrapSignatureColor —
            // Pit удалён, Лава (единственный оставшийся статичный
            // LethalTrapType) даёт тот же приоритет напрямую, без
            // промежуточной сигнатуры.
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Lava, decayProgress01: 0f, isTrapTrigger: false, isManaSource: true);

            Assert.AreEqual(TileDebugColor.LavaColor, TileDebugColor.Resolve(state));
        }

        // Задача «раскрытие опасности при примеривании» (PRD v9 §4.2
        // заменяется — было "сигнатура видна всегда"): пока триггер не
        // раскрыт, плита обязана выглядеть НЕОТЛИЧИМО от обычного пола —
        // тем же градиентом распада, что и любая другая плита, не особым
        // цветом/тоном. Владелец, 2026-09-05 («оставить только пять новых
        // ловушек»): единственное состояние этой категории теперь —
        // IsTrapTrigger (Pit как отдельный "скрытый LethalTrapType" удалён).
        [Test]
        public void Resolve_TrapTrigger_NotRevealed_LooksLikeOrdinaryFreshTile()
        {
            var hidden = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isTrapTrigger: true, isDangerSignatureRevealed: false);
            var ordinary = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0f, isTrapTrigger: false);

            Assert.AreEqual(TileDebugColor.Resolve(ordinary), TileDebugColor.Resolve(hidden));
        }

        // Задача «разрушение плиты», продолжение (владелец, 2026-09-01):
        // активная ловушка с таймингом сознательно НЕ выглядит как обычный
        // пол даже без раскрытия — "сработавшая ловушка — угроза прямо
        // сейчас, скрывать нельзя". Лава и активная ловушка с таймингом —
        // вне правила "раскрытие при примеривании", остаются видимыми
        // независимо от IsDangerSignatureRevealed.
        [Test]
        public void Resolve_ActiveTrap_NotRevealed_DoesNotLookLikeOrdinaryFreshTile()
        {
            var active = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.ArrowWave, decayProgress01: 0.4f, isTrapTrigger: false, isDangerSignatureRevealed: false);
            var ordinary = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: null, decayProgress01: 0.4f, isTrapTrigger: false);

            Assert.AreNotEqual(TileDebugColor.Resolve(ordinary), TileDebugColor.Resolve(active));
        }

        [Test]
        public void Resolve_Lava_NotRevealed_StillReturnsLavaColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.Lava, decayProgress01: 0f, isTrapTrigger: false, isDangerSignatureRevealed: false);

            Assert.AreEqual(TileDebugColor.LavaColor, TileDebugColor.Resolve(state));
        }

        [Test]
        public void Resolve_TimedTrapActive_NotRevealed_StillReturnsTimedTrapActiveColor()
        {
            var state = new TileVisualState(isStart: false, isCurrentPosition: false, isDestroyed: false, isBlocked: false, lethalTrap: LethalTrapType.BladeTact, decayProgress01: 0f, isTrapTrigger: false, isDangerSignatureRevealed: false);

            Assert.AreEqual(TileDebugColor.TimedTrapActiveColor, TileDebugColor.Resolve(state));
        }
    }
}
