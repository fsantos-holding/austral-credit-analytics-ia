namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Registra o ciclo de vida de cada importacao DFP no ledger <c>dfp.Importacao</c>:
/// abre a linha (status Processando) antes da carga, atualiza as contagens durante o
/// processamento e finaliza com o status terminal (Concluido/Falha/Cancelado).
/// </summary>
public interface IDfpImportacaoLog
{
    /// <summary>
    /// Abre uma linha no ledger (status Processando) e retorna o <c>Id</c> gerado, usado
    /// como <c>ImportacaoId</c> nas linhas de dados carregadas. Retorna null se o registro
    /// falhar (a importacao prossegue sem rastreio).
    /// </summary>
    Task<long?> IniciarAsync(
        string tipo, string tabela, string? conjunto, int? ano, string arquivo, string? usuario,
        CancellationToken ct = default);

    /// <summary>Atualiza as contagens parciais (removidas/lidas/importadas) da importacao.</summary>
    Task AtualizarAsync(
        long id, int linhasRemovidas, int linhasLidas, int linhasImportadas,
        CancellationToken ct = default);

    /// <summary>
    /// Finaliza a importacao: grava status terminal, mensagem, contagens finais,
    /// conjunto detectado e a data de conclusao.
    /// </summary>
    Task FinalizarAsync(
        long id, string status, string? mensagem,
        int linhasRemovidas, int linhasLidas, int linhasImportadas, string? conjunto,
        CancellationToken ct = default);
}
