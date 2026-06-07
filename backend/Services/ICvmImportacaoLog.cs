namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Registra o ciclo de vida de cada importacao CVM no ledger informado
/// (<paramref name="ledgerTabela"/>, ex.: dfp.Importacao ou itr.Importacao): abre a
/// linha (status Processando), atualiza contagens parciais e finaliza com o status
/// terminal. O nucleo de import (<see cref="CvmCsvImporter"/>) usa esta abstracao
/// para nao depender de uma base especifica.
/// </summary>
public interface ICvmImportacaoLog
{
    /// <summary>
    /// Abre uma linha no ledger (status Processando) e retorna o <c>Id</c> gerado, usado
    /// como <c>ImportacaoId</c> nas linhas de dados carregadas. Retorna null se o registro
    /// falhar (a importacao prossegue sem rastreio).
    /// </summary>
    Task<long?> IniciarAsync(
        string ledgerTabela, string tipo, string tabela, string? conjunto, int? ano,
        string arquivo, string? usuario, CancellationToken ct = default);

    /// <summary>Atualiza as contagens parciais (removidas/lidas/importadas) da importacao.</summary>
    Task AtualizarAsync(
        string ledgerTabela, long id, int linhasRemovidas, int linhasLidas, int linhasImportadas,
        CancellationToken ct = default);

    /// <summary>
    /// Finaliza a importacao: grava status terminal, mensagem, contagens finais,
    /// conjunto detectado e a data de conclusao.
    /// </summary>
    Task FinalizarAsync(
        string ledgerTabela, long id, string status, string? mensagem,
        int linhasRemovidas, int linhasLidas, int linhasImportadas, string? conjunto,
        CancellationToken ct = default);
}
