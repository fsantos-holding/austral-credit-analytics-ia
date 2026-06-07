using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AustralCreditAnalytics.Api.Models.Auth;
using Microsoft.IdentityModel.Tokens;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Emissao/validacao de JWT (HMAC-SHA256). A chave de assinatura vem de <c>Auth:JwtKey</c>
/// no appsettings; se ausente, e gerada e persistida em <c>App_Data/jwt.key</c> para
/// sobreviver a reinicializacoes (mesmo padrao das chaves de Data Protection).
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private const string Issuer = "AustralCreditAnalytics";
    private const string Audience = "AustralCreditAnalytics";

    private readonly SymmetricSecurityKey _key;
    private readonly int _accessTokenMinutes;
    private readonly int _rememberMeDays;

    public JwtTokenService(IConfiguration configuration, IWebHostEnvironment env)
    {
        _accessTokenMinutes = configuration.GetValue<int?>("Auth:AccessTokenMinutes") ?? 480;   // 8h
        _rememberMeDays = configuration.GetValue<int?>("Auth:RememberMeDays") ?? 30;             // 30 dias

        var keyMaterial = ResolveKeyMaterial(configuration, env);
        _key = new SymmetricSecurityKey(keyMaterial);
    }

    // Tipos de claim curtos e estaveis (evita o remapeamento URI dos handlers). Devem
    // casar com NameClaimType/RoleClaimType em GetValidationParameters e com a leitura
    // de claims nos controllers (sub/login/role).
    public const string SubClaim = "sub";
    public const string NameClaim = "name";
    public const string LoginClaim = "login";
    public const string RoleClaim = "role";

    public (string Token, DateTime ExpiraEmUtc) Generate(Usuario usuario, bool lembrarMe)
    {
        var now = DateTime.UtcNow;
        var expira = lembrarMe ? now.AddDays(_rememberMeDays) : now.AddMinutes(_accessTokenMinutes);

        var claims = new List<Claim>
        {
            new(SubClaim, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(NameClaim, usuario.NomeCompleto),
            new(LoginClaim, usuario.Login),
            new(RoleClaim, usuario.PerfilCodigo),
        };

        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: now,
            expires: expira,
            signingCredentials: creds);

        // Sem remapeamento de saida: os tipos das claims sao gravados exatamente como acima.
        var handler = new JwtSecurityTokenHandler();
        handler.OutboundClaimTypeMap.Clear();
        var jwt = handler.WriteToken(token);
        return (jwt, expira);
    }

    public TokenValidationParameters GetValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = _key,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        RoleClaimType = RoleClaim,
        NameClaimType = NameClaim,
    };

    private static byte[] ResolveKeyMaterial(IConfiguration configuration, IWebHostEnvironment env)
    {
        var configured = configuration["Auth:JwtKey"];
        if (!string.IsNullOrWhiteSpace(configured))
            return Encoding.UTF8.GetBytes(configured);

        // Sem chave no appsettings: gera/persiste uma chave aleatoria em App_Data/jwt.key.
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        var keyPath = Path.Combine(dataDir, "jwt.key");

        if (File.Exists(keyPath))
        {
            var existing = File.ReadAllText(keyPath).Trim();
            if (!string.IsNullOrWhiteSpace(existing))
            {
                try { return Convert.FromBase64String(existing); }
                catch (FormatException) { /* arquivo corrompido: regenera abaixo */ }
            }
        }

        var generated = RandomNumberGenerator.GetBytes(64);
        File.WriteAllText(keyPath, Convert.ToBase64String(generated));
        return generated;
    }
}
