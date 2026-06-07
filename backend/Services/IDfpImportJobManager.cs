using AustralCreditAnalytics.Api.Models.Dfp;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Gerencia jobs de importacao de CSV DFP em segundo plano (em memoria). Cada job
/// processa um arquivo temporario em lotes, reportando progresso e suportando
/// cancelamento cooperativo.
/// </summary>
public interface IDfpImportJobManager
{
    /// <summary>
    /// Cria um job e dispara o processamento em segundo plano do arquivo temporario
    /// <paramref name="caminhoTemp"/>. <paramref name="baseDados"/> seleciona o dataset
    /// ("DFP" anual ou "ITR" trimestral). <paramref name="modelo"/> e o nome amigavel da
    /// demonstracao e <paramref name="usuario"/> a identidade que iniciou a importacao.
    /// Retorna o job recem-criado (status Pendente).
    /// </summary>
    DfpImportJob Iniciar(
        string baseDados, string tipo, string modelo, string tabela, string arquivo, string caminhoTemp,
        long bytesTotais, string? usuario);

    /// <summary>Obtem o job pelo id, ou null se inexistente/ja descartado.</summary>
    DfpImportJob? Obter(string id);

    /// <summary>
    /// Solicita o cancelamento cooperativo do job. Retorna false se o job nao existe.
    /// Os lotes ja gravados permanecem (commit incremental).
    /// </summary>
    bool Cancelar(string id);
}
