using System.Security.Claims;
using AustralCreditAnalytics.Api.Models.Auth;
using AustralCreditAnalytics.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AustralCreditAnalytics.Api.Controllers;

/// <summary>Cadastro e gestao de usuarios. Restrito ao perfil Gestor.</summary>
[ApiController]
[Route("api/usuarios")]
[Authorize(Roles = Perfis.Gestor)]
public class UsuariosController : ControllerBase
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IPasswordHasher _hasher;
    private readonly IActivityLogger _activity;
    private readonly ISqlConnectionFactory _factory;
    private readonly ISchemaInitializer _schema;
    private readonly ILogger<UsuariosController> _logger;

    public UsuariosController(
        IUsuarioRepository usuarios,
        IPasswordHasher hasher,
        IActivityLogger activity,
        ISqlConnectionFactory factory,
        ISchemaInitializer schema,
        ILogger<UsuariosController> logger)
    {
        _usuarios = usuarios;
        _hasher = hasher;
        _activity = activity;
        _factory = factory;
        _schema = schema;
        _logger = logger;
    }

    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct)
        => Run(async () => Ok(await _usuarios.ListAsync(ct)), ct);

    [HttpPost]
    public Task<IActionResult> Create([FromBody] CreateUsuarioRequest request, CancellationToken ct)
        => Run(async () =>
        {
            var perfilCodigo = (request.PerfilCodigo ?? string.Empty).Trim().ToUpperInvariant();
            if (perfilCodigo != Perfis.Gestor && perfilCodigo != Perfis.Operador)
                return BadRequest(new { message = "Perfil invalido. Use GESTOR ou OPERADOR." });

            var login = request.Login.Trim();
            if (await _usuarios.LoginExistsAsync(login, ct))
                return Conflict(new { message = "Ja existe um usuario com esse login." });

            var perfilId = await _usuarios.GetPerfilIdByCodigoAsync(perfilCodigo, ct);
            if (perfilId is null)
                return BadRequest(new { message = "Perfil invalido." });

            var hash = _hasher.Hash(request.Senha);
            var id = await _usuarios.CreateAsync(
                login, request.NomeCompleto.Trim(), string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                hash, perfilId.Value, mustChangePassword: true, criadoPor: CurrentLogin(), ct);

            await _activity.LogAsync(CurrentUserId(), CurrentLogin(), "USUARIO_CRIADO", "Usuario",
                $"Criou o usuario '{login}' (perfil {perfilCodigo}).", ClientIp(), UserAgent(), ct);

            var view = await _usuarios.GetViewByIdAsync(id, ct);
            return Ok(new { ok = true, usuario = view });
        }, ct);

    [HttpPut("{id:long}/status")]
    public Task<IActionResult> SetStatus(long id, [FromBody] UpdateStatusRequest request, CancellationToken ct)
        => Run(async () =>
        {
            if (id == CurrentUserId() && !request.Ativo)
                return BadRequest(new { message = "Voce nao pode desativar o proprio usuario." });

            var ok = await _usuarios.SetStatusAsync(id, request.Ativo, ct);
            if (!ok)
                return NotFound(new { message = "Usuario nao encontrado." });

            await _activity.LogAsync(CurrentUserId(), CurrentLogin(),
                request.Ativo ? "USUARIO_ATIVADO" : "USUARIO_DESATIVADO", "Usuario",
                $"Alterou o status do usuario #{id} para {(request.Ativo ? "ativo" : "inativo")}.",
                ClientIp(), UserAgent(), ct);

            var view = await _usuarios.GetViewByIdAsync(id, ct);
            return Ok(new { ok = true, usuario = view });
        }, ct);

    [HttpPost("{id:long}/reset-senha")]
    public Task<IActionResult> ResetSenha(long id, [FromBody] ResetPasswordRequest request, CancellationToken ct)
        => Run(async () =>
        {
            var alvo = await _usuarios.GetViewByIdAsync(id, ct);
            if (alvo is null)
                return NotFound(new { message = "Usuario nao encontrado." });

            var hash = _hasher.Hash(request.NovaSenha);
            await _usuarios.UpdatePasswordAsync(id, hash, mustChangePassword: true, ct);

            await _activity.LogAsync(CurrentUserId(), CurrentLogin(), "RESET_SENHA", "Usuario",
                $"Resetou a senha do usuario '{alvo.Login}'.", ClientIp(), UserAgent(), ct);

            return Ok(new { ok = true, message = "Senha resetada. O usuario devera troca-la no proximo acesso." });
        }, ct);

    /// <summary>Garante conexao + schema e padroniza o tratamento de erros das acoes.</summary>
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action, CancellationToken ct)
    {
        if (!_factory.IsConfigured)
        {
            return StatusCode(StatusCodes.Status409Conflict,
                new { message = "Conexao nao configurada. Configure a conexao antes de gerenciar usuarios." });
        }

        try
        {
            await _schema.EnsureInitializedAsync(ct);
            return await action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha no gerenciamento de usuarios");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "Falha ao acessar o banco de dados. Verifique a configuracao e tente novamente." });
        }
    }

    private long? CurrentUserId()
        => long.TryParse(User.FindFirstValue(JwtTokenService.SubClaim), out var id) ? id : null;

    private string? CurrentLogin() => User.FindFirstValue(JwtTokenService.LoginClaim);
    private string? ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? UserAgent() => Request.Headers.UserAgent.ToString();
}
