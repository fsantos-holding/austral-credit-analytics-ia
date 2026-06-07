-- V017: tabelas FRE (schema [fre]) - Governanca: administradores, comites, conselho fiscal, auditores, politica de negociacao e relacoes.
-- Cada tabela espelha 1:1 o layout nativo da CVM + auditoria (Ano/ImportacaoId/ArquivoOrigem/CnpjNum).
-- Idempotente; lotes separados por GO.

-- Administrador Declaracao Genero (administrador_declaracao_genero)
IF OBJECT_ID('fre.AdministradorDeclaracaoGenero', 'U') IS NULL
BEGIN
    CREATE TABLE fre.AdministradorDeclaracaoGenero(
        Id                      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_AdministradorDeclaracaoGenero PRIMARY KEY,
        CNPJ_Companhia          VARCHAR(20) NULL,
        Data_Referencia         DATE NULL,
        ID_Documento            INT NULL,
        Nome_Companhia          VARCHAR(100) NULL,
        Orgao_Administracao     VARCHAR(100) NULL,
        Versao                  SMALLINT NULL,
        Quantidade_Nao_Binario  INT NULL,
        Quantidade_Sem_Resposta INT NULL,
        Quantidade_Feminino     INT NULL,
        Quantidade_Masculino    INT NULL,
        Quantidade_Outros       INT NULL,
        Nao_Aplicavel           VARCHAR(1) NULL,
        Ano                     SMALLINT       NULL,
        ImportacaoId            BIGINT         NULL,
        ArquivoOrigem           NVARCHAR(260)  NULL,
        CnpjNum                 AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc          DATETIME2      NOT NULL CONSTRAINT DF_Fre_AdministradorDeclaracaoGenero_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_AdministradorDeclaracaoGenero_CnpjAno' AND object_id = OBJECT_ID('fre.AdministradorDeclaracaoGenero'))
    CREATE INDEX IX_Fre_AdministradorDeclaracaoGenero_CnpjAno ON fre.AdministradorDeclaracaoGenero(CnpjNum, Ano);
GO

-- Administrador Declaracao Raca (administrador_declaracao_raca)
IF OBJECT_ID('fre.AdministradorDeclaracaoRaca', 'U') IS NULL
BEGIN
    CREATE TABLE fre.AdministradorDeclaracaoRaca(
        Id                      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_AdministradorDeclaracaoRaca PRIMARY KEY,
        CNPJ_Companhia          VARCHAR(20) NULL,
        Data_Referencia         DATE NULL,
        ID_Documento            INT NULL,
        Nome_Companhia          VARCHAR(100) NULL,
        Orgao_Administracao     VARCHAR(100) NULL,
        Versao                  SMALLINT NULL,
        Quantidade_Preto        INT NULL,
        Quantidade_Indigena     INT NULL,
        Quantidade_Amarelo      INT NULL,
        Quantidade_Pardo        INT NULL,
        Quantidade_Sem_Resposta INT NULL,
        Quantidade_Branco       INT NULL,
        Quantidade_Outros       INT NULL,
        Nao_Aplicavel           VARCHAR(1) NULL,
        Ano                     SMALLINT       NULL,
        ImportacaoId            BIGINT         NULL,
        ArquivoOrigem           NVARCHAR(260)  NULL,
        CnpjNum                 AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc          DATETIME2      NOT NULL CONSTRAINT DF_Fre_AdministradorDeclaracaoRaca_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_AdministradorDeclaracaoRaca_CnpjAno' AND object_id = OBJECT_ID('fre.AdministradorDeclaracaoRaca'))
    CREATE INDEX IX_Fre_AdministradorDeclaracaoRaca_CnpjAno ON fre.AdministradorDeclaracaoRaca(CnpjNum, Ano);
GO

-- Administrador Membro Conselho Fiscal (administrador_membro_conselho_fiscal)
IF OBJECT_ID('fre.AdministradorMembroConselhoFiscal', 'U') IS NULL
BEGIN
    CREATE TABLE fre.AdministradorMembroConselhoFiscal(
        Id                                BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_AdministradorMembroConselhoFiscal PRIMARY KEY,
        Cargo_Eletivo_Ocupado             VARCHAR(100) NULL,
        CNPJ_Companhia                    VARCHAR(20) NULL,
        Complemento_Cargo_Eletivo_Ocupado VARCHAR(100) NULL,
        CPF                               VARCHAR(20) NULL,
        Data_Eleicao                      DATE NULL,
        Data_Nascimento                   DATE NULL,
        Data_Posse                        DATE NULL,
        Data_Referencia                   DATE NULL,
        Eleito_Controlador                VARCHAR(1) NULL,
        Experiencia_Profissional          VARCHAR(8000) NULL,
        ID_Documento                      INT NULL,
        Nome                              VARCHAR(100) NULL,
        Nome_Companhia                    VARCHAR(100) NULL,
        Numero_Mandatos_Consecutivos      SMALLINT NULL,
        Orgao_Administracao               VARCHAR(100) NULL,
        Outro_Cargo_Funcao                VARCHAR(1000) NULL,
        Percentual_Participacao_Reunioes  DECIMAL(7,2) NULL,
        Prazo_Mandato                     VARCHAR(100) NULL,
        Profissao                         VARCHAR(100) NULL,
        Versao                            SMALLINT NULL,
        Data_Inicio_Primeiro_Mandato      DATE NULL,
        Ano                               SMALLINT       NULL,
        ImportacaoId                      BIGINT         NULL,
        ArquivoOrigem                     NVARCHAR(260)  NULL,
        CnpjNum                           AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                    DATETIME2      NOT NULL CONSTRAINT DF_Fre_AdministradorMembroConselhoFiscal_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_AdministradorMembroConselhoFiscal_CnpjAno' AND object_id = OBJECT_ID('fre.AdministradorMembroConselhoFiscal'))
    CREATE INDEX IX_Fre_AdministradorMembroConselhoFiscal_CnpjAno ON fre.AdministradorMembroConselhoFiscal(CnpjNum, Ano);
GO

-- Auditor (auditor)
IF OBJECT_ID('fre.Auditor', 'U') IS NULL
BEGIN
    CREATE TABLE fre.Auditor(
        Id                            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_Auditor PRIMARY KEY,
        Auditor                       VARCHAR(100) NULL,
        CNPJ_Auditor                  CHAR(14) NULL,
        CNPJ_Companhia                VARCHAR(20) NULL,
        Codigo_CVM_Auditor            CHAR(6) NULL,
        Data_Referencia               DATE NULL,
        ID_Documento                  INT NULL,
        Nome_Companhia                VARCHAR(100) NULL,
        Versao                        SMALLINT NULL,
        Justificativa_Substituicao    VARCHAR(8000) NULL,
        Servico_Contratado            VARCHAR(8000) NULL,
        Remuneracao_Auditor           VARCHAR(8000) NULL,
        Tipo_Origem_Auditor           VARCHAR(11) NULL,
        CPF_Auditor                   CHAR(14) NULL,
        Data_Inicio_Prestacao_Servico DATE NULL,
        Data_Fim_Contratacao          DATE NULL,
        Razao_Apresentada             VARCHAR(8000) NULL,
        ID_Auditor                    INT NULL,
        Data_Inicio_Contratacao       DATE NULL,
        Ano                           SMALLINT       NULL,
        ImportacaoId                  BIGINT         NULL,
        ArquivoOrigem                 NVARCHAR(260)  NULL,
        CnpjNum                       AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                DATETIME2      NOT NULL CONSTRAINT DF_Fre_Auditor_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_Auditor_CnpjAno' AND object_id = OBJECT_ID('fre.Auditor'))
    CREATE INDEX IX_Fre_Auditor_CnpjAno ON fre.Auditor(CnpjNum, Ano);
GO

-- Auditor Responsavel (auditor_responsavel)
IF OBJECT_ID('fre.AuditorResponsavel', 'U') IS NULL
BEGIN
    CREATE TABLE fre.AuditorResponsavel(
        Id                              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_AuditorResponsavel PRIMARY KEY,
        Bairro                          VARCHAR(20) NULL,
        CEP                             CHAR(8) NULL,
        Cidade                          VARCHAR(100) NULL,
        CNPJ_Companhia                  VARCHAR(20) NULL,
        Complemento                     VARCHAR(20) NULL,
        CPF_Responsavel_Tecnico         CHAR(14) NULL,
        Data_Referencia                 DATE NULL,
        DDD_Telefone                    CHAR(4) NULL,
        DDI_Telefone                    CHAR(4) NULL,
        Email                           VARCHAR(100) NULL,
        Fax                             CHAR(10) NULL,
        ID_Documento                    INT NULL,
        Logradouro                      VARCHAR(60) NULL,
        Nome_Companhia                  VARCHAR(100) NULL,
        Pais                            VARCHAR(100) NULL,
        Responsavel_Tecnico             VARCHAR(100) NULL,
        Sigla_UF                        VARCHAR(20) NULL,
        Telefone                        CHAR(10) NULL,
        UF                              VARCHAR(100) NULL,
        Versao                          SMALLINT NULL,
        Data_Inicio_Responsavel_Tecnico DATE NULL,
        Data_Fim_Responsavel_Tecnico    DATE NULL,
        ID_Auditor                      INT NULL,
        Ano                             SMALLINT       NULL,
        ImportacaoId                    BIGINT         NULL,
        ArquivoOrigem                   NVARCHAR(260)  NULL,
        CnpjNum                         AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                  DATETIME2      NOT NULL CONSTRAINT DF_Fre_AuditorResponsavel_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_AuditorResponsavel_CnpjAno' AND object_id = OBJECT_ID('fre.AuditorResponsavel'))
    CREATE INDEX IX_Fre_AuditorResponsavel_CnpjAno ON fre.AuditorResponsavel(CnpjNum, Ano);
GO

-- Membro Comite (membro_comite)
IF OBJECT_ID('fre.MembroComite', 'U') IS NULL
BEGIN
    CREATE TABLE fre.MembroComite(
        Id                               BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_MembroComite PRIMARY KEY,
        Cargo_Ocupado                    VARCHAR(100) NULL,
        CNPJ_Companhia                   VARCHAR(20) NULL,
        CPF                              VARCHAR(20) NULL,
        Data_Eleicao                     DATE NULL,
        Data_Nascimento                  DATE NULL,
        Data_Posse                       DATE NULL,
        Data_Referencia                  DATE NULL,
        Descricao_Outro_Cargo_Ocupado    VARCHAR(100) NULL,
        Descricao_Outros_Comites         VARCHAR(100) NULL,
        Experiencia_Profissional         VARCHAR(8000) NULL,
        ID_Documento                     INT NULL,
        Nome                             VARCHAR(100) NULL,
        Nome_Companhia                   VARCHAR(100) NULL,
        Numero_Mandatos_Consecutivos     SMALLINT NULL,
        Outro_Cargo_Funcao               VARCHAR(1000) NULL,
        Percentual_Participacao_Reunioes DECIMAL(7,2) NULL,
        Prazo_Mandato                    VARCHAR(100) NULL,
        Profissao                        VARCHAR(100) NULL,
        Tipo_Comite                      VARCHAR(100) NULL,
        Versao                           SMALLINT NULL,
        Data_Inicio_Primeiro_Mandato     DATE NULL,
        Ano                              SMALLINT       NULL,
        ImportacaoId                     BIGINT         NULL,
        ArquivoOrigem                    NVARCHAR(260)  NULL,
        CnpjNum                          AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                   DATETIME2      NOT NULL CONSTRAINT DF_Fre_MembroComite_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_MembroComite_CnpjAno' AND object_id = OBJECT_ID('fre.MembroComite'))
    CREATE INDEX IX_Fre_MembroComite_CnpjAno ON fre.MembroComite(CnpjNum, Ano);
GO

-- Politica Negociacao (politica_negociacao)
IF OBJECT_ID('fre.PoliticaNegociacao', 'U') IS NULL
BEGIN
    CREATE TABLE fre.PoliticaNegociacao(
        Id                                 BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_PoliticaNegociacao PRIMARY KEY,
        CNPJ_Companhia                     VARCHAR(20) NULL,
        Data_Referencia                    DATE NULL,
        ID_Documento                       INT NULL,
        Nome_Companhia                     VARCHAR(100) NULL,
        Versao                             SMALLINT NULL,
        ID_Politica                        INT NULL,
        Data_Aprovacao                     DATETIME2 NULL,
        Vedacao_Procedimentos_Fiscalizacao VARCHAR(8000) NULL,
        Principais_Caracteristicas         VARCHAR(8000) NULL,
        Orgao_Aprovacao                    VARCHAR(40) NULL,
        Ano                                SMALLINT       NULL,
        ImportacaoId                       BIGINT         NULL,
        ArquivoOrigem                      NVARCHAR(260)  NULL,
        CnpjNum                            AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                     DATETIME2      NOT NULL CONSTRAINT DF_Fre_PoliticaNegociacao_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_PoliticaNegociacao_CnpjAno' AND object_id = OBJECT_ID('fre.PoliticaNegociacao'))
    CREATE INDEX IX_Fre_PoliticaNegociacao_CnpjAno ON fre.PoliticaNegociacao(CnpjNum, Ano);
GO

-- Politica Negociacao Cargo (politica_negociacao_cargo)
IF OBJECT_ID('fre.PoliticaNegociacaoCargo', 'U') IS NULL
BEGIN
    CREATE TABLE fre.PoliticaNegociacaoCargo(
        Id                     BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_PoliticaNegociacaoCargo PRIMARY KEY,
        CNPJ_Companhia         VARCHAR(20) NULL,
        Data_Referencia        DATE NULL,
        ID_Documento           INT NULL,
        Nome_Companhia         VARCHAR(100) NULL,
        Versao                 SMALLINT NULL,
        ID_Politica            INT NULL,
        Cargo_Pessoa_Vinculada VARCHAR(1000) NULL,
        Ano                    SMALLINT       NULL,
        ImportacaoId           BIGINT         NULL,
        ArquivoOrigem          NVARCHAR(260)  NULL,
        CnpjNum                AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc         DATETIME2      NOT NULL CONSTRAINT DF_Fre_PoliticaNegociacaoCargo_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_PoliticaNegociacaoCargo_CnpjAno' AND object_id = OBJECT_ID('fre.PoliticaNegociacaoCargo'))
    CREATE INDEX IX_Fre_PoliticaNegociacaoCargo_CnpjAno ON fre.PoliticaNegociacaoCargo(CnpjNum, Ano);
GO

-- Relacao Familiar (relacao_familiar)
IF OBJECT_ID('fre.RelacaoFamiliar', 'U') IS NULL
BEGIN
    CREATE TABLE fre.RelacaoFamiliar(
        Id                              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_RelacaoFamiliar PRIMARY KEY,
        Cargo_Administrador             VARCHAR(1000) NULL,
        Cargo_Pessoa_Relacionada        VARCHAR(1000) NULL,
        CNPJ_Companhia                  VARCHAR(20) NULL,
        CNPJ_Emissor                    VARCHAR(20) NULL,
        CNPJ_Emissor_Pessoa_Relacionada VARCHAR(20) NULL,
        CPF_Administrador               VARCHAR(20) NULL,
        CPF_Pessoa_Relacionada          VARCHAR(20) NULL,
        Data_Referencia                 DATE NULL,
        ID_Documento                    INT NULL,
        Nome_Administrador              VARCHAR(100) NULL,
        Nome_Companhia                  VARCHAR(100) NULL,
        Nome_Emissor                    VARCHAR(100) NULL,
        Nome_Emissor_Pessoa_Relacionada VARCHAR(100) NULL,
        Nome_Pessoa_Relacionada         VARCHAR(100) NULL,
        Observacao                      VARCHAR(1000) NULL,
        Tipo_Parentesco                 VARCHAR(100) NULL,
        Versao                          SMALLINT NULL,
        Ano                             SMALLINT       NULL,
        ImportacaoId                    BIGINT         NULL,
        ArquivoOrigem                   NVARCHAR(260)  NULL,
        CnpjNum                         AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                  DATETIME2      NOT NULL CONSTRAINT DF_Fre_RelacaoFamiliar_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_RelacaoFamiliar_CnpjAno' AND object_id = OBJECT_ID('fre.RelacaoFamiliar'))
    CREATE INDEX IX_Fre_RelacaoFamiliar_CnpjAno ON fre.RelacaoFamiliar(CnpjNum, Ano);
GO

-- Relacao Subordinacao (relacao_subordinacao)
IF OBJECT_ID('fre.RelacaoSubordinacao', 'U') IS NULL
BEGIN
    CREATE TABLE fre.RelacaoSubordinacao(
        Id                           BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_RelacaoSubordinacao PRIMARY KEY,
        Cargo_Administrador          VARCHAR(1000) NULL,
        Cargo_Pessoa_Relacionada     VARCHAR(1000) NULL,
        Categoria_Pessoa_Relacionada VARCHAR(100) NULL,
        CNPJ_Companhia               VARCHAR(20) NULL,
        CPF_Administrador            VARCHAR(20) NULL,
        Data_Fim_Exercicio_Social    DATE NULL,
        Data_Inicio_Exercicio_Social DATE NULL,
        Data_Referencia              DATE NULL,
        Documento_Pessoa_Relacionada VARCHAR(20) NULL,
        ID_Documento                 INT NULL,
        Nome_Administrador           VARCHAR(100) NULL,
        Nome_Companhia               VARCHAR(100) NULL,
        Nome_Pessoa_Relacionada      VARCHAR(100) NULL,
        Observacao                   VARCHAR(1000) NULL,
        Tipo_Pessoa_Relacionada      VARCHAR(2) NULL,
        Tipo_Relacao                 VARCHAR(100) NULL,
        Versao                       SMALLINT NULL,
        Ano                          SMALLINT       NULL,
        ImportacaoId                 BIGINT         NULL,
        ArquivoOrigem                NVARCHAR(260)  NULL,
        CnpjNum                      AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc               DATETIME2      NOT NULL CONSTRAINT DF_Fre_RelacaoSubordinacao_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_RelacaoSubordinacao_CnpjAno' AND object_id = OBJECT_ID('fre.RelacaoSubordinacao'))
    CREATE INDEX IX_Fre_RelacaoSubordinacao_CnpjAno ON fre.RelacaoSubordinacao(CnpjNum, Ano);
GO

-- Responsavel (responsavel)
IF OBJECT_ID('fre.Responsavel', 'U') IS NULL
BEGIN
    CREATE TABLE fre.Responsavel(
        Id                BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_Responsavel PRIMARY KEY,
        CNPJ_Companhia    VARCHAR(20) NULL,
        Data_Referencia   DATE NULL,
        ID_Documento      INT NULL,
        Nome_Companhia    VARCHAR(100) NULL,
        Versao            SMALLINT NULL,
        Cargo_Responsavel VARCHAR(100) NULL,
        Nome_Responsavel  VARCHAR(100) NULL,
        Ano               SMALLINT       NULL,
        ImportacaoId      BIGINT         NULL,
        ArquivoOrigem     NVARCHAR(260)  NULL,
        CnpjNum           AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc    DATETIME2      NOT NULL CONSTRAINT DF_Fre_Responsavel_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_Responsavel_CnpjAno' AND object_id = OBJECT_ID('fre.Responsavel'))
    CREATE INDEX IX_Fre_Responsavel_CnpjAno ON fre.Responsavel(CnpjNum, Ano);
GO

