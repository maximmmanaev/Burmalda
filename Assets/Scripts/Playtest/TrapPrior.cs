using System.Collections.Generic;
using Burmalda.Generation;

namespace Burmalda.Playtest
{
    /// <summary>
    /// «Опыт игрока»: как часто каждый тип ловушки Ярусов 1–3 встречается в авторских шаблонах.
    /// Считается по каталогу, а не задаётся руками. Бот использует это, когда тип триггера неизвестен.
    /// </summary>
    public sealed class TrapPrior
    {
        public double Rock, Arrows, Crusher, WallLeft, WallRight;

        public static readonly TrapPrior Tunnel = From(SegmentTemplateCatalog.All);
        public static readonly TrapPrior Sanctuary = From(new[] { SegmentTemplateCatalog.SanctuaryLeshyTemplate });

        public static TrapPrior From(IEnumerable<SegmentTemplate> templates)
        {
            var prior = new TrapPrior();
            foreach (var template in templates)
                for (var r = 0; r < template.RowCount; r++)
                    for (var c = 0; c < template.Width; c++)
                        switch (template.TileAt(r, c))
                        {
                            case SegmentTileType.FallingRockTrigger: prior.Rock++; break;
                            case SegmentTileType.ArrowWaveTrigger: prior.Arrows++; break;
                            case SegmentTileType.MovingWallBothTrigger: prior.Crusher++; break;
                            case SegmentTileType.MovingWallLeftTrigger: prior.WallLeft++; break;
                            case SegmentTileType.MovingWallRightTrigger: prior.WallRight++; break;
                        }

            var total = prior.Rock + prior.Arrows + prior.Crusher + prior.WallLeft + prior.WallRight;
            if (total <= 0) return new TrapPrior { Rock = 0.2, Arrows = 0.2, Crusher = 0.2, WallLeft = 0.2, WallRight = 0.2 };
            prior.Rock /= total;
            prior.Arrows /= total;
            prior.Crusher /= total;
            prior.WallLeft /= total;
            prior.WallRight /= total;
            return prior;
        }
    }
}
