using AustralCreditAnalytics.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AustralCreditAnalytics.Api.Controllers;

/// <summary>
/// Formulario de Referencia (FRE / CVM): importacao dos ~50 modelos FRE para o schema
/// [fre] (uma tabela por modelo) e leitura do grupo de capital para a secao premium
/// "Composicao Capital". Endpoints independentes de DFP/ITR; reaproveita o nucleo de
/// import (<see cref="CvmCsvImporter"/>) e o gerenciador de jobs em memoria.
/// </summary>
[ApiController]
[Route("api/fre")]
[Authorize]
public class FreController : ControllerBase
{
    private readonly IDfpImportJobManager _jobs;
    private readonly IFreRepository _repository;
    private readonly ISqlConnectionFactory _factory;
    private readonly ISchemaInitializer _schema;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FreController> _logger;

    public FreController(
        IDfpImportJobManager jobs,
        IFreRepository repository,
        ISqlConnectionFactory factory,
        ISchemaInitializer schema,
        IWebHostEnvironment environment,
        ILogger<FreController> logger)
    {
        _jobs = jobs;
        _repository = repository;
        _factory = factory;
        _schema = schema;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>Lista os modelos FRE suportados (chave + tabela + descricao).</summary>
    [HttpGet("tipos")]
    public IActionResult Tipos()
        => Ok(FreModeloRegistry.Listar()
            .Select(d => new { tipo = d.Tipo, tabela = d.Tabela, descricao = d.Descricao }));

    /// <summary>
    /// Identifica o modelo FRE pelo nome do arquivo (ex.: "fre_cia_aberta_capital_social_2024.csv"
    /// -> capital_social). Retorna 404 se o nome nao for reconhecido. Util para o front pre-validar.
    /// </summary>
    [HttpGet("identificar")]
    public IActionResult Identificar([FromQuery] string? arquivo)
    {
        var tipo = FreModeloRegistry.IdentificarPorArquivo(arquivo);
        if (tipo is null)
            return NotFound(new { message = $"Nao foi possivel identificar um modelo FRE no nome '{arquivo}'." });

        var dem = FreModeloRegistry.Resolver(tipo)!;
        return Ok(new { tipo = dem.Tipo, tabela = dem.Tabela, descricao = dem.Descricao });
    }

    /// <summary>
    /// Inicia a importacao de um CSV FRE para o modelo informado como job em segundo plano.
    /// Envie o arquivo no campo multipart <c>arquivo</c>. Retorna 202 com o <c>jobId</c>.
    /// </summary>
    [HttpPost("importar/{tipo}")]
    [RequestSizeLimit(1_073_741_824)] // 1 GiB: os CSVs da CVM podem ser grandes.
    [RequestFormLimits(MultipartBodyLengthLimit = 1_073_741_824)]
    public Task<IActionResult> Importar(string tipo, IFormFile? arquivo, CancellationToken ct)
        => Run(async () =>
        {
            var dem = FreModeloRegistry.Resolver(tipo);
            if (dem is null)
                return BadRequest(new { message = $"Modelo FRE desconhecido: '{tipo}'. Consulte GET /api/fre/tipos." });

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
                "FRE", dem.Tipo, dem.Descricao, dem.Tabela, arquivo.FileName, caminhoTemp, bytesTotais, usuario);

            return Accepted(new { jobId = job.Id });
        }, ct);

    /// <summary>Status de um job de importacao FRE para polling. 404 se inexistente/expirado.</summary>
    [HttpGet("importar/status/{jobId}")]
    public IActionResult ImportarStatus(string jobId)
    {
        var job = _jobs.Obter(jobId);
        if (job is null || job.Base != "FRE")
            return NotFound(new { message = "Job de importacao nao encontrado ou ja expirado." });

        return Ok(job.Snapshot());
    }

    /// <summary>Cancela um job de importacao FRE em andamento (commit incremental preservado).</summary>
    [HttpDelete("importar/{jobId}")]
    public IActionResult CancelarImportacao(string jobId)
    {
        var job = _jobs.Obter(jobId);
        if (job is null || job.Base != "FRE")
            return NotFound(new { message = "Job de importacao nao encontrado ou ja expirado." });

        _jobs.Cancelar(jobId);
        return Ok(new { message = "Cancelamento solicitado." });
    }

    /// <summary>
    /// Historico do ledger fre.Importacao (mais recente primeiro). Filtros opcionais por
    /// <c>tipo</c> e <c>ano</c>; <c>limite</c> de linhas (padrao 100).
    /// </summary>
    [HttpGet("importacoes")]
    public Task<IActionResult> Importacoes(
        [FromQuery] string? tipo,
        [FromQuery] int? ano,
        [FromQuery] int limite,
        CancellationToken ct)
        => Run(async () => Ok(await _repository.GetImportacoesAsync(tipo, ano, limite <= 0 ? 100 : limite, ct)), ct);

    /// <summary>
    /// Companhias com documentos FRE disponiveis (seletor de empresa). Filtros opcionais:
    /// <c>busca</c> (razao social ou CNPJ) e <c>limite</c> (padrao 50).
    /// </summary>
    [HttpGet("empresas")]
    public Task<IActionResult> Empresas(
        [FromQuery] string? busca,
        [FromQuery] int limite,
        CancellationToken ct)
        => Run(async () => Ok(await _repository.GetEmpresasAsync(busca, limite <= 0 ? 50 : limite, ct)), ct);

    /// <summary>Anos de referencia disponiveis para o CNPJ (+ maior versao).</summary>
    [HttpGet("{cnpj}/anos")]
    public Task<IActionResult> Anos(string cnpj, CancellationToken ct)
        => Run(async () => Ok(await _repository.GetAnosAsync(cnpj, ct)), ct);

    /// <summary>
    /// Le um modelo FRE para a companhia. Filtros opcionais: <c>ano</c> e <c>versao</c>.
    /// Retorna as linhas mestre e, quando aplicavel, o detalhe da tabela filha (master/detail).
    /// </summary>
    [HttpGet("{cnpj}/modelo/{modelo}")]
    public Task<IActionResult> Modelo(
        string cnpj,
        string modelo,
        [FromQuery] int? ano,
        [FromQuery] int? versao,
        CancellationToken ct)
        => Run(async () =>
        {
            var dados = await _repository.GetModeloAsync(cnpj, modelo, ano, versao, ct);
            return dados is null
                ? BadRequest(new { message = $"Modelo FRE desconhecido: '{modelo}'. Consulte GET /api/fre/tipos." })
                : Ok(dados);
        }, ct);

    /// <summary>Resumo da composicao de capital (KPIs ON/PN/free float) por ano.</summary>
    [HttpGet("{cnpj}/capital-resumo")]
    public Task<IActionResult> CapitalResumo(string cnpj, [FromQuery] int? ano, CancellationToken ct)
        => Run(async () => Ok(await _repository.GetCapitalResumoAsync(cnpj, ano, ct)), ct);

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
            _logger.LogError(ex, "Falha na operacao da estrutura FRE");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "Falha ao acessar o banco de dados. Verifique a configuracao e tente novamente." });
        }
    }
}
