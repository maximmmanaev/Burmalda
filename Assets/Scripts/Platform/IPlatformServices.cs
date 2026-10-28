using System;

namespace Burmalda.Platform
{
    /// <summary>
    /// Сервисы платформы (Яндекс Игры, issue #328): инициализация, сигнал готовности,
    /// разметка геймплея, пауза и язык. Рекламу и покупки добавим сюда же, когда дойдём до монетизации.
    /// Вызовы до окончания <see cref="Initialize"/> допустимы — реализация сама их откладывает или игнорирует.
    /// </summary>
    public interface IPlatformServices
    {
        /// <summary>SDK платформы готов (в редакторе и вне WebGL — после <see cref="Initialize"/>).</summary>
        bool IsInitialized { get; }

        /// <summary>Код языка из SDK ("ru", "en", …); null, пока SDK не готов или его нет.</summary>
        string Language { get; }

        /// <summary>Срабатывает один раз, когда <see cref="IsInitialized"/> стал true.</summary>
        event Action Initialized;

        /// <summary>Платформа просит поставить игру на паузу (реклама, вкладка, окно покупки).</summary>
        event Action Paused;

        /// <summary>Платформа разрешила продолжить игру.</summary>
        event Action Resumed;

        void Initialize();

        /// <summary>Игра загрузилась и готова к взаимодействию (LoadingAPI.ready).</summary>
        void SignalReady();

        /// <summary>Игрок начал или возобновил игровой процесс (GameplayAPI.start).</summary>
        void GameplayStart();

        /// <summary>Игрок закончил или приостановил игровой процесс (GameplayAPI.stop).</summary>
        void GameplayStop();
    }
}
