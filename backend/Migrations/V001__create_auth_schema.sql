-- V001: cria o schema [seg] (seguranca) com perfis, usuarios e log de atividades.
-- Idempotente: cada objeto so e criado se ainda nao existir.
-- Lotes separados por GO (o runner divide e executa um por vez).

IF SCHEMA_ID('seg') IS NULL
    EXEC('CREATE SCHEMA seg');
GO

-- Perfil de acesso (lookup). Dois perfis fixos: GESTOR e OPERADOR.
IF OBJECT_ID('seg.Perfil', 'U') IS NULL
BEGIN
    CREATE TABLE seg.Perfil(
        Id     INT          NOT NULL PRIMARY KEY,
        Codigo NVARCHAR(20) NOT NULL UNIQUE,
        Nome   NVARCHAR(60) NOT NULL
    );
END
GO

-- Usuarios internos da aplicacao.
IF OBJECT_ID('seg.Usuario', 'U') IS NULL
BEGIN
    CREATE TABLE seg.Usuario(
        Id                 BIGINT IDENTITY PRIMARY KEY,
        Login              NVARCHAR(60)  NOT NULL,
        NomeCompleto       NVARCHAR(160) NOT NULL,
        Email              NVARCHAR(160) NULL,
        SenhaHash          NVARCHAR(400) NOT NULL,
        PerfilId           INT           NOT NULL REFERENCES seg.Perfil(Id),
        Ativo              BIT           NOT NULL CONSTRAINT DF_Usuario_Ativo DEFAULT 1,
        MustChangePassword BIT           NOT NULL CONSTRAINT DF_Usuario_MustChange DEFAULT 0,
        DataCriacaoUtc     DATETIME2     NOT NULL CONSTRAINT DF_Usuario_Criacao DEFAULT SYSUTCDATETIME(),
        CriadoPor          NVARCHAR(60)  NULL,
        UltimoLoginUtc     DATETIME2     NULL,
        CONSTRAINT UQ_Usuario_Login UNIQUE (Login)
    );
END
GO

-- Log de atividades dos usuarios (auditoria).
IF OBJECT_ID('seg.LogAtividade', 'U') IS NULL
BEGIN
    CREATE TABLE seg.LogAtividade(
        Id          BIGINT IDENTITY PRIMARY KEY,
        UsuarioId   BIGINT        NULL REFERENCES seg.Usuario(Id),
        Login       NVARCHAR(60)  NULL,
        Acao        NVARCHAR(40)  NOT NULL,
        Entidade    NVARCHAR(60)  NULL,
        Detalhe     NVARCHAR(MAX) NULL,
        IpAddress   NVARCHAR(60)  NULL,
        UserAgent   NVARCHAR(400) NULL,
        CriadoEmUtc DATETIME2     NOT NULL CONSTRAINT DF_LogAtividade_CriadoEm DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LogAtividade_Usuario' AND object_id = OBJECT_ID('seg.LogAtividade'))
    CREATE INDEX IX_LogAtividade_Usuario ON seg.LogAtividade(UsuarioId, CriadoEmUtc);
GO

-- Seed dos perfis (idempotente via MERGE).
MERGE seg.Perfil AS alvo
USING (VALUES
    (1, 'GESTOR',   'Gestor'),
    (2, 'OPERADOR', 'Operador')
) AS origem (Id, Codigo, Nome)
ON alvo.Id = origem.Id
WHEN MATCHED THEN
    UPDATE SET Codigo = origem.Codigo, Nome = origem.Nome
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Id, Codigo, Nome) VALUES (origem.Id, origem.Codigo, origem.Nome);
GO

-- Seed do gestor padrao (bootstrap). Senha inicial: Austral@123 (PBKDF2-HMACSHA256,
-- formato iteracoes.saltBase64.hashBase64). MustChangePassword=1 forca a troca no
-- primeiro acesso. So e inserido se ainda nao houver nenhum usuario cadastrado.
IF NOT EXISTS (SELECT 1 FROM seg.Usuario)
BEGIN
    INSERT INTO seg.Usuario (Login, NomeCompleto, Email, SenhaHash, PerfilId, Ativo, MustChangePassword, CriadoPor)
    VALUES (
        'admin',
        'Gestor Padrao',
        NULL,
        '100000.2e/ienW6JCJGptuEQnjrKQ==.Zu3WLptkH0a8yemO7Kpv9msewSaEHXOayNe1zMYrjCQ=',
        1, 1, 1, 'sistema'
    );
END
GO
