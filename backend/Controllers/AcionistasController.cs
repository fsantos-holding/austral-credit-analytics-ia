using AustralCreditAnalytics.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AustralCreditAnalytics.Api.Controllers;

/// <summary>
/// Dashboard de Acionistas Cruzados (FRE): cruza fre.PosicaoAcionaria entre todas as
/// companhias importadas para identificar acionistas presentes em mais de uma empresa.
/// Apenas leitura dos dados FRE ja importados. Mesmo padrao de tratamento de erros e
/// garantia de schema/conexao de <see cref="FreController"/>.
/// </summary>
[ApiController]
[Route("api/acionistas")]
[Authorize]
public class AcionistasController : ControllerBase
{
    private readonly IAcionistaCruzamentoRepository _repository;
    private readonly ISqlConnectionFactory _factory;
    private readonly ISchemaInitializer _schema;
    private readonly ILogger<AcionistasController> _logger;

    public AcionistasController(
        IAcionistaCruzamentoRepository repository,
        ISqlConnectionFactory factory,
        ISchemaInitializer schema,
        ILogger<AcionistasController> logger)
    {
        _repository = repository;
        _factory = factory;
        _schema = schema;
        _logger = logger;
    }

    /// <summary>
    /// Ranking de acionistas cruzados. Filtros opcionais: <c>ano</c>, <c>tipoPessoa</c>
    /// (PF/PJ), <c>minEmpresas</c> (minimo de empresas distintas, padrao 2), <c>busca</c>
    /// (nome/documento) e <c>limite</c> (padrao 50).
    /// </summary>
    [HttpGet("ranking")]
    public Task<IActionResult> Ranking(
        [FromQuery] int? ano,
        [FromQuery] string? tipoPessoa,
        [FromQuery] int minEmpresas,
        [FromQuery] string? busca,
        [FromQuery] int limite,
        CancellationToken ct)
        => Run(async () => Ok(await _repository.GetRankingAsync(
            ano, tipoPessoa, minEmpresas <= 0 ? 2 : minEmpresas, busca, limite <= 0 ? 50 : limite, ct)), ct);

    /// <summary>
    /// Drilldown de um acionista: todas as empresas em que aparece e os percentuais.
    /// <c>chave</c> e o documento (somente digitos) ou o nome normalizado. 404 se vazio.
    /// </summary>
    [HttpGet("{chave}/posicoes")]
    public Task<IActionResult> Posicoes(string chave, [FromQuery] int? ano, CancellationToken ct)
        => Run(async () =>
        {
            var detalhe = await _repository.GetPosicoesAcionistaAsync(chave, ano, ct);
            return detalhe is null
                ? NotFound(new { message = "Acionista nao encontrado para os filtros informados." })
                : Ok(detalhe);
        }, ct);

    /// <summary>KPIs, series e pares de empresas conectadas. Filtro opcional por <c>ano</c>.</summary>
    [HttpGet("insights")]
    public Task<IActionResult> Insights([FromQuery] int? ano, CancellationToken ct)
        => Run(async () => Ok(await _repository.GetInsightsAsync(ano, ct)), ct);

    /// <summary>Garante conexao + schema e padroniza o tratamento de erros das acoes.</summary>
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action, CancellationToken ct)
    {
        if (!_factory.IsConfigured)
        {
            return StatusCode(StatusCodes.Status409Conflict,
                new { message = "Conexao nao configurada. Configure a conexao antes de usar a estrutura FRE." });
        }

        try
        {
            await _schema.EnsureInitializedAsync(ct);
            return await action();
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status409Conflict, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na operacao do cruzamento de acionistas");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "Falha ao acessar o banco de dados. Verifique a configuracao e tente novamente." });
        }
    }
}
