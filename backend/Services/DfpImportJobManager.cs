using System.Collections.Concurrent;
using AustralCreditAnalytics.Api.Models.Dfp;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Implementacao em memoria de <see cref="IDfpImportJobManager"/>. Os jobs vivem em
/// um <see cref="ConcurrentDictionary{TKey,TValue}"/>; o processamento roda em uma
/// Task de segundo plano que cria seu proprio escopo de DI (via
/// <see cref="IServiceScopeFactory"/>) para resolver o <see cref="IDfpImportService"/>.
/// Jobs concluidos sao descartados apos <see cref="RetencaoJobConcluido"/>. Como tudo
/// e mantido em memoria, um recycle do processo descarta os jobs em andamento.
/// </summary>
public sealed class DfpImportJobManager : IDfpImportJobManager
{
    private static readonly TimeSpan RetencaoJobConcluido = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, DfpImportJob> _jobs = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DfpImportJobManager> _logger;

    public DfpImportJobManager(IServiceScopeFactory scopeFactory, ILogger<DfpImportJobManager> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public DfpImportJob Iniciar(
        string tipo, string modelo, string tabela, string arquivo, string caminhoTemp,
        long bytesTotais, string? usuario)
    {
        LimparAntigos();

        var job = new DfpImportJob
        {
            Tipo = tipo,
            Modelo = modelo,
            Tabela = tabela,
            Arquivo = arquivo,
            CaminhoTemp = caminhoTemp,
            BytesTotais = bytesTotais,
            Usuario = usuario,
        };

        _jobs[job.Id] = job;

        // Fire-and-forget: o processamento roda independente do request original.
        _ = Task.Run(() => ProcessarAsync(job));

        return job;
    }

    public DfpImportJob? Obter(string id) => _jobs.TryGetValue(id, out var job) ? job : null;

    public bool Cancelar(string id)
    {
        if (!_jobs.TryGetValue(id, out var job))
            return false;

        try
        {
            job.Cancelamento.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Job ja finalizou e liberou o token; cancelamento e no-op.
        }

        return true;
    }

    private async Task ProcessarAsync(DfpImportJob job)
    {
        var ct = job.Cancelamento.Token;
        try
        {
            job.MarcarProcessando();

            using var scope = _scopeFactory.CreateScope();
            var import = scope.ServiceProvider.GetRequiredService<IDfpImportService>();

            var progresso = new Progress<(long bytes, int lidas, int importadas, int removidas, string? conjunto, int? ano)>(
                p => job.AtualizarProgresso(p.bytes, p.lidas, p.importadas, p.removidas, p.conjunto, p.ano));

            var resultado = await import.ImportarArquivoAsync(
                job.Tipo, job.CaminhoTemp, job.Arquivo, job.Usuario, progresso, ct);

            job.AtualizarProgresso(
                job.BytesTotais, resultado.LinhasLidas, resultado.LinhasImportadas,
                resultado.LinhasRemovidas, resultado.Conjunto, resultado.Ano);
            job.Finalizar(DfpImportStatus.Concluido,
                MontarMensagem(resultado));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            job.Finalizar(DfpImportStatus.Cancelado,
                $"Importacao cancelada. {job.LinhasImportadas} linha(s) ja gravada(s) foram mantidas.");
            _logger.LogInformation("Job de importacao DFP {JobId} ({Tipo}) cancelado.", job.Id, job.Tipo);
        }
        catch (Exception ex)
        {
            job.Finalizar(DfpImportStatus.Falha, ex.Message);
            _logger.LogError(ex, "Falha no job de importacao DFP {JobId} ({Tipo}).", job.Id, job.Tipo);
        }
        finally
        {
            TryDeletarArquivo(job.CaminhoTemp);
            try { job.Cancelamento.Dispose(); } catch { /* idempotente */ }
        }
    }

    /// <summary>Mensagem final do job: modelo, conjunto/ano e linhas importadas/removidas.</summary>
    private static string MontarMensagem(DfpImportResult r)
    {
        var rotuloConjunto = r.Conjunto switch
        {
            "CON" => "Consolidado",
            "IND" => "Individual",
            "MIS" => "Misto",
            _ => null,
        };
        var escopo = rotuloConjunto is null
            ? (r.Ano?.ToString() ?? "—")
            : $"{rotuloConjunto}, {r.Ano}";
        var modelo = string.IsNullOrWhiteSpace(r.Modelo) ? r.Tipo : r.Modelo;
        return $"{r.LinhasImportadas} linha(s) importada(s) de {modelo} em {r.Tabela} ({escopo}); " +
               $"{r.LinhasRemovidas} removida(s) antes da carga.";
    }

    private void TryDeletarArquivo(string caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho))
            return;

        try
        {
            if (File.Exists(caminho))
                File.Delete(caminho);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nao foi possivel apagar o arquivo temporario de importacao '{Caminho}'.", caminho);
        }
    }

    /// <summary>Descarta jobs ja finalizados ha mais que <see cref="RetencaoJobConcluido"/>.</summary>
    private void LimparAntigos()
    {
        var limite = DateTime.UtcNow - RetencaoJobConcluido;
        foreach (var kv in _jobs)
        {
            if (kv.Value.Finalizado && kv.Value.AtualizadoEm < limite)
                _jobs.TryRemove(kv.Key, out _);
        }
    }
}
