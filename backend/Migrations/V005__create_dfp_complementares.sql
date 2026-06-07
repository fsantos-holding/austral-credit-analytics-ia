-- V005: demonstracoes complementares da DFP (espelham os metadados da CVM 1:1):
--   ComposicaoCapital - meta_dfp_cia_aberta_composicao_capital.txt
--                       (quantidades de acoes ordinarias/preferenciais em tesouraria
--                        e capital integralizado; sem CD_CVM no layout)
--   Parecer           - meta_dfp_cia_aberta_parecer.txt
--                       (texto do parecer/declaracao do auditor, uma linha por item)
-- Ambas tem CnpjNum (calculada, persistida) para rastreio por CNPJ.
-- Idempotente. Lotes separados por GO.

IF SCHEMA_ID('dfp') IS NULL
    EXEC('CREATE SCHEMA dfp');
GO

-- ============================ Composicao do Capital ======================================
IF OBJECT_ID('dfp.ComposicaoCapital', 'U') IS NULL
BEGIN
    CREATE TABLE dfp.ComposicaoCapital(
        Id                       BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Dfp_ComposicaoCapital PRIMARY KEY,
        CNPJ_CIA                 VARCHAR(20) NOT NULL,
        DENOM_CIA                VARCHAR(100) NULL,
        DT_REFER                 DATE         NOT NULL,
        VERSAO                   SMALLINT     NOT NULL,
        QT_ACAO_ORDIN_CAP_INTEGR BIGINT       NULL,             -- Acoes ordinarias - capital integralizado
        QT_ACAO_PREF_CAP_INTEGR  BIGINT       NULL,             -- Acoes preferenciais - capital integralizado
        QT_ACAO_ORDIN_TESOURO    BIGINT       NULL,             -- Acoes ordinarias em tesouraria
        QT_ACAO_PREF_TESOURO     BIGINT       NULL,             -- Acoes preferenciais em tesouraria
        QT_ACAO_TOTAL_CAP_INTEGR BIGINT       NULL,             -- Total de acoes - capital integralizado
        QT_ACAO_TOTAL_TESOURO    BIGINT       NULL,             -- Total de acoes em tesouraria
        CnpjNum AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_CIA, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ArquivoOrigem            NVARCHAR(260) NULL,
        ImportadoEmUtc           DATETIME2     NOT NULL CONSTRAINT DF_Dfp_ComposicaoCapital_ImportadoEm DEFAULT SYSUTCDATETIME()
    );
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ComposicaoCapital_CnpjNum' AND object_id = OBJECT_ID('dfp.ComposicaoCapital'))
    CREATE INDEX IX_ComposicaoCapital_CnpjNum ON dfp.ComposicaoCapital(CnpjNum, DT_REFER, VERSAO);
GO

-- ================================== Parecer / Declaracao =================================
IF OBJECT_ID('dfp.Parecer', 'U') IS NULL
BEGIN
    CREATE TABLE dfp.Parecer(
        Id                   BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Dfp_Parecer PRIMARY KEY,
        CNPJ_CIA             VARCHAR(20)   NOT NULL,
        DENOM_CIA            VARCHAR(100)  NULL,
        DT_REFER             DATE          NOT NULL,
        VERSAO               SMALLINT      NOT NULL,
        TP_PARECER_DECL      VARCHAR(101)  NULL,                -- Tipo do Parecer/Declaracao
        TP_RELAT_AUD         VARCHAR(19)   NULL,                -- Tipo do relatorio do auditor independente
        NUM_ITEM_PARECER_DECL SMALLINT     NULL,                -- Numero da linha do texto
        TXT_PARECER_DECL     VARCHAR(8000) NULL,                -- Texto do Parecer/Declaracao
        CnpjNum AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_CIA, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ArquivoOrigem        NVARCHAR(260) NULL,
        ImportadoEmUtc       DATETIME2     NOT NULL CONSTRAINT DF_Dfp_Parecer_ImportadoEm DEFAULT SYSUTCDATETIME()
    );
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Parecer_CnpjNum' AND object_id = OBJECT_ID('dfp.Parecer'))
    CREATE INDEX IX_Parecer_CnpjNum ON dfp.Parecer(CnpjNum, DT_REFER, VERSAO);
GO
