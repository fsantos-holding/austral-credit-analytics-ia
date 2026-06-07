using System.Security.Claims;
using AustralCreditAnalytics.Api.Models.Auth;
using AustralCreditAnalytics.Api.Models.Ia;
using AustralCreditAnalytics.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AustralCreditAnalytics.Api.Controllers;

/// <summary>
/// Configuracao e teste dos provedores de IA (OpenAI/Anthropic/compativeis).
/// Restrito ao perfil Gestor. Alteracoes de configuracao sao registradas no log
/// de atividade (seg.LogAtividade).
/// </summary>
[ApiController]
[Route("api/ia")]
[Authorize(Roles = Perfis.Gestor)]
public class IaController : ControllerBase
{
    private readonly IIaConfigStore _configStore;
    private readonly IIaCreditoService _credito;
    private readonly IActivityLogger _activity;

    public IaController(
        IIaConfigStore configStore,
        IIaCreditoService credito,
        IActivityLogger activity)
    {
        _configStore = configStore;
        _credito = credito;
        _activity = activity;
    }

    /// <summary>Configuracao de IA com as chaves mascaradas.</summary>
    [HttpGet("config")]
    public ActionResult<IaConfigView> GetConfig() => Ok(_configStore.LoadMasked());

    /// <summary>Salva a configuracao de IA (chaves cifradas; vazias mantem a anterior).</summary>
    [HttpPost("config")]
    public async Task<ActionResult> SaveConfig([FromBody] SalvarIaConfigRequest request, CancellationToken ct)
    {
        _configStore.Save(request);
        await _activity.LogAsync(CurrentUserId(), CurrentLogin(), "IA_CONFIG_SALVA", "ConfigIA",
            $"Atualizou a configuracao de IA (provedor ativo: {request.ProvedorAtivo}).",
            ClientIp(), UserAgent(), ct);
        return Ok(new { ok = true, message = "Configuracao de IA salva com sucesso." });
    }

    /// <summary>Valida a chave do provedor ativo.</summary>
    [HttpPost("config/test")]
    public async Task<ActionResult> TestConfig(CancellationToken ct)
    {
        var (ok, mensagem) = await _credito.TestarAsync(ct);
        return ok
            ? Ok(new { ok = true, message = mensagem })
            : BadRequest(new { message = mensagem });
    }

    private long? CurrentUserId()
        => long.TryParse(User.FindFirstValue(JwtTokenService.SubClaim), out var id) ? id : null;

    private string? CurrentLogin() => User.FindFirstValue(JwtTokenService.LoginClaim);
    private string? ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? UserAgent() => Request.Headers.UserAgent.ToString();
}
