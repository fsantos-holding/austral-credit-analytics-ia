-- V015: tabelas FRE (schema [fre]) - Valores mobiliarios: titulares, volume, tesouraria, titulos no exterior, mercados estrangeiros, recompra e participacoes.
-- Cada tabela espelha 1:1 o layout nativo da CVM + auditoria (Ano/ImportacaoId/ArquivoOrigem/CnpjNum).
-- Idempotente; lotes separados por GO.

-- Acao Entregue (acao_entregue)
IF OBJECT_ID('fre.AcaoEntregue', 'U') IS NULL
BEGIN
    CREATE TABLE fre.AcaoEntregue(
        Id                                BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_AcaoEntregue PRIMARY KEY,
        CNPJ_Companhia                    VARCHAR(20) NULL,
        Data_Fim_Exercicio_Social         DATE NULL,
        Data_Inicio_Exercicio_Social      DATE NULL,
        Data_Referencia                   DATE NULL,
        ID_Documento                      INT NULL,
        Nome_Companhia                    VARCHAR(100) NULL,
        Orgao_Administracao               VARCHAR(50) NULL,
        Versao                            SMALLINT NULL,
        Valor_Diferenca_Aquisicao_Mercado DECIMAL(18,2) NULL,
        Preco_Medio_Ponderado_Aquisicao   DECIMAL(18,2) NULL,
        Preco_Medio_Ponderado_Mercado     DECIMAL(18,2) NULL,
        Quantidade_Acoes                  INT NULL,
        Quantidade_Membros_Remunerados    DECIMAL(18,2) NULL,
        Quantidade_Total_Membros          DECIMAL(18,2) NULL,
        Ano                               SMALLINT       NULL,
        ImportacaoId                      BIGINT         NULL,
        ArquivoOrigem                     NVARCHAR(260)  NULL,
        CnpjNum                           AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                    DATETIME2      NOT NULL CONSTRAINT DF_Fre_AcaoEntregue_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_AcaoEntregue_CnpjAno' AND object_id = OBJECT_ID('fre.AcaoEntregue'))
    CREATE INDEX IX_Fre_AcaoEntregue_CnpjAno ON fre.AcaoEntregue(CnpjNum, Ano);
GO

-- Mercado Estrangeiro (mercado_estrangeiro)
IF OBJECT_ID('fre.MercadoEstrangeiro', 'U') IS NULL
BEGIN
    CREATE TABLE fre.MercadoEstrangeiro(
        Id                                BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_MercadoEstrangeiro PRIMARY KEY,
        CNPJ_Companhia                    VARCHAR(20) NULL,
        Data_Emissao                      DATE NULL,
        Data_Inicio_Listagem              DATE NULL,
        Data_Referencia                   DATE NULL,
        ID_Documento                      INT NULL,
        Mercado                           VARCHAR(100) NULL,
        Nome_Companhia                    VARCHAR(100) NULL,
        Valor_Mobiliario                  VARCHAR(100) NULL,
        Versao                            SMALLINT NULL,
        Descricao_Instituicao_Custodiante VARCHAR(600) NULL,
        Descricao_Banco_Depositario       VARCHAR(600) NULL,
        Percentual                        DECIMAL(18,6) NULL,
        Administradora                    VARCHAR(100) NULL,
        Pais_Negociacao                   VARCHAR(100) NULL,
        Descricao_Proporcao_Certificado   VARCHAR(600) NULL,
        Identificacao_Valor_Mobiliario    VARCHAR(100) NULL,
        Descricao_Segmento                VARCHAR(600) NULL,
        Ano                               SMALLINT       NULL,
        ImportacaoId                      BIGINT         NULL,
        ArquivoOrigem                     NVARCHAR(260)  NULL,
        CnpjNum                           AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                    DATETIME2      NOT NULL CONSTRAINT DF_Fre_MercadoEstrangeiro_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_MercadoEstrangeiro_CnpjAno' AND object_id = OBJECT_ID('fre.MercadoEstrangeiro'))
    CREATE INDEX IX_Fre_MercadoEstrangeiro_CnpjAno ON fre.MercadoEstrangeiro(CnpjNum, Ano);
GO

-- Outro Valor Mobiliario (outro_valor_mobiliario)
IF OBJECT_ID('fre.OutroValorMobiliario', 'U') IS NULL
BEGIN
    CREATE TABLE fre.OutroValorMobiliario(
        Id                                              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_OutroValorMobiliario PRIMARY KEY,
        CNPJ_Companhia                                  VARCHAR(20) NULL,
        Data_Emissao                                    DATE NULL,
        Data_Referencia                                 DATE NULL,
        Data_Vencimento                                 DATE NULL,
        ID_Documento                                    INT NULL,
        Nome_Companhia                                  VARCHAR(100) NULL,
        Valor_Mobiliario                                VARCHAR(100) NULL,
        Versao                                          SMALLINT NULL,
        Condicao_Alteracao_Direitos                     VARCHAR(8000) NULL,
        Caracteristicas_Valores_Mobiliarios_Divida      VARCHAR(8000) NULL,
        Descricao_Restricao_Circulacao                  VARCHAR(8000) NULL,
        Condicao_Conversibilidade_Efeito_Capital_Social VARCHAR(8000) NULL,
        Resgatavel                                      VARCHAR(3) NULL,
        Quantidade_Investidor_Institucional             DECIMAL(18,0) NULL,
        Conversibilidade                                VARCHAR(3) NULL,
        Restricao_Circulacao                            VARCHAR(3) NULL,
        Valor                                           DECIMAL(18,2) NULL,
        Saldo_Devedor                                   DECIMAL(18,2) NULL,
        Quantidade_Pessoa_Fisica                        DECIMAL(18,0) NULL,
        Quantidade_Pessoa_Juridica                      DECIMAL(18,0) NULL,
        Quantidade                                      DECIMAL(18,0) NULL,
        Hipotese_Resgate_Formula_Calculo                VARCHAR(8000) NULL,
        Identificacao_Valor_Mobiliario                  VARCHAR(100) NULL,
        Outras_Caracteristicas_Relevantes               VARCHAR(8000) NULL,
        Ano                                             SMALLINT       NULL,
        ImportacaoId                                    BIGINT         NULL,
        ArquivoOrigem                                   NVARCHAR(260)  NULL,
        CnpjNum                                         AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                                  DATETIME2      NOT NULL CONSTRAINT DF_Fre_OutroValorMobiliario_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_OutroValorMobiliario_CnpjAno' AND object_id = OBJECT_ID('fre.OutroValorMobiliario'))
    CREATE INDEX IX_Fre_OutroValorMobiliario_CnpjAno ON fre.OutroValorMobiliario(CnpjNum, Ano);
GO

-- Participacao Sociedade (participacao_sociedade)
IF OBJECT_ID('fre.ParticipacaoSociedade', 'U') IS NULL
BEGIN
    CREATE TABLE fre.ParticipacaoSociedade(
        Id                         BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_ParticipacaoSociedade PRIMARY KEY,
        CNPJ                       CHAR(14) NULL,
        CNPJ_Companhia             VARCHAR(20) NULL,
        Codigo_CVM                 VARCHAR(6) NULL,
        Data_Referencia            DATE NULL,
        ID_Documento               INT NULL,
        Nome_Companhia             VARCHAR(100) NULL,
        Versao                     SMALLINT NULL,
        Data_Valor_Contabil        DATE NULL,
        Data_Valor_Mercado         DATE NULL,
        Valor_Mercado              DECIMAL(18,2) NULL,
        Tipo_Sociedade             VARCHAR(100) NULL,
        UF_Sede                    VARCHAR(20) NULL,
        Razao_Aquisicao_Manutencao VARCHAR(1000) NULL,
        Valor_Contabil             DECIMAL(18,2) NULL,
        Descricao_Atividades       VARCHAR(1000) NULL,
        Razao_Social               VARCHAR(100) NULL,
        Participacao_Emissor       DECIMAL(24,8) NULL,
        Possui_Registro_CVM        VARCHAR(1) NULL,
        Municipio_Sede             VARCHAR(100) NULL,
        Pais_Sede                  VARCHAR(100) NULL,
        ID_Sociedade               INT NULL,
        Ano                        SMALLINT       NULL,
        ImportacaoId               BIGINT         NULL,
        ArquivoOrigem              NVARCHAR(260)  NULL,
        CnpjNum                    AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc             DATETIME2      NOT NULL CONSTRAINT DF_Fre_ParticipacaoSociedade_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_ParticipacaoSociedade_CnpjAno' AND object_id = OBJECT_ID('fre.ParticipacaoSociedade'))
    CREATE INDEX IX_Fre_ParticipacaoSociedade_CnpjAno ON fre.ParticipacaoSociedade(CnpjNum, Ano);
GO

-- Participacao Sociedade Valorizacao Acao (participacao_sociedade_valorizacao_acao)
IF OBJECT_ID('fre.ParticipacaoSociedadeValorizacaoAcao', 'U') IS NULL
BEGIN
    CREATE TABLE fre.ParticipacaoSociedadeValorizacaoAcao(
        Id                                  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_ParticipacaoSociedadeValorizacaoAcao PRIMARY KEY,
        CNPJ_Companhia                      VARCHAR(20) NULL,
        Data_Referencia                     DATE NULL,
        ID_Documento                        INT NULL,
        Nome_Companhia                      VARCHAR(100) NULL,
        Versao                              SMALLINT NULL,
        Data_Encerramento                   DATE NULL,
        Variacao_Percentual_Valor_Mercado   DECIMAL(24,8) NULL,
        Variacao_Percentual_Valor_Contabil  DECIMAL(24,8) NULL,
        Valor_Montante_Dividendos_Recebidos DECIMAL(18,2) NULL,
        ID_Sociedade                        INT NULL,
        Ano                                 SMALLINT       NULL,
        ImportacaoId                        BIGINT         NULL,
        ArquivoOrigem                       NVARCHAR(260)  NULL,
        CnpjNum                             AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                      DATETIME2      NOT NULL CONSTRAINT DF_Fre_ParticipacaoSociedadeValorizacaoAcao_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_ParticipacaoSociedadeValorizacaoAcao_CnpjAno' AND object_id = OBJECT_ID('fre.ParticipacaoSociedadeValorizacaoAcao'))
    CREATE INDEX IX_Fre_ParticipacaoSociedadeValorizacaoAcao_CnpjAno ON fre.ParticipacaoSociedadeValorizacaoAcao(CnpjNum, Ano);
GO

-- Plano Recompra (plano_recompra)
IF OBJECT_ID('fre.PlanoRecompra', 'U') IS NULL
BEGIN
    CREATE TABLE fre.PlanoRecompra(
        Id                                 BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_PlanoRecompra PRIMARY KEY,
        CNPJ_Companhia                     VARCHAR(20) NULL,
        Data_Referencia                    DATE NULL,
        ID_Documento                       INT NULL,
        Nome_Companhia                     VARCHAR(100) NULL,
        Versao                             SMALLINT NULL,
        Data_Fim_Recompra                  DATE NULL,
        Valor_Reserva_Disponivel_Recompra  DECIMAL(18,2) NULL,
        Data_Deliberacao                   DATE NULL,
        ID_Plano_Recompra                  INT NULL,
        Data_Inicio_Recompra               DATE NULL,
        Outras_Caracteristicas_Importantes VARCHAR(8000) NULL,
        Ano                                SMALLINT       NULL,
        ImportacaoId                       BIGINT         NULL,
        ArquivoOrigem                      NVARCHAR(260)  NULL,
        CnpjNum                            AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                     DATETIME2      NOT NULL CONSTRAINT DF_Fre_PlanoRecompra_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_PlanoRecompra_CnpjAno' AND object_id = OBJECT_ID('fre.PlanoRecompra'))
    CREATE INDEX IX_Fre_PlanoRecompra_CnpjAno ON fre.PlanoRecompra(CnpjNum, Ano);
GO

-- Plano Recompra Classe Acao (plano_recompra_classe_acao)
IF OBJECT_ID('fre.PlanoRecompraClasseAcao', 'U') IS NULL
BEGIN
    CREATE TABLE fre.PlanoRecompraClasseAcao(
        Id                            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_PlanoRecompraClasseAcao PRIMARY KEY,
        CNPJ_Companhia                VARCHAR(20) NULL,
        Data_Referencia               DATE NULL,
        ID_Documento                  INT NULL,
        Nome_Companhia                VARCHAR(100) NULL,
        Versao                        SMALLINT NULL,
        Quantidade_Prevista           BIGINT NULL,
        Especie_Acao                  VARCHAR(100) NULL,
        Quantidade_Adquirida          BIGINT NULL,
        Valor_Preco_Medio             DECIMAL(18,2) NULL,
        Escala_Cotacao                VARCHAR(100) NULL,
        Percentual_Previsto           DECIMAL(18,6) NULL,
        Tipo_Classe_Acao_Preferencial VARCHAR(100) NULL,
        Percentual_Adquirido          DECIMAL(18,6) NULL,
        ID_Plano_Recompra             INT NULL,
        Ano                           SMALLINT       NULL,
        ImportacaoId                  BIGINT         NULL,
        ArquivoOrigem                 NVARCHAR(260)  NULL,
        CnpjNum                       AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                DATETIME2      NOT NULL CONSTRAINT DF_Fre_PlanoRecompraClasseAcao_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_PlanoRecompraClasseAcao_CnpjAno' AND object_id = OBJECT_ID('fre.PlanoRecompraClasseAcao'))
    CREATE INDEX IX_Fre_PlanoRecompraClasseAcao_CnpjAno ON fre.PlanoRecompraClasseAcao(CnpjNum, Ano);
GO

-- Titular Valor Mobiliario (titular_valor_mobiliario)
IF OBJECT_ID('fre.TitularValorMobiliario', 'U') IS NULL
BEGIN
    CREATE TABLE fre.TitularValorMobiliario(
        Id                         BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_TitularValorMobiliario PRIMARY KEY,
        CNPJ_Companhia             VARCHAR(20) NULL,
        Data_Referencia            DATE NULL,
        ID_Documento               INT NULL,
        Nome_Companhia             VARCHAR(100) NULL,
        Valor_Mobiliario           VARCHAR(100) NULL,
        Versao                     SMALLINT NULL,
        Quantidade_Investidor      DECIMAL(18,0) NULL,
        Quantidade_Pessoa_Fisica   DECIMAL(18,0) NULL,
        Quantidade_Pessoa_Juridica DECIMAL(18,0) NULL,
        Ano                        SMALLINT       NULL,
        ImportacaoId               BIGINT         NULL,
        ArquivoOrigem              NVARCHAR(260)  NULL,
        CnpjNum                    AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc             DATETIME2      NOT NULL CONSTRAINT DF_Fre_TitularValorMobiliario_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_TitularValorMobiliario_CnpjAno' AND object_id = OBJECT_ID('fre.TitularValorMobiliario'))
    CREATE INDEX IX_Fre_TitularValorMobiliario_CnpjAno ON fre.TitularValorMobiliario(CnpjNum, Ano);
GO

-- Titulo Exterior (titulo_exterior)
IF OBJECT_ID('fre.TituloExterior', 'U') IS NULL
BEGIN
    CREATE TABLE fre.TituloExterior(
        Id                             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_TituloExterior PRIMARY KEY,
        CNPJ_Companhia                 VARCHAR(20) NULL,
        Data_Emissao                   DATE NULL,
        Data_Referencia                DATE NULL,
        Data_Vencimento                DATE NULL,
        ID_Documento                   INT NULL,
        Nome_Companhia                 VARCHAR(100) NULL,
        Valor_Mobiliario               VARCHAR(100) NULL,
        Versao                         SMALLINT NULL,
        Condicao_Alteracao_Direitos    VARCHAR(8000) NULL,
        Descricao_Restricao_Circulacao VARCHAR(8000) NULL,
        Valor_Nominal                  DECIMAL(18,2) NULL,
        Possibilidade_Resgate          VARCHAR(1) NULL,
        Conversibilidade               VARCHAR(1) NULL,
        Restricao_Circulacao           VARCHAR(1) NULL,
        Condicao_Conversibilidade      VARCHAR(8000) NULL,
        Saldo_Devedor                  DECIMAL(18,2) NULL,
        Quantidade                     BIGINT NULL,
        Outras_Caracteristicas         VARCHAR(8000) NULL,
        Identificacao_Valor_Mobiliario VARCHAR(100) NULL,
        Hipotese_Calculo_Resgate       VARCHAR(8000) NULL,
        Caracteristicas_Divida         VARCHAR(8000) NULL,
        Ano                            SMALLINT       NULL,
        ImportacaoId                   BIGINT         NULL,
        ArquivoOrigem                  NVARCHAR(260)  NULL,
        CnpjNum                        AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                 DATETIME2      NOT NULL CONSTRAINT DF_Fre_TituloExterior_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_TituloExterior_CnpjAno' AND object_id = OBJECT_ID('fre.TituloExterior'))
    CREATE INDEX IX_Fre_TituloExterior_CnpjAno ON fre.TituloExterior(CnpjNum, Ano);
GO

-- Valor Mobiliario Tesouraria Movimentacao (valor_mobiliario_tesouraria_movimentacao)
IF OBJECT_ID('fre.ValorMobiliarioTesourariaMovimentacao', 'U') IS NULL
BEGIN
    CREATE TABLE fre.ValorMobiliarioTesourariaMovimentacao(
        Id                                                BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_ValorMobiliarioTesourariaMovimentacao PRIMARY KEY,
        CNPJ_Companhia                                    VARCHAR(20) NULL,
        Data_Fim_Exercicio_Social                         DATE NULL,
        Data_Inicio_Exercicio_Social                      DATE NULL,
        Data_Referencia                                   DATE NULL,
        ID_Documento                                      INT NULL,
        Nome_Companhia                                    VARCHAR(100) NULL,
        Valor_Mobiliario                                  VARCHAR(100) NULL,
        Versao                                            SMALLINT NULL,
        Percentual_Relacao_Valores_Mobiliarios_Circulacao DECIMAL(18,6) NULL,
        Quantidade_Final                                  DECIMAL(18,0) NULL,
        Valor_Total_Final                                 DECIMAL(18,2) NULL,
        Valor_Total_Cancelado                             DECIMAL(18,2) NULL,
        Especie_Acao                                      VARCHAR(100) NULL,
        Valor_Preco_Medio_Adquirido                       DECIMAL(18,2) NULL,
        Quantidade_Adquirida                              DECIMAL(18,0) NULL,
        Quantidade_Inicial                                DECIMAL(18,0) NULL,
        Escala_Cotacao                                    VARCHAR(100) NULL,
        Valor_Preco_Medio_Final                           DECIMAL(18,2) NULL,
        Quantidade_Alienada                               DECIMAL(18,0) NULL,
        Valor_Total_Inicial                               DECIMAL(18,2) NULL,
        Descricao_Valor_Mobiliario                        VARCHAR(30) NULL,
        Tipo_Classe_Acao_Preferencial                     VARCHAR(100) NULL,
        Valor_Preco_Medio_Alienado                        DECIMAL(18,2) NULL,
        Valor_Preco_Medio_Inicial                         DECIMAL(18,2) NULL,
        Valor_Total_Alienado                              DECIMAL(18,2) NULL,
        Valor_Preco_Medio_Cancelado                       DECIMAL(18,2) NULL,
        Quantidade_Cancelada                              DECIMAL(18,0) NULL,
        Valor_Total_Adquirido                             DECIMAL(18,2) NULL,
        Ano                                               SMALLINT       NULL,
        ImportacaoId                                      BIGINT         NULL,
        ArquivoOrigem                                     NVARCHAR(260)  NULL,
        CnpjNum                                           AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                                    DATETIME2      NOT NULL CONSTRAINT DF_Fre_ValorMobiliarioTesourariaMovimentacao_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_ValorMobiliarioTesourariaMovimentacao_CnpjAno' AND object_id = OBJECT_ID('fre.ValorMobiliarioTesourariaMovimentacao'))
    CREATE INDEX IX_Fre_ValorMobiliarioTesourariaMovimentacao_CnpjAno ON fre.ValorMobiliarioTesourariaMovimentacao(CnpjNum, Ano);
GO

-- Valor Mobiliario Tesouraria Ultimo Exercicio (valor_mobiliario_tesouraria_ultimo_exercicio)
IF OBJECT_ID('fre.ValorMobiliarioTesourariaUltimoExercicio', 'U') IS NULL
BEGIN
    CREATE TABLE fre.ValorMobiliarioTesourariaUltimoExercicio(
        Id                            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_ValorMobiliarioTesourariaUltimoExercicio PRIMARY KEY,
        CNPJ_Companhia                VARCHAR(20) NULL,
        Data_Referencia               DATE NULL,
        ID_Documento                  INT NULL,
        Nome_Companhia                VARCHAR(100) NULL,
        Valor_Mobiliario              VARCHAR(100) NULL,
        Versao                        SMALLINT NULL,
        Especie_Acao                  VARCHAR(100) NULL,
        Valor_Preco_Medio             DECIMAL(18,2) NULL,
        Escala_Cotacao                VARCHAR(100) NULL,
        Data_Aquisicao                DATE NULL,
        Descricao_Valor_Mobiliario    VARCHAR(30) NULL,
        Tipo_Classe_Acao_Preferencial VARCHAR(100) NULL,
        Percentual_Circulacao         DECIMAL(18,6) NULL,
        Quantidade_Valor_Mobiliario   DECIMAL(18,0) NULL,
        Ano                           SMALLINT       NULL,
        ImportacaoId                  BIGINT         NULL,
        ArquivoOrigem                 NVARCHAR(260)  NULL,
        CnpjNum                       AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                DATETIME2      NOT NULL CONSTRAINT DF_Fre_ValorMobiliarioTesourariaUltimoExercicio_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_ValorMobiliarioTesourariaUltimoExercicio_CnpjAno' AND object_id = OBJECT_ID('fre.ValorMobiliarioTesourariaUltimoExercicio'))
    CREATE INDEX IX_Fre_ValorMobiliarioTesourariaUltimoExercicio_CnpjAno ON fre.ValorMobiliarioTesourariaUltimoExercicio(CnpjNum, Ano);
GO

-- Volume Valor Mobiliario (volume_valor_mobiliario)
IF OBJECT_ID('fre.VolumeValorMobiliario', 'U') IS NULL
BEGIN
    CREATE TABLE fre.VolumeValorMobiliario(
        Id                               BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fre_VolumeValorMobiliario PRIMARY KEY,
        Classe_Acao_Preferencial         VARCHAR(100) NULL,
        CNPJ_Companhia                   VARCHAR(20) NULL,
        Data_Fim_Exercicio_Social        DATE NULL,
        Data_Inicio_Exercicio_Social     DATE NULL,
        Data_Referencia                  DATE NULL,
        ID_Documento                     INT NULL,
        Nome_Companhia                   VARCHAR(100) NULL,
        Valor_Mobiliario                 VARCHAR(100) NULL,
        Versao                           SMALLINT NULL,
        Valor_Maior_Cotacao              DECIMAL(18,2) NULL,
        Especie_Acao                     VARCHAR(100) NULL,
        Valor_Volume_Negociado           DECIMAL(18,2) NULL,
        Valor_Menor_Cotacao              DECIMAL(18,2) NULL,
        Escala_Cotacao                   VARCHAR(22) NULL,
        Entidade_Administradora_Mercado  VARCHAR(100) NULL,
        Mercado_Valor_Mobiliario         VARCHAR(100) NULL,
        Valor_Cotacao_Media              DECIMAL(18,2) NULL,
        Descricao_Outro_Valor_Mobiliario VARCHAR(100) NULL,
        Data_Fim_Trimestre               DATE NULL,
        Ano                              SMALLINT       NULL,
        ImportacaoId                     BIGINT         NULL,
        ArquivoOrigem                    NVARCHAR(260)  NULL,
        CnpjNum                          AS (REPLACE(REPLACE(REPLACE(REPLACE(CNPJ_Companhia, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED,
        ImportadoEmUtc                   DATETIME2      NOT NULL CONSTRAINT DF_Fre_VolumeValorMobiliario_Imp DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_VolumeValorMobiliario_CnpjAno' AND object_id = OBJECT_ID('fre.VolumeValorMobiliario'))
    CREATE INDEX IX_Fre_VolumeValorMobiliario_CnpjAno ON fre.VolumeValorMobiliario(CnpjNum, Ano);
GO

