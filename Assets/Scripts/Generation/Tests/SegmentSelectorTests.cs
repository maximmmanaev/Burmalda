using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Burmalda.Generation.Tests
{
    public class SegmentSelectorTests
    {
        private const int Width = 3;

        // Задача «партии 1 и 2 + правила отбора»: TierWindowBelow/Above —
        // mutable static (дебаг-панель), снимок/восстановление вокруг
        // КАЖДОГО теста — тот же принцип, что и share-поля
        // TunnelObstacleGenerator (Core.Tests.TunnelObstacleGeneratorTests).
        private int _savedWindowBelow, _savedWindowAbove;

        [SetUp]
        public void SaveWindow()
        {
            _savedWindowBelow = SegmentSelector.TierWindowBelow;
            _savedWindowAbove = SegmentSelector.TierWindowAbove;
        }

        [TearDown]
        public void RestoreWindow()
        {
            SegmentSelector.TierWindowBelow = _savedWindowBelow;
            SegmentSelector.TierWindowAbove = _savedWindowAbove;
        }

        private static SegmentTemplate Template(string name, int difficultyTier, SegmentRewardTag tag = SegmentRewardTag.Coins)
        {
            var tiles = new SegmentTileType[5, Width];
            return new SegmentTemplate(name, difficultyTier, tag, tiles);
        }

        private static readonly SegmentTemplate Easy = Template("easy", 1);
        private static readonly SegmentTemplate Medium = Template("medium", 3);
        private static readonly SegmentTemplate Hard = Template("hard", 5);

        [Test]
        public void SelectNext_DefaultWindow_OnlyTargetAndOneTierAbove()
        {
            // Дефолт [цель, цель+1] — пример из задачи "тиры 1-2 на первом Ярусе".
            var catalog = new List<SegmentTemplate> { Easy, Medium, Hard };
            var selector = new SegmentSelector(catalog, new RunSeed(1));

            for (var i = 0; i < 200; i++)
            {
                var tier = selector.SelectNext(1).DifficultyTier;
                Assert.GreaterOrEqual(tier, 1);
                Assert.LessOrEqual(tier, 2);
            }
        }

        [Test]
        public void SelectNext_DefaultWindow_ExcludesTemplatesBelowTarget()
        {
            // Тир 1 (Easy) не должен возвращаться, когда цель — 3: окно [3,4].
            var catalog = new List<SegmentTemplate> { Easy, Medium, Hard };
            var selector = new SegmentSelector(catalog, new RunSeed(1));

            for (var i = 0; i < 200; i++)
                Assert.AreNotSame(Easy, selector.SelectNext(3));
        }

        [Test]
        public void SelectNext_WindowClampedToValidTierRange()
        {
            // Цель 5 + TierWindowAbove(1) вышла бы за MaxDifficultyTier(5) —
            // должно зажаться, не бросать исключение из-за пустого диапазона.
            var catalog = new List<SegmentTemplate> { Hard };
            var selector = new SegmentSelector(catalog, new RunSeed(1));

            Assert.AreSame(Hard, selector.SelectNext(5));
        }

        [Test]
        public void SelectNext_NoCandidatesMatchWindow_Throws()
        {
            var catalog = new List<SegmentTemplate> { Hard };
            var selector = new SegmentSelector(catalog, new RunSeed(1));

            Assert.Throws<InvalidOperationException>(() => selector.SelectNext(1));
        }

        [Test]
        public void SelectNext_SameSeed_ProducesIdenticalSequence()
        {
            SegmentSelector.TierWindowBelow = 10; // широкое окно — тест про детерминизм, не про фильтрацию
            SegmentSelector.TierWindowAbove = 10;
            var catalog = new List<SegmentTemplate> { Easy, Medium, Hard };
            var a = new SegmentSelector(catalog, new RunSeed(777));
            var b = new SegmentSelector(catalog, new RunSeed(777));

            for (var i = 0; i < 30; i++)
                Assert.AreSame(a.SelectNext(5), b.SelectNext(5));
        }

        [Test]
        public void SelectNext_ManyDraws_VisitsMoreThanOneCandidate()
        {
            SegmentSelector.TierWindowBelow = 10; // широкое окно — тест про разнообразие выбора, не про фильтрацию
            SegmentSelector.TierWindowAbove = 10;
            var catalog = new List<SegmentTemplate> { Easy, Medium, Hard };
            var selector = new SegmentSelector(catalog, new RunSeed(42));

            var seen = new HashSet<SegmentTemplate>();
            for (var i = 0; i < 100; i++) seen.Add(selector.SelectNext(5));

            Assert.Greater(seen.Count, 1);
        }

        // Задача «партии 1 и 2 + правила отбора»: "не повторять шаблон подряд".
        [Test]
        public void SelectNext_DoesNotRepeatLastSelected_WhenOtherCandidatesExist()
        {
            SegmentSelector.TierWindowBelow = 10;
            SegmentSelector.TierWindowAbove = 10;
            var catalog = new List<SegmentTemplate> { Easy, Medium, Hard };
            var selector = new SegmentSelector(catalog, new RunSeed(3));

            var previous = selector.SelectNext(5);
            for (var i = 0; i < 100; i++)
            {
                var next = selector.SelectNext(5);
                Assert.AreNotSame(previous, next, "Один и тот же шаблон применён дважды подряд.");
                previous = next;
            }
        }

        [Test]
        public void SelectNext_SingleCandidateInWindow_RepeatsRegardlessOfLastSelected()
        {
            // Единственный кандидат — сам себе последний; повтор неизбежен и не должен бросать исключение.
            var catalog = new List<SegmentTemplate> { Hard };
            var selector = new SegmentSelector(catalog, new RunSeed(1));

            for (var i = 0; i < 5; i++)
                Assert.AreSame(Hard, selector.SelectNext(5));
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
