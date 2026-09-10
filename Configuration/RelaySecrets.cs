namespace TelegramRelay.Configuration;

/// <summary>
/// Normalizes relay secrets and treats Docker/appsettings placeholders as unset.
/// </summary>
public static class RelaySecrets
{
    public static string Normalize(string? value) => value?.Trim() ?? string.Empty;

    public static bool IsConfigured(string? value)
    {
        string normalized = Normalize(value);
        return normalized.Length > 0 &&
               !normalized.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);
    }
}
