using AustralCreditAnalytics.Api.Models.Ia;

namespace AustralCreditAnalytics.Api.Services;

public interface IIaConfigStore
{
    /// <summary>Persiste a configuracao de IA cifrada em disco (merge com a chave existente).</summary>
    void Save(SalvarIaConfigRequest request);

    /// <summary>
    /// Configuracao efetiva para uso interno (com chaves em texto plano):
    /// a chave salva pela UI tem prioridade; se vazia, cai no appsettings.
    /// </summary>
    IaConfig LoadEffective();

    /// <summary>Projecao segura (chaves mascaradas) para a tela de Configuracoes.</summary>
    IaConfigView LoadMasked();
}
