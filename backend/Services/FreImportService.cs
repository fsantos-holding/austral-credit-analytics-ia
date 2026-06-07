using AustralCreditAnalytics.Api.Models.Dfp;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Importacao FRE (Formulario de Referencia, schema [fre]). Adaptador fino sobre o nucleo
/// <see cref="CvmCsvImporter"/>, fixando o descritor <see cref="CvmDatasets.Fre"/> (grava na
/// tabela fre.* do modelo resolvido e registra em fre.Importacao).
/// </summary>
public class FreImportService : IFreImportService
{
    private readonly CvmCsvImporter _importer;

    public FreImportService(CvmCsvImporter importer) => _importer = importer;

    public Task<DfpImportResult> ImportarArquivoAsync(
        string tipo,
        string caminhoArquivo,
        string nomeArquivo,
        string? usuario,
        IProgress<(long bytes, int lidas, int importadas, int removidas, string? conjunto, int? ano)>? progresso = null,
        CancellationToken ct = default)
        => _importer.ImportarArquivoAsync(
            CvmDatasets.Fre, tipo, caminhoArquivo, nomeArquivo, usuario, progresso, ct);
}
