-- V013: cria o schema [fre] (Formulario de Referencia / CVM), o ledger fre.Importacao
-- (espelha dfp.Importacao / itr.Importacao) e a tabela indice fre.Documento.
-- As tabelas dos demais modelos FRE sao criadas em V014-V018. Idempotente; lotes por GO.

IF SCHEMA_ID('fre') IS NULL
    EXEC('CREATE SCHEMA fre');
GO

IF OBJECT_ID('fre.Importacao', 'U') IS NULL
BEGIN
    CREATE TABLE fre.Importacao(
        Id               BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_Importacao PRIMARY KEY,
        Tipo             VARCHAR(60)   NOT NULL,                 -- Chave de API do modelo FRE
        Tabela           VARCHAR(80)   NOT NULL,                 -- Tabela destino (fre.*)
        Conjunto         CHAR(3)       NULL,                     -- Sempre NULL na base FRE (sem GRUPO_DFP)
        Ano              SMALLINT      NULL,                     -- Ano de referencia (do nome do arquivo)
        Arquivo          NVARCHAR(260) NULL,                     -- Nome do arquivo importado
        LinhasLidas      INT           NOT NULL CONSTRAINT DF_Fre_Importacao_Lidas      DEFAULT 0,
        LinhasImportadas INT           NOT NULL CONSTRAINT DF_Fre_Importacao_Importadas DEFAULT 0,
        LinhasRemovidas  INT           NOT NULL CONSTRAINT DF_Fre_Importacao_Removidas  DEFAULT 0,
        Status           VARCHAR(20)   NOT NULL CONSTRAINT DF_Fre_Importacao_Status     DEFAULT 'Processando',
        Mensagem         NVARCHAR(1000) NULL,
        Usuario          NVARCHAR(128) NULL,
        IniciadoEmUtc    DATETIME2     NOT NULL CONSTRAINT DF_Fre_Importacao_Iniciado   DEFAULT SYSUTCDATETIME(),
        ConcluidoEmUtc   DATETIME2     NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_Importacao_Escopo' AND object_id = OBJECT_ID('fre.Importacao'))
    CREATE INDEX IX_Fre_Importacao_Escopo ON fre.Importacao(Tipo, Ano);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_Importacao_Iniciado' AND object_id = OBJECT_ID('fre.Importacao'))
    CREATE INDEX IX_Fre_Importacao_Iniciado ON fre.Importacao(IniciadoEmUtc DESC);
GO

-- Indice de documentos FRE entregues (modelo principal fre_cia_aberta_AAAA.csv).
IF OBJECT_ID('fre.Documento', 'U') IS NULL
BEGIN
    CREATE TABLE fre.Documento(
        Id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_Documento PRIMARY KEY,
        CATEG_DOC      VARCHAR(20) NULL,
        CD_CVM         CHAR(6) NULL,
        CNPJ_CIA       VARCHAR(20) NULL,
        DENOM_CIA      VARCHAR(100) NULL,
        DT_RECEB       DATE NULL,
        DT_REFER       DATE NULL,
        ID_DOC         INT NULL,
        LINK_DOC       VARCHAR(121) NULL,
        VERSAO         SMALLINT NULL,
        Ano            SMALLINT       NULL,
        ImportacaoId   BIGINT         NULL,
        ArquivoOrigem  NVARCHAR(260)  NULL,
        CnpjNum        AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_CIA, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc DATETIME2      NOT NULL CONSTRAINT DF_Fre_Documento_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_Documento_CnpjAno' AND object_id = OBJECT_ID('fre.Documento'))
    CREATE INDEX IX_Fre_Documento_CnpjAno ON fre.Documento(CnpjNum, Ano);
GO

