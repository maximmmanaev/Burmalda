using System;
using System.Collections.Generic;
using Burmalda.Core;
using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Задача «симуляция стрелы» (владелец): «также как было, но нужен 3D
    /// объект, который пролетает иногда слева иногда справа — должна быть
    /// симуляция стрелы, плиты становятся смертельными по очереди волной».
    /// Сама механика волны (<see cref="ArrowWaveTrapSystem"/>) НЕ меняется —
    /// владелец явно подтвердил «так же, как было» (последовательное
    /// армирование столбцов по одному). Этот класс — ТОЛЬКО визуальная
    /// надстройка: подписывается на <see cref="TrapSystemsController.ArrowWaveStarted"/>
    /// и запускает пролёт примитивного 3D-объекта через весь ряд от одного
    /// края до другого, синхронно с реальным таймингом волны (<c>Width *
    /// ArrowWaveTrapSystem.StepSeconds</c> — ровно столько же секунд реально
    /// занимает армирование всех столбцов). Направление пролёта («иногда
    /// слева иногда справа») — <see cref="RowWaveDirection"/> самого
    /// триггера, задаётся при генерации, этот класс её не выбирает.
    ///
    /// <b>Примитивная геометрия, не финальный арт</b> — тот же принцип, что
    /// <see cref="TunnelDebugVisual"/> (её doc-комментарий): тонкий вытянутый
    /// куб (древко) + немного больший куб на ведущем конце (наконечник),
    /// собранные кодом без .prefab — оба Cube, не Sphere (см. doc-комментарий
    /// <see cref="BuildArrowObject"/> про баг с устройства). Настоящая
    /// 3D-модель стрелы — отдельная задача авторинга (как текстуры
    /// <see cref="TileArtKind"/> уже не в скоупе агента).
    ///
    /// <b>Коллайдеры примитивов удаляются сразу после создания</b> — в
    /// отличие от плит <see cref="TunnelDebugVisual"/> (которым BoxCollider
    /// нужен для raycast примеривания), летящий декоративный объект не
    /// должен попадать под тот же raycast и мешать наведению на плиту.
    ///
    /// Чистый C#-класс, тикается тонким driver'ом
    /// (<see cref="ArrowProjectileVisualController"/>) — тот же паттерн, что
    /// <see cref="TunnelDebugVisual"/>/<c>Decay.TrailDecaySystem</c>.
    /// </summary>
    public sealed class ArrowProjectileVisual : IDisposable
    {
        // Размеры в долях TileSize — не баланс, чисто визуальные константы
        // (тот же статус, что CollapseTiltMinDegrees и подобные в
        // TunnelDebugVisual — не вынесены в дебаг-панель).
        private const float ShaftLength = 0.6f;
        private const float ShaftThickness = 0.08f;
        private const float HeadSize = 0.18f;

        // Высота пролёта над полом — доля TileSize. Чуть выше самих плит
        // (TunnelDebugVisual.TileHeight ~0.1), чтобы силуэт стрелы не тонул
        // в геометрии пола и был виден поперёк всего ряда.
        private const float FlightHeight = 0.3f;

        // Тёплый оранжевый с эмиссией — не пересекается с палитрой
        // TileDebugColor (опасность — сизо-фиолетовый/оливковый/пурпур,
        // ворота — янтарный, источники — фиолетовый/медный) и с
        // TunnelDebugVisual.BombWarningPulseTint/FallingRockWarningPulseTint
        // (те — пульсация НА плите, не отдельный летящий объект).
        private static readonly Color ProjectileColor = new Color(1f, 0.55f, 0.1f);

        private sealed class ActiveProjectile
        {
            public GameObject Root;
            public Vector3 Start;
            public Vector3 End;
            public float Duration;
            public float Elapsed;
        }

        private readonly TrapSystemsController _traps;
        private readonly WorldGridProjection _projection;
        private readonly int _width;
        private readonly Transform _parent;
        private readonly Material _material;
        private readonly List<ActiveProjectile> _active = new List<ActiveProjectile>();
        private bool _disposed;

        /// <summary>Ложь, если ни один шейдер не найден (см. doc-комментарий TunnelDebugVisual про баг 2026-08-14 на устройстве) — визуал безопасно самоотключается, ничего не создаёт.</summary>
        public bool IsEnabled => _material != null;

        public ArrowProjectileVisual(TrapSystemsController traps, WorldGridProjection projection, int width, Transform parent)
        {
            _traps = traps ?? throw new ArgumentNullException(nameof(traps));
            _projection = projection;
            _width = width;
            _parent = parent;
            _material = CreateMaterial();

            if (!IsEnabled)
            {
                Debug.LogWarning("ArrowProjectileVisual: ни один шейдер не найден (Shader.Find вернул null для всех вариантов) — визуал пролёта стрелы отключён в этой сборке.");
                return;
            }

            _traps.ArrowWaveStarted += OnArrowWaveStarted;
        }

        /// <summary>Продвигает все летящие объекты на <paramref name="deltaSeconds"/> реального времени — вызывать явно из Update() владеющего MonoBehaviour.</summary>
        public void Tick(float deltaSeconds)
        {
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var projectile = _active[i];
                projectile.Elapsed += deltaSeconds;
                var t = projectile.Duration > 0f ? Mathf.Clamp01(projectile.Elapsed / projectile.Duration) : 1f;
                if (projectile.Root != null)
                    projectile.Root.transform.localPosition = Vector3.Lerp(projectile.Start, projectile.End, t);

                if (t >= 1f)
                {
                    if (projectile.Root != null) UnityEngine.Object.Destroy(projectile.Root);
                    _active.RemoveAt(i);
                }
            }
        }

        /// <summary>Отписывается от Controller'а и уничтожает все ещё летящие объекты. Вызывать при завершении забега/уничтожении системы.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _traps.ArrowWaveStarted -= OnArrowWaveStarted;
            foreach (var projectile in _active)
                if (projectile.Root != null) UnityEngine.Object.Destroy(projectile.Root);
            _active.Clear();
            _disposed = true;
        }

        private void OnArrowWaveStarted(int row, RowWaveDirection direction)
        {
            if (!IsEnabled) return;

            var tileSize = _projection.TileSize;
            var firstColumn = direction == RowWaveDirection.LeftToRight ? 0 : _width - 1;
            var lastColumn = direction == RowWaveDirection.LeftToRight ? _width - 1 : 0;
            var start = _projection.ToWorldPosition(new GridCoordinate(row, firstColumn));
            var end = _projection.ToWorldPosition(new GridCoordinate(row, lastColumn));
            start.y = end.y = FlightHeight * tileSize;

            // Вся волна проходит за Width*StepSeconds секунд — ровно столько
            // реально занимает армирование всех столбцов ряда
            // (ArrowWaveTrapSystem.StepSeconds на каждый столбец) — пролёт
            // синхронизирован с реальным таймингом волны, не придумывает
            // свой собственный.
            var duration = _width * ArrowWaveTrapSystem.StepSeconds;

            var root = BuildArrowObject(tileSize, direction);
            root.transform.SetParent(_parent, worldPositionStays: false);
            root.transform.localPosition = start;

            _active.Add(new ActiveProjectile
            {
                Root = root,
                Start = start,
                End = end,
                Duration = duration,
                Elapsed = 0f
            });
        }

        private GameObject BuildArrowObject(float tileSize, RowWaveDirection direction)
        {
            var root = new GameObject("ArrowProjectile");

            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shaft.name = "Shaft";
            UnityEngine.Object.Destroy(shaft.GetComponent<Collider>());
            shaft.transform.SetParent(root.transform, worldPositionStays: false);
            shaft.transform.localScale = new Vector3(ShaftLength * tileSize, ShaftThickness * tileSize, ShaftThickness * tileSize);
            shaft.GetComponent<Renderer>().sharedMaterial = _material;

            // Наконечник — на ВЕДУЩЕМ конце древка по направлению полёта
            // (направление само по себе не меняет геометрию объекта, только
            // то, с какой стороны древка стоит наконечник) — тот же приём,
            // что различает FirstColumnIndex в ArrowWaveTrapSystem.
            //
            // Баг с устройства (живой плейтест, 2026-10-01): PrimitiveType.Sphere
            // падал на реальной сборке с "Can't add component because class
            // 'SphereCollider' doesn't exist!" на каждый пуск волны — IL2CPP
            // стриппинг вырезает SphereCollider, потому что ничто больше в
            // проекте на него не ссылается (тот же класс бага, что уже
            // решался для MeshCollider, см. doc-комментарий TunnelDebugVisual).
            // Не фатально (CreatePrimitive логирует и возвращает GameObject
            // без коллайдера, Destroy(null) ниже безопасен), но заливало
            // dev-console реальными ошибками на каждую стрелу — визуально
            // неотличимо от "игра ломается". Cube/BoxCollider — тот же
            // примитив, что уже безопасно использует TunnelDebugVisual везде.
            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            UnityEngine.Object.Destroy(head.GetComponent<Collider>());
            head.transform.SetParent(root.transform, worldPositionStays: false);
            head.transform.localScale = Vector3.one * (HeadSize * tileSize);
            var leadingEdge = (direction == RowWaveDirection.LeftToRight ? 1f : -1f) * (ShaftLength * 0.5f * tileSize);
            head.transform.localPosition = new Vector3(leadingEdge, 0f, 0f);
            head.GetComponent<Renderer>().sharedMaterial = _material;

            return root;
        }

        // Тот же fallback-список шейдеров, что TunnelDebugVisual.CreateTemplateMaterial —
        // уже подтверждён рабочим на устройстве (issue про Shader.Find,
        // 2026-08-14), не изобретается заново.
        private static Material CreateMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Unlit/Color");
            if (shader == null) return null;

            var material = new Material(shader) { color = ProjectileColor };
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", ProjectileColor * 0.6f);
            }
            material.enableInstancing = true;
            return material;
        }
    }
}
