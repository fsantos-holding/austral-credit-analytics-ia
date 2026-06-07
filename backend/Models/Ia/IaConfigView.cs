namespace AustralCreditAnalytics.Api.Models.Ia;

/// <summary>
/// Projecao segura da configuracao de IA para a tela de Configuracoes.
/// As chaves nunca retornam em texto plano: apenas flags de "configurada"
/// e uma mascara de exibicao.
/// </summary>
public class IaConfigView
{
    public string ProvedorAtivo { get; set; } = "openai";
    public bool WebSearch { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 60;

    public string? OpenAiModelo { get; set; }
    public bool OpenAiConfigurada { get; set; }
    public string? OpenAiMask { get; set; }

    public string? AnthropicModelo { get; set; }
    public bool AnthropicConfigurada { get; set; }
    public string? AnthropicMask { get; set; }

    /// <summary>Provedores personalizados (chaves mascaradas).</summary>
    public List<IaProvedorPersonalizadoView> Personalizados { get; set; } = new();

    /// <summary>True quando ao menos um provedor possui chave (UI ou appsettings).</summary>
    public bool IsConfigured { get; set; }
}

/// <summary>Projecao segura de um provedor personalizado para a tela de Configuracoes.</summary>
public class IaProvedorPersonalizadoView
{
    public string Id { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? BaseUrl { get; set; }
    public string? Modelo { get; set; }
    public bool Configurada { get; set; }
    public string? Mask { get; set; }
}
