using System;
using System.Linq;
using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Currencies.Tests
{
    /// <summary>Ключи удалены целиком (PRD v10-3): валюта забега одна — Мана. Проверка на уровне типов.</summary>
    public class NoKeysTests
    {
        [Test]
        public void CurrencyController_HasNoRunKeys()
        {
            Assert.IsNull(typeof(CurrencyController).GetProperty("RunKeys"));
            Assert.IsNull(typeof(CurrencyController).GetField("KeysPerSource"));
        }

        [Test]
        public void Tile_HasNoKeySource()
        {
            Assert.IsNull(typeof(Tile).GetProperty("IsKeySource"));
            Assert.IsNull(typeof(Tile).GetMethod("MarkKeySource"));
            Assert.IsNotNull(typeof(Tile).GetProperty("IsGateVault"), "тайник за Воротами остаётся ролью плиты");
        }

        [Test]
        public void NoPublicEnumOrTypeInGameAssemblies_NamedKeys()
        {
            var offenders = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name.StartsWith("Burmalda.") && !a.GetName().Name.EndsWith(".Tests"))
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsEnum && t.GetEnumNames().Any(n => n == "Keys" || n == "KeySource" || n == "KeyBond"))
                .Select(t => t.FullName)
                .ToList();

            CollectionAssert.IsEmpty(offenders);
        }
    }
}
