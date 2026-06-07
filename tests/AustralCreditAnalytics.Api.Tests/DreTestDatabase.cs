using AustralCreditAnalytics.Api.Models;
using AustralCreditAnalytics.Api.Services;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AustralCreditAnalytics.Api.Tests;

/// <summary>
/// Banco de testes efemero para os golden tests do motor de trimestralizacao e das views
/// comparativas. Conecta-se ao SQL Server indicado por <c>ACA_TEST_SQLSERVER</c> (string de
/// conexao para o servidor/master, ex.: <c>Server=(localdb)\MSSQLLocalDB;Integrated
/// Security=true;TrustServerCertificate=true</c>), cria um database descartavel com o
/// subconjunto de schema usado pelo motor (itr.Dre, dfp.Dre, itr.DreTrimestral + views),
/// e o remove no <see cref="Dispose"/>. Quando a variavel nao esta definida, os testes que
/// dependem do banco sao ignorados (no-op), de modo que a suite roda sem um SQL Server.
/// </summary>
public sealed class DreTestDatabase : IDisposable
{
    public bool Available { get; }
    public bool Configured { get; }
    public string? SkipReason { get; }
    public ISqlConnectionFactory Factory { get; }

    private readonly string? _serverConnString;
    private readonly string? _dbName;
    private readonly string? _dbConnString;

    public DreTestDatabase()
    {
        _serverConnString = Environment.GetEnvironmentVariable("ACA_TEST_SQLSERVER");
        Configured = !string.IsNullOrWhiteSpace(_serverConnString);
        if (string.IsNullOrWhiteSpace(_serverConnString))
        {
            SkipReason = "Defina ACA_TEST_SQLSERVER (string de conexao do servidor) para rodar os golden tests.";
            Factory = new TestConnectionFactory(string.Empty);
            return;
        }

        try
        {
            _dbName = $"aca_test_{Guid.NewGuid():N}";
            using (var master = new SqlConnection(_serverConnString))
            {
                master.Open();
                master.Execute($"CREATE DATABASE [{_dbName}];");
            }

            var csb = new SqlConnectionStringBuilder(_serverConnString) { InitialCatalog = _dbName };
            _dbConnString = csb.ConnectionString;
            Factory = new TestConnectionFactory(_dbConnString);

            CriarSchema();
            Available = true;
        }
        catch (Exception ex)
        {
            SkipReason = $"SQL Server indisponivel para os golden tests: {ex.Message}";
            Factory = new TestConnectionFactory(_dbConnString ?? string.Empty);
            Available = false;
        }
    }

    private void CriarSchema()
    {
        using var c = new SqlConnection(_dbConnString);
        c.Open();
        var batches = System.Text.RegularExpressions.Regex.Split(
            Ddl, @"^\s*GO\s*$",
            System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (var batch in batches.Select(b => b.Trim()).Where(b => b.Length > 0))
            c.Execute(batch);
    }

    /// <summary>Insere uma linha bruta em itr.Dre / dfp.Dre (layout nativo da CVM).</summary>
    public void SeedDre(
        string tabela, string cdCvm, string conjunto, string cdConta, string dsConta,
        DateTime dtIni, DateTime dtFim, decimal vlConta, string ordem = "ULTIMO",
        string escala = "UNIDADE", string moeda = "REAL", string stContaFixa = "S",
        short versao = 1, long importacaoId = 1, string cnpj = "00000000000100",
        string denom = "CIA TESTE")
    {
        using var c = new SqlConnection(_dbConnString);
        c.Open();
        c.Execute($"""
            INSERT INTO {tabela}
                (CNPJ_CIA, CnpjNum, CD_CVM, DENOM_CIA, VERSAO, GRUPO_DFP, MOEDA, ESCALA_MOEDA,
                 ORDEM_EXERC, DT_INI_EXERC, DT_FIM_EXERC, CD_CONTA, DS_CONTA, VL_CONTA,
                 ST_CONTA_FIXA, Conjunto, Ano, ImportacaoId)
            VALUES
                (@cnpj, @cnpjNum, @cdCvm, @denom, @versao, 'DF Consolidado', @moeda, @escala,
                 @ordem, @dtIni, @dtFim, @cdConta, @dsConta, @vlConta,
                 @stContaFixa, @conjunto, @ano, @importacaoId);
            """,
            new
            {
                cnpj, cnpjNum = cnpj, cdCvm, denom, versao, moeda, escala, ordem,
                dtIni, dtFim, cdConta, dsConta, vlConta, stContaFixa, conjunto,
                ano = (short)dtFim.Year, importacaoId,
            });
    }

    public IReadOnlyList<dynamic> Query(string sql, object? prm = null)
    {
        using var c = new SqlConnection(_dbConnString);
        c.Open();
        return c.Query(sql, prm).ToList();
    }

    public void Dispose()
    {
        if (_dbName is null || string.IsNullOrWhiteSpace(_serverConnString))
            return;
        try
        {
            using var master = new SqlConnection(_serverConnString);
            master.Open();
            master.Execute($"""
                IF DB_ID('{_dbName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{_dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{_dbName}];
                END
                """);
        }
        catch { /* best effort */ }
    }

    /// <summary>
    /// DDL minimo, alinhado a V002/V009/V010/V011/V012, com o suficiente para o motor e as
    /// views. CnpjNum e coluna comum (nos testes ja recebe os digitos). Lotes separados por GO.
    /// </summary>
    private const string Ddl = """
        IF SCHEMA_ID('itr') IS NULL EXEC('CREATE SCHEMA itr');
        GO
        IF SCHEMA_ID('dfp') IS NULL EXEC('CREATE SCHEMA dfp');
        GO
        CREATE TABLE itr.Dre(
            Id BIGINT IDENTITY(1,1) PRIMARY KEY,
            CNPJ_CIA VARCHAR(20) NULL, CnpjNum VARCHAR(20) NULL, CD_CVM CHAR(6) NOT NULL,
            DENOM_CIA VARCHAR(100) NULL, VERSAO SMALLINT NOT NULL,
            GRUPO_DFP VARCHAR(206) NULL, MOEDA VARCHAR(100) NULL, ESCALA_MOEDA VARCHAR(100) NULL,
            ORDEM_EXERC VARCHAR(9) NOT NULL, DT_INI_EXERC DATE NULL, DT_FIM_EXERC DATE NULL,
            CD_CONTA VARCHAR(18) NOT NULL, DS_CONTA VARCHAR(100) NULL, VL_CONTA DECIMAL(29,10) NULL,
            ST_CONTA_FIXA VARCHAR(1) NULL, Conjunto CHAR(3) NULL, Ano SMALLINT NULL, ImportacaoId BIGINT NULL);
        GO
        CREATE TABLE dfp.Dre(
            Id BIGINT IDENTITY(1,1) PRIMARY KEY,
            CNPJ_CIA VARCHAR(20) NULL, CnpjNum VARCHAR(20) NULL, CD_CVM CHAR(6) NOT NULL,
            DENOM_CIA VARCHAR(100) NULL, VERSAO SMALLINT NOT NULL,
            GRUPO_DFP VARCHAR(206) NULL, MOEDA VARCHAR(100) NULL, ESCALA_MOEDA VARCHAR(100) NULL,
            ORDEM_EXERC VARCHAR(9) NOT NULL, DT_INI_EXERC DATE NULL, DT_FIM_EXERC DATE NULL,
            CD_CONTA VARCHAR(18) NOT NULL, DS_CONTA VARCHAR(100) NULL, VL_CONTA DECIMAL(29,10) NULL,
            ST_CONTA_FIXA VARCHAR(1) NULL, Conjunto CHAR(3) NULL, Ano SMALLINT NULL, ImportacaoId BIGINT NULL);
        GO
        CREATE TABLE itr.DreTrimestral(
            Id BIGINT IDENTITY(1,1) PRIMARY KEY,
            CD_CVM CHAR(6) NOT NULL, CnpjNum VARCHAR(20) NULL, DENOM_CIA VARCHAR(100) NULL,
            Ano SMALLINT NOT NULL, Trimestre TINYINT NOT NULL, Conjunto CHAR(3) NOT NULL,
            CD_CONTA VARCHAR(18) NOT NULL, DS_CONTA VARCHAR(100) NULL, ST_CONTA_FIXA VARCHAR(1) NULL,
            ValorTrimestral DECIMAL(29,10) NULL, ValorAcumulado DECIMAL(29,10) NULL,
            OrigemTrimestre CHAR(3) NOT NULL, EscalaMoeda VARCHAR(100) NULL,
            MoedaEstrangeira BIT NOT NULL DEFAULT 0, ExercicioNaoCalendario BIT NOT NULL DEFAULT 0,
            BaixaComparabilidade BIT NOT NULL DEFAULT 0,
            Inconsistente BIT NOT NULL DEFAULT 0, MotivoInconsistencia NVARCHAR(200) NULL,
            CalculadoEmUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
            CONSTRAINT UQ_Itr_DreTrimestral UNIQUE (CD_CVM, Conjunto, Ano, Trimestre, CD_CONTA));
        GO
        EXEC('CREATE OR ALTER VIEW itr.vw_DreComparativo AS
        WITH base AS (
            SELECT CD_CVM, CnpjNum, DENOM_CIA, Conjunto, CD_CONTA, DS_CONTA, ST_CONTA_FIXA, MOEDA,
                   ORDEM_EXERC, DT_FIM_EXERC,
                   VL_CONTA * CASE UPPER(LTRIM(RTRIM(ESCALA_MOEDA)))
                        WHEN ''MILHAO'' THEN 1000000.0 WHEN ''MIL'' THEN 1000.0 ELSE 1.0 END AS VlBase,
                   ROW_NUMBER() OVER (PARTITION BY CD_CVM, Conjunto, CD_CONTA, DT_FIM_EXERC, ORDEM_EXERC
                                      ORDER BY VERSAO DESC, DT_INI_EXERC DESC) AS rn
            FROM itr.Dre
            WHERE DT_FIM_EXERC IS NOT NULL AND DT_INI_EXERC = DATEFROMPARTS(YEAR(DT_FIM_EXERC),1,1) AND Conjunto IS NOT NULL)
        SELECT u.CD_CVM, u.CnpjNum, u.DENOM_CIA, u.Conjunto, u.CD_CONTA, u.DS_CONTA, u.ST_CONTA_FIXA,
               YEAR(u.DT_FIM_EXERC) AS AnoUltimo, u.DT_FIM_EXERC AS DtFimUltimo, u.VlBase AS ValorUltimo,
               ant.DT_FIM_EXERC AS DtFimPenultimo, ant.VlBase AS ValorPenultimo,
               (u.VlBase - ant.VlBase) AS VariacaoAbsoluta,
               CASE WHEN ant.VlBase IS NULL OR ant.VlBase = 0 THEN NULL ELSE (u.VlBase - ant.VlBase)/ABS(ant.VlBase) END AS VariacaoPercentual,
               CASE WHEN ant.VlBase IS NOT NULL AND SIGN(u.VlBase) <> SIGN(ant.VlBase) THEN 1 ELSE 0 END AS InversaoDeSinal,
               CASE WHEN u.ST_CONTA_FIXA COLLATE Latin1_General_CI_AI = ''N'' THEN 1 ELSE 0 END AS BaixaComparabilidade,
               CASE WHEN pen.VlBase IS NOT NULL AND ant.VlBase IS NOT NULL AND ABS(pen.VlBase - ant.VlBase) > 0.005 THEN 1 ELSE 0 END AS Reapresentado,
               ''UNIDADE'' AS EscalaMoeda
        FROM base u
        LEFT JOIN base ant ON ant.CD_CVM=u.CD_CVM AND ant.Conjunto=u.Conjunto AND ant.CD_CONTA=u.CD_CONTA AND ant.rn=1
             AND ant.ORDEM_EXERC COLLATE Latin1_General_CI_AI=''ULTIMO'' AND ant.DT_FIM_EXERC=DATEADD(YEAR,-1,u.DT_FIM_EXERC)
        LEFT JOIN base pen ON pen.CD_CVM=u.CD_CVM AND pen.Conjunto=u.Conjunto AND pen.CD_CONTA=u.CD_CONTA AND pen.rn=1
             AND pen.ORDEM_EXERC COLLATE Latin1_General_CI_AI=''PENULTIMO'' AND pen.DT_FIM_EXERC=DATEADD(YEAR,-1,u.DT_FIM_EXERC)
        WHERE u.rn=1 AND u.ORDEM_EXERC COLLATE Latin1_General_CI_AI=''ULTIMO'';');
        GO
        """;

    private sealed class TestConnectionFactory : ISqlConnectionFactory
    {
        private readonly string _connString;
        public TestConnectionFactory(string connString) => _connString = connString;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_connString);
        public SqlConnection CreateFromSaved() => new(_connString);
        public SqlConnection CreateWritable() => new(_connString);
        public SqlConnection Create(ConnectionConfig config) => throw new NotSupportedException();
        public string BuildConnectionString(ConnectionConfig config) => throw new NotSupportedException();
    }
}
