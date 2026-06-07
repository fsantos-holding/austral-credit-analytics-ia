using System.Text.Json.Serialization;

namespace AustralCreditAnalytics.Api.Models.Itr;

/// <summary>
/// Linha trimestral de uma conta da DRE (mapeia itr.vw_DreTrimestral). Os valores ja
/// vem de-acumulados pelo motor (<c>ValorTrimestral</c>); <c>ValorAcumulado</c> preserva
/// o valor acumulado original da CVM.
/// </summary>
public sealed class ItrDreTrimestral
{
    [JsonPropertyName("cdCvm")]
    public string? CD_CVM { get; set; }

    [JsonPropertyName("cnpjNum")]
    public string? CnpjNum { get; set; }

    [JsonPropertyName("denominacao")]
    public string? DENOM_CIA { get; set; }

    [JsonPropertyName("ano")]
    public short Ano { get; set; }

    [JsonPropertyName("trimestre")]
    public byte Trimestre { get; set; }

    [JsonPropertyName("conjunto")]
    public string? Conjunto { get; set; }

    [JsonPropertyName("cdConta")]
    public string CD_CONTA { get; set; } = string.Empty;

    [JsonPropertyName("dsConta")]
    public string? DS_CONTA { get; set; }

    [JsonPropertyName("valorTrimestral")]
    public decimal? ValorTrimestral { get; set; }

    [JsonPropertyName("valorAcumulado")]
    public decimal? ValorAcumulado { get; set; }

    [JsonPropertyName("origemTrimestre")]
    public string? OrigemTrimestre { get; set; }

    [JsonPropertyName("escalaMoeda")]
    public string? EscalaMoeda { get; set; }

    [JsonPropertyName("inconsistente")]
    public bool Inconsistente { get; set; }

    [JsonPropertyName("motivoInconsistencia")]
    public string? MotivoInconsistencia { get; set; }
}

/// <summary>
/// Resumo de uma companhia com DRE trimestral disponivel (seletor de empresa da tela de
/// leitura de ITR). Agrega itr.DreTrimestral por <c>CnpjNum</c>.
/// </summary>
public sealed class ItrEmpresaResumo
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

/// <summary>Linha do ledger de importacoes ITR (mapeia itr.Importacao).</summary>
public sealed class ItrImportacaoHistorico
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("tipo")]
    public string Tipo { get; set; } = string.Empty;

    [JsonPropertyName("tabela")]
    public string Tabela { get; set; } = string.Empty;

    [JsonPropertyName("conjunto")]
    public string? Conjunto { get; set; }

    [JsonPropertyName("ano")]
    public short? Ano { get; set; }

    [JsonPropertyName("arquivo")]
    public string? Arquivo { get; set; }

    [JsonPropertyName("linhasLidas")]
    public int LinhasLidas { get; set; }

    [JsonPropertyName("linhasImportadas")]
    public int LinhasImportadas { get; set; }

    [JsonPropertyName("linhasRemovidas")]
    public int LinhasRemovidas { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("mensagem")]
    public string? Mensagem { get; set; }

    [JsonPropertyName("usuario")]
    public string? Usuario { get; set; }

    [JsonPropertyName("iniciadoEmUtc")]
    public DateTime IniciadoEmUtc { get; set; }

    [JsonPropertyName("concluidoEmUtc")]
    public DateTime? ConcluidoEmUtc { get; set; }
}
