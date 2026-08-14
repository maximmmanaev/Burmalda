using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Burmalda.Generation.Tests
{
    public class SegmentSelectorTests
    {
        private const int Width = 3;

        private static SegmentTemplate Template(string name, int difficultyTier, SegmentRewardTag tag = SegmentRewardTag.Coins)
        {
            var tiles = new SegmentTileType[5, Width];
            return new SegmentTemplate(name, difficultyTier, tag, tiles);
        }

        private static readonly SegmentTemplate Easy = Template("easy", 1);
        private static readonly SegmentTemplate Medium = Template("medium", 3);
        private static readonly SegmentTemplate Hard = Template("hard", 5);

        [Test]
        public void SelectNext_FiltersOutTemplatesAboveMaxDifficulty()
        {
            var catalog = new List<SegmentTemplate> { Easy, Medium, Hard };
            var selector = new SegmentSelector(catalog, new RunSeed(1));

            for (var i = 0; i < 200; i++)
                Assert.LessOrEqual(selector.SelectNext(2).DifficultyTier, 2);
        }

        [Test]
        public void SelectNext_NoCandidatesMatchFilter_Throws()
        {
            var catalog = new List<SegmentTemplate> { Hard };
            var selector = new SegmentSelector(catalog, new RunSeed(1));

            Assert.Throws<InvalidOperationException>(() => selector.SelectNext(1));
        }

        [Test]
        public void SelectNext_SameSeed_ProducesIdenticalSequence()
        {
            var catalog = new List<SegmentTemplate> { Easy, Medium, Hard };
            var a = new SegmentSelector(catalog, new RunSeed(777));
            var b = new SegmentSelector(catalog, new RunSeed(777));

            for (var i = 0; i < 30; i++)
                Assert.AreSame(a.SelectNext(5), b.SelectNext(5));
        }

        [Test]
        public void SelectNext_ManyDraws_VisitsMoreThanOneCandidate()
        {
            var catalog = new List<SegmentTemplate> { Easy, Medium, Hard };
            var selector = new SegmentSelector(catalog, new RunSeed(42));

            var seen = new HashSet<SegmentTemplate>();
            for (var i = 0; i < 100; i++) seen.Add(selector.SelectNext(5));

            Assert.Greater(seen.Count, 1);
        }

        [Test]
        public void Constructor_EmptyCatalog_Throws()
        {
            Assert.Throws<ArgumentException>(() => new SegmentSelector(new List<SegmentTemplate>(), new RunSeed(1)));
        }

        [Test]
        public void Constructor_NullSeed_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new SegmentSelector(new List<SegmentTemplate> { Easy }, null));
        }
    }
}
