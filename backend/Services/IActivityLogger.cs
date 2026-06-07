namespace AustralCreditAnalytics.Api.Services;

/// <summary>Registro de atividades dos usuarios em seg.LogAtividade (auditoria).</summary>
public interface IActivityLogger
{
    /// <summary>Grava um evento de atividade. Tolerante a falha: nunca lanca.</summary>
    Task LogAsync(
        long? usuarioId,
        string? login,
        string acao,
        string? entidade = null,
        string? detalhe = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default);
}
