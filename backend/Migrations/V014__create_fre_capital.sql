-- V014: tabelas FRE (schema [fre]) - Grupo de capital: capital social, aumentos/reducoes/desdobramentos, distribuicao, posicao acionaria e direito de acao.
-- Cada tabela espelha 1:1 o layout nativo da CVM + auditoria (Ano/ImportacaoId/ArquivoOrigem/CnpjNum).
-- Idempotente; lotes separados por GO.

-- Capital Social (capital_social)
IF OBJECT_ID('fre.CapitalSocial', 'U') IS NULL
BEGIN
    CREATE TABLE fre.CapitalSocial(
        Id                             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_CapitalSocial PRIMARY KEY,
        CNPJ_Companhia                 VARCHAR(20) NULL,
        Data_Referencia                DATE NULL,
        ID_Documento                   INT NULL,
        Nome_Companhia                 VARCHAR(100) NULL,
        Versao                         SMALLINT NULL,
        Data_Autorizacao_Aprovacao     DATE NULL,
        Prazo_Integralizacao           VARCHAR(30) NULL,
        Valor_Capital                  DECIMAL(18,2) NULL,
        Quantidade_Acoes_Ordinarias    DECIMAL(18,0) NULL,
        Quantidade_Acoes_Preferenciais DECIMAL(18,0) NULL,
        ID_Capital_Social              INT NULL,
        Tipo_Capital                   VARCHAR(100) NULL,
        Quantidade_Total_Acoes         DECIMAL(18,0) NULL,
        Ano                            SMALLINT       NULL,
        ImportacaoId                   BIGINT         NULL,
        ArquivoOrigem                  NVARCHAR(260)  NULL,
        CnpjNum                        AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                 DATETIME2      NOT NULL CONSTRAINT DF_Fre_CapitalSocial_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_CapitalSocial_CnpjAno' AND object_id = OBJECT_ID('fre.CapitalSocial'))
    CREATE INDEX IX_Fre_CapitalSocial_CnpjAno ON fre.CapitalSocial(CnpjNum, Ano);
GO

-- Capital Social Aumento (capital_social_aumento)
IF OBJECT_ID('fre.CapitalSocialAumento', 'U') IS NULL
BEGIN
    CREATE TABLE fre.CapitalSocialAumento(
        Id                                  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_CapitalSocialAumento PRIMARY KEY,
        CNPJ_Companhia                      VARCHAR(20) NULL,
        Data_Emissao                        DATE NULL,
        Data_Referencia                     DATE NULL,
        ID_Documento                        INT NULL,
        Nome_Companhia                      VARCHAR(100) NULL,
        Versao                              SMALLINT NULL,
        Fator_Cotacao                       VARCHAR(100) NULL,
        Criterio_Determinacao_Preco_Emissao VARCHAR(8000) NULL,
        Preco_Emissao                       DECIMAL(24,8) NULL,
        Subscricao_Capital_Anterior         DECIMAL(24,8) NULL,
        Valor_Total_Emissao                 DECIMAL(18,2) NULL,
        Quantidade_Acoes_Ordinarias         DECIMAL(18,0) NULL,
        Quantidade_Acoes_Preferenciais      DECIMAL(18,0) NULL,
        Orgao_Deliberacao_Aumento           VARCHAR(100) NULL,
        Tipo_Subscricao                     VARCHAR(100) NULL,
        Data_Deliberacao                    DATE NULL,
        Forma_Integralizacao                VARCHAR(8000) NULL,
        ID_Capital_Social_Aumento           INT NULL,
        Quantidade_Total_Acoes              DECIMAL(18,0) NULL,
        Ano                                 SMALLINT       NULL,
        ImportacaoId                        BIGINT         NULL,
        ArquivoOrigem                       NVARCHAR(260)  NULL,
        CnpjNum                             AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                      DATETIME2      NOT NULL CONSTRAINT DF_Fre_CapitalSocialAumento_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_CapitalSocialAumento_CnpjAno' AND object_id = OBJECT_ID('fre.CapitalSocialAumento'))
    CREATE INDEX IX_Fre_CapitalSocialAumento_CnpjAno ON fre.CapitalSocialAumento(CnpjNum, Ano);
GO

-- Capital Social Aumento Classe Acao (capital_social_aumento_classe_acao)
IF OBJECT_ID('fre.CapitalSocialAumentoClasseAcao', 'U') IS NULL
BEGIN
    CREATE TABLE fre.CapitalSocialAumentoClasseAcao(
        Id                            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_CapitalSocialAumentoClasseAcao PRIMARY KEY,
        CNPJ_Companhia                VARCHAR(20) NULL,
        Data_Referencia               DATE NULL,
        ID_Documento                  INT NULL,
        Nome_Companhia                VARCHAR(100) NULL,
        Versao                        SMALLINT NULL,
        Tipo_Classe_Acao_Preferencial VARCHAR(100) NULL,
        Quantidade_Acoes              DECIMAL(18,0) NULL,
        ID_Capital_Social_Aumento     INT NULL,
        Ano                           SMALLINT       NULL,
        ImportacaoId                  BIGINT         NULL,
        ArquivoOrigem                 NVARCHAR(260)  NULL,
        CnpjNum                       AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                DATETIME2      NOT NULL CONSTRAINT DF_Fre_CapitalSocialAumentoClasseAcao_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_CapitalSocialAumentoClasseAcao_CnpjAno' AND object_id = OBJECT_ID('fre.CapitalSocialAumentoClasseAcao'))
    CREATE INDEX IX_Fre_CapitalSocialAumentoClasseAcao_CnpjAno ON fre.CapitalSocialAumentoClasseAcao(CnpjNum, Ano);
GO

-- Capital Social Classe Acao (capital_social_classe_acao)
IF OBJECT_ID('fre.CapitalSocialClasseAcao', 'U') IS NULL
BEGIN
    CREATE TABLE fre.CapitalSocialClasseAcao(
        Id                            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_CapitalSocialClasseAcao PRIMARY KEY,
        CNPJ_Companhia                VARCHAR(20) NULL,
        Data_Referencia               DATE NULL,
        ID_Documento                  INT NULL,
        Nome_Companhia                VARCHAR(100) NULL,
        Versao                        SMALLINT NULL,
        ID_Capital_Social             INT NULL,
        Tipo_Classe_Acao_Preferencial VARCHAR(100) NULL,
        Quantidade_Acoes              DECIMAL(18,0) NULL,
        Ano                           SMALLINT       NULL,
        ImportacaoId                  BIGINT         NULL,
        ArquivoOrigem                 NVARCHAR(260)  NULL,
        CnpjNum                       AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                DATETIME2      NOT NULL CONSTRAINT DF_Fre_CapitalSocialClasseAcao_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_CapitalSocialClasseAcao_CnpjAno' AND object_id = OBJECT_ID('fre.CapitalSocialClasseAcao'))
    CREATE INDEX IX_Fre_CapitalSocialClasseAcao_CnpjAno ON fre.CapitalSocialClasseAcao(CnpjNum, Ano);
GO

-- Capital Social Desdobramento (capital_social_desdobramento)
IF OBJECT_ID('fre.CapitalSocialDesdobramento', 'U') IS NULL
BEGIN
    CREATE TABLE fre.CapitalSocialDesdobramento(
        Id                                              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_CapitalSocialDesdobramento PRIMARY KEY,
        CNPJ_Companhia                                  VARCHAR(20) NULL,
        Data_Referencia                                 DATE NULL,
        ID_Documento                                    INT NULL,
        Nome_Companhia                                  VARCHAR(100) NULL,
        Versao                                          SMALLINT NULL,
        ID_Capital_Social_Desdobramento                 INT NULL,
        Data_Aprovacao                                  DATE NULL,
        Quantidade_Total_Acoes_Antes_Aprovacao          DECIMAL(18,0) NULL,
        Quantidade_Acoes_Preferenciais_Depois_Aprovacao DECIMAL(18,0) NULL,
        Quantidade_Acoes_Preferenciais_Antes_Aprovacao  DECIMAL(18,0) NULL,
        Tipo_Evento                                     VARCHAR(100) NULL,
        Quantidade_Total_Acoes_Depois_Aprovacao         DECIMAL(18,0) NULL,
        Quantidade_Acoes_Ordinarias_Depois_Aprovacao    DECIMAL(18,0) NULL,
        Quantidade_Acoes_Ordinarias_Antes_Aprovacao     DECIMAL(18,0) NULL,
        Ano                                             SMALLINT       NULL,
        ImportacaoId                                    BIGINT         NULL,
        ArquivoOrigem                                   NVARCHAR(260)  NULL,
        CnpjNum                                         AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                                  DATETIME2      NOT NULL CONSTRAINT DF_Fre_CapitalSocialDesdobramento_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_CapitalSocialDesdobramento_CnpjAno' AND object_id = OBJECT_ID('fre.CapitalSocialDesdobramento'))
    CREATE INDEX IX_Fre_CapitalSocialDesdobramento_CnpjAno ON fre.CapitalSocialDesdobramento(CnpjNum, Ano);
GO

-- Capital Social Desdobramento Classe Acao (capital_social_desdobramento_classe_acao)
IF OBJECT_ID('fre.CapitalSocialDesdobramentoClasseAcao', 'U') IS NULL
BEGIN
    CREATE TABLE fre.CapitalSocialDesdobramentoClasseAcao(
        Id                                BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_CapitalSocialDesdobramentoClasseAcao PRIMARY KEY,
        CNPJ_Companhia                    VARCHAR(20) NULL,
        Data_Referencia                   DATE NULL,
        ID_Documento                      INT NULL,
        Nome_Companhia                    VARCHAR(100) NULL,
        Versao                            SMALLINT NULL,
        ID_Capital_Social_Desdobramento   INT NULL,
        Quantidade_Acoes_Depois_Aprovacao DECIMAL(18,0) NULL,
        Quantidade_Acoes_Antes_Aprovacao  DECIMAL(18,0) NULL,
        Tipo_Classe_Acao_Preferencial     VARCHAR(100) NULL,
        Ano                               SMALLINT       NULL,
        ImportacaoId                      BIGINT         NULL,
        ArquivoOrigem                     NVARCHAR(260)  NULL,
        CnpjNum                           AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                    DATETIME2      NOT NULL CONSTRAINT DF_Fre_CapitalSocialDesdobramentoClasseAcao_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_CapitalSocialDesdobramentoClasseAcao_CnpjAno' AND object_id = OBJECT_ID('fre.CapitalSocialDesdobramentoClasseAcao'))
    CREATE INDEX IX_Fre_CapitalSocialDesdobramentoClasseAcao_CnpjAno ON fre.CapitalSocialDesdobramentoClasseAcao(CnpjNum, Ano);
GO

-- Capital Social Reducao (capital_social_reducao)
IF OBJECT_ID('fre.CapitalSocialReducao', 'U') IS NULL
BEGIN
    CREATE TABLE fre.CapitalSocialReducao(
        Id                             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_CapitalSocialReducao PRIMARY KEY,
        CNPJ_Companhia                 VARCHAR(20) NULL,
        Data_Referencia                DATE NULL,
        ID_Documento                   INT NULL,
        Nome_Companhia                 VARCHAR(100) NULL,
        Versao                         SMALLINT NULL,
        Quantidade_Acoes_Ordinarias    DECIMAL(18,0) NULL,
        Reducao_Capital_Anterior       DECIMAL(18,6) NULL,
        Quantidade_Acoes_Preferenciais DECIMAL(18,0) NULL,
        ID_Capital_Social_Reducao      INT NULL,
        Razao_Reducao                  VARCHAR(8000) NULL,
        Forma_Restituicao              VARCHAR(8000) NULL,
        Data_Deliberacao               DATE NULL,
        Data_Reducao                   DATE NULL,
        Valor_Total_Reducao            DECIMAL(18,2) NULL,
        Valor_Restituido_Por_Acao      DECIMAL(18,8) NULL,
        Quantidade_Total_Acoes         DECIMAL(18,0) NULL,
        Ano                            SMALLINT       NULL,
        ImportacaoId                   BIGINT         NULL,
        ArquivoOrigem                  NVARCHAR(260)  NULL,
        CnpjNum                        AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                 DATETIME2      NOT NULL CONSTRAINT DF_Fre_CapitalSocialReducao_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_CapitalSocialReducao_CnpjAno' AND object_id = OBJECT_ID('fre.CapitalSocialReducao'))
    CREATE INDEX IX_Fre_CapitalSocialReducao_CnpjAno ON fre.CapitalSocialReducao(CnpjNum, Ano);
GO

-- Capital Social Reducao Classe Acao (capital_social_reducao_classe_acao)
IF OBJECT_ID('fre.CapitalSocialReducaoClasseAcao', 'U') IS NULL
BEGIN
    CREATE TABLE fre.CapitalSocialReducaoClasseAcao(
        Id                            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_CapitalSocialReducaoClasseAcao PRIMARY KEY,
        CNPJ_Companhia                VARCHAR(20) NULL,
        Data_Referencia               DATE NULL,
        ID_Documento                  INT NULL,
        Nome_Companhia                VARCHAR(100) NULL,
        Versao                        SMALLINT NULL,
        Tipo_Classe_Acao_Preferencial VARCHAR(100) NULL,
        Quantidade_Acoes              DECIMAL(18,0) NULL,
        ID_Capital_Social_Reducao     INT NULL,
        Ano                           SMALLINT       NULL,
        ImportacaoId                  BIGINT         NULL,
        ArquivoOrigem                 NVARCHAR(260)  NULL,
        CnpjNum                       AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                DATETIME2      NOT NULL CONSTRAINT DF_Fre_CapitalSocialReducaoClasseAcao_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_CapitalSocialReducaoClasseAcao_CnpjAno' AND object_id = OBJECT_ID('fre.CapitalSocialReducaoClasseAcao'))
    CREATE INDEX IX_Fre_CapitalSocialReducaoClasseAcao_CnpjAno ON fre.CapitalSocialReducaoClasseAcao(CnpjNum, Ano);
GO

-- Capital Social Titulo Conversivel (capital_social_titulo_conversivel)
IF OBJECT_ID('fre.CapitalSocialTituloConversivel', 'U') IS NULL
BEGIN
    CREATE TABLE fre.CapitalSocialTituloConversivel(
        Id                      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_CapitalSocialTituloConversivel PRIMARY KEY,
        CNPJ_Companhia          VARCHAR(20) NULL,
        Data_Referencia         DATE NULL,
        ID_Documento            INT NULL,
        Nome_Companhia          VARCHAR(100) NULL,
        Versao                  SMALLINT NULL,
        Condicoes_Conversao     VARCHAR(8000) NULL,
        ID_Capital_Social       INT NULL,
        Titulo_Conversivel_Acao VARCHAR(100) NULL,
        Ano                     SMALLINT       NULL,
        ImportacaoId            BIGINT         NULL,
        ArquivoOrigem           NVARCHAR(260)  NULL,
        CnpjNum                 AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc          DATETIME2      NOT NULL CONSTRAINT DF_Fre_CapitalSocialTituloConversivel_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_CapitalSocialTituloConversivel_CnpjAno' AND object_id = OBJECT_ID('fre.CapitalSocialTituloConversivel'))
    CREATE INDEX IX_Fre_CapitalSocialTituloConversivel_CnpjAno ON fre.CapitalSocialTituloConversivel(CnpjNum, Ano);
GO

-- Direito Acao (direito_acao)
IF OBJECT_ID('fre.DireitoAcao', 'U') IS NULL
BEGIN
    CREATE TABLE fre.DireitoAcao(
        Id                                              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_DireitoAcao PRIMARY KEY,
        Classe_Acao_Preferencial                        VARCHAR(100) NULL,
        CNPJ_Companhia                                  VARCHAR(20) NULL,
        Data_Referencia                                 DATE NULL,
        ID_Documento                                    INT NULL,
        Nome_Companhia                                  VARCHAR(100) NULL,
        Versao                                          SMALLINT NULL,
        Condicao_Alteracao_Direitos                     VARCHAR(8000) NULL,
        Direito_Voto                                    VARCHAR(100) NULL,
        Especie_Acao                                    VARCHAR(100) NULL,
        Descricao_Voto_Restrito                         VARCHAR(8000) NULL,
        Descricao_Restricao_Circulacao                  VARCHAR(8000) NULL,
        Condicao_Conversibilidade_Efeito_Capital_Social VARCHAR(8000) NULL,
        Resgatavel                                      VARCHAR(1) NULL,
        Caracteristicas_Reembolso_Capital               VARCHAR(8000) NULL,
        Percentual_Tag_Along                            DECIMAL(18,6) NULL,
        Conversibilidade                                VARCHAR(1) NULL,
        Restricao_Circulacao                            VARCHAR(1) NULL,
        Direito_Dividendo                               VARCHAR(8000) NULL,
        Direito_Reembolso_Capital                       VARCHAR(1) NULL,
        Hipotese_Resgate_Formula_Calculo                VARCHAR(8000) NULL,
        Outras_Caracteristicas_Relevantes               VARCHAR(8000) NULL,
        Ano                                             SMALLINT       NULL,
        ImportacaoId                                    BIGINT         NULL,
        ArquivoOrigem                                   NVARCHAR(260)  NULL,
        CnpjNum                                         AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                                  DATETIME2      NOT NULL CONSTRAINT DF_Fre_DireitoAcao_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_DireitoAcao_CnpjAno' AND object_id = OBJECT_ID('fre.DireitoAcao'))
    CREATE INDEX IX_Fre_DireitoAcao_CnpjAno ON fre.DireitoAcao(CnpjNum, Ano);
GO

-- Distribuicao Capital (distribuicao_capital)
IF OBJECT_ID('fre.DistribuicaoCapital', 'U') IS NULL
BEGIN
    CREATE TABLE fre.DistribuicaoCapital(
        Id                                                BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_DistribuicaoCapital PRIMARY KEY,
        CNPJ_Companhia                                    VARCHAR(20) NULL,
        Data_Referencia                                   DATE NULL,
        Data_Ultima_Assembleia                            DATE NULL,
        ID_Documento                                      INT NULL,
        Nome_Companhia                                    VARCHAR(100) NULL,
        Percentual_Acoes_Ordinarias_Circulacao            DECIMAL(18,6) NULL,
        Percentual_Acoes_Preferenciais_Circulacao         DECIMAL(18,6) NULL,
        Percentual_Total_Acoes_Circulacao                 DECIMAL(18,6) NULL,
        Quantidade_Acionistas_Investidores_Institucionais DECIMAL(18,0) NULL,
        Quantidade_Acionistas_PF                          DECIMAL(18,0) NULL,
        Quantidade_Acionistas_PJ                          DECIMAL(18,0) NULL,
        Quantidade_Acoes_Ordinarias_Circulacao            DECIMAL(18,0) NULL,
        Quantidade_Acoes_Preferenciais_Circulacao         DECIMAL(18,0) NULL,
        Quantidade_Total_Acoes_Circulacao                 DECIMAL(18,0) NULL,
        Versao                                            SMALLINT NULL,
        Ano                                               SMALLINT       NULL,
        ImportacaoId                                      BIGINT         NULL,
        ArquivoOrigem                                     NVARCHAR(260)  NULL,
        CnpjNum                                           AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                                    DATETIME2      NOT NULL CONSTRAINT DF_Fre_DistribuicaoCapital_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_DistribuicaoCapital_CnpjAno' AND object_id = OBJECT_ID('fre.DistribuicaoCapital'))
    CREATE INDEX IX_Fre_DistribuicaoCapital_CnpjAno ON fre.DistribuicaoCapital(CnpjNum, Ano);
GO

-- Distribuicao Capital Classe Acao (distribuicao_capital_classe_acao)
IF OBJECT_ID('fre.DistribuicaoCapitalClasseAcao', 'U') IS NULL
BEGIN
    CREATE TABLE fre.DistribuicaoCapitalClasseAcao(
        Id                                        BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_DistribuicaoCapitalClasseAcao PRIMARY KEY,
        Classe_Acoes_Preferenciais                VARCHAR(100) NULL,
        CNPJ_Companhia                            VARCHAR(20) NULL,
        Data_Referencia                           DATE NULL,
        ID_Documento                              INT NULL,
        Nome_Companhia                            VARCHAR(100) NULL,
        Percentual_Acoes_Preferenciais_Circulacao DECIMAL(18,6) NULL,
        Quantidade_Acoes_Preferenciais_Circulacao DECIMAL(18,0) NULL,
        Sigla_Classe_Acoes_Preferenciais          VARCHAR(20) NULL,
        Versao                                    SMALLINT NULL,
        Ano                                       SMALLINT       NULL,
        ImportacaoId                              BIGINT         NULL,
        ArquivoOrigem                             NVARCHAR(260)  NULL,
        CnpjNum                                   AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                            DATETIME2      NOT NULL CONSTRAINT DF_Fre_DistribuicaoCapitalClasseAcao_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_DistribuicaoCapitalClasseAcao_CnpjAno' AND object_id = OBJECT_ID('fre.DistribuicaoCapitalClasseAcao'))
    CREATE INDEX IX_Fre_DistribuicaoCapitalClasseAcao_CnpjAno ON fre.DistribuicaoCapitalClasseAcao(CnpjNum, Ano);
GO

-- Posicao Acionaria (posicao_acionaria)
IF OBJECT_ID('fre.PosicaoAcionaria', 'U') IS NULL
BEGIN
    CREATE TABLE fre.PosicaoAcionaria(
        Id                                      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_PosicaoAcionaria PRIMARY KEY,
        Acionista                               VARCHAR(100) NULL,
        Acionista_Controlador                   VARCHAR(1) NULL,
        Acionista_Relacionado                   VARCHAR(100) NULL,
        CNPJ_Companhia                          VARCHAR(20) NULL,
        CPF_CNPJ_Acionista                      VARCHAR(20) NULL,
        CPF_CNPJ_Acionista_Relacionado          VARCHAR(20) NULL,
        CPF_CNPJ_Representante_legal            VARCHAR(20) NULL,
        Data_Composicao_Capital_Social          DATE NULL,
        Data_Referencia                         DATE NULL,
        Data_Ultima_Alteracao                   DATE NULL,
        ID_Acionista                            INT NULL,
        ID_Acionista_Relacionado                INT NULL,
        ID_Documento                            INT NULL,
        Nacionalidade                           VARCHAR(30) NULL,
        Nome_Companhia                          VARCHAR(100) NULL,
        Participante_Acordo_Acionistas          VARCHAR(1) NULL,
        Percentual_Acao_Ordinaria_Circulacao    DECIMAL(18,6) NULL,
        Percentual_Acao_Preferencial_Circulacao DECIMAL(18,6) NULL,
        Percentual_Total_Acoes_Circulacao       DECIMAL(18,6) NULL,
        Quantidade_Acao_Ordinaria_Circulacao    DECIMAL(18,0) NULL,
        Quantidade_Acao_Preferencial_Circulacao DECIMAL(18,0) NULL,
        Quantidade_Total_Acoes_Circulacao       DECIMAL(18,0) NULL,
        Representante_Legal                     VARCHAR(100) NULL,
        Residente_Exterior                      VARCHAR(1) NULL,
        Sigla_UF                                VARCHAR(20) NULL,
        Tipo_Pessoa_Acionista                   VARCHAR(2) NULL,
        Tipo_Pessoa_Acionista_Relacionado       VARCHAR(2) NULL,
        Tipo_Pessoa_Representante_Legal         VARCHAR(2) NULL,
        Versao                                  SMALLINT NULL,
        Ano                                     SMALLINT       NULL,
        ImportacaoId                            BIGINT         NULL,
        ArquivoOrigem                           NVARCHAR(260)  NULL,
        CnpjNum                                 AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                          DATETIME2      NOT NULL CONSTRAINT DF_Fre_PosicaoAcionaria_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_PosicaoAcionaria_CnpjAno' AND object_id = OBJECT_ID('fre.PosicaoAcionaria'))
    CREATE INDEX IX_Fre_PosicaoAcionaria_CnpjAno ON fre.PosicaoAcionaria(CnpjNum, Ano);
GO

-- Posicao Acionaria Classe Acao (posicao_acionaria_classe_acao)
IF OBJECT_ID('fre.PosicaoAcionariaClasseAcao', 'U') IS NULL
BEGIN
    CREATE TABLE fre.PosicaoAcionariaClasseAcao(
        Id                            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_PosicaoAcionariaClasseAcao PRIMARY KEY,
        CNPJ_Companhia                VARCHAR(20) NULL,
        Data_Referencia               DATE NULL,
        ID_Acionista                  INT NULL,
        ID_Documento                  INT NULL,
        Nome_Companhia                VARCHAR(100) NULL,
        Versao                        SMALLINT NULL,
        Percentual_Acoes              DECIMAL(18,6) NULL,
        Tipo_Classe_Acao_Preferencial VARCHAR(100) NULL,
        Quantidade_Acoes              DECIMAL(18,0) NULL,
        Ano                           SMALLINT       NULL,
        ImportacaoId                  BIGINT         NULL,
        ArquivoOrigem                 NVARCHAR(260)  NULL,
        CnpjNum                       AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                DATETIME2      NOT NULL CONSTRAINT DF_Fre_PosicaoAcionariaClasseAcao_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_PosicaoAcionariaClasseAcao_CnpjAno' AND object_id = OBJECT_ID('fre.PosicaoAcionariaClasseAcao'))
    CREATE INDEX IX_Fre_PosicaoAcionariaClasseAcao_CnpjAno ON fre.PosicaoAcionariaClasseAcao(CnpjNum, Ano);
GO

