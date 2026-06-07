using System.Globalization;
using AustralCreditAnalytics.Api.Models.Fre;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Agrega os dados FRE (schema [fre]) por dominio em um <see cref="FreDossie"/>. Espelha o
/// padrao de <see cref="FreRepository"/> (mesmo <see cref="ISqlConnectionFactory"/>,
/// normalizacao de CNPJ, filtro por Ano/Versao). Todos os nomes de tabela/coluna sao
/// literais de codigo (nunca input livre); os valores (cnpj/ano/versao) sao sempre
/// parametrizados, portanto o SQL e seguro.
/// </summary>
public class FreDossieRepository : IFreDossieRepository
{
    private readonly ISqlConnectionFactory _factory;

    public FreDossieRepository(ISqlConnectionFactory factory) => _factory = factory;

    public async Task<FreDossie> GetDossieAsync(
        string cnpj, int? ano, int? versao, CancellationToken ct = default)
    {
        var cnpjNum = NormalizarCnpj(cnpj);
        var dossie = new FreDossie { CnpjNum = cnpjNum, Ano = ano, Versao = versao };
        if (cnpjNum.Length == 0)
            return dossie;

        await using var cn = _factory.CreateFromSaved();

        // Resolve ano (mais recente) e versao (maior do ano) a partir do indice de documentos.
        var doc = (await cn.QueryAsync(new CommandDefinition("""
            SELECT TOP 1 Ano, VERSAO AS Versao, DENOM_CIA AS Denominacao
            FROM fre.Documento
            WHERE CnpjNum = @cnpjNum AND (@ano IS NULL OR Ano = @ano)
            ORDER BY Ano DESC, VERSAO DESC;
            """, new { cnpjNum, ano = (short?)ano }, cancellationToken: ct))).FirstOrDefault();

        if (doc is not null)
        {
            var d = new Dictionary<string, object>((IDictionary<string, object>)doc);
            dossie.Ano = ano ?? ToInt(d.GetValueOrDefault("Ano"));
            dossie.Versao = versao ?? ToInt(d.GetValueOrDefault("Versao"));
            dossie.Denominacao = d.GetValueOrDefault("Denominacao") as string;
        }

        var p = new { cnpjNum, ano = (short?)dossie.Ano, versao = (short?)dossie.Versao };

        dossie.Secoes.Add(await VisaoGeralAsync(cn, p, ct));
        dossie.Secoes.Add(await AcionariaAsync(cn, p, ct));
        dossie.Secoes.Add(await GovernancaAsync(cn, p, ct));
        dossie.Secoes.Add(await RemuneracaoAsync(cn, p, ct));
        dossie.Secoes.Add(await EmpregadosAsync(cn, p, ct));
        dossie.Secoes.Add(await ExteriorAsync(cn, p, ct));
        dossie.Secoes.Add(await PartesRelacionadasAsync(cn, p, ct));

        return dossie;
    }

    // ---------------------------------------------------------------- Visao Geral

    private static async Task<FreDossieSecao> VisaoGeralAsync(
        SqlConnection cn, object p, CancellationToken ct)
    {
        var secao = new FreDossieSecao { Chave = "visao", Titulo = "Visao Geral" };

        var totAdmin = await ScalarAsync(cn, """
            SELECT SUM(ISNULL(Quantidade_Masculino,0) + ISNULL(Quantidade_Feminino,0)
                     + ISNULL(Quantidade_Nao_Binario,0) + ISNULL(Quantidade_Outros,0)
                     + ISNULL(Quantidade_Sem_Resposta,0))
            FROM fre.AdministradorDeclaracaoGenero
            WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao;
            """, p, ct);

        var totAuditores = await ScalarAsync(cn, """
            SELECT COUNT(DISTINCT Auditor)
            FROM fre.Auditor
            WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao;
            """, p, ct);

        var totRemun = await ScalarAsync(cn, """
            SELECT SUM(ISNULL(Total_Remuneracao,0))
            FROM fre.RemuneracaoTotalOrgao
            WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao;
            """, p, ct);

        var totPartes = await ScalarAsync(cn, """
            SELECT COUNT(*)
            FROM fre.TransacaoParteRelacionada
            WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao;
            """, p, ct);

        var freeFloat = await ScalarAsync(cn, """
            SELECT TOP 1 PercentualFreeFloat
            FROM fre.vw_CapitalResumo
            WHERE CnpjNum = @cnpjNum AND Ano = @ano;
            """, p, ct);

        secao.Kpis.Add(new FreKpi { Rotulo = "Administradores", Valor = totAdmin, Formato = "inteiro", Icone = "users" });
        secao.Kpis.Add(new FreKpi { Rotulo = "Auditores", Valor = totAuditores, Formato = "inteiro", Icone = "shield-check" });
        secao.Kpis.Add(new FreKpi { Rotulo = "Remuneracao Total", Valor = totRemun, Formato = "moeda", Icone = "cash" });
        secao.Kpis.Add(new FreKpi { Rotulo = "Partes Relacionadas", Valor = totPartes, Formato = "inteiro", Icone = "arrows-exchange" });
        secao.Kpis.Add(new FreKpi { Rotulo = "Free Float", Valor = freeFloat, Formato = "percent", Icone = "chart-pie" });

        // Historico do emissor (constituicao/registro) como bloco de cards.
        var hist = await SingleAsync(cn, """
            SELECT TOP 1 Data_Constituicao_Emissor, Pais_Constituicao_Emissor,
                   Forma_Constituicao_Emissor, Data_Registro_Emissor, Requisicao_Registro_Emissor
            FROM fre.HistoricoEmissor
            WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao
            ORDER BY Versao DESC;
            """, p, ct);

        if (hist is not null)
        {
            var bloco = new FreDossieBloco { Chave = "historico_emissor", Titulo = "Historico do Emissor", Icone = "building" };
            bloco.Itens.Add(new FreBlocoItem { Rotulo = "Data de Constituicao", Valor = FormatarData(hist.GetValueOrDefault("Data_Constituicao_Emissor")) });
            bloco.Itens.Add(new FreBlocoItem { Rotulo = "Pais de Constituicao", Valor = hist.GetValueOrDefault("Pais_Constituicao_Emissor") as string });
            bloco.Itens.Add(new FreBlocoItem { Rotulo = "Forma de Constituicao", Valor = hist.GetValueOrDefault("Forma_Constituicao_Emissor") as string });
            bloco.Itens.Add(new FreBlocoItem { Rotulo = "Data de Registro CVM", Valor = FormatarData(hist.GetValueOrDefault("Data_Registro_Emissor")) });
            bloco.Itens.Add(new FreBlocoItem { Rotulo = "Requisicao do Registro", Valor = hist.GetValueOrDefault("Requisicao_Registro_Emissor") as string });
            secao.Blocos.Add(bloco);
        }

        return secao;
    }

    // ---------------------------------------------------------------- Acionaria

    private static async Task<FreDossieSecao> AcionariaAsync(
        SqlConnection cn, object p, CancellationToken ct)
    {
        var secao = new FreDossieSecao { Chave = "acionaria", Titulo = "Composicao Acionaria" };

        var cap = await SingleAsync(cn, """
            SELECT TOP 1 AcoesOrdinarias, AcoesPreferenciais, AcoesTotal, PercentualFreeFloat
            FROM fre.vw_CapitalResumo
            WHERE CnpjNum = @cnpjNum AND Ano = @ano;
            """, p, ct);

        if (cap is not null)
        {
            secao.Kpis.Add(new FreKpi { Rotulo = "Acoes Ordinarias (ON)", Valor = ToDecimal(cap.GetValueOrDefault("AcoesOrdinarias")), Formato = "inteiro", Icone = "chart-bar" });
            secao.Kpis.Add(new FreKpi { Rotulo = "Acoes Preferenciais (PN)", Valor = ToDecimal(cap.GetValueOrDefault("AcoesPreferenciais")), Formato = "inteiro", Icone = "chart-bar" });
            secao.Kpis.Add(new FreKpi { Rotulo = "Total de Acoes", Valor = ToDecimal(cap.GetValueOrDefault("AcoesTotal")), Formato = "inteiro", Icone = "stack" });
            secao.Kpis.Add(new FreKpi { Rotulo = "Free Float", Valor = ToDecimal(cap.GetValueOrDefault("PercentualFreeFloat")), Formato = "percent", Icone = "chart-pie" });
        }

        // Donut: posicao acionaria por acionista (% do capital total).
        var acionistas = await cn.QueryAsync(new CommandDefinition("""
            SELECT TOP 10 Acionista AS Cat, SUM(ISNULL(Percentual_Total_Acoes_Circulacao,0)) AS V
            FROM fre.PosicaoAcionaria
            WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao
              AND Acionista IS NOT NULL AND Acionista <> ''
            GROUP BY Acionista
            HAVING SUM(ISNULL(Percentual_Total_Acoes_Circulacao,0)) > 0
            ORDER BY V DESC;
            """, p, cancellationToken: ct));

        var serieAcionistas = SerieRows("posicao_acionaria", "Posicao Acionaria (% do capital)", "donut",
            acionistas, "Cat", new[] { ("% Capital", "V") });
        if (serieAcionistas.Categorias.Count > 0)
            secao.Series.Add(serieAcionistas);

        // Donut: distribuicao de acionistas por tipo de pessoa.
        var dist = await SingleAsync(cn, """
            SELECT TOP 1 Quantidade_Acionistas_PF, Quantidade_Acionistas_PJ,
                   Quantidade_Acionistas_Investidores_Institucionais
            FROM fre.DistribuicaoCapital
            WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao
            ORDER BY Versao DESC;
            """, p, ct);

        if (dist is not null)
        {
            var serie = new FreSerie { Chave = "distribuicao_capital", Titulo = "Acionistas por Tipo de Pessoa", Tipo = "donut" };
            serie.Categorias.Add("Pessoa Fisica");
            serie.Categorias.Add("Pessoa Juridica");
            serie.Categorias.Add("Institucional");
            serie.Valores.Add(new FreSerieValor
            {
                Rotulo = "Acionistas",
                Dados = new List<decimal?>
                {
                    ToDecimal(dist.GetValueOrDefault("Quantidade_Acionistas_PF")),
                    ToDecimal(dist.GetValueOrDefault("Quantidade_Acionistas_PJ")),
                    ToDecimal(dist.GetValueOrDefault("Quantidade_Acionistas_Investidores_Institucionais")),
                },
            });
            if (serie.Valores[0].Dados.Any(v => v.GetValueOrDefault() > 0))
                secao.Series.Add(serie);
        }

        return secao;
    }

    // ---------------------------------------------------------------- Governanca

    private static async Task<FreDossieSecao> GovernancaAsync(
        SqlConnection cn, object p, CancellationToken ct)
    {
        var secao = new FreDossieSecao { Chave = "governanca", Titulo = "Governanca & Pessoas" };

        var genero = await cn.QueryAsync(new CommandDefinition("""
            SELECT Orgao_Administracao AS Cat,
                   SUM(ISNULL(Quantidade_Masculino,0))    AS Masculino,
                   SUM(ISNULL(Quantidade_Feminino,0))     AS Feminino,
                   SUM(ISNULL(Quantidade_Nao_Binario,0))  AS NaoBinario,
                   SUM(ISNULL(Quantidade_Outros,0))       AS Outros,
                   SUM(ISNULL(Quantidade_Sem_Resposta,0)) AS SemResposta
            FROM fre.AdministradorDeclaracaoGenero
            WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao
            GROUP BY Orgao_Administracao
            ORDER BY Orgao_Administracao;
            """, p, cancellationToken: ct));

        var serieGenero = SerieRows("administrador_genero", "Genero por Orgao de Administracao", "barras_empilhadas",
            genero, "Cat", new[]
            {
                ("Masculino", "Masculino"), ("Feminino", "Feminino"), ("Nao Binario", "NaoBinario"),
                ("Outros", "Outros"), ("Sem Resposta", "SemResposta"),
            });
        if (serieGenero.Categorias.Count > 0)
            secao.Series.Add(serieGenero);

        var raca = await cn.QueryAsync(new CommandDefinition("""
            SELECT Orgao_Administracao AS Cat,
                   SUM(ISNULL(Quantidade_Branco,0))       AS Branco,
                   SUM(ISNULL(Quantidade_Preto,0))        AS Preto,
                   SUM(ISNULL(Quantidade_Pardo,0))        AS Pardo,
                   SUM(ISNULL(Quantidade_Amarelo,0))      AS Amarelo,
                   SUM(ISNULL(Quantidade_Indigena,0))     AS Indigena,
                   SUM(ISNULL(Quantidade_Outros,0))       AS Outros,
                   SUM(ISNULL(Quantidade_Sem_Resposta,0)) AS SemResposta
            FROM fre.AdministradorDeclaracaoRaca
            WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao
            GROUP BY Orgao_Administracao
            ORDER BY Orgao_Administracao;
            """, p, cancellationToken: ct));

        var serieRaca = SerieRows("administrador_raca", "Raca/Cor por Orgao de Administracao", "barras_empilhadas",
            raca, "Cat", new[]
            {
                ("Branco", "Branco"), ("Preto", "Preto"), ("Pardo", "Pardo"), ("Amarelo", "Amarelo"),
                ("Indigena", "Indigena"), ("Outros", "Outros"), ("Sem Resposta", "SemResposta"),
            });
        if (serieRaca.Categorias.Count > 0)
            secao.Series.Add(serieRaca);

        // KPIs: conselho fiscal, comites e participacao media em reunioes.
        var conselho = await ScalarAsync(cn, """
            SELECT COUNT(*) FROM fre.AdministradorMembroConselhoFiscal
            WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao;
            """, p, ct);
        var comites = await ScalarAsync(cn, """
            SELECT COUNT(*) FROM fre.MembroComite
            WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao;
            """, p, ct);
        var partReunioes = await ScalarAsync(cn, """
            SELECT AVG(Percentual_Participacao_Reunioes) FROM fre.AdministradorMembroConselhoFiscal
            WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao
              AND Percentual_Participacao_Reunioes IS NOT NULL;
            """, p, ct);

        secao.Kpis.Add(new FreKpi { Rotulo = "Membros do Conselho Fiscal", Valor = conselho, Formato = "inteiro", Icone = "gavel" });
        secao.Kpis.Add(new FreKpi { Rotulo = "Membros de Comites", Valor = comites, Formato = "inteiro", Icone = "users-group" });
        secao.Kpis.Add(new FreKpi { Rotulo = "Participacao Media em Reunioes", Valor = partReunioes, Formato = "percent", Icone = "calendar-check" });

        // Bloco: auditor independente (registro mais recente) + responsavel tecnico.
        var auditor = await SingleAsync(cn, """
            SELECT TOP 1 Auditor, CNPJ_Auditor, Servico_Contratado, Tipo_Origem_Auditor,
                   Data_Inicio_Contratacao, Data_Fim_Contratacao
            FROM fre.Auditor
            WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao
            ORDER BY Versao DESC, Data_Inicio_Contratacao DESC;
            """, p, ct);

        if (auditor is not null)
        {
            var bloco = new FreDossieBloco { Chave = "auditor", Titulo = "Auditor Independente", Icone = "shield-check" };
            bloco.Itens.Add(new FreBlocoItem { Rotulo = "Auditor", Valor = auditor.GetValueOrDefault("Auditor") as string });
            bloco.Itens.Add(new FreBlocoItem { Rotulo = "CNPJ", Valor = auditor.GetValueOrDefault("CNPJ_Auditor") as string });
            bloco.Itens.Add(new FreBlocoItem { Rotulo = "Servico Contratado", Valor = auditor.GetValueOrDefault("Servico_Contratado") as string });
            bloco.Itens.Add(new FreBlocoItem { Rotulo = "Origem", Valor = auditor.GetValueOrDefault("Tipo_Origem_Auditor") as string });
            var ini = FormatarData(auditor.GetValueOrDefault("Data_Inicio_Contratacao"));
            var fim = FormatarData(auditor.GetValueOrDefault("Data_Fim_Contratacao"));
            bloco.Itens.Add(new FreBlocoItem { Rotulo = "Periodo de Contratacao", Valor = $"{ini ?? "-"} ate {fim ?? "atual"}" });

            var resp = await SingleAsync(cn, """
                SELECT TOP 1 Responsavel_Tecnico, Email
                FROM fre.AuditorResponsavel
                WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao
                ORDER BY Versao DESC;
                """, p, ct);
            if (resp is not null)
            {
                bloco.Itens.Add(new FreBlocoItem { Rotulo = "Responsavel Tecnico", Valor = resp.GetValueOrDefault("Responsavel_Tecnico") as string });
                bloco.Itens.Add(new FreBlocoItem { Rotulo = "E-mail", Valor = resp.GetValueOrDefault("Email") as string });
            }
            secao.Blocos.Add(bloco);
        }

        // Barras horizontais: relacoes familiares por tipo de parentesco.
        var familiar = await cn.QueryAsync(new CommandDefinition("""
            SELECT Tipo_Parentesco AS Cat, COUNT(*) AS V
            FROM fre.RelacaoFamiliar
            WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao
              AND Tipo_Parentesco IS NOT NULL AND Tipo_Parentesco <> ''
            GROUP BY Tipo_Parentesco
            ORDER BY V DESC;
            """, p, cancellationToken: ct));
        var serieFamiliar = SerieRows("relacao_familiar", "Relacoes Familiares por Parentesco", "barras_horizontais",
            familiar, "Cat", new[] { ("Ocorrencias", "V") });
        if (serieFamiliar.Categorias.Count > 0)
            secao.Series.Add(serieFamiliar);

        // Barras horizontais: relacoes de subordinacao por tipo.
        var subord = await cn.QueryAsync(new CommandDefinition("""
            SELECT Tipo_Relacao AS Cat, COUNT(*) AS V
            FROM fre.RelacaoSubordinacao
            WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao
              AND Tipo_Relacao IS NOT NULL AND Tipo_Relacao <> ''
            GROUP BY Tipo_Relacao
            ORDER BY V DESC;
            """, p, cancellationToken: ct));
        var serieSubord = SerieRows("relacao_subordinacao", "Relacoes de Subordinacao por Tipo", "barras_horizontais",
            subord, "Cat", new[] { ("Ocorrencias", "V") });
        if (serieSubord.Categorias.Count > 0)
            secao.Series.Add(serieSubord);

        return secao;
    }

    // ---------------------------------------------------------------- Remuneracao

    private static async Task<FreDossieSecao> RemuneracaoAsync(
        SqlConnection cn, object p, CancellationToken ct)
    {
        var secao = new FreDossieSecao { Chave = "remuneracao", Titulo = "Remuneracao" };

        var total = await ScalarAsync(cn, """
            SELECT SUM(ISNULL(Total_Remuneracao,0))
            FROM fre.RemuneracaoTotalOrgao
            WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao;
            """, p, ct);
        secao.Kpis.Add(new FreKpi { Rotulo = "Remuneracao Total", Valor = total, Formato = "moeda", Icone = "cash" });

        // Barras empilhadas: composicao da remuneracao por orgao.
        var comp = await cn.QueryAsync(new CommandDefinition("""
            SELECT Orgao_Administracao AS Cat,
                   SUM(ISNULL(Salario,0))                       AS Salario,
                   SUM(ISNULL(Bonus,0))                         AS Bonus,
                   SUM(ISNULL(Participacao_Resultados,0))       AS Participacao,
                   SUM(ISNULL(Baseada_Acoes,0))                 AS Acoes,
                   SUM(ISNULL(Beneficios_Diretos_Indiretos,0))  AS Beneficios,
                   SUM(ISNULL(Comissoes,0))                     AS Comissoes
            FROM fre.RemuneracaoTotalOrgao
            WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao
            GROUP BY Orgao_Administracao
            ORDER BY Orgao_Administracao;
            """, p, cancellationToken: ct));
        var serieComp = SerieRows("remuneracao_composicao", "Composicao da Remuneracao por Orgao", "barras_empilhadas",
            comp, "Cat", new[]
            {
                ("Salario", "Salario"), ("Bonus", "Bonus"), ("Part. Resultados", "Participacao"),
                ("Baseada em Acoes", "Acoes"), ("Beneficios", "Beneficios"), ("Comissoes", "Comissoes"),
            });
        if (serieComp.Categorias.Count > 0)
            secao.Series.Add(serieComp);

        // Barras: maior / media / menor remuneracao por orgao.
        var faixa = await cn.QueryAsync(new CommandDefinition("""
            SELECT Orgao_Administracao AS Cat,
                   MAX(ISNULL(Valor_Maior_Remuneracao,0)) AS Maior,
                   AVG(ISNULL(Valor_Medio_Remuneracao,0)) AS Media,
                   MIN(ISNULL(Valor_Menor_Remuneracao,0)) AS Menor
            FROM fre.RemuneracaoMaximaMinimaMedia
            WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao
            GROUP BY Orgao_Administracao
            ORDER BY Orgao_Administracao;
            """, p, cancellationToken: ct));
        var serieFaixa = SerieRows("remuneracao_faixa", "Maior / Media / Menor Remuneracao por Orgao", "barras",
            faixa, "Cat", new[] { ("Maior", "Maior"), ("Media", "Media"), ("Menor", "Menor") });
        if (serieFaixa.Categorias.Count > 0)
            secao.Series.Add(serieFaixa);

        return secao;
    }

    // ---------------------------------------------------------------- Empregados

    private static async Task<FreDossieSecao> EmpregadosAsync(
        SqlConnection cn, object p, CancellationToken ct)
    {
        var secao = new FreDossieSecao { Chave = "empregados", Titulo = "Empregados" };

        // Barras empilhadas: faixa etaria por local.
        var faixa = await cn.QueryAsync(new CommandDefinition("""
            SELECT Local AS Cat,
                   SUM(ISNULL(Quantidade_Ate30Anos,0))   AS Ate30,
                   SUM(ISNULL(Quantidade_30a50Anos,0))   AS De30a50,
                   SUM(ISNULL(Quantidade_Acima50Anos,0)) AS Acima50
            FROM fre.EmpregadoLocalFaixaEtaria
            WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao
            GROUP BY Local
            ORDER BY Local;
            """, p, cancellationToken: ct));
        var serieFaixa = SerieRows("empregado_faixa_etaria", "Faixa Etaria por Local", "barras_empilhadas",
            faixa, "Cat", new[] { ("Ate 30 anos", "Ate30"), ("30 a 50 anos", "De30a50"), ("Acima de 50 anos", "Acima50") });
        if (serieFaixa.Categorias.Count > 0)
            secao.Series.Add(serieFaixa);

        // Donut: genero (somado em todas as classes).
        var genero = await SingleAsync(cn, """
            SELECT SUM(ISNULL(Quantidade_Masculino,0))    AS Masculino,
                   SUM(ISNULL(Quantidade_Feminino,0))     AS Feminino,
                   SUM(ISNULL(Quantidade_Nao_Binario,0))  AS NaoBinario,
                   SUM(ISNULL(Quantidade_Outros,0))       AS Outros,
                   SUM(ISNULL(Quantidade_Sem_Resposta,0)) AS SemResposta
            FROM fre.EmpregadoDeclaracaoGenero
            WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao;
            """, p, ct);
        if (genero is not null)
        {
            var serie = new FreSerie { Chave = "empregado_genero", Titulo = "Empregados por Genero", Tipo = "donut" };
            foreach (var (rot, key) in new[] { ("Masculino", "Masculino"), ("Feminino", "Feminino"),
                         ("Nao Binario", "NaoBinario"), ("Outros", "Outros"), ("Sem Resposta", "SemResposta") })
                serie.Categorias.Add(rot);
            serie.Valores.Add(new FreSerieValor
            {
                Rotulo = "Empregados",
                Dados = new List<decimal?>
                {
                    ToDecimal(genero.GetValueOrDefault("Masculino")),
                    ToDecimal(genero.GetValueOrDefault("Feminino")),
                    ToDecimal(genero.GetValueOrDefault("NaoBinario")),
                    ToDecimal(genero.GetValueOrDefault("Outros")),
                    ToDecimal(genero.GetValueOrDefault("SemResposta")),
                },
            });
            if (serie.Valores[0].Dados.Any(v => v.GetValueOrDefault() > 0))
                secao.Series.Add(serie);
        }

        // Barras empilhadas: raca/cor por classe.
        var raca = await cn.QueryAsync(new CommandDefinition("""
            SELECT Classe AS Cat,
                   SUM(ISNULL(Quantidade_Branco,0))       AS Branco,
                   SUM(ISNULL(Quantidade_Preto,0))        AS Preto,
                   SUM(ISNULL(Quantidade_Pardo,0))        AS Pardo,
                   SUM(ISNULL(Quantidade_Amarelo,0))      AS Amarelo,
                   SUM(ISNULL(Quantidade_Indigena,0))     AS Indigena,
                   SUM(ISNULL(Quantidade_Outros,0))       AS Outros,
                   SUM(ISNULL(Quantidade_Sem_Resposta,0)) AS SemResposta
            FROM fre.EmpregadoDeclaracaoRaca
            WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao
            GROUP BY Classe
            ORDER BY Classe;
            """, p, cancellationToken: ct));
        var serieRaca = SerieRows("empregado_raca", "Raca/Cor por Classe", "barras_empilhadas",
            raca, "Cat", new[]
            {
                ("Branco", "Branco"), ("Preto", "Preto"), ("Pardo", "Pardo"), ("Amarelo", "Amarelo"),
                ("Indigena", "Indigena"), ("Outros", "Outros"), ("Sem Resposta", "SemResposta"),
            });
        if (serieRaca.Categorias.Count > 0)
            secao.Series.Add(serieRaca);

        return secao;
    }

    // ---------------------------------------------------------------- Exterior

    private static async Task<FreDossieSecao> ExteriorAsync(
        SqlConnection cn, object p, CancellationToken ct)
    {
        var secao = new FreDossieSecao { Chave = "exterior", Titulo = "Titulos no Exterior" };

        var totTitulos = await ScalarAsync(cn, """
            SELECT COUNT(*) FROM fre.TituloExterior
            WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao;
            """, p, ct);
        var totMercados = await ScalarAsync(cn, """
            SELECT COUNT(*) FROM fre.MercadoEstrangeiro
            WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao;
            """, p, ct);
        secao.Kpis.Add(new FreKpi { Rotulo = "Titulos no Exterior", Valor = totTitulos, Formato = "inteiro", Icone = "world" });
        secao.Kpis.Add(new FreKpi { Rotulo = "Mercados Estrangeiros", Valor = totMercados, Formato = "inteiro", Icone = "building-bank" });

        // Barras: listagens em mercados estrangeiros por pais de negociacao.
        var porPais = await cn.QueryAsync(new CommandDefinition("""
            SELECT Pais_Negociacao AS Cat, COUNT(*) AS V
            FROM fre.MercadoEstrangeiro
            WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao
              AND Pais_Negociacao IS NOT NULL AND Pais_Negociacao <> ''
            GROUP BY Pais_Negociacao
            ORDER BY V DESC;
            """, p, cancellationToken: ct));
        var seriePais = SerieRows("mercado_estrangeiro_pais", "Listagens por Pais de Negociacao", "barras",
            porPais, "Cat", new[] { ("Listagens", "V") });
        if (seriePais.Categorias.Count > 0)
            secao.Series.Add(seriePais);

        return secao;
    }

    // ---------------------------------------------------------------- Partes Relacionadas

    private static async Task<FreDossieSecao> PartesRelacionadasAsync(
        SqlConnection cn, object p, CancellationToken ct)
    {
        var secao = new FreDossieSecao { Chave = "partes", Titulo = "Partes Relacionadas" };

        var resumo = await SingleAsync(cn, """
            SELECT COUNT(*) AS Qtd, SUM(ISNULL(Montante_Envolvido,0)) AS Montante
            FROM fre.TransacaoParteRelacionada
            WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao;
            """, p, ct);
        if (resumo is not null)
        {
            secao.Kpis.Add(new FreKpi { Rotulo = "Transacoes", Valor = ToDecimal(resumo.GetValueOrDefault("Qtd")), Formato = "inteiro", Icone = "arrows-exchange" });
            secao.Kpis.Add(new FreKpi { Rotulo = "Montante Total Envolvido", Valor = ToDecimal(resumo.GetValueOrDefault("Montante")), Formato = "moeda", Icone = "cash" });
        }

        var top = await cn.QueryAsync(new CommandDefinition("""
            SELECT TOP 10 Parte_Relacionada AS Cat, SUM(ISNULL(Montante_Envolvido,0)) AS V
            FROM fre.TransacaoParteRelacionada
            WHERE CnpjNum = @cnpjNum AND Ano = @ano AND Versao = @versao
              AND Parte_Relacionada IS NOT NULL AND Parte_Relacionada <> ''
            GROUP BY Parte_Relacionada
            HAVING SUM(ISNULL(Montante_Envolvido,0)) > 0
            ORDER BY V DESC;
            """, p, cancellationToken: ct));
        var serieTop = SerieRows("partes_top", "Maiores Montantes por Parte Relacionada", "barras_horizontais",
            top, "Cat", new[] { ("Montante", "V") });
        if (serieTop.Categorias.Count > 0)
            secao.Series.Add(serieTop);

        return secao;
    }

    // ---------------------------------------------------------------- Helpers

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

    private static async Task<decimal?> ScalarAsync(SqlConnection cn, string sql, object p, CancellationToken ct)
    {
        var v = await cn.ExecuteScalarAsync(new CommandDefinition(sql, p, cancellationToken: ct));
        return ToDecimal(v);
    }

    private static async Task<Dictionary<string, object>?> SingleAsync(
        SqlConnection cn, string sql, object p, CancellationToken ct)
    {
        var row = (await cn.QueryAsync(new CommandDefinition(sql, p, cancellationToken: ct))).FirstOrDefault();
        return row is null ? null : new Dictionary<string, object>((IDictionary<string, object>)row);
    }

    private static decimal? ToDecimal(object? v)
        => v is null || v is DBNull ? null : Convert.ToDecimal(v, CultureInfo.InvariantCulture);

    private static int? ToInt(object? v)
        => v is null || v is DBNull ? null : Convert.ToInt32(v, CultureInfo.InvariantCulture);

    private static string? FormatarData(object? v)
        => v is DateTime dt ? dt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : null;

    private static string NormalizarCnpj(string? cnpj)
        => string.IsNullOrWhiteSpace(cnpj)
            ? string.Empty
            : new string(cnpj.Where(char.IsDigit).ToArray());
}
