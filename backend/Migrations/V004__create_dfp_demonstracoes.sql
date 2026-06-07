-- V004: demonstracoes contabeis por conta (espelham os metadados da CVM 1:1, no
-- mesmo padrao de dfp.Dre):
--   BPA    - Balanco Patrimonial Ativo            (so DT_FIM_EXERC)
--   BPP    - Balanco Patrimonial Passivo          (so DT_FIM_EXERC)
--   DRA    - Demonstracao do Resultado Abrangente (DT_INI + DT_FIM)
--   DVA    - Demonstracao do Valor Adicionado     (DT_INI + DT_FIM)
--   DFC_MD - Fluxo de Caixa (Metodo Direto)       (DT_INI + DT_FIM)
--   DFC_MI - Fluxo de Caixa (Metodo Indireto)     (DT_INI + DT_FIM)
--   DMPL   - Mutacoes do Patrimonio Liquido       (DT_INI + DT_FIM + COLUNA_DF)
-- Cada tabela tem CnpjNum (calculada, persistida) para rastreio por CNPJ.
-- Idempotente. Lotes separados por GO.

IF SCHEMA_ID('dfp') IS NULL
    EXEC('CREATE SCHEMA dfp');
GO

-- Alinha a tabela dfp.Dre (criada na V002) ao padrao de rastreio por CNPJ.
IF OBJECT_ID('dfp.Dre', 'U') IS NOT NULL
   AND COL_LENGTH('dfp.Dre', 'CnpjNum') IS NULL
    EXEC('ALTER TABLE dfp.Dre ADD CnpjNum AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_CIA, ''.'', ''''), ''/'', ''''), ''-'', ''''), '' '', '''')) PERSISTED');
GO

IF OBJECT_ID('dfp.Dre', 'U') IS NOT NULL
   AND COL_LENGTH('dfp.Dre', 'CnpjNum') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dre_CnpjNum' AND object_id = OBJECT_ID('dfp.Dre'))
    CREATE INDEX IX_Dre_CnpjNum ON dfp.Dre(CnpjNum, DT_REFER, VERSAO) INCLUDE (CD_CONTA, VL_CONTA);
GO

-- ============================ Balanco Patrimonial Ativo (BPA) ============================
IF OBJECT_ID('dfp.Bpa', 'U') IS NULL
BEGIN
    CREATE TABLE dfp.Bpa(
        Id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Dfp_Bpa PRIMARY KEY,
        CNPJ_CIA       VARCHAR(20)    NOT NULL,
        CD_CVM         CHAR(6)        NOT NULL,
        DENOM_CIA      VARCHAR(100)   NULL,
        DT_REFER       DATE           NOT NULL,
        VERSAO         SMALLINT       NOT NULL,
        GRUPO_DFP      VARCHAR(206)   NULL,
        MOEDA          VARCHAR(100)   NULL,
        ESCALA_MOEDA   VARCHAR(100)   NULL,
        ORDEM_EXERC    VARCHAR(9)     NOT NULL,
        DT_FIM_EXERC   DATE           NULL,
        CD_CONTA       VARCHAR(18)    NOT NULL,
        DS_CONTA       VARCHAR(100)   NULL,
        VL_CONTA       DECIMAL(29,10) NULL,
        ST_CONTA_FIXA  VARCHAR(1)     NULL,
        CnpjNum AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_CIA, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ArquivoOrigem  NVARCHAR(260)  NULL,
        ImportadoEmUtc DATETIME2      NOT NULL CONSTRAINT DF_Dfp_Bpa_ImportadoEm DEFAULT SYSUTCDATETIME()
    );
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bpa_CnpjNum' AND object_id = OBJECT_ID('dfp.Bpa'))
    CREATE INDEX IX_Bpa_CnpjNum ON dfp.Bpa(CnpjNum, DT_REFER, VERSAO) INCLUDE (CD_CONTA, VL_CONTA);
GO

-- ============================ Balanco Patrimonial Passivo (BPP) ===========================
IF OBJECT_ID('dfp.Bpp', 'U') IS NULL
BEGIN
    CREATE TABLE dfp.Bpp(
        Id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Dfp_Bpp PRIMARY KEY,
        CNPJ_CIA       VARCHAR(20)    NOT NULL,
        CD_CVM         CHAR(6)        NOT NULL,
        DENOM_CIA      VARCHAR(100)   NULL,
        DT_REFER       DATE           NOT NULL,
        VERSAO         SMALLINT       NOT NULL,
        GRUPO_DFP      VARCHAR(206)   NULL,
        MOEDA          VARCHAR(100)   NULL,
        ESCALA_MOEDA   VARCHAR(100)   NULL,
        ORDEM_EXERC    VARCHAR(9)     NOT NULL,
        DT_FIM_EXERC   DATE           NULL,
        CD_CONTA       VARCHAR(18)    NOT NULL,
        DS_CONTA       VARCHAR(100)   NULL,
        VL_CONTA       DECIMAL(29,10) NULL,
        ST_CONTA_FIXA  VARCHAR(1)     NULL,
        CnpjNum AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_CIA, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ArquivoOrigem  NVARCHAR(260)  NULL,
        ImportadoEmUtc DATETIME2      NOT NULL CONSTRAINT DF_Dfp_Bpp_ImportadoEm DEFAULT SYSUTCDATETIME()
    );
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bpp_CnpjNum' AND object_id = OBJECT_ID('dfp.Bpp'))
    CREATE INDEX IX_Bpp_CnpjNum ON dfp.Bpp(CnpjNum, DT_REFER, VERSAO) INCLUDE (CD_CONTA, VL_CONTA);
GO

-- ====================== Demonstracao do Resultado Abrangente (DRA) ========================
IF OBJECT_ID('dfp.Dra', 'U') IS NULL
BEGIN
    CREATE TABLE dfp.Dra(
        Id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Dfp_Dra PRIMARY KEY,
        CNPJ_CIA       VARCHAR(20)    NOT NULL,
        CD_CVM         CHAR(6)        NOT NULL,
        DENOM_CIA      VARCHAR(100)   NULL,
        DT_REFER       DATE           NOT NULL,
        VERSAO         SMALLINT       NOT NULL,
        GRUPO_DFP      VARCHAR(206)   NULL,
        MOEDA          VARCHAR(100)   NULL,
        ESCALA_MOEDA   VARCHAR(100)   NULL,
        ORDEM_EXERC    VARCHAR(9)     NOT NULL,
        DT_INI_EXERC   DATE           NULL,
        DT_FIM_EXERC   DATE           NULL,
        CD_CONTA       VARCHAR(18)    NOT NULL,
        DS_CONTA       VARCHAR(100)   NULL,
        VL_CONTA       DECIMAL(29,10) NULL,
        ST_CONTA_FIXA  VARCHAR(1)     NULL,
        CnpjNum AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_CIA, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ArquivoOrigem  NVARCHAR(260)  NULL,
        ImportadoEmUtc DATETIME2      NOT NULL CONSTRAINT DF_Dfp_Dra_ImportadoEm DEFAULT SYSUTCDATETIME()
    );
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dra_CnpjNum' AND object_id = OBJECT_ID('dfp.Dra'))
    CREATE INDEX IX_Dra_CnpjNum ON dfp.Dra(CnpjNum, DT_REFER, VERSAO) INCLUDE (CD_CONTA, VL_CONTA);
GO

-- ======================== Demonstracao do Valor Adicionado (DVA) ==========================
IF OBJECT_ID('dfp.Dva', 'U') IS NULL
BEGIN
    CREATE TABLE dfp.Dva(
        Id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Dfp_Dva PRIMARY KEY,
        CNPJ_CIA       VARCHAR(20)    NOT NULL,
        CD_CVM         CHAR(6)        NOT NULL,
        DENOM_CIA      VARCHAR(100)   NULL,
        DT_REFER       DATE           NOT NULL,
        VERSAO         SMALLINT       NOT NULL,
        GRUPO_DFP      VARCHAR(206)   NULL,
        MOEDA          VARCHAR(100)   NULL,
        ESCALA_MOEDA   VARCHAR(100)   NULL,
        ORDEM_EXERC    VARCHAR(9)     NOT NULL,
        DT_INI_EXERC   DATE           NULL,
        DT_FIM_EXERC   DATE           NULL,
        CD_CONTA       VARCHAR(18)    NOT NULL,
        DS_CONTA       VARCHAR(100)   NULL,
        VL_CONTA       DECIMAL(29,10) NULL,
        ST_CONTA_FIXA  VARCHAR(1)     NULL,
        CnpjNum AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_CIA, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ArquivoOrigem  NVARCHAR(260)  NULL,
        ImportadoEmUtc DATETIME2      NOT NULL CONSTRAINT DF_Dfp_Dva_ImportadoEm DEFAULT SYSUTCDATETIME()
    );
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dva_CnpjNum' AND object_id = OBJECT_ID('dfp.Dva'))
    CREATE INDEX IX_Dva_CnpjNum ON dfp.Dva(CnpjNum, DT_REFER, VERSAO) INCLUDE (CD_CONTA, VL_CONTA);
GO

-- ===================== Fluxo de Caixa - Metodo Direto (DFC_MD) ============================
IF OBJECT_ID('dfp.DfcMd', 'U') IS NULL
BEGIN
    CREATE TABLE dfp.DfcMd(
        Id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Dfp_DfcMd PRIMARY KEY,
        CNPJ_CIA       VARCHAR(20)    NOT NULL,
        CD_CVM         CHAR(6)        NOT NULL,
        DENOM_CIA      VARCHAR(100)   NULL,
        DT_REFER       DATE           NOT NULL,
        VERSAO         SMALLINT       NOT NULL,
        GRUPO_DFP      VARCHAR(206)   NULL,
        MOEDA          VARCHAR(100)   NULL,
        ESCALA_MOEDA   VARCHAR(100)   NULL,
        ORDEM_EXERC    VARCHAR(9)     NOT NULL,
        DT_INI_EXERC   DATE           NULL,
        DT_FIM_EXERC   DATE           NULL,
        CD_CONTA       VARCHAR(18)    NOT NULL,
        DS_CONTA       VARCHAR(100)   NULL,
        VL_CONTA       DECIMAL(29,10) NULL,
        ST_CONTA_FIXA  VARCHAR(1)     NULL,
        CnpjNum AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_CIA, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ArquivoOrigem  NVARCHAR(260)  NULL,
        ImportadoEmUtc DATETIME2      NOT NULL CONSTRAINT DF_Dfp_DfcMd_ImportadoEm DEFAULT SYSUTCDATETIME()
    );
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DfcMd_CnpjNum' AND object_id = OBJECT_ID('dfp.DfcMd'))
    CREATE INDEX IX_DfcMd_CnpjNum ON dfp.DfcMd(CnpjNum, DT_REFER, VERSAO) INCLUDE (CD_CONTA, VL_CONTA);
GO

-- ==================== Fluxo de Caixa - Metodo Indireto (DFC_MI) ===========================
IF OBJECT_ID('dfp.DfcMi', 'U') IS NULL
BEGIN
    CREATE TABLE dfp.DfcMi(
        Id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Dfp_DfcMi PRIMARY KEY,
        CNPJ_CIA       VARCHAR(20)    NOT NULL,
        CD_CVM         CHAR(6)        NOT NULL,
        DENOM_CIA      VARCHAR(100)   NULL,
        DT_REFER       DATE           NOT NULL,
        VERSAO         SMALLINT       NOT NULL,
        GRUPO_DFP      VARCHAR(206)   NULL,
        MOEDA          VARCHAR(100)   NULL,
        ESCALA_MOEDA   VARCHAR(100)   NULL,
        ORDEM_EXERC    VARCHAR(9)     NOT NULL,
        DT_INI_EXERC   DATE           NULL,
        DT_FIM_EXERC   DATE           NULL,
        CD_CONTA       VARCHAR(18)    NOT NULL,
        DS_CONTA       VARCHAR(100)   NULL,
        VL_CONTA       DECIMAL(29,10) NULL,
        ST_CONTA_FIXA  VARCHAR(1)     NULL,
        CnpjNum AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_CIA, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ArquivoOrigem  NVARCHAR(260)  NULL,
        ImportadoEmUtc DATETIME2      NOT NULL CONSTRAINT DF_Dfp_DfcMi_ImportadoEm DEFAULT SYSUTCDATETIME()
    );
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DfcMi_CnpjNum' AND object_id = OBJECT_ID('dfp.DfcMi'))
    CREATE INDEX IX_DfcMi_CnpjNum ON dfp.DfcMi(CnpjNum, DT_REFER, VERSAO) INCLUDE (CD_CONTA, VL_CONTA);
GO

-- ================= Mutacoes do Patrimonio Liquido (DMPL, com COLUNA_DF) ===================
IF OBJECT_ID('dfp.Dmpl', 'U') IS NULL
BEGIN
    CREATE TABLE dfp.Dmpl(
        Id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Dfp_Dmpl PRIMARY KEY,
        CNPJ_CIA       VARCHAR(20)    NOT NULL,
        CD_CVM         CHAR(6)        NOT NULL,
        DENOM_CIA      VARCHAR(100)   NULL,
        DT_REFER       DATE           NOT NULL,
        VERSAO         SMALLINT       NOT NULL,
        GRUPO_DFP      VARCHAR(206)   NULL,
        MOEDA          VARCHAR(100)   NULL,
        ESCALA_MOEDA   VARCHAR(100)   NULL,
        ORDEM_EXERC    VARCHAR(9)     NOT NULL,
        DT_INI_EXERC   DATE           NULL,
        DT_FIM_EXERC   DATE           NULL,
        COLUNA_DF      VARCHAR(60)    NULL,                     -- Coluna da demonstracao financeira (DMPL)
        CD_CONTA       VARCHAR(18)    NOT NULL,
        DS_CONTA       VARCHAR(100)   NULL,
        VL_CONTA       DECIMAL(29,10) NULL,
        ST_CONTA_FIXA  VARCHAR(1)     NULL,
        CnpjNum AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_CIA, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ArquivoOrigem  NVARCHAR(260)  NULL,
        ImportadoEmUtc DATETIME2      NOT NULL CONSTRAINT DF_Dfp_Dmpl_ImportadoEm DEFAULT SYSUTCDATETIME()
    );
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dmpl_CnpjNum' AND object_id = OBJECT_ID('dfp.Dmpl'))
    CREATE INDEX IX_Dmpl_CnpjNum ON dfp.Dmpl(CnpjNum, DT_REFER, VERSAO) INCLUDE (CD_CONTA, VL_CONTA);
GO
