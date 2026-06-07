namespace AustralCreditAnalytics.Api.Services;

public interface IIaCreditoService
{
    /// <summary>Valida rapidamente a chave do provedor ativo (ping leve).</summary>
    Task<(bool ok, string mensagem)> TestarAsync(CancellationToken ct = default);
}

/// <summary>Erro de negocio da analise (config ausente, chave invalida, etc.).</summary>
public class IaCreditoException : Exception
{
    public IaCreditoException(string message) : base(message) { }
}
