using System.Text.Json.Serialization;

namespace AustralCreditAnalytics.Api.Models.Fre;

/// <summary>
/// Item do ranking de acionistas cruzados: um acionista (identificado por
/// <see cref="Chave"/>) com a contagem de empresas distintas em que aparece e os
/// percentuais agregados. As empresas associadas vem em <see cref="Empresas"/> para o
/// drilldown leve direto no ranking.
/// </summary>
public sealed class AcionistaRankingItem
{
    /// <summary>Chave de cruzamento: digitos do CPF/CNPJ ou, na ausencia, o nome normalizado.</summary>
    [JsonPropertyName("chave")]
    public string Chave { get; set; } = string.Empty;

    [JsonPropertyName("nome")]
    public string? Nome { get; set; }

    /// <summary>Tipo de pessoa predominante do acionista ("PF" | "PJ" | outros codigos da CVM).</summary>
    [JsonPropertyName("tipoPessoa")]
    public string? TipoPessoa { get; set; }

    /// <summary>Documento (CPF/CNPJ somente digitos) quando disponivel; vazio quando a chave e por nome.</summary>
    [JsonPropertyName("documento")]
    public string? Documento { get; set; }

    /// <summary>True quando a chave usou o fallback por nome (cruzamento aproximado).</summary>
    [JsonPropertyName("chavePorNome")]
    public bool ChavePorNome { get; set; }

    /// <summary>True quando o acionista e controlador em ao menos uma das empresas.</summary>
    [JsonPropertyName("controlador")]
    public bool Controlador { get; set; }

    [JsonPropertyName("qtdEmpresas")]
    public int QtdEmpresas { get; set; }

    [JsonPropertyName("percentualMedio")]
    public decimal? PercentualMedio { get; set; }

    [JsonPropertyName("percentualMaximo")]
    public decimal? PercentualMaximo { get; set; }

    [JsonPropertyName("empresas")]
    public IList<AcionistaEmpresaPosicao> Empresas { get; set; } = new List<AcionistaEmpresaPosicao>();
}

/// <summary>
/// Posicao de um acionista em uma empresa especifica (linha do drilldown). Reflete a
/// ultima Ano/Versao da empresa para o acionista.
/// </summary>
public sealed class AcionistaEmpresaPosicao
{
    [JsonPropertyName("cnpjNum")]
    public string CnpjNum { get; set; } = string.Empty;

    [JsonPropertyName("nomeCompanhia")]
    public string? NomeCompanhia { get; set; }

    [JsonPropertyName("ano")]
    public int? Ano { get; set; }

    [JsonPropertyName("versao")]
    public int? Versao { get; set; }

    [JsonPropertyName("controlador")]
    public bool Controlador { get; set; }

    [JsonPropertyName("percentualOrdinarias")]
    public decimal? PercentualOrdinarias { get; set; }

    [JsonPropertyName("percentualPreferenciais")]
    public decimal? PercentualPreferenciais { get; set; }

    [JsonPropertyName("percentualTotal")]
    public decimal? PercentualTotal { get; set; }
}

/// <summary>
/// Detalhe completo de um acionista cruzado (drilldown via /api/acionistas/{chave}/posicoes):
/// identificacao + a lista de todas as empresas e percentuais.
/// </summary>
public sealed class AcionistaDetalhe
{
    [JsonPropertyName("chave")]
    public string Chave { get; set; } = string.Empty;

    [JsonPropertyName("nome")]
    public string? Nome { get; set; }

    [JsonPropertyName("tipoPessoa")]
    public string? TipoPessoa { get; set; }

    [JsonPropertyName("documento")]
    public string? Documento { get; set; }

    [JsonPropertyName("chavePorNome")]
    public bool ChavePorNome { get; set; }

    [JsonPropertyName("qtdEmpresas")]
    public int QtdEmpresas { get; set; }

    [JsonPropertyName("empresas")]
    public IList<AcionistaEmpresaPosicao> Empresas { get; set; } = new List<AcionistaEmpresaPosicao>();
}

/// <summary>
/// Par de empresas que compartilham um ou mais acionistas em comum (aresta da rede de
/// conexoes), com a contagem de socios em comum.
/// </summary>
public sealed class AcionistaParEmpresas
{
    [JsonPropertyName("cnpjNumA")]
    public string CnpjNumA { get; set; } = string.Empty;

    [JsonPropertyName("empresaA")]
    public string? EmpresaA { get; set; }

    [JsonPropertyName("cnpjNumB")]
    public string CnpjNumB { get; set; } = string.Empty;

    [JsonPropertyName("empresaB")]
    public string? EmpresaB { get; set; }

    [JsonPropertyName("qtdSociosComum")]
    public int QtdSociosComum { get; set; }
}

/// <summary>
/// Pacote de insights do dashboard de acionistas cruzados: KPIs e series (reaproveitando
/// <see cref="FreKpi"/>/<see cref="FreSerie"/> da tela premium) + a tabela de pares de
/// empresas conectadas.
/// </summary>
public sealed class AcionistaInsights
{
    [JsonPropertyName("ano")]
    public int? Ano { get; set; }

    [JsonPropertyName("kpis")]
    public IList<FreKpi> Kpis { get; set; } = new List<FreKpi>();

    [JsonPropertyName("series")]
    public IList<FreSerie> Series { get; set; } = new List<FreSerie>();

    [JsonPropertyName("pares")]
    public IList<AcionistaParEmpresas> Pares { get; set; } = new List<AcionistaParEmpresas>();
}
