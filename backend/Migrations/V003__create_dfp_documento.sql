-- V003: tabela-indice dos documentos DFP (espelha meta_dfp_cia_aberta.txt /
-- arquivo "dfp_cia_aberta_YYYY.csv"). E o ponto de entrada do rastreio: a partir
-- de um CNPJ encontram-se todos os documentos (CD_CVM, DT_REFER, VERSAO) que
-- amarram as demais demonstracoes (BPA, BPP, DRE, DFC, ...).
-- Idempotente. Lotes separados por GO (o runner divide e executa um por vez).

IF SCHEMA_ID('dfp') IS NULL
    EXEC('CREATE SCHEMA dfp');
GO

-- Documento: uma linha por documento entregue pela companhia.
-- Colunas nativas mantem nome/dominio do metadado para carga direta do CSV.
-- CnpjNum e uma coluna calculada (somente digitos) para rastreio rapido por CNPJ,
-- independentemente da formatacao (00.000.000/0001-00 vs 00000000000100).
IF OBJECT_ID('dfp.Documento', 'U') IS NULL
BEGIN
    CREATE TABLE dfp.Documento(
        Id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Dfp_Documento PRIMARY KEY,

        CNPJ_CIA       VARCHAR(20)    NOT NULL,                 -- CNPJ da companhia
        CD_CVM         CHAR(6)        NULL,                     -- Codigo CVM
        DENOM_CIA      VARCHAR(100)   NULL,                     -- Nome empresarial da companhia
        CATEG_DOC      VARCHAR(20)    NULL,                     -- Categoria do documento
        DT_REFER       DATE           NOT NULL,                 -- Data de referencia do documento
        DT_RECEB       DATE           NULL,                     -- Data de recebimento do documento
        VERSAO         SMALLINT       NOT NULL,                 -- Versao do documento
        ID_DOC         INT            NULL,                     -- Identificador do documento (CVM)
        LINK_DOC       VARCHAR(121)   NULL,                     -- Endereco para download do documento

        -- CNPJ somente digitos (rastreio por CNPJ sem depender da formatacao).
        CnpjNum AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_CIA, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,

        -- Auditoria da carga (controle interno, fora do layout da CVM).
        ArquivoOrigem  NVARCHAR(260)  NULL,
        ImportadoEmUtc DATETIME2      NOT NULL CONSTRAINT DF_Dfp_Documento_ImportadoEm DEFAULT SYSUTCDATETIME()
    );
END
GO

-- Rastreio por CNPJ (entrada principal da aplicacao).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dfp_Documento_Cnpj' AND object_id = OBJECT_ID('dfp.Documento'))
    CREATE INDEX IX_Dfp_Documento_Cnpj ON dfp.Documento(CnpjNum, DT_REFER, VERSAO) INCLUDE (CD_CVM, CATEG_DOC, DENOM_CIA);
GO

-- Busca por codigo CVM + periodo.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dfp_Documento_Cvm' AND object_id = OBJECT_ID('dfp.Documento'))
    CREATE INDEX IX_Dfp_Documento_Cvm ON dfp.Documento(CD_CVM, DT_REFER, VERSAO);
GO
