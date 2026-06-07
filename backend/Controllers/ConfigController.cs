using AustralCreditAnalytics.Api.Models;
using AustralCreditAnalytics.Api.Models.Auth;
using AustralCreditAnalytics.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AustralCreditAnalytics.Api.Controllers;

// AllowAnonymous + gate manual: enquanto a conexao nao esta configurada (bootstrap),
// a tela de Configuracoes fica acessivel sem login para permitir o primeiro setup;
// uma vez configurada, passa a exigir um Gestor autenticado.
[ApiController]
[Route("api/config")]
[AllowAnonymous]
public class ConfigController : ControllerBase
{
    private readonly IConnectionConfigStore _store;
    private readonly IConnectionTester _tester;
    private readonly ISchemaInitializer _schema;
    private readonly ISqlConnectionFactory _factory;
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(
        IConnectionConfigStore store,
        IConnectionTester tester,
        ISchemaInitializer schema,
        ISqlConnectionFactory factory,
        ILogger<ConfigController> logger)
    {
        _store = store;
        _tester = tester;
        _schema = schema;
        _factory = factory;
        _logger = logger;
    }

    /// <summary>Config salva com senha mascarada + flag isConfigured.</summary>
    [HttpGet]
    public ActionResult<ConnectionConfigView> Get()
    {
        var gate = Gate();
        if (gate is not null)
            return gate;

        var view = _store.LoadMasked();
        if (view is null)
            return Ok(new ConnectionConfigView { IsConfigured = false });

        return Ok(view);
    }

    /// <summary>Valida (via ModelState) e salva a configuracao cifrada.</summary>
    [HttpPost]
    public ActionResult Save([FromBody] ConnectionConfig config)
    {
        var gate = Gate();
        if (gate is not null)
            return gate;

        _store.Save(config);
        _logger.LogInformation("Configuracao de conexao salva para o servidor {Server}", config.Server);

        // Dispara a auto-migracao do schema apos a 1a configuracao (idempotente).
        _ = _schema.EnsureInitializedAsync();

        return Ok(new { ok = true, message = "Configuracao salva com sucesso." });
    }

    /// <summary>Testa a conexao em etapas e retorna as latencias reais.</summary>
    [HttpPost("test")]
    public async Task<ActionResult<ConnectionTestResult>> Test(
        [FromBody] ConnectionConfig config,
        CancellationToken ct)
    {
        var gate = Gate();
        if (gate is not null)
            return gate;

        try
        {
            var result = await _tester.TestAsync(config, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado no teste de conexao");
            return StatusCode(500, new { message = "Erro inesperado ao testar a conexao. Consulte os logs do servidor para detalhes." });
        }
    }

    /// <summary>
    /// Bootstrap: sem conexao configurada, libera o acesso para o primeiro setup.
    /// Com a conexao ja configurada, exige um Gestor autenticado. Retorna o
    /// resultado de bloqueio (401/403) ou null quando autorizado.
    /// </summary>
    private ObjectResult? Gate()
    {
        if (!_factory.IsConfigured)
            return null;

        if (User?.Identity?.IsAuthenticated != true)
        {
            return StatusCode(StatusCodes.Status401Unauthorized,
                new { message = "Autenticacao necessaria." });
        }

        if (!User.IsInRole(Perfis.Gestor))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Apenas gestores podem alterar a configuracao de conexao." });
        }

        return null;
    }
}
