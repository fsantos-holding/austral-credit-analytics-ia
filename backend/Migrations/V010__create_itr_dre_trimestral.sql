-- V010: tabela de saida do motor de trimestralizacao (FinancialStatementQuarter).
-- Cada linha e uma conta da DRE de uma companhia em um trimestre (1..4) de um ano,
-- ja de-acumulada (ValorTrimestral) e tambem com o valor acumulado original
-- (ValorAcumulado). O motor (TrimestralizacaoService) une itr.Dre (T1-T3) e
-- dfp.Dre (4T anual), deduplica por MAX(VERSAO) e grava o resultado aqui.
--   OrigemTrimestre - 'ITR' (T1-T3) ou 'DFP' (4T, vindo do anual).
--   Inconsistente   - sinaliza trimestre intermediario ausente ou serie atipica.
-- Idempotente. Lotes separados por GO.

IF SCHEMA_ID('itr') IS NULL
    EXEC('CREATE SCHEMA itr');
GO

IF OBJECT_ID('itr.DreTrimestral', 'U') IS NULL
BEGIN
    CREATE TABLE itr.DreTrimestral(
        Id                    BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Itr_DreTrimestral PRIMARY KEY,

        CD_CVM                CHAR(6)        NOT NULL,           -- Codigo CVM da companhia
        CnpjNum               VARCHAR(20)    NULL,              -- CNPJ so digitos (busca)
        DENOM_CIA             VARCHAR(100)   NULL,              -- Nome empresarial
        Ano                   SMALLINT       NOT NULL,           -- Ano do trimestre (YEAR(DT_FIM_EXERC))
        Trimestre             TINYINT        NOT NULL,           -- 1..4
        Conjunto              CHAR(3)        NOT NULL,           -- 'CON'/'IND'

        CD_CONTA              VARCHAR(18)    NOT NULL,           -- Codigo da conta
        DS_CONTA              VARCHAR(100)   NULL,              -- Descricao da conta

        ValorTrimestral       DECIMAL(29,10) NULL,              -- Valor do trimestre (de-acumulado)
        ValorAcumulado        DECIMAL(29,10) NULL,              -- Valor acumulado original (CVM)
        OrigemTrimestre       CHAR(3)        NOT NULL,           -- 'ITR' ou 'DFP'
        EscalaMoeda           VARCHAR(100)   NULL,              -- Escala monetaria (formatacao)

        Inconsistente         BIT            NOT NULL CONSTRAINT DF_Itr_DreTri_Inconsistente DEFAULT 0,
        MotivoInconsistencia  NVARCHAR(200)  NULL,

        CalculadoEmUtc        DATETIME2      NOT NULL CONSTRAINT DF_Itr_DreTri_Calculado DEFAULT SYSUTCDATETIME(),

        CONSTRAINT UQ_Itr_DreTrimestral UNIQUE (CD_CVM, Conjunto, Ano, Trimestre, CD_CONTA)
    );
END
GO

-- Leitura por companhia/periodo (endpoint de leitura trimestral).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Itr_DreTri_Cnpj' AND object_id = OBJECT_ID('itr.DreTrimestral'))
    CREATE INDEX IX_Itr_DreTri_Cnpj ON itr.DreTrimestral(CnpjNum, Conjunto, Ano, Trimestre);
GO

-- View de leitura amigavel (o dicionario de contas e aplicado no frontend).
CREATE OR ALTER VIEW itr.vw_DreTrimestral AS
    SELECT
        CD_CVM, CnpjNum, DENOM_CIA, Ano, Trimestre, Conjunto,
        CD_CONTA, DS_CONTA, ValorTrimestral, ValorAcumulado,
        OrigemTrimestre, EscalaMoeda, Inconsistente, MotivoInconsistencia, CalculadoEmUtc
    FROM itr.DreTrimestral;
GO
