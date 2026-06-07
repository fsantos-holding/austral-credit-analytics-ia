using AustralCreditAnalytics.Api.Models.Dfp;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>Importa um CSV da CVM (layout DFP) para a tabela correspondente.</summary>
public interface IDfpImportService
{
    /// <summary>
    /// Le o CSV em disco (separador ';', encoding ISO-8859-1, cabecalho com os nomes
    /// nativos da CVM) e grava em massa, em lotes, na tabela da demonstracao
    /// <paramref name="tipo"/>. Extrai o ano de referencia do nome do arquivo, detecta o
    /// conjunto (Consolidado/Individual) pelo GRUPO_DFP, remove o escopo (tipo+conjunto+ano)
    /// ja existente antes da carga e registra a importacao no ledger <c>dfp.Importacao</c>
    /// associando cada linha gravada via <c>ImportacaoId</c>. Reporta o progresso (bytes,
    /// linhas lidas/importadas/removidas, conjunto e ano) via <paramref name="progresso"/>
    /// e respeita o cancelamento cooperativo de <paramref name="ct"/> (os lotes ja gravados
    /// permanecem). <paramref name="usuario"/> e a identidade que iniciou a importacao.
    /// </summary>
    Task<DfpImportResult> ImportarArquivoAsync(
        string tipo,
        string caminhoArquivo,
        string nomeArquivo,
        string? usuario,
        IProgress<(long bytes, int lidas, int importadas, int removidas, string? conjunto, int? ano)>? progresso = null,
        CancellationToken ct = default);
}
