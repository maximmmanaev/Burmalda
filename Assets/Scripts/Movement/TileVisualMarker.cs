using Burmalda.Core;
using UnityEngine;

namespace Burmalda.Movement
{
    /// <summary>
    /// Отметка на GameObject'е плитки — какую <see cref="GridCoordinate"/>
    /// она физически представляет в мире (issue 2026-08-14: тап по пустому
    /// месту экрана двигал камеру/трейл, потому что
    /// <see cref="GridTraceInputController"/> раньше кастовал луч на
    /// абстрактную математическую плоскость пола, а не на реальные
    /// объекты плиток — ЛЮБАЯ точка тапа, включая пустой пол за пределами
    /// раскрытой сетки, давала какую-то мировую точку и координату).
    ///
    /// Теперь <see cref="GridTraceInputController"/> кастует луч через
    /// <c>Physics.Raycast</c> и читает координату ПРЯМО с попавшего
    /// коллайдера через этот компонент — не пересчитывает её обратно из
    /// точки удара (обратный пересчёт ловил бы пустой пол вне сетки так же
    /// "успешно", как и реальную плитку, в этом и была причина бага).
    ///
    /// Любая система, рендерящая тайлы физическими объектами (сейчас —
    /// только <c>Burmalda.DebugVisuals.TunnelDebugVisual</c>, debug-
    /// плейсхолдер; в будущем — финальный арт), должна вешать этот
    /// компонент на GameObject с Collider'ом — иначе игрок не сможет
    /// физически попасть тапом по этой плитке (Raycast её просто не найдёт).
    /// </summary>
    public sealed class TileVisualMarker : MonoBehaviour
    {
        /// <summary>Координата плитки, которую физически представляет этот GameObject.</summary>
        public GridCoordinate Coordinate { get; private set; }

        /// <summary>Задаёт координату сразу после <c>AddComponent</c> — MonoBehaviour не может принять её через конструктор.</summary>
        public void Initialize(GridCoordinate coordinate) => Coordinate = coordinate;
    }
}
