using System;
using System.Collections.Generic;
using System.Linq;

namespace Burmalda.Artifacts
{
    /// <summary>Ранг бога внутри мира (PRD v10 §5.2): младший, средний, старший.</summary>
    public enum GodRank
    {
        Junior,
        Middle,
        Senior,
    }

    /// <summary>Строка таблицы §5.2: какой бог хранит Святилище Яруса.</summary>
    public sealed class YarusGod
    {
        public YarusGod(int yarus, Mythology mythology, GodRank rank, God god, bool hasOwnLayout)
        {
            Yarus = yarus;
            Mythology = mythology;
            Rank = rank;
            God = god;
            HasOwnLayout = hasOwnLayout;
        }

        public int Yarus { get; }

        public Mythology Mythology { get; }

        public GodRank Rank { get; }

        public God God { get; }

        /// <summary>
        /// У Святилища есть собственная авторская раскладка. <b>Заглушка:</b> пока есть только раскладка Лешего, Ярусы 2–10
        /// используют её же — бог, ранг, мифология и Афиша берутся из таблицы, а раскладка — временная.
        /// </summary>
        public bool HasOwnLayout { get; }
    }

    /// <summary>Десять богов по Ярусам (PRD v10 §5.2) — единственный источник бога, ранга и мифологии Яруса.</summary>
    public static class GodTable
    {
        public const int YarusCount = 10;

        public static IReadOnlyList<YarusGod> All { get; } = new List<YarusGod>
        {
            new YarusGod(1, Mythology.Slavic, GodRank.Junior, God.Leshy, hasOwnLayout: true),
            new YarusGod(2, Mythology.Slavic, GodRank.Middle, God.Mokosh, hasOwnLayout: false),
            new YarusGod(3, Mythology.Slavic, GodRank.Senior, God.Perun, hasOwnLayout: false),
            new YarusGod(4, Mythology.Sumerian, GodRank.Junior, God.Humbaba, hasOwnLayout: false),
            new YarusGod(5, Mythology.Sumerian, GodRank.Middle, God.Ereshkigal, hasOwnLayout: false),
            new YarusGod(6, Mythology.Sumerian, GodRank.Senior, God.Enlil, hasOwnLayout: false),
            new YarusGod(7, Mythology.Ojibwe, GodRank.Junior, God.Memegwesi, hasOwnLayout: false),
            new YarusGod(8, Mythology.Ojibwe, GodRank.Middle, God.Mishibijiu, hasOwnLayout: false),
            new YarusGod(9, Mythology.Ojibwe, GodRank.Senior, God.Animikii, hasOwnLayout: false),
            new YarusGod(10, Mythology.Slavic, GodRank.Senior, God.Chernobog, hasOwnLayout: false), // финал
        };

        public static YarusGod ForYarus(int yarus)
        {
            if (yarus < 1 || yarus > YarusCount) throw new ArgumentOutOfRangeException(nameof(yarus), yarus, "Ярус должен быть в 1..10.");
            return All[yarus - 1];
        }

        public static YarusGod ForGod(God god) => All.Single(g => g.God == god);
    }
}
