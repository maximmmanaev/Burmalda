using System.Collections.Generic;
using Burmalda.Altar;
using Burmalda.Artifacts;
using Burmalda.Core;
using Burmalda.Generation;

namespace Burmalda.Bootstrap
{
    /// <summary>
    /// Связывает Алтарь с генерацией тоннеля (PRD v10 §13.2): Ярус и номер Алтаря даёт <see cref="SegmentGenerationController"/>,
    /// Афиша — типы ловушек раскладки Святилища (пока одна раскладка — Леший, для всех Ярусов), сид — сид похода.
    /// </summary>
    public sealed class AltarContextAdapter : IAltarContext
    {
        private readonly SegmentGenerationController _segments;

        public AltarContextAdapter(SegmentGenerationController segments) => _segments = segments;

        public bool TryGetSpot(GridCoordinate coordinate, out int yarus, out int ordinal) =>
            _segments.TryGetAltarSpot(coordinate.Row, out yarus, out ordinal);

        public IReadOnlyCollection<ThreatType> AfishaThreats(int shrineYarus) => ThreatsOf(SegmentTemplateCatalog.SanctuaryLeshyTemplate);

        public int RunSeed => _segments.RunSeedValue;

        /// <summary>Типы угроз (§8.4) по ловушкам раскладки Святилища.</summary>
        public static IReadOnlyCollection<ThreatType> ThreatsOf(SegmentTemplate template)
        {
            var threats = new HashSet<ThreatType>();
            for (var r = 0; r < template.RowCount; r++)
                for (var c = 0; c < template.Width; c++)
                    switch (template.TileAt(r, c))
                    {
                        case SegmentTileType.FallingRockTrigger:
                        case SegmentTileType.MovingWallBothTrigger:
                        case SegmentTileType.MovingWallLeftTrigger:
                        case SegmentTileType.MovingWallRightTrigger:
                            threats.Add(ThreatType.Blunt);
                            break;
                        case SegmentTileType.ArrowWaveTrigger:
                        case SegmentTileType.BladeTactTrigger:
                            threats.Add(ThreatType.Slashing);
                            break;
                        case SegmentTileType.BombTrigger:
                            threats.Add(ThreatType.Explosion);
                            break;
                    }

            return threats;
        }
    }
}
