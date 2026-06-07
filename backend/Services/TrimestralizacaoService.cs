using Dapper;
using Microsoft.Data.SqlClient;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Implementacao do motor de trimestralizacao via window functions no SQL Server (DRE v2).
/// Une itr.Dre (T1-T3, DT_FIM em 03/06/09) e dfp.Dre (4T, DT_FIM em 12) tomando SOMENTE a
/// linha ACUMULADA do exercicio (DT_INI_EXERC = 01/01 do ano, D1) e ORDEM_EXERC = ULTIMO
/// (acento-insensivel). Antes de qualquer aritmetica, normaliza VL_CONTA para R$ pela
/// ESCALA_MOEDA (D3) e deduplica de forma deterministica por
/// (VERSAO DESC, DT_INI_EXERC DESC, ImportacaoId DESC) (D2). O trimestre vem de
/// MONTH(DT_FIM_EXERC)/3 -- garantido em 1..4 pelo filtro de mes, preservando lacunas para
/// o controle do T4 (D6) -- e a de-acumulacao usa LAG por (CD_CVM, Conjunto, CD_CONTA, ano).
/// Propaga ST_CONTA_FIXA e marca BaixaComparabilidade (D5), MoedaEstrangeira (D3) e
/// ExercicioNaoCalendario (D4). Quando o T4 (DFP) nao tem o 3T acumulado para de-acumular,
/// grava ValorTrimestral = NULL + Inconsistente (D6), preservando ValorAcumulado. Os valores
/// gravados ja estao em R$ (EscalaMoeda = 'UNIDADE'); o frontend apenas formata. O resultado
/// substitui o escopo recalculado em itr.DreTrimestral (DELETE + INSERT em transacao).
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
            -- ITR: SOMENTE a linha ACUMULADA do exercicio (D1: DT_INI = 01/01 do ano).
            SELECT CD_CVM, CnpjNum, DENOM_CIA, Conjunto, ST_CONTA_FIXA, MOEDA,
                   CD_CONTA, DS_CONTA, DT_FIM_EXERC, DT_INI_EXERC, VERSAO, ImportacaoId,
                   VL_CONTA *
                     CASE UPPER(LTRIM(RTRIM(ESCALA_MOEDA)))      -- D3: normaliza para R$
                          WHEN 'MILHAO' THEN 1000000.0 WHEN 'MILHÃO' THEN 1000000.0
                          WHEN 'MIL' THEN 1000.0 ELSE 1.0 END                AS VlBase,
                   'ITR' AS Origem
            FROM itr.Dre
            WHERE ORDEM_EXERC COLLATE Latin1_General_CI_AI = 'ULTIMO'
              AND DT_FIM_EXERC IS NOT NULL
              AND DT_INI_EXERC = DATEFROMPARTS(YEAR(DT_FIM_EXERC), 1, 1)      -- D1: fixa o YTD
              AND MONTH(DT_FIM_EXERC) IN (3, 6, 9)
            UNION ALL
            -- DFP: anual (Jan-Dez), acumulado do exercicio por definicao.
            SELECT CD_CVM, CnpjNum, DENOM_CIA, Conjunto, ST_CONTA_FIXA, MOEDA,
                   CD_CONTA, DS_CONTA, DT_FIM_EXERC, DT_INI_EXERC, VERSAO, ImportacaoId,
                   VL_CONTA *
                     CASE UPPER(LTRIM(RTRIM(ESCALA_MOEDA)))
                          WHEN 'MILHAO' THEN 1000000.0 WHEN 'MILHÃO' THEN 1000000.0
                          WHEN 'MIL' THEN 1000.0 ELSE 1.0 END                AS VlBase,
                   'DFP' AS Origem
            FROM dfp.Dre
            WHERE ORDEM_EXERC COLLATE Latin1_General_CI_AI = 'ULTIMO'
              AND DT_FIM_EXERC IS NOT NULL
              AND DT_INI_EXERC = DATEFROMPARTS(YEAR(DT_FIM_EXERC), 1, 1)
              AND MONTH(DT_FIM_EXERC) = 12
        ),
        escopo AS (
            SELECT * FROM fonte
            WHERE Conjunto IS NOT NULL
              AND (@ano      IS NULL OR YEAR(DT_FIM_EXERC) = @ano)
              AND (@conjunto IS NULL OR Conjunto = @conjunto)
              AND (@cnpjNum  IS NULL OR CnpjNum  = @cnpjNum)
        ),
        dedup AS (   -- D2: maior VERSAO com desempate deterministico
            SELECT *,
                   ROW_NUMBER() OVER (
                       PARTITION BY CD_CVM, Conjunto, CD_CONTA, DT_FIM_EXERC
                       ORDER BY VERSAO DESC, DT_INI_EXERC DESC, ImportacaoId DESC) AS rn
            FROM escopo
        ),
        unico AS (
            SELECT CD_CVM, CnpjNum, DENOM_CIA, Conjunto, ST_CONTA_FIXA, MOEDA, CD_CONTA, DS_CONTA,
                   VlBase, Origem, DT_FIM_EXERC,
                   YEAR(DT_FIM_EXERC)      AS Ano,
                   -- D4: trimestre pela ordem real do periodo. O filtro de mes garante
                   -- MONTH IN (3,6,9,12), de modo que MONTH/3 retorna 1..4 e PRESERVA as
                   -- lacunas (essencial para detectar o 3T ausente no T4 da DFP, D6).
                   MONTH(DT_FIM_EXERC) / 3 AS Trimestre,
                   CASE WHEN MONTH(DT_FIM_EXERC) NOT IN (3, 6, 9, 12) THEN 1 ELSE 0 END
                                           AS ExercicioNaoCalendario
            FROM dedup WHERE rn = 1
        ),
        serie AS (
            SELECT *,
                   COUNT(*)       OVER (PARTITION BY CD_CVM, Conjunto, CD_CONTA, Ano)                     AS QtdTri,
                   LAG(VlBase)    OVER (PARTITION BY CD_CVM, Conjunto, CD_CONTA, Ano ORDER BY Trimestre)  AS VlBaseAnterior,
                   LAG(Trimestre) OVER (PARTITION BY CD_CVM, Conjunto, CD_CONTA, Ano ORDER BY Trimestre)  AS TriAnterior
            FROM unico
        )
        INSERT INTO itr.DreTrimestral
            (CD_CVM, CnpjNum, DENOM_CIA, Ano, Trimestre, Conjunto, CD_CONTA, DS_CONTA,
             ST_CONTA_FIXA, ValorTrimestral, ValorAcumulado, OrigemTrimestre, EscalaMoeda,
             MoedaEstrangeira, ExercicioNaoCalendario, BaixaComparabilidade,
             Inconsistente, MotivoInconsistencia)
        SELECT
            s.CD_CVM, s.CnpjNum, s.DENOM_CIA, s.Ano, s.Trimestre, s.Conjunto, s.CD_CONTA, s.DS_CONTA,
            s.ST_CONTA_FIXA,
            -- D6: T1 = proprio valor; sem antecessor (T>1) -> NULL; T4 (DFP) com 3T ausente
            -- (antecessor != T3 e fora do salto aceito 1->4) -> NULL preservando ValorAcumulado.
            CASE WHEN s.Trimestre = 1                THEN s.VlBase
                 WHEN s.VlBaseAnterior IS NULL       THEN NULL
                 WHEN s.Trimestre = 4 AND s.Origem = 'DFP' AND s.TriAnterior NOT IN (1, 3) THEN NULL
                 ELSE s.VlBase - s.VlBaseAnterior END AS ValorTrimestral,
            s.VlBase                                 AS ValorAcumulado,    -- R4 (em R$)
            s.Origem                                 AS OrigemTrimestre,   -- R5
            'UNIDADE'                                AS EscalaMoeda,       -- D3: ja em R$
            CASE WHEN s.MOEDA COLLATE Latin1_General_CI_AI <> 'REAL'
                      AND s.MOEDA IS NOT NULL THEN 1 ELSE 0 END            AS MoedaEstrangeira,   -- D3
            s.ExercicioNaoCalendario,                                                            -- D4
            CASE WHEN s.ST_CONTA_FIXA COLLATE Latin1_General_CI_AI = 'N' THEN 1 ELSE 0 END
                                                                          AS BaixaComparabilidade, -- D5
            -- R6 + D6: gap intermediario (exceto salto 1T->4T com 4T de DFP) ou T>1 sem antecessor.
            CASE WHEN s.Trimestre - ISNULL(s.TriAnterior, s.Trimestre) > 1
                      AND NOT (s.Trimestre = 4 AND s.TriAnterior = 1 AND s.Origem = 'DFP')
                     THEN 1
                 WHEN s.Trimestre > 1 AND s.VlBaseAnterior IS NULL THEN 1
                 ELSE 0 END                                               AS Inconsistente,
            CASE WHEN s.Trimestre > 1 AND s.VlBaseAnterior IS NULL
                     THEN N'Trimestre ' + CONVERT(NVARCHAR(2), s.Trimestre) + N' sem antecessor para de-acumular'
                 WHEN s.Trimestre - ISNULL(s.TriAnterior, s.Trimestre) > 1
                      AND NOT (s.Trimestre = 4 AND s.TriAnterior = 1 AND s.Origem = 'DFP')
                     THEN N'Trimestre(s) intermediario(s) ausente(s) antes do T' + CONVERT(NVARCHAR(2), s.Trimestre)
                 ELSE NULL END                                            AS MotivoInconsistencia
        FROM serie s
        WHERE NOT (s.QtdTri = 1 AND s.Trimestre <> 1);   -- R7: registro unico que nao seja T1
        """;

    /// <summary>Mantem apenas os digitos do CNPJ (ex.: 00.000.000/0001-00 -> 00000000000100).</summary>
    private static string NormalizarCnpj(string? cnpj)
        => string.IsNullOrWhiteSpace(cnpj)
            ? string.Empty
            : new string(cnpj.Where(char.IsDigit).ToArray());
}
