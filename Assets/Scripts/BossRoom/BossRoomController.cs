using Burmalda.Currencies;
using Burmalda.Movement;
using Burmalda.RunLifecycle;
using UnityEngine;

namespace Burmalda.BossRoom
{
    /// <summary>
    /// Тонкий driver <see cref="BossRoomRunSystem"/> (по паттерну
    /// <c>Altar.AltarController</c>/<c>Boss.BossController</c>): пересобирает
    /// систему на каждый <see cref="GridTraceInputController.RunStarted"/>
    /// (нужны свежие Grid/Trail/Мана этого забега), тикает волну реальным
    /// временем в <see cref="Update"/>. Добавляется <c>Bootstrap.RunBootstrap</c>
    /// на тот же GameObject, что и остальные Controller'ы забега — не
    /// самобутстрапится сам.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossRoomController : MonoBehaviour
    {
        [SerializeField] private GridTraceInputController _input;
        [SerializeField] private CurrencyController _currency;
        [SerializeField] private RunController _runController;

        private BossRoomRunSystem _system;

        /// <summary>Активная Комната текущего забега — null вне Комнаты. Для HUD (см. <c>DebugVisuals.RunHudOverlay</c>).</summary>
        public BossRoom ActiveRoom => _system?.ActiveRoom;

        private void Awake()
        {
            if (_input == null) _input = GetComponent<GridTraceInputController>();
            if (_currency == null) _currency = GetComponent<CurrencyController>();
            if (_runController == null) _runController = GetComponent<RunController>();
        }

        private void OnEnable()
        {
            if (_input != null) _input.RunStarted += HandleRunStarted;
        }

        private void OnDisable()
        {
            if (_input != null) _input.RunStarted -= HandleRunStarted;
            DisposeSystem();
        }

        private void Update()
        {
            if (_system == null) RebuildSystem();
            _system?.Tick(Time.deltaTime);
        }

        private void HandleRunStarted() => RebuildSystem();

        private void RebuildSystem()
        {
            DisposeSystem();
            if (_input == null || _input.Grid == null || _input.Trail == null) return;
            if (_currency == null || _currency.RunManaCrystals == null) return;
            if (_runController == null) return;

            _system = new BossRoomRunSystem(_input.Grid, _input.Trail, _currency.RunManaCrystals, () => UnityEngine.Random.value, ReportBossDefeat);
        }

        // Читает RunController.RunState заново при каждом вызове — не
        // захватывает инстанс на момент пересборки (тот же паттерн, что
        // Boss.BossController.ReportBossDefeat — см. её докстрингу).
        private void ReportBossDefeat(string reason) => _runController.RunState?.ReportBossDefeat(reason);

        private void DisposeSystem()
        {
            _system?.Dispose();
            _system = null;
        }
    }
}
