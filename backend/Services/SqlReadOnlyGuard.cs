using System.Text.RegularExpressions;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Defesa em profundidade: garante que a consulta configurada seja somente-leitura.
/// Remove comentarios, exige que a query inicie com SELECT ou WITH (CTE) e rejeita
/// tokens de escrita/DDL/execucao ou multiplos statements.
/// </summary>
public static class SqlReadOnlyGuard
{
    private static readonly string[] ForbiddenKeywords =
    {
        "INSERT", "UPDATE", "DELETE", "MERGE", "DROP", "ALTER", "CREATE",
        "TRUNCATE", "EXEC", "EXECUTE", "GRANT", "REVOKE", "INTO",
    };

    private static readonly Regex LineComments =
        new(@"--[^\n\r]*", RegexOptions.Compiled);

    private static readonly Regex BlockComments =
        new(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex ForbiddenKeywordPattern = new(
        $@"\b({string.Join("|", ForbiddenKeywords)})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ForbiddenProcedurePrefix = new(
        @"\b(sp_|xp_)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private const string RejectionMessage =
        "A consulta configurada nao e permitida: apenas operacoes de leitura (SELECT) sao aceitas.";

    /// <summary>
    /// Valida que a query e somente-leitura. Lanca <see cref="InvalidOperationException"/>
    /// com mensagem generica caso reprove.
    /// </summary>
    public static void EnsureReadOnly(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new InvalidOperationException(RejectionMessage);

        var sanitized = StripComments(query).Trim();

        // Permite um unico ponto-e-virgula final; qualquer outro indica multiplos statements.
        sanitized = sanitized.TrimEnd();
        if (sanitized.EndsWith(';'))
            sanitized = sanitized[..^1].TrimEnd();

        if (sanitized.Length == 0)
            throw new InvalidOperationException(RejectionMessage);

        if (sanitized.Contains(';'))
            throw new InvalidOperationException(RejectionMessage);

        var startsValid =
            StartsWithKeyword(sanitized, "SELECT") ||
            StartsWithKeyword(sanitized, "WITH");
        if (!startsValid)
            throw new InvalidOperationException(RejectionMessage);

        if (ForbiddenKeywordPattern.IsMatch(sanitized))
            throw new InvalidOperationException(RejectionMessage);

        if (ForbiddenProcedurePrefix.IsMatch(sanitized))
            throw new InvalidOperationException(RejectionMessage);
    }

    private static string StripComments(string query)
    {
        var withoutBlocks = BlockComments.Replace(query, " ");
        return LineComments.Replace(withoutBlocks, " ");
    }

    private static bool StartsWithKeyword(string sql, string keyword)
    {
        if (!sql.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
            return false;

        // Garante limite de palavra (ex.: "SELECTED" nao deve passar).
        if (sql.Length == keyword.Length)
            return true;

        var next = sql[keyword.Length];
        return !char.IsLetterOrDigit(next) && next != '_';
    }
}
