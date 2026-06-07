-- V008: ledger de importacoes DFP. Uma linha por importacao de CSV (qualquer tipo),
-- registrando o escopo carregado (Tipo/Tabela/Conjunto/Ano/Arquivo), as contagens
-- (lidas/importadas/removidas), o status e o periodo de execucao. Cada linha de dados
-- das tabelas dfp.* referencia esta tabela via ImportacaoId (V007), permitindo rastrear
-- a origem de cada movimento importado.
-- Idempotente. Lotes separados por GO.

IF SCHEMA_ID('dfp') IS NULL
    EXEC('CREATE SCHEMA dfp');
GO

IF OBJECT_ID('dfp.Importacao', 'U') IS NULL
BEGIN
    CREATE TABLE dfp.Importacao(
        Id               BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Dfp_Importacao PRIMARY KEY,
        Tipo             VARCHAR(30)   NOT NULL,                 -- Chave de API da demonstracao (DRE, BPA, ...)
        Tabela           VARCHAR(60)   NOT NULL,                 -- Tabela destino (dfp.Dre, ...)
        Conjunto         CHAR(3)       NULL,                     -- 'CON'/'IND'/'MIS' (misto) ou NULL (sem GRUPO_DFP)
        Ano              SMALLINT      NULL,                     -- Ano de referencia (do nome do arquivo)
        Arquivo          NVARCHAR(260) NULL,                     -- Nome do arquivo importado
        LinhasLidas      INT           NOT NULL CONSTRAINT DF_Dfp_Importacao_Lidas      DEFAULT 0,
        LinhasImportadas INT           NOT NULL CONSTRAINT DF_Dfp_Importacao_Importadas DEFAULT 0,
        LinhasRemovidas  INT           NOT NULL CONSTRAINT DF_Dfp_Importacao_Removidas  DEFAULT 0,
        Status           VARCHAR(20)   NOT NULL CONSTRAINT DF_Dfp_Importacao_Status     DEFAULT 'Processando',
        Mensagem         NVARCHAR(1000) NULL,
        Usuario          NVARCHAR(128) NULL,                     -- Identidade que iniciou a importacao
        IniciadoEmUtc    DATETIME2     NOT NULL CONSTRAINT DF_Dfp_Importacao_Iniciado   DEFAULT SYSUTCDATETIME(),
        ConcluidoEmUtc   DATETIME2     NULL
    );
END
GO

-- Lookup do escopo (limpeza/rastreio por tipo + conjunto + ano).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dfp_Importacao_Escopo' AND object_id = OBJECT_ID('dfp.Importacao'))
    CREATE INDEX IX_Dfp_Importacao_Escopo ON dfp.Importacao(Tipo, Conjunto, Ano);
GO

-- Historico mais recente primeiro.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dfp_Importacao_Iniciado' AND object_id = OBJECT_ID('dfp.Importacao'))
    CREATE INDEX IX_Dfp_Importacao_Iniciado ON dfp.Importacao(IniciadoEmUtc DESC);
GO
