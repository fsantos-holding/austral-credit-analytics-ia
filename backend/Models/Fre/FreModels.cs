using System.Text.Json.Serialization;

namespace AustralCreditAnalytics.Api.Models.Fre;

/// <summary>
/// Resumo de uma companhia com dados FRE disponiveis (seletor de empresa da secao
/// "Composicao Capital"). Agrega fre.Documento por <c>CnpjNum</c>.
/// </summary>
public sealed class FreEmpresaResumo
{
    [JsonPropertyName("cnpjNum")]
    public string CnpjNum { get; set; } = string.Empty;

    [JsonPropertyName("cdCvm")]
    public string? CD_CVM { get; set; }

    [JsonPropertyName("denominacao")]
    public string? DENOM_CIA { get; set; }

    [JsonPropertyName("ultimoAno")]
    public short? UltimoAno { get; set; }

    [JsonPropertyName("qtdAnos")]
    public int QtdAnos { get; set; }
}

/// <summary>Ano de referencia disponivel para uma companhia + maior versao entregue.</summary>
public sealed class FreAnoDisponivel
{
    [JsonPropertyName("ano")]
    public short Ano { get; set; }

    [JsonPropertyName("versaoMax")]
    public short? VersaoMax { get; set; }
}

/// <summary>Descritor de uma coluna nativa de um modelo FRE (nome + tipo CLR).</summary>
public sealed record FreColuna(
    [property: JsonPropertyName("nome")] string Nome,
    [property: JsonPropertyName("kind")] string Kind);

/// <summary>
/// Detalhe (master/detail) de um modelo FRE: as linhas de uma tabela filha
/// <c>*_classe_acao</c>, ligadas a tabela mestre pela coluna <see cref="JoinKey"/>
/// (um <c>ID_*</c> compartilhado, ex.: ID_Capital_Social / ID_Acionista).
/// </summary>
public sealed class FreModeloDetalhe
{
    [JsonPropertyName("modelo")]
    public string Modelo { get; set; } = string.Empty;

    [JsonPropertyName("tabela")]
    public string Tabela { get; set; } = string.Empty;

    [JsonPropertyName("descricao")]
    public string Descricao { get; set; } = string.Empty;

    /// <summary>Coluna ID compartilhada com a tabela mestre (chave do agrupamento).</summary>
    [JsonPropertyName("joinKey")]
    public string? JoinKey { get; set; }

    [JsonPropertyName("colunas")]
    public IReadOnlyList<FreColuna> Colunas { get; set; } = Array.Empty<FreColuna>();

    [JsonPropertyName("linhas")]
    public IReadOnlyList<IDictionary<string, object?>> Linhas { get; set; } =
        Array.Empty<IDictionary<string, object?>>();
}

/// <summary>
/// Dados de um modelo FRE para uma companhia/ano: metadados do modelo, as linhas mestre
/// e, quando aplicavel, o detalhe da tabela filha <c>*_classe_acao</c>.
/// </summary>
public sealed class FreModeloDados
{
    [JsonPropertyName("modelo")]
    public string Modelo { get; set; } = string.Empty;

    [JsonPropertyName("tabela")]
    public string Tabela { get; set; } = string.Empty;

    [JsonPropertyName("descricao")]
    public string Descricao { get; set; } = string.Empty;

    [JsonPropertyName("colunas")]
    public IReadOnlyList<FreColuna> Colunas { get; set; } = Array.Empty<FreColuna>();

    [JsonPropertyName("linhas")]
    public IReadOnlyList<IDictionary<string, object?>> Linhas { get; set; } =
        Array.Empty<IDictionary<string, object?>>();

    [JsonPropertyName("detalhe")]
    public FreModeloDetalhe? Detalhe { get; set; }
}

/// <summary>
/// Resumo da composicao de capital de uma companhia/ano (mapeia fre.vw_CapitalResumo),
/// usado nos KPIs (acoes ON/PN/total, acoes em circulacao e free float).
/// </summary>
public sealed class FreCapitalResumo
{
    [JsonPropertyName("cnpjNum")]
    public string? CnpjNum { get; set; }

    [JsonPropertyName("denominacao")]
    public string? DENOM_CIA { get; set; }

    [JsonPropertyName("ano")]
    public short Ano { get; set; }

    [JsonPropertyName("versao")]
    public short? Versao { get; set; }

    [JsonPropertyName("tipoCapital")]
    public string? TipoCapital { get; set; }

    [JsonPropertyName("valorCapital")]
    public decimal? ValorCapital { get; set; }

    [JsonPropertyName("acoesOrdinarias")]
    public decimal? AcoesOrdinarias { get; set; }

    [JsonPropertyName("acoesPreferenciais")]
    public decimal? AcoesPreferenciais { get; set; }

    [JsonPropertyName("acoesTotal")]
    public decimal? AcoesTotal { get; set; }

    [JsonPropertyName("acoesOrdinariasCirculacao")]
    public decimal? AcoesOrdinariasCirculacao { get; set; }

    [JsonPropertyName("acoesPreferenciaisCirculacao")]
    public decimal? AcoesPreferenciaisCirculacao { get; set; }

    [JsonPropertyName("acoesCirculacao")]
    public decimal? AcoesCirculacao { get; set; }

    [JsonPropertyName("percentualFreeFloat")]
    public decimal? PercentualFreeFloat { get; set; }
}
