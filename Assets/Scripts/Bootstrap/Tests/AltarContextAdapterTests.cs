using System.Linq;
using Burmalda.Artifacts;
using Burmalda.Generation;
using NUnit.Framework;

namespace Burmalda.Bootstrap.Tests
{
    public class AltarContextAdapterTests
    {
        [Test]
        public void LeshyLayout_Threats_AreBluntAndSlashing_ComputedFromTiles()
        {
            var threats = AltarContextAdapter.ThreatsOf(SegmentTemplateCatalog.SanctuaryLeshyTemplate);

            // Раскладка Лешего: Камни `r`, Давилка `d`, Стрелы `w` — без Лезвий/Бомб/Огня.
            CollectionAssert.AreEquivalent(new[] { ThreatType.Blunt, ThreatType.Slashing }, threats.ToList());
        }

        [Test]
        public void ThreatsOf_FollowTrapTilesOfTheLayout()
        {
            var tiles = new SegmentTileType[5, 5];
            tiles[1, 1] = SegmentTileType.BombTrigger;
            tiles[2, 2] = SegmentTileType.MovingWallLeftTrigger;
            var template = new SegmentTemplate("t", 1, SegmentRewardTag.Coins, tiles);

            CollectionAssert.AreEquivalent(new[] { ThreatType.Explosion, ThreatType.Blunt }, AltarContextAdapter.ThreatsOf(template).ToList());
        }
    }
}
