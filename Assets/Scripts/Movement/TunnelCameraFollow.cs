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
    /// <see cref="SmoothingFactor"/>). Высота
    /// камеры (<paramref name="heightOffset"/> конструктора) — новое для 3D,
    /// в 2D-прототипе аналога нет (см. увеличение скоупа в issue #8).
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
        // По запросу владельца продукта: белая плитка (игрок) должна быть
        // ближе к низу экрана, а не строго в центре — камера целится не в
        // самого игрока, а в точку впереди него по глубине тоннеля. Так же
        // «на глазок», без issue, значение — предмет плейтеста (Спринт 10).
        private const int LookAheadRowsBeyondPlayer = 6;

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
            TargetRotation = ComputeTargetRotation(TargetPosition, _trail.CurrentPosition.Row);
            CurrentRotation = TargetRotation;

            // PositionChanged (а не Advanced) — камера должна следовать за
            // игроком и назад, при возврате на уже пройденную плиту (#61),
            // не только вперёд на новых плитках (иначе некуда отступить).
            _trail.PositionChanged += OnPositionChanged;
        }

        /// <summary>Точка, к которой плавно движется камера — со смещением позади игрока (PRD 16).</summary>
        public Vector3 TargetPosition { get; private set; }

        /// <summary>Текущая сглаженная позиция камеры — присваивать Transform.position.</summary>
        public Vector3 CurrentPosition { get; private set; }

        /// <summary>Целевой поворот камеры — смотрит на игрока из <see cref="TargetPosition"/>.</summary>
        public Quaternion TargetRotation { get; private set; }

        /// <summary>Текущий сглаженный поворот камеры — присваивать Transform.rotation.</summary>
        public Quaternion CurrentRotation { get; private set; }

        /// <summary>
        /// Продвигает сглаживание позиции и поворота на один тик — константа
        /// сглаживания применяется за вызов, а не масштабируется на deltaTime
        /// (как и в прототипе, где draw() вызывается раз за кадр).
        /// </summary>
        public void Tick()
        {
            CurrentPosition += (TargetPosition - CurrentPosition) * SmoothingFactor;
            CurrentRotation = Quaternion.Slerp(CurrentRotation, TargetRotation, SmoothingFactor);
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
            TargetRotation = ComputeTargetRotation(TargetPosition, coordinate.Row);
        }

        private Vector3 ComputeTargetPosition(GridCoordinate playerPosition)
        {
            // #62: камера двигается только вперёд-назад (по Z) — столбец
            // зафиксирован на центре ширины тоннеля, не следует за игроком
            // по X (иначе камера уезжает влево-вправо при диагональном пути).
            // Поворот камеры также больше не заезжает за игроком по X — см.
            // ComputeTargetRotation, здесь меняется только позиция.
            var trailingRow = Math.Max(0, playerPosition.Row - TrailingRowsBehindPlayer);
            var followCoordinate = new GridCoordinate(trailingRow, _projection.Width / 2);
            return _projection.ToWorldPosition(followCoordinate) + _heightOffset;
        }

        private Quaternion ComputeTargetRotation(Vector3 cameraPosition, int playerRow)
        {
            // По просьбе владельца продукта камера больше не поворачивается
            // влево-вправо вслед за реальным столбцом игрока (аналогично #62
            // для позиции) — точка взгляда берётся по центру тоннеля, меняется
            // только по Z (глубина), боковой (X) составляющей у направления нет.
            var cameraRow = Math.Max(0, playerRow - TrailingRowsBehindPlayer);

            // Баг-репорт владельца продукта: в начале забега камера ещё не
            // отстала на штатные TrailingRowsBehindPlayer рядов (почти
            // совпадает с игроком по глубине) — если в этот момент целиться
            // на полный LookAheadRowsBeyondPlayer вперёд, угол получается
            // настолько крутым, что игрок выпадает из кадра. Поэтому запас
            // взгляда вперёд масштабируется по тому, насколько камера уже
            // "отстала": 0 в самый первый момент (камера целится прямо в
            // игрока — гарантированно виден), полный запас — как только
            // отставание достигает штатных TrailingRowsBehindPlayer рядов.
            var caughtUpDistance = playerRow - cameraRow;
            var lookAheadScale = Mathf.Clamp01((float)caughtUpDistance / TrailingRowsBehindPlayer);
            var lookAheadRow = playerRow + LookAheadRowsBeyondPlayer * lookAheadScale;

            var centerColumnX = _projection.ToWorldPosition(new GridCoordinate(0, _projection.Width / 2)).x;
            var lookAtPoint = new Vector3(centerColumnX, 0f, (lookAheadRow + 0.5f) * _projection.TileSize);
            var direction = lookAtPoint - cameraPosition;
            return direction.sqrMagnitude > 0f ? Quaternion.LookRotation(direction, Vector3.up) : Quaternion.identity;
        }
    }
}
