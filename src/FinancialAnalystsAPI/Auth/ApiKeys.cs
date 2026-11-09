using System.Security.Cryptography;
using System.Text;
using FinancialAnalystsAPI.Data;
using FinancialAnalystsAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace FinancialAnalystsAPI.Auth;

public static class ApiKeys
{
    public const string HeaderName = "X-Api-Key";
    public const string Scheme = "ApiKey";
    public const string AdminPolicy = "Admin";
    private const string Prefix = "fa_";

    /// <summary>A new random key: "fa_" plus 32 random bytes, base64url-encoded.</summary>
    public static string Generate() =>
        Prefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string key) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    public static string DisplayPrefix(string key) => key[..Math.Min(10, key.Length)];
}

/// <summary>Creates, lists and revokes clients. Used by the <c>apikey</c> command.</summary>
public sealed class ApiClientStore(AnalystsDbContext db, TimeProvider time)
{
    /// <returns>The new key. It is shown once and only its hash is stored.</returns>
    public async Task<string> CreateAsync(string name, ApiClientRole role, CancellationToken ct)
    {
        if (await db.ApiClients.AnyAsync(c => c.Name == name, ct))
            throw new InvalidOperationException($"A client named '{name}' already exists.");

        var key = ApiKeys.Generate();
        db.ApiClients.Add(new ApiClient
        {
            Name = name,
            KeyHash = ApiKeys.Hash(key),
            KeyPrefix = ApiKeys.DisplayPrefix(key),
            Role = role,
            CreatedAtUtc = time.GetUtcNow().UtcDateTime,
        });
        await db.SaveChangesAsync(ct);
        return key;
    }

    public Task<List<ApiClient>> ListAsync(CancellationToken ct) =>
        db.ApiClients.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);

    public async Task<bool> RevokeAsync(string name, CancellationToken ct)
    {
        var client = await db.ApiClients.SingleOrDefaultAsync(c => c.Name == name && c.IsActive, ct);
        if (client is null) return false;
        client.IsActive = false;
        client.RevokedAtUtc = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return true;
    }
}
