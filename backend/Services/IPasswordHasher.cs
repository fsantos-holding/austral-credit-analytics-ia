namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Geracao e verificacao de hash de senha (PBKDF2-HMACSHA256) no formato
/// <c>iteracoes.saltBase64.hashBase64</c>.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string storedHash);
}
