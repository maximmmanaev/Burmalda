using System;
using Burmalda.Core;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.Movement.Tests
{
    public class WorldGridProjectionTests
    {
        [Test]
        public void ToGridCoordinate_OriginPoint_ReturnsRowZeroAndCenterColumn()
        {
            var projection = new WorldGridProjection(tileSize: 1f, width: 5);

            var coordinate = projection.ToGridCoordinate(new Vector3(0f, 0f, 0f));

            Assert.AreEqual(new GridCoordinate(0, 2), coordinate);
        }

        [Test]
        public void ToGridCoordinate_PointFurtherAlongTunnel_IncreasesRow()
        {
            var projection = new WorldGridProjection(tileSize: 2f, width: 5);

            var coordinate = projection.ToGridCoordinate(new Vector3(0f, 0f, 5f));

            Assert.AreEqual(2, coordinate.Row);
        }

        [Test]
        public void ToWorldPosition_ThenToGridCoordinate_RoundTripsToSameCoordinate()
        {
            var projection = new WorldGridProjection(tileSize: 1.5f, width: 5);
            var original = new GridCoordinate(4, 1);

            var worldPosition = projection.ToWorldPosition(original);
            var roundTripped = projection.ToGridCoordinate(worldPosition);

            Assert.AreEqual(original, roundTripped);
        }

        [Test]
        public void Constructor_NonPositiveTileSize_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WorldGridProjection(0f, 5));
        }
    }
}
