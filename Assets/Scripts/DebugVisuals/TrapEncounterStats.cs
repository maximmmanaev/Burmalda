namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Задача «измерить, а не оценить» (владелец, 2026-09-08): "«мало» и
    /// «много» станут числом, которое владелец назовёт после забега, а не
    /// ощущением". Чистое хранилище счётчиков текущего забега — mutable
    /// static, тот же принцип, что <see cref="TrapRevealFeedback"/>/
    /// <c>Core.TunnelObstacleGenerator</c>'s <c>*Share</c>-поля. Пишет
    /// <see cref="TrapEncounterTracker"/>, читает <see cref="TrapDensityDebugPanel"/>.
    ///
    /// Ориентир владельца для старта наблюдений: одна сработавшая ловушка
    /// на 4–6 пройденных рядов — эта пара чисел и должна дать такое
    /// отношение, если плотность (после задачи «неизбежность, а не
    /// количество») действительно достаточна.
    /// </summary>
    public static class TrapEncounterStats
    {
        /// <summary>
        /// Сколько РАЗНЫХ плит-триггеров (issues #213-#217) трейл посетил
        /// за текущий забег — каждая считается не больше одного раза
        /// (см. <see cref="TrapEncounterTracker"/>), тем же принципом
        /// одноразового срабатывания, что уже у самих систем ловушек
        /// ("Movement.ArrowWaveTrapSystem" и т.п.: "повторный проход не
        /// запускает вторую параллельную активацию").
        /// </summary>
        public static int TrapsTriggered { get; private set; }

        /// <summary>Ряд, до которого дошёл трейл текущего забега (<c>GridTraceTrail.CurrentPosition.Row</c> на момент последнего шага) — знаменатель отношения "ловушка на N рядов".</summary>
        public static int RowsTraversed { get; private set; }

        public static void RecordTrapTrigger() => TrapsTriggered++;

        public static void RecordRow(int row)
        {
            if (row > RowsTraversed) RowsTraversed = row;
        }

        /// <summary>Сбрасывается на каждый новый забег (см. <see cref="TrapEncounterTracker"/>, подписка на <c>GridTraceInputController.RunStarted</c>).</summary>
        public static void Reset()
        {
            TrapsTriggered = 0;
            RowsTraversed = 0;
        }
    }
}
