using AustralCreditAnalytics.Api.Models.Dfp;
using AustralCreditAnalytics.Api.Models.Fre;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>Leitura dos modelos FRE (schema [fre]) para a secao "Composicao Capital".</summary>
public interface IFreRepository
{
    /// <summary>
    /// Companhias com documentos FRE disponiveis (seletor de empresa). Filtro opcional por
    /// <paramref name="busca"/> (razao social ou CNPJ) e limite de resultados.
    /// </summary>
    Task<IReadOnlyList<FreEmpresaResumo>> GetEmpresasAsync(
        string? busca, int limite, CancellationToken ct = default);

    /// <summary>Anos de referencia disponiveis para o CNPJ (+ maior versao entregue).</summary>
    Task<IReadOnlyList<FreAnoDisponivel>> GetAnosAsync(string cnpj, CancellationToken ct = default);

    /// <summary>
    /// Le um modelo FRE para a companhia/ano (opcionalmente uma versao). Retorna as linhas
    /// mestre e, quando existir, o detalhe da tabela filha <c>*_classe_acao</c> (master/detail).
    /// </summary>
    Task<FreModeloDados?> GetModeloAsync(
        string cnpj, string modelo, int? ano, int? versao, CancellationToken ct = default);

    /// <summary>Resumo da composicao de capital (KPIs ON/PN/free float) por ano (fre.vw_CapitalResumo).</summary>
    Task<IReadOnlyList<FreCapitalResumo>> GetCapitalResumoAsync(
        string cnpj, int? ano, CancellationToken ct = default);

    /// <summary>
    /// Historico do ledger fre.Importacao (mais recente primeiro), com filtros opcionais por
    /// tipo e ano. Limita a <paramref name="limite"/> linhas.
    /// </summary>
    Task<IReadOnlyList<DfpImportacaoHistorico>> GetImportacoesAsync(
        string? tipo, int? ano, int limite, CancellationToken ct = default);
}
