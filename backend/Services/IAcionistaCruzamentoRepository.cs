using AustralCreditAnalytics.Api.Models.Fre;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Cruza as posicoes acionarias (fre.PosicaoAcionaria) entre todas as companhias
/// importadas, identificando acionistas presentes em mais de uma empresa. A chave de
/// cruzamento e o documento (CPF/CNPJ somente digitos) e, na ausencia, o nome normalizado.
/// Usa um snapshot da ultima (Ano, Versao) de cada empresa, opcionalmente filtrado por ano.
/// </summary>
public interface IAcionistaCruzamentoRepository
{
    /// <summary>
    /// Ranking dos acionistas presentes em <paramref name="minEmpresas"/> ou mais empresas
    /// distintas. Filtros opcionais: <paramref name="ano"/>, <paramref name="tipoPessoa"/>
    /// e <paramref name="busca"/> (nome/documento). Cada item ja traz as empresas (drilldown).
    /// </summary>
    Task<IReadOnlyList<AcionistaRankingItem>> GetRankingAsync(
        int? ano, string? tipoPessoa, int minEmpresas, string? busca, int limite,
        CancellationToken ct = default);

    /// <summary>
    /// Detalhe de um acionista (drilldown): identificacao + todas as empresas em que aparece
    /// com os percentuais. Retorna null quando a chave nao tem posicoes.
    /// </summary>
    Task<AcionistaDetalhe?> GetPosicoesAcionistaAsync(
        string chave, int? ano, CancellationToken ct = default);

    /// <summary>
    /// Insights agregados do cruzamento: KPIs, series (tipo de pessoa, top conectores) e
    /// pares de empresas com acionistas em comum. Filtro opcional por <paramref name="ano"/>.
    /// </summary>
    Task<AcionistaInsights> GetInsightsAsync(int? ano, CancellationToken ct = default);
}
