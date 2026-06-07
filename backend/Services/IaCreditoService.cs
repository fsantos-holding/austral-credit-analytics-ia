using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AustralCreditAnalytics.Api.Models.Ia;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Valida a conectividade/chave do agente de IA configurado.
/// Suporta OpenAI (Responses API), Anthropic (Messages API) e provedores
/// compativeis com a API da OpenAI via chat/completions (Together, Groq,
/// OpenRouter, DeepSeek, Mistral, Ollama, etc.).
/// </summary>
public class IaCreditoService : IIaCreditoService
{
    private readonly IIaConfigStore _configStore;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<IaCreditoService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public IaCreditoService(
        IIaConfigStore configStore,
        IHttpClientFactory httpFactory,
        ILogger<IaCreditoService> logger)
    {
        _configStore = configStore;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<(bool ok, string mensagem)> TestarAsync(CancellationToken ct = default)
    {
        var cfg = _configStore.LoadEffective();
        try
        {
            var prov = ResolveProvider(cfg);
            var prompt = "Responda apenas com o JSON {\"ok\":true}.";
            _ = prov.Kind switch
            {
                "anthropic" => await CallAnthropicAsync(cfg, prov.ApiKey, prov.Modelo, prompt, ct, useWebSearch: false),
                "custom" => await CallOpenAiCompatibleAsync(cfg, prov, prompt, ct, useWebSearch: false),
                _ => await CallOpenAiAsync(cfg, prov.ApiKey, prov.Modelo, prompt, ct, useWebSearch: false),
            };
            return (true, $"Conexao com {prov.Nome} ({prov.Modelo}) validada.");
        }
        catch (IaCreditoException ex)
        {
            return (false, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao testar provedor de IA");
            return (false, "Falha ao validar a chave de IA. Verifique a chave e o modelo.");
        }
    }

    /// <summary>Provedor resolvido para a chamada (com chave em texto plano).</summary>
    private sealed record ResolvedProvider(string Kind, string Nome, string ApiKey, string Modelo, string? BaseUrl);

    private static ResolvedProvider ResolveProvider(IaConfig cfg)
    {
        var ativo = (cfg.ProvedorAtivo ?? "openai").Trim();

        if (ativo.Equals("anthropic", StringComparison.OrdinalIgnoreCase))
        {
            var key = cfg.Anthropic?.ApiKey;
            if (string.IsNullOrWhiteSpace(key))
                throw new IaCreditoException("Chave da Anthropic nao configurada. Configure em Configuracoes.");
            return new ResolvedProvider("anthropic", "anthropic", key, cfg.Anthropic?.Modelo ?? "claude-sonnet-4-6", null);
        }

        if (!ativo.Equals("openai", StringComparison.OrdinalIgnoreCase))
        {
            var custom = cfg.Personalizados?
                .FirstOrDefault(p => string.Equals(p.Id, ativo, StringComparison.OrdinalIgnoreCase));
            if (custom is null)
                throw new IaCreditoException("Provedor de IA selecionado nao encontrado. Revise em Configuracoes.");

            var nome = string.IsNullOrWhiteSpace(custom.Nome) ? "provedor personalizado" : custom.Nome.Trim();
            if (string.IsNullOrWhiteSpace(custom.BaseUrl))
                throw new IaCreditoException($"URL base do provedor \"{nome}\" nao configurada. Configure em Configuracoes.");
            if (string.IsNullOrWhiteSpace(custom.ApiKey))
                throw new IaCreditoException($"Chave do provedor \"{nome}\" nao configurada. Configure em Configuracoes.");
            if (string.IsNullOrWhiteSpace(custom.Modelo))
                throw new IaCreditoException($"Modelo do provedor \"{nome}\" nao configurado. Configure em Configuracoes.");

            return new ResolvedProvider("custom", nome, custom.ApiKey, custom.Modelo.Trim(), custom.BaseUrl.Trim());
        }

        var okey = cfg.OpenAI?.ApiKey;
        if (string.IsNullOrWhiteSpace(okey))
            throw new IaCreditoException("Chave da OpenAI nao configurada. Configure em Configuracoes.");
        return new ResolvedProvider("openai", "openai", okey, cfg.OpenAI?.Modelo ?? "gpt-4o", null);
    }

    private async Task<string> CallOpenAiAsync(
        IaConfig cfg, string apiKey, string modelo, string prompt, CancellationToken ct, bool useWebSearch = true)
    {
        var body = new Dictionary<string, object?>
        {
            ["model"] = modelo,
            ["input"] = prompt,
        };
        if (useWebSearch && cfg.WebSearch)
            body["tools"] = new object[] { new { type = "web_search" } };

        using var http = CreateClient(cfg);
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        req.Content = JsonContent(body);

        using var res = await http.SendAsync(req, ct);
        var payload = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new IaCreditoException(MapHttpError("OpenAI", res.StatusCode, payload));

        return ExtractOpenAiText(payload);
    }

    private async Task<string> CallAnthropicAsync(
        IaConfig cfg, string apiKey, string modelo, string prompt, CancellationToken ct, bool useWebSearch = true)
    {
        var body = new Dictionary<string, object?>
        {
            ["model"] = modelo,
            ["max_tokens"] = 1500,
            ["messages"] = new object[]
            {
                new { role = "user", content = prompt },
            },
        };
        if (useWebSearch && cfg.WebSearch)
        {
            body["tools"] = new object[]
            {
                new { type = "web_search_20250305", name = "web_search", max_uses = 5 },
            };
        }

        using var http = CreateClient(cfg);
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        req.Headers.Add("x-api-key", apiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");
        req.Content = JsonContent(body);

        using var res = await http.SendAsync(req, ct);
        var payload = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new IaCreditoException(MapHttpError("Anthropic", res.StatusCode, payload));

        return ExtractAnthropicText(payload);
    }

    /// <summary>
    /// Chama um provedor compativel com a API da OpenAI (Together, Groq, OpenRouter,
    /// DeepSeek, Mistral, Ollama, etc.) no endpoint <c>{BaseUrl}/chat/completions</c>.
    /// A maioria desses provedores nao expoe a tool de web search; por seguranca,
    /// nao enviamos ferramentas para nao quebrar a requisicao.
    /// </summary>
    private async Task<string> CallOpenAiCompatibleAsync(
        IaConfig cfg, ResolvedProvider prov, string prompt, CancellationToken ct, bool useWebSearch = true)
    {
        _ = useWebSearch; // ferramentas nao sao enviadas a provedores compativeis
        var url = BuildChatCompletionsUrl(prov.BaseUrl!);
        var body = new Dictionary<string, object?>
        {
            ["model"] = prov.Modelo,
            ["messages"] = new object[]
            {
                new { role = "user", content = prompt },
            },
        };

        using var http = CreateClient(cfg);
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", prov.ApiKey);
        req.Content = JsonContent(body);

        using var res = await http.SendAsync(req, ct);
        var payload = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new IaCreditoException(MapHttpError(prov.Nome, res.StatusCode, payload));

        return ExtractChatCompletionsText(payload);
    }

    /// <summary>
    /// Normaliza a URL base para o endpoint de chat completions, tolerando que o
    /// usuario informe a raiz (https://api.together.xyz), a versao (.../v1) ou a
    /// rota completa (.../v1/chat/completions).
    /// </summary>
    private static string BuildChatCompletionsUrl(string baseUrl)
    {
        var url = baseUrl.Trim().TrimEnd('/');
        if (url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            return url;
        return url + "/chat/completions";
    }

    /// <summary>Extrai o texto de choices[0].message.content da Chat Completions API.</summary>
    private static string ExtractChatCompletionsText(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        if (root.TryGetProperty("choices", out var choices)
            && choices.ValueKind == JsonValueKind.Array
            && choices.GetArrayLength() > 0)
        {
            var first = choices[0];
            if (first.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var content))
            {
                if (content.ValueKind == JsonValueKind.String)
                    return content.GetString() ?? payload;

                // Alguns provedores retornam content como array de blocos {type,text}.
                if (content.ValueKind == JsonValueKind.Array)
                {
                    var sb = new StringBuilder();
                    foreach (var c in content.EnumerateArray())
                    {
                        if (c.TryGetProperty("text", out var txt))
                            sb.Append(txt.GetString());
                    }
                    if (sb.Length > 0) return sb.ToString();
                }
            }
        }

        return payload;
    }

    private HttpClient CreateClient(IaConfig cfg)
    {
        var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(cfg.TimeoutSeconds > 0 ? cfg.TimeoutSeconds : 60);
        return http;
    }

    private static StringContent JsonContent(object body)
        => new(JsonSerializer.Serialize(body, JsonOpts), Encoding.UTF8, "application/json");

    /// <summary>Concatena os blocos de texto (output_text) da Responses API da OpenAI.</summary>
    private static string ExtractOpenAiText(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var sb = new StringBuilder();

        if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in output.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var contentArr) || contentArr.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (var c in contentArr.EnumerateArray())
                {
                    if (c.TryGetProperty("type", out var t) && t.GetString() == "output_text"
                        && c.TryGetProperty("text", out var txt))
                    {
                        sb.Append(txt.GetString());
                    }
                }
            }
        }

        return sb.Length > 0 ? sb.ToString() : payload;
    }

    /// <summary>Concatena os blocos type=="text" da resposta da Anthropic.</summary>
    private static string ExtractAnthropicText(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var sb = new StringBuilder();

        if (root.TryGetProperty("content", out var contentArr) && contentArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in contentArr.EnumerateArray())
            {
                if (c.TryGetProperty("type", out var t) && t.GetString() == "text"
                    && c.TryGetProperty("text", out var txt))
                {
                    sb.Append(txt.GetString());
                }
            }
        }

        return sb.Length > 0 ? sb.ToString() : payload;
    }

    private static string MapHttpError(string provedor, System.Net.HttpStatusCode status, string payload)
    {
        var detail = Truncate(payload, 300);
        return status switch
        {
            System.Net.HttpStatusCode.Unauthorized => $"Chave de API da {provedor} invalida ou sem permissao.",
            System.Net.HttpStatusCode.TooManyRequests => $"Limite de requisicoes da {provedor} atingido. Tente novamente em instantes.",
            _ => $"Falha na chamada a {provedor} (HTTP {(int)status}). {detail}",
        };
    }

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "...";
}
