using System.Text.RegularExpressions;
using AustralCreditAnalytics.Api.Models.Dfp;
using static AustralCreditAnalytics.Api.Models.Dfp.DfpColumnKind;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Catalogo das demonstracoes DFP suportadas: associa a chave de API ao layout
/// nativo da CVM (tabela destino + colunas/tipos). E a fonte unica de verdade
/// usada tanto pela importacao do CSV quanto pela leitura.
/// </summary>
public static class DfpDemonstracaoRegistry
{
    private static DfpColumn C(string name, DfpColumnKind kind) => new(name, kind);

    // Colunas comuns das demonstracoes por conta (identificacao + classificacao + conta).
    private static IEnumerable<DfpColumn> Cabecalho() => new[]
    {
        C("CNPJ_CIA", Text), C("CD_CVM", Text), C("DENOM_CIA", Text),
        C("DT_REFER", Date), C("VERSAO", Short),
        C("GRUPO_DFP", Text), C("MOEDA", Text), C("ESCALA_MOEDA", Text), C("ORDEM_EXERC", Text),
    };

    private static IEnumerable<DfpColumn> Conta() => new[]
    {
        C("CD_CONTA", Text), C("DS_CONTA", Text), C("VL_CONTA", DfpColumnKind.Decimal), C("ST_CONTA_FIXA", Text),
    };

    // Demonstracao de resultado/fluxo: tem DT_INI_EXERC e DT_FIM_EXERC.
    private static IReadOnlyList<DfpColumn> ColsResultado() => Cabecalho()
        .Append(C("DT_INI_EXERC", Date)).Append(C("DT_FIM_EXERC", Date))
        .Concat(Conta()).ToList();

    // Balanco patrimonial: ponto no tempo, so DT_FIM_EXERC.
    private static IReadOnlyList<DfpColumn> ColsBalanco() => Cabecalho()
        .Append(C("DT_FIM_EXERC", Date))
        .Concat(Conta()).ToList();

    // DMPL: resultado + COLUNA_DF.
    private static IReadOnlyList<DfpColumn> ColsDmpl() => Cabecalho()
        .Append(C("DT_INI_EXERC", Date)).Append(C("DT_FIM_EXERC", Date)).Append(C("COLUNA_DF", Text))
        .Concat(Conta()).ToList();

    private static readonly IReadOnlyList<DfpDemonstracao> Todas = new List<DfpDemonstracao>
    {
        new("DOCUMENTO", "dfp.Documento", "Indice de documentos DFP entregues", new[]
        {
            C("CNPJ_CIA", Text), C("CD_CVM", Text), C("DENOM_CIA", Text), C("CATEG_DOC", Text),
            C("DT_REFER", Date), C("DT_RECEB", Date), C("VERSAO", Short), C("ID_DOC", Int), C("LINK_DOC", Text),
        }, TemConjunto: false),
        new("DRE",    "dfp.Dre",    "Demonstracao do Resultado do Exercicio",   ColsResultado(), TemConjunto: true),
        new("BPA",    "dfp.Bpa",    "Balanco Patrimonial Ativo",                ColsBalanco(),   TemConjunto: true),
        new("BPP",    "dfp.Bpp",    "Balanco Patrimonial Passivo",              ColsBalanco(),   TemConjunto: true),
        new("DRA",    "dfp.Dra",    "Demonstracao do Resultado Abrangente",     ColsResultado(), TemConjunto: true),
        new("DVA",    "dfp.Dva",    "Demonstracao do Valor Adicionado",         ColsResultado(), TemConjunto: true),
        new("DFC_MD", "dfp.DfcMd",  "Fluxo de Caixa - Metodo Direto",           ColsResultado(), TemConjunto: true),
        new("DFC_MI", "dfp.DfcMi",  "Fluxo de Caixa - Metodo Indireto",         ColsResultado(), TemConjunto: true),
        new("DMPL",   "dfp.Dmpl",   "Mutacoes do Patrimonio Liquido",           ColsDmpl(),      TemConjunto: true),
        new("COMPOSICAO_CAPITAL", "dfp.ComposicaoCapital", "Composicao do Capital Social", new[]
        {
            C("CNPJ_CIA", Text), C("DENOM_CIA", Text), C("DT_REFER", Date), C("VERSAO", Short),
            C("QT_ACAO_ORDIN_CAP_INTEGR", Long), C("QT_ACAO_PREF_CAP_INTEGR", Long),
            C("QT_ACAO_ORDIN_TESOURO", Long), C("QT_ACAO_PREF_TESOURO", Long),
            C("QT_ACAO_TOTAL_CAP_INTEGR", Long), C("QT_ACAO_TOTAL_TESOURO", Long),
        }, TemConjunto: false),
        new("PARECER", "dfp.Parecer", "Parecer / Declaracao do auditor", new[]
        {
            C("CNPJ_CIA", Text), C("DENOM_CIA", Text), C("DT_REFER", Date), C("VERSAO", Short),
            C("TP_PARECER_DECL", Text), C("TP_RELAT_AUD", Text),
            C("NUM_ITEM_PARECER_DECL", Short), C("TXT_PARECER_DECL", Text),
        }, TemConjunto: false),
    };

    private static readonly IReadOnlyDictionary<string, DfpDemonstracao> PorTipo =
        Todas.ToDictionary(d => d.Tipo, StringComparer.OrdinalIgnoreCase);

    /// <summary>Todas as demonstracoes suportadas (inclui o indice DOCUMENTO).</summary>
    public static IReadOnlyList<DfpDemonstracao> Listar() => Todas;

    /// <summary>Resolve a demonstracao pela chave (case-insensitive). Null se desconhecida.</summary>
    public static DfpDemonstracao? Resolver(string? tipo)
        => tipo is not null && PorTipo.TryGetValue(tipo.Trim(), out var d) ? d : null;

    /// <summary>Conjunto consolidado ('CON').</summary>
    public const string ConjuntoConsolidado = "CON";

    /// <summary>Conjunto individual ('IND').</summary>
    public const string ConjuntoIndividual = "IND";

    /// <summary>Conjunto misto ('MIS'): o arquivo trouxe linhas CON e IND.</summary>
    public const string ConjuntoMisto = "MIS";

    private static readonly Regex AnoRegex = new(@"(19|20)\d{2}", RegexOptions.Compiled);

    /// <summary>
    /// Deriva o conjunto (CON/IND) a partir do conteudo de GRUPO_DFP, que e a fonte
    /// autoritativa: "DF Consolidado*" -> CON; "DF Individual*" -> IND; caso contrario null.
    /// </summary>
    public static string? ResolverConjunto(string? grupoDfp)
    {
        if (string.IsNullOrWhiteSpace(grupoDfp))
            return null;

        var g = grupoDfp.Trim();
        if (g.StartsWith("DF Consolidado", StringComparison.OrdinalIgnoreCase) ||
            g.Contains("Consolidado", StringComparison.OrdinalIgnoreCase))
            return ConjuntoConsolidado;
        if (g.StartsWith("DF Individual", StringComparison.OrdinalIgnoreCase) ||
            g.Contains("Individual", StringComparison.OrdinalIgnoreCase))
            return ConjuntoIndividual;

        return null;
    }

    /// <summary>
    /// Fallback do conjunto pelo nome do arquivo (ex.: "..._con_2023.csv" -> CON,
    /// "..._ind_2023.csv" -> IND). Usado quando GRUPO_DFP nao permite a deteccao.
    /// </summary>
    public static string? ResolverConjuntoPorNome(string? nomeArquivo)
    {
        if (string.IsNullOrWhiteSpace(nomeArquivo))
            return null;

        var n = nomeArquivo.ToLowerInvariant();
        if (n.Contains("_con_") || n.Contains("_con.") || n.Contains("consolidado"))
            return ConjuntoConsolidado;
        if (n.Contains("_ind_") || n.Contains("_ind.") || n.Contains("individual"))
            return ConjuntoIndividual;

        return null;
    }

    /// <summary>
    /// Extrai o ano de referencia do nome do arquivo (primeiro 19xx/20xx encontrado,
    /// ex.: "dfp_cia_aberta_DRE_con_2023.csv" -> 2023). Null se nenhum for encontrado.
    /// </summary>
    public static int? ExtrairAno(string? nomeArquivo)
    {
        if (string.IsNullOrWhiteSpace(nomeArquivo))
            return null;

        var m = AnoRegex.Match(nomeArquivo);
        return m.Success && int.TryParse(m.Value, out var ano) ? ano : null;
    }
}
