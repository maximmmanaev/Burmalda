using NUnit.Framework;

namespace Burmalda.Currencies.Tests
{
    public class PersistentWalletTests
    {
        [Test]
        public void NewWallet_BalanceIsZero()
        {
            var wallet = new PersistentWallet();

            Assert.AreEqual(0, wallet.Balance);
        }

        [Test]
        public void Deposit_PositiveAmount_IncreasesBalance()
        {
            var wallet = new PersistentWallet();

            wallet.Deposit(10);
            wallet.Deposit(5);

            Assert.AreEqual(15, wallet.Balance);
        }

        [Test]
        public void Deposit_ZeroOrNegativeAmount_IsNoOp()
        {
            var wallet = new PersistentWallet();
            wallet.Deposit(10);

            wallet.Deposit(0);
            wallet.Deposit(-5);

            Assert.AreEqual(10, wallet.Balance);
        }

        [Test]
        public void Spend_SufficientBalance_DecreasesBalanceAndReturnsTrue()
        {
            var wallet = new PersistentWallet();
            wallet.Deposit(10);

            var spent = wallet.Spend(6);

            Assert.IsTrue(spent);
            Assert.AreEqual(4, wallet.Balance);
        }

        [Test]
        public void Spend_ExactBalance_LeavesZero()
        {
            var wallet = new PersistentWallet();
            wallet.Deposit(10);

            var spent = wallet.Spend(10);

            Assert.IsTrue(spent);
            Assert.AreEqual(0, wallet.Balance);
        }

        [Test]
        public void Spend_InsufficientBalance_ReturnsFalseAndLeavesBalanceUnchanged()
        {
            var wallet = new PersistentWallet();
            wallet.Deposit(5);

            var spent = wallet.Spend(6);

            Assert.IsFalse(spent);
            Assert.AreEqual(5, wallet.Balance);
        }

        [Test]
        public void Spend_ZeroOrNegativeAmount_ReturnsFalse()
        {
            var wallet = new PersistentWallet();
            wallet.Deposit(5);

            Assert.IsFalse(wallet.Spend(0));
            Assert.IsFalse(wallet.Spend(-1));
            Assert.AreEqual(5, wallet.Balance);
        }

        [Test]
        public void Deposit_RaisesChangedWithNewBalance()
        {
            var wallet = new PersistentWallet();
            var seen = -1;
            wallet.Changed += balance => seen = balance;

            wallet.Deposit(8);

            Assert.AreEqual(8, seen);
        }

        [Test]
        public void Spend_Successful_RaisesChangedWithNewBalance()
        {
            var wallet = new PersistentWallet();
            wallet.Deposit(10);
            var seen = -1;
            wallet.Changed += balance => seen = balance;

            wallet.Spend(4);

            Assert.AreEqual(6, seen);
        }

        [Test]
        public void Spend_Failed_DoesNotRaiseChanged()
        {
            var wallet = new PersistentWallet();
            wallet.Deposit(5);
            var fired = false;
            wallet.Changed += _ => fired = true;

            wallet.Spend(100);

            Assert.IsFalse(fired);
        }
    }
}
