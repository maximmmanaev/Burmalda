using UnityEngine;

namespace Burmalda.Movement
{
    /// <summary>
    /// Хотфикс камеры: угол наклона (Rotation X, см. <see cref="TunnelCameraFollow"/>)
    /// без неба в кадре + устойчивая ширина обзора тоннеля в
    /// <see cref="DesiredVisibleTiles"/> плиток на любом аспекте экрана.
    /// Чистая математика, без зависимости от Camera/сцены — тестируется в
    /// EditMode без сцены. Применяется в <see cref="TunnelCameraController"/>.
    /// </summary>
    public static class TunnelCameraFraming
    {
        /// <summary>
        /// Желаемая ширина видимой части тоннеля, в плитках. Осознанно
        /// меньше полной ширины сетки (Width=5) — плитки должны оставаться
        /// достаточно крупными на экране для тач-таргета на мобильном (см.
        /// forbidden-actions.md — баланс/UX-параметры не менять на глаз без
        /// запроса); НЕ увеличивать до 5 просто чтобы сузить допуск
        /// width-framing теста — это отдельная, осознанно принятая цель.
        /// </summary>
        public const float DesiredVisibleTiles = 4f;

        /// <summary>
        /// На сколько рядов позади ТЕКУЩЕЙ позиции игрока лежит опорный ряд,
        /// по которому калибруется ширина обзора — тот же ряд, что проверяет
        /// <c>TunnelCameraViewportFramingTests</c> (реальный
        /// <c>Camera.WorldToViewportPoint</c> на реальных world-позициях
        /// крайних плиток). Число само по себе не физически обосновано (как
        /// и раньше не было) — это просто "представительный" ряд где-то в
        /// кадре, используемый как эталон для калибровки; отдельная
        /// константа, а не магическое число прямо в тесте, чтобы код и тест
        /// гарантированно не разъезжались (см. историю бага несогласованной
        /// калибровки от вырожденного старта забега).
        /// </summary>
        public const int FramingReferenceRowsBehindPlayer = 2;

        /// <summary>
        /// Горизонтальный FOV (градусы), при котором на дистанции ВДОЛЬ ОСИ
        /// ВЗГЛЯДА камеры (не по прямой) до опорного ряда в кадре по ширине
        /// помещается ровно <see cref="DesiredVisibleTiles"/> плиток.
        ///
        /// Хотфикс: раньше здесь бралось евклидово расстояние по прямой
        /// R = sqrt(cameraHeight² + groundDistanceToRow²), как будто камера
        /// смотрит точно на опорную точку (депт по прямой ≈ депт по оси
        /// взгляда). Это грубое приближение почти не давало ошибки на пологом
        /// pitch (35°, опорный ряд почти на оси взгляда), но на более крутом
        /// pitch (50°+) опорный ряд сильно не на оси, и приближение ломает
        /// калибровку ширины (проверено: реальный WorldToViewportPoint давал
        /// края видимого окна DesiredVisibleTiles на ~-0.08/1.08 вместо
        /// точных 0.0/1.0 — см. TunnelCameraViewportFramingTests). Теперь
        /// depth считается честно — проекция вектора камера→опорная точка на
        /// направление взгляда (Quaternion.Euler(pitch,0,0) * Vector3.forward,
        /// раскрытое для 2D-случая без yaw/roll): depth = cameraHeight*sin(pitch)
        /// + groundDistanceToRow*cos(pitch). Обязательное условие того, чтобы
        /// Pitch Degrees был по-настоящему тюнящимся полем (issue: без этого
        /// ширина обзора была корректна только на одном конкретном pitch).
        /// </summary>
        public static float ComputeDesiredHorizontalFovDegrees(float cameraHeight, float groundDistanceToRow, float pitchDegrees, float tileSize)
        {
            var halfWidth = DesiredVisibleTiles * tileSize / 2f;
            var pitchRad = pitchDegrees * Mathf.Deg2Rad;
            var depth = cameraHeight * Mathf.Sin(pitchRad) + groundDistanceToRow * Mathf.Cos(pitchRad);
            return 2f * Mathf.Atan(halfWidth / depth) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Дистанция по Z от камеры до опорного ряда калибровки ширины —
        /// вычисленная от УСТОЯВШЕГОСЯ (steady-state) режима следования
        /// камеры, а не от старта забега.
        ///
        /// Раньше (баг, найденный в ручном тестировании) эта дистанция
        /// считалась как |heightOffset.z| — вырожденный случай: на старте
        /// забега трейлинг-ряд камеры (<c>TunnelCameraFollow.TrailingRowsBehindPlayer</c>
        /// назад от игрока, клампится к 0) и опорный ряд калибровки — один и
        /// тот же нулевой ряд, так что вся "дистанция" схлопывалась в один
        /// компонент offset'а. На устоявшемся режиме (игрок продвинулся
        /// дальше <c>trailingRowsBehindPlayer</c> рядов) трейлинг-ряд камеры
        /// больше не клампится, и опорный ряд калибровки
        /// (<see cref="FramingReferenceRowsBehindPlayer"/> рядов позади
        /// ТЕКУЩЕЙ позиции игрока) оказывается ровно
        /// (<paramref name="trailingRowsBehindPlayer"/> - <see cref="FramingReferenceRowsBehindPlayer"/>)
        /// рядов ВПЕРЕДИ трейлинг-ряда камеры — независимо от того, на каком
        /// именно ряду игрок, поэтому эту дистанцию можно посчитать заранее,
        /// без реального трейла (нужна на старте забега, когда трейл ещё в
        /// вырожденном состоянии).
        /// </summary>
        public static float ComputeSteadyStateGroundDistanceToReferenceRow(
            float heightOffsetZ, float tileSize, int trailingRowsBehindPlayer, int referenceRowsBehindPlayer = FramingReferenceRowsBehindPlayer)
        {
            var referenceRowsAheadOfCameraTrailingRow = trailingRowsBehindPlayer - referenceRowsBehindPlayer;
            return Mathf.Abs(referenceRowsAheadOfCameraTrailingRow * tileSize - heightOffsetZ);
        }

        /// <summary>
        /// Вертикальный FOV (то, что принимает Camera.fieldOfView),
        /// пересчитанный из желаемого горизонтального FOV под текущий
        /// aspect (Screen.width/Screen.height) — держит горизонтальный FOV
        /// (а значит и ширину обзора тоннеля) стабильным при смене
        /// разрешения/ориентации экрана.
        /// </summary>
        public static float ComputeVerticalFovDegrees(float desiredHorizontalFovDegrees, float aspect)
        {
            var vFovRad = 2f * Mathf.Atan(
                Mathf.Tan(desiredHorizontalFovDegrees * 0.5f * Mathf.Deg2Rad) / aspect);
            return vFovRad * Mathf.Rad2Deg;
        }
    }
}
