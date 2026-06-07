-- V009: cria o schema [itr] (Informacoes Trimestrais / CVM), isolado do schema [dfp].
-- A DRE trimestral (jan-mar, abr-jun, jul-set) vem dos arquivos ITR; o schema [itr]
-- espelha o layout nativo da CVM da DRE (igual a dfp.Dre) para permitir carga direta
-- (SqlBulkCopy) a partir do CSV "itr_cia_aberta_DRE_*.csv". O schema ja identifica a
-- origem (ITR), portanto nao ha coluna Origem.
--   itr.Dre        - uma linha por conta/exercicio do arquivo trimestral da CVM.
--   itr.Importacao - ledger proprio de importacoes ITR (espelha dfp.Importacao).
-- Idempotente: cada objeto so e criado se ainda nao existir. Lotes separados por GO.

IF SCHEMA_ID('itr') IS NULL
    EXEC('CREATE SCHEMA itr');
GO

-- DRE trimestral: mesmas colunas nativas da CVM de dfp.Dre + auditoria da carga.
IF OBJECT_ID('itr.Dre', 'U') IS NULL
BEGIN
    CREATE TABLE itr.Dre(
        Id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Itr_Dre PRIMARY KEY,

        -- Identificacao da companhia / documento.
        CNPJ_CIA       VARCHAR(20)    NOT NULL,                 -- CNPJ da companhia
        CD_CVM         CHAR(6)        NOT NULL,                 -- Codigo CVM
        DENOM_CIA      VARCHAR(100)   NULL,                     -- Nome empresarial da companhia
        DT_REFER       DATE           NOT NULL,                 -- Data de referencia do documento
        VERSAO         SMALLINT       NOT NULL,                 -- Versao do documento

        -- Classificacao da demonstracao.
        GRUPO_DFP      VARCHAR(206)   NULL,                     -- Nome e nivel de agregacao da demonstracao
        MOEDA          VARCHAR(100)   NULL,                     -- Moeda
        ESCALA_MOEDA   VARCHAR(100)   NULL,                     -- Escala monetaria
        ORDEM_EXERC    VARCHAR(9)     NOT NULL,                 -- Ordem do exercicio social (ULTIMO/PENULTIMO)
        DT_INI_EXERC   DATE           NULL,                     -- Data inicio do exercicio social
        DT_FIM_EXERC   DATE           NULL,                     -- Data fim do exercicio social

        -- Conta e valor.
        CD_CONTA       VARCHAR(18)    NOT NULL,                 -- Codigo da conta
        DS_CONTA       VARCHAR(100)   NULL,                     -- Descricao da conta
        VL_CONTA       DECIMAL(29,10) NULL,                     -- Valor da conta (acumulado ate DT_FIM_EXERC)
        ST_CONTA_FIXA  VARCHAR(1)     NULL,                     -- Indica se e conta fixa (S/N)

        -- Auditoria da carga (controle interno, fora do layout da CVM).
        Conjunto       CHAR(3)        NULL,                     -- 'CON'/'IND' (derivado do GRUPO_DFP)
        Ano            SMALLINT       NULL,                     -- Ano de referencia (do nome do arquivo)
        ImportacaoId   BIGINT         NULL,                     -- Aponta para itr.Importacao
        ArquivoOrigem  NVARCHAR(260)  NULL,
        CnpjNum AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_CIA, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc DATETIME2      NOT NULL CONSTRAINT DF_Itr_Dre_ImportadoEm DEFAULT SYSUTCDATETIME()
    );
END
GO

-- Busca por companhia (seletor de empresa + leitura por CNPJ).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Itr_Dre_CnpjNum' AND object_id = OBJECT_ID('itr.Dre'))
    CREATE INDEX IX_Itr_Dre_CnpjNum ON itr.Dre(CnpjNum, DT_REFER, VERSAO) INCLUDE (CD_CONTA, VL_CONTA);
GO

-- Indice do motor de trimestralizacao (dedup MAX(VERSAO) + de-acumulacao por conta/periodo).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Itr_Dre_Motor' AND object_id = OBJECT_ID('itr.Dre'))
    CREATE INDEX IX_Itr_Dre_Motor ON itr.Dre(CD_CVM, Conjunto, ORDEM_EXERC, CD_CONTA, DT_FIM_EXERC) INCLUDE (VERSAO, VL_CONTA);
GO

-- Escopo do DELETE de reimportacao = (Ano, Conjunto).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Itr_Dre_AnoConjunto' AND object_id = OBJECT_ID('itr.Dre'))
    CREATE INDEX IX_Itr_Dre_AnoConjunto ON itr.Dre(Ano, Conjunto);
GO

-- ============================ Ledger proprio de importacoes ITR ============================
IF OBJECT_ID('itr.Importacao', 'U') IS NULL
BEGIN
    CREATE TABLE itr.Importacao(
        Id               BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Itr_Importacao PRIMARY KEY,
        Tipo             VARCHAR(30)   NOT NULL,                 -- Chave de API da demonstracao (DRE)
        Tabela           VARCHAR(60)   NOT NULL,                 -- Tabela destino (itr.Dre)
        Conjunto         CHAR(3)       NULL,                     -- 'CON'/'IND'/'MIS' (misto) ou NULL
        Ano              SMALLINT      NULL,                     -- Ano de referencia (do nome do arquivo)
        Arquivo          NVARCHAR(260) NULL,                     -- Nome do arquivo importado
        LinhasLidas      INT           NOT NULL CONSTRAINT DF_Itr_Importacao_Lidas      DEFAULT 0,
        LinhasImportadas INT           NOT NULL CONSTRAINT DF_Itr_Importacao_Importadas DEFAULT 0,
        LinhasRemovidas  INT           NOT NULL CONSTRAINT DF_Itr_Importacao_Removidas  DEFAULT 0,
        Status           VARCHAR(20)   NOT NULL CONSTRAINT DF_Itr_Importacao_Status     DEFAULT 'Processando',
        Mensagem         NVARCHAR(1000) NULL,
        Usuario          NVARCHAR(128) NULL,                     -- Identidade que iniciou a importacao
        IniciadoEmUtc    DATETIME2     NOT NULL CONSTRAINT DF_Itr_Importacao_Iniciado   DEFAULT SYSUTCDATETIME(),
        ConcluidoEmUtc   DATETIME2     NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Itr_Importacao_Escopo' AND object_id = OBJECT_ID('itr.Importacao'))
    CREATE INDEX IX_Itr_Importacao_Escopo ON itr.Importacao(Tipo, Conjunto, Ano);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Itr_Importacao_Iniciado' AND object_id = OBJECT_ID('itr.Importacao'))
    CREATE INDEX IX_Itr_Importacao_Iniciado ON itr.Importacao(IniciadoEmUtc DESC);
GO
