using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Burmalda.DebugVisuals.Tests
{
    /// <summary>
    /// Задача «двойные флаги на плитах», п.4: "PNG перезаписывались поверх
    /// старых файлов, GUID сохранились, но несовпадение имени и содержимого —
    /// отдельный класс ошибки". Не гипотеза — уже случалось дважды в задаче
    /// «тёплый набор плит»: <c>tile-fresh-c.png</c> оказался побайтовым дублем
    /// стадии распада, а первая версия <c>tile-hidden-trap-signature.png</c> —
    /// дублем <c>tile-half-decayed.png</c>. Оба раза каталог был технически
    /// корректно подключён (GUID валиден, ссылка не null) — ошибка была в
    /// СОДЕРЖИМОМ файла, которое ни GUID, ни компиляция не проверяют.
    ///
    /// Этот тест грузит реальный <c>Assets/Resources/Art/TileArtCatalog.asset</c>
    /// и хэширует СЫРЫЕ БАЙТЫ PNG-файлов (не пиксели через Texture2D — тайлы
    /// импортированы как Default, не гарантированно Read/Write Enabled) через
    /// <c>AssetDatabase.GetAssetPath</c>: если два концептуально разных слота
    /// каталога указывают на файлы с ОДИНАКОВЫМ содержимым, это почти всегда
    /// ошибка копипаста/перезаписи, а не совпадение. Единственное намеренное
    /// исключение на момент написания теста — см. AllowedDuplicateContentPairs
    /// ниже. Владелец, 2026-09-05 «оставить только пять новых ловушек»: пара
    /// HiddenTrapSignature/TriggerSignature ушла вместе с самим TileArtKind.HiddenTrapSignature
    /// (яма/взрыв удалены из игры).
    /// </summary>
    public class TileArtCatalogTests
    {
        private TileArtCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<TileArtCatalog>("Assets/Resources/Art/TileArtCatalog.asset");
            Assert.IsNotNull(_catalog, "Assets/Resources/Art/TileArtCatalog.asset не найден — Editor-скрипт ArtIntegrationSetup ещё не запускался?");
        }

        // Каждый TileArtKind, для которого каталог ОБЯЗАН отдавать текстуру
        // (не None — см. TileArtKindResolver, часть состояний намеренно без
        // готового арта, напр. Boss до Комнаты Босса в Unity).
        private static readonly TileArtKind[] ExpectedWiredKinds =
        {
            TileArtKind.Fresh,
            TileArtKind.HalfDecayed,
            TileArtKind.AboutToDecay,
            TileArtKind.Destroyed,
            TileArtKind.Start,
            TileArtKind.Blocked,
            TileArtKind.Lava,
            TileArtKind.TimedTrapActive,
            TileArtKind.TriggerSignature,
            TileArtKind.CurrentPosition,
            TileArtKind.ManaSource,
            TileArtKind.KeySource,
            TileArtKind.Lever,
            TileArtKind.GateClosed,
            TileArtKind.GateOpen,
            TileArtKind.Altar,
        };

        [TestCaseSource(nameof(ExpectedWiredKinds))]
        public void Get_ExpectedKind_ReturnsNonNullTexture(TileArtKind kind)
        {
            Assert.IsNotNull(_catalog.Get(kind), $"TileArtCatalog.Get({kind}) вернул null — слот не заполнен ArtIntegrationSetup.");
        }

        // Задача «разрушение плиты»: CrackMaskTexture — НЕ TileArtKind (см.
        // её doc-комментарий в TileArtCatalog), отдельный аксессор, поэтому
        // отдельные тесты, а не запись в ExpectedWiredKinds.
        [Test]
        public void CrackMaskTexture_ReturnsNonNullTexture()
        {
            Assert.IsNotNull(_catalog.CrackMaskTexture, "TileArtCatalog.CrackMaskTexture вернул null — ArtIntegrationSetup.BuildTileArtCatalog не подключил tile-crack-mask.png?");
        }

        [Test]
        public void CrackMaskTexture_DoesNotShareContentWithAnyOtherCatalogSlot()
        {
            var crackMask = _catalog.CrackMaskTexture;
            if (crackMask == null) return; // уже отдельно проверено выше

            var crackMaskHash = HashAssetContent(crackMask);
            foreach (var kind in ExpectedWiredKinds)
            {
                var texture = _catalog.Get(kind);
                if (texture == null) continue;

                Assert.AreNotEqual(crackMaskHash, HashAssetContent(texture),
                    $"tile-crack-mask.png дублирует содержимое слота '{kind}' — маска трещин должна быть отдельным производным файлом, не переиспользованной стадией распада как есть.");
            }
        }

        [Test]
        public void GetFreshVariant_BothIndices_ReturnNonNullTexture()
        {
            Assert.IsNotNull(_catalog.GetFreshVariant(0), "GetFreshVariant(0) — tile-fresh.png");
            Assert.IsNotNull(_catalog.GetFreshVariant(1), "GetFreshVariant(1) — tile-fresh-b.png");
        }

        // Пары слотов, которым РАЗРЕШЕНО указывать на один и тот же файл —
        // единственное известное намеренное совпадение на момент написания
        // теста (см. класс-докстринг). Любая другая пара с совпадающим
        // содержимым — предположительно ошибка, тест обязан её найти.
        private static readonly (TileArtKind A, TileArtKind B)[] AllowedDuplicateContentPairs =
        {
            // tile-start.png не вошла в тёплый лист — владелец прямо
            // попросил временно использовать tile-fresh.png (см.
            // ArtIntegrationSetup.BuildTileArtCatalog) до отдельной
            // генерации. Намеренный дубль, не ошибка копипаста.
            (TileArtKind.Fresh, TileArtKind.Start),
        };

        [Test]
        public void DistinctCatalogSlots_DoNotShareIdenticalFileContent_ExceptKnownIntentionalPairs()
        {
            var hashToKinds = new Dictionary<string, List<TileArtKind>>();
            foreach (var kind in ExpectedWiredKinds)
            {
                var texture = _catalog.Get(kind);
                if (texture == null) continue; // уже отдельно проверено выше — здесь не дублировать ошибку

                var hash = HashAssetContent(texture);
                if (!hashToKinds.TryGetValue(hash, out var kinds))
                {
                    kinds = new List<TileArtKind>();
                    hashToKinds[hash] = kinds;
                }
                kinds.Add(kind);
            }

            foreach (var kinds in hashToKinds.Values)
            {
                if (kinds.Count < 2) continue;

                for (var i = 0; i < kinds.Count; i++)
                for (var j = i + 1; j < kinds.Count; j++)
                {
                    var isAllowed = false;
                    foreach (var pair in AllowedDuplicateContentPairs)
                        if ((pair.A == kinds[i] && pair.B == kinds[j]) || (pair.A == kinds[j] && pair.B == kinds[i]))
                            isAllowed = true;

                    Assert.IsTrue(isAllowed,
                        $"TileArtCatalog: '{kinds[i]}' и '{kinds[j]}' указывают на файлы с одинаковым содержимым " +
                        $"('{AssetDatabase.GetAssetPath(_catalog.Get(kinds[i]))}' и '{AssetDatabase.GetAssetPath(_catalog.Get(kinds[j]))}') " +
                        "— похоже на ошибку копипаста/перезаписи PNG (см. класс-докстринг), не в списке намеренных совпадений.");
                }
            }
        }

        [Test]
        public void FreshVariants_DoNotShareIdenticalFileContent_WithEachOtherOrAnyOtherSlot()
        {
            var freshA = _catalog.GetFreshVariant(0);
            var freshB = _catalog.GetFreshVariant(1);
            if (freshA == null || freshB == null) return; // уже отдельно проверено выше

            Assert.AreNotEqual(HashAssetContent(freshA), HashAssetContent(freshB),
                "tile-fresh.png и tile-fresh-b.png — одинаковое содержимое, второй вариант перестаёт быть разнообразием пола.");

            var freshAHash = HashAssetContent(freshA);
            var freshBHash = HashAssetContent(freshB);
            foreach (var kind in ExpectedWiredKinds)
            {
                // Fresh — сам GetFreshVariant(0) (сравнение с собой же).
                // Start намеренно = Fresh (см. AllowedDuplicateContentPairs) —
                // оба сравнения с tile-fresh.png под другим именем слота бессмысленны.
                if (kind == TileArtKind.Fresh || kind == TileArtKind.Start) continue;

                var texture = _catalog.Get(kind);
                if (texture == null) continue;

                var hash = HashAssetContent(texture);
                Assert.AreNotEqual(freshAHash, hash, $"tile-fresh.png дублирует содержимое слота '{kind}'.");
                Assert.AreNotEqual(freshBHash, hash, $"tile-fresh-b.png дублирует содержимое слота '{kind}'.");
            }
        }

        private static string HashAssetContent(Texture2D texture)
        {
            var path = AssetDatabase.GetAssetPath(texture);
            Assert.IsFalse(string.IsNullOrEmpty(path), $"AssetDatabase.GetAssetPath не нашёл путь для текстуры '{texture.name}' — не сохранённый на диске ассет?");

            var bytes = File.ReadAllBytes(path);
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(bytes);
            return System.Convert.ToBase64String(hash);
        }
    }
}
