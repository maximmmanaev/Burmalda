using System;
using NUnit.Framework;

namespace Burmalda.Artifacts.Tests
{
    public class IdolLoadoutTests
    {
        [Test]
        public void NewLoadout_BothSlotsAreEmpty()
        {
            var loadout = new IdolLoadout();

            Assert.IsNull(loadout.GetSlot(0));
            Assert.IsNull(loadout.GetSlot(1));
        }

        [Test]
        public void Equip_SetsSlotToGivenIdol()
        {
            var loadout = new IdolLoadout();
            var idol = new Idol("i1", "Тестовый");

            loadout.Equip(0, idol);

            Assert.AreSame(idol, loadout.GetSlot(0));
            Assert.IsNull(loadout.GetSlot(1));
        }

        [Test]
        public void Unequip_ClearsSlot()
        {
            var loadout = new IdolLoadout();
            loadout.Equip(1, new Idol("i1", "Тестовый"));

            loadout.Unequip(1);

            Assert.IsNull(loadout.GetSlot(1));
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void GetSlot_IndexOutOfRange_Throws(int index)
        {
            var loadout = new IdolLoadout();
            Assert.Throws<ArgumentOutOfRangeException>(() => loadout.GetSlot(index));
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void Equip_IndexOutOfRange_Throws(int index)
        {
            var loadout = new IdolLoadout();
            Assert.Throws<ArgumentOutOfRangeException>(() => loadout.Equip(index, new Idol("i1", "Тестовый")));
        }

        [Test]
        public void Equip_NullIdol_Throws()
        {
            var loadout = new IdolLoadout();
            Assert.Throws<ArgumentNullException>(() => loadout.Equip(0, null));
        }
    }
}
