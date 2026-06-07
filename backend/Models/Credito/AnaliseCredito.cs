using System.Text.Json.Serialization;

namespace AustralCreditAnalytics.Api.Models.Credito;

/// <summary>
/// Registro de analise de credito consumido pelo dashboard (index.html).
/// As colunas retornadas pela consulta configurada (Credito:Query) devem casar
/// (case-insensitive) com estas propriedades.
/// </summary>
public class AnaliseCreditoRecord
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>Data da analise no formato dd/MM/yyyy (string, igual ao que o frontend exibe).</summary>
    [JsonPropertyName("dataAnalise")]
    public string DataAnalise { get; set; } = string.Empty;

    [JsonPropertyName("tomador")]
    public string Tomador { get; set; } = string.Empty;

    [JsonPropertyName("documento")]
    public string? Documento { get; set; }

    [JsonPropertyName("valorSolicitado")]
    public decimal ValorSolicitado { get; set; }

    [JsonPropertyName("scoreCredito")]
    public int? ScoreCredito { get; set; }

    [JsonPropertyName("rating")]
    public string? Rating { get; set; }

    [JsonPropertyName("probabilidadeInadimplencia")]
    public decimal? ProbabilidadeInadimplencia { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("analista")]
    public string? Analista { get; set; }
}
