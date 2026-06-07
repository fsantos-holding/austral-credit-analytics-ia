using Dapper;
using Microsoft.Data.SqlClient;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Implementacao do motor de trimestralizacao via window functions no SQL Server.
/// Une itr.Dre (T1-T3, DT_FIM em 03/06/09) e dfp.Dre (4T, DT_FIM em 12), filtra
/// ORDEM_EXERC = ULTIMO (acento-insensivel), deduplica por MAX(VERSAO), deriva o
/// trimestre de MONTH(DT_FIM_EXERC)/3 e de-acumula via LAG por
/// (CD_CVM, Conjunto, CD_CONTA, ano). O resultado substitui o escopo recalculado em
/// itr.DreTrimestral (DELETE + INSERT em transacao).
/// </summary>
public class TrimestralizacaoService : ITrimestralizacaoService
{
    private const int CommandTimeout = 300;

    private readonly ISqlConnectionFactory _factory;
    private readonly ILogger<TrimestralizacaoService> _logger;

    public TrimestralizacaoService(ISqlConnectionFactory factory, ILogger<TrimestralizacaoService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<int> RecalcularAsync(
        int? ano = null, string? conjunto = null, string? cnpj = null, CancellationToken ct = default)
    {
        var conjuntoFiltro = string.IsNullOrWhiteSpace(conjunto) ? null : conjunto.Trim().ToUpperInvariant();
        var cnpjNum = NormalizarCnpj(cnpj);
        var cnpjFiltro = cnpjNum.Length == 0 ? null : cnpjNum;

        var prm = new
        {
            ano = (short?)ano,
            conjunto = conjuntoFiltro,
            cnpjNum = cnpjFiltro,
        };

        await using var connection = _factory.CreateWritable();
        await connection.OpenAsync(ct);
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            // 1. Remove o escopo a recalcular de itr.DreTrimestral.
            await connection.ExecuteAsync(new CommandDefinition(
                """
                DELETE FROM itr.DreTrimestral
                 WHERE (@ano      IS NULL OR Ano      = @ano)
                   AND (@conjunto IS NULL OR Conjunto = @conjunto)
                   AND (@cnpjNum  IS NULL OR CnpjNum  = @cnpjNum);
                """,
                prm, transaction: tx, commandTimeout: CommandTimeout, cancellationToken: ct));

            // 2. Recalcula e grava a serie trimestral de-acumulada.
            var gravadas = await connection.ExecuteAsync(new CommandDefinition(
                RecalcularSql, prm, transaction: tx, commandTimeout: CommandTimeout, cancellationToken: ct));

            await tx.CommitAsync(ct);

            _logger.LogInformation(
                "Trimestralizacao recalculada (Ano={Ano}, Conjunto={Conjunto}, Cnpj={Cnpj}): {Linhas} linha(s) gravada(s).",
                ano?.ToString() ?? "*", conjuntoFiltro ?? "*", cnpjFiltro ?? "*", gravadas);

            return gravadas;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private const string RecalcularSql = """
        WITH fonte AS (
            SELECT CD_CVM, CnpjNum, DENOM_CIA, Conjunto, ESCALA_MOEDA,
                   CD_CONTA, DS_CONTA, DT_FIM_EXERC, VERSAO, VL_CONTA, 'ITR' AS Origem
            FROM itr.Dre
            WHERE ORDEM_EXERC COLLATE Latin1_General_CI_AI = 'ULTIMO'
              AND DT_FIM_EXERC IS NOT NULL
              AND MONTH(DT_FIM_EXERC) IN (3, 6, 9)
            UNION ALL
            SELECT CD_CVM, CnpjNum, DENOM_CIA, Conjunto, ESCALA_MOEDA,
                   CD_CONTA, DS_CONTA, DT_FIM_EXERC, VERSAO, VL_CONTA, 'DFP' AS Origem
            FROM dfp.Dre
            WHERE ORDEM_EXERC COLLATE Latin1_General_CI_AI = 'ULTIMO'
              AND DT_FIM_EXERC IS NOT NULL
              AND MONTH(DT_FIM_EXERC) = 12
        ),
        escopo AS (
            SELECT * FROM fonte
            WHERE Conjunto IS NOT NULL
              AND (@ano      IS NULL OR YEAR(DT_FIM_EXERC) = @ano)
              AND (@conjunto IS NULL OR Conjunto = @conjunto)
              AND (@cnpjNum  IS NULL OR CnpjNum  = @cnpjNum)
        ),
        dedup AS (
            SELECT *,
                   ROW_NUMBER() OVER (
                       PARTITION BY CD_CVM, Conjunto, CD_CONTA, DT_FIM_EXERC
                       ORDER BY VERSAO DESC) AS rn
            FROM escopo
        ),
        unico AS (
            SELECT CD_CVM, CnpjNum, DENOM_CIA, Conjunto, ESCALA_MOEDA, CD_CONTA, DS_CONTA,
                   VL_CONTA, Origem,
                   YEAR(DT_FIM_EXERC)      AS Ano,
                   MONTH(DT_FIM_EXERC) / 3 AS Trimestre
            FROM dedup WHERE rn = 1
        ),
        serie AS (
            SELECT *,
                   COUNT(*)       OVER (PARTITION BY CD_CVM, Conjunto, CD_CONTA, Ano)                     AS QtdTri,
                   LAG(VL_CONTA)  OVER (PARTITION BY CD_CVM, Conjunto, CD_CONTA, Ano ORDER BY Trimestre)  AS VlAnterior,
                   LAG(Trimestre) OVER (PARTITION BY CD_CVM, Conjunto, CD_CONTA, Ano ORDER BY Trimestre)  AS TriAnterior
            FROM unico
        )
        INSERT INTO itr.DreTrimestral
            (CD_CVM, CnpjNum, DENOM_CIA, Ano, Trimestre, Conjunto, CD_CONTA, DS_CONTA,
             ValorTrimestral, ValorAcumulado, OrigemTrimestre, EscalaMoeda, Inconsistente, MotivoInconsistencia)
        SELECT
            s.CD_CVM, s.CnpjNum, s.DENOM_CIA, s.Ano, s.Trimestre, s.Conjunto, s.CD_CONTA, s.DS_CONTA,
            s.VL_CONTA - ISNULL(s.VlAnterior, 0) AS ValorTrimestral,
            s.VL_CONTA                           AS ValorAcumulado,
            s.Origem                             AS OrigemTrimestre,
            s.ESCALA_MOEDA                       AS EscalaMoeda,
            CASE WHEN s.TriAnterior IS NOT NULL AND (s.Trimestre - s.TriAnterior) > 1
                      AND NOT (s.Trimestre = 4 AND s.Origem = 'DFP')
                 THEN 1 ELSE 0 END               AS Inconsistente,
            CASE WHEN s.TriAnterior IS NOT NULL AND (s.Trimestre - s.TriAnterior) > 1
                      AND NOT (s.Trimestre = 4 AND s.Origem = 'DFP')
                 THEN N'Trimestre(s) intermediario(s) ausente(s) antes do T' + CONVERT(NVARCHAR(2), s.Trimestre)
                 ELSE NULL END                   AS MotivoInconsistencia
        FROM serie s
        WHERE NOT (s.QtdTri = 1 AND s.Trimestre <> 1);
        """;

    /// <summary>Mantem apenas os digitos do CNPJ (ex.: 00.000.000/0001-00 -> 00000000000100).</summary>
    private static string NormalizarCnpj(string? cnpj)
        => string.IsNullOrWhiteSpace(cnpj)
            ? string.Empty
            : new string(cnpj.Where(char.IsDigit).ToArray());
}
