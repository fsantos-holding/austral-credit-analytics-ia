namespace AustralCreditAnalytics.Api.Models.Ia;

/// <summary>
/// Configuracao dos provedores de IA. Alem dos nativos (OpenAI/Anthropic),
/// suporta provedores personalizados compativeis com a API da OpenAI
/// (ex.: Together, Groq, OpenRouter, DeepSeek, Mistral, Ollama), informando
/// URL base, chave e modelo. As chaves sao persistidas cifradas (Data Protection)
/// e nunca retornadas em texto plano fora do uso interno (servico de analise).
/// O appsettings serve de fallback para os provedores nativos.
/// </summary>
public class IaConfig
{
    /// <summary>"openai", "anthropic" ou o Id de um provedor personalizado.</summary>
    public string ProvedorAtivo { get; set; } = "openai";

    /// <summary>Habilita a busca na internet (web search) do provedor.</summary>
    public bool WebSearch { get; set; } = true;

    /// <summary>Timeout (segundos) das chamadas ao provedor de IA.</summary>
    public int TimeoutSeconds { get; set; } = 60;

    public IaProvedorConfig OpenAI { get; set; } = new() { Modelo = "gpt-4o" };
    public IaProvedorConfig Anthropic { get; set; } = new() { Modelo = "claude-sonnet-4-6" };

    /// <summary>Provedores compativeis com a API da OpenAI (chat/completions).</summary>
    public List<IaProvedorPersonalizado> Personalizados { get; set; } = new();
}

/// <summary>Chave + modelo de um provedor nativo.</summary>
public class IaProvedorConfig
{
    public string? ApiKey { get; set; }
    public string? Modelo { get; set; }
}

/// <summary>
/// Provedor de IA personalizado, compativel com a API da OpenAI
/// (endpoint <c>{BaseUrl}/chat/completions</c>).
/// </summary>
public class IaProvedorPersonalizado
{
    /// <summary>Identificador estavel do provedor (gerado no servidor).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("n");

    /// <summary>Nome amigavel exibido na UI (ex.: "Together").</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>URL base da API (ex.: https://api.together.xyz/v1).</summary>
    public string? BaseUrl { get; set; }

    public string? ApiKey { get; set; }
    public string? Modelo { get; set; }
}
