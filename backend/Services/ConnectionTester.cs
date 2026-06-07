using System.Diagnostics;
using System.Net.Sockets;
using Dapper;
using AustralCreditAnalytics.Api.Models;
using Microsoft.Data.SqlClient;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Diagnostico de conectividade em quatro etapas, com latencia real:
///  1) net  - alcance TCP ao host/porta;
///  2) auth - abertura da conexao (autenticacao acontece no Open);
///  3) db   - contexto do banco (DB_NAME());
///  4) app  - consulta leve confirmando o pool de conexoes.
/// </summary>
public class ConnectionTester : IConnectionTester
{
    private readonly ISqlConnectionFactory _factory;
    private readonly ILogger<ConnectionTester> _logger;

    public ConnectionTester(ISqlConnectionFactory factory, ILogger<ConnectionTester> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<ConnectionTestResult> TestAsync(ConnectionConfig config, CancellationToken ct = default)
    {
        var result = new ConnectionTestResult();
        var total = Stopwatch.StartNew();

        // ----- Etapa 1: alcance de rede (TCP) -----
        // Instancias nomeadas (ex.: localhost\SQLEXPRESS) nao usam a porta 1433 por padrao;
        // o teste TCP nessa porta falharia mesmo com o SQL Server saudavel.
        if (HasNamedInstance(config.Server))
        {
            result.Net.State = "on";
            result.Net.Ms = 0;
            result.Net.Message = "Instancia nomeada — validacao na conexao";
        }
        else
        {
            var (host, port) = ResolveHostPort(config);
            var timeout = TimeSpan.FromSeconds(config.ConnectionTimeout > 0 ? config.ConnectionTimeout : 30);
            var netOk = await RunStepAsync(result.Net, result, total,
                failureStepMessage: $"Servidor inacessivel em {host}:{port}",
                failureSummaryPrefix: "Falha de rede: ",
                action: async () =>
                {
                    using var tcp = new TcpClient();
                    var connectTask = tcp.ConnectAsync(host, port);
                    var completed = await Task.WhenAny(connectTask, Task.Delay(timeout, ct));
                    if (completed != connectTask || !tcp.Connected)
                        throw new TimeoutException($"Servidor inacessivel em {host}:{port}");
                    return "Servidor acessivel";
                });
            if (!netOk)
                return result;
        }

        // ----- Etapa 2: autenticacao (Open) -----
        SqlConnection? connection = null;
        var authOk = await RunStepAsync(result.Auth, result, total,
            failureStepMessage: "Falha de autenticacao",
            failureSummaryPrefix: "Falha de autenticacao: ",
            action: async () =>
            {
                connection = _factory.Create(config);
                await connection.OpenAsync(ct);
                return string.Equals(config.AuthMode, "windows", StringComparison.OrdinalIgnoreCase)
                    ? "Windows Auth OK"
                    : "Login validado";
            });
        if (!authOk)
        {
            if (connection is not null)
                await connection.DisposeAsync();
            return result;
        }

        try
        {
            // ----- Etapa 3: acesso ao banco -----
            var dbOk = await RunStepAsync(result.Db, result, total,
                failureStepMessage: "Banco indisponivel",
                failureSummaryPrefix: "Falha no acesso ao banco: ",
                action: async () =>
                {
                    var dbName = await connection!.ExecuteScalarAsync<string>(
                        new CommandDefinition("SELECT DB_NAME();", cancellationToken: ct));
                    return string.IsNullOrEmpty(dbName) ? "Banco disponivel" : $"Banco '{dbName}' disponivel";
                });
            if (!dbOk)
                return result;

            // ----- Etapa 4: conexao da aplicacao -----
            var appOk = await RunStepAsync(result.App, result, total,
                failureStepMessage: "Falha na consulta de validacao",
                failureSummaryPrefix: "Falha na consulta da aplicacao: ",
                action: async () =>
                {
                    await connection!.ExecuteScalarAsync<int>(
                        new CommandDefinition("SELECT 1;", cancellationToken: ct));
                    return "Pool de conexoes OK";
                });
            if (!appOk)
                return result;
        }
        finally
        {
            if (connection is not null)
                await connection.DisposeAsync();
        }

        Finish(result, total, true, "Conexao estabelecida com sucesso.");
        return result;
    }

    /// <summary>
    /// Executa uma etapa do diagnostico padronizando medicao de latencia e tratamento de erro:
    /// em caso de falha, marca a etapa como "off", registra o detalhe no log e consolida o
    /// resultado com a mensagem de resumo apropriada.
    /// </summary>
    private async Task<bool> RunStepAsync(
        ConnectionTestStep step,
        ConnectionTestResult result,
        Stopwatch total,
        string failureStepMessage,
        string failureSummaryPrefix,
        Func<Task<string>> action)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var message = await action();
            sw.Stop();
            step.State = "on";
            step.Ms = sw.ElapsedMilliseconds;
            step.Message = message;
            return true;
        }
        catch (Exception ex)
        {
            sw.Stop();
            step.State = "off";
            step.Ms = sw.ElapsedMilliseconds;
            step.Message = failureStepMessage;
            _logger.LogWarning(ex, "Falha na etapa {Step} do teste de conexao", step.Step);
            Finish(result, total, false, failureSummaryPrefix + ex.Message);
            return false;
        }
    }

    private static void Finish(ConnectionTestResult result, Stopwatch total, bool ok, string message)
    {
        total.Stop();
        result.TotalMs = total.ElapsedMilliseconds;
        result.Ok = ok;
        result.Message = message;
    }

    private static bool HasNamedInstance(string? server)
    {
        if (string.IsNullOrWhiteSpace(server))
            return false;

        var name = server.Trim();
        var commaIdx = name.IndexOf(',');
        if (commaIdx >= 0)
            name = name[..commaIdx].Trim();

        return name.IndexOf('\\') >= 0;
    }

    /// <summary>
    /// Extrai host e porta para o teste TCP. Suporta "host", "host,porta" e "host\instancia".
    /// Nao usar para instancias nomeadas sem porta fixa (ver HasNamedInstance).
    /// </summary>
    private static (string Host, int Port) ResolveHostPort(ConnectionConfig config)
    {
        var server = (config.Server ?? string.Empty).Trim();
        var port = 1433;

        if (int.TryParse(config.Port?.Trim(), out var parsedPort) && parsedPort > 0)
            port = parsedPort;

        // "host,porta" tem prioridade sobre o campo Port.
        var commaIdx = server.IndexOf(',');
        if (commaIdx >= 0)
        {
            var portPart = server[(commaIdx + 1)..].Trim();
            if (int.TryParse(portPart, out var inlinePort) && inlinePort > 0)
                port = inlinePort;
            server = server[..commaIdx].Trim();
        }

        // Remove instancia nomeada ("host\SQLEXPRESS") para o teste TCP.
        var backslashIdx = server.IndexOf('\\');
        if (backslashIdx >= 0)
            server = server[..backslashIdx].Trim();

        if (string.IsNullOrWhiteSpace(server) ||
            server.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            server == ".")
        {
            server = "127.0.0.1";
        }

        return (server, port);
    }
}
