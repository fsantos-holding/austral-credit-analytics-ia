using AustralCreditAnalytics.Api.Models;

namespace AustralCreditAnalytics.Api.Services;

public interface IConnectionConfigStore
{
    /// <summary>Indica se ja existe uma configuracao salva.</summary>
    bool IsConfigured { get; }

    /// <summary>Persiste a configuracao cifrada em disco.</summary>
    void Save(ConnectionConfig config);

    /// <summary>Le a configuracao salva (com a senha em texto plano) para uso interno. Null se nao configurada.</summary>
    ConnectionConfig? Load();

    /// <summary>Le a configuracao salva mascarando a senha, para exibicao na UI.</summary>
    ConnectionConfigView? LoadMasked();
}
