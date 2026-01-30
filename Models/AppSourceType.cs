namespace Mobius.Models
{
    /// <summary>
    /// Источник приложения (откуда оно получено/обнаружено).
    /// ВАЖНО: этот enum должен существовать в проекте ТОЛЬКО ОДИН РАЗ,
    /// иначе будет CS0101.
    /// </summary>
    public enum AppSourceType
    {
        Unknown = 0,
        Steam = 1,
        Manual = 2
    }
}
