using Dapper;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Grava o registro de atividades em seg.LogAtividade via canal gravavel. Tolerante a
/// falha: erros sao logados no ILogger e nunca interrompem o fluxo de negocio.
/// </summary>
public class ActivityLogger : IActivityLogger
{
    private const int CommandTimeout = 30;

    private readonly ISqlConnectionFactory _factory;
    private readonly ILogger<ActivityLogger> _logger;

    public ActivityLogger(ISqlConnectionFactory factory, ILogger<ActivityLogger> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task LogAsync(
        long? usuarioId,
        string? login,
        string acao,
        string? entidade = null,
        string? detalhe = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default)
    {
        try
        {
            await using var connection = _factory.CreateWritable();
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO seg.LogAtividade (UsuarioId, Login, Acao, Entidade, Detalhe, IpAddress, UserAgent)
                VALUES (@usuarioId, @login, @acao, @entidade, @detalhe, @ipAddress, @userAgent)
                """,
                new
                {
                    usuarioId,
                    login = Truncate(login, 60),
                    acao = Truncate(acao, 40),
                    entidade = Truncate(entidade, 60),
                    detalhe,
                    ipAddress = Truncate(ipAddress, 60),
                    userAgent = Truncate(userAgent, 400),
                },
                commandTimeout: CommandTimeout, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao registrar atividade ({Acao}) do usuario {Login}.", acao, login);
        }
    }

    private static string? Truncate(string? value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
