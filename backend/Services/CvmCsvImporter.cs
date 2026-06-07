using System.Data;
using System.Globalization;
using System.Text;
using AustralCreditAnalytics.Api.Models.Dfp;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Nucleo reutilizavel de importacao dos CSVs da CVM (open data DFP/ITR) via SqlBulkCopy.
/// O comportamento (separador ';', encoding ISO-8859-1, cabecalho nativo, mapeamento pelo
/// <see cref="DfpDemonstracaoRegistry"/>, deteccao de conjunto/ano, DELETE de escopo,
/// carga em lotes e ledger) e identico para DFP e ITR; o que muda fica no descritor
/// <see cref="CvmDataset"/> (tabela destino, ledger e regra de resolucao do tipo). Cada
/// carga: (1) extrai o ano do nome do arquivo; (2) detecta o conjunto pelo GRUPO_DFP;
/// (3) abre o ledger; (4) remove o escopo (tipo+conjunto+ano); (5) grava em lotes; (6) finaliza.
/// </summary>
public class CvmCsvImporter
{
    private static readonly Encoding Latin1 = Encoding.Latin1; // CVM publica os CSVs em ISO-8859-1.
    private const char Separator = ';';
    private const int BatchSize = 10_000; // Linhas por lote (commit incremental).

    private readonly ISqlConnectionFactory _factory;
    private readonly ICvmImportacaoLog _log;
    private readonly ILogger<CvmCsvImporter> _logger;

    public CvmCsvImporter(
        ISqlConnectionFactory factory, ICvmImportacaoLog log, ILogger<CvmCsvImporter> logger)
    {
        _factory = factory;
        _log = log;
        _logger = logger;
    }

    public async Task<DfpImportResult> ImportarArquivoAsync(
        CvmDataset dataset,
        string tipo,
        string caminhoArquivo,
        string nomeArquivo,
        string? usuario,
        IProgress<(long bytes, int lidas, int importadas, int removidas, string? conjunto, int? ano)>? progresso = null,
        CancellationToken ct = default)
    {
        var destino = dataset.Resolver(tipo)
            ?? throw new InvalidOperationException(
                $"Tipo de demonstracao desconhecido ou nao suportado pela base {dataset.Codigo}: '{tipo}'.");
        var dem = destino.Demonstracao;
        var tabela = destino.Tabela;

        var arquivo = string.IsNullOrWhiteSpace(nomeArquivo) ? Path.GetFileName(caminhoArquivo) : nomeArquivo;

        var ano = DfpDemonstracaoRegistry.ExtrairAno(arquivo)
            ?? throw new InvalidOperationException(
                $"Nao foi possivel identificar o ano de referencia no nome do arquivo '{arquivo}'. " +
                "Inclua o ano (ex.: 'itr_cia_aberta_DRE_con_2023.csv').");

        // Le do FileStream para acompanhar a posicao real (bytes) e calcular o %.
        await using var fileStream = new FileStream(
            caminhoArquivo, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1 << 16, useAsync: true);
        using var reader = new StreamReader(fileStream, Latin1);

        var headerLine = await reader.ReadLineAsync(ct);
        if (string.IsNullOrWhiteSpace(headerLine))
            throw new InvalidOperationException("Arquivo CSV vazio ou sem cabecalho.");

        var header = headerLine.Split(Separator).Select(h => h.Trim()).ToArray();

        // Mapeia cada coluna conhecida do layout para o indice da coluna no CSV.
        var mapeadas = new List<(int Index, DfpColumn Col)>();
        foreach (var col in dem.Colunas)
        {
            var idx = Array.FindIndex(header, h => string.Equals(h, col.Name, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
                mapeadas.Add((idx, col));
        }

        if (mapeadas.Count == 0)
            throw new InvalidOperationException(
                $"Nenhuma coluna do cabecalho casou com o layout de '{dem.Tipo}'. Verifique se o arquivo corresponde a demonstracao selecionada.");

        var grupoIdx = dem.TemConjunto
            ? Array.FindIndex(header, h => string.Equals(h, "GRUPO_DFP", StringComparison.OrdinalIgnoreCase))
            : -1;
        var dtReferIdx = Array.FindIndex(header, h => string.Equals(h, "DT_REFER", StringComparison.OrdinalIgnoreCase));

        // Espia a primeira linha de dados para descobrir o conjunto do escopo (necessario
        // para o DELETE). Mantem a linha em buffer para ser processada normalmente depois.
        string? primeiraLinha = null;
        string? conjuntoEscopo = null;
        if (dem.TemConjunto)
        {
            string? l;
            while ((l = await reader.ReadLineAsync(ct)) is not null)
            {
                if (l.Length == 0)
                    continue;
                primeiraLinha = l;
                if (grupoIdx >= 0)
                {
                    var campos = SplitRow(l, header.Length);
                    var grupo = grupoIdx < campos.Length ? campos[grupoIdx] : null;
                    conjuntoEscopo = DfpDemonstracaoRegistry.ResolverConjunto(grupo);
                }
                break;
            }
            conjuntoEscopo ??= DfpDemonstracaoRegistry.ResolverConjuntoPorNome(arquivo);
        }

        var importacaoId = await _log.IniciarAsync(
            dataset.LedgerTabela, dem.Tipo, tabela, conjuntoEscopo, ano, arquivo, usuario, ct);

        var lidas = 0;
        var importadas = 0;
        var removidas = 0;
        var temCon = conjuntoEscopo == DfpDemonstracaoRegistry.ConjuntoConsolidado;
        var temInd = conjuntoEscopo == DfpDemonstracaoRegistry.ConjuntoIndividual;
        var anosDivergentes = new SortedSet<int>();

        try
        {
            await using var connection = _factory.CreateWritable();
            await connection.OpenAsync(ct);

            // 1. Limpeza do escopo (reimportacao substitui apenas tipo+conjunto+ano).
            removidas = await LimparEscopoAsync(connection, tabela, dem.TemConjunto, conjuntoEscopo, ano, ct);
            progresso?.Report((fileStream.Position, lidas, importadas, removidas, conjuntoEscopo, ano));

            var table = BuildTable(mapeadas, dem.TemConjunto, out var bulkColumns);

            using var bulk = new SqlBulkCopy(connection)
            {
                DestinationTableName = tabela,
                BulkCopyTimeout = 0,
                BatchSize = 5000,
            };
            foreach (var name in bulkColumns)
                bulk.ColumnMappings.Add(name, name);

            // 2. Processa a primeira linha em buffer (se houver) + o restante do arquivo.
            // O buffer (primeiraLinha) so e preenchido quando TemConjunto (espiamos a 1a linha
            // de dados para detectar CON/IND). Sem buffer (ex.: FRE e os tipos DFP sem GRUPO_DFP),
            // a primeira iteracao deve ler direto do reader; caso contrario o laco quebraria
            // imediatamente (line == null) e nenhuma linha seria importada.
            var line = primeiraLinha;
            var primeira = primeiraLinha is not null;
            while (true)
            {
                if (!primeira)
                    line = await reader.ReadLineAsync(ct);
                primeira = false;

                if (line is null)
                    break;
                if (line.Length == 0)
                    continue;

                var fields = SplitRow(line, header.Length);
                lidas++;

                string? conjuntoLinha = null;
                if (dem.TemConjunto)
                {
                    var grupo = grupoIdx >= 0 && grupoIdx < fields.Length ? fields[grupoIdx] : null;
                    conjuntoLinha = DfpDemonstracaoRegistry.ResolverConjunto(grupo) ?? conjuntoEscopo;
                    if (conjuntoLinha == DfpDemonstracaoRegistry.ConjuntoConsolidado) temCon = true;
                    else if (conjuntoLinha == DfpDemonstracaoRegistry.ConjuntoIndividual) temInd = true;
                }

                if (dtReferIdx >= 0 && dtReferIdx < fields.Length)
                {
                    var anoLinha = AnoDeData(fields[dtReferIdx]);
                    if (anoLinha is int al && al != ano)
                        anosDivergentes.Add(al);
                }

                var row = table.NewRow();
                foreach (var (index, col) in mapeadas)
                {
                    var raw = index < fields.Length ? fields[index] : null;
                    row[col.Name] = Convert(raw, col.Kind);
                }
                if (dem.TemConjunto)
                    row["Conjunto"] = (object?)conjuntoLinha ?? DBNull.Value;
                row["Ano"] = ano;
                row["ImportacaoId"] = (object?)importacaoId ?? DBNull.Value;
                row["ArquivoOrigem"] = arquivo;
                table.Rows.Add(row);

                if (table.Rows.Count >= BatchSize)
                {
                    ct.ThrowIfCancellationRequested();
                    await bulk.WriteToServerAsync(table, ct);
                    importadas += table.Rows.Count;
                    table.Clear();
                    var conjAtual = ConjuntoAcumulado(dem.TemConjunto, temCon, temInd, conjuntoEscopo);
                    progresso?.Report((fileStream.Position, lidas, importadas, removidas, conjAtual, ano));
                    if (importacaoId is long lid)
                        await _log.AtualizarAsync(dataset.LedgerTabela, lid, removidas, lidas, importadas, ct);
                }
            }

            // Lote final (linhas restantes apos o ultimo BatchSize).
            if (table.Rows.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                await bulk.WriteToServerAsync(table, ct);
                importadas += table.Rows.Count;
                table.Clear();
            }

            var conjuntoFinal = ConjuntoAcumulado(dem.TemConjunto, temCon, temInd, conjuntoEscopo);
            progresso?.Report((fileStream.Length, lidas, importadas, removidas, conjuntoFinal, ano));

            var mensagem = MontarMensagem(tabela, conjuntoFinal, ano, importadas, removidas, anosDivergentes);

            if (importacaoId is long id)
                await _log.FinalizarAsync(dataset.LedgerTabela, id, "Concluido", mensagem, removidas, lidas, importadas, conjuntoFinal, ct);

            _logger.LogInformation(
                "Importacao {Base} {Tipo} ({Conjunto}/{Ano}): {Lidas} lidas, {Importadas} gravadas, {Removidas} removidas em {Tabela} ({Arquivo}).",
                dataset.Codigo, dem.Tipo, conjuntoFinal ?? "-", ano, lidas, importadas, removidas, tabela, arquivo);

            return new DfpImportResult
            {
                Tipo = dem.Tipo,
                Modelo = dem.Descricao,
                Tabela = tabela,
                Arquivo = arquivo,
                Conjunto = conjuntoFinal,
                Ano = ano,
                LinhasLidas = lidas,
                LinhasImportadas = importadas,
                LinhasRemovidas = removidas,
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (importacaoId is long id)
            {
                var conjuntoFinal = ConjuntoAcumulado(dem.TemConjunto, temCon, temInd, conjuntoEscopo);
                await _log.FinalizarAsync(
                    dataset.LedgerTabela, id, "Cancelado",
                    $"Importacao cancelada. {importadas} linha(s) ja gravada(s) foram mantidas.",
                    removidas, lidas, importadas, conjuntoFinal, CancellationToken.None);
            }
            throw;
        }
        catch (Exception ex)
        {
            if (importacaoId is long id)
            {
                var conjuntoFinal = ConjuntoAcumulado(dem.TemConjunto, temCon, temInd, conjuntoEscopo);
                await _log.FinalizarAsync(
                    dataset.LedgerTabela, id, "Falha", ex.Message, removidas, lidas, importadas, conjuntoFinal, CancellationToken.None);
            }
            throw;
        }
    }

    /// <summary>
    /// Remove o escopo da reimportacao: por (Ano, Conjunto) nas demonstracoes por conta
    /// (quando o conjunto e conhecido) ou por (Ano) nas demais. Retorna as linhas afetadas.
    /// </summary>
    private static async Task<int> LimparEscopoAsync(
        SqlConnection connection, string tabela, bool temConjunto, string? conjunto, int ano, CancellationToken ct)
    {
        var sql = temConjunto && conjunto is not null
            ? $"DELETE FROM {tabela} WHERE Ano = @ano AND Conjunto = @conjunto"
            : $"DELETE FROM {tabela} WHERE Ano = @ano";

        return await connection.ExecuteAsync(new CommandDefinition(
            sql, new { ano = (short)ano, conjunto }, commandTimeout: 0, cancellationToken: ct));
    }

    private static string? ConjuntoAcumulado(bool temConjunto, bool temCon, bool temInd, string? fallback)
    {
        if (!temConjunto)
            return null;
        if (temCon && temInd)
            return DfpDemonstracaoRegistry.ConjuntoMisto;
        if (temCon)
            return DfpDemonstracaoRegistry.ConjuntoConsolidado;
        if (temInd)
            return DfpDemonstracaoRegistry.ConjuntoIndividual;
        return fallback;
    }

    private static string MontarMensagem(
        string tabela, string? conjunto, int ano, int importadas, int removidas, SortedSet<int> anosDivergentes)
    {
        var rotuloConjunto = conjunto switch
        {
            "CON" => "Consolidado",
            "IND" => "Individual",
            "MIS" => "Misto",
            _ => null,
        };
        var escopo = rotuloConjunto is null ? $"{ano}" : $"{rotuloConjunto}, {ano}";
        var msg = $"{importadas} linha(s) importada(s) em {tabela} ({escopo}); {removidas} removida(s) antes da carga.";
        if (anosDivergentes.Count > 0)
            msg += $" Aviso: o arquivo contem linhas com ano(s) {string.Join(", ", anosDivergentes)} diferentes do ano do nome ({ano}).";
        return msg;
    }

    private static DataTable BuildTable(
        IReadOnlyList<(int Index, DfpColumn Col)> mapeadas, bool temConjunto, out IReadOnlyList<string> bulkColumns)
    {
        var table = new DataTable();
        var cols = new List<string>();

        foreach (var (_, col) in mapeadas)
        {
            table.Columns.Add(new DataColumn(col.Name, ClrType(col.Kind)) { AllowDBNull = true });
            cols.Add(col.Name);
        }

        if (temConjunto)
        {
            table.Columns.Add(new DataColumn("Conjunto", typeof(string)) { AllowDBNull = true });
            cols.Add("Conjunto");
        }

        table.Columns.Add(new DataColumn("Ano", typeof(short)) { AllowDBNull = true });
        cols.Add("Ano");

        table.Columns.Add(new DataColumn("ImportacaoId", typeof(long)) { AllowDBNull = true });
        cols.Add("ImportacaoId");

        table.Columns.Add(new DataColumn("ArquivoOrigem", typeof(string)) { AllowDBNull = true });
        cols.Add("ArquivoOrigem");

        bulkColumns = cols;
        return table;
    }

    private static Type ClrType(DfpColumnKind kind) => kind switch
    {
        DfpColumnKind.Date => typeof(DateTime),
        DfpColumnKind.Short => typeof(short),
        DfpColumnKind.Int => typeof(int),
        DfpColumnKind.Long => typeof(long),
        DfpColumnKind.Decimal => typeof(decimal),
        _ => typeof(string),
    };

    private static object Convert(string? raw, DfpColumnKind kind)
    {
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value))
            return DBNull.Value;

        switch (kind)
        {
            case DfpColumnKind.Date:
                return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                    ? d
                    : DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2) ? d2 : DBNull.Value;

            case DfpColumnKind.Short:
                return short.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) ? s : DBNull.Value;

            case DfpColumnKind.Int:
                return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : DBNull.Value;

            case DfpColumnKind.Long:
                return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : DBNull.Value;

            case DfpColumnKind.Decimal:
                return ParseDecimal(value);

            default:
                return value;
        }
    }

    /// <summary>Extrai o ano de um DT_REFER (yyyy-MM-dd ou parseavel). Null se vazio/invalido.</summary>
    private static int? AnoDeData(string? raw)
    {
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value))
            return null;
        if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d.Year;
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2) ? d2.Year : null;
    }

    // CVM usa ponto como separador decimal no VL_CONTA; aceitamos virgula como tolerancia.
    private static object ParseDecimal(string value)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var dec))
            return dec;

        var normalized = value.Replace(".", string.Empty).Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var dec2)
            ? dec2
            : DBNull.Value;
    }

    /// <summary>
    /// Divide a linha pelo separador. Se houver mais campos que o cabecalho (caso de
    /// texto livre com ';', ex.: TXT_PARECER_DECL), o excedente e reunido no ultimo campo.
    /// </summary>
    private static string[] SplitRow(string line, int expected)
    {
        var parts = line.Split(Separator);
        if (parts.Length <= expected)
            return parts;

        var trimmed = new string[expected];
        Array.Copy(parts, trimmed, expected - 1);
        trimmed[expected - 1] = string.Join(Separator, parts.Skip(expected - 1));
        return trimmed;
    }
}
