using NUnit.Framework;
using UnityEngine;

namespace Burmalda.Movement.Tests
{
    /// <summary>
    /// Тесты на <see cref="GridTraceInputController.PointerPositionChangeFilter"/> —
    /// единственная часть <see cref="GridTraceInputController"/> (#60),
    /// тестируемая без сцены/реального Input System. Сам MonoBehaviour
    /// (Awake/Update, ScreenPointToRay) тестами не покрыт — как и раньше.
    /// </summary>
    public class GridTraceInputControllerTests
    {
        [Test]
        public void HasChanged_FirstCall_ReturnsTrue()
        {
            var filter = new GridTraceInputController.PointerPositionChangeFilter();

            Assert.IsTrue(filter.HasChanged(new Vector2(10f, 20f)));
        }

        [Test]
        public void HasChanged_SamePositionAsLastProcessed_ReturnsFalse()
        {
            var filter = new GridTraceInputController.PointerPositionChangeFilter();
            var position = new Vector2(10f, 20f);
            filter.HasChanged(position);

            Assert.IsFalse(filter.HasChanged(position));
        }

        [Test]
        public void HasChanged_StaticHoldAcrossManyFrames_OnlyFirstFrameReturnsTrue()
        {
            // Воспроизводит баг #60: зажатый неподвижный клик опрашивается
            // каждый кадр с одинаковой позицией — должен дать максимум один шаг.
            var filter = new GridTraceInputController.PointerPositionChangeFilter();
            var position = new Vector2(10f, 20f);

            var results = new bool[5];
            for (var i = 0; i < results.Length; i++) results[i] = filter.HasChanged(position);

            Assert.IsTrue(results[0]);
            for (var i = 1; i < results.Length; i++) Assert.IsFalse(results[i], $"кадр {i} не должен повторно сработать на той же позиции");
        }

        [Test]
        public void HasChanged_DifferentPositionThanLastProcessed_ReturnsTrue()
        {
            var filter = new GridTraceInputController.PointerPositionChangeFilter();
            filter.HasChanged(new Vector2(10f, 20f));

            Assert.IsTrue(filter.HasChanged(new Vector2(11f, 20f)));
        }

        [Test]
        public void HasChanged_ChainOfDifferentPositions_EachReturnsTrue()
        {
            // Реальное перетаскивание через несколько ячеек — цепочка шагов должна работать.
            var filter = new GridTraceInputController.PointerPositionChangeFilter();

            Assert.IsTrue(filter.HasChanged(new Vector2(0f, 0f)));
            Assert.IsTrue(filter.HasChanged(new Vector2(10f, 0f)));
            Assert.IsTrue(filter.HasChanged(new Vector2(20f, 0f)));
            Assert.IsTrue(filter.HasChanged(new Vector2(30f, 10f)));
        }

        [Test]
        public void Reset_ThenSamePositionAsBeforeReset_ReturnsTrueAgain()
        {
            // Палец отпустили и снова нажали в той же точке — это новое
            // нажатие, а не «неизменная позиция», должно засчитаться.
            var filter = new GridTraceInputController.PointerPositionChangeFilter();
            var position = new Vector2(10f, 20f);
            filter.HasChanged(position);

            filter.Reset();

            Assert.IsTrue(filter.HasChanged(position));
        }
    }
}
