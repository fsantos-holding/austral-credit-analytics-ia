using AustralCreditAnalytics.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Erros de validacao de modelo devolvem { message } (esperado pelo frontend),
// usando a primeira mensagem PT-BR definida via DataAnnotations no model.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var message = context.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
            ?? "Dados invalidos.";
        return new BadRequestObjectResult(new { message });
    };
});

// Data Protection: chaves persistidas em App_Data/keys para cifrar segredos em repouso.
var keysDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
try
{
    Directory.CreateDirectory(keysDir);
}
catch (Exception ex)
{
    throw new InvalidOperationException(
        $"Nao foi possivel criar a pasta de chaves em '{keysDir}'. " +
        "Conceda permissao de Modificar ao identity do Application Pool do IIS em App_Data/.", ex);
}
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDir))
    .SetApplicationName("AustralCreditAnalytics");

// Conexao SQL: config cifrada salva pela tela + fabrica + teste de conectividade.
builder.Services.AddSingleton<IConnectionConfigStore, ConnectionConfigStore>();
builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IConnectionTester, ConnectionTester>();

// Persistencia + auto-migracao versionada (idempotente) dos schemas seg/credito/dfp.
builder.Services.AddSingleton<ISchemaInitializer, SqlMigrationRunner>();
builder.Services.AddScoped<IAnaliseCreditoRepository, AnaliseCreditoRepository>();

// CVM: nucleo de import compartilhado (DFP/ITR) + ledger generico parametrizado por tabela.
builder.Services.AddScoped<ICvmImportacaoLog, CvmImportacaoLog>();
builder.Services.AddScoped<CvmCsvImporter>();

// DFP (CVM, anual): importacao dos CSVs por demonstracao + rastreio da estrutura por CNPJ.
builder.Services.AddScoped<IDfpImportService, DfpImportService>();
builder.Services.AddScoped<IDfpRepository, DfpRepository>();
builder.Services.AddSingleton<IDfpImportJobManager, DfpImportJobManager>();

// ITR (CVM, trimestral): importacao isolada (schema [itr]), motor de trimestralizacao e leitura.
builder.Services.AddScoped<IItrImportService, ItrImportService>();
builder.Services.AddScoped<IItrRepository, ItrRepository>();
builder.Services.AddScoped<ITrimestralizacaoService, TrimestralizacaoService>();
builder.Services.AddScoped<IDreComparativoService, DreComparativoService>();

// FRE (CVM, Formulario de Referencia): importacao dos ~50 modelos (schema [fre]) + leitura do grupo de capital.
builder.Services.AddScoped<IFreImportService, FreImportService>();
builder.Services.AddScoped<IFreRepository, FreRepository>();

// Autenticacao/permissionamento: usuarios, perfis e log de atividades no proprio banco.
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IActivityLogger, ActivityLogger>();

// IA: configuracao cifrada das chaves (OpenAI/Anthropic) + analise de risco de credito.
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IIaConfigStore, IaConfigStore>();
builder.Services.AddScoped<IIaCreditoService, IaCreditoService>();

// Token JWT: a chave vem de Auth:JwtKey ou e gerada/persistida em App_Data/jwt.key.
var jwtTokenService = new JwtTokenService(builder.Configuration, builder.Environment);
builder.Services.AddSingleton<IJwtTokenService>(jwtTokenService);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Sem remapeamento de entrada: os tipos das claims sao lidos como emitidos (sub/login/role).
        options.MapInboundClaims = false;
        options.TokenValidationParameters = jwtTokenService.GetValidationParameters();
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Auto-migracao dos schemas na inicializacao (apenas se ja configurado).
// Idempotente e tolerante a falha: nao derruba a aplicacao.
using (var scope = app.Services.CreateScope())
{
    var schema = scope.ServiceProvider.GetRequiredService<ISchemaInitializer>();
    _ = schema.EnsureInitializedAsync();
}

app.UseExceptionHandler(handler =>
{
    handler.Run(async context =>
    {
        var ex = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        if (ex is not null)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("GlobalExceptionHandler");
            logger.LogError(ex, "Erro nao tratado ao processar {Path}", context.Request.Path);
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { message = "Erro interno do servidor." });
    });
});

// Subaplicacao no IIS (ex.: /austral-credit-analytics-ia): ASPNETCORE_APPL_PATH ou PathBase no appsettings.
var pathBase = builder.Configuration["PathBase"]
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_APPL_PATH");
if (!string.IsNullOrWhiteSpace(pathBase))
{
    pathBase = pathBase.TrimEnd('/');
    app.UsePathBase(pathBase);
}

// Serve o index.html (e demais assets) a partir de wwwroot, evitando CORS.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
