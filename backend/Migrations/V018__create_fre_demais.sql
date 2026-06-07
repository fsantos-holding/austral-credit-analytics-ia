-- V018: tabelas FRE (schema [fre]) - Demais modelos FRE: empregados, ativos imobilizado/intangivel, historico, grupo economico e partes relacionadas.
-- Cada tabela espelha 1:1 o layout nativo da CVM + auditoria (Ano/ImportacaoId/ArquivoOrigem/CnpjNum).
-- Idempotente; lotes separados por GO.

-- Ativo Imobilizado (ativo_imobilizado)
IF OBJECT_ID('fre.AtivoImobilizado', 'U') IS NULL
BEGIN
    CREATE TABLE fre.AtivoImobilizado(
        Id                  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_AtivoImobilizado PRIMARY KEY,
        CNPJ_Companhia      VARCHAR(20) NULL,
        Data_Referencia     DATE NULL,
        ID_Documento        INT NULL,
        Nome_Companhia      VARCHAR(100) NULL,
        Pais                VARCHAR(100) NULL,
        UF                  VARCHAR(20) NULL,
        Versao              SMALLINT NULL,
        Descricao_Bem_Ativo VARCHAR(80) NULL,
        Tipo_Propriedade    VARCHAR(100) NULL,
        Municipio           VARCHAR(100) NULL,
        Ano                 SMALLINT       NULL,
        ImportacaoId        BIGINT         NULL,
        ArquivoOrigem       NVARCHAR(260)  NULL,
        CnpjNum             AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc      DATETIME2      NOT NULL CONSTRAINT DF_Fre_AtivoImobilizado_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_AtivoImobilizado_CnpjAno' AND object_id = OBJECT_ID('fre.AtivoImobilizado'))
    CREATE INDEX IX_Fre_AtivoImobilizado_CnpjAno ON fre.AtivoImobilizado(CnpjNum, Ano);
GO

-- Ativo Intangivel (ativo_intangivel)
IF OBJECT_ID('fre.AtivoIntangivel', 'U') IS NULL
BEGIN
    CREATE TABLE fre.AtivoIntangivel(
        Id                         BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_AtivoIntangivel PRIMARY KEY,
        CNPJ_Companhia             VARCHAR(20) NULL,
        Data_Referencia            DATE NULL,
        ID_Documento               INT NULL,
        Nome_Companhia             VARCHAR(100) NULL,
        Tipo_Ativo                 VARCHAR(100) NULL,
        Versao                     SMALLINT NULL,
        Descricao_Ativo            VARCHAR(100) NULL,
        Evento_Perda_Direito       VARCHAR(1000) NULL,
        Consequencia_Perda_Direito VARCHAR(1000) NULL,
        Duracao                    VARCHAR(30) NULL,
        Ano                        SMALLINT       NULL,
        ImportacaoId               BIGINT         NULL,
        ArquivoOrigem              NVARCHAR(260)  NULL,
        CnpjNum                    AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc             DATETIME2      NOT NULL CONSTRAINT DF_Fre_AtivoIntangivel_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_AtivoIntangivel_CnpjAno' AND object_id = OBJECT_ID('fre.AtivoIntangivel'))
    CREATE INDEX IX_Fre_AtivoIntangivel_CnpjAno ON fre.AtivoIntangivel(CnpjNum, Ano);
GO

-- Empregado Declaracao Genero (empregado_declaracao_genero)
IF OBJECT_ID('fre.EmpregadoDeclaracaoGenero', 'U') IS NULL
BEGIN
    CREATE TABLE fre.EmpregadoDeclaracaoGenero(
        Id                      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_EmpregadoDeclaracaoGenero PRIMARY KEY,
        Classe                  VARCHAR(100) NULL,
        CNPJ_Companhia          VARCHAR(20) NULL,
        Data_Referencia         DATE NULL,
        ID_Documento            INT NULL,
        Versao                  SMALLINT NULL,
        Quantidade_Nao_Binario  INT NULL,
        Quantidade_Sem_Resposta INT NULL,
        Quantidade_Feminino     INT NULL,
        Quantidade_Masculino    INT NULL,
        Quantidade_Outros       INT NULL,
        Ano                     SMALLINT       NULL,
        ImportacaoId            BIGINT         NULL,
        ArquivoOrigem           NVARCHAR(260)  NULL,
        CnpjNum                 AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc          DATETIME2      NOT NULL CONSTRAINT DF_Fre_EmpregadoDeclaracaoGenero_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_EmpregadoDeclaracaoGenero_CnpjAno' AND object_id = OBJECT_ID('fre.EmpregadoDeclaracaoGenero'))
    CREATE INDEX IX_Fre_EmpregadoDeclaracaoGenero_CnpjAno ON fre.EmpregadoDeclaracaoGenero(CnpjNum, Ano);
GO

-- Empregado Declaracao Raca (empregado_declaracao_raca)
IF OBJECT_ID('fre.EmpregadoDeclaracaoRaca', 'U') IS NULL
BEGIN
    CREATE TABLE fre.EmpregadoDeclaracaoRaca(
        Id                      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_EmpregadoDeclaracaoRaca PRIMARY KEY,
        Classe                  VARCHAR(100) NULL,
        CNPJ_Companhia          VARCHAR(20) NULL,
        Data_Referencia         DATE NULL,
        ID_Documento            INT NULL,
        Versao                  SMALLINT NULL,
        Quantidade_Preto        INT NULL,
        Quantidade_Indigena     INT NULL,
        Quantidade_Amarelo      INT NULL,
        Quantidade_Pardo        INT NULL,
        Quantidade_Sem_Resposta INT NULL,
        Quantidade_Branco       INT NULL,
        Quantidade_Outros       INT NULL,
        Ano                     SMALLINT       NULL,
        ImportacaoId            BIGINT         NULL,
        ArquivoOrigem           NVARCHAR(260)  NULL,
        CnpjNum                 AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc          DATETIME2      NOT NULL CONSTRAINT DF_Fre_EmpregadoDeclaracaoRaca_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_EmpregadoDeclaracaoRaca_CnpjAno' AND object_id = OBJECT_ID('fre.EmpregadoDeclaracaoRaca'))
    CREATE INDEX IX_Fre_EmpregadoDeclaracaoRaca_CnpjAno ON fre.EmpregadoDeclaracaoRaca(CnpjNum, Ano);
GO

-- Empregado Local Faixa Etaria (empregado_local_faixa_etaria)
IF OBJECT_ID('fre.EmpregadoLocalFaixaEtaria', 'U') IS NULL
BEGIN
    CREATE TABLE fre.EmpregadoLocalFaixaEtaria(
        Id                     BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_EmpregadoLocalFaixaEtaria PRIMARY KEY,
        CNPJ_Companhia         VARCHAR(20) NULL,
        Data_Referencia        DATE NULL,
        ID_Documento           INT NULL,
        Nome_Companhia         VARCHAR(100) NULL,
        Versao                 SMALLINT NULL,
        Local                  VARCHAR(100) NULL,
        Quantidade_Ate30Anos   INT NULL,
        Quantidade_Acima50Anos INT NULL,
        Quantidade_30a50Anos   INT NULL,
        Ano                    SMALLINT       NULL,
        ImportacaoId           BIGINT         NULL,
        ArquivoOrigem          NVARCHAR(260)  NULL,
        CnpjNum                AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc         DATETIME2      NOT NULL CONSTRAINT DF_Fre_EmpregadoLocalFaixaEtaria_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_EmpregadoLocalFaixaEtaria_CnpjAno' AND object_id = OBJECT_ID('fre.EmpregadoLocalFaixaEtaria'))
    CREATE INDEX IX_Fre_EmpregadoLocalFaixaEtaria_CnpjAno ON fre.EmpregadoLocalFaixaEtaria(CnpjNum, Ano);
GO

-- Grupo Economico Reestruturacao (grupo_economico_reestruturacao)
IF OBJECT_ID('fre.GrupoEconomicoReestruturacao', 'U') IS NULL
BEGIN
    CREATE TABLE fre.GrupoEconomicoReestruturacao(
        Id                 BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_GrupoEconomicoReestruturacao PRIMARY KEY,
        CNPJ_Companhia     VARCHAR(20) NULL,
        Data_Referencia    DATE NULL,
        ID_Documento       INT NULL,
        Nome_Companhia     VARCHAR(100) NULL,
        Versao             SMALLINT NULL,
        Data_Operacao      DATE NULL,
        Descricao_Operacao VARCHAR(8000) NULL,
        Evento_Societario  VARCHAR(100) NULL,
        Ano                SMALLINT       NULL,
        ImportacaoId       BIGINT         NULL,
        ArquivoOrigem      NVARCHAR(260)  NULL,
        CnpjNum            AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc     DATETIME2      NOT NULL CONSTRAINT DF_Fre_GrupoEconomicoReestruturacao_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_GrupoEconomicoReestruturacao_CnpjAno' AND object_id = OBJECT_ID('fre.GrupoEconomicoReestruturacao'))
    CREATE INDEX IX_Fre_GrupoEconomicoReestruturacao_CnpjAno ON fre.GrupoEconomicoReestruturacao(CnpjNum, Ano);
GO

-- Historico Emissor (historico_emissor)
IF OBJECT_ID('fre.HistoricoEmissor', 'U') IS NULL
BEGIN
    CREATE TABLE fre.HistoricoEmissor(
        Id                              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_HistoricoEmissor PRIMARY KEY,
        CNPJ_Companhia                  VARCHAR(20) NULL,
        Data_Referencia                 DATE NULL,
        ID_Documento                    INT NULL,
        Nome_Companhia                  VARCHAR(100) NULL,
        Versao                          SMALLINT NULL,
        Data_Registro_Emissor           DATE NULL,
        Prazo_Duracao_Emissor           DATE NULL,
        Requisicao_Registro_Emissor     VARCHAR(1) NULL,
        Data_Constituicao_Emissor       DATE NULL,
        Pais_Constituicao_Emissor       VARCHAR(100) NULL,
        Sigla_Pais_Constituicao_Emissor VARCHAR(20) NULL,
        Forma_Constituicao_Emissor      VARCHAR(1000) NULL,
        Ano                             SMALLINT       NULL,
        ImportacaoId                    BIGINT         NULL,
        ArquivoOrigem                   NVARCHAR(260)  NULL,
        CnpjNum                         AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                  DATETIME2      NOT NULL CONSTRAINT DF_Fre_HistoricoEmissor_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_HistoricoEmissor_CnpjAno' AND object_id = OBJECT_ID('fre.HistoricoEmissor'))
    CREATE INDEX IX_Fre_HistoricoEmissor_CnpjAno ON fre.HistoricoEmissor(CnpjNum, Ano);
GO

-- Transacao Parte Relacionada (transacao_parte_relacionada)
IF OBJECT_ID('fre.TransacaoParteRelacionada', 'U') IS NULL
BEGIN
    CREATE TABLE fre.TransacaoParteRelacionada(
        Id                                       BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_TransacaoParteRelacionada PRIMARY KEY,
        CNPJ_Companhia                           VARCHAR(20) NULL,
        Data_Referencia                          DATE NULL,
        Data_Transacao                           DATE NULL,
        Documento_Parte_Relacionada              VARCHAR(20) NULL,
        Duracao_Transacao                        VARCHAR(1000) NULL,
        Emprestimo_Divida                        VARCHAR(1) NULL,
        Especificacao_Posicao_Contratual_Emissor VARCHAR(1000) NULL,
        Garantia_Seguro                          VARCHAR(1000) NULL,
        ID_Documento                             INT NULL,
        Montante_Envolvido                       DECIMAL(18,2) NULL,
        Montante_Interesse_Parte_Relacionada     VARCHAR(1000) NULL,
        Natureza_Razao_Operacao                  VARCHAR(1000) NULL,
        Nome_Companhia                           VARCHAR(100) NULL,
        Objeto_Contrato                          VARCHAR(1000) NULL,
        Parte_Relacionada                        VARCHAR(100) NULL,
        Posicao_Contratual_Emissor               VARCHAR(7) NULL,
        Relacao_Emissor                          VARCHAR(8000) NULL,
        Rescisao                                 VARCHAR(8000) NULL,
        Saldo_Existente                          VARCHAR(100) NULL,
        Taxa_Juros                               DECIMAL(18,6) NULL,
        Tipo_Pessoa                              VARCHAR(2) NULL,
        Versao                                   SMALLINT NULL,
        Ano                                      SMALLINT       NULL,
        ImportacaoId                             BIGINT         NULL,
        ArquivoOrigem                            NVARCHAR(260)  NULL,
        CnpjNum                                  AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                           DATETIME2      NOT NULL CONSTRAINT DF_Fre_TransacaoParteRelacionada_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_TransacaoParteRelacionada_CnpjAno' AND object_id = OBJECT_ID('fre.TransacaoParteRelacionada'))
    CREATE INDEX IX_Fre_TransacaoParteRelacionada_CnpjAno ON fre.TransacaoParteRelacionada(CnpjNum, Ano);
GO

