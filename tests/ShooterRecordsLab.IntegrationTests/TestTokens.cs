using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace ShooterRecordsLab.IntegrationTests;

internal static class TestTokens
{
    public const string Issuer = "ShooterRecordsLab.OfflineTests";
    public const string Audience = "ShooterRecordsLab.Api.OfflineTests";
    public static string KeyBase64 { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public static string Create(string scope = "matches.write", string? issuer = null, string? audience = null,
        bool wrongKey = false, bool expired = false, bool future = false, string algorithm = SecurityAlgorithms.HmacSha256)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(issuer ?? Issuer, audience ?? Audience,
            [new Claim("scope", scope)],
            notBefore: expired ? now.AddMinutes(-20) : future ? now.AddMinutes(10) : now.AddMinutes(-1),
            expires: expired ? now.AddMinutes(-10) : now.AddMinutes(20),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(wrongKey ? RandomNumberGenerator.GetBytes(64) : Convert.FromBase64String(KeyBase64)), algorithm));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static void Authorize(HttpClient client) => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Create());
}
