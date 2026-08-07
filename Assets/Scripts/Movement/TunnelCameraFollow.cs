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
    /// Хотфикс (Rotation X 18.4°/29° -> 35°): поворот камеры больше не
    /// вычисляется динамически «взглядом вперёд» — прежний
    /// LookAheadRowsBeyondPlayer и вся логика подбора точки взгляда убраны.
    /// Вместо этого Rotation X зафиксирован (см. <see cref="PitchDegrees"/>):
    /// при вертикальном FOV 60° горизонт исчезает из кадра при pitch >= 30°
    /// (vFOV/2) — 35° берётся с запасом на покачивание камеры. Устойчивость
    /// ширины обзора тоннеля на разных аспектах экрана теперь обеспечивает
    /// не поворот, а динамический FOV — см. <see cref="TunnelCameraFraming"/>
    /// и <see cref="TunnelCameraController"/>.
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
        // legacy/burmolda_demo.html, tryAct()/returnToAltar(): cameraTargetRow = Math.max(0, r-5)
        private const int TrailingRowsBehindPlayer = 5;
        // Хотфикс: Rotation X зафиксирован на 35° — обоснование в doc-комментарии класса.
        private const float PitchDegrees = 35f;

        private static readonly Quaternion FixedRotation = Quaternion.Euler(PitchDegrees, 0f, 0f);

        private readonly GridTraceTrail _trail;
        private readonly WorldGridProjection _projection;
        private readonly Vector3 _heightOffset;
        private bool _disposed;

        public TunnelCameraFollow(GridTraceTrail trail, WorldGridProjection projection, Vector3 heightOffset)
        {
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _projection = projection;
            _heightOffset = heightOffset;

            TargetPosition = ComputeTargetPosition(_trail.CurrentPosition);
            CurrentPosition = TargetPosition;

            // PositionChanged (а не Advanced) — камера должна следовать за
            // игроком и назад, при возврате на уже пройденную плиту (#61),
            // не только вперёд на новых плитках (иначе некуда отступить).
            _trail.PositionChanged += OnPositionChanged;
        }

        /// <summary>Точка, к которой плавно движется камера — со смещением позади игрока (PRD 16).</summary>
        public Vector3 TargetPosition { get; private set; }

        /// <summary>Текущая сглаженная позиция камеры — присваивать Transform.position.</summary>
        public Vector3 CurrentPosition { get; private set; }

        /// <summary>Поворот камеры — фиксированный Rotation X = <see cref="PitchDegrees"/>, не зависит от позиции трейла.</summary>
        public Quaternion TargetRotation => FixedRotation;

        /// <summary>Совпадает с <see cref="TargetRotation"/> — поворот фиксирован, сглаживать нечего.</summary>
        public Quaternion CurrentRotation => FixedRotation;

        /// <summary>
        /// Продвигает сглаживание позиции на один тик — константа сглаживания
        /// применяется за вызов, а не масштабируется на deltaTime (как и в
        /// прототипе, где draw() вызывается раз за кадр). Поворот больше не
        /// сглаживается — он фиксирован (<see cref="PitchDegrees"/>).
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
    }
}
