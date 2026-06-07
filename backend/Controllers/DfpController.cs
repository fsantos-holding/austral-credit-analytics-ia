using AustralCreditAnalytics.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AustralCreditAnalytics.Api.Controllers;

/// <summary>
/// Estrutura DFP (Demonstracoes Financeiras Padronizadas / CVM): importacao dos
/// CSVs por demonstracao e rastreio de toda a estrutura por CNPJ.
/// </summary>
[ApiController]
[Route("api/dfp")]
[Authorize]
public class DfpController : ControllerBase
{
    private readonly IDfpImportJobManager _jobs;
    private readonly IDfpRepository _repository;
    private readonly ISqlConnectionFactory _factory;
    private readonly ISchemaInitializer _schema;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DfpController> _logger;

    public DfpController(
        IDfpImportJobManager jobs,
        IDfpRepository repository,
        ISqlConnectionFactory factory,
        ISchemaInitializer schema,
        IWebHostEnvironment environment,
        ILogger<DfpController> logger)
    {
        _jobs = jobs;
        _repository = repository;
        _factory = factory;
        _schema = schema;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>Lista os tipos de demonstracao suportados (chave + tabela + descricao).</summary>
    [HttpGet("tipos")]
    public IActionResult Tipos()
        => Ok(DfpDemonstracaoRegistry.Listar()
            .Select(d => new { tipo = d.Tipo, tabela = d.Tabela, descricao = d.Descricao }));

    /// <summary>
    /// Inicia a importacao de um CSV da CVM para a demonstracao informada como um job
    /// em segundo plano. Envie o arquivo no campo multipart <c>arquivo</c>. O arquivo e
    /// salvo em disco e processado em lotes; retorna <c>202 Accepted</c> com o
    /// <c>jobId</c> para acompanhamento via GET importar/status/{jobId}.
    /// </summary>
    [HttpPost("importar/{tipo}")]
    [RequestSizeLimit(1_073_741_824)] // 1 GiB: os CSVs anuais da CVM podem ser grandes.
    [RequestFormLimits(MultipartBodyLengthLimit = 1_073_741_824)]
    public Task<IActionResult> Importar(string tipo, IFormFile? arquivo, CancellationToken ct)
        => Run(async () =>
        {
            var dem = DfpDemonstracaoRegistry.Resolver(tipo);
            if (dem is null)
                return BadRequest(new { message = $"Tipo de demonstracao desconhecido: '{tipo}'. Consulte GET /api/dfp/tipos." });

            if (arquivo is null || arquivo.Length == 0)
                return BadRequest(new { message = "Envie o arquivo CSV no campo 'arquivo'." });

            var pastaImports = Path.Combine(_environment.ContentRootPath, "App_Data", "imports");
            Directory.CreateDirectory(pastaImports);
            var caminhoTemp = Path.Combine(pastaImports, $"{Guid.NewGuid():N}.csv");

            await using (var destino = new FileStream(caminhoTemp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await arquivo.CopyToAsync(destino, ct);
            }

            var bytesTotais = new FileInfo(caminhoTemp).Length;
            var usuario = User.Identity?.Name;
            var job = _jobs.Iniciar(
                dem.Tipo, dem.Descricao, dem.Tabela, arquivo.FileName, caminhoTemp, bytesTotais, usuario);

            return Accepted(new { jobId = job.Id });
        }, ct);

    /// <summary>Status de um job de importacao para polling. 404 se inexistente/expirado.</summary>
    [HttpGet("importar/status/{jobId}")]
    public IActionResult ImportarStatus(string jobId)
    {
        var job = _jobs.Obter(jobId);
        if (job is null)
            return NotFound(new { message = "Job de importacao nao encontrado ou ja expirado." });

        return Ok(job.Snapshot());
    }

    /// <summary>Cancela um job de importacao em andamento (commit incremental preservado).</summary>
    [HttpDelete("importar/{jobId}")]
    public IActionResult CancelarImportacao(string jobId)
    {
        if (!_jobs.Cancelar(jobId))
            return NotFound(new { message = "Job de importacao nao encontrado ou ja expirado." });

        return Ok(new { message = "Cancelamento solicitado." });
    }

    /// <summary>Mapa da estrutura disponivel para um CNPJ (documentos + contagem por demonstracao).</summary>
    [HttpGet("{cnpj}/estrutura")]
    public Task<IActionResult> Estrutura(string cnpj, CancellationToken ct)
        => Run(async () => Ok(await _repository.GetEstruturaAsync(cnpj, ct)), ct);

    /// <summary>
    /// Contas consolidadas do CNPJ (todas as demonstracoes por conta). Filtros opcionais:
    /// <c>tipo</c> (DRE, BPA, ...), <c>dtRefer</c> (yyyyMMdd), <c>ordem</c> (ULTIMO/PENULTIMO),
    /// <c>conjunto</c> (CON/IND) e <c>ano</c>.
    /// </summary>
    [HttpGet("{cnpj}/contas")]
    public Task<IActionResult> Contas(
        string cnpj,
        [FromQuery] string? tipo,
        [FromQuery] string? dtRefer,
        [FromQuery] string? ordem,
        [FromQuery] string? conjunto,
        [FromQuery] int? ano,
        CancellationToken ct)
        => Run(async () => Ok(await _repository.GetContasAsync(cnpj, tipo, dtRefer, ordem, conjunto, ano, ct)), ct);

    /// <summary>
    /// Historico de importacoes (ledger dfp.Importacao), mais recente primeiro. Filtros
    /// opcionais por <c>tipo</c> e <c>ano</c>; <c>limite</c> de linhas (padrao 100).
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
                new { message = "Conexao nao configurada. Configure a conexao antes de usar a estrutura DFP." });
        }

        try
        {
            await _schema.EnsureInitializedAsync(ct);
            return await action();
        }
        catch (InvalidOperationException ex)
        {
            // Mensagens de validacao da importacao/leitura sao seguras para exibir.
            return StatusCode(StatusCodes.Status409Conflict, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na operacao da estrutura DFP");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "Falha ao acessar o banco de dados. Verifique a configuracao e tente novamente." });
        }
    }
}
