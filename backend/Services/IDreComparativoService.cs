using AustralCreditAnalytics.Api.Models.Itr;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Comparativo Penultimo x Ultimo da DRE (DRE v2, secao 5). Atende os modos homologo
/// (YoY, via vw_DreComparativo) e sequencial (QoQ, via itr.DreTrimestral).
/// </summary>
public interface IDreComparativoService
{
    /// <summary>
    /// Obtem o comparativo por conta para o CNPJ.
    /// </summary>
    /// <param name="baseDados">"DFP" ou "ITR" (define a view homologa consultada).</param>
    /// <param name="cnpj">CNPJ (qualquer formatacao; so digitos sao considerados).</param>
    /// <param name="conjunto">"CON"/"IND" ou null (ambos).</param>
    /// <param name="ano">Ano do periodo "ultimo"; null = mais recente disponivel.</param>
    /// <param name="modo">Homologo (padrao) ou Sequencial.</param>
    Task<IReadOnlyList<DreComparativoItem>> ObterAsync(
        string baseDados,
        string cnpj,
        string? conjunto,
        int? ano,
        ModoComparativo modo,
        CancellationToken ct = default);
}
