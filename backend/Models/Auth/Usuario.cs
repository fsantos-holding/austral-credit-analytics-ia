namespace AustralCreditAnalytics.Api.Models.Auth;

/// <summary>Codigos dos perfis de acesso (alinhados ao seed de seg.Perfil).</summary>
public static class Perfis
{
    public const string Gestor = "GESTOR";
    public const string Operador = "OPERADOR";

    public const int GestorId = 1;
    public const int OperadorId = 2;
}

/// <summary>Usuario interno persistido em seg.Usuario (uso no servidor; inclui o hash).</summary>
public class Usuario
{
    public long Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string NomeCompleto { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string SenhaHash { get; set; } = string.Empty;
    public int PerfilId { get; set; }
    public string PerfilCodigo { get; set; } = string.Empty;
    public bool Ativo { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime DataCriacaoUtc { get; set; }
    public string? CriadoPor { get; set; }
    public DateTime? UltimoLoginUtc { get; set; }
}

/// <summary>Projecao segura de usuario (sem hash) para listagens e /me.</summary>
public class UsuarioView
{
    public long Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string NomeCompleto { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int PerfilId { get; set; }
    public string PerfilCodigo { get; set; } = string.Empty;
    public string PerfilNome { get; set; } = string.Empty;
    public bool Ativo { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime DataCriacaoUtc { get; set; }
    public string? CriadoPor { get; set; }
    public DateTime? UltimoLoginUtc { get; set; }
}
