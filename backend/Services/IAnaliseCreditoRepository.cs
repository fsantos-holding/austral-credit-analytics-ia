using AustralCreditAnalytics.Api.Models.Credito;

namespace AustralCreditAnalytics.Api.Services;

public interface IAnaliseCreditoRepository
{
    /// <summary>
    /// Consulta as analises de credito usando a query configurada em Credito:Query,
    /// filtrando por periodo (datas YYYYMMDD) e status opcional.
    /// </summary>
    Task<IReadOnlyList<AnaliseCreditoRecord>> GetAsync(
        string? dataInicial, string? dataFinal, string? status, CancellationToken ct = default);
}
