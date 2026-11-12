using System;
using System.Collections.Generic;

namespace Burmalda.Persistence
{
    /// <summary>
    /// Сохраняемый прогресс (PRD "не в разделе" — сквозная инфраструктура,
    /// issue #107): "Прогресс (Идолы, Тотем, Руны, Монеты, Коллекция,
    /// разблокированный пул артефактов, Ярусы Глубины) сохраняется
    /// локально". <see cref="Serializable"/> для <c>UnityEngine.JsonUtility</c>.
    ///
    /// <b>Кристаллы удалены из игры полностью</b> (PRD v9 раздел 5, задача
    /// по экономике "Мана как доход забега") — поле <c>crystalsBalance</c>
    /// убрано без замены.
    ///
    /// <b>Идолы/Тотем/Руны намеренно не сохраняются</b> — в проекте пока
    /// нет каталога конкретных Идолов/Тотемов (только их классы, ни одного
    /// сконкретного экземпляра/содержимого не определено PRD, в отличие от
    /// 5 примеров Амулетов/Талисманов, issue #17) — сохранять нечего без
    /// придумывания игрового контента, которого нет в тексте PRD. Это
    /// честный, задокументированный пробел (см. changelog), не молчаливое
    /// решение — как только появится каталог конкретных Идолов/Тотемов
    /// (естественно — при контент-наполнении Спринта 10 или позже), сюда
    /// добавятся поля для их id/уровней пассивов.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public int coinsBalance;
        public List<string> collectionRecordedIds = new List<string>();
        public List<string> poolUnlockedIds = new List<string>();
        public int depthRecordBestTier;
        /// <summary>Привезённые, но не открытые Саркофаги (ранги, PRD v10 §22.3): потеря прогресса — критический дефект.</summary>
        public List<int> unopenedSarcophagiRanks = new List<int>();
        /// <summary>Редкость каждого неоткрытого Саркофага (параллельно рангам; нет в сохранениях версии &lt; 3 — тогда Редкий).</summary>
        public List<int> unopenedSarcophagiRarities = new List<int>();
        /// <summary>Бог каждого неоткрытого Саркофага (параллельно рангам; нет в сохранениях версии &lt; 3 — Леший).</summary>
        public List<int> unopenedSarcophagiGods = new List<int>();
        /// <summary>Части Идолов/Тотемов: id предмета и число частей (параллельные списки, PRD v10 §21.4, §29).</summary>
        public List<string> partItemIds = new List<string>();
        public List<int> partItemCounts = new List<int>();
        /// <summary>Защита от невезения (§21.2): число «пустых» Саркофагов по редкости [Редкий, Эпический, Легендарный] — для части и для Реликвии.</summary>
        public List<int> partMisses = new List<int>();
        public List<int> relicMisses = new List<int>();
        /// <summary>Первая часть уже выдана (§21.2, §29).</summary>
        public bool firstPartGranted;
        /// <summary>Активные Идол и Тотем (слоты §17; пустая строка — слот пуст).</summary>
        public string activeIdolId = "";
        public string activeTotemId = "";
        /// <summary>Уровни Идолов/Тотемов (L1–L3, PRD v10 §22.4): id и уровень — параллельные списки.</summary>
        public List<string> itemLevelIds = new List<string>();
        public List<int> itemLevels = new List<int>();
    }
}
