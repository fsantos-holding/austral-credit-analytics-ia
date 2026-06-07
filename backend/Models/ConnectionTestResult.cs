namespace AustralCreditAnalytics.Api.Models;

/// <summary>
/// Resultado de uma etapa individual do diagnostico (rede, auth, banco, app).
/// </summary>
public class ConnectionTestStep
{
    /// <summary>Identificador da etapa: net | auth | db | app (espelha os IDs do painel de status).</summary>
    public string Step { get; set; } = string.Empty;

    /// <summary>Estado da etapa: "on" (ok), "off" (falhou) ou "wait" (nao executada).</summary>
    public string State { get; set; } = "wait";

    /// <summary>Mensagem curta exibida no painel.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Latencia real da etapa em milissegundos.</summary>
    public long Ms { get; set; }
}

/// <summary>
/// Resultado consolidado de POST /api/config/test.
/// </summary>
public class ConnectionTestResult
{
    public ConnectionTestStep Net { get; set; } = new() { Step = "net" };
    public ConnectionTestStep Auth { get; set; } = new() { Step = "auth" };
    public ConnectionTestStep Db { get; set; } = new() { Step = "db" };
    public ConnectionTestStep App { get; set; } = new() { Step = "app" };

    public long TotalMs { get; set; }
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;
}
