using AustralCreditAnalytics.Api.Models.Dfp;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Demonstracao resolvida + tabela destino efetiva. O layout de colunas
/// (<see cref="Demonstracao"/>) e compartilhado entre os datasets; muda apenas a
/// <see cref="Tabela"/> de destino (ex.: dfp.Dre vs itr.Dre).
/// </summary>
public sealed record CvmDemonstracaoDestino(DfpDemonstracao Demonstracao, string Tabela);

/// <summary>
/// Descritor de um conjunto de dados da CVM importavel pelo nucleo
/// <see cref="CvmCsvImporter"/>. Isola o que difere entre DFP e ITR: o codigo da
/// base, a tabela do ledger de importacao e a regra que resolve um <c>tipo</c> de
/// demonstracao na demonstracao + tabela destino. O parsing/bulk-copy/delete de
/// escopo e identico para ambos.
/// </summary>
public sealed class CvmDataset
{
    /// <summary>Codigo da base: "DFP" ou "ITR".</summary>
    public required string Codigo { get; init; }

    /// <summary>Tabela do ledger de importacoes (ex.: dfp.Importacao / itr.Importacao).</summary>
    public required string LedgerTabela { get; init; }

    /// <summary>
    /// Resolve um <c>tipo</c> de demonstracao na demonstracao + tabela destino do
    /// dataset, ou null se o tipo nao for suportado por este dataset.
    /// </summary>
    public required Func<string, CvmDemonstracaoDestino?> Resolver { get; init; }
}

/// <summary>Datasets CVM suportados (fonte unica dos descritores DFP e ITR).</summary>
public static class CvmDatasets
{
    /// <summary>DFP (anual): todas as demonstracoes do registry, gravadas no schema [dfp].</summary>
    public static readonly CvmDataset Dfp = new()
    {
        Codigo = "DFP",
        LedgerTabela = "dfp.Importacao",
        Resolver = tipo =>
        {
            var dem = DfpDemonstracaoRegistry.Resolver(tipo);
            return dem is null ? null : new CvmDemonstracaoDestino(dem, dem.Tabela);
        },
    };

    /// <summary>
    /// ITR (trimestral): so DRE. Reaproveita o layout DRE do registry (identico ao
    /// da CVM), redirecionando a gravacao para itr.Dre.
    /// </summary>
    public static readonly CvmDataset Itr = new()
    {
        Codigo = "ITR",
        LedgerTabela = "itr.Importacao",
        Resolver = tipo =>
        {
            var dem = DfpDemonstracaoRegistry.Resolver(tipo);
            if (dem is null || !string.Equals(dem.Tipo, "DRE", StringComparison.OrdinalIgnoreCase))
                return null;
            return new CvmDemonstracaoDestino(dem, "itr.Dre");
        },
    };
}
