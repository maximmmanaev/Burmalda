using System.Linq;
using NUnit.Framework;

namespace Burmalda.Artifacts.Tests
{
    public class TotemCatalogTests
    {
        [Test]
        public void Totems_ContainsExactlyFour_OnePerAbilityType()
        {
            Assert.AreEqual(4, TotemCatalog.Totems.Count);

            var abilities = TotemCatalog.Totems.Select(t => t.ActiveAbility).ToList();
            CollectionAssert.AreEquivalent(
                new[] { TotemAbilityType.Dash, TotemAbilityType.Breach, TotemAbilityType.Invulnerability, TotemAbilityType.SecondWind },
                abilities);
        }

        [Test]
        public void Totems_AllHaveUniqueIds()
        {
            var ids = TotemCatalog.Totems.Select(t => t.Id).ToList();
            CollectionAssert.AllItemsAreUnique(ids);
        }

        [Test]
        public void Totems_AllHaveNonEmptyNames()
        {
            Assert.IsTrue(TotemCatalog.Totems.All(t => !string.IsNullOrEmpty(t.Name)));
        }
    }
}
