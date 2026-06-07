using AustralCreditAnalytics.Api.Models.Dfp;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Importa um CSV FRE (Formulario de Referencia / CVM) para o schema [fre], isolado de
/// DFP/ITR. Mesma assinatura de <see cref="IDfpImportService"/>/<see cref="IItrImportService"/>
/// para permitir despacho generico pelo gerenciador de jobs. Suporta todos os modelos do
/// <see cref="FreModeloRegistry"/>.
/// </summary>
public interface IFreImportService
{
    /// <summary>
    /// Le o CSV FRE em disco (separador ';', encoding ISO-8859-1, cabecalho nativo da CVM)
    /// e grava em massa, em lotes, na tabela fre.* do modelo. Extrai o ano do nome do arquivo,
    /// remove o escopo (Ano) existente e registra no ledger <c>fre.Importacao</c>. A base FRE
    /// nao tem GRUPO_DFP, portanto nao distingue conjunto (CON/IND).
    /// </summary>
    Task<DfpImportResult> ImportarArquivoAsync(
        string tipo,
        string caminhoArquivo,
        string nomeArquivo,
        string? usuario,
        IProgress<(long bytes, int lidas, int importadas, int removidas, string? conjunto, int? ano)>? progresso = null,
        CancellationToken ct = default);
}
