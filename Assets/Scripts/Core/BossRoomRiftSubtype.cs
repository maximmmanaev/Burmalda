namespace Burmalda.Core
{
    /// <summary>
    /// Подтип плиты-Разлома (PRD v8 §8.2): «подтип Жилы даёт Ману, подтип
    /// Резонанса даёт +0.1 множителя в секунду». Осмысленно только когда
    /// <see cref="Tile.BossRoomTile"/> == <see cref="BossRoomTileKind.Rift"/>.
    /// </summary>
    public enum BossRoomRiftSubtype
    {
        Vein,
        Resonance
    }
}
