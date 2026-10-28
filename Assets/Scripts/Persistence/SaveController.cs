using System;
using Burmalda.Altar;
using Burmalda.Sanctuary;
using Burmalda.Bootstrap;
using Burmalda.Currencies;
using UnityEngine;

namespace Burmalda.Persistence
{
    /// <summary>
    /// Тонкая обвязка над <see cref="SaveCoordinator"/> (issue #107, #330). Создаётся из кода —
    /// <see cref="Create"/> по <c>RuntimeInitializeOnLoadMethod</c>, сцену не правим. Ждёт, пока
    /// <see cref="RunBootstrap"/> соберёт кошелёк, Алтарь и Босса, затем один раз загружает прогресс и
    /// после этого сохраняет по событиям изменения (монеты, коллекция, пул, рекорд глубины, первая победа),
    /// а при сворачивании, потере фокуса и выходе — срочно.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SaveController : MonoBehaviour
    {
        /// <summary>Имя объекта: адресат <c>SendMessage</c> из .jslib.</summary>
        public const string HostName = "SaveHost";

        [SerializeField] private CurrencyController _currency;
        [SerializeField] private AltarController _altar;
        [SerializeField] private SanctuaryController _sanctuary;

        private SaveCoordinator _coordinator;
        private bool _subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (FindAnyObjectByType<SaveController>() != null) return;

            var host = new GameObject(HostName);
            host.AddComponent<SaveController>();
            DontDestroyOnLoad(host);
        }

        private void Update()
        {
            if (_coordinator == null) TryStart();
            else _coordinator.Tick(Time.unscaledTime); // не Time.time: на паузе платформы timeScale = 0
        }

        private void TryStart()
        {
            var bootstrap = RunBootstrap.Instance;
            if (_currency == null && bootstrap != null) _currency = bootstrap.Currency;
            if (_altar == null && bootstrap != null) _altar = bootstrap.Altar;
            if (_sanctuary == null && bootstrap != null) _sanctuary = bootstrap.Sanctuary;
            if (!IsReady()) return;

            _coordinator = new SaveCoordinator(
                SaveStorageFactory.Create(HostName),
                () => ProgressSnapshot.Capture(_currency.Coins, _altar.Collection, _altar.Pool, _sanctuary.DepthRecord),
                data => ProgressSnapshot.Apply(data, _currency.Coins, _altar.Collection, _altar.Pool, _sanctuary.DepthRecord),
                () => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            _coordinator.Load(Subscribe);
        }

        // Подписка — после загрузки: Apply сам дёргает события, и это не повод писать обратно то же самое.
        private void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            _currency.Coins.Changed += OnCoinsChanged;
            _altar.Collection.Recorded += OnId;
            _altar.Pool.Unlocked += OnId;
            _sanctuary.DepthRecord.RecordBroken += OnCoinsChanged;
        }

        private void OnDestroy()
        {
            if (!_subscribed) return;
            _currency.Coins.Changed -= OnCoinsChanged;
            _altar.Collection.Recorded -= OnId;
            _altar.Pool.Unlocked -= OnId;
            _sanctuary.DepthRecord.RecordBroken -= OnCoinsChanged;
        }

        private void OnCoinsChanged(int _) => _coordinator.MarkDirty();
        private void OnId(string _) => _coordinator.MarkDirty();

        private void OnApplicationPause(bool paused)
        {
            if (paused) _coordinator?.Flush();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) _coordinator?.Flush();
        }

        private void OnApplicationQuit() => _coordinator?.Flush();

        // Ответы .jslib (SendMessage, аргумент всегда строка).
        private void OnCloudLoaded(string text) => YandexCloudSaveStorage.Current?.OnLoaded(text);
        private void OnCloudLoadFailed(string error)
        {
            Debug.LogWarning($"[Save] облако недоступно, читаем локальную копию: {error}");
            YandexCloudSaveStorage.Current?.OnLoaded(null);
        }
        private void OnCloudWritten(string ok) => YandexCloudSaveStorage.Current?.OnWritten(ok == "1");

        private bool IsReady() =>
            _currency != null && _currency.Coins != null &&
            _altar != null && _altar.Collection != null && _altar.Pool != null &&
            _sanctuary != null && _sanctuary.DepthRecord != null;
    }
}
