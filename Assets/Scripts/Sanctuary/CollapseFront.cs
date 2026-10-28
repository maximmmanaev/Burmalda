using System;

namespace Burmalda.Sanctuary
{
    /// <summary>
    /// Фронт обрушения Святилища (PRD v10 §14.2): пол обрушивается горизонтальными
    /// рядами от входа вперёд и становится Лавой. <see cref="RowPosition"/> — ряд, до
    /// которого (включительно) пол уже обрушен; растёт со временем. Назад пути нет.
    /// </summary>
    public sealed class CollapseFront
    {
        public CollapseFront(int startRow, float rowsPerSecond)
        {
            if (rowsPerSecond <= 0f)
                throw new ArgumentOutOfRangeException(nameof(rowsPerSecond), rowsPerSecond, "Скорость обрушения должна быть положительной.");
            RowPosition = startRow;
            RowsPerSecond = rowsPerSecond;
        }

        public float RowPosition { get; private set; }

        public float RowsPerSecond { get; }

        /// <summary>Продвигает фронт; <paramref name="speedMultiplier"/> — ускорение (взятый Саркофаг).</summary>
        public void Tick(float deltaSeconds, float speedMultiplier = 1f)
        {
            if (deltaSeconds <= 0f) return;
            RowPosition += RowsPerSecond * speedMultiplier * deltaSeconds;
        }

        /// <summary>Пол в <paramref name="row"/> уже обрушен.</summary>
        public bool HasCollapsed(int row) => RowPosition >= row;
    }
}
