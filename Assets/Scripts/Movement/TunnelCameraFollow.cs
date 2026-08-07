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
    /// legacy/burmolda_demo.html (cameraRow/cameraTargetRow, draw()) — константа
    /// сглаживания и офсет отставания по ряду перенесены буквально. Высота
    /// камеры (<paramref name="heightOffset"/> конструктора) — новое для 3D,
    /// в 2D-прототипе аналога нет (см. увеличение скоупа в issue #8).
    /// </summary>
    public sealed class TunnelCameraFollow : IDisposable
    {
        // legacy/burmolda_demo.html, draw(): cameraRow += (cameraTargetRow-cameraRow)*0.045
        private const float SmoothingFactor = 0.045f;
        // legacy/burmolda_demo.html, tryAct()/returnToAltar(): cameraTargetRow = Math.max(0, r-5)
        private const int TrailingRowsBehindPlayer = 5;

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
            TargetRotation = ComputeTargetRotation(TargetPosition, _trail.CurrentPosition);
            CurrentRotation = TargetRotation;

            _trail.Advanced += OnTrailAdvanced;
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
            _trail.Advanced -= OnTrailAdvanced;
            _disposed = true;
        }

        private void OnTrailAdvanced(GridCoordinate coordinate)
        {
            TargetPosition = ComputeTargetPosition(coordinate);
            TargetRotation = ComputeTargetRotation(TargetPosition, coordinate);
        }

        private Vector3 ComputeTargetPosition(GridCoordinate playerPosition)
        {
            // #62: камера двигается только вперёд-назад (по Z) — столбец
            // зафиксирован на центре ширины тоннеля, не следует за игроком
            // по X (иначе камера уезжает влево-вправо при диагональном пути).
            // Поворот камеры при этом всё ещё смотрит на игрока — см.
            // ComputeTargetRotation, здесь меняется только позиция.
            var trailingRow = Math.Max(0, playerPosition.Row - TrailingRowsBehindPlayer);
            var followCoordinate = new GridCoordinate(trailingRow, _projection.Width / 2);
            return _projection.ToWorldPosition(followCoordinate) + _heightOffset;
        }

        private Quaternion ComputeTargetRotation(Vector3 cameraPosition, GridCoordinate playerPosition)
        {
            var lookAtPoint = _projection.ToWorldPosition(playerPosition);
            var direction = lookAtPoint - cameraPosition;
            return direction.sqrMagnitude > 0f ? Quaternion.LookRotation(direction, Vector3.up) : Quaternion.identity;
        }
    }
}
