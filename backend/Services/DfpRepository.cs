using System.Globalization;
using AustralCreditAnalytics.Api.Models.Dfp;
using Dapper;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Le a estrutura DFP por CNPJ a partir das views do schema [dfp]. O CNPJ informado
/// e normalizado para somente digitos e comparado contra a coluna calculada CnpjNum,
/// permitindo busca independente da formatacao.
/// </summary>
public class DfpRepository : IDfpRepository
{
    private readonly ISqlConnectionFactory _factory;

    public DfpRepository(ISqlConnectionFactory factory) => _factory = factory;

    public async Task<IReadOnlyList<DfpEstruturaResumo>> GetEstruturaAsync(
        string cnpj, CancellationToken ct = default)
    {
        var cnpjNum = NormalizarCnpj(cnpj);
        if (cnpjNum.Length == 0)
            return Array.Empty<DfpEstruturaResumo>();

        const string sql = """
            SELECT CNPJ_CIA, CD_CVM, DENOM_CIA, DT_REFER, VERSAO, CATEG_DOC, DT_RECEB, LINK_DOC,
                   QtBpaCon, QtBpaInd, QtBppCon, QtBppInd, QtDreCon, QtDreInd, QtDraCon, QtDraInd,
                   QtDvaCon, QtDvaInd, QtDfcMdCon, QtDfcMdInd, QtDfcMiCon, QtDfcMiInd, QtDmplCon, QtDmplInd,
                   QtComposicaoCapital, QtParecer
            FROM dfp.vw_Estrutura
            WHERE CnpjNum = @cnpjNum
            ORDER BY DT_REFER DESC, VERSAO DESC;
            """;

        await using var connection = _factory.CreateFromSaved();
        var rows = await connection.QueryAsync<DfpEstruturaResumo>(
            new CommandDefinition(sql, new { cnpjNum }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<DfpConta>> GetContasAsync(
        string cnpj, string? tipo, string? dtRefer, string? ordemExerc,
        string? conjunto, int? ano, CancellationToken ct = default)
    {
        var cnpjNum = NormalizarCnpj(cnpj);
        if (cnpjNum.Length == 0)
            return Array.Empty<DfpConta>();

        var tipoFiltro = string.IsNullOrWhiteSpace(tipo) ? null : tipo.Trim().ToUpperInvariant();
        var ordemFiltro = string.IsNullOrWhiteSpace(ordemExerc) ? null : ordemExerc.Trim().ToUpperInvariant();
        var conjuntoFiltro = string.IsNullOrWhiteSpace(conjunto) ? null : conjunto.Trim().ToUpperInvariant();
        DateTime? dataFiltro = ParseData(dtRefer);

        const string sql = """
            SELECT TIPO_DEM, CNPJ_CIA, CD_CVM, DENOM_CIA, DT_REFER, VERSAO,
                   GRUPO_DFP, Conjunto, Ano, MOEDA, ESCALA_MOEDA, ORDEM_EXERC, DT_INI_EXERC, DT_FIM_EXERC,
                   COLUNA_DF, CD_CONTA, DS_CONTA, VL_CONTA, ST_CONTA_FIXA
            FROM dfp.vw_Conta
            WHERE CnpjNum = @cnpjNum
              AND (@tipo     IS NULL OR TIPO_DEM = @tipo)
              AND (@dt       IS NULL OR DT_REFER = @dt)
              AND (@ordem    IS NULL OR ORDEM_EXERC = @ordem)
              AND (@conjunto IS NULL OR Conjunto = @conjunto)
              AND (@ano      IS NULL OR Ano = @ano)
            ORDER BY DT_REFER DESC, VERSAO DESC, TIPO_DEM, CD_CONTA;
            """;

        await using var connection = _factory.CreateFromSaved();
        var rows = await connection.QueryAsync<DfpConta>(new CommandDefinition(
            sql,
            new
            {
                cnpjNum,
                tipo = tipoFiltro,
                dt = dataFiltro,
                ordem = ordemFiltro,
                conjunto = conjuntoFiltro,
                ano = (short?)ano,
            },
            cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<DfpImportacaoHistorico>> GetImportacoesAsync(
        string? tipo, int? ano, int limite, CancellationToken ct = default)
    {
        var tipoFiltro = string.IsNullOrWhiteSpace(tipo) ? null : tipo.Trim().ToUpperInvariant();
        var top = Math.Clamp(limite, 1, 1000);

        var sql = $"""
            SELECT TOP ({top})
                   Id, Tipo, Tabela, Conjunto, Ano, Arquivo,
                   LinhasLidas, LinhasImportadas, LinhasRemovidas,
                   Status, Mensagem, Usuario, IniciadoEmUtc, ConcluidoEmUtc
            FROM dfp.Importacao
            WHERE (@tipo IS NULL OR Tipo = @tipo)
              AND (@ano  IS NULL OR Ano = @ano)
            ORDER BY IniciadoEmUtc DESC, Id DESC;
            """;

        await using var connection = _factory.CreateFromSaved();
        var rows = await connection.QueryAsync<DfpImportacaoHistorico>(new CommandDefinition(
            sql, new { tipo = tipoFiltro, ano = (short?)ano }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<DfpEmpresaResumo>> GetEmpresasAsync(
        string? busca, int limite, CancellationToken ct = default)
    {
        var top = Math.Clamp(limite, 1, 500);
        var buscaFiltro = string.IsNullOrWhiteSpace(busca) ? null : busca.Trim();

        var sql = $"""
            SELECT TOP ({top})
                   CnpjNum,
                   MIN(CNPJ_CIA)            AS CNPJ_CIA,
                   MIN(CD_CVM)              AS CD_CVM,
                   MAX(DENOM_CIA)           AS DENOM_CIA,
                   MAX(Ano)                 AS UltimoAno,
                   COUNT(DISTINCT Ano)      AS QtdAnos
            FROM dfp.Dre
            WHERE (@busca IS NULL OR DENOM_CIA LIKE '%' + @busca + '%' OR CnpjNum LIKE '%' + @busca + '%')
            GROUP BY CnpjNum
            ORDER BY DENOM_CIA;
            """;

        await using var connection = _factory.CreateFromSaved();
        var rows = await connection.QueryAsync<DfpEmpresaResumo>(new CommandDefinition(
            sql, new { busca = buscaFiltro }, cancellationToken: ct));
        return rows.ToList();
    }

    /// <summary>Mantem apenas os digitos do CNPJ (ex.: 00.000.000/0001-00 -> 00000000000100).</summary>
    private static string NormalizarCnpj(string? cnpj)
        => string.IsNullOrWhiteSpace(cnpj)
            ? string.Empty
            : new string(cnpj.Where(char.IsDigit).ToArray());

    private static DateTime? ParseData(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d1))
            return d1;
        return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2)
            ? d2
            : null;
    }
}
