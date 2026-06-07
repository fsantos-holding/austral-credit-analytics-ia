using AustralCreditAnalytics.Api.Models.Dfp;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Importacao ITR (trimestral, schema [itr]). Adaptador fino sobre o nucleo
/// <see cref="CvmCsvImporter"/>, fixando o descritor <see cref="CvmDatasets.Itr"/>
/// (so DRE, gravando em itr.Dre e registrando em itr.Importacao).
/// </summary>
public class ItrImportService : IItrImportService
{
    private readonly CvmCsvImporter _importer;

    public ItrImportService(CvmCsvImporter importer) => _importer = importer;

    public Task<DfpImportResult> ImportarArquivoAsync(
        string tipo,
        string caminhoArquivo,
        string nomeArquivo,
        string? usuario,
        IProgress<(long bytes, int lidas, int importadas, int removidas, string? conjunto, int? ano)>? progresso = null,
        CancellationToken ct = default)
        => _importer.ImportarArquivoAsync(
            CvmDatasets.Itr, tipo, caminhoArquivo, nomeArquivo, usuario, progresso, ct);
}
