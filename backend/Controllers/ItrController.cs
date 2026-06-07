using AustralCreditAnalytics.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AustralCreditAnalytics.Api.Controllers;

/// <summary>
/// Informacoes Trimestrais (ITR / CVM): importacao isolada dos CSVs trimestrais (DRE)
/// para o schema [itr], motor de trimestralizacao (de-acumulacao T1-T3 do ITR + 4T do
/// DFP anual) e leitura da serie trimestral por CNPJ. Endpoints independentes do DFP.
/// </summary>
[ApiController]
[Route("api/itr")]
[Authorize]
public class ItrController : ControllerBase
{
    private readonly IDfpImportJobManager _jobs;
    private readonly IItrRepository _repository;
    private readonly ITrimestralizacaoService _engine;
    private readonly IDreComparativoService _comparativo;
    private readonly ISqlConnectionFactory _factory;
    private readonly ISchemaInitializer _schema;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ItrController> _logger;

    public ItrController(
        IDfpImportJobManager jobs,
        IItrRepository repository,
        ITrimestralizacaoService engine,
        IDreComparativoService comparativo,
        ISqlConnectionFactory factory,
        ISchemaInitializer schema,
        IWebHostEnvironment environment,
        ILogger<ItrController> logger)
    {
        _jobs = jobs;
        _repository = repository;
        _engine = engine;
        _comparativo = comparativo;
        _factory = factory;
        _schema = schema;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// Inicia a importacao de um CSV trimestral da CVM (so DRE) como job em segundo plano.
    /// Envie o arquivo no campo multipart <c>arquivo</c>. Retorna 202 com o <c>jobId</c>.
    /// </summary>
    [HttpPost("importar/{tipo}")]
    [RequestSizeLimit(1_073_741_824)] // 1 GiB: os CSVs da CVM podem ser grandes.
    [RequestFormLimits(MultipartBodyLengthLimit = 1_073_741_824)]
    public Task<IActionResult> Importar(string tipo, IFormFile? arquivo, CancellationToken ct)
        => Run(async () =>
        {
            var destino = CvmDatasets.Itr.Resolver(tipo);
            if (destino is null)
                return BadRequest(new { message = $"Tipo de demonstracao nao suportado pela base ITR: '{tipo}'. Apenas DRE e aceito." });

            if (arquivo is null || arquivo.Length == 0)
                return BadRequest(new { message = "Envie o arquivo CSV no campo 'arquivo'." });

            var pastaImports = Path.Combine(_environment.ContentRootPath, "App_Data", "imports");
            Directory.CreateDirectory(pastaImports);
            var caminhoTemp = Path.Combine(pastaImports, $"{Guid.NewGuid():N}.csv");

            await using (var stream = new FileStream(caminhoTemp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await arquivo.CopyToAsync(stream, ct);
            }

            var bytesTotais = new FileInfo(caminhoTemp).Length;
            var usuario = User.Identity?.Name;
            var job = _jobs.Iniciar(
                "ITR", destino.Demonstracao.Tipo, destino.Demonstracao.Descricao, destino.Tabela,
                arquivo.FileName, caminhoTemp, bytesTotais, usuario);

            return Accepted(new { jobId = job.Id });
        }, ct);

    /// <summary>Status de um job de importacao para polling. 404 se inexistente/expirado.</summary>
    [HttpGet("importar/status/{jobId}")]
    public IActionResult ImportarStatus(string jobId)
    {
        var job = _jobs.Obter(jobId);
        if (job is null || job.Base != "ITR")
            return NotFound(new { message = "Job de importacao nao encontrado ou ja expirado." });

        return Ok(job.Snapshot());
    }

    /// <summary>Cancela um job de importacao ITR em andamento (commit incremental preservado).</summary>
    [HttpDelete("importar/{jobId}")]
    public IActionResult CancelarImportacao(string jobId)
    {
        var job = _jobs.Obter(jobId);
        if (job is null || job.Base != "ITR")
            return NotFound(new { message = "Job de importacao nao encontrado ou ja expirado." });

        _jobs.Cancelar(jobId);
        return Ok(new { message = "Cancelamento solicitado." });
    }

    /// <summary>
    /// Lista companhias com DRE trimestral disponivel (seletor de empresa). Filtros opcionais:
    /// <c>busca</c> (razao social ou CNPJ) e <c>limite</c> (padrao 50).
    /// </summary>
    [HttpGet("empresas")]
    public Task<IActionResult> Empresas(
        [FromQuery] string? busca,
        [FromQuery] int limite,
        CancellationToken ct)
        => Run(async () => Ok(await _repository.GetEmpresasAsync(busca, limite <= 0 ? 50 : limite, ct)), ct);

    /// <summary>
    /// Serie trimestral da DRE do CNPJ (valores ja de-acumulados). Filtros opcionais:
    /// <c>conjunto</c> (CON/IND) e <c>ano</c>.
    /// </summary>
    [HttpGet("{cnpj}/dre/trimestral")]
    public Task<IActionResult> DreTrimestral(
        string cnpj,
        [FromQuery] string? conjunto,
        [FromQuery] int? ano,
        CancellationToken ct)
        => Run(async () => Ok(await _repository.GetDreTrimestralAsync(cnpj, conjunto, ano, ct)), ct);

    /// <summary>
    /// Comparativo Penultimo x Ultimo da DRE (YoY por padrao). Filtros: <c>conjunto</c>
    /// (CON/IND), <c>ano</c> (do periodo "ultimo"; padrao = mais recente) e <c>modo</c>
    /// (<c>homologo</c> ou <c>sequencial</c>). Valores ja em R$.
    /// </summary>
    [HttpGet("{cnpj}/dre/comparativo")]
    public Task<IActionResult> DreComparativo(
        string cnpj,
        [FromQuery] string? conjunto,
        [FromQuery] int? ano,
        [FromQuery] string? modo,
        CancellationToken ct)
        => Run(async () => Ok(await _comparativo.ObterAsync(
            "ITR", cnpj, conjunto, ano, ParseModo(modo), ct)), ct);

    /// <summary>Resolve o modo do comparativo (padrao Homologo). "sequencial"/"qoq" -> Sequencial.</summary>
    private static Models.Itr.ModoComparativo ParseModo(string? modo)
    {
        var m = modo?.Trim().ToLowerInvariant();
        return m is "sequencial" or "seq" or "qoq"
            ? Models.Itr.ModoComparativo.Sequencial
            : Models.Itr.ModoComparativo.Homologo;
    }

    /// <summary>
    /// Recalcula a trimestralizacao sob demanda. Filtros opcionais (query): <c>cnpj</c>,
    /// <c>conjunto</c> (CON/IND) e <c>ano</c>. Sem filtros, recalcula tudo. Retorna o total gravado.
    /// </summary>
    [HttpPost("dre/trimestralizar")]
    public Task<IActionResult> Trimestralizar(
        [FromQuery] string? cnpj,
        [FromQuery] string? conjunto,
        [FromQuery] int? ano,
        CancellationToken ct)
        => Run(async () =>
        {
            var gravadas = await _engine.RecalcularAsync(ano, conjunto, cnpj, ct);
            return Ok(new { linhasGravadas = gravadas });
        }, ct);

    /// <summary>
    /// Historico do ledger itr.Importacao (mais recente primeiro). Filtros opcionais por
    /// <c>tipo</c> e <c>ano</c>; <c>limite</c> de linhas (padrao 100).
    /// </summary>
    [HttpGet("importacoes")]
    public Task<IActionResult> Importacoes(
        [FromQuery] string? tipo,
        [FromQuery] int? ano,
        [FromQuery] int limite,
        CancellationToken ct)
        => Run(async () => Ok(await _repository.GetImportacoesAsync(tipo, ano, limite <= 0 ? 100 : limite, ct)), ct);

    /// <summary>Garante conexao + schema e padroniza o tratamento de erros das acoes.</summary>
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action, CancellationToken ct)
    {
        if (!_factory.IsConfigured)
        {
            return StatusCode(StatusCodes.Status409Conflict,
                new { message = "Conexao nao configurada. Configure a conexao antes de usar a estrutura ITR." });
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
            _logger.LogError(ex, "Falha na operacao da estrutura ITR");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "Falha ao acessar o banco de dados. Verifique a configuracao e tente novamente." });
        }
    }
}
