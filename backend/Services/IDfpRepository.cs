using AustralCreditAnalytics.Api.Models.Dfp;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>Leitura da estrutura DFP por CNPJ (rastreio + contas).</summary>
public interface IDfpRepository
{
    /// <summary>
    /// Resumo por documento (CNPJ/periodo/versao) com a contagem de linhas de cada
    /// demonstracao. E o "mapa" da estrutura disponivel para o CNPJ informado.
    /// </summary>
    Task<IReadOnlyList<DfpEstruturaResumo>> GetEstruturaAsync(string cnpj, CancellationToken ct = default);

    /// <summary>
    /// Contas consolidadas (dfp.vw_Conta) do CNPJ, com filtros opcionais por tipo de
    /// demonstracao, data de referencia (yyyyMMdd), ordem do exercicio, conjunto
    /// (CON/IND) e ano de referencia.
    /// </summary>
    Task<IReadOnlyList<DfpConta>> GetContasAsync(
        string cnpj, string? tipo, string? dtRefer, string? ordemExerc,
        string? conjunto, int? ano, CancellationToken ct = default);

    /// <summary>
    /// Historico do ledger dfp.Importacao (mais recente primeiro), com filtros opcionais
    /// por tipo e ano. Limita a <paramref name="limite"/> linhas.
    /// </summary>
    Task<IReadOnlyList<DfpImportacaoHistorico>> GetImportacoesAsync(
        string? tipo, int? ano, int limite, CancellationToken ct = default);

    /// <summary>
    /// Lista as companhias distintas que possuem DRE importada (para o seletor de
    /// companhia da tela de leitura de DRE). Filtro opcional por <paramref name="busca"/>
    /// (razao social ou CNPJ) e limite de resultados.
    /// </summary>
    Task<IReadOnlyList<DfpEmpresaResumo>> GetEmpresasAsync(
        string? busca, int limite, CancellationToken ct = default);
}
