using System.Text.Json.Serialization;

namespace AustralCreditAnalytics.Api.Models.Fre;

/// <summary>
/// Dossie FRE consolidado de uma companhia/ano: agregacoes por dominio (acionaria,
/// governanca, remuneracao, empregados, exterior, partes relacionadas) prontas para
/// alimentar KPIs e graficos da tela premium. A estrutura e generica (secoes -> kpis /
/// series / blocos) para evitar um DTO rigido por tabela. As tabelas detalhadas brutas
/// continuam vindo de <c>GET /api/fre/{cnpj}/modelo/{modelo}</c>.
/// </summary>
public sealed class FreDossie
{
    [JsonPropertyName("cnpjNum")]
    public string CnpjNum { get; set; } = string.Empty;

    [JsonPropertyName("denominacao")]
    public string? Denominacao { get; set; }

    [JsonPropertyName("ano")]
    public int? Ano { get; set; }

    [JsonPropertyName("versao")]
    public int? Versao { get; set; }

    [JsonPropertyName("secoes")]
    public IList<FreDossieSecao> Secoes { get; set; } = new List<FreDossieSecao>();
}

/// <summary>
/// Uma secao do dossie (ex.: "governanca"), agrupando KPIs, series para graficos e
/// blocos auxiliares (cards/listas). O frontend escolhe o melhor visual por item.
/// </summary>
public sealed class FreDossieSecao
{
    [JsonPropertyName("chave")]
    public string Chave { get; set; } = string.Empty;

    [JsonPropertyName("titulo")]
    public string Titulo { get; set; } = string.Empty;

    [JsonPropertyName("kpis")]
    public IList<FreKpi> Kpis { get; set; } = new List<FreKpi>();

    [JsonPropertyName("series")]
    public IList<FreSerie> Series { get; set; } = new List<FreSerie>();

    [JsonPropertyName("blocos")]
    public IList<FreDossieBloco> Blocos { get; set; } = new List<FreDossieBloco>();
}

/// <summary>
/// Indicador chave (KPI) com formato declarado para o frontend renderizar (numero,
/// moeda, percentual ou texto livre).
/// </summary>
public sealed class FreKpi
{
    [JsonPropertyName("rotulo")]
    public string Rotulo { get; set; } = string.Empty;

    [JsonPropertyName("valor")]
    public decimal? Valor { get; set; }

    /// <summary>"inteiro" | "moeda" | "percent" | "texto".</summary>
    [JsonPropertyName("formato")]
    public string Formato { get; set; } = "inteiro";

    [JsonPropertyName("texto")]
    public string? Texto { get; set; }

    [JsonPropertyName("icone")]
    public string? Icone { get; set; }
}

/// <summary>
/// Serie generica para grafico. Suporta series simples (1 valor por categoria) e
/// empilhadas/multi (varios <see cref="FreSerieValor"/>, cada um com 1 valor por categoria).
/// </summary>
public sealed class FreSerie
{
    [JsonPropertyName("chave")]
    public string Chave { get; set; } = string.Empty;

    [JsonPropertyName("titulo")]
    public string Titulo { get; set; } = string.Empty;

    /// <summary>"donut" | "barras" | "barras_empilhadas" | "barras_horizontais".</summary>
    [JsonPropertyName("tipo")]
    public string Tipo { get; set; } = "barras";

    [JsonPropertyName("categorias")]
    public IList<string> Categorias { get; set; } = new List<string>();

    [JsonPropertyName("valores")]
    public IList<FreSerieValor> Valores { get; set; } = new List<FreSerieValor>();
}

/// <summary>Uma sequencia de dados nomeada de uma <see cref="FreSerie"/> (1 valor por categoria).</summary>
public sealed class FreSerieValor
{
    [JsonPropertyName("rotulo")]
    public string Rotulo { get; set; } = string.Empty;

    [JsonPropertyName("dados")]
    public IList<decimal?> Dados { get; set; } = new List<decimal?>();
}

/// <summary>
/// Bloco auxiliar de uma secao (card ou mini-lista) para informacoes que nao cabem
/// em KPI/serie, ex.: dados do auditor ou do historico do emissor. Pares rotulo/valor.
/// </summary>
public sealed class FreDossieBloco
{
    [JsonPropertyName("chave")]
    public string Chave { get; set; } = string.Empty;

    [JsonPropertyName("titulo")]
    public string Titulo { get; set; } = string.Empty;

    [JsonPropertyName("icone")]
    public string? Icone { get; set; }

    [JsonPropertyName("itens")]
    public IList<FreBlocoItem> Itens { get; set; } = new List<FreBlocoItem>();
}

/// <summary>Par rotulo/valor de um <see cref="FreDossieBloco"/>.</summary>
public sealed class FreBlocoItem
{
    [JsonPropertyName("rotulo")]
    public string Rotulo { get; set; } = string.Empty;

    [JsonPropertyName("valor")]
    public string? Valor { get; set; }
}
