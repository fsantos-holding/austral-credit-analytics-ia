-- V019: views de apoio do grupo de capital FRE, consumidas pela secao premium
-- "Composicao Capital". Resumem, por empresa/ano (ultima versao), a composicao de
-- acoes (ON/PN/total) a partir de fre.CapitalSocial e o free float a partir de
-- fre.DistribuicaoCapital. CREATE OR ALTER -> idempotente. Lotes separados por GO.

CREATE OR ALTER VIEW fre.vw_CapitalResumo AS
WITH cs AS (
    SELECT
        CnpjNum, Ano, Nome_Companhia, Tipo_Capital, Valor_Capital,
        Quantidade_Acoes_Ordinarias, Quantidade_Acoes_Preferenciais, Quantidade_Total_Acoes, Versao,
        ROW_NUMBER() OVER (
            PARTITION BY CnpjNum, Ano
            ORDER BY Versao DESC, Quantidade_Total_Acoes DESC) AS rn
    FROM fre.CapitalSocial
    WHERE CnpjNum IS NOT NULL AND CnpjNum <> ''
),
dc AS (
    SELECT
        CnpjNum, Ano,
        Quantidade_Acoes_Ordinarias_Circulacao, Quantidade_Acoes_Preferenciais_Circulacao,
        Quantidade_Total_Acoes_Circulacao, Percentual_Total_Acoes_Circulacao, Versao,
        ROW_NUMBER() OVER (PARTITION BY CnpjNum, Ano ORDER BY Versao DESC) AS rn
    FROM fre.DistribuicaoCapital
    WHERE CnpjNum IS NOT NULL AND CnpjNum <> ''
)
SELECT
    cs.CnpjNum,
    cs.Nome_Companhia                              AS DENOM_CIA,
    cs.Ano,
    cs.Versao,
    cs.Tipo_Capital                               AS TipoCapital,
    cs.Valor_Capital                              AS ValorCapital,
    cs.Quantidade_Acoes_Ordinarias                AS AcoesOrdinarias,
    cs.Quantidade_Acoes_Preferenciais             AS AcoesPreferenciais,
    cs.Quantidade_Total_Acoes                     AS AcoesTotal,
    dc.Quantidade_Acoes_Ordinarias_Circulacao     AS AcoesOrdinariasCirculacao,
    dc.Quantidade_Acoes_Preferenciais_Circulacao  AS AcoesPreferenciaisCirculacao,
    dc.Quantidade_Total_Acoes_Circulacao          AS AcoesCirculacao,
    dc.Percentual_Total_Acoes_Circulacao          AS PercentualFreeFloat
FROM cs
LEFT JOIN dc
    ON dc.CnpjNum = cs.CnpjNum AND dc.Ano = cs.Ano AND dc.rn = 1
WHERE cs.rn = 1;
GO
