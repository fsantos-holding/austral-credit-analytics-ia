using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Aplica os scripts de migracao embutidos (Migrations/*.sql) sobre o canal de
/// escrita (<see cref="ISqlConnectionFactory.CreateWritable"/>). Mantem a auditoria
/// em <c>seg.__SchemaVersions</c>, aplicando cada script pendente em transacao
/// e registrando versao, nome, checksum SHA-256, data, autor e duracao.
/// </summary>
public class SqlMigrationRunner : ISchemaInitializer
{
    private const string ResourcePrefix = "AustralCreditAnalytics.Api.Migrations.";

    // Divide o script em lotes pelos separadores GO (cada um em sua propria linha).
    private static readonly Regex BatchSeparator =
        new(@"^\s*GO\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    private readonly ISqlConnectionFactory _factory;
    private readonly ILogger<SqlMigrationRunner> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public SqlMigrationRunner(ISqlConnectionFactory factory, ILogger<SqlMigrationRunner> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        if (_initialized)
            return;

        if (!_factory.IsConfigured)
        {
            _logger.LogInformation("Migracao adiada: conexao ainda nao configurada.");
            return;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_initialized)
                return;

            await using var connection = _factory.CreateWritable();
            await connection.OpenAsync(ct);

            await EnsureVersionsTableAsync(connection, ct);

            var applied = (await connection.QueryAsync<int>(
                "SELECT Version FROM seg.__SchemaVersions")).ToHashSet();

            var appliedBy = $"{Environment.MachineName}\\{Environment.UserName}";

            foreach (var script in LoadScripts())
            {
                if (applied.Contains(script.Version))
                    continue;

                await ApplyScriptAsync(connection, script, appliedBy, ct);
                _logger.LogInformation(
                    "Migracao aplicada: V{Version} ({Script}).",
                    script.Version, script.Name);
            }

            _initialized = true;
        }
        catch (Exception ex)
        {
            // Nao derruba a aplicacao: o fluxo de negocio ficara indisponivel ate corrigir.
            _logger.LogError(ex, "Falha ao aplicar as migracoes do schema.");
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task EnsureVersionsTableAsync(SqlConnection connection, CancellationToken ct)
    {
        const string sql = """
            IF SCHEMA_ID('seg') IS NULL EXEC('CREATE SCHEMA seg');
            IF OBJECT_ID('seg.__SchemaVersions', 'U') IS NULL
                CREATE TABLE seg.__SchemaVersions(
                    Version      INT           NOT NULL PRIMARY KEY,
                    ScriptName   NVARCHAR(200) NOT NULL,
                    Checksum     NVARCHAR(64)  NOT NULL,
                    AppliedAtUtc DATETIME2     NOT NULL CONSTRAINT DF_SV_At DEFAULT SYSUTCDATETIME(),
                    AppliedBy    NVARCHAR(128) NOT NULL,
                    DurationMs   INT           NOT NULL
                );
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
    }

    private async Task ApplyScriptAsync(
        SqlConnection connection, MigrationScript script, string appliedBy, CancellationToken ct)
    {
        var checksum = ComputeChecksum(script.Sql);
        var started = System.Diagnostics.Stopwatch.StartNew();

        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            foreach (var batch in SplitBatches(script.Sql))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    batch, transaction: tx, commandTimeout: 120, cancellationToken: ct));
            }

            started.Stop();

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO seg.__SchemaVersions (Version, ScriptName, Checksum, AppliedBy, DurationMs)
                VALUES (@Version, @ScriptName, @Checksum, @AppliedBy, @DurationMs)
                """,
                new
                {
                    script.Version,
                    ScriptName = script.Name,
                    Checksum = checksum,
                    AppliedBy = appliedBy,
                    DurationMs = (int)started.ElapsedMilliseconds,
                },
                transaction: tx,
                cancellationToken: ct));

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static IEnumerable<string> SplitBatches(string sql)
        => BatchSeparator.Split(sql)
            .Select(b => b.Trim())
            .Where(b => b.Length > 0);

    private static string ComputeChecksum(string sql)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sql));
        return Convert.ToHexString(bytes);
    }

    private static IEnumerable<MigrationScript> LoadScripts()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var scripts = new List<MigrationScript>();

        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith(ResourcePrefix, StringComparison.Ordinal) ||
                !resource.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                continue;

            var fileName = resource[ResourcePrefix.Length..];
            var version = ParseVersion(fileName);
            if (version is null)
                continue;

            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            scripts.Add(new MigrationScript(version.Value, fileName, reader.ReadToEnd()));
        }

        return scripts.OrderBy(s => s.Version);
    }

    private static int? ParseVersion(string fileName)
    {
        // Espera "V001__descricao.sql"; extrai o numero apos o 'V' inicial.
        if (fileName.Length < 2 || (fileName[0] != 'V' && fileName[0] != 'v'))
            return null;

        var digits = new string(fileName.Skip(1).TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var version) ? version : null;
    }

    private sealed record MigrationScript(int Version, string Name, string Sql);
}
