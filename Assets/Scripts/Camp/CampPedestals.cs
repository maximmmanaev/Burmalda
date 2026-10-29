using System;
using System.Linq;
using Burmalda.Artifacts;

namespace Burmalda.Camp
{
    /// <summary>
    /// Состояние постамента Идола или Тотема у входа в гробницу (PRD v10 §22.2): тёмный силуэт, каждая часть зажигает кусок
    /// (<c>1/4</c>), собранный предмет стоит на постаменте. Накопленная защита от невезения — разгорающийся силуэт [ГД].
    /// </summary>
    public readonly struct PedestalState
    {
        public PedestalState(CollectibleKind kind, CollectibleItem item, int lit, int total, bool assembled, float pityGlow)
        {
            Kind = kind;
            Item = item;
            Lit = lit;
            Total = total;
            Assembled = assembled;
            PityGlow = pityGlow;
        }

        public CollectibleKind Kind { get; }

        /// <summary>Предмет на постаменте; null — пустой постамент (тёмный силуэт без предмета).</summary>
        public CollectibleItem Item { get; }

        /// <summary>Зажжено кусков (частей).</summary>
        public int Lit { get; }

        /// <summary>Кусков всего (частей до сборки); 0 у пустого постамента.</summary>
        public int Total { get; }

        public bool Assembled { get; }

        /// <summary>0..1: насколько ярче тёмный силуэт из-за накопленной защиты от невезения [ГД].</summary>
        public float PityGlow { get; }
    }

    /// <summary>Ближайшая цель после открытия Саркофагов (§22.3): предмет, до сборки которого меньше всего частей.</summary>
    public readonly struct CampGoal
    {
        public CampGoal(CollectibleItem item, int partsLeft)
        {
            Item = item;
            PartsLeft = partsLeft;
        }

        public CollectibleItem Item { get; }

        public int PartsLeft { get; }
    }

    public static class CampPedestals
    {
        /// <summary>Сумма «пустых» Саркофагов по частям, при которой силуэт горит ярче всего [ГД, черновик].</summary>
        public static int PityGlowCap = 20;

        /// <summary>
        /// Что стоит на постаменте вида [ГД]: предмет, который собирается сейчас (больше всего частей, не собран); если ничего не
        /// собирается — собранный предмет высшей редкости; иначе пусто.
        /// </summary>
        public static PedestalState For(CollectionProgress progress, CollectibleKind kind)
        {
            var items = CollectibleCatalog.All.Where(i => i.Kind == kind).ToList();
            var glow = PityGlowOf(progress);

            var inProgress = items.Where(i => !progress.IsAssembled(i) && progress.PartsOf(i.Id) > 0)
                .OrderByDescending(i => progress.PartsOf(i.Id)).FirstOrDefault();
            if (inProgress != null)
                return new PedestalState(kind, inProgress, progress.PartsOf(inProgress.Id), inProgress.PartsRequired, false, glow);

            var assembled = items.Where(progress.IsAssembled).OrderByDescending(i => (int)i.Rarity).FirstOrDefault();
            if (assembled != null)
                return new PedestalState(kind, assembled, assembled.PartsRequired, assembled.PartsRequired, true, glow);

            return new PedestalState(kind, null, 0, 0, false, glow);
        }

        public static float PityGlowOf(CollectionProgress progress)
        {
            var misses = Enumerable.Range(0, 3).Sum(i => progress.PartMisses((RelicRarity)i));
            return Math.Min(1f, misses / (float)Math.Max(1, PityGlowCap));
        }

        /// <summary>Ближайшая цель: начатый предмет, которому не хватает меньше всего частей; иначе первый несобранный Редкий; null, если всё собрано.</summary>
        public static CampGoal? NearestGoal(CollectionProgress progress)
        {
            var open = CollectibleCatalog.All.Where(i => !progress.IsAssembled(i)).ToList();
            if (open.Count == 0) return null;
            var best = open.OrderBy(i => progress.PartsOf(i.Id) > 0 ? 0 : 1)
                .ThenBy(i => i.PartsRequired - progress.PartsOf(i.Id))
                .ThenBy(i => (int)i.Rarity).First();
            return new CampGoal(best, best.PartsRequired - progress.PartsOf(best.Id));
        }
    }
}
