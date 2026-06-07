-- V012: views do comparativo homologo (YoY) da DRE, uma por base (DRE v2, secao 5.2).
-- Cruzam, por conta, o periodo corrente (ULTIMO) com o mesmo periodo do ano anterior:
--   ant - ULTIMO do ano anterior (valor final/autoritativo ja importado);
--   pen - PENULTIMO publicado no proprio arquivo corrente (comparativo da CVM).
-- Regras: variacao percentual robusta a base zero/negativa (D7), inversao de sinal,
-- baixa comparabilidade para conta livre (D5) e deteccao de reapresentacao (D8):
-- pen <> ant para o mesmo periodo => Reapresentado = 1. Os valores ja saem em R$
-- (ESCALA_MOEDA normalizada na CTE base); o frontend apenas formata.
-- Idempotente (CREATE OR ALTER). Lotes separados por GO.

CREATE OR ALTER VIEW itr.vw_DreComparativo AS
WITH base AS (
    SELECT CD_CVM, CnpjNum, DENOM_CIA, Conjunto, CD_CONTA, DS_CONTA, ST_CONTA_FIXA, MOEDA,
           ORDEM_EXERC, DT_FIM_EXERC,
           VL_CONTA * CASE UPPER(LTRIM(RTRIM(ESCALA_MOEDA)))
                WHEN 'MILHAO' THEN 1000000.0 WHEN 'MILHÃO' THEN 1000000.0
                WHEN 'MIL' THEN 1000.0 ELSE 1.0 END AS VlBase,
           ROW_NUMBER() OVER (
             PARTITION BY CD_CVM, Conjunto, CD_CONTA, DT_FIM_EXERC, ORDEM_EXERC
             ORDER BY VERSAO DESC, DT_INI_EXERC DESC) AS rn
    FROM itr.Dre
    WHERE DT_FIM_EXERC IS NOT NULL
      AND DT_INI_EXERC = DATEFROMPARTS(YEAR(DT_FIM_EXERC), 1, 1)   -- serie acumulada (D1)
      AND Conjunto IS NOT NULL
)
SELECT
    u.CD_CVM, u.CnpjNum, u.DENOM_CIA, u.Conjunto, u.CD_CONTA, u.DS_CONTA, u.ST_CONTA_FIXA,
    YEAR(u.DT_FIM_EXERC)               AS AnoUltimo,
    u.DT_FIM_EXERC                     AS DtFimUltimo,
    u.VlBase                           AS ValorUltimo,
    ant.DT_FIM_EXERC                   AS DtFimPenultimo,
    ant.VlBase                         AS ValorPenultimo,          -- ULTIMO do ano anterior (final)
    (u.VlBase - ant.VlBase)            AS VariacaoAbsoluta,
    CASE WHEN ant.VlBase IS NULL OR ant.VlBase = 0 THEN NULL       -- D7
         ELSE (u.VlBase - ant.VlBase) / ABS(ant.VlBase) END        AS VariacaoPercentual,
    CASE WHEN ant.VlBase IS NOT NULL
              AND SIGN(u.VlBase) <> SIGN(ant.VlBase) THEN 1 ELSE 0 END AS InversaoDeSinal,
    CASE WHEN u.ST_CONTA_FIXA COLLATE Latin1_General_CI_AI = 'N' THEN 1 ELSE 0 END
                                       AS BaixaComparabilidade,     -- D5
    CASE WHEN pen.VlBase IS NOT NULL AND ant.VlBase IS NOT NULL
              AND ABS(pen.VlBase - ant.VlBase) > 0.005 THEN 1 ELSE 0 END AS Reapresentado, -- D8
    'UNIDADE'                          AS EscalaMoeda
FROM base u
LEFT JOIN base ant   -- ULTIMO do mesmo periodo, ano anterior (valor final/autoritativo)
       ON ant.CD_CVM = u.CD_CVM AND ant.Conjunto = u.Conjunto
      AND ant.CD_CONTA = u.CD_CONTA AND ant.rn = 1
      AND ant.ORDEM_EXERC COLLATE Latin1_General_CI_AI = 'ULTIMO'
      AND ant.DT_FIM_EXERC = DATEADD(YEAR, -1, u.DT_FIM_EXERC)
LEFT JOIN base pen   -- PENULTIMO publicado dentro do arquivo corrente (comparativo restated)
       ON pen.CD_CVM = u.CD_CVM AND pen.Conjunto = u.Conjunto
      AND pen.CD_CONTA = u.CD_CONTA AND pen.rn = 1
      AND pen.ORDEM_EXERC COLLATE Latin1_General_CI_AI = 'PENULTIMO'
      AND pen.DT_FIM_EXERC = DATEADD(YEAR, -1, u.DT_FIM_EXERC)
WHERE u.rn = 1
  AND u.ORDEM_EXERC COLLATE Latin1_General_CI_AI = 'ULTIMO';
GO

CREATE OR ALTER VIEW dfp.vw_DreComparativo AS
WITH base AS (
    SELECT CD_CVM, CnpjNum, DENOM_CIA, Conjunto, CD_CONTA, DS_CONTA, ST_CONTA_FIXA, MOEDA,
           ORDEM_EXERC, DT_FIM_EXERC,
           VL_CONTA * CASE UPPER(LTRIM(RTRIM(ESCALA_MOEDA)))
                WHEN 'MILHAO' THEN 1000000.0 WHEN 'MILHÃO' THEN 1000000.0
                WHEN 'MIL' THEN 1000.0 ELSE 1.0 END AS VlBase,
           ROW_NUMBER() OVER (
             PARTITION BY CD_CVM, Conjunto, CD_CONTA, DT_FIM_EXERC, ORDEM_EXERC
             ORDER BY VERSAO DESC, DT_INI_EXERC DESC) AS rn
    FROM dfp.Dre
    WHERE DT_FIM_EXERC IS NOT NULL
      AND DT_INI_EXERC = DATEFROMPARTS(YEAR(DT_FIM_EXERC), 1, 1)   -- ano cheio (D1)
      AND Conjunto IS NOT NULL
)
SELECT
    u.CD_CVM, u.CnpjNum, u.DENOM_CIA, u.Conjunto, u.CD_CONTA, u.DS_CONTA, u.ST_CONTA_FIXA,
    YEAR(u.DT_FIM_EXERC)               AS AnoUltimo,
    u.DT_FIM_EXERC                     AS DtFimUltimo,
    u.VlBase                           AS ValorUltimo,
    ant.DT_FIM_EXERC                   AS DtFimPenultimo,
    ant.VlBase                         AS ValorPenultimo,          -- ULTIMO do ano anterior (final)
    (u.VlBase - ant.VlBase)            AS VariacaoAbsoluta,
    CASE WHEN ant.VlBase IS NULL OR ant.VlBase = 0 THEN NULL       -- D7
         ELSE (u.VlBase - ant.VlBase) / ABS(ant.VlBase) END        AS VariacaoPercentual,
    CASE WHEN ant.VlBase IS NOT NULL
              AND SIGN(u.VlBase) <> SIGN(ant.VlBase) THEN 1 ELSE 0 END AS InversaoDeSinal,
    CASE WHEN u.ST_CONTA_FIXA COLLATE Latin1_General_CI_AI = 'N' THEN 1 ELSE 0 END
                                       AS BaixaComparabilidade,     -- D5
    CASE WHEN pen.VlBase IS NOT NULL AND ant.VlBase IS NOT NULL
              AND ABS(pen.VlBase - ant.VlBase) > 0.005 THEN 1 ELSE 0 END AS Reapresentado, -- D8
    'UNIDADE'                          AS EscalaMoeda
FROM base u
LEFT JOIN base ant
       ON ant.CD_CVM = u.CD_CVM AND ant.Conjunto = u.Conjunto
      AND ant.CD_CONTA = u.CD_CONTA AND ant.rn = 1
      AND ant.ORDEM_EXERC COLLATE Latin1_General_CI_AI = 'ULTIMO'
      AND ant.DT_FIM_EXERC = DATEADD(YEAR, -1, u.DT_FIM_EXERC)
LEFT JOIN base pen
       ON pen.CD_CVM = u.CD_CVM AND pen.Conjunto = u.Conjunto
      AND pen.CD_CONTA = u.CD_CONTA AND pen.rn = 1
      AND pen.ORDEM_EXERC COLLATE Latin1_General_CI_AI = 'PENULTIMO'
      AND pen.DT_FIM_EXERC = DATEADD(YEAR, -1, u.DT_FIM_EXERC)
WHERE u.rn = 1
  AND u.ORDEM_EXERC COLLATE Latin1_General_CI_AI = 'ULTIMO';
GO
