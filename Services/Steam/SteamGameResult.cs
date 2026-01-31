namespace Mobius.Services.Steam
{
    /// <summary>
    /// Результат поиска по Steam Store.
    /// </summary>
    public sealed class SteamGameResult
    {
        public int AppId { get; set; }
        public string Name { get; set; }

        // Удобно для биндинга/отладки
        public override string ToString() => $"{Name} ({AppId})";
    }
}
