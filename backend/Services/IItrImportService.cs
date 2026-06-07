using AustralCreditAnalytics.Api.Models.Dfp;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Importa um CSV trimestral da CVM (layout DRE) para o schema [itr], isolado do DFP.
/// Mesma assinatura de <see cref="IDfpImportService"/> para permitir despacho generico
/// pelo gerenciador de jobs.
/// </summary>
public interface IItrImportService
{
    /// <summary>
    /// Le o CSV ITR em disco (separador ';', encoding ISO-8859-1, cabecalho nativo da CVM)
    /// e grava em massa, em lotes, em <c>itr.Dre</c>. Extrai o ano do nome do arquivo,
    /// detecta o conjunto (CON/IND) pelo GRUPO_DFP, remove o escopo (Ano+Conjunto) existente
    /// e registra no ledger <c>itr.Importacao</c>. So suporta o tipo DRE.
    /// </summary>
    Task<DfpImportResult> ImportarArquivoAsync(
        string tipo,
        string caminhoArquivo,
        string nomeArquivo,
        string? usuario,
        IProgress<(long bytes, int lidas, int importadas, int removidas, string? conjunto, int? ano)>? progresso = null,
        CancellationToken ct = default);
}
