-- V016: tabelas FRE (schema [fre]) - Remuneracao de administradores: total por orgao, variavel, baseada em acoes e maxima/minima/media.
-- Cada tabela espelha 1:1 o layout nativo da CVM + auditoria (Ano/ImportacaoId/ArquivoOrigem/CnpjNum).
-- Idempotente; lotes separados por GO.

-- Remuneracao Acao (remuneracao_acao)
IF OBJECT_ID('fre.RemuneracaoAcao', 'U') IS NULL
BEGIN
    CREATE TABLE fre.RemuneracaoAcao(
        Id                                     BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_RemuneracaoAcao PRIMARY KEY,
        CNPJ_Companhia                         VARCHAR(20) NULL,
        Data_Fim_Exercicio_Social              DATE NULL,
        Data_Inicio_Exercicio_Social           DATE NULL,
        Data_Referencia                        DATE NULL,
        ID_Documento                           INT NULL,
        Nome_Companhia                         VARCHAR(100) NULL,
        Orgao_Administracao                    VARCHAR(50) NULL,
        Versao                                 SMALLINT NULL,
        Preco_Medio_Ponderado_Opcoes_Perdidas  DECIMAL(18,2) NULL,
        Diluicao_Potencial                     DECIMAL(18,6) NULL,
        Quantidade_Membros_Remunerados         DECIMAL(18,2) NULL,
        Preco_Medio_Ponderado_Opcoes_Exercidas DECIMAL(18,2) NULL,
        Preco_Medio_Ponderado_Opcoes_Em_Aberto DECIMAL(18,2) NULL,
        Quantidade_Total_Membros               DECIMAL(18,2) NULL,
        Ano                                    SMALLINT       NULL,
        ImportacaoId                           BIGINT         NULL,
        ArquivoOrigem                          NVARCHAR(260)  NULL,
        CnpjNum                                AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                         DATETIME2      NOT NULL CONSTRAINT DF_Fre_RemuneracaoAcao_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_RemuneracaoAcao_CnpjAno' AND object_id = OBJECT_ID('fre.RemuneracaoAcao'))
    CREATE INDEX IX_Fre_RemuneracaoAcao_CnpjAno ON fre.RemuneracaoAcao(CnpjNum, Ano);
GO

-- Remuneracao Maxima Minima Media (remuneracao_maxima_minima_media)
IF OBJECT_ID('fre.RemuneracaoMaximaMinimaMedia', 'U') IS NULL
BEGIN
    CREATE TABLE fre.RemuneracaoMaximaMinimaMedia(
        Id                           BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_RemuneracaoMaximaMinimaMedia PRIMARY KEY,
        CNPJ_Companhia               VARCHAR(20) NULL,
        Data_Fim_Exercicio_Social    DATE NULL,
        Data_Inicio_Exercicio_Social DATE NULL,
        Data_Referencia              DATE NULL,
        ID_Documento                 INT NULL,
        Nome_Companhia               VARCHAR(100) NULL,
        Numero_Membros               DECIMAL(18,2) NULL,
        Numero_Membros_Remunerados   DECIMAL(18,2) NULL,
        Observacao                   VARCHAR(8000) NULL,
        Orgao_Administracao          VARCHAR(100) NULL,
        Valor_Maior_Remuneracao      DECIMAL(18,2) NULL,
        Valor_Medio_Remuneracao      DECIMAL(18,2) NULL,
        Valor_Menor_Remuneracao      DECIMAL(18,2) NULL,
        Versao                       SMALLINT NULL,
        Ano                          SMALLINT       NULL,
        ImportacaoId                 BIGINT         NULL,
        ArquivoOrigem                NVARCHAR(260)  NULL,
        CnpjNum                      AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc               DATETIME2      NOT NULL CONSTRAINT DF_Fre_RemuneracaoMaximaMinimaMedia_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_RemuneracaoMaximaMinimaMedia_CnpjAno' AND object_id = OBJECT_ID('fre.RemuneracaoMaximaMinimaMedia'))
    CREATE INDEX IX_Fre_RemuneracaoMaximaMinimaMedia_CnpjAno ON fre.RemuneracaoMaximaMinimaMedia(CnpjNum, Ano);
GO

-- Remuneracao Total Orgao (remuneracao_total_orgao)
IF OBJECT_ID('fre.RemuneracaoTotalOrgao', 'U') IS NULL
BEGIN
    CREATE TABLE fre.RemuneracaoTotalOrgao(
        Id                                      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_RemuneracaoTotalOrgao PRIMARY KEY,
        Baseada_Acoes                           DECIMAL(18,2) NULL,
        Beneficios_Diretos_Indiretos            DECIMAL(18,2) NULL,
        Bonus                                   DECIMAL(18,2) NULL,
        Cessacao_Cargo                          DECIMAL(18,2) NULL,
        CNPJ_Companhia                          VARCHAR(20) NULL,
        Comissoes                               DECIMAL(18,2) NULL,
        Data_Fim_Exercicio_Social               DATE NULL,
        Data_Inicio_Exercicio_Social            DATE NULL,
        Data_Referencia                         DATE NULL,
        Descricao_Outros_Remuneracoes_Fixas     VARCHAR(8000) NULL,
        Descricao_Outros_Remuneracoes_Variaveis VARCHAR(8000) NULL,
        ID_Documento                            INT NULL,
        Nome_Companhia                          VARCHAR(100) NULL,
        Numero_Membros                          DECIMAL(18,2) NULL,
        Numero_Membros_Remunerados              DECIMAL(18,2) NULL,
        Observacao                              VARCHAR(8000) NULL,
        Orgao_Administracao                     VARCHAR(100) NULL,
        Outros_Valores_Fixos                    DECIMAL(18,2) NULL,
        Outros_Valores_Variaveis                DECIMAL(18,2) NULL,
        Participacao_Resultados                 DECIMAL(18,2) NULL,
        Participacao_Reunioes                   DECIMAL(18,2) NULL,
        Participacoes_Comites                   DECIMAL(18,2) NULL,
        Pos_emprego                             DECIMAL(18,2) NULL,
        Salario                                 DECIMAL(18,2) NULL,
        Total_Remuneracao                       DECIMAL(18,2) NULL,
        Total_Remuneracao_Orgao                 DECIMAL(18,2) NULL,
        Versao                                  SMALLINT NULL,
        Ano                                     SMALLINT       NULL,
        ImportacaoId                            BIGINT         NULL,
        ArquivoOrigem                           NVARCHAR(260)  NULL,
        CnpjNum                                 AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                          DATETIME2      NOT NULL CONSTRAINT DF_Fre_RemuneracaoTotalOrgao_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_RemuneracaoTotalOrgao_CnpjAno' AND object_id = OBJECT_ID('fre.RemuneracaoTotalOrgao'))
    CREATE INDEX IX_Fre_RemuneracaoTotalOrgao_CnpjAno ON fre.RemuneracaoTotalOrgao(CnpjNum, Ano);
GO

-- Remuneracao Variavel (remuneracao_variavel)
IF OBJECT_ID('fre.RemuneracaoVariavel', 'U') IS NULL
BEGIN
    CREATE TABLE fre.RemuneracaoVariavel(
        Id                                 BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_RemuneracaoVariavel PRIMARY KEY,
        CNPJ_Companhia                     VARCHAR(20) NULL,
        Data_Fim_Exercicio_Social          DATE NULL,
        Data_Inicio_Exercicio_Social       DATE NULL,
        Data_Referencia                    DATE NULL,
        ID_Documento                       INT NULL,
        Nome_Companhia                     VARCHAR(100) NULL,
        Orgao_Administracao                VARCHAR(50) NULL,
        Versao                             SMALLINT NULL,
        Participacao_Valor_Efetivo         DECIMAL(18,2) NULL,
        Bonus_Valor_Maximo                 DECIMAL(18,2) NULL,
        Bonus_Valor_Minimo                 DECIMAL(18,2) NULL,
        Participacao_Valor_Minimo          DECIMAL(18,2) NULL,
        Quantidade_Membros_Remunerados     DECIMAL(18,2) NULL,
        Bonus_Valor_Metas_Atingidas        DECIMAL(18,2) NULL,
        Participacao_Valor_Metas_Atingidas DECIMAL(18,2) NULL,
        Participacao_Valor_Maximo          DECIMAL(18,2) NULL,
        Bonus_Valor_Efetivo                DECIMAL(18,2) NULL,
        Quantidade_Total_Membros           DECIMAL(18,2) NULL,
        Ano                                SMALLINT       NULL,
        ImportacaoId                       BIGINT         NULL,
        ArquivoOrigem                      NVARCHAR(260)  NULL,
        CnpjNum                            AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                     DATETIME2      NOT NULL CONSTRAINT DF_Fre_RemuneracaoVariavel_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_RemuneracaoVariavel_CnpjAno' AND object_id = OBJECT_ID('fre.RemuneracaoVariavel'))
    CREATE INDEX IX_Fre_RemuneracaoVariavel_CnpjAno ON fre.RemuneracaoVariavel(CnpjNum, Ano);
GO

