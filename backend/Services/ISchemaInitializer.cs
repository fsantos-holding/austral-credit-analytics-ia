namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Garante que o schema [seg] e as tabelas internas existam, aplicando os
/// scripts de migracao pendentes de forma idempotente.
/// </summary>
public interface ISchemaInitializer
{
    /// <summary>
    /// Aplica as migracoes pendentes. Idempotente: pode ser chamado no startup e
    /// apos salvar a configuracao; so executa uma vez por processo (apos sucesso).
    /// Nunca lanca: falhas sao logadas para nao derrubar a aplicacao.
    /// </summary>
    Task EnsureInitializedAsync(CancellationToken ct = default);
}
