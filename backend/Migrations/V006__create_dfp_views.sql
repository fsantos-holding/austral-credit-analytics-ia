-- V006: views de rastreio por CNPJ.
--   dfp.vw_Conta     - consolida todas as demonstracoes por conta (DRE, BPA, BPP,
--                      DRA, DVA, DFC_MD, DFC_MI, DMPL) num formato unico, com a
--                      coluna TIPO_DEM identificando a origem. Permite ler "toda a
--                      estrutura" de um CNPJ em uma so consulta.
--   dfp.vw_Estrutura - resumo por documento (CNPJ/periodo/versao) com a contagem de
--                      linhas existentes em cada demonstracao.
-- Recriadas via CREATE OR ALTER (idempotente). Lotes separados por GO.

CREATE OR ALTER VIEW dfp.vw_Conta AS
    SELECT 'DRE'    AS TIPO_DEM, CNPJ_CIA, CnpjNum, CD_CVM, DENOM_CIA, DT_REFER, VERSAO,
           GRUPO_DFP, MOEDA, ESCALA_MOEDA, ORDEM_EXERC, DT_INI_EXERC, DT_FIM_EXERC,
           CAST(NULL AS VARCHAR(60)) AS COLUNA_DF, CD_CONTA, DS_CONTA, VL_CONTA, ST_CONTA_FIXA
    FROM dfp.Dre
    UNION ALL
    SELECT 'BPA', CNPJ_CIA, CnpjNum, CD_CVM, DENOM_CIA, DT_REFER, VERSAO,
           GRUPO_DFP, MOEDA, ESCALA_MOEDA, ORDEM_EXERC, CAST(NULL AS DATE), DT_FIM_EXERC,
           CAST(NULL AS VARCHAR(60)), CD_CONTA, DS_CONTA, VL_CONTA, ST_CONTA_FIXA
    FROM dfp.Bpa
    UNION ALL
    SELECT 'BPP', CNPJ_CIA, CnpjNum, CD_CVM, DENOM_CIA, DT_REFER, VERSAO,
           GRUPO_DFP, MOEDA, ESCALA_MOEDA, ORDEM_EXERC, CAST(NULL AS DATE), DT_FIM_EXERC,
           CAST(NULL AS VARCHAR(60)), CD_CONTA, DS_CONTA, VL_CONTA, ST_CONTA_FIXA
    FROM dfp.Bpp
    UNION ALL
    SELECT 'DRA', CNPJ_CIA, CnpjNum, CD_CVM, DENOM_CIA, DT_REFER, VERSAO,
           GRUPO_DFP, MOEDA, ESCALA_MOEDA, ORDEM_EXERC, DT_INI_EXERC, DT_FIM_EXERC,
           CAST(NULL AS VARCHAR(60)), CD_CONTA, DS_CONTA, VL_CONTA, ST_CONTA_FIXA
    FROM dfp.Dra
    UNION ALL
    SELECT 'DVA', CNPJ_CIA, CnpjNum, CD_CVM, DENOM_CIA, DT_REFER, VERSAO,
           GRUPO_DFP, MOEDA, ESCALA_MOEDA, ORDEM_EXERC, DT_INI_EXERC, DT_FIM_EXERC,
           CAST(NULL AS VARCHAR(60)), CD_CONTA, DS_CONTA, VL_CONTA, ST_CONTA_FIXA
    FROM dfp.Dva
    UNION ALL
    SELECT 'DFC_MD', CNPJ_CIA, CnpjNum, CD_CVM, DENOM_CIA, DT_REFER, VERSAO,
           GRUPO_DFP, MOEDA, ESCALA_MOEDA, ORDEM_EXERC, DT_INI_EXERC, DT_FIM_EXERC,
           CAST(NULL AS VARCHAR(60)), CD_CONTA, DS_CONTA, VL_CONTA, ST_CONTA_FIXA
    FROM dfp.DfcMd
    UNION ALL
    SELECT 'DFC_MI', CNPJ_CIA, CnpjNum, CD_CVM, DENOM_CIA, DT_REFER, VERSAO,
           GRUPO_DFP, MOEDA, ESCALA_MOEDA, ORDEM_EXERC, DT_INI_EXERC, DT_FIM_EXERC,
           CAST(NULL AS VARCHAR(60)), CD_CONTA, DS_CONTA, VL_CONTA, ST_CONTA_FIXA
    FROM dfp.DfcMi
    UNION ALL
    SELECT 'DMPL', CNPJ_CIA, CnpjNum, CD_CVM, DENOM_CIA, DT_REFER, VERSAO,
           GRUPO_DFP, MOEDA, ESCALA_MOEDA, ORDEM_EXERC, DT_INI_EXERC, DT_FIM_EXERC,
           COLUNA_DF, CD_CONTA, DS_CONTA, VL_CONTA, ST_CONTA_FIXA
    FROM dfp.Dmpl;
GO

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
        (SELECT COUNT(*) FROM dfp.Bpa  x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO) AS QtBpa,
        (SELECT COUNT(*) FROM dfp.Bpp  x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO) AS QtBpp,
        (SELECT COUNT(*) FROM dfp.Dre  x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO) AS QtDre,
        (SELECT COUNT(*) FROM dfp.Dra  x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO) AS QtDra,
        (SELECT COUNT(*) FROM dfp.Dva  x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO) AS QtDva,
        (SELECT COUNT(*) FROM dfp.DfcMd x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO) AS QtDfcMd,
        (SELECT COUNT(*) FROM dfp.DfcMi x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO) AS QtDfcMi,
        (SELECT COUNT(*) FROM dfp.Dmpl x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO) AS QtDmpl,
        (SELECT COUNT(*) FROM dfp.ComposicaoCapital x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO) AS QtComposicaoCapital,
        (SELECT COUNT(*) FROM dfp.Parecer x WHERE x.CnpjNum = d.CnpjNum AND x.DT_REFER = d.DT_REFER AND x.VERSAO = d.VERSAO) AS QtParecer
    FROM dfp.Documento d;
GO
