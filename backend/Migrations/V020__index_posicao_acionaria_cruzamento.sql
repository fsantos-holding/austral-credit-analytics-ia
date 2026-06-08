-- V020: suporte de performance ao Dashboard de Acionistas Cruzados.
-- Adiciona a coluna computada persistida fre.PosicaoAcionaria.DocAcionistaNum (apenas os
-- digitos de CPF_CNPJ_Acionista) e indices que servem o cruzamento de acionistas entre
-- companhias (chave do acionista) e o snapshot da ultima Ano/Versao por empresa.
-- Idempotente; lotes separados por GO. Mesmo padrao das migrations FRE anteriores.

-- Coluna computada com apenas os digitos do CPF/CNPJ do acionista (espelha o tratamento de CnpjNum).
IF COL_LENGTH('fre.PosicaoAcionaria', 'DocAcionistaNum') IS NULL
    ALTER TABLE fre.PosicaoAcionaria
        ADD DocAcionistaNum AS (REPLACE(REPLACE(REPLACE(REPLACE(CPF_CNPJ_Acionista, '.', ''), '/', ''), '-', ''), ' ', '')) PERSISTED;
GO

-- Cruzamento por acionista: localiza rapidamente todas as empresas de um mesmo documento.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_PosicaoAcionaria_DocAcionista' AND object_id = OBJECT_ID('fre.PosicaoAcionaria'))
    CREATE INDEX IX_Fre_PosicaoAcionaria_DocAcionista ON fre.PosicaoAcionaria(DocAcionistaNum);
GO

-- Snapshot da ultima Ano/Versao por empresa + colunas cobertas (evita key lookup no ranking/insights).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fre_PosicaoAcionaria_Snapshot' AND object_id = OBJECT_ID('fre.PosicaoAcionaria'))
    CREATE INDEX IX_Fre_PosicaoAcionaria_Snapshot ON fre.PosicaoAcionaria(CnpjNum, Ano, Versao)
        INCLUDE (DocAcionistaNum, Acionista, Tipo_Pessoa_Acionista, Acionista_Controlador,
                 Percentual_Total_Acoes_Circulacao, Percentual_Acao_Ordinaria_Circulacao,
                 Percentual_Acao_Preferencial_Circulacao, Nome_Companhia);
GO
