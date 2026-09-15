using NUnit.Framework;

namespace Burmalda.Core.Tests
{
    /// <summary>
    /// Хотфикс «страж ролей плиты роняет забег вместо диагностики»
    /// (владелец, 2026-09-16, см. doc-комментарий
    /// Tile.GuardAgainstConflictingRole/Tile.ThrowOnRoleConflict):
    /// <see cref="Tile.ThrowOnRoleConflict"/> по умолчанию false (поведение
    /// built-игры) — EditMode-тесты этой сборки ожидают СТРОГИЙ режим
    /// (бросает <see cref="System.InvalidOperationException"/>, см.
    /// <see cref="TileTests"/>), но EditMode-раннер не входит в Play mode,
    /// поэтому <c>Bootstrap.RunBootstrap.Awake</c> (который включает флаг
    /// через <c>Application.isEditor</c>) здесь никогда не выполняется —
    /// флаг нужно включить отдельно. <c>[SetUpFixture]</c> без явного
    /// namespace-класса внутри применяется ко всей сборке (это единственный
    /// такой класс в Burmalda.Core.Tests).
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
