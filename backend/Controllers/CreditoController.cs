using System.Globalization;
using AustralCreditAnalytics.Api.Models.Credito;
using AustralCreditAnalytics.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AustralCreditAnalytics.Api.Controllers;

[ApiController]
[Route("api/credito")]
[Authorize]
public class CreditoController : ControllerBase
{
    private readonly IAnaliseCreditoRepository _repository;
    private readonly ISqlConnectionFactory _factory;
    private readonly ISchemaInitializer _schema;
    private readonly ILogger<CreditoController> _logger;

    public CreditoController(
        IAnaliseCreditoRepository repository,
        ISqlConnectionFactory factory,
        ISchemaInitializer schema,
        ILogger<CreditoController> logger)
    {
        _repository = repository;
        _factory = factory;
        _schema = schema;
        _logger = logger;
    }

    /// <summary>Retorna as analises de credito, filtrando por periodo e status (opcionais).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AnaliseCreditoRecord>>> Get(
        [FromQuery] string? dataInicial,
        [FromQuery] string? dataFinal,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        if (dataInicial is not null && !IsValidDate(dataInicial))
            return BadRequest(new { message = "Data inicial invalida. Use o formato YYYYMMDD." });

        if (dataFinal is not null && !IsValidDate(dataFinal))
            return BadRequest(new { message = "Data final invalida. Use o formato YYYYMMDD." });

        if (IsValidDate(dataInicial) && IsValidDate(dataFinal)
            && string.CompareOrdinal(dataInicial, dataFinal) > 0)
            return BadRequest(new { message = "A data inicial deve ser anterior ou igual a data final." });

        if (!_factory.IsConfigured)
        {
            return StatusCode(StatusCodes.Status409Conflict,
                new { message = "Conexao nao configurada. Configure em Configuracoes antes de carregar os dados." });
        }

        try
        {
            await _schema.EnsureInitializedAsync(ct);
            var records = await _repository.GetAsync(dataInicial, dataFinal, status, ct);
            return Ok(records);
        }
        catch (InvalidOperationException ex)
        {
            // Query ausente ou reprovada pelo guard somente-leitura: a mensagem e segura para exibir.
            return StatusCode(StatusCodes.Status409Conflict, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao consultar analises de credito");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "Falha ao consultar o banco de dados. Verifique a configuracao da conexao e tente novamente." });
        }
    }

    private static bool IsValidDate(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
