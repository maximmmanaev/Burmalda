using System;
using NUnit.Framework;

namespace Burmalda.Generation.Tests
{
    public class GateVaultPricingTests
    {
        [Test]
        public void ComputeVaultKeys_OnePurchase_ReturnsKeysPerVaultPurchase()
        {
            Assert.AreEqual(GateVaultPricing.KeysPerVaultPurchase, GateVaultPricing.ComputeVaultKeys(1.0));
        }

        [Test]
        public void ComputeVaultKeys_OneAndAHalfPurchases_RoundsToNearest()
        {
            var original = GateVaultPricing.KeysPerVaultPurchase;
            try
            {
                GateVaultPricing.KeysPerVaultPurchase = 80;

                Assert.AreEqual(120, GateVaultPricing.ComputeVaultKeys(1.5));
            }
            finally
            {
                GateVaultPricing.KeysPerVaultPurchase = original;
            }
        }

        [Test]
        public void ComputeVaultKeys_ZeroOrNegativePurchases_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GateVaultPricing.ComputeVaultKeys(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => GateVaultPricing.ComputeVaultKeys(-1));
        }
    }
}
