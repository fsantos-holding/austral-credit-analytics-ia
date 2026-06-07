using System.ComponentModel.DataAnnotations;

namespace AustralCreditAnalytics.Api.Models;

/// <summary>
/// Dados de conexao informados pela tela de Configuracoes.
/// Espelha os campos do formulario em index.html (view-config).
/// A validacao usa DataAnnotations (mensagens em PT-BR) + IValidatableObject
/// para a regra condicional de autenticacao SQL.
/// </summary>
public class ConnectionConfig : IValidatableObject
{
    [Required(ErrorMessage = "Informe o endereco do servidor SQL Server.")]
    public string Server { get; set; } = string.Empty;

    public string? Port { get; set; } = "1433";

    [Required(ErrorMessage = "Informe o nome do banco de dados de destino.")]
    public string Database { get; set; } = string.Empty;

    /// <summary>"sql" (SQL Server Authentication) ou "windows" (Trusted Connection).</summary>
    public string AuthMode { get; set; } = "sql";

    public string? User { get; set; }

    public string? Password { get; set; }

    public bool Encrypt { get; set; } = true;

    /// <summary>
    /// Por padrao valida o certificado do servidor (false). Habilite apenas em
    /// ambientes com certificado autoassinado conhecido, pela tela de Configuracoes.
    /// </summary>
    public bool TrustServerCertificate { get; set; } = false;

    /// <summary>Timeout de conexao em segundos.</summary>
    public int ConnectionTimeout { get; set; } = 30;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var isWindowsAuth = string.Equals(AuthMode, "windows", StringComparison.OrdinalIgnoreCase);
        if (!isWindowsAuth && (string.IsNullOrWhiteSpace(User) || string.IsNullOrEmpty(Password)))
        {
            yield return new ValidationResult(
                "Usuario e senha sao obrigatorios na autenticacao SQL Server.",
                new[] { nameof(User), nameof(Password) });
        }
    }
}
