using System.Security.Claims;
using AustralCreditAnalytics.Api.Models.Auth;
using AustralCreditAnalytics.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AustralCreditAnalytics.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly IActivityLogger _activity;
    private readonly ISqlConnectionFactory _factory;
    private readonly ISchemaInitializer _schema;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUsuarioRepository usuarios,
        IPasswordHasher hasher,
        IJwtTokenService jwt,
        IActivityLogger activity,
        ISqlConnectionFactory factory,
        ISchemaInitializer schema,
        ILogger<AuthController> logger)
    {
        _usuarios = usuarios;
        _hasher = hasher;
        _jwt = jwt;
        _activity = activity;
        _factory = factory;
        _schema = schema;
        _logger = logger;
    }

    /// <summary>Autentica o usuario e emite um JWT. Registra a atividade (sucesso/falha).</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (!_factory.IsConfigured)
        {
            return StatusCode(StatusCodes.Status409Conflict, new
            {
                message = "Conexao com o banco nao configurada. Configure a conexao antes de autenticar."
            });
        }

        try
        {
            await _schema.EnsureInitializedAsync(ct);

            var login = request.Login.Trim();
            var usuario = await _usuarios.GetByLoginAsync(login, ct);

            if (usuario is null || !_hasher.Verify(request.Senha, usuario.SenhaHash))
            {
                await _activity.LogAsync(usuario?.Id, login, "LOGIN_FALHA", "Auth",
                    "Credenciais invalidas.", ClientIp(), UserAgent(), ct);
                return Unauthorized(new { message = "Usuario ou senha invalidos." });
            }

            if (!usuario.Ativo)
            {
                await _activity.LogAsync(usuario.Id, login, "LOGIN_BLOQUEADO", "Auth",
                    "Usuario inativo.", ClientIp(), UserAgent(), ct);
                return Unauthorized(new { message = "Usuario inativo. Procure um gestor." });
            }

            await _usuarios.UpdateLastLoginAsync(usuario.Id, ct);
            var (token, expira) = _jwt.Generate(usuario, request.LembrarMe);

            await _activity.LogAsync(usuario.Id, login, "LOGIN", "Auth",
                request.LembrarMe ? "Login com 'lembrar-me'." : "Login.", ClientIp(), UserAgent(), ct);

            return Ok(new LoginResponse
            {
                Token = token,
                ExpiraEmUtc = expira,
                Usuario = ToView(usuario),
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao autenticar usuario");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "Falha ao acessar o banco de dados de autenticacao. Tente novamente." });
        }
    }

    /// <summary>Dados do usuario autenticado.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var id = CurrentUserId();
        if (id is null)
            return Unauthorized();

        var view = await _usuarios.GetViewByIdAsync(id.Value, ct);
        if (view is null || !view.Ativo)
            return Unauthorized(new { message = "Usuario nao encontrado ou inativo." });

        return Ok(view);
    }

    /// <summary>Troca a senha do proprio usuario autenticado (limpa MustChangePassword).</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var id = CurrentUserId();
        if (id is null)
            return Unauthorized();

        var usuario = await _usuarios.GetByLoginAsync(CurrentLogin() ?? string.Empty, ct);
        if (usuario is null || usuario.Id != id.Value)
            return Unauthorized();

        if (!_hasher.Verify(request.SenhaAtual, usuario.SenhaHash))
            return BadRequest(new { message = "Senha atual incorreta." });

        if (request.SenhaAtual == request.NovaSenha)
            return BadRequest(new { message = "A nova senha deve ser diferente da atual." });

        var novoHash = _hasher.Hash(request.NovaSenha);
        await _usuarios.UpdatePasswordAsync(usuario.Id, novoHash, mustChangePassword: false, ct);

        await _activity.LogAsync(usuario.Id, usuario.Login, "TROCA_SENHA", "Auth",
            "Senha alterada pelo proprio usuario.", ClientIp(), UserAgent(), ct);

        return Ok(new { ok = true, message = "Senha alterada com sucesso." });
    }

    /// <summary>Registra o logout (o descarte do token ocorre no navegador).</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await _activity.LogAsync(CurrentUserId(), CurrentLogin(), "LOGOUT", "Auth",
            null, ClientIp(), UserAgent(), ct);
        return Ok(new { ok = true });
    }

    private long? CurrentUserId()
        => long.TryParse(User.FindFirstValue(JwtTokenService.SubClaim), out var id) ? id : null;

    private string? CurrentLogin() => User.FindFirstValue(JwtTokenService.LoginClaim);

    private string? ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? UserAgent() => Request.Headers.UserAgent.ToString();

    private static UsuarioView ToView(Usuario u) => new()
    {
        Id = u.Id,
        Login = u.Login,
        NomeCompleto = u.NomeCompleto,
        Email = u.Email,
        PerfilId = u.PerfilId,
        PerfilCodigo = u.PerfilCodigo,
        PerfilNome = u.PerfilCodigo == Perfis.Gestor ? "Gestor" : "Operador",
        Ativo = u.Ativo,
        MustChangePassword = u.MustChangePassword,
        DataCriacaoUtc = u.DataCriacaoUtc,
        CriadoPor = u.CriadoPor,
        UltimoLoginUtc = u.UltimoLoginUtc,
    };
}
