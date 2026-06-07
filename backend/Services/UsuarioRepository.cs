using Dapper;
using AustralCreditAnalytics.Api.Models.Auth;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Persistencia dos usuarios em seg.Usuario (Dapper sobre o canal gravavel
/// <see cref="ISqlConnectionFactory.CreateWritable"/>). Comandos parametrizados.
/// </summary>
public class UsuarioRepository : IUsuarioRepository
{
    private const int CommandTimeout = 30;

    private const string SelectFull = """
        SELECT u.Id, u.Login, u.NomeCompleto, u.Email, u.SenhaHash, u.PerfilId,
               p.Codigo AS PerfilCodigo, u.Ativo, u.MustChangePassword,
               u.DataCriacaoUtc, u.CriadoPor, u.UltimoLoginUtc
        FROM seg.Usuario u
        JOIN seg.Perfil p ON p.Id = u.PerfilId
        """;

    private const string SelectView = """
        SELECT u.Id, u.Login, u.NomeCompleto, u.Email, u.PerfilId,
               p.Codigo AS PerfilCodigo, p.Nome AS PerfilNome, u.Ativo, u.MustChangePassword,
               u.DataCriacaoUtc, u.CriadoPor, u.UltimoLoginUtc
        FROM seg.Usuario u
        JOIN seg.Perfil p ON p.Id = u.PerfilId
        """;

    private readonly ISqlConnectionFactory _factory;

    public UsuarioRepository(ISqlConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<Usuario?> GetByLoginAsync(string login, CancellationToken ct = default)
    {
        await using var connection = _factory.CreateWritable();
        return await connection.QuerySingleOrDefaultAsync<Usuario>(new CommandDefinition(
            SelectFull + " WHERE u.Login = @login",
            new { login }, commandTimeout: CommandTimeout, cancellationToken: ct));
    }

    public async Task<UsuarioView?> GetViewByIdAsync(long id, CancellationToken ct = default)
    {
        await using var connection = _factory.CreateWritable();
        return await connection.QuerySingleOrDefaultAsync<UsuarioView>(new CommandDefinition(
            SelectView + " WHERE u.Id = @id",
            new { id }, commandTimeout: CommandTimeout, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<UsuarioView>> ListAsync(CancellationToken ct = default)
    {
        await using var connection = _factory.CreateWritable();
        var rows = await connection.QueryAsync<UsuarioView>(new CommandDefinition(
            SelectView + " ORDER BY u.NomeCompleto",
            commandTimeout: CommandTimeout, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<bool> LoginExistsAsync(string login, CancellationToken ct = default)
    {
        await using var connection = _factory.CreateWritable();
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM seg.Usuario WHERE Login = @login",
            new { login }, commandTimeout: CommandTimeout, cancellationToken: ct));
        return count > 0;
    }

    public async Task<long> CreateAsync(string login, string nomeCompleto, string? email, string senhaHash,
        int perfilId, bool mustChangePassword, string? criadoPor, CancellationToken ct = default)
    {
        await using var connection = _factory.CreateWritable();
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO seg.Usuario (Login, NomeCompleto, Email, SenhaHash, PerfilId, Ativo, MustChangePassword, CriadoPor)
            OUTPUT inserted.Id
            VALUES (@login, @nomeCompleto, @email, @senhaHash, @perfilId, 1, @mustChangePassword, @criadoPor)
            """,
            new { login, nomeCompleto, email, senhaHash, perfilId, mustChangePassword, criadoPor },
            commandTimeout: CommandTimeout, cancellationToken: ct));
    }

    public async Task<bool> SetStatusAsync(long id, bool ativo, CancellationToken ct = default)
    {
        await using var connection = _factory.CreateWritable();
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE seg.Usuario SET Ativo = @ativo WHERE Id = @id",
            new { id, ativo }, commandTimeout: CommandTimeout, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> UpdatePasswordAsync(long id, string senhaHash, bool mustChangePassword, CancellationToken ct = default)
    {
        await using var connection = _factory.CreateWritable();
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE seg.Usuario SET SenhaHash = @senhaHash, MustChangePassword = @mustChangePassword WHERE Id = @id",
            new { id, senhaHash, mustChangePassword }, commandTimeout: CommandTimeout, cancellationToken: ct));
        return rows > 0;
    }

    public async Task UpdateLastLoginAsync(long id, CancellationToken ct = default)
    {
        await using var connection = _factory.CreateWritable();
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE seg.Usuario SET UltimoLoginUtc = SYSUTCDATETIME() WHERE Id = @id",
            new { id }, commandTimeout: CommandTimeout, cancellationToken: ct));
    }

    public async Task<int?> GetPerfilIdByCodigoAsync(string codigo, CancellationToken ct = default)
    {
        await using var connection = _factory.CreateWritable();
        return await connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT Id FROM seg.Perfil WHERE Codigo = @codigo",
            new { codigo }, commandTimeout: CommandTimeout, cancellationToken: ct));
    }
}
