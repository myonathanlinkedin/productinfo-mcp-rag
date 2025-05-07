﻿using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

public class JwtGeneratorService : IJwtGenerator
{
    private readonly UserManager<User> userManager;
    private readonly IRsaKeyProvider keyProvider;
    private readonly string audience;
    private readonly string issuer;
    private readonly int tokenExpirationSeconds;
    private readonly int refreshTokenExpirationDays;

    private const string RsaAlgorithm = SecurityAlgorithms.RsaSha256Signature;
    private readonly Dictionary<string, (string RefreshToken, DateTime Expiry)> refreshTokenStore = new();

    public JwtGeneratorService(UserManager<User> userManager, ApplicationSettings appSettings, IRsaKeyProvider keyProvider)
    {
        this.userManager = userManager;
        this.keyProvider = keyProvider;
        this.audience = appSettings.Audience;
        this.issuer = appSettings.Issuer;
        this.tokenExpirationSeconds = appSettings.TokenExpirationSeconds;
    }

    public async Task<(string AccessToken, string RefreshToken)> GenerateToken(User user)
    {
        var rsa = keyProvider.GetPrivateKey();
        var tokenHandler = new JwtSecurityTokenHandler();

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.Email)
            }),
            Expires = DateTime.UtcNow.AddSeconds(tokenExpirationSeconds),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(rsa), RsaAlgorithm)
        };

        if (await userManager.IsInRoleAsync(user, "Administrator"))
        {
            tokenDescriptor.Subject.AddClaim(new Claim(ClaimTypes.Role, "Administrator"));
        }

        var accessToken = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
        var refreshToken = GenerateRefreshToken();

        refreshTokenStore[user.Id] = (refreshToken, DateTime.UtcNow.AddDays(refreshTokenExpirationDays));

        return (accessToken, refreshToken);
    }

    public async Task<string> RefreshToken(string userId, string providedRefreshToken)
    {
        if (!refreshTokenStore.TryGetValue(userId, out var storedToken) ||
            storedToken.RefreshToken != providedRefreshToken ||
            storedToken.Expiry < DateTime.UtcNow)
        {
            throw new SecurityTokenException("Invalid or expired refresh token");
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user == null) throw new SecurityTokenException("User not found");

        return (await GenerateToken(user)).AccessToken;
    }

    private string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public JsonWebKey GetPublicKey() => keyProvider.GetPublicJwk();
}
