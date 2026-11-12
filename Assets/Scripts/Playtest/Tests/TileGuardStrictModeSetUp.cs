using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Playtest.Tests
{
    /// <summary>Строгий страж ролей плиты на время тестов бота — как в Generation.Tests.</summary>
    [SetUpFixture]
    public sealed class TileGuardStrictModeSetUp
    {
        private bool _savedThrowOnRoleConflict;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _savedThrowOnRoleConflict = Tile.ThrowOnRoleConflict;
            Tile.ThrowOnRoleConflict = true;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            Tile.ThrowOnRoleConflict = _savedThrowOnRoleConflict;
        }
    }
}
