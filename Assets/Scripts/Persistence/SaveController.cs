using System;
using System.IO;
using Burmalda.Altar;
using Burmalda.Boss;
using Burmalda.Currencies;
using UnityEngine;

namespace Burmalda.Persistence
{
    /// <summary>
    /// Сохранение/загрузка прогресса (PRD — сквозная инфраструктура, issue
    /// #107, релиз-блокер): "сохраняется локально и переживает перезапуск
    /// приложения". Файл в <see cref="Application.persistentDataPath"/> —
    /// работает без сети (игра и так офлайн-работоспособна целиком: в
    /// проекте нет ни одного сетевого вызова ни в одной системе).
    ///
    /// Загрузка — в <see cref="Start"/> (после того, как все остальные
    /// Controller'ы уже отработали свой <c>Awake</c> и создали пустые
    /// постоянные кошельки/пул/рекорд — <see cref="ProgressSnapshot.Apply"/>
    /// аддитивно доливает в них сохранённые значения). Сохранение — на
    /// <see cref="OnApplicationPause"/> (сворачивание — основной риск потери
    /// прогресса на мобильном) и <see cref="OnApplicationQuit"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SaveController : MonoBehaviour
    {
        private const string SaveFileName = "burmalda_save.json";

        [SerializeField] private CurrencyController _currency;
        [SerializeField] private AltarController _altar;
        [SerializeField] private BossController _boss;

        private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        private void Awake()
        {
            if (_currency == null) _currency = GetComponent<CurrencyController>();
            if (_altar == null) _altar = GetComponent<AltarController>();
            if (_boss == null) _boss = GetComponent<BossController>();
        }

        private void Start() => Load();

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) Save();
        }

        private void OnApplicationQuit() => Save();

        public void Save()
        {
            if (!IsReady()) return;

            var data = ProgressSnapshot.Capture(_currency.Coins, _altar.Collection, _altar.Pool, _boss.DepthRecord, _boss.Tracker);
            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(data));
            }
            catch (IOException)
            {
                // Диск недоступен/переполнен — прогресс останется в памяти
                // текущей сессии, следующая попытка Save() (пауза/выход)
                // повторит запись. Не бросаем — сбой сохранения не должен
                // ронять игру.
            }
        }

        public void Load()
        {
            if (!IsReady()) return;
            if (!File.Exists(SavePath)) return;

            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException)
            {
                return; // повреждённый файл сохранения — не роняем игру, прогресс не восстановлен
            }

            ProgressSnapshot.Apply(data, _currency.Coins, _altar.Collection, _altar.Pool, _boss.DepthRecord, _boss.Tracker);
        }

        private bool IsReady() =>
            _currency != null && _currency.Coins != null &&
            _altar != null && _altar.Collection != null && _altar.Pool != null &&
            _boss != null && _boss.DepthRecord != null && _boss.Tracker != null;
    }
}
