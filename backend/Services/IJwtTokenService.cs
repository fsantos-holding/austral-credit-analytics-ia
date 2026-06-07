using AustralCreditAnalytics.Api.Models.Auth;
using Microsoft.IdentityModel.Tokens;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>Emite tokens JWT para usuarios autenticados e expoe os parametros de validacao.</summary>
public interface IJwtTokenService
{
    /// <summary>Gera um JWT para o usuario. <paramref name="lembrarMe"/> usa a validade longa.</summary>
    (string Token, DateTime ExpiraEmUtc) Generate(Usuario usuario, bool lembrarMe);

    /// <summary>Parametros usados pelo middleware JwtBearer para validar os tokens emitidos.</summary>
    TokenValidationParameters GetValidationParameters();
}
