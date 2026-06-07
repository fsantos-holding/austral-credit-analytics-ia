using System.Globalization;
using AustralCreditAnalytics.Api.Models.Itr;
using Dapper;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Comparativo Penultimo x Ultimo da DRE (DRE v2, secao 5). No modo homologo le a view
/// {base}.vw_DreComparativo (YoY, com deteccao de reapresentacao); no modo sequencial le os
/// dois ultimos (Ano, Trimestre) por conta de itr.DreTrimestral (QoQ). Os valores ja chegam
/// em R$ das views/tabela; este servico apenas calcula as variacoes do modo sequencial e
/// monta os rotulos de periodo.
/// </summary>
public class DreComparativoService : IDreComparativoService
{
    private readonly ISqlConnectionFactory _factory;

    public DreComparativoService(ISqlConnectionFactory factory) => _factory = factory;

    public async Task<IReadOnlyList<DreComparativoItem>> ObterAsync(
        string baseDados, string cnpj, string? conjunto, int? ano, ModoComparativo modo, CancellationToken ct = default)
    {
        var cnpjNum = NormalizarCnpj(cnpj);
        if (cnpjNum.Length == 0)
            return Array.Empty<DreComparativoItem>();

        var conjuntoFiltro = string.IsNullOrWhiteSpace(conjunto) ? null : conjunto.Trim().ToUpperInvariant();
        var ehItr = string.Equals(baseDados?.Trim(), "ITR", StringComparison.OrdinalIgnoreCase);
        var ehDfp = string.Equals(baseDados?.Trim(), "DFP", StringComparison.OrdinalIgnoreCase);
        if (!ehItr && !ehDfp)
            throw new InvalidOperationException("Base invalida. Use 'DFP' ou 'ITR'.");

        return modo == ModoComparativo.Sequencial
            ? await ObterSequencialAsync(cnpjNum, conjuntoFiltro, ano, ct)
            : await ObterHomologoAsync(ehDfp ? "dfp" : "itr", ehItr, cnpjNum, conjuntoFiltro, ano, ct);
    }

    /// <summary>Modo homologo (YoY): le a view comparativa da base.</summary>
    private async Task<IReadOnlyList<DreComparativoItem>> ObterHomologoAsync(
        string schema, bool ehItr, string cnpjNum, string? conjunto, int? ano, CancellationToken ct)
    {
        var sql = $"""
            WITH cmp AS (
                SELECT v.*,
                       ROW_NUMBER() OVER (PARTITION BY v.Conjunto, v.CD_CONTA
                                          ORDER BY v.DtFimUltimo DESC) AS rk
                FROM {schema}.vw_DreComparativo v
                WHERE v.CnpjNum = @cnpjNum
                  AND (@conjunto IS NULL OR v.Conjunto = @conjunto)
                  AND v.AnoUltimo = ISNULL(@ano, (
                        SELECT MAX(x.AnoUltimo) FROM {schema}.vw_DreComparativo x
                        WHERE x.CnpjNum = @cnpjNum AND (@conjunto IS NULL OR x.Conjunto = @conjunto)))
            )
            SELECT CD_CONTA, DS_CONTA, ST_CONTA_FIXA, DtFimUltimo, ValorUltimo,
                   DtFimPenultimo, ValorPenultimo, VariacaoAbsoluta, VariacaoPercentual,
                   InversaoDeSinal, BaixaComparabilidade, Reapresentado
            FROM cmp
            WHERE rk = 1
            ORDER BY CD_CONTA;
            """;

        await using var connection = _factory.CreateFromSaved();
        var rows = await connection.QueryAsync<HomologoRow>(new CommandDefinition(
            sql, new { cnpjNum, conjunto, ano = (short?)ano }, cancellationToken: ct));

        return rows.Select(r => new DreComparativoItem(
            CdConta: r.CD_CONTA,
            DsConta: r.DS_CONTA,
            ContaFixa: EhContaFixa(r.ST_CONTA_FIXA),
            PeriodoUltimo: Rotulo(ehItr, r.DtFimUltimo),
            ValorUltimo: r.ValorUltimo,
            PeriodoPenultimo: Rotulo(ehItr, r.DtFimPenultimo),
            ValorPenultimo: r.ValorPenultimo,
            VariacaoAbsoluta: r.VariacaoAbsoluta,
            VariacaoPercentual: r.VariacaoPercentual,
            InversaoDeSinal: r.InversaoDeSinal,
            BaixaComparabilidade: r.BaixaComparabilidade,
            Reapresentado: r.Reapresentado)).ToList();
    }

    /// <summary>Modo sequencial (QoQ): dois ultimos (Ano, Trimestre) por conta de itr.DreTrimestral.</summary>
    private async Task<IReadOnlyList<DreComparativoItem>> ObterSequencialAsync(
        string cnpjNum, string? conjunto, int? ano, CancellationToken ct)
    {
        const string sql = """
            WITH ranked AS (
                SELECT CD_CONTA, DS_CONTA, ST_CONTA_FIXA, Ano, Trimestre, ValorTrimestral,
                       BaixaComparabilidade,
                       ROW_NUMBER() OVER (PARTITION BY CD_CONTA
                                          ORDER BY Ano DESC, Trimestre DESC) AS rk
                FROM itr.DreTrimestral
                WHERE CnpjNum = @cnpjNum
                  AND (@conjunto IS NULL OR Conjunto = @conjunto)
                  AND (@ano      IS NULL OR Ano <= @ano)
                  AND ValorTrimestral IS NOT NULL
            )
            SELECT u.CD_CONTA, u.DS_CONTA, u.ST_CONTA_FIXA, u.BaixaComparabilidade,
                   u.Ano AS AnoUltimo, u.Trimestre AS TriUltimo, u.ValorTrimestral AS ValorUltimo,
                   p.Ano AS AnoPenultimo, p.Trimestre AS TriPenultimo, p.ValorTrimestral AS ValorPenultimo
            FROM ranked u
            LEFT JOIN ranked p ON p.CD_CONTA = u.CD_CONTA AND p.rk = 2
            WHERE u.rk = 1
            ORDER BY u.CD_CONTA;
            """;

        await using var connection = _factory.CreateFromSaved();
        var rows = await connection.QueryAsync<SequencialRow>(new CommandDefinition(
            sql, new { cnpjNum, conjunto, ano = (short?)ano }, cancellationToken: ct));

        return rows.Select(r =>
        {
            var temPar = r.ValorPenultimo.HasValue && r.ValorUltimo.HasValue;
            decimal? varAbs = temPar ? r.ValorUltimo!.Value - r.ValorPenultimo!.Value : null;
            decimal? varPct = (r.ValorPenultimo is { } ant && ant != 0m && r.ValorUltimo.HasValue)
                ? (r.ValorUltimo!.Value - ant) / Math.Abs(ant)
                : null;
            var inversao = temPar && Math.Sign(r.ValorUltimo!.Value) != Math.Sign(r.ValorPenultimo!.Value);

            return new DreComparativoItem(
                CdConta: r.CD_CONTA,
                DsConta: r.DS_CONTA,
                ContaFixa: EhContaFixa(r.ST_CONTA_FIXA),
                PeriodoUltimo: RotuloTri(r.TriUltimo, r.AnoUltimo),
                ValorUltimo: r.ValorUltimo,
                PeriodoPenultimo: r.AnoPenultimo.HasValue ? RotuloTri(r.TriPenultimo, r.AnoPenultimo.Value) : "—",
                ValorPenultimo: r.ValorPenultimo,
                VariacaoAbsoluta: varAbs,
                VariacaoPercentual: varPct,
                InversaoDeSinal: inversao,
                BaixaComparabilidade: r.BaixaComparabilidade,
                Reapresentado: false);
        }).ToList();
    }

    private static bool EhContaFixa(string? st)
        => string.Equals(st?.Trim(), "S", StringComparison.OrdinalIgnoreCase);

    /// <summary>Rotulo do periodo: ano cheio (DFP) ou "T{n} {ano}" (ITR, derivado do mes).</summary>
    private static string Rotulo(bool ehItr, DateTime? dtFim)
    {
        if (dtFim is not { } d)
            return "—";
        return ehItr
            ? RotuloTri((byte)Math.Clamp(d.Month / 3, 1, 4), d.Year)
            : d.Year.ToString(CultureInfo.InvariantCulture);
    }

    private static string RotuloTri(byte? trimestre, int ano)
        => trimestre is { } t ? $"T{t} {ano}" : ano.ToString(CultureInfo.InvariantCulture);

    private static string NormalizarCnpj(string? cnpj)
        => string.IsNullOrWhiteSpace(cnpj)
            ? string.Empty
            : new string(cnpj.Where(char.IsDigit).ToArray());

    private sealed class HomologoRow
    {
        public string CD_CONTA { get; set; } = string.Empty;
        public string? DS_CONTA { get; set; }
        public string? ST_CONTA_FIXA { get; set; }
        public DateTime DtFimUltimo { get; set; }
        public decimal? ValorUltimo { get; set; }
        public DateTime? DtFimPenultimo { get; set; }
        public decimal? ValorPenultimo { get; set; }
        public decimal? VariacaoAbsoluta { get; set; }
        public decimal? VariacaoPercentual { get; set; }
        public bool InversaoDeSinal { get; set; }
        public bool BaixaComparabilidade { get; set; }
        public bool Reapresentado { get; set; }
    }

    private sealed class SequencialRow
    {
        public string CD_CONTA { get; set; } = string.Empty;
        public string? DS_CONTA { get; set; }
        public string? ST_CONTA_FIXA { get; set; }
        public bool BaixaComparabilidade { get; set; }
        public short AnoUltimo { get; set; }
        public byte TriUltimo { get; set; }
        public decimal? ValorUltimo { get; set; }
        public short? AnoPenultimo { get; set; }
        public byte? TriPenultimo { get; set; }
        public decimal? ValorPenultimo { get; set; }
    }
}
