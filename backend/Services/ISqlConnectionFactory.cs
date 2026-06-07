using AustralCreditAnalytics.Api.Models;
using Microsoft.Data.SqlClient;

namespace AustralCreditAnalytics.Api.Services;

public interface ISqlConnectionFactory
{
    /// <summary>True quando ha uma conexao disponivel (appsettings ou config salva).</summary>
    bool IsConfigured { get; }

    /// <summary>Monta o connection string a partir da config informada.</summary>
    string BuildConnectionString(ConnectionConfig config);

    /// <summary>Cria (sem abrir) uma SqlConnection a partir da config informada.</summary>
    SqlConnection Create(ConnectionConfig config);

    /// <summary>Cria (sem abrir) uma SqlConnection a partir da config salva. Lanca se nao configurada.</summary>
    SqlConnection CreateFromSaved();

    /// <summary>
    /// Cria (sem abrir) uma SqlConnection gravavel a partir da config salva, sem
    /// <c>ApplicationIntent.ReadOnly</c>. Usada pelo fluxo interno (migracoes,
    /// autenticacao e persistencia parametrizada). Lanca se nao configurada.
    /// </summary>
    SqlConnection CreateWritable();
}
