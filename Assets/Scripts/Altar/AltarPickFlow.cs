using System;
using System.Collections.Generic;
using Burmalda.Artifacts;
using Burmalda.Core;

namespace Burmalda.Altar
{
    /// <summary>Что Алтарь должен узнать о забеге, чтобы выбрать предложение (даёт композиционный корень).</summary>
    public interface IAltarContext
    {
        /// <summary>Какой это Алтарь: Ярус и номер (1, 2 — перед Святилищем Яруса; 3 — в конце Святилища).</summary>
        bool TryGetSpot(GridCoordinate coordinate, out int yarus, out int ordinal);

        /// <summary>Типы угроз Святилища Яруса — то, что показывает Афиша.</summary>
        IReadOnlyCollection<ThreatType> AfishaThreats(int shrineYarus);

        /// <summary>Сид похода.</summary>
        int RunSeed { get; }

        /// <summary>Ярус под игроком (Святилище, из которого берут Саркофаг, §21.1).</summary>
        int CurrentYarus { get; }
    }

    /// <summary>
    /// Афиша Алтаря (PRD v10 §13.3): знак бога ближайшего Святилища и типы его ловушек — без текста. Типы считаются
    /// по раскладке Святилища; раскладки Ярусов 2–10 пока заглушка (<see cref="YarusGod.HasOwnLayout"/>).
    /// </summary>
    public sealed class AltarAfisha
    {
        public AltarAfisha(YarusGod shrine, IReadOnlyCollection<ThreatType> threats)
        {
            Shrine = shrine;
            Threats = threats;
        }

        /// <summary>Бог, ранг и мифология ближайшего Святилища.</summary>
        public YarusGod Shrine { get; }

        /// <summary>Типы ловушек Святилища.</summary>
        public IReadOnlyCollection<ThreatType> Threats { get; }
    }

    /// <summary>
    /// Достижение Алтаря → сессия выбора «1 из 2» (PRD v10 §13): определяет Ярус и ближайшее Святилище впереди,
    /// собирает запрос для <see cref="AltarOfferGenerator"/>, учитывает бонус тайника и гарантию защитного Амулета.
    /// Последний Алтарь после Яруса 10 выбора не даёт (там единственный выбор «Вернуться в Лагерь», §6.3).
    /// </summary>
    public sealed class AltarPickFlow
    {
        public const int LastYarus = 10;

        private readonly ArtifactPool _pool;
        private readonly RunRelicBuild _build;
        private readonly VaultBonusTracker _vault;
        private readonly IAltarContext _context;
        private readonly HashSet<int> _yarusesWithProtectiveOffer = new HashSet<int>();

        public AltarPickFlow(ArtifactPool pool, RunRelicBuild build, VaultBonusTracker vault, IAltarContext context)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _build = build ?? throw new ArgumentNullException(nameof(build));
            _vault = vault;
            _context = context;
        }

        private void Resolve(GridCoordinate coordinate, out int yarus, out int ordinal, out int shrineYarus)
        {
            yarus = 1;
            ordinal = 1;
            if (_context != null && _context.TryGetSpot(coordinate, out var y, out var o))
            {
                yarus = y;
                ordinal = o;
            }

            shrineYarus = ordinal == 3 ? yarus + 1 : yarus; // Алтарь в конце Святилища смотрит на Святилище следующего Яруса
        }

        /// <summary>Афиша этого Алтаря; null у последнего Алтаря после Яруса 10 (впереди Святилищ нет).</summary>
        public AltarAfisha AfishaFor(GridCoordinate coordinate)
        {
            Resolve(coordinate, out _, out _, out var shrineYarus);
            if (shrineYarus > LastYarus) return null;
            return new AltarAfisha(GodTable.ForYarus(shrineYarus), _context?.AfishaThreats(shrineYarus) ?? Array.Empty<ThreatType>());
        }

        /// <summary>Сессия выбора для этого Алтаря; null, если выбирать нечего (нет открытых Реликвий / финальный Алтарь).</summary>
        public AltarPickSession OnAltarReached(GridCoordinate coordinate)
        {
            Resolve(coordinate, out var yarus, out var ordinal, out var shrineYarus);
            if (shrineYarus > LastYarus) return null;

            var request = new AltarOfferRequest
            {
                ShrineYarus = shrineYarus,
                IsLastAltarBeforeShrine = ordinal == 2,
                ProtectiveAmuletAlreadyOffered = _yarusesWithProtectiveOffer.Contains(yarus),
                AfishaThreats = _context?.AfishaThreats(shrineYarus) ?? Array.Empty<ThreatType>(),
                VaultBonus = _vault != null && _vault.PendingExtraOptions > 0,
                IsOpened = _pool.IsUnlocked,
                Build = _build,
                Seed = unchecked((_context?.RunSeed ?? 0) * 31 + yarus * 10 + ordinal),
            };

            var offer = AltarOfferGenerator.Generate(request);
            if (offer.Options.Count == 0) return null;

            _vault?.Consume();
            if (offer.ContainsProtectiveAmulet) _yarusesWithProtectiveOffer.Add(yarus);
            return new AltarPickSession(offer, _build, yarus);
        }
    }
}
