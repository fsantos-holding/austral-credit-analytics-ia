using Dapper;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Implementacao de <see cref="IDfpImportacaoLog"/> sobre o canal gravavel. Tolerante a
/// falha: erros sao logados e nunca interrompem a importacao em si (o rastreio e
/// secundario ao fluxo de carga).
/// </summary>
public class DfpImportacaoLog : IDfpImportacaoLog
{
    private const int CommandTimeout = 30;

    private readonly ISqlConnectionFactory _factory;
    private readonly ILogger<DfpImportacaoLog> _logger;

    public DfpImportacaoLog(ISqlConnectionFactory factory, ILogger<DfpImportacaoLog> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<long?> IniciarAsync(
        string tipo, string tabela, string? conjunto, int? ano, string arquivo, string? usuario,
        CancellationToken ct = default)
    {
        try
        {
            await using var connection = _factory.CreateWritable();
            var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                INSERT INTO dfp.Importacao (Tipo, Tabela, Conjunto, Ano, Arquivo, Usuario, Status)
                OUTPUT INSERTED.Id
                VALUES (@tipo, @tabela, @conjunto, @ano, @arquivo, @usuario, 'Processando');
                """,
                new
                {
                    tipo = Truncate(tipo, 30),
                    tabela = Truncate(tabela, 60),
                    conjunto,
                    ano = (short?)ano,
                    arquivo = Truncate(arquivo, 260),
                    usuario = Truncate(usuario, 128),
                },
                commandTimeout: CommandTimeout, cancellationToken: ct));
            return id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao abrir o registro de importacao DFP ({Tipo}/{Arquivo}).", tipo, arquivo);
            return null;
        }
    }

    public async Task AtualizarAsync(
        long id, int linhasRemovidas, int linhasLidas, int linhasImportadas,
        CancellationToken ct = default)
    {
        try
        {
            await using var connection = _factory.CreateWritable();
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE dfp.Importacao
                   SET LinhasRemovidas = @linhasRemovidas,
                       LinhasLidas = @linhasLidas,
                       LinhasImportadas = @linhasImportadas
                 WHERE Id = @id
                """,
                new { id, linhasRemovidas, linhasLidas, linhasImportadas },
                commandTimeout: CommandTimeout, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao atualizar o registro de importacao DFP {Id}.", id);
        }
    }

    public async Task FinalizarAsync(
        long id, string status, string? mensagem,
        int linhasRemovidas, int linhasLidas, int linhasImportadas, string? conjunto,
        CancellationToken ct = default)
    {
        try
        {
            await using var connection = _factory.CreateWritable();
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE dfp.Importacao
                   SET Status = @status,
                       Mensagem = @mensagem,
                       LinhasRemovidas = @linhasRemovidas,
                       LinhasLidas = @linhasLidas,
                       LinhasImportadas = @linhasImportadas,
                       Conjunto = COALESCE(@conjunto, Conjunto),
                       ConcluidoEmUtc = SYSUTCDATETIME()
                 WHERE Id = @id
                """,
                new
                {
                    id,
                    status = Truncate(status, 20),
                    mensagem = Truncate(mensagem, 1000),
                    linhasRemovidas,
                    linhasLidas,
                    linhasImportadas,
                    conjunto,
                },
                commandTimeout: CommandTimeout, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao finalizar o registro de importacao DFP {Id}.", id);
        }
    }

    private static string? Truncate(string? value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
