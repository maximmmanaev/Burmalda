using System;
using Burmalda.Core;
using UnityEngine;

namespace Burmalda.Movement
{
    /// <summary>
    /// Плавное следование 3D-камеры от третьего лица за трейлом (PRD 16):
    /// камера идёт сверху-сзади за <see cref="GridTraceTrail.CurrentPosition"/>
    /// (через <see cref="WorldGridProjection.ToWorldPosition"/>), а не
    /// телепортируется на каждый ход. Портировано из camera-логики
    /// legacy/burmolda_demo.html (cameraRow/cameraTargetRow, draw()) — офсет
    /// отставания по ряду перенесён буквально, константа сглаживания
    /// отличается от прототипа (замедлена по запросу владельца продукта, см.
    /// <see cref="SmoothingFactor"/>).
    ///
    /// Хотфикс (Rotation X 18.4°/29°/35° -> 50°): целевой (игровой) наклон
    /// камеры больше не вычисляется динамическим «взглядом вперёд» — вместо
    /// этого он фиксирован на <see cref="PitchDegrees"/> в устоявшемся
    /// режиме. Устойчивость ширины обзора тоннеля на разных аспектах экрана
    /// обеспечивает не поворот, а динамический FOV — см.
    /// <see cref="TunnelCameraFraming"/> и <see cref="TunnelCameraController"/>.
    ///
    /// Top-down интро на старте забега (возвращено — та же формула, что была
    /// у старой убранной LookAheadRowsBeyondPlayer-логики, только для угла,
    /// не для точки взгляда): в момент старта трейлинг-ряд камеры клампится
    /// к 0 и совпадает с позицией игрока — на фиксированном игровом pitch
    /// стартовая плитка физически оказывалась вне кадра (проверено вручную
    /// через WorldToViewportPoint, см. changelog). Вместо фиксированного
    /// pitch с первого кадра — интерполяция от <see cref="IntroPitchDegrees"/>
    /// (почти top-down, гарантированно видит плитку прямо под камерой) к
    /// <see cref="PitchDegrees"/> по мере того, как камера "нагоняет" штатное
    /// отставание (<see cref="TrailingRowsBehindPlayer"/>) — см.
    /// <see cref="ComputeCurrentPitchDegrees"/>.
    ///
    /// <see cref="HeightOffset"/>, <see cref="PitchDegrees"/> и
    /// <see cref="IntroPitchDegrees"/> — публично изменяемые свойства, не
    /// только конструкторские параметры: раньше все применялись один раз в
    /// конструкторе, и Transform камеры каждый кадр перезаписывался
    /// вычисленным значением — из инспектора/Scene view положение камеры
    /// поменять было нельзя даже в Play Mode, правка терялась на следующем
    /// кадре. Теперь <see cref="TunnelCameraController"/> прокидывает
    /// значения своих полей сюда каждый кадр — правки в инспекторе
    /// применяются вживую, без пересборки Follow/рестарта забега.
    /// </summary>
    public sealed class TunnelCameraFollow : IDisposable
    {
        // Было буквально из legacy/burmolda_demo.html, draw(): 0.045 (cameraRow
        // += (cameraTargetRow-cameraRow)*0.045). Дважды замедлено по прямому
        // запросу владельца продукта (0.045 -> 0.02 -> 0.01) — камера
        // ощущалась слишком резкой/дёрганой, не давала игроку спокойно
        // обдумать маршрут (черновой тюнинг «на глазок», без формального
        // issue — финальное значение задаст плейтест баланса, Спринт 10).
        private const float SmoothingFactor = 0.01f;

        // legacy/burmolda_demo.html, tryAct()/returnToAltar(): cameraTargetRow = Math.max(0, r-5).
        // Публичная — нужна TunnelCameraController (калибровка FOV, см.
        // TunnelCameraFraming.ComputeSteadyStateGroundDistanceToReferenceRow)
        // и тестам вне этого класса, а не только ComputeTargetPosition/
        // ComputeCurrentPitchDegrees здесь.
        public const int TrailingRowsBehindPlayer = 5;

        // Хотфикс: игровой (устоявшийся) Rotation X — обоснование в
        // doc-комментарии класса. Публичная константа, а не только значение
        // по умолчанию параметра конструктора — TunnelCameraController
        // использует её как значение по умолчанию своего инспекторного поля.
        public const float DefaultPitchDegrees = 50f;

        // Top-down интро на старте забега — почти вертикально вниз, но не
        // 90° (сингулярность взгляда строго по вертикали — при 90° forward
        // становится параллелен "up", теряется однозначность поворота).
        public const float DefaultIntroPitchDegrees = 85f;

        private readonly GridTraceTrail _trail;
        private readonly WorldGridProjection _projection;
        private Vector3 _heightOffset;
        private float _pitchDegrees;
        private float _introPitchDegrees;
        private bool _disposed;

        public TunnelCameraFollow(
            GridTraceTrail trail,
            WorldGridProjection projection,
            Vector3 heightOffset,
            float pitchDegrees = DefaultPitchDegrees,
            float introPitchDegrees = DefaultIntroPitchDegrees)
        {
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _projection = projection;
            _heightOffset = heightOffset;
            _pitchDegrees = pitchDegrees;
            _introPitchDegrees = introPitchDegrees;

            TargetPosition = ComputeTargetPosition(_trail.CurrentPosition);
            CurrentPosition = TargetPosition;

            // PositionChanged (а не Advanced) — камера должна следовать за
            // игроком и назад, при возврате на уже пройденную плиту (#61),
            // не только вперёд на новых плитках (иначе некуда отступить).
            _trail.PositionChanged += OnPositionChanged;
        }

        /// <summary>
        /// Смещение камеры над/позади игрока. Изменение немедленно
        /// пересчитывает <see cref="TargetPosition"/> от текущей позиции
        /// трейла — не нужно ждать следующего хода игрока, чтобы увидеть эффект.
        /// </summary>
        public Vector3 HeightOffset
        {
            get => _heightOffset;
            set
            {
                _heightOffset = value;
                TargetPosition = ComputeTargetPosition(_trail.CurrentPosition);
            }
        }

        /// <summary>Точка, к которой плавно движется камера — со смещением позади игрока (PRD 16).</summary>
        public Vector3 TargetPosition { get; private set; }

        /// <summary>Текущая сглаженная позиция камеры — присваивать Transform.position.</summary>
        public Vector3 CurrentPosition { get; private set; }

        /// <summary>
        /// Целевой (устоявшийся, игровой) наклон камеры (Rotation X),
        /// градусы — то значение, к которому <see cref="TargetRotation"/>
        /// приходит по мере top-down интро (см. doc-комментарий класса), а
        /// не то, что показывает камера прямо сейчас (для этого —
        /// <see cref="TargetRotation"/>). Публично изменяем — значение можно
        /// крутить в инспекторе <see cref="TunnelCameraController"/> вживую,
        /// включая Play Mode.
        /// </summary>
        public float PitchDegrees
        {
            get => _pitchDegrees;
            set => _pitchDegrees = value;
        }

        /// <summary>
        /// Стартовый (top-down интро) наклон камеры, градусы — см.
        /// doc-комментарий класса. Публично изменяем по тому же принципу,
        /// что и <see cref="PitchDegrees"/>.
        /// </summary>
        public float IntroPitchDegrees
        {
            get => _introPitchDegrees;
            set => _introPitchDegrees = value;
        }

        /// <summary>
        /// Поворот камеры прямо сейчас — интерполяция между
        /// <see cref="IntroPitchDegrees"/> и <see cref="PitchDegrees"/> по
        /// тому, насколько камера уже "нагнала" штатное отставание
        /// (см. <see cref="ComputeCurrentPitchDegrees"/>). Вычисляется живьём
        /// от текущей позиции трейла при каждом обращении — не кэшируется,
        /// поэтому не нужно отдельно реагировать на изменение
        /// PitchDegrees/IntroPitchDegrees, чтобы результат оставался свежим.
        /// Без поворота влево-вправо/крена (yaw/roll всегда 0).
        /// </summary>
        public Quaternion TargetRotation => Quaternion.Euler(ComputeCurrentPitchDegrees(), 0f, 0f);

        /// <summary>Совпадает с <see cref="TargetRotation"/> — поворот не сглаживается отдельно, интерполяция уже даёт плавность по рядам.</summary>
        public Quaternion CurrentRotation => TargetRotation;

        /// <summary>
        /// Продвигает сглаживание позиции на один тик — константа сглаживания
        /// применяется за вызов, а не масштабируется на deltaTime (как и в
        /// прототипе, где draw() вызывается раз за кадр). Поворот не
        /// участвует — он не по времени, а по рядам (см. <see cref="ComputeCurrentPitchDegrees"/>).
        /// </summary>
        public void Tick()
        {
            CurrentPosition += (TargetPosition - CurrentPosition) * SmoothingFactor;
        }

        /// <summary>Отписывается от трейла. Вызывать при завершении забега/уничтожении системы.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _trail.PositionChanged -= OnPositionChanged;
            _disposed = true;
        }

        private void OnPositionChanged(GridCoordinate coordinate)
        {
            TargetPosition = ComputeTargetPosition(coordinate);
        }

        private Vector3 ComputeTargetPosition(GridCoordinate playerPosition)
        {
            // #62: камера двигается только вперёд-назад (по Z) — столбец
            // зафиксирован на центре ширины тоннеля, не следует за игроком
            // по X (иначе камера уезжает влево-вправо при диагональном пути).
            var trailingRow = Math.Max(0, playerPosition.Row - TrailingRowsBehindPlayer);
            var followCoordinate = new GridCoordinate(trailingRow, _projection.Width / 2);
            return _projection.ToWorldPosition(followCoordinate) + _heightOffset;
        }

        /// <summary>
        /// Тот же паттерн интерполяции, что был у старой (убранной)
        /// LookAheadRowsBeyondPlayer-логики точки взгляда — только теперь для
        /// угла наклона, не для точки: 0 в самый первый момент забега
        /// (трейлинг-ряд камеры клампится к 0, совпадает с игроком) — камера
        /// смотрит почти строго вниз, стартовая плитка гарантированно видна;
        /// 1, как только камера нагоняет штатное отставание
        /// (<see cref="TrailingRowsBehindPlayer"/> рядов) — игровой pitch.
        /// </summary>
        private float ComputeCurrentPitchDegrees()
        {
            var playerRow = _trail.CurrentPosition.Row;
            var cameraTrailingRow = Math.Max(0, playerRow - TrailingRowsBehindPlayer);
            var caughtUpDistance = playerRow - cameraTrailingRow;
            var scale = Mathf.Clamp01((float)caughtUpDistance / TrailingRowsBehindPlayer);
            return Mathf.Lerp(_introPitchDegrees, _pitchDegrees, scale);
        }
    }
}
