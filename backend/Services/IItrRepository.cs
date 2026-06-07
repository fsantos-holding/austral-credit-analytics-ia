using AustralCreditAnalytics.Api.Models.Itr;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>Leitura da DRE trimestralizada por CNPJ (schema [itr]).</summary>
public interface IItrRepository
{
    /// <summary>
    /// Serie trimestral (itr.vw_DreTrimestral) do CNPJ, com filtros opcionais por conjunto
    /// (CON/IND) e ano. Ordenada por ano/trimestre/conta.
    /// </summary>
    Task<IReadOnlyList<ItrDreTrimestral>> GetDreTrimestralAsync(
        string cnpj, string? conjunto, int? ano, CancellationToken ct = default);

    /// <summary>
    /// Companhias com DRE trimestral disponivel (seletor de empresa). Filtro opcional por
    /// <paramref name="busca"/> (razao social ou CNPJ) e limite de resultados.
    /// </summary>
    Task<IReadOnlyList<ItrEmpresaResumo>> GetEmpresasAsync(
        string? busca, int limite, CancellationToken ct = default);

    /// <summary>
    /// Historico do ledger itr.Importacao (mais recente primeiro), com filtros opcionais
    /// por tipo e ano. Limita a <paramref name="limite"/> linhas.
    /// </summary>
    Task<IReadOnlyList<ItrImportacaoHistorico>> GetImportacoesAsync(
        string? tipo, int? ano, int limite, CancellationToken ct = default);
}
