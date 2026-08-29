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
        public bool hasWonBossBefore;
    }
}
