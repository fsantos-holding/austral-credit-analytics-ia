using AustralCreditAnalytics.Api.Models.Dfp;
using AustralCreditAnalytics.Api.Models.Fre;
using Dapper;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Le os modelos FRE (schema [fre]) por CNPJ. Os nomes de tabela/coluna vem sempre do
/// <see cref="FreModeloRegistry"/> (nunca de input livre), portanto a interpolacao em SQL
/// e segura; os valores (cnpj/ano/versao) sao sempre parametrizados. Espelha o padrao de
/// <see cref="ItrRepository"/>/<see cref="DfpRepository"/>.
/// </summary>
public class FreRepository : IFreRepository
{
    private readonly ISqlConnectionFactory _factory;

    public FreRepository(ISqlConnectionFactory factory) => _factory = factory;

    public async Task<IReadOnlyList<FreEmpresaResumo>> GetEmpresasAsync(
        string? busca, int limite, CancellationToken ct = default)
    {
        var top = Math.Clamp(limite, 1, 500);
        var buscaFiltro = string.IsNullOrWhiteSpace(busca) ? null : busca.Trim();

        var sql = $"""
            SELECT TOP ({top})
                   CnpjNum,
                   MIN(CD_CVM)         AS CD_CVM,
                   MAX(DENOM_CIA)      AS DENOM_CIA,
                   MAX(Ano)            AS UltimoAno,
                   COUNT(DISTINCT Ano) AS QtdAnos
            FROM fre.Documento
            WHERE CnpjNum IS NOT NULL AND CnpjNum <> ''
              AND (@busca IS NULL OR DENOM_CIA LIKE '%' + @busca + '%' OR CnpjNum LIKE '%' + @busca + '%')
            GROUP BY CnpjNum
            ORDER BY MAX(DENOM_CIA);
            """;

        await using var connection = _factory.CreateFromSaved();
        var rows = await connection.QueryAsync<FreEmpresaResumo>(new CommandDefinition(
            sql, new { busca = buscaFiltro }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<FreAnoDisponivel>> GetAnosAsync(string cnpj, CancellationToken ct = default)
    {
        var cnpjNum = NormalizarCnpj(cnpj);
        if (cnpjNum.Length == 0)
            return Array.Empty<FreAnoDisponivel>();

        const string sql = """
            SELECT Ano, MAX(VERSAO) AS VersaoMax
            FROM fre.Documento
            WHERE CnpjNum = @cnpjNum AND Ano IS NOT NULL
            GROUP BY Ano
            ORDER BY Ano DESC;
            """;

        await using var connection = _factory.CreateFromSaved();
        var rows = await connection.QueryAsync<FreAnoDisponivel>(new CommandDefinition(
            sql, new { cnpjNum }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<FreModeloDados?> GetModeloAsync(
        string cnpj, string modelo, int? ano, int? versao, CancellationToken ct = default)
    {
        var dem = FreModeloRegistry.Resolver(modelo);
        if (dem is null)
            return null;

        var cnpjNum = NormalizarCnpj(cnpj);
        if (cnpjNum.Length == 0)
            return null;

        await using var connection = _factory.CreateFromSaved();

        var versaoCol = NomeColunaVersao(dem);
        var (linhas, _) = await LerTabelaAsync(connection, dem, cnpjNum, ano, versao, versaoCol, ct);

        var dados = new FreModeloDados
        {
            Modelo = dem.Tipo,
            Tabela = dem.Tabela,
            Descricao = dem.Descricao,
            Colunas = dem.Colunas.Select(c => new FreColuna(c.Name, c.Kind.ToString())).ToList(),
            Linhas = linhas,
        };

        // Master/detail: se existir um modelo filho "<modelo>_classe_acao", traz suas linhas
        // ligadas pelo ID_* compartilhado (ex.: ID_Capital_Social, ID_Acionista).
        var detDem = FreModeloRegistry.Resolver($"{dem.Tipo}_classe_acao");
        if (detDem is not null)
        {
            var detVersaoCol = NomeColunaVersao(detDem);
            var (detLinhas, _) = await LerTabelaAsync(connection, detDem, cnpjNum, ano, versao, detVersaoCol, ct);
            dados.Detalhe = new FreModeloDetalhe
            {
                Modelo = detDem.Tipo,
                Tabela = detDem.Tabela,
                Descricao = detDem.Descricao,
                JoinKey = DescobrirJoinKey(dem, detDem),
                Colunas = detDem.Colunas.Select(c => new FreColuna(c.Name, c.Kind.ToString())).ToList(),
                Linhas = detLinhas,
            };
        }

        return dados;
    }

    public async Task<IReadOnlyList<FreCapitalResumo>> GetCapitalResumoAsync(
        string cnpj, int? ano, CancellationToken ct = default)
    {
        var cnpjNum = NormalizarCnpj(cnpj);
        if (cnpjNum.Length == 0)
            return Array.Empty<FreCapitalResumo>();

        const string sql = """
            SELECT CnpjNum, DENOM_CIA, Ano, Versao, TipoCapital, ValorCapital,
                   AcoesOrdinarias, AcoesPreferenciais, AcoesTotal,
                   AcoesOrdinariasCirculacao, AcoesPreferenciaisCirculacao, AcoesCirculacao,
                   PercentualFreeFloat
            FROM fre.vw_CapitalResumo
            WHERE CnpjNum = @cnpjNum
              AND (@ano IS NULL OR Ano = @ano)
            ORDER BY Ano DESC;
            """;

        await using var connection = _factory.CreateFromSaved();
        var rows = await connection.QueryAsync<FreCapitalResumo>(new CommandDefinition(
            sql, new { cnpjNum, ano = (short?)ano }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<DfpImportacaoHistorico>> GetImportacoesAsync(
        string? tipo, int? ano, int limite, CancellationToken ct = default)
    {
        var tipoFiltro = string.IsNullOrWhiteSpace(tipo) ? null : tipo.Trim();
        var top = Math.Clamp(limite, 1, 1000);

        var sql = $"""
            SELECT TOP ({top})
                   Id, Tipo, Tabela, Conjunto, Ano, Arquivo,
                   LinhasLidas, LinhasImportadas, LinhasRemovidas,
                   Status, Mensagem, Usuario, IniciadoEmUtc, ConcluidoEmUtc
            FROM fre.Importacao
            WHERE (@tipo IS NULL OR Tipo = @tipo)
              AND (@ano  IS NULL OR Ano = @ano)
            ORDER BY IniciadoEmUtc DESC, Id DESC;
            """;

        await using var connection = _factory.CreateFromSaved();
        var rows = await connection.QueryAsync<DfpImportacaoHistorico>(new CommandDefinition(
            sql, new { tipo = tipoFiltro, ano = (short?)ano }, cancellationToken: ct));
        return rows.ToList();
    }

    /// <summary>Le as linhas nativas de um modelo FRE filtrando por CNPJ/ano/versao.</summary>
    private static async Task<(IReadOnlyList<IDictionary<string, object?>> Linhas, int Total)> LerTabelaAsync(
        Microsoft.Data.SqlClient.SqlConnection connection, DfpDemonstracao dem,
        string cnpjNum, int? ano, int? versao, string? versaoCol, CancellationToken ct)
    {
        var colunas = string.Join(", ", dem.Colunas.Select(c => $"[{c.Name}]"));
        var filtroVersao = versao is not null && versaoCol is not null
            ? $"AND [{versaoCol}] = @versao"
            : string.Empty;

        var sql = $"""
            SELECT {colunas}
            FROM {dem.Tabela}
            WHERE CnpjNum = @cnpjNum
              AND (@ano IS NULL OR Ano = @ano)
              {filtroVersao}
            ORDER BY Id;
            """;

        var rows = await connection.QueryAsync(new CommandDefinition(
            sql, new { cnpjNum, ano = (short?)ano, versao = (short?)versao }, cancellationToken: ct));

        var linhas = rows
            .Select(r => (IDictionary<string, object?>)((IDictionary<string, object>)r)
                .ToDictionary(kv => kv.Key, kv => (object?)kv.Value))
            .ToList();

        return (linhas, linhas.Count);
    }

    /// <summary>Nome da coluna de versao do modelo (Versao / VERSAO), ou null se nao houver.</summary>
    private static string? NomeColunaVersao(DfpDemonstracao dem)
        => dem.Colunas.FirstOrDefault(c => string.Equals(c.Name, "Versao", StringComparison.OrdinalIgnoreCase))?.Name;

    /// <summary>
    /// Descobre a coluna ID_* compartilhada entre mestre e detalhe (excluindo ID_Documento),
    /// usada para agrupar o master/detail no frontend.
    /// </summary>
    private static string? DescobrirJoinKey(DfpDemonstracao mestre, DfpDemonstracao detalhe)
    {
        var mestreIds = mestre.Colunas
            .Select(c => c.Name)
            .Where(n => n.StartsWith("ID_", StringComparison.OrdinalIgnoreCase)
                     && !n.Equals("ID_Documento", StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return detalhe.Colunas
            .Select(c => c.Name)
            .FirstOrDefault(n => mestreIds.Contains(n));
    }

    /// <summary>Mantem apenas os digitos do CNPJ (ex.: 00.000.000/0001-00 -> 00000000000100).</summary>
    private static string NormalizarCnpj(string? cnpj)
        => string.IsNullOrWhiteSpace(cnpj)
            ? string.Empty
            : new string(cnpj.Where(char.IsDigit).ToArray());
}
