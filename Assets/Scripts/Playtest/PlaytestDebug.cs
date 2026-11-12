using System.Text;
using Burmalda.Core;

namespace Burmalda.Playtest
{
    /// <summary>Отладочный вид поля вокруг бота — для разбора смертей.</summary>
    public static class PlaytestDebug
    {
        /// <summary>
        /// Ряды от дальнего к ближнему. @ бот, # стена, l Лава, T раскрытый триггер, t скрытый триггер,
        /// ! предупреждение Камня, A Алтарь, S Саркофаг, o пройденная плита, . пол, пробел — плиты ещё нет.
        /// </summary>
        public static string DumpAround(PlaytestRun run, int back = 3, int ahead = 6)
        {
            var position = run.Trail.CurrentPosition;
            var text = new StringBuilder();
            for (var row = position.Row + ahead; row >= position.Row - back; row--)
            {
                if (row < 0) continue;
                text.Append(row.ToString().PadLeft(4)).Append(' ');
                for (var column = 0; column < PlaytestRun.Width; column++)
                {
                    var c = new GridCoordinate(row, column);
                    text.Append(Symbol(run, c, position));
                }

                text.AppendLine();
            }

            return text.ToString();
        }

        private static char Symbol(PlaytestRun run, GridCoordinate c, GridCoordinate position)
        {
            if (c == position) return '@';
            if (!run.Grid.TryGetTile(c, out var tile)) return ' ';
            if (tile.IsBlocked || (tile.IsGated && !tile.IsLeverGateOpen)) return '#';
            if (tile.LethalTrap.HasValue) return 'l';
            if (tile.IsFallingRockWarningActive) return '!';
            if (tile.IsTrapTrigger) return tile.IsDangerSignatureRevealed ? 'T' : 't';
            if (tile.IsAltar) return 'A';
            if (tile.HasSarcophagus) return 'S';
            return run.Trail.HasVisited(c) ? 'o' : '.';
        }
    }
}
