namespace AustralCreditAnalytics.Api.Models.Ia;

/// <summary>Payload de entrada da configuracao de IA (tela de Configuracoes).</summary>
public class SalvarIaConfigRequest
{
    public string ProvedorAtivo { get; set; } = "openai";
    public bool WebSearch { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Quando nula/vazia, mantem a chave ja salva (nao sobrescreve).</summary>
    public string? OpenAiApiKey { get; set; }
    public string? OpenAiModelo { get; set; }

    public string? AnthropicApiKey { get; set; }
    public string? AnthropicModelo { get; set; }

    /// <summary>Provedores compativeis com a OpenAI (Together, Groq, OpenRouter, etc.).</summary>
    public List<SalvarIaProvedorPersonalizado>? Personalizados { get; set; }
}

/// <summary>Item de provedor personalizado no payload de configuracao.</summary>
public class SalvarIaProvedorPersonalizado
{
    /// <summary>Id existente (vazio cria um novo provedor).</summary>
    public string? Id { get; set; }
    public string? Nome { get; set; }
    public string? BaseUrl { get; set; }

    /// <summary>Quando nula/vazia, mantem a chave ja salva (nao sobrescreve).</summary>
    public string? ApiKey { get; set; }
    public string? Modelo { get; set; }

    /// <summary>Marca o provedor para remocao.</summary>
    public bool Remover { get; set; }
}
