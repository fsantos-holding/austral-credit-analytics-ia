using AustralCreditAnalytics.Api.Models.Fre;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Agrega os dados FRE (schema [fre]) de uma companhia/ano em um <see cref="FreDossie"/>
/// pronto para a tela premium (KPIs/series/blocos por dominio). Quando a versao nao e
/// informada, usa a maior versao do ano. As tabelas detalhadas brutas continuam vindo
/// de <see cref="IFreRepository.GetModeloAsync"/>.
/// </summary>
public interface IFreDossieRepository
{
    /// <summary>
    /// Monta o dossie FRE consolidado da companhia. <paramref name="ano"/> nulo usa o ano
    /// mais recente; <paramref name="versao"/> nula usa a maior versao do ano.
    /// </summary>
    Task<FreDossie> GetDossieAsync(string cnpj, int? ano, int? versao, CancellationToken ct = default);
}
