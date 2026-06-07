-- V007: rastreabilidade de conjunto (Consolidado/Individual), ano e importacao.
--   Conjunto CHAR(3)  - 'CON'/'IND' (so demonstracoes por conta, que tem GRUPO_DFP).
--   Ano      SMALLINT - ano de referencia (extraido do nome do arquivo), em todas as tabelas.
--   ImportacaoId BIGINT - aponta para a linha de dfp.Importacao (ledger, V008) que gerou a linha.
-- Idempotente: cada coluna so e adicionada se COL_LENGTH(...) IS NULL.
-- Recria as views vw_Conta (expoe Conjunto/Ano) e vw_Estrutura (contagem por CON/IND).
-- Lotes separados por GO.

-- ============================ Conjunto (8 tabelas por conta) ==============================
IF COL_LENGTH('dfp.Dre',   'Conjunto') IS NULL ALTER TABLE dfp.Dre   ADD Conjunto CHAR(3) NULL;
IF COL_LENGTH('dfp.Bpa',   'Conjunto') IS NULL ALTER TABLE dfp.Bpa   ADD Conjunto CHAR(3) NULL;
IF COL_LENGTH('dfp.Bpp',   'Conjunto') IS NULL ALTER TABLE dfp.Bpp   ADD Conjunto CHAR(3) NULL;
IF COL_LENGTH('dfp.Dra',   'Conjunto') IS NULL ALTER TABLE dfp.Dra   ADD Conjunto CHAR(3) NULL;
IF COL_LENGTH('dfp.Dva',   'Conjunto') IS NULL ALTER TABLE dfp.Dva   ADD Conjunto CHAR(3) NULL;
IF COL_LENGTH('dfp.DfcMd', 'Conjunto') IS NULL ALTER TABLE dfp.DfcMd ADD Conjunto CHAR(3) NULL;
IF COL_LENGTH('dfp.DfcMi', 'Conjunto') IS NULL ALTER TABLE dfp.DfcMi ADD Conjunto CHAR(3) NULL;
IF COL_LENGTH('dfp.Dmpl',  'Conjunto') IS NULL ALTER TABLE dfp.Dmpl  ADD Conjunto CHAR(3) NULL;
GO

-- ============================ Ano (todas as 11 tabelas) ===================================
IF COL_LENGTH('dfp.Documento',         'Ano') IS NULL ALTER TABLE dfp.Documento         ADD Ano SMALLINT NULL;
IF COL_LENGTH('dfp.Dre',               'Ano') IS NULL ALTER TABLE dfp.Dre               ADD Ano SMALLINT NULL;
IF COL_LENGTH('dfp.Bpa',               'Ano') IS NULL ALTER TABLE dfp.Bpa               ADD Ano SMALLINT NULL;
IF COL_LENGTH('dfp.Bpp',               'Ano') IS NULL ALTER TABLE dfp.Bpp               ADD Ano SMALLINT NULL;
IF COL_LENGTH('dfp.Dra',               'Ano') IS NULL ALTER TABLE dfp.Dra               ADD Ano SMALLINT NULL;
IF COL_LENGTH('dfp.Dva',               'Ano') IS NULL ALTER TABLE dfp.Dva               ADD Ano SMALLINT NULL;
IF COL_LENGTH('dfp.DfcMd',             'Ano') IS NULL ALTER TABLE dfp.DfcMd             ADD Ano SMALLINT NULL;
IF COL_LENGTH('dfp.DfcMi',             'Ano') IS NULL ALTER TABLE dfp.DfcMi             ADD Ano SMALLINT NULL;
IF COL_LENGTH('dfp.Dmpl',              'Ano') IS NULL ALTER TABLE dfp.Dmpl              ADD Ano SMALLINT NULL;
IF COL_LENGTH('dfp.ComposicaoCapital', 'Ano') IS NULL ALTER TABLE dfp.ComposicaoCapital ADD Ano SMALLINT NULL;
IF COL_LENGTH('dfp.Parecer',           'Ano') IS NULL ALTER TABLE dfp.Parecer           ADD Ano SMALLINT NULL;
GO

-- ============================ ImportacaoId (todas as 11 tabelas) ==========================
IF COL_LENGTH('dfp.Documento',         'ImportacaoId') IS NULL ALTER TABLE dfp.Documento         ADD ImportacaoId BIGINT NULL;
IF COL_LENGTH('dfp.Dre',               'ImportacaoId') IS NULL ALTER TABLE dfp.Dre               ADD ImportacaoId BIGINT NULL;
IF COL_LENGTH('dfp.Bpa',               'ImportacaoId') IS NULL ALTER TABLE dfp.Bpa               ADD ImportacaoId BIGINT NULL;
IF COL_LENGTH('dfp.Bpp',               'ImportacaoId') IS NULL ALTER TABLE dfp.Bpp               ADD ImportacaoId BIGINT NULL;
IF COL_LENGTH('dfp.Dra',               'ImportacaoId') IS NULL ALTER TABLE dfp.Dra               ADD ImportacaoId BIGINT NULL;
IF COL_LENGTH('dfp.Dva',               'ImportacaoId') IS NULL ALTER TABLE dfp.Dva               ADD ImportacaoId BIGINT NULL;
IF COL_LENGTH('dfp.DfcMd',             'ImportacaoId') IS NULL ALTER TABLE dfp.DfcMd             ADD ImportacaoId BIGINT NULL;
IF COL_LENGTH('dfp.DfcMi',             'ImportacaoId') IS NULL ALTER TABLE dfp.DfcMi             ADD ImportacaoId BIGINT NULL;
IF COL_LENGTH('dfp.Dmpl',              'ImportacaoId') IS NULL ALTER TABLE dfp.Dmpl              ADD ImportacaoId BIGINT NULL;
IF COL_LENGTH('dfp.ComposicaoCapital', 'ImportacaoId') IS NULL ALTER TABLE dfp.ComposicaoCapital ADD ImportacaoId BIGINT NULL;
IF COL_LENGTH('dfp.Parecer',           'ImportacaoId') IS NULL ALTER TABLE dfp.Parecer           ADD ImportacaoId BIGINT NULL;
GO

-- ===================== Indices auxiliares do DELETE por escopo =============================
-- Tabelas por conta: escopo = (Ano, Conjunto).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dre_AnoConjunto'   AND object_id = OBJECT_ID('dfp.Dre'))   CREATE INDEX IX_Dre_AnoConjunto   ON dfp.Dre(Ano, Conjunto);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bpa_AnoConjunto'   AND object_id = OBJECT_ID('dfp.Bpa'))   CREATE INDEX IX_Bpa_AnoConjunto   ON dfp.Bpa(Ano, Conjunto);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bpp_AnoConjunto'   AND object_id = OBJECT_ID('dfp.Bpp'))   CREATE INDEX IX_Bpp_AnoConjunto   ON dfp.Bpp(Ano, Conjunto);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dra_AnoConjunto'   AND object_id = OBJECT_ID('dfp.Dra'))   CREATE INDEX IX_Dra_AnoConjunto   ON dfp.Dra(Ano, Conjunto);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dva_AnoConjunto'   AND object_id = OBJECT_ID('dfp.Dva'))   CREATE INDEX IX_Dva_AnoConjunto   ON dfp.Dva(Ano, Conjunto);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DfcMd_AnoConjunto' AND object_id = OBJECT_ID('dfp.DfcMd')) CREATE INDEX IX_DfcMd_AnoConjunto ON dfp.DfcMd(Ano, Conjunto);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DfcMi_AnoConjunto' AND object_id = OBJECT_ID('dfp.DfcMi')) CREATE INDEX IX_DfcMi_AnoConjunto ON dfp.DfcMi(Ano, Conjunto);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dmpl_AnoConjunto'  AND object_id = OBJECT_ID('dfp.Dmpl'))  CREATE INDEX IX_Dmpl_AnoConjunto  ON dfp.Dmpl(Ano, Conjunto);
GO

-- Tabelas sem conjunto: escopo = (Ano).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Documento_Ano'         AND object_id = OBJECT_ID('dfp.Documento'))         CREATE INDEX IX_Documento_Ano         ON dfp.Documento(Ano);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ComposicaoCapital_Ano' AND object_id = OBJECT_ID('dfp.ComposicaoCapital')) CREATE INDEX IX_ComposicaoCapital_Ano ON dfp.ComposicaoCapital(Ano);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Parecer_Ano'           AND object_id = OBJECT_ID('dfp.Parecer'))           CREATE INDEX IX_Parecer_Ano           ON dfp.Parecer(Ano);
GO

-- ============================ Recria vw_Conta (expoe Conjunto/Ano) =========================
CREATE OR ALTER VIEW dfp.vw_Conta AS
    SELECT 'DRE'    AS TIPO_DEM, CNPJ_CIA, CnpjNum, CD_CVM, DENOM_CIA, DT_REFER, VERSAO,
           GRUPO_DFP, Conjunto, Ano, MOEDA, ESCALA_MOEDA, ORDEM_EXERC, DT_INI_EXERC, DT_FIM_EXERC,
           CAST(NULL AS VARCHAR(60)) AS COLUNA_DF, CD_CONTA, DS_CONTA, VL_CONTA, ST_CONTA_FIXA
    FROM dfp.Dre
    UNION ALL
    SELECT 'BPA', CNPJ_CIA, CnpjNum, CD_CVM, DENOM_CIA, DT_REFER, VERSAO,
           GRUPO_DFP, Conjunto, Ano, MOEDA, ESCALA_MOEDA, ORDEM_EXERC, CAST(NULL AS DATE), DT_FIM_EXERC,
           CAST(NULL AS VARCHAR(60)), CD_CONTA, DS_CONTA, VL_CONTA, ST_CONTA_FIXA
    FROM dfp.Bpa
    UNION ALL
    SELECT 'BPP', CNPJ_CIA, CnpjNum, CD_CVM, DENOM_CIA, DT_REFER, VERSAO,
           GRUPO_DFP, Conjunto, Ano, MOEDA, ESCALA_MOEDA, ORDEM_EXERC, CAST(NULL AS DATE), DT_FIM_EXERC,
           CAST(NULL AS VARCHAR(60)), CD_CONTA, DS_CONTA, VL_CONTA, ST_CONTA_FIXA
    FROM dfp.Bpp
    UNION ALL
    SELECT 'DRA', CNPJ_CIA, CnpjNum, CD_CVM, DENOM_CIA, DT_REFER, VERSAO,
           GRUPO_DFP, Conjunto, Ano, MOEDA, ESCALA_MOEDA, ORDEM_EXERC, DT_INI_EXERC, DT_FIM_EXERC,
           CAST(NULL AS VARCHAR(60)), CD_CONTA, DS_CONTA, VL_CONTA, ST_CONTA_FIXA
    FROM dfp.Dra
    UNION ALL
    SELECT 'DVA', CNPJ_CIA, CnpjNum, CD_CVM, DENOM_CIA, DT_REFER, VERSAO,
           GRUPO_DFP, Conjunto, Ano, MOEDA, ESCALA_MOEDA, ORDEM_EXERC, DT_INI_EXERC, DT_FIM_EXERC,
           CAST(NULL AS VARCHAR(60)), CD_CONTA, DS_CONTA, VL_CONTA, ST_CONTA_FIXA
    FROM dfp.Dva
    UNION ALL
    SELECT 'DFC_MD', CNPJ_CIA, CnpjNum, CD_CVM, DENOM_CIA, DT_REFER, VERSAO,
           GRUPO_DFP, Conjunto, Ano, MOEDA, ESCALA_MOEDA, ORDEM_EXERC, DT_INI_EXERC, DT_FIM_EXERC,
           CAST(NULL AS VARCHAR(60)), CD_CONTA, DS_CONTA, VL_CONTA, ST_CONTA_FIXA
    FROM dfp.DfcMd
    UNION ALL
    SELECT 'DFC_MI', CNPJ_CIA, CnpjNum, CD_CVM, DENOM_CIA, DT_REFER, VERSAO,
           GRUPO_DFP, Conjunto, Ano, MOEDA, ESCALA_MOEDA, ORDEM_EXERC, DT_INI_EXERC, DT_FIM_EXERC,
           CAST(NULL AS VARCHAR(60)), CD_CONTA, DS_CONTA, VL_CONTA, ST_CONTA_FIXA
    FROM dfp.DfcMi
    UNION ALL
    SELECT 'DMPL', CNPJ_CIA, CnpjNum, CD_CVM, DENOM_CIA, DT_REFER, VERSAO,
           GRUPO_DFP, Conjunto, Ano, MOEDA, ESCALA_MOEDA, ORDEM_EXERC, DT_INI_EXERC, DT_FIM_EXERC,
           COLUNA_DF, CD_CONTA, DS_CONTA, VL_CONTA, ST_CONTA_FIXA
    FROM dfp.Dmpl;
GO

-- ============== Recria vw_Estrutura (contagem por demonstracao, separada por CON/IND) ======
CREATE OR ALTER VIEW dfp.vw_Estrutura AS
    SELECT
        d.CNPJ_CIA,
        d.CnpjNum,
        d.CD_CVM,
        d.DENOM_CIA,
        d.DT_REFER,
        d.VERSAO,
        d.CATEG_DOC,
        d.DT_RECEB,
        d.LINK_DOC,
        (SELECT COUNT(*) FROM dfp.Bpa  x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO AND x.Conjunto = 'CON') AS QtBpaCon,
        (SELECT COUNT(*) FROM dfp.Bpa  x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO AND x.Conjunto = 'IND') AS QtBpaInd,
        (SELECT COUNT(*) FROM dfp.Bpp  x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO AND x.Conjunto = 'CON') AS QtBppCon,
        (SELECT COUNT(*) FROM dfp.Bpp  x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO AND x.Conjunto = 'IND') AS QtBppInd,
        (SELECT COUNT(*) FROM dfp.Dre  x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO AND x.Conjunto = 'CON') AS QtDreCon,
        (SELECT COUNT(*) FROM dfp.Dre  x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO AND x.Conjunto = 'IND') AS QtDreInd,
        (SELECT COUNT(*) FROM dfp.Dra  x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO AND x.Conjunto = 'CON') AS QtDraCon,
        (SELECT COUNT(*) FROM dfp.Dra  x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO AND x.Conjunto = 'IND') AS QtDraInd,
        (SELECT COUNT(*) FROM dfp.Dva  x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO AND x.Conjunto = 'CON') AS QtDvaCon,
        (SELECT COUNT(*) FROM dfp.Dva  x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO AND x.Conjunto = 'IND') AS QtDvaInd,
        (SELECT COUNT(*) FROM dfp.DfcMd x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO AND x.Conjunto = 'CON') AS QtDfcMdCon,
        (SELECT COUNT(*) FROM dfp.DfcMd x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO AND x.Conjunto = 'IND') AS QtDfcMdInd,
        (SELECT COUNT(*) FROM dfp.DfcMi x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO AND x.Conjunto = 'CON') AS QtDfcMiCon,
        (SELECT COUNT(*) FROM dfp.DfcMi x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO AND x.Conjunto = 'IND') AS QtDfcMiInd,
        (SELECT COUNT(*) FROM dfp.Dmpl x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO AND x.Conjunto = 'CON') AS QtDmplCon,
        (SELECT COUNT(*) FROM dfp.Dmpl x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO AND x.Conjunto = 'IND') AS QtDmplInd,
        (SELECT COUNT(*) FROM dfp.ComposicaoCapital x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO) AS QtComposicaoCapital,
        (SELECT COUNT(*) FROM dfp.Parecer x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO) AS QtParecer
    FROM dfp.Documento d;
GO
