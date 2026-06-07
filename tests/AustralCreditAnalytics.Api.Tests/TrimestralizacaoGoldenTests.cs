using AustralCreditAnalytics.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AustralCreditAnalytics.Api.Tests;

/// <summary>
/// Testes de aceite (golden) do motor de trimestralizacao (DRE v2) e da view comparativa.
/// Cobrem: (1) ITR 2T/3T trazem acumulada E trimestre isolado -> so a acumulada entra;
/// (2) reapresentacao (PENULTIMO do arquivo divergente do ULTIMO ja importado);
/// (3) exercicio nao-dezembro; (4) mistura de escala MIL/UNIDADE; alem de T4 sem 3T (D6)
/// e moeda estrangeira (D3). Rodam apenas quando <c>ACA_TEST_SQLSERVER</c> esta definido;
/// caso contrario sao ignorados (no-op), para a suite rodar sem um SQL Server.
///
/// Para rodar com banco (ex.: LocalDB):
///   setx ACA_TEST_SQLSERVER "Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true"
///   dotnet test
/// </summary>
public class TrimestralizacaoGoldenTests : IClassFixture<DreTestDatabase>
{
    private readonly DreTestDatabase _db;
    private readonly TrimestralizacaoService _engine;

    public TrimestralizacaoGoldenTests(DreTestDatabase db)
    {
        _db = db;
        _engine = new TrimestralizacaoService(db.Factory, NullLogger<TrimestralizacaoService>.Instance);
    }

    private bool Skip()
    {
        if (_db.Available) return false;
        // Se o banco foi configurado (ACA_TEST_SQLSERVER) mas nao subiu, falha alto em vez de
        // mascarar o problema; sem configuracao, ignora silenciosamente (no-op).
        if (_db.Configured)
            throw new InvalidOperationException(_db.SkipReason ?? "Banco de testes indisponivel.");
        return true;
    }

    // --------------------------------------------------------------------------------------
    // (1) ITR 2T/3T: a CVM traz, para o mesmo DT_FIM, a linha acumulada (DT_INI=01/01) e a
    //     do trimestre isolado (DT_INI=01/04, 01/07). So a acumulada deve alimentar o motor.
    // --------------------------------------------------------------------------------------
    [Fact]
    public async Task SoAcumuladaDoExercicioAlimentaOMotor()
    {
        if (Skip()) return;
        const string cnpj = "10000000000001";
        const string cvm = "010001";

        _db.SeedDre("itr.Dre", cvm, "CON", "3.01", "Receita", D(2023, 1, 1), D(2023, 3, 31), 100m, cnpj: cnpj);
        _db.SeedDre("itr.Dre", cvm, "CON", "3.01", "Receita", D(2023, 1, 1), D(2023, 6, 30), 250m, cnpj: cnpj);
        _db.SeedDre("itr.Dre", cvm, "CON", "3.01", "Receita", D(2023, 4, 1), D(2023, 6, 30), 150m, cnpj: cnpj); // isolado -> ignorar
        _db.SeedDre("itr.Dre", cvm, "CON", "3.01", "Receita", D(2023, 1, 1), D(2023, 9, 30), 420m, cnpj: cnpj);
        _db.SeedDre("itr.Dre", cvm, "CON", "3.01", "Receita", D(2023, 7, 1), D(2023, 9, 30), 170m, cnpj: cnpj); // isolado -> ignorar
        _db.SeedDre("dfp.Dre", cvm, "CON", "3.01", "Receita", D(2023, 1, 1), D(2023, 12, 31), 600m, cnpj: cnpj);

        await _engine.RecalcularAsync(cnpj: cnpj);
        var rows = Tri(cnpj);

        Assert.Equal(4, rows.Count);
        AssertTri(rows[0], 1, acc: 100m, tri: 100m, inconsistente: false);
        AssertTri(rows[1], 2, acc: 250m, tri: 150m, inconsistente: false);
        AssertTri(rows[2], 3, acc: 420m, tri: 170m, inconsistente: false);
        AssertTri(rows[3], 4, acc: 600m, tri: 180m, inconsistente: false);
    }

    // --------------------------------------------------------------------------------------
    // (D6) T4 (DFP) com 3T ausente (mas 1T/2T presentes) -> ValorTrimestral NULL + inconsistente,
    //      preservando ValorAcumulado. O salto aceito 1T->4T continua de-acumulando.
    // --------------------------------------------------------------------------------------
    [Fact]
    public async Task T4SemTerceiroTrimestreGravaNuloInconsistente()
    {
        if (Skip()) return;
        const string cnpj = "10000000000002";
        const string cvm = "010002";

        _db.SeedDre("itr.Dre", cvm, "CON", "3.01", "Receita", D(2023, 1, 1), D(2023, 3, 31), 100m, cnpj: cnpj);
        _db.SeedDre("itr.Dre", cvm, "CON", "3.01", "Receita", D(2023, 1, 1), D(2023, 6, 30), 250m, cnpj: cnpj);
        // 3T ausente.
        _db.SeedDre("dfp.Dre", cvm, "CON", "3.01", "Receita", D(2023, 1, 1), D(2023, 12, 31), 600m, cnpj: cnpj);

        await _engine.RecalcularAsync(cnpj: cnpj);
        var rows = Tri(cnpj);

        var t4 = rows.Single(r => (byte)r.Trimestre == 4);
        Assert.Null((decimal?)t4.ValorTrimestral);
        Assert.Equal(600m, (decimal?)t4.ValorAcumulado);
        Assert.True((bool)t4.Inconsistente);
    }

    // --------------------------------------------------------------------------------------
    // (3) Exercicio nao-dezembro: o pin YTD (DT_INI=01/01) + filtro de mes (3,6,9,12) restringe
    //     a serie aos trimestres-calendario; um fechamento em 30/11 nao entra na serie.
    // --------------------------------------------------------------------------------------
    [Fact]
    public async Task ExercicioNaoDezembroNaoEntraNaSerie()
    {
        if (Skip()) return;
        const string cnpj = "10000000000003";
        const string cvm = "010003";

        _db.SeedDre("itr.Dre", cvm, "CON", "3.01", "Receita", D(2023, 1, 1), D(2023, 3, 31), 100m, cnpj: cnpj);
        _db.SeedDre("itr.Dre", cvm, "CON", "3.01", "Receita", D(2023, 1, 1), D(2023, 11, 30), 999m, cnpj: cnpj); // nao-calendario

        await _engine.RecalcularAsync(cnpj: cnpj);
        var rows = Tri(cnpj);

        Assert.Single(rows);
        Assert.Equal(1, (int)(byte)rows[0].Trimestre);
        Assert.DoesNotContain(rows, r => (decimal?)r.ValorAcumulado == 999m);
    }

    // --------------------------------------------------------------------------------------
    // (4) Mistura de escala: ITR em MIL, DFP em UNIDADE. A normalizacao para R$ acontece ANTES
    //     da de-acumulacao, de modo que T4 = 600.000 - 420.000 = 180.000 (grandezas compativeis).
    // --------------------------------------------------------------------------------------
    [Fact]
    public async Task MisturaEscalaNormalizaParaReaisAntesDaDeacumulacao()
    {
        if (Skip()) return;
        const string cnpj = "10000000000004";
        const string cvm = "010004";

        _db.SeedDre("itr.Dre", cvm, "CON", "3.02", "Lucro", D(2023, 1, 1), D(2023, 3, 31), 100m, escala: "MIL", cnpj: cnpj);
        _db.SeedDre("itr.Dre", cvm, "CON", "3.02", "Lucro", D(2023, 1, 1), D(2023, 6, 30), 250m, escala: "MIL", cnpj: cnpj);
        _db.SeedDre("itr.Dre", cvm, "CON", "3.02", "Lucro", D(2023, 1, 1), D(2023, 9, 30), 420m, escala: "MIL", cnpj: cnpj);
        _db.SeedDre("dfp.Dre", cvm, "CON", "3.02", "Lucro", D(2023, 1, 1), D(2023, 12, 31), 600000m, escala: "UNIDADE", cnpj: cnpj);

        await _engine.RecalcularAsync(cnpj: cnpj);
        var rows = Tri(cnpj);

        AssertTri(rows[0], 1, acc: 100000m, tri: 100000m, inconsistente: false);
        AssertTri(rows[2], 3, acc: 420000m, tri: 170000m, inconsistente: false);
        AssertTri(rows[3], 4, acc: 600000m, tri: 180000m, inconsistente: false);
        Assert.All(rows, r => Assert.Equal("UNIDADE", ((string)r.EscalaMoeda).Trim()));
    }

    // --------------------------------------------------------------------------------------
    // (D3) Moeda estrangeira: MOEDA <> 'REAL' marca MoedaEstrangeira = 1.
    // --------------------------------------------------------------------------------------
    [Fact]
    public async Task MoedaEstrangeiraEhSinalizada()
    {
        if (Skip()) return;
        const string cnpj = "10000000000005";
        const string cvm = "010005";

        _db.SeedDre("itr.Dre", cvm, "CON", "3.01", "Receita", D(2023, 1, 1), D(2023, 3, 31), 100m, moeda: "DOLAR", cnpj: cnpj);

        await _engine.RecalcularAsync(cnpj: cnpj);
        var rows = Tri(cnpj);

        Assert.Single(rows);
        Assert.True((bool)rows[0].MoedaEstrangeira);
    }

    // --------------------------------------------------------------------------------------
    // (2) Reapresentacao (D8) via itr.vw_DreComparativo: o PENULTIMO publicado no arquivo de
    //     2023 (100) diverge do ULTIMO ja importado para 2022 (90) -> Reapresentado = 1.
    // --------------------------------------------------------------------------------------
    [Fact]
    public void ReapresentacaoEhDetectadaNaViewComparativa()
    {
        if (Skip()) return;
        const string cnpj = "10000000000006";
        const string cvm = "010006";

        // Conta reapresentada: ULTIMO 2023 = 130; PENULTIMO (no arquivo 2023) p/ 2022 = 100;
        // ULTIMO 2022 ja importado (autoritativo) = 90.
        _db.SeedDre("itr.Dre", cvm, "CON", "3.01", "Receita", D(2023, 1, 1), D(2023, 9, 30), 130m, ordem: "ULTIMO", cnpj: cnpj);
        _db.SeedDre("itr.Dre", cvm, "CON", "3.01", "Receita", D(2022, 1, 1), D(2022, 9, 30), 100m, ordem: "PENULTIMO", cnpj: cnpj);
        _db.SeedDre("itr.Dre", cvm, "CON", "3.01", "Receita", D(2022, 1, 1), D(2022, 9, 30), 90m, ordem: "ULTIMO", cnpj: cnpj);

        // Conta estavel (controle): PENULTIMO == ULTIMO do ano anterior -> nao reapresentada.
        _db.SeedDre("itr.Dre", cvm, "CON", "3.02", "Custo", D(2023, 1, 1), D(2023, 9, 30), 70m, ordem: "ULTIMO", cnpj: cnpj);
        _db.SeedDre("itr.Dre", cvm, "CON", "3.02", "Custo", D(2022, 1, 1), D(2022, 9, 30), 50m, ordem: "PENULTIMO", cnpj: cnpj);
        _db.SeedDre("itr.Dre", cvm, "CON", "3.02", "Custo", D(2022, 1, 1), D(2022, 9, 30), 50m, ordem: "ULTIMO", cnpj: cnpj);

        var rows = _db.Query(
            """
            SELECT CD_CONTA, ValorUltimo, ValorPenultimo, VariacaoAbsoluta, Reapresentado
            FROM itr.vw_DreComparativo
            WHERE CnpjNum = @cnpj AND AnoUltimo = 2023
            ORDER BY CD_CONTA;
            """,
            new { cnpj });

        var reapr = rows.Single(r => (string)r.CD_CONTA == "3.01");
        Assert.Equal(130m, (decimal?)reapr.ValorUltimo);
        Assert.Equal(90m, (decimal?)reapr.ValorPenultimo);   // ant = ULTIMO 2022 (autoritativo)
        Assert.Equal(40m, (decimal?)reapr.VariacaoAbsoluta);
        Assert.True(Convert.ToBoolean(reapr.Reapresentado));

        var estavel = rows.Single(r => (string)r.CD_CONTA == "3.02");
        Assert.False(Convert.ToBoolean(estavel.Reapresentado));
    }

    // --------------------------------------------------------------------------------------
    private static DateTime D(int y, int m, int d) => new(y, m, d);

    private IReadOnlyList<dynamic> Tri(string cnpj) => _db.Query(
        """
        SELECT Trimestre, ValorAcumulado, ValorTrimestral, EscalaMoeda, MoedaEstrangeira,
               BaixaComparabilidade, Inconsistente, MotivoInconsistencia
        FROM itr.DreTrimestral
        WHERE CnpjNum = @cnpj
        ORDER BY Ano, Trimestre;
        """,
        new { cnpj });

    private static void AssertTri(dynamic row, int trimestre, decimal? acc, decimal? tri, bool inconsistente)
    {
        Assert.Equal(trimestre, (int)(byte)row.Trimestre);
        Assert.Equal(acc, (decimal?)row.ValorAcumulado);
        Assert.Equal(tri, (decimal?)row.ValorTrimestral);
        Assert.Equal(inconsistente, (bool)row.Inconsistente);
    }
}
