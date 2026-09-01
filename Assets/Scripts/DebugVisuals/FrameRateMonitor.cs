namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Сглаженный FPS и время кадра для отладочного оверлея (issue #190,
    /// Спринт 12a — <c>Application.targetFrameRate=60</c> уже настроен
    /// <see cref="FrameRateSetup"/>, но живого измерения фактического
    /// FPS/времени кадра в игре не было, только конфигурация цели).
    ///
    /// Мгновенный <c>1/Time.deltaTime</c> скачет на КАЖДОМ кадре (GC-пауза
    /// на один кадр даёт "40 FPS", соседний кадр — снова "60") — нечитаем на
    /// глаз, задача прямо просит "сглаженный, не мгновенный кадр-к-кадру
    /// шум". Экспоненциальное скользящее среднее (EMA) по ВРЕМЕНИ кадра
    /// (не по самому FPS — усреднять 1/x после инверсии даёт другую кривую,
    /// чем усреднить x до инверсии, а читатель ждёт "средний FPS похож на
    /// то, что видно глазом", т.е. усреднение времени кадра) — тот же
    /// принцип "прошлое значение плюс доля нового", что уже применяется в
    /// проекте (см. <c>Decay.TrailDecaySystem._decayAcceleration</c>, хоть
    /// там и линейный рост, не EMA).
    ///
    /// Чистый C#-класс, тикается снаружи (<see cref="FrameRateOverlay"/>) —
    /// тот же паттерн "логика отдельно от MonoBehaviour-driver'а", что
    /// <c>Decay.TrailDecaySystem</c>/<c>Movement.TrapRevealSystem</c>,
    /// тестируется без сцены и без реального времени.
    /// </summary>
    public sealed class FrameRateMonitor
    {
        /// <summary>
        /// Доля НОВОГО значения на каждый тик EMA, 0..1. Меньше — плавнее
        /// (устойчивее к одиночным просадкам GC), но медленнее показывает
        /// реальный устойчивый скачок. 0.1 — на глаз сходится за ~1 секунду
        /// при 60 FPS (десяток кадров), не значение баланса игры (это
        /// исключительно отладочный инструмент, не предмет
        /// docs/rules/forbidden-actions.md).
        /// </summary>
        private const float SmoothingFactor = 0.1f;

        private bool _hasSample;

        /// <summary>Время последнего тика в секундах, БЕЗ сглаживания — "время последнего кадра в мс" из критерия задачи, читатель сам домножает на 1000.</summary>
        public float LastFrameTimeSeconds { get; private set; }

        /// <summary>Сглаженное (EMA) время кадра в секундах.</summary>
        public float SmoothedFrameTimeSeconds { get; private set; }

        /// <summary>Сглаженный FPS — 1/<see cref="SmoothedFrameTimeSeconds"/>, 0 пока не было ни одного валидного тика.</summary>
        public float SmoothedFps => SmoothedFrameTimeSeconds > 0f ? 1f / SmoothedFrameTimeSeconds : 0f;

        /// <summary>
        /// Продвигает измерение на <paramref name="deltaSeconds"/> секунд
        /// реального (не игрового) времени. Не-op для нуля/отрицательного
        /// значения (первый кадр после паузы/загрузки сцены иногда даёт
        /// deltaTime=0 — не должен закапывать сглаженное значение в
        /// бесконечный FPS).
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;

            LastFrameTimeSeconds = deltaSeconds;

            if (!_hasSample)
            {
                // Первый валидный сэмпл — сразу текущее значение, не
                // усреднённое с несуществующим "прошлым" (иначе первые
                // секунды после старта показывали бы то, чего не было).
                SmoothedFrameTimeSeconds = deltaSeconds;
                _hasSample = true;
                return;
            }

            SmoothedFrameTimeSeconds += (deltaSeconds - SmoothedFrameTimeSeconds) * SmoothingFactor;
        }
    }
}
