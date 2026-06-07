using AustralCreditAnalytics.Api.Models;
using Microsoft.Data.SqlClient;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Monta o connection string autoritativo (no servidor) a partir da config,
/// suportando autenticacao SQL e Windows, Encrypt, TrustServerCertificate e timeout.
/// </summary>
public class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly IConnectionConfigStore _store;
    private readonly IConfiguration _configuration;

    public SqlConnectionFactory(IConnectionConfigStore store, IConfiguration configuration)
    {
        _store = store;
        _configuration = configuration;
    }

    /// <summary>
    /// True quando ha uma conexao disponivel: a string crua em
    /// <c>ConnectionStrings:Default</c> (prioritaria) ou a config salva pela tela.
    /// </summary>
    public bool IsConfigured
        => !string.IsNullOrWhiteSpace(_configuration.GetConnectionString("Default"))
           || _store.IsConfigured;

    public string BuildConnectionString(ConnectionConfig config)
        => BuildConnectionString(config, readOnly: true);

    private static string BuildConnectionString(ConnectionConfig config, bool readOnly)
    {
        var dataSource = config.Server.Trim();
        if (!string.IsNullOrWhiteSpace(config.Port) && config.Port.Trim() != "1433")
        {
            // Porta customizada: SqlClient usa "host,porta".
            dataSource = $"{dataSource},{config.Port.Trim()}";
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = dataSource,
            InitialCatalog = config.Database.Trim(),
            Encrypt = config.Encrypt,
            TrustServerCertificate = config.TrustServerCertificate,
            ConnectTimeout = config.ConnectionTimeout > 0 ? config.ConnectionTimeout : 30,
            ApplicationName = "AustralCreditAnalytics",
        };

        if (readOnly)
        {
            // Leitura do dashboard: sinaliza intencao de leitura ao SQL Server
            // (roteia para replica secundaria em Always On, quando disponivel).
            builder.ApplicationIntent = ApplicationIntent.ReadOnly;
        }

        if (string.Equals(config.AuthMode, "windows", StringComparison.OrdinalIgnoreCase))
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.IntegratedSecurity = false;
            builder.UserID = config.User ?? string.Empty;
            builder.Password = config.Password ?? string.Empty;
        }

        return builder.ConnectionString;
    }

    public SqlConnection Create(ConnectionConfig config)
        => new(BuildConnectionString(config));

    public SqlConnection CreateFromSaved()
    {
        // Prioridade: connection string crua do appsettings (ConnectionStrings:Default).
        // Por ser crua, atributos como ApplicationName/ApplicationIntent nao sao injetados
        // automaticamente; inclua-os na string se desejado.
        var rawConnectionString = _configuration.GetConnectionString("Default");
        if (!string.IsNullOrWhiteSpace(rawConnectionString))
        {
            return new SqlConnection(rawConnectionString);
        }

        var config = _store.Load()
            ?? throw new InvalidOperationException("Nenhuma configuracao de conexao foi salva. Configure em /api/config.");
        return Create(config);
    }

    public SqlConnection CreateWritable()
    {
        // Canal de escrita do fluxo interno. Prioriza a connection string crua
        // (ConnectionStrings:Default); se ela contiver ApplicationIntent=ReadOnly, a
        // escrita falhara (o banco configurado precisa ser primario/gravavel).
        var rawConnectionString = _configuration.GetConnectionString("Default");
        if (!string.IsNullOrWhiteSpace(rawConnectionString))
        {
            return new SqlConnection(rawConnectionString);
        }

        var config = _store.Load()
            ?? throw new InvalidOperationException("Nenhuma configuracao de conexao foi salva. Configure em /api/config.");
        return new SqlConnection(BuildConnectionString(config, readOnly: false));
    }
}
