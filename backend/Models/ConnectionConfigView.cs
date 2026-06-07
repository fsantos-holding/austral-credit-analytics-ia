namespace AustralCreditAnalytics.Api.Models;

/// <summary>
/// Projecao retornada por GET /api/config: nunca expoe a senha em texto plano.
/// </summary>
public class ConnectionConfigView
{
    public string Server { get; set; } = string.Empty;
    public string? Port { get; set; }
    public string Database { get; set; } = string.Empty;
    public string AuthMode { get; set; } = "sql";
    public string? User { get; set; }
    /// <summary>Senha mascarada (ex: "************") apenas para exibicao.</summary>
    public string? PasswordMask { get; set; }
    public bool Encrypt { get; set; } = true;
    public bool TrustServerCertificate { get; set; } = true;
    public int ConnectionTimeout { get; set; } = 30;
    public bool IsConfigured { get; set; }
}
