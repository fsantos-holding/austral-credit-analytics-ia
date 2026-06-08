using System.Globalization;
using AustralCreditAnalytics.Api.Models.Fre;
using Dapper;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Cruza as posicoes acionarias (fre.PosicaoAcionaria) entre todas as companhias
/// importadas para identificar acionistas presentes em mais de uma empresa. Le os dados
/// FRE ja importados (nenhuma nova importacao). Os nomes de tabela/coluna sao literais de
/// codigo; os valores (ano/tipo/busca/chave) sao sempre parametrizados, portanto o SQL e
/// seguro. Espelha o padrao de <see cref="FreDossieRepository"/> (mesmo
/// <see cref="ISqlConnectionFactory"/> e helpers de conversao).
/// </summary>
public class AcionistaCruzamentoRepository : IAcionistaCruzamentoRepository
{
    private readonly ISqlConnectionFactory _factory;

    public AcionistaCruzamentoRepository(ISqlConnectionFactory factory) => _factory = factory;

    /// <summary>
    /// CTEs base do cruzamento, reutilizadas por todas as consultas:
    /// <list type="bullet">
    ///   <item><c>alvo</c>: ultima (Ano, Versao) de cada empresa (filtravel por @ano).</item>
    ///   <item><c>snap</c>: todas as linhas de posicao da empresa nesse snapshot.</item>
    ///   <item><c>posicao</c>: 1 linha por (acionista, empresa) com a chave de cruzamento.</item>
    /// </list>
    /// A consulta final e anexada apos este prefixo.
    /// </summary>
    private const string CteBase = """
        WITH alvo AS (
            SELECT CnpjNum, Ano, Versao,
                   ROW_NUMBER() OVER (PARTITION BY CnpjNum ORDER BY Ano DESC, Versao DESC) AS rn
            FROM (
                SELECT DISTINCT CnpjNum, Ano, Versao
                FROM fre.PosicaoAcionaria
                WHERE CnpjNum IS NOT NULL AND CnpjNum <> ''
                  AND (@ano IS NULL OR Ano = @ano)
            ) d
        ),
        snap AS (
            SELECT pa.CnpjNum, pa.Ano, pa.Versao, pa.Nome_Companhia, pa.Acionista,
                   pa.DocAcionistaNum, pa.Tipo_Pessoa_Acionista, pa.Acionista_Controlador,
                   pa.Percentual_Acao_Ordinaria_Circulacao, pa.Percentual_Acao_Preferencial_Circulacao,
                   pa.Percentual_Total_Acoes_Circulacao
            FROM fre.PosicaoAcionaria pa
            INNER JOIN alvo a
                ON a.CnpjNum = pa.CnpjNum AND a.Ano = pa.Ano AND a.Versao = pa.Versao AND a.rn = 1
        ),
        posicao AS (
            SELECT
                COALESCE(NULLIF(s.DocAcionistaNum, ''), NULLIF(UPPER(LTRIM(RTRIM(s.Acionista))), '')) AS Chave,
                s.CnpjNum,
                MAX(CASE WHEN NULLIF(s.DocAcionistaNum, '') IS NULL THEN 1 ELSE 0 END) AS ChavePorNome,
                MAX(s.Nome_Companhia)                                AS NomeCompanhia,
                MAX(CAST(s.Ano AS INT))                              AS Ano,
                MAX(CAST(s.Versao AS INT))                           AS Versao,
                MAX(s.Acionista)                                     AS Nome,
                MAX(s.Tipo_Pessoa_Acionista)                         AS TipoPessoa,
                MAX(NULLIF(s.DocAcionistaNum, ''))                   AS Documento,
                MAX(CASE WHEN UPPER(LTRIM(RTRIM(s.Acionista_Controlador))) IN ('S', '1', 'SIM', 'TRUE') THEN 1 ELSE 0 END) AS Controlador,
                SUM(ISNULL(s.Percentual_Acao_Ordinaria_Circulacao, 0))    AS PercON,
                SUM(ISNULL(s.Percentual_Acao_Preferencial_Circulacao, 0)) AS PercPN,
                SUM(ISNULL(s.Percentual_Total_Acoes_Circulacao, 0))       AS PercTotal
            FROM snap s
            GROUP BY
                COALESCE(NULLIF(s.DocAcionistaNum, ''), NULLIF(UPPER(LTRIM(RTRIM(s.Acionista))), '')),
                s.CnpjNum
            HAVING COALESCE(NULLIF(s.DocAcionistaNum, ''), NULLIF(UPPER(LTRIM(RTRIM(s.Acionista))), '')) IS NOT NULL
        )
        """;

    public async Task<IReadOnlyList<AcionistaRankingItem>> GetRankingAsync(
        int? ano, string? tipoPessoa, int minEmpresas, string? busca, int limite,
        CancellationToken ct = default)
    {
        var top = Math.Clamp(limite, 1, 500);
        var min = Math.Max(minEmpresas, 2);
        var tipo = string.IsNullOrWhiteSpace(tipoPessoa) ? null : tipoPessoa.Trim();
        var filtroBusca = string.IsNullOrWhiteSpace(busca) ? null : busca.Trim();

        var sql = $"""
            {CteBase}
            SELECT TOP ({top})
                   Chave,
                   MAX(Nome)            AS Nome,
                   MAX(TipoPessoa)      AS TipoPessoa,
                   MAX(Documento)       AS Documento,
                   MAX(ChavePorNome)    AS ChavePorNome,
                   MAX(Controlador)     AS Controlador,
                   COUNT(DISTINCT CnpjNum) AS QtdEmpresas,
                   AVG(PercTotal)       AS PercentualMedio,
                   MAX(PercTotal)       AS PercentualMaximo
            FROM posicao
            WHERE (@tipo IS NULL OR TipoPessoa = @tipo)
            GROUP BY Chave
            HAVING COUNT(DISTINCT CnpjNum) >= @min
               AND (@busca IS NULL OR MAX(Nome) LIKE '%' + @busca + '%' OR MAX(Documento) LIKE '%' + @busca + '%')
            ORDER BY COUNT(DISTINCT CnpjNum) DESC, MAX(PercTotal) DESC, MAX(Nome);
            """;

        var p = new { ano = (short?)ano, tipo, min, busca = filtroBusca };

        await using var cn = _factory.CreateFromSaved();
        var rows = (await cn.QueryAsync(new CommandDefinition(sql, p, cancellationToken: ct))).ToList();

        var itens = rows.Select(MapRanking).ToList();
        if (itens.Count == 0)
            return itens;

        // Drilldown leve: anexa as empresas de cada acionista do ranking em uma unica consulta.
        var chaves = itens.Select(i => i.Chave).ToArray();
        var posicoes = await QueryPosicoesAsync(cn, ano, chaves, null, ct);
        var porChave = posicoes.GroupBy(x => x.Chave)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Posicao).ToList(), StringComparer.Ordinal);

        foreach (var item in itens)
            if (porChave.TryGetValue(item.Chave, out var empresas))
                item.Empresas = empresas;

        return itens;
    }

    public async Task<AcionistaDetalhe?> GetPosicoesAcionistaAsync(
        string chave, int? ano, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(chave))
            return null;

        await using var cn = _factory.CreateFromSaved();
        var posicoes = await QueryPosicoesAsync(cn, ano, new[] { chave }, null, ct);
        if (posicoes.Count == 0)
            return null;

        var empresas = posicoes.Select(x => x.Posicao).ToList();
        var first = posicoes[0];
        return new AcionistaDetalhe
        {
            Chave = chave,
            Nome = first.Nome,
            TipoPessoa = first.TipoPessoa,
            Documento = first.Documento,
            ChavePorNome = first.ChavePorNome,
            QtdEmpresas = empresas.Select(e => e.CnpjNum).Distinct(StringComparer.Ordinal).Count(),
            Empresas = empresas,
        };
    }

    public async Task<AcionistaInsights> GetInsightsAsync(int? ano, CancellationToken ct = default)
    {
        var insights = new AcionistaInsights { Ano = ano };

        await using var cn = _factory.CreateFromSaved();
        var p = new { ano = (short?)ano };

        // Resumo dos acionistas cruzados (2+ empresas): contagem, maior conector e split PF/PJ/outros.
        var resumo = (await cn.QueryAsync(new CommandDefinition($"""
            {CteBase},
            cruz AS (
                SELECT Chave, MAX(TipoPessoa) AS TipoPessoa, COUNT(DISTINCT CnpjNum) AS QtdEmpresas
                FROM posicao
                GROUP BY Chave
                HAVING COUNT(DISTINCT CnpjNum) >= 2
            )
            SELECT
                COUNT(*)                                                              AS Cruzados,
                ISNULL(MAX(QtdEmpresas), 0)                                           AS MaiorConector,
                SUM(CASE WHEN TipoPessoa = 'PF' THEN 1 ELSE 0 END)                    AS PF,
                SUM(CASE WHEN TipoPessoa = 'PJ' THEN 1 ELSE 0 END)                    AS PJ,
                SUM(CASE WHEN ISNULL(TipoPessoa, '') NOT IN ('PF', 'PJ') THEN 1 ELSE 0 END) AS Outros
            FROM cruz;
            """, p, cancellationToken: ct))).FirstOrDefault();

        var empresasConectadas = await cn.ExecuteScalarAsync<int?>(new CommandDefinition($"""
            {CteBase},
            cruz AS (
                SELECT Chave FROM posicao GROUP BY Chave HAVING COUNT(DISTINCT CnpjNum) >= 2
            )
            SELECT COUNT(DISTINCT p.CnpjNum)
            FROM posicao p
            INNER JOIN cruz c ON c.Chave = p.Chave;
            """, p, cancellationToken: ct));

        var d = resumo is null ? null : new Dictionary<string, object>((IDictionary<string, object>)resumo);
        var cruzados = ToDecimal(d?.GetValueOrDefault("Cruzados"));
        var maiorConector = ToDecimal(d?.GetValueOrDefault("MaiorConector"));
        var pf = ToDecimal(d?.GetValueOrDefault("PF"));
        var pj = ToDecimal(d?.GetValueOrDefault("PJ"));
        var outros = ToDecimal(d?.GetValueOrDefault("Outros"));

        insights.Kpis.Add(new FreKpi { Rotulo = "Acionistas Cruzados", Valor = cruzados, Formato = "inteiro", Icone = "affiliate" });
        insights.Kpis.Add(new FreKpi { Rotulo = "Empresas Conectadas", Valor = empresasConectadas, Formato = "inteiro", Icone = "building" });
        insights.Kpis.Add(new FreKpi { Rotulo = "Maior Conector (empresas)", Valor = maiorConector, Formato = "inteiro", Icone = "share" });
        insights.Kpis.Add(new FreKpi { Rotulo = "Pessoas Fisicas", Valor = pf, Formato = "inteiro", Icone = "user" });
        insights.Kpis.Add(new FreKpi { Rotulo = "Pessoas Juridicas", Valor = pj, Formato = "inteiro", Icone = "building-bank" });

        // Donut: distribuicao PF / PJ / Outros entre os acionistas cruzados.
        if ((pf.GetValueOrDefault() + pj.GetValueOrDefault() + outros.GetValueOrDefault()) > 0)
        {
            var dist = new FreSerie { Chave = "acionistas_tipo", Titulo = "Acionistas Cruzados por Tipo de Pessoa", Tipo = "donut" };
            dist.Categorias.Add("Pessoa Fisica");
            dist.Categorias.Add("Pessoa Juridica");
            dist.Categorias.Add("Outros");
            dist.Valores.Add(new FreSerieValor { Rotulo = "Acionistas", Dados = new List<decimal?> { pf, pj, outros } });
            insights.Series.Add(dist);
        }

        // Barras: Top 15 acionistas por numero de empresas conectadas.
        var top = await cn.QueryAsync(new CommandDefinition($"""
            {CteBase}
            SELECT TOP (15)
                   MAX(Nome)               AS Cat,
                   COUNT(DISTINCT CnpjNum)  AS V
            FROM posicao
            GROUP BY Chave
            HAVING COUNT(DISTINCT CnpjNum) >= 2
            ORDER BY COUNT(DISTINCT CnpjNum) DESC, MAX(Nome);
            """, p, cancellationToken: ct));

        var serieTop = SerieRows("top_conectores", "Top 15 Acionistas por Numero de Empresas", "barras_horizontais",
            top, "Cat", new[] { ("Empresas", "V") });
        if (serieTop.Categorias.Count > 0)
            insights.Series.Add(serieTop);

        // Pares de empresas com acionistas em comum (arestas da rede de conexoes).
        var pares = await cn.QueryAsync(new CommandDefinition($"""
            {CteBase}
            SELECT TOP (25)
                   p1.CnpjNum            AS CnpjNumA,
                   MAX(p1.NomeCompanhia) AS EmpresaA,
                   p2.CnpjNum            AS CnpjNumB,
                   MAX(p2.NomeCompanhia) AS EmpresaB,
                   COUNT(DISTINCT p1.Chave) AS QtdSociosComum
            FROM posicao p1
            INNER JOIN posicao p2 ON p2.Chave = p1.Chave AND p2.CnpjNum > p1.CnpjNum
            GROUP BY p1.CnpjNum, p2.CnpjNum
            ORDER BY COUNT(DISTINCT p1.Chave) DESC;
            """, p, cancellationToken: ct));

        foreach (var row in pares)
        {
            var pr = new Dictionary<string, object>((IDictionary<string, object>)row);
            insights.Pares.Add(new AcionistaParEmpresas
            {
                CnpjNumA = pr.GetValueOrDefault("CnpjNumA") as string ?? string.Empty,
                EmpresaA = pr.GetValueOrDefault("EmpresaA") as string,
                CnpjNumB = pr.GetValueOrDefault("CnpjNumB") as string ?? string.Empty,
                EmpresaB = pr.GetValueOrDefault("EmpresaB") as string,
                QtdSociosComum = ToInt(pr.GetValueOrDefault("QtdSociosComum")) ?? 0,
            });
        }

        return insights;
    }

    /// <summary>
    /// Le as posicoes (1 por empresa) de um conjunto de chaves de acionista, ordenadas por
    /// participacao total. Retorna tambem os campos de identificacao do acionista para reuso.
    /// </summary>
    private async Task<IReadOnlyList<(string Chave, string? Nome, string? TipoPessoa, string? Documento, bool ChavePorNome, AcionistaEmpresaPosicao Posicao)>>
        QueryPosicoesAsync(
            Microsoft.Data.SqlClient.SqlConnection cn, int? ano, string[] chaves, int? _, CancellationToken ct)
    {
        if (chaves.Length == 0)
            return Array.Empty<(string, string?, string?, string?, bool, AcionistaEmpresaPosicao)>();

        var sql = $"""
            {CteBase}
            SELECT Chave, Nome, TipoPessoa, Documento, ChavePorNome,
                   CnpjNum, NomeCompanhia, Ano, Versao, Controlador,
                   PercON, PercPN, PercTotal
            FROM posicao
            WHERE Chave IN @chaves
            ORDER BY Chave, PercTotal DESC, NomeCompanhia;
            """;

        var rows = await cn.QueryAsync(new CommandDefinition(
            sql, new { ano = (short?)ano, chaves }, cancellationToken: ct));

        var result = new List<(string, string?, string?, string?, bool, AcionistaEmpresaPosicao)>();
        foreach (var row in rows)
        {
            var r = new Dictionary<string, object>((IDictionary<string, object>)row);
            var pos = new AcionistaEmpresaPosicao
            {
                CnpjNum = r.GetValueOrDefault("CnpjNum") as string ?? string.Empty,
                NomeCompanhia = r.GetValueOrDefault("NomeCompanhia") as string,
                Ano = ToInt(r.GetValueOrDefault("Ano")),
                Versao = ToInt(r.GetValueOrDefault("Versao")),
                Controlador = ToInt(r.GetValueOrDefault("Controlador")) == 1,
                PercentualOrdinarias = ToDecimal(r.GetValueOrDefault("PercON")),
                PercentualPreferenciais = ToDecimal(r.GetValueOrDefault("PercPN")),
                PercentualTotal = ToDecimal(r.GetValueOrDefault("PercTotal")),
            };
            result.Add((
                r.GetValueOrDefault("Chave") as string ?? string.Empty,
                r.GetValueOrDefault("Nome") as string,
                r.GetValueOrDefault("TipoPessoa") as string,
                r.GetValueOrDefault("Documento") as string,
                ToInt(r.GetValueOrDefault("ChavePorNome")) == 1,
                pos));
        }

        return result;
    }

    private static AcionistaRankingItem MapRanking(dynamic row)
    {
        var r = new Dictionary<string, object>((IDictionary<string, object>)row);
        return new AcionistaRankingItem
        {
            Chave = r.GetValueOrDefault("Chave") as string ?? string.Empty,
            Nome = r.GetValueOrDefault("Nome") as string,
            TipoPessoa = r.GetValueOrDefault("TipoPessoa") as string,
            Documento = r.GetValueOrDefault("Documento") as string,
            ChavePorNome = ToInt(r.GetValueOrDefault("ChavePorNome")) == 1,
            Controlador = ToInt(r.GetValueOrDefault("Controlador")) == 1,
            QtdEmpresas = ToInt(r.GetValueOrDefault("QtdEmpresas")) ?? 0,
            PercentualMedio = ToDecimal(r.GetValueOrDefault("PercentualMedio")),
            PercentualMaximo = ToDecimal(r.GetValueOrDefault("PercentualMaximo")),
        };
    }

    /// <summary>Monta uma serie a partir de linhas agrupadas (categoria + N colunas de valor).</summary>
    private static FreSerie SerieRows(
        string chave, string titulo, string tipo,
        IEnumerable<dynamic> rows, string catKey, (string rotulo, string key)[] cols)
    {
        var serie = new FreSerie { Chave = chave, Titulo = titulo, Tipo = tipo };
        var valores = cols.Select(c => new FreSerieValor { Rotulo = c.rotulo }).ToList();
        foreach (var row in rows)
        {
            var d = new Dictionary<string, object>((IDictionary<string, object>)row);
            serie.Categorias.Add(Convert.ToString(d.GetValueOrDefault(catKey), CultureInfo.InvariantCulture) ?? string.Empty);
            for (var i = 0; i < cols.Length; i++)
                valores[i].Dados.Add(ToDecimal(d.GetValueOrDefault(cols[i].key)));
        }
        foreach (var v in valores)
            serie.Valores.Add(v);
        return serie;
    }

    private static decimal? ToDecimal(object? v)
        => v is null || v is DBNull ? null : Convert.ToDecimal(v, CultureInfo.InvariantCulture);

    private static int? ToInt(object? v)
        => v is null || v is DBNull ? null : Convert.ToInt32(v, CultureInfo.InvariantCulture);
}
