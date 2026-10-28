using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Generation.Tests
{
    /// <summary>
    /// Хотфикс «страж ролей плиты роняет забег вместо диагностики»
    /// (владелец, 2026-09-16, см. doc-комментарий
    /// Core.Tile.GuardAgainstConflictingRole/Core.Tile.ThrowOnRoleConflict):
    /// та же причина, что и <c>Core.Tests.TileGuardStrictModeSetUp</c> —
    /// EditMode-раннер не входит в Play mode, поэтому
    /// <c>Bootstrap.RunBootstrap.Awake</c> здесь не выполняется. Доработка
    /// PR #290, п.1: Authored-vs-Generated (единственный конфликт, который
    /// реально возникает в этой сборке — см.
    /// <c>Generation.Tests.SegmentGenerationCoexistenceTests.
    /// RevealedBeforeClaimed_ObstacleGeneratorWins_TemplateTriggerWins</c>)
    /// разрешается приоритетом источника ОДИНАКОВО что в строгом, что в
    /// нестрогом режиме — строгий режим здесь сейчас ничего не ловит
    /// сверх нестрогого, но остаётся включённым на случай будущего
    /// теста на конфликт одного источника (единственный случай, где
    /// строгий режим ещё бросает, см. doc-комментарий
    /// Core.Tile.GuardAgainstConflictingRole). <c>[SetUpFixture]</c> без
    /// явного namespace-класса внутри применяется ко всей сборке (это
    /// единственный такой класс в Burmalda.Generation.Tests).
    /// </summary>
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
