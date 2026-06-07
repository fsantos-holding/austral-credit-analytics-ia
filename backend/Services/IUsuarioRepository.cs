using AustralCreditAnalytics.Api.Models.Auth;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>Persistencia dos usuarios internos (schema [seg]) via Dapper.</summary>
public interface IUsuarioRepository
{
    Task<Usuario?> GetByLoginAsync(string login, CancellationToken ct = default);
    Task<UsuarioView?> GetViewByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<UsuarioView>> ListAsync(CancellationToken ct = default);
    Task<bool> LoginExistsAsync(string login, CancellationToken ct = default);

    /// <summary>Cria o usuario e retorna o Id gerado.</summary>
    Task<long> CreateAsync(string login, string nomeCompleto, string? email, string senhaHash,
        int perfilId, bool mustChangePassword, string? criadoPor, CancellationToken ct = default);

    Task<bool> SetStatusAsync(long id, bool ativo, CancellationToken ct = default);

    /// <summary>Atualiza o hash da senha e ajusta a flag de troca obrigatoria.</summary>
    Task<bool> UpdatePasswordAsync(long id, string senhaHash, bool mustChangePassword, CancellationToken ct = default);

    Task UpdateLastLoginAsync(long id, CancellationToken ct = default);

    Task<int?> GetPerfilIdByCodigoAsync(string codigo, CancellationToken ct = default);
}
