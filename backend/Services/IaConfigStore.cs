using System.Text.Json;
using AustralCreditAnalytics.Api.Models.Ia;
using Microsoft.AspNetCore.DataProtection;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Persiste a configuracao de IA cifrada em App_Data/iaconfig.dat usando
/// ASP.NET Data Protection. As chaves nunca sao gravadas nem retornadas em
/// texto plano fora do uso interno (servico de analise). O appsettings (secao
/// "Ia") serve de fallback quando a UI nao informou uma chave.
/// </summary>
public class IaConfigStore : IIaConfigStore
{
    private const string ProtectorPurpose = "AustralCreditAnalytics.IaConfig.v1";

    private readonly IDataProtector _protector;
    private readonly IConfiguration _configuration;
    private readonly string _filePath;
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public IaConfigStore(IDataProtectionProvider provider, IWebHostEnvironment env, IConfiguration configuration)
    {
        _protector = provider.CreateProtector(ProtectorPurpose);
        _configuration = configuration;

        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "iaconfig.dat");
    }

    public void Save(SalvarIaConfigRequest request)
    {
        // Merge: chave vazia mantem a chave ja salva (evita perder o segredo ao salvar a tela).
        var atual = LoadStored() ?? new IaConfig();

        atual.ProvedorAtivo = NormalizeProvider(request.ProvedorAtivo);
        atual.WebSearch = request.WebSearch;
        atual.TimeoutSeconds = request.TimeoutSeconds > 0 ? request.TimeoutSeconds : 60;

        atual.OpenAI ??= new IaProvedorConfig();
        atual.Anthropic ??= new IaProvedorConfig();

        if (!string.IsNullOrWhiteSpace(request.OpenAiModelo))
            atual.OpenAI.Modelo = request.OpenAiModelo.Trim();
        if (!string.IsNullOrWhiteSpace(request.OpenAiApiKey))
            atual.OpenAI.ApiKey = request.OpenAiApiKey.Trim();

        if (!string.IsNullOrWhiteSpace(request.AnthropicModelo))
            atual.Anthropic.Modelo = request.AnthropicModelo.Trim();
        if (!string.IsNullOrWhiteSpace(request.AnthropicApiKey))
            atual.Anthropic.ApiKey = request.AnthropicApiKey.Trim();

        atual.Personalizados = MergePersonalizados(atual.Personalizados, request.Personalizados);

        // Se o provedor ativo apontava para um personalizado removido, volta ao OpenAI.
        if (!IsProviderValid(atual.ProvedorAtivo, atual.Personalizados))
            atual.ProvedorAtivo = "openai";

        var json = JsonSerializer.Serialize(atual, JsonOpts);
        var cipher = _protector.Protect(json);

        lock (_lock)
        {
            File.WriteAllText(_filePath, cipher);
        }
    }

    public IaConfig LoadEffective()
    {
        var fromSettings = LoadFromAppSettings();
        var stored = LoadStored();

        if (stored is null)
            return fromSettings;

        // A config salva pela UI tem prioridade; chaves vazias caem no appsettings.
        var personalizados = (stored.Personalizados ?? new List<IaProvedorPersonalizado>())
            .Where(p => !string.IsNullOrWhiteSpace(p?.Id))
            .ToList();

        var eff = new IaConfig
        {
            ProvedorAtivo = NormalizeProvider(stored.ProvedorAtivo),
            WebSearch = stored.WebSearch,
            TimeoutSeconds = stored.TimeoutSeconds > 0 ? stored.TimeoutSeconds : fromSettings.TimeoutSeconds,
            OpenAI = new IaProvedorConfig
            {
                ApiKey = FirstNonEmpty(stored.OpenAI?.ApiKey, fromSettings.OpenAI.ApiKey),
                Modelo = FirstNonEmpty(stored.OpenAI?.Modelo, fromSettings.OpenAI.Modelo),
            },
            Anthropic = new IaProvedorConfig
            {
                ApiKey = FirstNonEmpty(stored.Anthropic?.ApiKey, fromSettings.Anthropic.ApiKey),
                Modelo = FirstNonEmpty(stored.Anthropic?.Modelo, fromSettings.Anthropic.Modelo),
            },
            Personalizados = personalizados,
        };

        // Provedor ativo invalido (ex.: personalizado removido) cai no OpenAI.
        if (!IsProviderValid(eff.ProvedorAtivo, eff.Personalizados))
            eff.ProvedorAtivo = "openai";

        return eff;
    }

    public IaConfigView LoadMasked()
    {
        var eff = LoadEffective();
        var openConf = !string.IsNullOrWhiteSpace(eff.OpenAI.ApiKey);
        var anthConf = !string.IsNullOrWhiteSpace(eff.Anthropic.ApiKey);

        var personalizados = eff.Personalizados.Select(p =>
        {
            var conf = !string.IsNullOrWhiteSpace(p.ApiKey);
            return new IaProvedorPersonalizadoView
            {
                Id = p.Id,
                Nome = p.Nome,
                BaseUrl = p.BaseUrl,
                Modelo = p.Modelo,
                Configurada = conf,
                Mask = conf ? new string('\u2022', 16) : null,
            };
        }).ToList();

        return new IaConfigView
        {
            ProvedorAtivo = eff.ProvedorAtivo,
            WebSearch = eff.WebSearch,
            TimeoutSeconds = eff.TimeoutSeconds,
            OpenAiModelo = eff.OpenAI.Modelo,
            OpenAiConfigurada = openConf,
            OpenAiMask = openConf ? new string('\u2022', 16) : null,
            AnthropicModelo = eff.Anthropic.Modelo,
            AnthropicConfigurada = anthConf,
            AnthropicMask = anthConf ? new string('\u2022', 16) : null,
            Personalizados = personalizados,
            IsConfigured = openConf || anthConf || personalizados.Any(p => p.Configurada),
        };
    }

    private IaConfig? LoadStored()
    {
        lock (_lock)
        {
            if (!File.Exists(_filePath))
                return null;

            try
            {
                var cipher = File.ReadAllText(_filePath);
                var json = _protector.Unprotect(cipher);
                return JsonSerializer.Deserialize<IaConfig>(json, JsonOpts);
            }
            catch
            {
                // Arquivo corrompido ou chave de protecao trocada: trata como nao configurado.
                return null;
            }
        }
    }

    private IaConfig LoadFromAppSettings()
    {
        var section = _configuration.GetSection("Ia");
        var cfg = section.Get<IaConfig>() ?? new IaConfig();
        cfg.ProvedorAtivo = NormalizeProvider(cfg.ProvedorAtivo);
        cfg.OpenAI ??= new IaProvedorConfig { Modelo = "gpt-4o" };
        cfg.Anthropic ??= new IaProvedorConfig { Modelo = "claude-sonnet-4-6" };
        cfg.Personalizados ??= new List<IaProvedorPersonalizado>();
        if (cfg.TimeoutSeconds <= 0) cfg.TimeoutSeconds = 60;
        return cfg;
    }

    /// <summary>
    /// Normaliza o identificador do provedor ativo: nativos viram minusculo
    /// ("openai"/"anthropic"); qualquer outro valor e tratado como Id de
    /// provedor personalizado e preservado.
    /// </summary>
    private static string NormalizeProvider(string? p)
    {
        var v = (p ?? string.Empty).Trim();
        if (v.Length == 0) return "openai";
        var lower = v.ToLowerInvariant();
        if (lower == "anthropic") return "anthropic";
        if (lower == "openai") return "openai";
        return v;
    }

    private static bool IsProviderValid(string? provedor, List<IaProvedorPersonalizado> personalizados)
    {
        var v = (provedor ?? string.Empty).Trim();
        if (v.Length == 0) return false;
        if (v.Equals("openai", StringComparison.OrdinalIgnoreCase)) return true;
        if (v.Equals("anthropic", StringComparison.OrdinalIgnoreCase)) return true;
        return personalizados.Any(p => string.Equals(p.Id, v, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Mescla a lista de provedores personalizados preservando segredos: chave
    /// vazia mantem a anterior, itens marcados com Remover saem da lista e novos
    /// itens (sem Id) recebem um Id gerado. Itens sem nome sao ignorados.
    /// </summary>
    private static List<IaProvedorPersonalizado> MergePersonalizados(
        List<IaProvedorPersonalizado>? atuais,
        List<SalvarIaProvedorPersonalizado>? requisitados)
    {
        var existentes = (atuais ?? new List<IaProvedorPersonalizado>())
            .Where(p => !string.IsNullOrWhiteSpace(p?.Id))
            .ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);

        // null = a UI nao enviou a lista; nada muda.
        if (requisitados is null)
            return existentes.Values.ToList();

        var resultado = new List<IaProvedorPersonalizado>();
        foreach (var item in requisitados)
        {
            if (item is null) continue;
            if (item.Remover) continue;

            var nome = (item.Nome ?? string.Empty).Trim();
            var baseUrl = (item.BaseUrl ?? string.Empty).Trim();
            var modelo = (item.Modelo ?? string.Empty).Trim();
            var apiKey = (item.ApiKey ?? string.Empty).Trim();

            IaProvedorPersonalizado alvo;
            if (!string.IsNullOrWhiteSpace(item.Id) && existentes.TryGetValue(item.Id, out var atual))
                alvo = atual;
            else
                alvo = new IaProvedorPersonalizado { Id = SanitizeId(item.Id) ?? Guid.NewGuid().ToString("n") };

            // Provedor sem nome nem URL e descartado (linha em branco na UI).
            if (nome.Length == 0 && baseUrl.Length == 0 && string.IsNullOrWhiteSpace(alvo.ApiKey))
                continue;

            alvo.Nome = nome.Length > 0 ? nome : alvo.Nome;
            if (baseUrl.Length > 0) alvo.BaseUrl = baseUrl;
            if (modelo.Length > 0) alvo.Modelo = modelo;
            if (apiKey.Length > 0) alvo.ApiKey = apiKey; // vazio preserva o segredo

            resultado.Add(alvo);
        }

        return resultado;
    }

    /// <summary>Aceita um Id informado pela UI apenas se for seguro ([a-z0-9-_], ate 64).</summary>
    private static string? SanitizeId(string? id)
    {
        var v = (id ?? string.Empty).Trim();
        if (v.Length is < 1 or > 64) return null;
        foreach (var ch in v)
        {
            if (!(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_'))
                return null;
        }
        return v;
    }

    private static string? FirstNonEmpty(string? a, string? b)
        => !string.IsNullOrWhiteSpace(a) ? a : (!string.IsNullOrWhiteSpace(b) ? b : null);
}
