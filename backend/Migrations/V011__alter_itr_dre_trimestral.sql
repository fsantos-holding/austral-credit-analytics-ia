-- V011: amplia itr.DreTrimestral para suportar a leitura confiavel por periodo (DRE v2).
-- O motor de trimestralizacao (TrimestralizacaoService) passa a:
--   - normalizar a escala/moeda para R$ ANTES da de-acumulacao (D3); EscalaMoeda fica 'UNIDADE';
--   - propagar ST_CONTA_FIXA e marcar BaixaComparabilidade para contas livres (D5);
--   - marcar MoedaEstrangeira quando MOEDA <> 'REAL' (D3);
--   - marcar ExercicioNaoCalendario quando o fechamento nao cai em 03/06/09/12 (D4).
-- Acrescenta as 4 colunas de forma idempotente, sem tocar na UQ_Itr_DreTrimestral
-- (CD_CVM, Conjunto, Ano, Trimestre, CD_CONTA), e recria itr.vw_DreTrimestral expondo-as.
-- Idempotente. Lotes separados por GO.

-- ST_CONTA_FIXA: indica se a conta e fixa (S) ou livre (N) na taxonomia da CVM.
IF COL_LENGTH('itr.DreTrimestral', 'ST_CONTA_FIXA') IS NULL
    ALTER TABLE itr.DreTrimestral ADD ST_CONTA_FIXA VARCHAR(1) NULL;
GO

-- MoedaEstrangeira: 1 quando MOEDA <> 'REAL' (valor nao normalizavel para R$).
IF COL_LENGTH('itr.DreTrimestral', 'MoedaEstrangeira') IS NULL
    ALTER TABLE itr.DreTrimestral
        ADD MoedaEstrangeira BIT NOT NULL CONSTRAINT DF_Itr_DreTri_MoedaEstr DEFAULT 0;
GO

-- ExercicioNaoCalendario: 1 quando o fechamento nao cai em 03/06/09/12 (D4).
IF COL_LENGTH('itr.DreTrimestral', 'ExercicioNaoCalendario') IS NULL
    ALTER TABLE itr.DreTrimestral
        ADD ExercicioNaoCalendario BIT NOT NULL CONSTRAINT DF_Itr_DreTri_ExNaoCal DEFAULT 0;
GO

-- BaixaComparabilidade: 1 para contas livres (ST_CONTA_FIXA = N), cujo CD_CONTA pode
-- mudar entre periodos e nao deve ser comparado diretamente (D5).
IF COL_LENGTH('itr.DreTrimestral', 'BaixaComparabilidade') IS NULL
    ALTER TABLE itr.DreTrimestral
        ADD BaixaComparabilidade BIT NOT NULL CONSTRAINT DF_Itr_DreTri_BaixaCmp DEFAULT 0;
GO

-- View de leitura amigavel: expoe as novas colunas de flag/escala. O dicionario de
-- contas continua aplicado no frontend; os valores ja chegam em R$ (EscalaMoeda='UNIDADE').
CREATE OR ALTER VIEW itr.vw_DreTrimestral AS
    SELECT
        CD_CVM, CnpjNum, DENOM_CIA, Ano, Trimestre, Conjunto,
        CD_CONTA, DS_CONTA, ST_CONTA_FIXA,
        ValorTrimestral, ValorAcumulado, OrigemTrimestre, EscalaMoeda,
        MoedaEstrangeira, ExercicioNaoCalendario, BaixaComparabilidade,
        Inconsistente, MotivoInconsistencia, CalculadoEmUtc
    FROM itr.DreTrimestral;
GO
