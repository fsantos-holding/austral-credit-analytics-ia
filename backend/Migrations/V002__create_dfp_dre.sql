-- V002: cria o schema [dfp] (Demonstracoes Financeiras Padronizadas / CVM) e a
-- tabela de importacao da DRE (Demonstracao do Resultado do Exercicio).
-- A estrutura espelha 1:1 os metadados do arquivo da CVM
-- (meta_dfp_cia_aberta_DRE.txt): nomes, tipos e tamanhos das colunas sao
-- preservados para permitir carga direta (BULK INSERT / SqlBulkCopy) a partir
-- do CSV "dfp_cia_aberta_DRE_*.csv".
-- Idempotente: cada objeto so e criado se ainda nao existir.
-- Lotes separados por GO (o runner divide e executa um por vez).

IF SCHEMA_ID('dfp') IS NULL
    EXEC('CREATE SCHEMA dfp');
GO

-- DRE: uma linha por conta/exercicio do arquivo da CVM.
-- Colunas nativas (CNPJ_CIA, DT_REFER, ...) mantem o nome e o dominio do metadado
-- para que a importacao mapeie diretamente o cabecalho do CSV.
IF OBJECT_ID('dfp.Dre', 'U') IS NULL
BEGIN
    CREATE TABLE dfp.Dre(
        Id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Dre PRIMARY KEY,

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
        VL_CONTA       DECIMAL(29,10) NULL,                     -- Valor da conta
        ST_CONTA_FIXA  VARCHAR(1)     NULL,                     -- Indica se e conta fixa (S/N)

        -- Auditoria da carga (controle interno, fora do layout da CVM).
        ArquivoOrigem  NVARCHAR(260)  NULL,
        ImportadoEmUtc DATETIME2      NOT NULL CONSTRAINT DF_Dre_ImportadoEm DEFAULT SYSUTCDATETIME()
    );
END
GO

-- Consulta tipica: por companhia e periodo de referencia.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dre_Cia_Refer' AND object_id = OBJECT_ID('dfp.Dre'))
    CREATE INDEX IX_Dre_Cia_Refer ON dfp.Dre(CD_CVM, DT_REFER, VERSAO) INCLUDE (CD_CONTA, VL_CONTA);
GO

-- Busca por conta especifica dentro de um exercicio.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dre_Conta' AND object_id = OBJECT_ID('dfp.Dre'))
    CREATE INDEX IX_Dre_Conta ON dfp.Dre(CNPJ_CIA, DT_REFER, ORDEM_EXERC, CD_CONTA);
GO
