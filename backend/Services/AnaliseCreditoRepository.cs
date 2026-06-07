using System.Globalization;
using Dapper;
using AustralCreditAnalytics.Api.Models.Credito;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Le as analises de credito com Dapper a partir da base existente do cliente.
/// A consulta SQL e configurada em appsettings (chave "Credito:Query") e deve
/// apontar para a tabela/view do cliente cujas colunas casem (case-insensitive)
/// com <see cref="AnaliseCreditoRecord"/>. A query passa pelo guard somente-leitura;
/// os filtros de periodo/status sao aplicados no servidor sobre o resultado.
/// </summary>
public class AnaliseCreditoRepository : IAnaliseCreditoRepository
{
    private readonly ISqlConnectionFactory _factory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AnaliseCreditoRepository> _logger;

    public AnaliseCreditoRepository(
        ISqlConnectionFactory factory,
        IConfiguration configuration,
        ILogger<AnaliseCreditoRepository> logger)
    {
        _factory = factory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AnaliseCreditoRecord>> GetAsync(
        string? dataInicial, string? dataFinal, string? status, CancellationToken ct = default)
    {
        var query = _configuration["Credito:Query"];
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new InvalidOperationException(
                "A consulta de credito nao esta configurada. Defina 'Credito:Query' em appsettings.json apontando para a tabela/view do banco.");
        }

        SqlReadOnlyGuard.EnsureReadOnly(query);

        var commandTimeout = _configuration.GetValue<int?>("Credito:CommandTimeoutSeconds") ?? 60;

        await using var connection = _factory.CreateFromSaved();

        var command = new CommandDefinition(query, commandTimeout: commandTimeout, cancellationToken: ct);
        var rows = await connection.QueryAsync(command);

        var inicial = ParseDate(dataInicial);
        var final = ParseDate(dataFinal);
        var statusFilter = string.IsNullOrWhiteSpace(status) ? null : status.Trim();

        var result = new List<AnaliseCreditoRecord>();
        foreach (var row in rows)
        {
            var dict = (IDictionary<string, object>)row;
            var rawDate = GetRawDate(dict, "DataAnalise");

            if (inicial is not null && rawDate is not null && rawDate.Value.Date < inicial.Value.Date)
                continue;
            if (final is not null && rawDate is not null && rawDate.Value.Date > final.Value.Date)
                continue;

            var statusValue = GetString(dict, "Status");
            if (statusFilter is not null && !string.Equals(statusValue, statusFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            result.Add(MapRow(dict, rawDate));
        }

        return result;
    }

    private static AnaliseCreditoRecord MapRow(IDictionary<string, object> row, DateTime? rawDate)
    {
        return new AnaliseCreditoRecord
        {
            Id = GetLong(row, "Id"),
            DataAnalise = rawDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? GetString(row, "DataAnalise"),
            Tomador = GetString(row, "Tomador", "nm_tomador", "Nome"),
            Documento = GetNullableString(row, "Documento", "Cnpj", "nr_cnpj"),
            ValorSolicitado = GetDecimal(row, "ValorSolicitado", "Valor"),
            ScoreCredito = GetNullableInt(row, "ScoreCredito", "Score"),
            Rating = GetNullableString(row, "Rating"),
            ProbabilidadeInadimplencia = GetNullableDecimal(row, "ProbabilidadeInadimplencia", "Risco"),
            Status = GetString(row, "Status"),
            Analista = GetNullableString(row, "Analista"),
        };
    }

    private static object? GetValue(IDictionary<string, object> row, params string[] names)
    {
        foreach (var name in names)
        {
            if (row.TryGetValue(name, out var value) && value is not null && value is not DBNull)
                return value;
        }

        foreach (var name in names)
        {
            foreach (var kvp in row)
            {
                if (string.Equals(kvp.Key, name, StringComparison.OrdinalIgnoreCase)
                    && kvp.Value is not null && kvp.Value is not DBNull)
                    return kvp.Value;
            }
        }

        return null;
    }

    private static string GetString(IDictionary<string, object> row, params string[] names)
        => GetValue(row, names)?.ToString() ?? string.Empty;

    private static string? GetNullableString(IDictionary<string, object> row, params string[] names)
    {
        var value = GetValue(row, names)?.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static long GetLong(IDictionary<string, object> row, params string[] names)
    {
        var value = GetValue(row, names);
        if (value is null) return 0;
        try { return Convert.ToInt64(value, CultureInfo.InvariantCulture); }
        catch { return 0; }
    }

    private static int? GetNullableInt(IDictionary<string, object> row, params string[] names)
    {
        var value = GetValue(row, names);
        if (value is null) return null;
        try { return Convert.ToInt32(value, CultureInfo.InvariantCulture); }
        catch { return null; }
    }

    private static decimal GetDecimal(IDictionary<string, object> row, params string[] names)
    {
        var value = GetValue(row, names);
        if (value is null) return 0m;
        try { return Convert.ToDecimal(value, CultureInfo.InvariantCulture); }
        catch { return 0m; }
    }

    private static decimal? GetNullableDecimal(IDictionary<string, object> row, params string[] names)
    {
        var value = GetValue(row, names);
        if (value is null) return null;
        try { return Convert.ToDecimal(value, CultureInfo.InvariantCulture); }
        catch { return null; }
    }

    private static DateTime? GetRawDate(IDictionary<string, object> row, params string[] names)
    {
        var value = GetValue(row, names);
        if (value is null) return null;
        if (value is DateTime dt) return dt;
        if (value is DateOnly d) return d.ToDateTime(TimeOnly.MinValue);
        return DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }
}
