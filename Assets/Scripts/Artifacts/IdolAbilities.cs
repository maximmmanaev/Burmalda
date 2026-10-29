using System;
using System.Collections.Generic;
using Burmalda.Core;

namespace Burmalda.Artifacts
{
    /// <summary>
    /// Способности пяти Идолов, 3 уровня (PRD v10 §18.2). Числа — ЧЕРНОВЫЕ [CONFIG] (статические поля). Ни одна не восстанавливает
    /// разрушенный маршрут, не даёт иммунитета к Лаве и не заменяет Бросок Судьбы (§32).
    /// </summary>
    public static class IdolAbilities
    {
        public const string Rush = "idol-rush";
        public const string Shield = "idol-shield";
        public const string Breach = "idol-breach";
        public const string Time = "idol-time";
        public const string Passage = "idol-passage";

        // ---- Числа [CONFIG, черновик] ----
        public static int RushDistance = 2;
        public static float RushInvulnerabilitySeconds = 1f;
        public static int BreachLineLength = 3;
        public static float SlowSeconds = 3f;
        public static float FreezeSeconds = 1.5f;
        public static int TimeRevealRadius = 4;
        public static int PassageRadiusL1 = 2;
        public static int PassageRadiusL2 = 3;

        public static IReadOnlyList<IIdolAbility> All() => new IIdolAbility[]
        {
            new RushAbility(), new ShieldAbility(), new BreachAbility(), new TimeAbility(), new PassageAbility(),
        };

        // ---- Геометрия ----

        /// <summary>Плита существует, цела и проходима: не стена, не разрушена, без ловушки, ворота открыты.</summary>
        public static bool IsIntact(TunnelGrid grid, GridCoordinate c)
        {
            if (!grid.Contains(c) || !grid.TryGetTile(c, out var tile)) return false;
            return !tile.IsBlocked && !tile.IsDestroyed && !tile.LethalTrap.HasValue && !(tile.IsGated && !tile.IsLeverGateOpen);
        }

        private static (int dr, int dc) Direction(GridCoordinate from, GridCoordinate aim) =>
            (Math.Sign(aim.Row - from.Row), Math.Sign(aim.Column - from.Column));

        // ---- Рывок ----

        private sealed class RushAbility : IIdolAbility
        {
            public string IdolId => Rush;

            public IdolTargeting Targeting => IdolTargeting.Aim;

            public bool Execute(IdolAbilityContext ctx, int level, GridCoordinate? aim)
            {
                if (!aim.HasValue) return false;
                var from = ctx.Trail.CurrentPosition;
                var (dr, dc) = Direction(from, aim.Value);
                if (dr == 0 && dc == 0) return false;
                if (level < 2 && dr != 0 && dc != 0) return false; // ур. 1: только по прямой, ур. 2 — любое из 8 направлений

                // Через опасность (ловушки, Лава, разрушенные плиты), но не через стены.
                for (var i = 1; i < RushDistance; i++)
                {
                    var mid = new GridCoordinate(from.Row + dr * i, from.Column + dc * i);
                    if (ctx.Grid.Contains(mid) && ctx.Grid.TryGetTile(mid, out var tile) && tile.IsBlocked) return false;
                }

                var landing = new GridCoordinate(from.Row + dr * RushDistance, from.Column + dc * RushDistance);
                if (!ctx.Trail.JumpTo(landing)) return false;
                if (level >= 3) ctx.Effects.GrantInvulnerability(RushInvulnerabilitySeconds); // ур. 3: после прыжка 1 с неуязвимости к ловушкам
                return true;
            }
        }

        // ---- Щит ----

        private sealed class ShieldAbility : IIdolAbility
        {
            public string IdolId => Shield;

            public IdolTargeting Targeting => IdolTargeting.Immediate;

            public bool Execute(IdolAbilityContext ctx, int level, GridCoordinate? aim)
            {
                ctx.Effects.GrantShield(level >= 3 ? 2 : 1); // ур. 3: выдерживает два попадания подряд
                // Ур. 2: поглощённый удар отбрасывает игрока на безопасную соседнюю плиту. Лаву щит не поглощает — отбрасывать не от чего.
                ctx.Effects.ShieldAbsorbed = level >= 2 ? (Action)(() => KnockBack(ctx)) : null;
                return true;
            }

            private static void KnockBack(IdolAbilityContext ctx)
            {
                var from = ctx.Trail.CurrentPosition;
                GridCoordinate? best = null;
                for (var dr = -1; dr <= 1; dr++)
                    for (var dc = -1; dc <= 1; dc++)
                    {
                        if (dr == 0 && dc == 0) continue;
                        var c = new GridCoordinate(from.Row + dr, from.Column + dc);
                        if (!IsIntact(ctx.Grid, c)) continue;
                        // Предпочитаем уже пройденную плиту (маршрут цел), затем — ближайшую назад.
                        if (!best.HasValue || Score(ctx, c) > Score(ctx, best.Value)) best = c;
                    }

                if (best.HasValue) ctx.Trail.JumpTo(best.Value);
            }

            private static int Score(IdolAbilityContext ctx, GridCoordinate c) => (ctx.Trail.HasVisited(c) ? 10 : 0) - c.Row;
        }

        // ---- Пробой ----

        private sealed class BreachAbility : IIdolAbility
        {
            public string IdolId => Breach;

            public IdolTargeting Targeting => IdolTargeting.Aim;

            public bool Execute(IdolAbilityContext ctx, int level, GridCoordinate? aim)
            {
                if (!aim.HasValue) return false;
                var from = ctx.Trail.CurrentPosition;
                var (dr, dc) = Direction(from, aim.Value);
                if (dr == 0 && dc == 0) return false;

                var did = false;
                var length = level >= 3 ? BreachLineLength : 1; // ур. 3: линия из трёх клеток
                for (var i = 1; i <= length; i++)
                {
                    var c = new GridCoordinate(from.Row + dr * i, from.Column + dc * i);
                    if (!ctx.Grid.Contains(c) || !ctx.Grid.TryGetTile(c, out var tile)) continue;
                    if (tile.BreakWall()) did = true;
                    if (level >= 2 && i == 1 && tile.DefuseTrapTrigger()) did = true; // ур. 2: заодно обезвреживает триггер на соседней плите
                }

                return did;
            }
        }

        // ---- Время ----

        private sealed class TimeAbility : IIdolAbility
        {
            public string IdolId => Time;

            public IdolTargeting Targeting => IdolTargeting.Immediate;

            public bool Execute(IdolAbilityContext ctx, int level, GridCoordinate? aim)
            {
                ctx.Effects.StartSlow(SlowSeconds); // ловушки и распад вдвое медленнее
                if (level >= 2) RevealTypesAround(ctx); // ур. 2: во время замедления видны типы ловушек вокруг
                if (level >= 3) ctx.Effects.StartFreeze(FreezeSeconds); // ур. 3: полная остановка, игрок ходит
                return true;
            }

            private static void RevealTypesAround(IdolAbilityContext ctx)
            {
                var p = ctx.Trail.CurrentPosition;
                for (var r = p.Row - TimeRevealRadius; r <= p.Row + TimeRevealRadius; r++)
                    for (var c = 0; c < ctx.Grid.Width; c++)
                        if (ctx.Grid.TryGetTile(new GridCoordinate(r, c), out var tile) && tile.IsTrapTrigger) tile.RevealTrapType();
            }
        }

        // ---- Переход ----

        private sealed class PassageAbility : IIdolAbility
        {
            public string IdolId => Passage;

            public IdolTargeting Targeting => IdolTargeting.Aim;

            public bool Execute(IdolAbilityContext ctx, int level, GridCoordinate? aim)
            {
                if (!aim.HasValue) return false;
                var from = ctx.Trail.CurrentPosition;
                var radius = level >= 2 ? PassageRadiusL2 : PassageRadiusL1;
                if (Math.Max(Math.Abs(aim.Value.Row - from.Row), Math.Abs(aim.Value.Column - from.Column)) > radius) return false;
                if (!IsIntact(ctx.Grid, aim.Value)) return false; // целая видимая плита
                if (level < 2 && WallBetween(ctx, from, aim.Value)) return false; // ур. 1 — не сквозь стены, ур. 2 — сквозь

                if (!ctx.Trail.JumpTo(aim.Value)) return false;
                if (level >= 3) ctx.Effects.SuppressTrapsForSteps(1); // ур. 3: плита приземления и следующий шаг не активируют ловушки
                return true;
            }

            private static bool WallBetween(IdolAbilityContext ctx, GridCoordinate from, GridCoordinate to)
            {
                var steps = Math.Max(Math.Abs(to.Row - from.Row), Math.Abs(to.Column - from.Column));
                for (var i = 1; i < steps; i++)
                {
                    var r = from.Row + (int)Math.Round((to.Row - from.Row) * i / (double)steps);
                    var c = from.Column + (int)Math.Round((to.Column - from.Column) * i / (double)steps);
                    if (ctx.Grid.TryGetTile(new GridCoordinate(r, c), out var tile) && tile.IsBlocked) return true;
                }

                return false;
            }
        }
    }
}
