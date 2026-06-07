namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Motor de trimestralizacao da DRE: une as fontes acumuladas (itr.Dre para T1-T3 e
/// dfp.Dre para o 4T anual), deduplica por MAX(VERSAO), de-acumula via LAG e grava a
/// serie trimestral (de-acumulada) em itr.DreTrimestral.
/// </summary>
public interface ITrimestralizacaoService
{
    /// <summary>
    /// Recalcula a serie trimestral no escopo informado e regrava em itr.DreTrimestral
    /// (DELETE do escopo + INSERT do recalculo, em transacao). Todos os filtros sao
    /// opcionais: <paramref name="ano"/> (ano do trimestre), <paramref name="conjunto"/>
    /// (CON/IND) e <paramref name="cnpj"/> (qualquer formatacao). Sem filtros, recalcula tudo.
    /// Retorna a quantidade de linhas trimestrais gravadas.
    /// </summary>
    Task<int> RecalcularAsync(
        int? ano = null, string? conjunto = null, string? cnpj = null, CancellationToken ct = default);
}
