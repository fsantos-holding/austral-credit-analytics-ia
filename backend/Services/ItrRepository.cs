using AustralCreditAnalytics.Api.Models.Itr;
using Dapper;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Le a DRE trimestralizada por CNPJ a partir do schema [itr]. O CNPJ e normalizado
/// para somente digitos e comparado contra CnpjNum, permitindo busca independente da
/// formatacao. Espelha o padrao de <see cref="DfpRepository"/>.
/// </summary>
public class ItrRepository : IItrRepository
{
    private readonly ISqlConnectionFactory _factory;

    public ItrRepository(ISqlConnectionFactory factory) => _factory = factory;

    public async Task<IReadOnlyList<ItrDreTrimestral>> GetDreTrimestralAsync(
        string cnpj, string? conjunto, int? ano, CancellationToken ct = default)
    {
        var cnpjNum = NormalizarCnpj(cnpj);
        if (cnpjNum.Length == 0)
            return Array.Empty<ItrDreTrimestral>();

        var conjuntoFiltro = string.IsNullOrWhiteSpace(conjunto) ? null : conjunto.Trim().ToUpperInvariant();

        const string sql = """
            SELECT CD_CVM, CnpjNum, DENOM_CIA, Ano, Trimestre, Conjunto,
                   CD_CONTA, DS_CONTA, ValorTrimestral, ValorAcumulado,
                   OrigemTrimestre, EscalaMoeda, Inconsistente, MotivoInconsistencia
            FROM itr.vw_DreTrimestral
            WHERE CnpjNum = @cnpjNum
              AND (@conjunto IS NULL OR Conjunto = @conjunto)
              AND (@ano      IS NULL OR Ano = @ano)
            ORDER BY Ano DESC, Trimestre, Conjunto, CD_CONTA;
            """;

        await using var connection = _factory.CreateFromSaved();
        var rows = await connection.QueryAsync<ItrDreTrimestral>(new CommandDefinition(
            sql, new { cnpjNum, conjunto = conjuntoFiltro, ano = (short?)ano }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ItrEmpresaResumo>> GetEmpresasAsync(
        string? busca, int limite, CancellationToken ct = default)
    {
        var top = Math.Clamp(limite, 1, 500);
        var buscaFiltro = string.IsNullOrWhiteSpace(busca) ? null : busca.Trim();

        var sql = $"""
            SELECT TOP ({top})
                   CnpjNum,
                   MIN(CD_CVM)         AS CD_CVM,
                   MAX(DENOM_CIA)      AS DENOM_CIA,
                   MAX(Ano)            AS UltimoAno,
                   COUNT(DISTINCT Ano) AS QtdAnos
            FROM itr.DreTrimestral
            WHERE (@busca IS NULL OR DENOM_CIA LIKE '%' + @busca + '%' OR CnpjNum LIKE '%' + @busca + '%')
            GROUP BY CnpjNum
            ORDER BY MAX(DENOM_CIA);
            """;

        await using var connection = _factory.CreateFromSaved();
        var rows = await connection.QueryAsync<ItrEmpresaResumo>(new CommandDefinition(
            sql, new { busca = buscaFiltro }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ItrImportacaoHistorico>> GetImportacoesAsync(
        string? tipo, int? ano, int limite, CancellationToken ct = default)
    {
        var tipoFiltro = string.IsNullOrWhiteSpace(tipo) ? null : tipo.Trim().ToUpperInvariant();
        var top = Math.Clamp(limite, 1, 1000);

        var sql = $"""
            SELECT TOP ({top})
                   Id, Tipo, Tabela, Conjunto, Ano, Arquivo,
                   LinhasLidas, LinhasImportadas, LinhasRemovidas,
                   Status, Mensagem, Usuario, IniciadoEmUtc, ConcluidoEmUtc
            FROM itr.Importacao
            WHERE (@tipo IS NULL OR Tipo = @tipo)
              AND (@ano  IS NULL OR Ano = @ano)
            ORDER BY IniciadoEmUtc DESC, Id DESC;
            """;

        await using var connection = _factory.CreateFromSaved();
        var rows = await connection.QueryAsync<ItrImportacaoHistorico>(new CommandDefinition(
            sql, new { tipo = tipoFiltro, ano = (short?)ano }, cancellationToken: ct));
        return rows.ToList();
    }

    /// <summary>Mantem apenas os digitos do CNPJ (ex.: 00.000.000/0001-00 -> 00000000000100).</summary>
    private static string NormalizarCnpj(string? cnpj)
        => string.IsNullOrWhiteSpace(cnpj)
            ? string.Empty
            : new string(cnpj.Where(char.IsDigit).ToArray());
}
