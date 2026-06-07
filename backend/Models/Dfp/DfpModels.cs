using System.Text.Json.Serialization;

namespace AustralCreditAnalytics.Api.Models.Dfp;

/// <summary>Tipo CLR de uma coluna do layout CVM, usado na conversao do CSV.</summary>
public enum DfpColumnKind
{
    Text,
    Date,
    Short,
    Int,
    Long,
    Decimal,
}

/// <summary>Coluna nativa do layout CVM (nome igual ao cabecalho do CSV).</summary>
public sealed record DfpColumn(string Name, DfpColumnKind Kind);

/// <summary>
/// Metadados de uma demonstracao DFP: chave de API (<see cref="Tipo"/>), tabela
/// destino (schema-qualificada) e as colunas nativas do CSV da CVM.
/// <paramref name="TemConjunto"/> indica se a demonstracao tem GRUPO_DFP e, portanto,
/// distingue Consolidado/Individual (coluna Conjunto). Documento, ComposicaoCapital
/// e Parecer nao tem GRUPO_DFP e ficam sem conjunto.
/// </summary>
public sealed record DfpDemonstracao(
    string Tipo,
    string Tabela,
    string Descricao,
    IReadOnlyList<DfpColumn> Colunas,
    bool TemConjunto);

/// <summary>Resultado de uma importacao de CSV para uma demonstracao DFP.</summary>
public sealed class DfpImportResult
{
    [JsonPropertyName("tipo")]
    public string Tipo { get; set; } = string.Empty;

    /// <summary>Nome amigavel da demonstracao (descricao do registry).</summary>
    [JsonPropertyName("modelo")]
    public string Modelo { get; set; } = string.Empty;

    [JsonPropertyName("tabela")]
    public string Tabela { get; set; } = string.Empty;

    [JsonPropertyName("arquivo")]
    public string Arquivo { get; set; } = string.Empty;

    /// <summary>Conjunto detectado: CON/IND/MIS ou null (demonstracao sem GRUPO_DFP).</summary>
    [JsonPropertyName("conjunto")]
    public string? Conjunto { get; set; }

    /// <summary>Ano de referencia extraido do nome do arquivo.</summary>
    [JsonPropertyName("ano")]
    public int? Ano { get; set; }

    [JsonPropertyName("linhasLidas")]
    public int LinhasLidas { get; set; }

    [JsonPropertyName("linhasImportadas")]
    public int LinhasImportadas { get; set; }

    /// <summary>Linhas removidas do escopo (tipo+conjunto+ano) antes da carga.</summary>
    [JsonPropertyName("linhasRemovidas")]
    public int LinhasRemovidas { get; set; }
}

/// <summary>Estado de um job de importacao de CSV DFP em segundo plano.</summary>
public enum DfpImportStatus
{
    Pendente,
    Processando,
    Concluido,
    Cancelado,
    Falha,
}

/// <summary>
/// Job de importacao de um CSV DFP processado em segundo plano. Mantido em memoria
/// pelo gerenciador de jobs; o acesso aos campos mutaveis e protegido por <see cref="_sync"/>.
/// Os membros marcados com <see cref="JsonIgnoreAttribute"/> nao sao serializados.
/// </summary>
public sealed class DfpImportJob
{
    private readonly object _sync = new();

    private DfpImportStatus _status = DfpImportStatus.Pendente;
    private long _bytesProcessados;
    private int _linhasLidas;
    private int _linhasImportadas;
    private int _linhasRemovidas;
    private string? _conjunto;
    private int? _ano;
    private string? _mensagem;
    private DateTime _atualizadoEm = DateTime.UtcNow;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string Tipo { get; init; } = string.Empty;

    /// <summary>Nome amigavel da demonstracao (descricao do registry).</summary>
    public string Modelo { get; init; } = string.Empty;
    public string Tabela { get; init; } = string.Empty;
    public string Arquivo { get; init; } = string.Empty;
    public long BytesTotais { get; init; }
    public DateTime CriadoEm { get; } = DateTime.UtcNow;

    /// <summary>Identidade que iniciou a importacao (do User.Identity).</summary>
    [JsonIgnore]
    public string? Usuario { get; init; }

    /// <summary>Caminho do arquivo temporario salvo em disco (apagado ao finalizar).</summary>
    [JsonIgnore]
    public string CaminhoTemp { get; init; } = string.Empty;

    /// <summary>Fonte de cancelamento cooperativo do processamento em segundo plano.</summary>
    [JsonIgnore]
    public CancellationTokenSource Cancelamento { get; } = new();

    public DfpImportStatus Status { get { lock (_sync) return _status; } }
    public long BytesProcessados { get { lock (_sync) return _bytesProcessados; } }
    public int LinhasLidas { get { lock (_sync) return _linhasLidas; } }
    public int LinhasImportadas { get { lock (_sync) return _linhasImportadas; } }
    public int LinhasRemovidas { get { lock (_sync) return _linhasRemovidas; } }
    public string? Conjunto { get { lock (_sync) return _conjunto; } }
    public int? Ano { get { lock (_sync) return _ano; } }
    public string? Mensagem { get { lock (_sync) return _mensagem; } }
    public DateTime AtualizadoEm { get { lock (_sync) return _atualizadoEm; } }

    /// <summary>True quando o job ja atingiu um estado terminal.</summary>
    [JsonIgnore]
    public bool Finalizado => Status is DfpImportStatus.Concluido or DfpImportStatus.Cancelado or DfpImportStatus.Falha;

    public void MarcarProcessando()
    {
        lock (_sync)
        {
            _status = DfpImportStatus.Processando;
            _atualizadoEm = DateTime.UtcNow;
        }
    }

    public void AtualizarProgresso(
        long bytesProcessados, int linhasLidas, int linhasImportadas,
        int linhasRemovidas, string? conjunto, int? ano)
    {
        lock (_sync)
        {
            _bytesProcessados = bytesProcessados;
            _linhasLidas = linhasLidas;
            _linhasImportadas = linhasImportadas;
            _linhasRemovidas = linhasRemovidas;
            if (conjunto is not null)
                _conjunto = conjunto;
            if (ano is not null)
                _ano = ano;
            _atualizadoEm = DateTime.UtcNow;
        }
    }

    public void Finalizar(DfpImportStatus status, string? mensagem)
    {
        lock (_sync)
        {
            _status = status;
            _mensagem = mensagem;
            _atualizadoEm = DateTime.UtcNow;
        }
    }

    public DfpImportJobStatus Snapshot()
    {
        lock (_sync)
        {
            var percentual = BytesTotais > 0
                ? Math.Clamp((double)_bytesProcessados / BytesTotais * 100d, 0d, 100d)
                : (_status == DfpImportStatus.Concluido ? 100d : 0d);

            return new DfpImportJobStatus
            {
                JobId = Id,
                Tipo = Tipo,
                Modelo = Modelo,
                Tabela = Tabela,
                Arquivo = Arquivo,
                Conjunto = _conjunto,
                Ano = _ano,
                Status = _status.ToString(),
                Percentual = Math.Round(percentual, 1),
                LinhasLidas = _linhasLidas,
                LinhasImportadas = _linhasImportadas,
                LinhasRemovidas = _linhasRemovidas,
                BytesProcessados = _bytesProcessados,
                BytesTotais = BytesTotais,
                Mensagem = _mensagem,
            };
        }
    }
}

/// <summary>Resposta de polling do status de um <see cref="DfpImportJob"/>.</summary>
public sealed class DfpImportJobStatus
{
    [JsonPropertyName("jobId")]
    public string JobId { get; set; } = string.Empty;

    [JsonPropertyName("tipo")]
    public string Tipo { get; set; } = string.Empty;

    [JsonPropertyName("modelo")]
    public string Modelo { get; set; } = string.Empty;

    [JsonPropertyName("tabela")]
    public string Tabela { get; set; } = string.Empty;

    [JsonPropertyName("arquivo")]
    public string Arquivo { get; set; } = string.Empty;

    [JsonPropertyName("conjunto")]
    public string? Conjunto { get; set; }

    [JsonPropertyName("ano")]
    public int? Ano { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("percentual")]
    public double Percentual { get; set; }

    [JsonPropertyName("linhasLidas")]
    public int LinhasLidas { get; set; }

    [JsonPropertyName("linhasImportadas")]
    public int LinhasImportadas { get; set; }

    [JsonPropertyName("linhasRemovidas")]
    public int LinhasRemovidas { get; set; }

    [JsonPropertyName("bytesProcessados")]
    public long BytesProcessados { get; set; }

    [JsonPropertyName("bytesTotais")]
    public long BytesTotais { get; set; }

    [JsonPropertyName("mensagem")]
    public string? Mensagem { get; set; }
}

/// <summary>
/// Resumo da estrutura disponivel de um documento (mapeia dfp.vw_Estrutura):
/// por CNPJ/periodo/versao, quantas linhas existem em cada demonstracao.
/// </summary>
public sealed class DfpEstruturaResumo
{
    public string CNPJ_CIA { get; set; } = string.Empty;
    public string? CD_CVM { get; set; }
    public string? DENOM_CIA { get; set; }
    public DateTime DT_REFER { get; set; }
    public short VERSAO { get; set; }
    public string? CATEG_DOC { get; set; }
    public DateTime? DT_RECEB { get; set; }
    public string? LINK_DOC { get; set; }
    public int QtBpaCon { get; set; }
    public int QtBpaInd { get; set; }
    public int QtBppCon { get; set; }
    public int QtBppInd { get; set; }
    public int QtDreCon { get; set; }
    public int QtDreInd { get; set; }
    public int QtDraCon { get; set; }
    public int QtDraInd { get; set; }
    public int QtDvaCon { get; set; }
    public int QtDvaInd { get; set; }
    public int QtDfcMdCon { get; set; }
    public int QtDfcMdInd { get; set; }
    public int QtDfcMiCon { get; set; }
    public int QtDfcMiInd { get; set; }
    public int QtDmplCon { get; set; }
    public int QtDmplInd { get; set; }
    public int QtComposicaoCapital { get; set; }
    public int QtParecer { get; set; }
}

/// <summary>Linha de conta consolidada de qualquer demonstracao (mapeia dfp.vw_Conta).</summary>
public sealed class DfpConta
{
    public string TIPO_DEM { get; set; } = string.Empty;
    public string CNPJ_CIA { get; set; } = string.Empty;
    public string? CD_CVM { get; set; }
    public string? DENOM_CIA { get; set; }
    public DateTime DT_REFER { get; set; }
    public short VERSAO { get; set; }
    public string? GRUPO_DFP { get; set; }
    public string? Conjunto { get; set; }
    public int? Ano { get; set; }
    public string? MOEDA { get; set; }
    public string? ESCALA_MOEDA { get; set; }
    public string? ORDEM_EXERC { get; set; }
    public DateTime? DT_INI_EXERC { get; set; }
    public DateTime? DT_FIM_EXERC { get; set; }
    public string? COLUNA_DF { get; set; }
    public string CD_CONTA { get; set; } = string.Empty;
    public string? DS_CONTA { get; set; }
    public decimal? VL_CONTA { get; set; }
    public string? ST_CONTA_FIXA { get; set; }
}

/// <summary>Linha do ledger de importacoes DFP (mapeia dfp.Importacao).</summary>
public sealed class DfpImportacaoHistorico
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("tipo")]
    public string Tipo { get; set; } = string.Empty;

    [JsonPropertyName("tabela")]
    public string Tabela { get; set; } = string.Empty;

    [JsonPropertyName("conjunto")]
    public string? Conjunto { get; set; }

    [JsonPropertyName("ano")]
    public short? Ano { get; set; }

    [JsonPropertyName("arquivo")]
    public string? Arquivo { get; set; }

    [JsonPropertyName("linhasLidas")]
    public int LinhasLidas { get; set; }

    [JsonPropertyName("linhasImportadas")]
    public int LinhasImportadas { get; set; }

    [JsonPropertyName("linhasRemovidas")]
    public int LinhasRemovidas { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("mensagem")]
    public string? Mensagem { get; set; }

    [JsonPropertyName("usuario")]
    public string? Usuario { get; set; }

    [JsonPropertyName("iniciadoEmUtc")]
    public DateTime IniciadoEmUtc { get; set; }

    [JsonPropertyName("concluidoEmUtc")]
    public DateTime? ConcluidoEmUtc { get; set; }
}
