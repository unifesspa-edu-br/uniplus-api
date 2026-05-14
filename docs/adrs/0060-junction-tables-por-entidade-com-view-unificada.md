---
status: "accepted"
date: "2026-05-14"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0060: Junction tables por entidade com view unificada para AreasDeInteresse

## Contexto e enunciado do problema

[ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md) estabelece o Pattern 3 — junction table com validade temporal para `AreasDeInteresse` — como mecanismo de armazenamento do modelo de visibilidade por áreas. A ADR especificou "junction table" mas deixou em aberto se devíamos usar uma única tabela polimórfica ou uma tabela por entidade. Com 9 entidades de catálogo distribuídas em 3 bancos PostgreSQL isolados (per [ADR-0054](0054-naming-convention-e-strategy-migrations.md) e `docs/guia-banco-de-dados.md`), a escolha de topologia tem consequências materiais.

Uma única tabela polimórfica cruzando as 9 entidades é **tecnicamente impossível** porque elas vivem em 3 bancos isolados (`uniplus_parametrizacao`, `uniplus_selecao`, `uniplus_organizacao`) com usuários e connection strings distintos. Tabela polimórfica não consegue declarar foreign key para tabelas em outro banco.

As opções remanescentes:

- **Opção B**: uma junction table por entidade de catálogo, com FK real ao pai. Total ~9 tabelas.
- **Opção C**: uma tabela polimórfica por DbContext (3 no total), com discriminador `item_type` e sem FK.
- **Opção D**: Opção B (FK por entidade) mais uma SQL view por DbContext que unifica consultas cross-catálogo para leituras administrativas.

Council (Architect + Pragmatic Engineer + Devil's Advocate) chegou a consenso na Opção D como a forma correta — Architect e Pragmatic convergem em tabelas por entidade (B) para o modelo de escrita, e Devil's Advocate acrescenta a view unificada como reforço do lado de leitura.

Argumentos principais do council:

- **Architect**: enforcement de FK não é negociável para a verdade de snapshot/histórico; discriminador polimórfico derrota exclusion constraints do PostgreSQL; ter ~9 tabelas é feature (evolução independente); template via `CatalogVisibilityConfiguration<T>` resolve a boilerplate.
- **Pragmatic Engineer**: "9 vs 3 tabelas" é métrica de vaidade; o que grita às 3 da manhã é a integridade da FK; debug no plantão ganha de cardinalidade de schema.
- **Devil's Advocate**: por-entidade está certo no write, mas reads cross-catálogo (relatórios LGPD, "tudo visível para o CEPS em data X") viram 9 UNIONs em 9 sites distintos; entregar Opção D = B + view unificada já no dia 1, porque adicionar a view depois quando já há dados custa mais; desenhar a view com coluna `module_id` desde o início para acomodar módulos futuros.

## Drivers da decisão

- **Integridade referencial não negociável** para verdade de snapshot/histórico exigida por evidência judicial.
- **Operabilidade no plantão**: orphans cross-table viram bug noturno se discriminador for typo silencioso.
- **Reads cross-catálogo são cenário real** (LGPD, audit) e não devem virar 9 UNIONs por call site.
- **Bounded reference data**: cardinalidade pequena ≤ 500 linhas por tabela permite GIST exclusion sem custo.
- **Forward-compat**: a view precisa acomodar `Ingresso`, `Auxilio` etc. sem refactor mais tarde.

## Opções consideradas

- **A**: Tabela polimórfica única cruzando módulos (descartada como impossível pela isolation).
- **B**: Uma junction table por entidade.
- **C**: Tabela polimórfica por DbContext (3 no total).
- **D**: Por-entidade (B) + view unificada por DbContext (escolhida).

## Resultado da decisão

**Escolhida:** "D — junction tables por entidade no write, view unificada por DbContext no read", porque combina FK enforcement no nível do banco (Architect + Pragmatic) com economia de UNIONs no read cross-catálogo (Devil's Advocate), sem violar a isolation entre bancos.

### Write model — junction tables por entidade

Cada entidade de catálogo é dona da sua junction table de `AreasDeInteresse`. Total 9 tabelas em 3 bancos:

**Banco `uniplus_organizacao`**:

- (nenhuma — `AreaOrganizacional` não tem `AreasDeInteresse`; ela **é** a área, não é visível-para-áreas).

**Banco `uniplus_parametrizacao`**:

- `modalidade_areas_de_interesse`
- `necessidade_especial_areas_de_interesse`
- `tipo_documento_areas_de_interesse`
- `endereco_areas_de_interesse`

**Banco `uniplus_selecao`** (estende o existente):

- `tipo_edital_areas_de_interesse`
- `tipo_etapa_areas_de_interesse`
- `criterio_desempate_areas_de_interesse`
- `local_prova_areas_de_interesse`
- `obrigatoriedade_legal_areas_de_interesse`

Todas têm a mesma forma:

```sql
{entity}_areas_de_interesse(
  {entity}_id        uuid    NOT NULL REFERENCES {entity}(id) ON DELETE RESTRICT,
  area_codigo        varchar(32) NOT NULL,
  valid_from         timestamptz NOT NULL,
  valid_to           timestamptz NULL,                          -- NULL = ativa
  added_by           varchar(255) NOT NULL,                     -- sub do admin
  PRIMARY KEY ({entity}_id, area_codigo, valid_from),
  EXCLUDE USING GIST (
    {entity}_id WITH =,
    area_codigo WITH =,
    tstzrange(valid_from, valid_to, '[)') WITH &&
  )                                                              -- impede sobreposição de janelas
);

CREATE INDEX ix_{entity}_areas_active
  ON {entity}_areas_de_interesse ({entity}_id, area_codigo)
  WHERE valid_to IS NULL;
```

**EF Core**: configuração templatada via classe base `CatalogVisibilityConfiguration<TParent>` em `Unifesspa.UniPlus.Infrastructure.Core.Persistence.Configurations`. Cada `IEntityTypeConfiguration` de catálogo invoca a base. Boilerplate por catálogo cai para ~5 linhas.

### Read model — view unificada por DbContext

Cada DbContext expõe uma view que faz `UNION ALL` das suas junctions, com discriminador de tipo e identificador de módulo:

**`parametrizacao.vw_catalog_area_visibility`**:

```sql
CREATE VIEW vw_catalog_area_visibility AS
SELECT 'parametrizacao' AS modulo, 'modalidade' AS item_type, modalidade_id AS item_id,
       area_codigo, valid_from, valid_to, added_by
FROM modalidade_areas_de_interesse
UNION ALL
SELECT 'parametrizacao', 'necessidade_especial', necessidade_especial_id,
       area_codigo, valid_from, valid_to, added_by
FROM necessidade_especial_areas_de_interesse
UNION ALL
-- tipo_documento, endereco
;
```

**`selecao.vw_catalog_area_visibility`**: análoga para os 5 catálogos do Selecao com `modulo = 'selecao'`.

A view suporta consultas tipo "mostre tudo visível para CEPS em 2026-04-12" com 1 query por DbContext em vez de 4-5 UNIONs inline em cada call site.

### Agregação cross-banco (quando precisar)

Para consultas genuinamente cross-módulo ("todo catálogo onde CRCA teve visibilidade em 2026, em toda a plataforma"), um agregador de aplicação consulta cada banco via Wolverine e combina os resultados. É hot-path frio (relatórios LGPD), não path quente. O agregador vai em `Unifesspa.UniPlus.Audit.Application` (módulo Audit futuro, post-V1) ou em script SQL em `tools/audit/` para V1.

### Enforcement do exclusion constraint

`EXCLUDE USING GIST` exige a extensão `btree_gist` do PostgreSQL. A primeira migration de cada DbContext novo adiciona:

```sql
CREATE EXTENSION IF NOT EXISTS btree_gist;
```

### Integração com snapshot do ADR-0057

Quando `Edital.Publicar()` roda (Pattern 1 de [ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md)), o snapshot lê da **view unificada** para os catálogos referenciados pelo edital, materializando o estado temporal de `AreasDeInteresse`. A tabela `edital_governance_snapshot` armazena as tuplas resolvidas por hash para evidência legal.

## Consequências

### Positivas

- FK enforcement no banco pega violações antes que corrompam histórico de auditoria.
- `tstzrange` + GIST impedem janelas de validade sobrepostas para o mesmo `(pai, área)`.
- Consultas cross-catálogo passam pela view unificada — 1 query por DbContext com índice composto `(area_codigo, valid_from, valid_to)`.
- Tabelas por entidade evoluem independentemente; novo catálogo é 1 migration + 1 chamada à config base.
- `CatalogVisibilityConfiguration<T>` torna o overhead de 9 tabelas barato (preocupação do Pragmatic resolvida).
- Forward-compat: coluna `modulo` na view acomoda módulos futuros (Ingresso, Auxilio etc.).

### Negativas

- 9 junction tables + 2 views vs 3 polimórficas.
- Extensão `btree_gist` precisa estar provisionada (pequena mudança de infra, inclusa na primeira migration).
- Quando um catálogo novo entra, a view precisa ser atualizada (`CREATE OR REPLACE VIEW` em migration).

## Confirmação

- **Risco**: catálogo novo é adicionado e esquecem de atualizar a view.
  **Mitigação**: fitness test ArchUnitNET — toda entidade que herda `CatalogEntityBase` (marker) deve aparecer em UNION na view correspondente. Teste lê DDL da view em runtime e quebra o build se faltar.
- **Risco**: performance do GIST exclusion em escala.
  **Mitigação**: catálogos são bounded reference data (≤ 100 linhas por entidade); junctions ≤ 100 × ~5 áreas = ≤ 500 por tabela. Negligível.
- **Risco**: view fica lenta se uma junction crescer desproporcionalmente.
  **Mitigação**: é `UNION ALL` (sem DISTINCT), pushdown para índices subjacentes; se necessário, `MATERIALIZED VIEW` refrescada por evento Kafka (defer até dor empírica).
- **Risco**: devs copy-pasteiam config de junction e divergem (cheiro "mês 18" do Devil's Advocate).
  **Mitigação**: base `CatalogVisibilityConfiguration<T>` centraliza o boilerplate; fitness test confirma que toda config usa a base.

## Prós e contras das opções

### A — Tabela polimórfica única cruzando módulos

- **Prós**: 1 config única; cross-catálogo trivial.
- **Contras**: Arquiteturalmente inadmissível com 3 bancos isolados. Mesmo em 1 banco, sem FK, exclusion defeated.
- **Por que rejeitada**: viola a isolation policy do [ADR-0054](0054-naming-convention-e-strategy-migrations.md).

### C — Polimórfica por DbContext (3 no total)

- **Prós**: 3 tabelas em vez de 9; 1 config EF por DbContext.
- **Contras**: sem FK; orphans inevitáveis; typo em `item_type` compila e queima em prod; exclusion para janelas não-sobrepostas falha; junior dev não lê resolução polimórfica em 90 segundos; reconstrução LGPD por `(item_type, item_id)` composite é frágil.
- **Por que rejeitada**: council unânime no FK enforcement; "9 vs 3 tabelas" é vaidade; failure modes de junctions polimórficas em catálogos de produção são anti-pattern bem documentado.

### B (sozinha) — sem view unificada

- **Prós**: marginalmente mais simples.
- **Contras**: cross-catálogo explode em 4-5 UNIONs por call site; "todos os itens visíveis para CEPS em data X" vira range scan temporal 9-vias que o otimizador não pré-agrega.
- **Por que rejeitada**: adicionar view depois custa mais que adicionar agora (Devil's Advocate decisivo).

### D — Por-entidade + view unificada (escolhida)

- **Prós**: discussão acima.
- **Contras**: discussão acima.

## Mais informações

- [ADR-0056](0056-parametrizacao-modulo-e-read-side-carve-out.md) — Módulo Parametrizacao e carve-out read-side.
- [ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md) — RBAC por áreas, Pattern 3 detalhado nesta ADR.
- [ADR-0059](0059-sprint-3-decomposicao-estrategia-paralela.md) — Decomposição Sprint 3 (Phase 1 inclui essa infra).
- [ADR-0054](0054-naming-convention-e-strategy-migrations.md) — snake_case + isolation 3-DB.
- Documentação do PostgreSQL `btree_gist` extension.
- DAMA-DMBOK 2 — Padrões de temporal validity.
