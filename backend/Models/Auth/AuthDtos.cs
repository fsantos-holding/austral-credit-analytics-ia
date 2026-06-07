using System.ComponentModel.DataAnnotations;

namespace AustralCreditAnalytics.Api.Models.Auth;

/// <summary>Credenciais de login. <see cref="LembrarMe"/> controla a validade do token.</summary>
public class LoginRequest
{
    [Required(ErrorMessage = "Informe o usuario.")]
    public string Login { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    public string Senha { get; set; } = string.Empty;

    /// <summary>Quando true, emite um token de validade longa (login lembrado no navegador).</summary>
    public bool LembrarMe { get; set; }
}

/// <summary>Resposta do login: token JWT, validade e dados do usuario autenticado.</summary>
public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiraEmUtc { get; set; }
    public UsuarioView Usuario { get; set; } = new();
}

/// <summary>Troca de senha do proprio usuario autenticado.</summary>
public class ChangePasswordRequest
{
    [Required(ErrorMessage = "Informe a senha atual.")]
    public string SenhaAtual { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a nova senha.")]
    [MinLength(8, ErrorMessage = "A nova senha deve ter ao menos 8 caracteres.")]
    public string NovaSenha { get; set; } = string.Empty;
}

/// <summary>Cadastro de um novo usuario (apenas gestor).</summary>
public class CreateUsuarioRequest
{
    [Required(ErrorMessage = "Informe o usuario (login).")]
    [MinLength(3, ErrorMessage = "O login deve ter ao menos 3 caracteres.")]
    public string Login { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o nome completo.")]
    public string NomeCompleto { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "E-mail invalido.")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Informe a senha inicial.")]
    [MinLength(8, ErrorMessage = "A senha deve ter ao menos 8 caracteres.")]
    public string Senha { get; set; } = string.Empty;

    /// <summary>Codigo do perfil: GESTOR ou OPERADOR.</summary>
    [Required(ErrorMessage = "Informe o perfil.")]
    public string PerfilCodigo { get; set; } = string.Empty;
}

/// <summary>Ativa/desativa um usuario (apenas gestor).</summary>
public class UpdateStatusRequest
{
    public bool Ativo { get; set; }
}

/// <summary>Reset de senha de um usuario por um gestor.</summary>
public class ResetPasswordRequest
{
    [Required(ErrorMessage = "Informe a nova senha.")]
    [MinLength(8, ErrorMessage = "A senha deve ter ao menos 8 caracteres.")]
    public string NovaSenha { get; set; } = string.Empty;
}
