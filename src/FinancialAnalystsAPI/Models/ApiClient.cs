namespace FinancialAnalystsAPI.Models;

/// <summary>A caller holding an API key. Only a SHA-256 hash of the key is stored.</summary>
public class ApiClient
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Lower-case hex SHA-256 of the key.</summary>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>First characters of the key, so a client can be identified without the key itself.</summary>
    public string KeyPrefix { get; set; } = string.Empty;

    public ApiClientRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}

public enum ApiClientRole
{
    Reader,
    Admin,
}
