using Dapper;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Implementacao de <see cref="ICvmImportacaoLog"/> sobre o canal gravavel, parametrizada
/// pela tabela do ledger (dfp.Importacao / itr.Importacao). Tolerante a falha: erros sao
/// logados e nunca interrompem a importacao em si (o rastreio e secundario a carga).
/// O nome da tabela vem dos descritores <see cref="CvmDatasets"/> (valor controlado, nao
/// proveniente de entrada do usuario), portanto e seguro interpola-lo no SQL.
/// </summary>
public class CvmImportacaoLog : ICvmImportacaoLog
{
    private const int CommandTimeout = 30;

    private readonly ISqlConnectionFactory _factory;
    private readonly ILogger<CvmImportacaoLog> _logger;

    public CvmImportacaoLog(ISqlConnectionFactory factory, ILogger<CvmImportacaoLog> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<long?> IniciarAsync(
        string ledgerTabela, string tipo, string tabela, string? conjunto, int? ano,
        string arquivo, string? usuario, CancellationToken ct = default)
    {
        try
        {
            await using var connection = _factory.CreateWritable();
            var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                $"""
                INSERT INTO {ledgerTabela} (Tipo, Tabela, Conjunto, Ano, Arquivo, Usuario, Status)
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
            _logger.LogError(ex, "Falha ao abrir o registro de importacao em {Ledger} ({Tipo}/{Arquivo}).", ledgerTabela, tipo, arquivo);
            return null;
        }
    }

    public async Task AtualizarAsync(
        string ledgerTabela, long id, int linhasRemovidas, int linhasLidas, int linhasImportadas,
        CancellationToken ct = default)
    {
        try
        {
            await using var connection = _factory.CreateWritable();
            await connection.ExecuteAsync(new CommandDefinition(
                $"""
                UPDATE {ledgerTabela}
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
            _logger.LogWarning(ex, "Falha ao atualizar o registro de importacao {Ledger} {Id}.", ledgerTabela, id);
        }
    }

    public async Task FinalizarAsync(
        string ledgerTabela, long id, string status, string? mensagem,
        int linhasRemovidas, int linhasLidas, int linhasImportadas, string? conjunto,
        CancellationToken ct = default)
    {
        try
        {
            await using var connection = _factory.CreateWritable();
            await connection.ExecuteAsync(new CommandDefinition(
                $"""
                UPDATE {ledgerTabela}
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
            _logger.LogWarning(ex, "Falha ao finalizar o registro de importacao {Ledger} {Id}.", ledgerTabela, id);
        }
    }

    private static string? Truncate(string? value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
