# Austral — Credit Analytics IA (Backend)

API em **ASP.NET Core 9** que conecta ao **SQL Server** (via Dapper) e serve o frontend (`index.html`).
Como navegadores não conectam diretamente ao SQL Server, esta camada intermediária:

- monta e **persiste a connection string cifrada** (ASP.NET Data Protection);
- expõe um **teste de conexão em etapas** com latências reais (rede, autenticação, banco, app);
- aplica **migrações versionadas** no startup (schema `seg`), de forma idempotente;
- consulta as **análises de crédito** da base existente do cliente via Dapper, com uma query **configurável** (`Credito:Query`);
- permite **configurar e testar provedores de IA** (OpenAI / Anthropic / compatíveis OpenAI);
- serve o `index.html` em `wwwroot`, evitando CORS.

## Requisitos

- **.NET SDK 9.0** (o projeto tem como alvo `net9.0`).
- Acesso de rede a uma instância SQL Server (configurável depois, pela tela).

## Como rodar

Na pasta `backend/`:

```bash
dotnet run
```

A API sobe em `http://localhost:5090` e **serve o frontend** na raiz:

```
http://localhost:5090/
```

O passo de build copia automaticamente o `index.html` da raiz do repositório para `wwwroot/`
(alvo MSBuild `CopyFrontend`), então **edite sempre o `index.html` da raiz**.

## Primeiro acesso (bootstrap)

1. A aplicação sobe **sem banco configurado**; a tela de **Login** indica que é preciso configurar a conexão.
2. Como ainda não há conexão, `/api/config` fica acessível **sem login**. Configure a conexão SQL pela tela.
   (Para o setup inicial você pode usar diretamente os endpoints `GET/POST /api/config` enquanto não há login.)
3. Ao **salvar a conexão**, as migrações criam o schema `seg` (auth) e semeiam o gestor padrão.
4. Faça login com o gestor padrão:
   - **Usuário:** `admin`
   - **Senha inicial:** `Austral@123` (troca obrigatória no primeiro acesso)
5. Configure `Credito:Query` em `appsettings.json` apontando para a tabela/view de análises de crédito da sua base.
6. O dashboard de **Análise de Crédito** carrega os dados de `GET /api/credito`.

> Em produção, o ideal é já fornecer `ConnectionStrings:Default` no deploy; assim o gestor padrão é
> semeado no startup e o login funciona de imediato.

## Endpoints

| Método | Rota                              | Descrição                                                        |
|--------|-----------------------------------|------------------------------------------------------------------|
| GET    | `/api/config`                     | Config salva com **senha mascarada** + flag `isConfigured`.      |
| POST   | `/api/config`                     | Valida e **salva** a connection string cifrada.                  |
| POST   | `/api/config/test`                | Testa em etapas: `{ net, auth, db, app, totalMs, ok, message }`. |
| POST   | `/api/auth/login`                 | Autentica e devolve `{ token, expiraEmUtc, usuario }`.           |
| GET    | `/api/auth/me`                    | Dados do usuário autenticado.                                    |
| POST   | `/api/auth/change-password`       | Troca a senha do próprio usuário.                               |
| POST   | `/api/auth/logout`                | Registra o logout.                                              |
| GET    | `/api/credito`                    | Análises de crédito. Filtros: `dataInicial`, `dataFinal`, `status`. |
| GET    | `/api/usuarios`                   | Lista usuários. **Somente Gestor.**                             |
| POST   | `/api/usuarios`                   | Cadastra usuário. **Somente Gestor.**                           |
| PUT    | `/api/usuarios/{id}/status`       | Ativa/desativa usuário. **Somente Gestor.**                     |
| POST   | `/api/usuarios/{id}/reset-senha`  | Reseta a senha de um usuário. **Somente Gestor.**               |
| GET    | `/api/ia/config`                  | Config de IA com chaves mascaradas. **Somente Gestor.**         |
| POST   | `/api/ia/config`                  | Salva a config de IA (chaves cifradas). **Somente Gestor.**     |
| POST   | `/api/ia/config/test`             | Valida a chave do provedor ativo. **Somente Gestor.**           |
| GET    | `/api/dfp/tipos`                  | Demonstrações DFP suportadas (`tipo`, `tabela`, `descrição`).    |
| POST   | `/api/dfp/importar/{tipo}`        | Importa um CSV da CVM (campo multipart `arquivo`) para a demonstração. |
| GET    | `/api/dfp/{cnpj}/estrutura`       | Mapa da estrutura por CNPJ: documentos + contagem por demonstração. |
| GET    | `/api/dfp/{cnpj}/contas`          | Contas consolidadas do CNPJ. Filtros: `tipo`, `dtRefer` (yyyyMMdd), `ordem`. |

## Configuração da consulta (`Credito:Query`)

Os dados de crédito vêm da **base existente do cliente** — a aplicação **não cria nem semeia**
tabelas de crédito. A consulta do dashboard é **obrigatória** e configurável em `appsettings.json`,
apontando para a tabela/view do seu banco. Enquanto não configurada, `GET /api/credito` retorna
`409` com uma mensagem orientando a configuração.

As colunas retornadas devem casar (case-insensitive) com as propriedades de `AnaliseCreditoRecord`
(`Id`, `DataAnalise`, `Tomador`, `Documento`, `ValorSolicitado`, `ScoreCredito`, `Rating`,
`ProbabilidadeInadimplencia`, `Status`, `Analista`). A query passa pelo guard
somente-leitura (`SqlReadOnlyGuard`); os filtros de período/status são aplicados no servidor.

```json
{
  "Credito": {
    "CommandTimeoutSeconds": 60,
    "Query": "SELECT Id, DataAnalise, Tomador, Documento, ValorSolicitado, ScoreCredito, Rating, ProbabilidadeInadimplencia, Status, Analista FROM dbo.SuaTabelaDeCredito ORDER BY DataAnalise DESC"
  }
}
```

## Connection string via `appsettings` (`ConnectionStrings:Default`)

Além da tela de **Configurações**, é possível informar uma **connection string crua** em `appsettings.json`.
Quando preenchida, ela tem **prioridade** sobre a config salva pela tela. Por ser crua, atributos como
`Application Name`/`ApplicationIntent=ReadOnly` não são injetados automaticamente — inclua-os se desejar.

## Segurança

- **Segredos cifrados em repouso** (connection string e chaves de IA) via `IDataProtector`; as chaves de
  proteção ficam em `App_Data/keys/`. As senhas/chaves **nunca** retornam em texto plano (apenas máscaras).
- **Autenticação JWT** (HMAC-SHA256). Defina `Auth:JwtKey` em produção; se vazio, é gerada e persistida em
  `App_Data/jwt.key`.
- **Perfis**: `GESTOR` (acesso total, incluindo Configurações, Usuários e IA) e `OPERADOR` (apenas o dashboard).
- **Somente leitura no dashboard**: `Credito:Query` passa pelo `SqlReadOnlyGuard` e a leitura usa
  `ApplicationIntent=ReadOnly`. Conceda ao login da API apenas `db_datareader` (+ escrita nos schemas internos
  se for usar migração/auditoria/usuários).
- Consultas **parametrizadas** via Dapper; erros retornam mensagens genéricas (detalhes só nos logs).
- `App_Data/` e `*.dat` estão no `.gitignore` (segredos e artefatos não entram no controle de versão).

## Migrações (`Migrations/*.sql`)

Embutidas no assembly e aplicadas no startup (idempotentes), com auditoria em `seg.__SchemaVersions`:

- `V001__create_auth_schema.sql` — schema `seg` (Perfil, Usuario, LogAtividade) + gestor padrão.
- `V002__create_dfp_dre.sql` — schema `dfp` + tabela `dfp.Dre` (DRE).
- `V003__create_dfp_documento.sql` — `dfp.Documento` (índice de documentos; entrada do rastreio por CNPJ).
- `V004__create_dfp_demonstracoes.sql` — `Bpa`, `Bpp`, `Dra`, `Dva`, `DfcMd`, `DfcMi`, `Dmpl` + `CnpjNum` no `Dre`.
- `V005__create_dfp_complementares.sql` — `ComposicaoCapital`, `Parecer`.
- `V006__create_dfp_views.sql` — `dfp.vw_Conta` (contas consolidadas) e `dfp.vw_Estrutura` (resumo por documento).

A aplicação **não cria** tabelas de crédito: os dados de análise vêm da base do cliente via `Credito:Query`.
Alterações na configuração de IA são registradas no log de atividade (`seg.LogAtividade`).

## Estrutura DFP (CVM) — rastreio por CNPJ + importação de CSV

O schema `dfp` espelha **1:1** os layouts dos arquivos abertos da CVM (DFP), preservando os nomes
nativos das colunas para permitir carga direta dos CSVs. Cada tabela tem uma coluna calculada e
persistida `CnpjNum` (CNPJ só com dígitos) indexada, que é o ponto de entrada do **rastreio por CNPJ**
independentemente da formatação (`00.000.000/0001-00` ou `00000000000100`).

| Demonstração            | Tabela                   | Arquivo CVM (`meta_dfp_cia_aberta_*`)         |
|-------------------------|--------------------------|------------------------------------------------|
| Índice de documentos    | `dfp.Documento`          | `meta_dfp_cia_aberta.txt`                       |
| DRE                     | `dfp.Dre`                | `..._DRE.txt`                                    |
| Balanço Ativo (BPA)     | `dfp.Bpa`                | `..._BPA.txt`                                    |
| Balanço Passivo (BPP)   | `dfp.Bpp`                | `..._BPP.txt`                                    |
| Resultado Abrangente    | `dfp.Dra`                | `..._DRA.txt`                                    |
| Valor Adicionado        | `dfp.Dva`                | `..._DVA.txt`                                    |
| Fluxo de Caixa (Direto) | `dfp.DfcMd`              | `..._DFC_MD.txt`                                 |
| Fluxo de Caixa (Indir.) | `dfp.DfcMi`              | `..._DFC_MI.txt`                                 |
| Mutações do PL (DMPL)   | `dfp.Dmpl`              | `..._DMPL.txt` (inclui `COLUNA_DF`)             |
| Composição do Capital   | `dfp.ComposicaoCapital`  | `..._composicao_capital.txt`                    |
| Parecer / Declaração    | `dfp.Parecer`            | `..._parecer.txt`                               |

Chave de amarração entre as demonstrações: `CNPJ_CIA` + `DT_REFER` + `VERSAO`.

### Importação dos CSVs

`POST /api/dfp/importar/{tipo}` recebe o arquivo no campo multipart `arquivo` e grava em massa
(`SqlBulkCopy`) na tabela correspondente. O leitor assume o padrão da CVM: separador `;`,
encoding **ISO-8859-1** e cabeçalho com os nomes nativos dos campos. As colunas são mapeadas
**pelo nome do cabeçalho** (colunas desconhecidas são ignoradas; ausentes ficam nulas), então a
ordem das colunas no CSV não importa. Datas em `yyyy-MM-dd` e `VL_CONTA` com ponto decimal.

```bash
curl -X POST "http://localhost:5090/api/dfp/importar/DRE" \
  -H "Authorization: Bearer <token>" \
  -F "arquivo=@dfp_cia_aberta_DRE_2023.csv"
```

### Leitura por CNPJ

```bash
# Mapa da estrutura (documentos + contagem por demonstração)
curl "http://localhost:5090/api/dfp/00000000000191/estrutura" -H "Authorization: Bearer <token>"

# Contas consolidadas (filtros opcionais: tipo, dtRefer, ordem)
curl "http://localhost:5090/api/dfp/00000000000191/contas?tipo=DRE&ordem=ULTIMO" -H "Authorization: Bearer <token>"
```

## Estrutura

```
backend/
├─ AustralCreditAnalytics.Api.csproj   # net9.0, CopyFrontend + EmbeddedResource Migrations\*.sql
├─ Program.cs                          # DI, Data Protection, JWT, auto-migração
├─ appsettings.json                    # Auth / Credito:Query / Ia
├─ Controllers/                        # Auth, Config, Credito, Usuarios, Ia
├─ Models/                             # Connection*, Auth/*, Credito/*, Ia/*
├─ Services/                           # factory, store, tester, migration runner, repos, IA
├─ Migrations/                         # V001 (auth, embutida)
└─ wwwroot/                            # index.html copiado da raiz no build
```
