---
status: "accepted"
date: "2026-05-14"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0057: RBAC por áreas com snapshot, histórico e invariantes auditáveis

## Contexto e enunciado do problema

Toda entidade admin-editável dos catálogos cross-cutting de Parametrizacao e dos catálogos domain-specific de Selecao carrega dois campos de governança:

- `Proprietario: AreaCodigo?` — a área que administra (edita) o item. Quando `null`, o item é gerido apenas pela plataforma (CTIC).
- `AreasDeInteresse: IReadOnlySet<AreaCodigo>` — o conjunto de áreas que enxergam/usam o item. Conjunto vazio significa **global** (visível a todas as áreas).

A regra de visibilidade no GET público: o caller enxerga o item se `caller.áreas ∩ item.AreasDeInteresse ≠ ∅` OU se `item.AreasDeInteresse = ∅` (item global). A regra de escrita: o caller pode editar se `caller ∈ {area}-admin` E `{area} == item.Proprietario`, OU se o caller for `plataforma-admin` (com auditoria do tipo "on-behalf-of").

O modelo é poderoso mas tem armadilhas reais. Durante o council debate (rodada 2), o Devil's Advocate apontou seis pontos que, se ignorados, produzem o cenário de falha conhecido como **string-stuffing trap** em 18 meses — quando PROEX, PROGEP e Prefeitura demandarem governance shapes ligeiramente diferentes (sub-áreas, role-áreas, delegações com vigência) e o modelo simples absorver complexidade por proliferação de strings até forçar uma reescrita. O sponsor pediu que todos os seis pontos fossem endereçados nesta ADR, não adiados.

Os seis pontos:

1. **Drift de `AreasDeInteresse` no meio do ciclo do edital**: se um `LocalProva` é `["CEPS"]` na publicação do edital e depois muda para `["CEPS","PROEG"]`, projeções do edital publicado mudam silenciosamente. Sem snapshot.
2. **Handover de `Proprietario` sem histórico**: quando o CRCA assume um `TipoDocumento` antes do CEPS, a auditoria precisa responder "quem podia editar isso em 12/04/2026?" — uma coluna única não basta.
3. **Invariante `Proprietario ∈ AreasDeInteresse`** sem especificação explícita.
4. **Cache correctness**: o reader é cached em Redis; visibilidade depende das áreas do caller — se o cache for por-caller a chave explode em combinatória.
5. **Política conflitante em áreas compartilhadas**: CEPS quer desativar, PROEG ainda usa — `Ativo: bool` único não resolve.
6. **Fan-out de auditoria do `plataforma-admin`**: edição on-behalf-of em item compartilhado por 3 áreas — cada área precisa ver o evento.

## Drivers da decisão

- **Reprodutibilidade jurídica**: dado um mandado de segurança sobre um edital publicado em data X, a plataforma precisa reconstruir exatamente quem enxergava o quê naquele momento.
- **Closed roster sustentável**: o roster de `AreaOrganizacional` precisa permanecer fechado (per [ADR-0055](0055-organizacao-institucional-bounded-context.md)); adições viram ADR.
- **Sem reinvenção descontrolada**: invariantes não documentados divergem entre módulos durante onboarding.
- **Antecipação da armadilha "string-stuffing"**: sub-áreas, role-áreas e delegações com vigência ficam explicitamente fora de escopo V1, mas com triggers de revisão escritos.
- **Cache eficiente sob carga**: a invalidação cross-pod deve ser atômica; views per-caller cached criam combinatória inviável.

## Opções consideradas

- **A**: Endereçar parcialmente (snapshot apenas, sem histórico de `Proprietario`).
- **B**: Endereçar nenhum dos seis pontos em V1 e iterar quando aparecer dor real (YAGNI puro).
- **C**: Endereçar todos os seis pontos agora com 7 invariantes/patterns documentados (snapshot + histórico SCD Type 2 + invariante explícita + cache caller-explícito + política cooperativa + fan-out at read time + closed roster reforçado).

## Resultado da decisão

**Escolhida:** "C — endereçar todos os seis pontos agora", porque a postura legal (reprodutibilidade jurídica para mandados de segurança) e a antecipação da string-stuffing trap justificam o investimento adiantado. As estruturas (tabelas history, junction temporal, snapshots) têm custo de manutenção baixo e custo de adição posterior alto.

### Invariante 1: `Proprietario` deve estar em `AreasDeInteresse` (ou ambos vazios)

**Regra:** se `Proprietario != null`, então `Proprietario ∈ AreasDeInteresse`. Se `AreasDeInteresse = ∅` (item global), então `Proprietario = null` (apenas plataforma-admin edita globais).

**Enforcement:** validado nos construtores de domínio e nos command handlers. Retorna `Result.Failure(ProprietarioForaDeAreasDeInteresseError)` em violação. Não enforçado no nível de DB (sem check constraint cross-coluna-array); enforçado na fronteira da aplicação.

**Caso de borda:** remover a área do `Proprietario` de `AreasDeInteresse` exige que o caller também atribua novo `Proprietario` (ou ambos null/empty) no mesmo comando. Update multi-campo é atômico.

### Invariante 2: imutabilidade de `AreaCodigo` após criação

`AreaOrganizacional.Codigo` é `protected init` — não muda após construção. Para "renomear" uma área:

1. Cria nova área com novo código (ADR exigida per [ADR-0055](0055-organizacao-institucional-bounded-context.md)).
2. Migra referências via procedimento operacional documentado (script de migração de dados).
3. Soft-delete da área antiga. Referências continuam resolvendo para a área deletada (com flag `IsDeleted = true`), preservando auditoria histórica; operações novas não podem usar área deletada.

### Pattern 1: snapshot de `AreasDeInteresse` e `Proprietario` ao publicar edital

`Edital.Publicar()` snapshota a **governança em si** de cada item de catálogo referenciado pelo edital, além dos snapshots já especificados em RN08 (`docs/visao-do-projeto.md`).

O snapshot inclui:

- Para cada item de catálogo referenciado (Modalidade, LocalProva, TipoDocumento, etc.): `(item_id, item_codigo, proprietario_at_bind, areas_de_interesse_at_bind, vigencia_at_bind, hash)`.
- Armazenado em tabela `edital_governance_snapshot`, append-only.

**Motivo:** quando um candidato processa alegando "não fui aceito porque minha área foi excluída do edital", o snapshot prova exatamente qual era o conjunto `AreasDeInteresse` no momento da publicação — mesmo que tenha sido editado depois.

**Implementação:**

- Projeção `ParametrizacaoGovernanceProjection` (read-only, desnormalizada) materializa o tuple atual `(item, proprietario, areas_de_interesse)` para alta eficiência de leitura.
- `Edital.Publicar()` lê a projeção e copia para `EditalGovernanceSnapshot`.
- A consulta de auditoria usa o snapshot; a consulta operacional do dia a dia usa o estado atual.

### Pattern 2: histórico SCD Type 2 para `Proprietario`

Tabela `proprietario_historico`:

```text
item_id: Guid
proprietario_codigo: AreaCodigo
valid_from: timestamptz
valid_to: timestamptz?  -- null = linha vigente
changed_by: string
```

- Append-only.
- Nova linha sempre que `Proprietario` muda em um item.
- `valid_to = null` na linha vigente; fechar uma linha preenche `valid_to`.
- Index em `(item_id, valid_from DESC)` para consulta "dono em momento X".

**Exemplo de consulta:** "quem podia editar `TipoDocumento.HistoricoEscolar` em 12/04/2026?" — SQL: `SELECT proprietario_codigo FROM proprietario_historico WHERE item_id = ? AND ? BETWEEN valid_from AND COALESCE(valid_to, 'infinity')`.

**O mesmo pattern para histórico de `AreasDeInteresse`:** tabela `area_interesse_binding_historico(item_id, area_codigo, valid_from, valid_to, changed_by)` — uma linha por ciclo de vida de `(item, area)`. Junction table com validade temporal.

### Pattern 3: junction table com validade temporal

Per decisão do council R2 (Architect e Pragmatic concordaram): junction table, não JSON column.

```text
catalog_item_areas_de_interesse(
  item_id: Guid
  item_type: string         -- discriminador para queries cross-table
  area_codigo: AreaCodigo
  valid_from: timestamptz
  valid_to: timestamptz?    -- null = vigente
  added_by: string          -- sub do JWT do admin que adicionou
)
PRIMARY KEY (item_id, area_codigo, valid_from)
```

A junction é **temporal por construção** — adicionar/remover binding é `INSERT new row` / `UPDATE valid_to = now()`, nunca `DELETE`. Isso faz `AreaInteresseBindingHistorico` e o estado atual virarem a mesma tabela, com queries diferentes.

**Query de visibilidade** (apenas vigentes):

```sql
WHERE EXISTS (
  SELECT 1 FROM catalog_item_areas_de_interesse cai
  WHERE cai.item_id = item.id
    AND cai.valid_to IS NULL
    AND cai.area_codigo = ANY(:caller_areas)
)
OR NOT EXISTS (
  SELECT 1 FROM catalog_item_areas_de_interesse cai
  WHERE cai.item_id = item.id
    AND cai.valid_to IS NULL
)  -- item global: sem bindings = visível a todos
```

### Pattern 4: filtro de áreas com cache raw-set e parâmetro explícito

Per decisão unânime do council R2: métodos `IXxxReader` recebem `IReadOnlyCollection<AreaCodigo> areasCaller` como parâmetro. O cache armazena o conjunto raw, não-filtrado; o filtro acontece depois da retrieval.

```csharp
public sealed class ModalidadeReader : IModalidadeReader
{
    private readonly IDistributedCache _cache;
    private readonly IModalidadeRepository _repo;

    public async Task<IReadOnlyList<ModalidadeView>> ListarVisiveisAsync(
        IReadOnlyCollection<AreaCodigo> areasCaller,
        CancellationToken ct)
    {
        var rawSet = await _cache.GetOrCreateAsync(
            "parametrizacao:modalidades",
            async _ => await _repo.ListarTodosComBindingsAsync(ct));

        return rawSet
            .Where(m => m.AreasDeInteresse.Count == 0 
                       || m.AreasDeInteresse.Overlaps(areasCaller))
            .Select(m => m.ToView())
            .ToList();
    }
}
```

**A chave do cache é global, não por-caller.** O filtro é em memória depois do cache hit — O(N×M) onde N é tamanho do catálogo (≤ 100) e M é o número de áreas do caller (≤ 5). Negligível.

**Background jobs e operações `plataforma-admin`** passam `areasCaller` vazio E declaram atributo `[ExplicitlyUnscoped]` na call site:

```csharp
[ExplicitlyUnscoped(Reason = "Relatório noturno — escopo plataforma-admin")]
public async Task ExecutarRelatorioMensal(...)
{
    var todas = await _reader.ListarVisiveisAsync(
        areasCaller: [],
        ct);
}
```

Quando `areasCaller` é vazio, o reader retorna **apenas itens globais** (`AreasDeInteresse = ∅`), NÃO todos os itens. "Unscoped" por padrão significa "vê só o que é globalmente visível" — comportamento seguro por default. Para ver mesmo tudo, o caller invoca `ListarTodasIncluindoAreaEspecificasAsync(...)` — método com nome que força intent deliberada, e fitness test assere obrigatoriedade de role `plataforma-admin`.

### Pattern 5: `Ativo` único com política cooperativa documentada (ponto 5)

Para V1, `Ativo: bool` permanece coluna única. **A política cooperativa é prática operacional documentada, não enforcement técnico:**

- Antes de desativar item com múltiplas `AreasDeInteresse`, o `Proprietario` notifica as outras áreas interessadas via comunicação interna (email, ticket).
- A admin UI mostra a lista de `AreasDeInteresse` e pergunta: "este item é compartilhado com {areas}. Confirmar desativação?"
- Desativação publica `CatalogItemDesativadoEvent` via Kafka; áreas interessadas podem reagir (notificar usuários, arquivar referências).

**Adiada para ADR futura (com trigger condition):** semântica de `Ativo` por área. Trigger: 2+ incidentes documentados de conflito inter-área de desativação em janela de 6 meses.

**Justificativa:** construir ciclo de vida por-área agora é overengineering — a plataforma ainda não operou tempo suficiente para identificar quais conflitos são reais. Política cooperativa + audit trail são suficientes para V1.

### Pattern 6: fan-out de auditoria em read time (ponto 6)

Quando `plataforma-admin` (ou qualquer admin) edita um item compartilhado entre múltiplas áreas, o evento de auditoria é gravado **uma vez** na tabela canônica (`catalog_item_audit_log`). Views de auditoria por área são construídas em **read time** via JOIN sobre os bindings vigentes:

```sql
-- "Me mostre tudo que afetou itens visíveis ao CEPS na última semana"
SELECT log.*
FROM catalog_item_audit_log log
JOIN catalog_item_areas_de_interesse cai
  ON cai.item_id = log.item_id
WHERE cai.area_codigo = 'CEPS'
  AND log.changed_at >= '2026-05-06'
  AND (cai.valid_from <= log.changed_at 
       AND (cai.valid_to IS NULL OR cai.valid_to >= log.changed_at))
```

O JOIN temporal garante que o CEPS vê eventos de auditoria sobre itens que eram visíveis ao CEPS **no momento da mudança**, mesmo se a visibilidade mudou depois.

**Edições on-behalf-of do `plataforma-admin`** carregam coluna `OnBehalfOfArea: AreaCodigo?` na linha de auditoria. Quando preenchida, views de auditoria por área destacam ("plataforma-admin editou em nome do CEPS").

### Reforço de closed roster (anti string-stuffing)

Per [ADR-0055](0055-organizacao-institucional-bounded-context.md), adicionar nova `AreaOrganizacional` exige ADR + `AdrReferenceCode`. Esta ADR adiciona o corolário:

**Conceitos de sub-área, role-área e vigência-área são explicitamente fora de escopo V1.** Triggers para ADR futura:

- **Sub-área**: 2+ áreas formalmente pedem subdivisão interna em reuniões operacionais (ex.: PROEX quer `PROEX.EditalPNAES` distinto de `PROEX.EditalBolsas`).
- **Role-área**: qualquer área precisa de permissões por papel além de `admin`/`leitor` para mais de 3 sub-papéis distintos.
- **Vigência-área**: 2+ casos documentados de delegação temporária de admin entre áreas em 6 meses.

Quando triggered, nova ADR projeta a extensão. Até lá, **`AreaOrganizacional` é a única dimensão de governança** e `AreasDeInteresse: IReadOnlySet<AreaCodigo>` é o único elo de dados inter-área.

## Consequências

### Positivas

- Os seis pontos do Devil's Advocate endereçados por invariantes explícitas e patterns.
- Auditoria legal completa: snapshot na publicação + histórico de `Proprietario` + junction temporal de `AreasDeInteresse`.
- Reforço de closed roster previne string-stuffing trap.
- Estratégia de cache é eficiente (chave global única, filtro pós-cache).
- Fan-out de auditoria evita amplificação de escrita (uma linha canônica, JOIN em leitura).
- Evolução futura (sub-área, role-área, vigência-área) tem triggers documentados, não hand-waving aspiracional.

### Negativas

- Junction table com validade temporal é mais complexa que coluna list simples.
- Tabelas de snapshot (`edital_governance_snapshot`, `proprietario_historico`, `catalog_item_areas_de_interesse` com temporal) crescem monotonicamente; disciplina de storage necessária.
- Admin UI precisa apresentar a política cooperativa para desativação de `Ativo` (um step de confirmação extra).
- Novos devs precisam aprender 7 patterns de uma vez — custo de onboarding.

### Neutras

- A escolha não fecha discussão sobre como `Ativo` por-área seria modelado no futuro; documenta apenas o trigger.

## Confirmação

- **Risco**: tabelas de snapshot crescem descontroladamente.
  **Mitigação**: crescimento estimado ~1000 linhas/ano (100 catálogos × 10 ciclos de edital). Negligível no horizonte de 5 anos. Soft-delete + política de arquivamento para editais com mais de 7 anos per retenção LGPD.
- **Risco**: queries temporais na junction ficam lentas em escala.
  **Mitigação**: index parcial em `WHERE valid_to IS NULL` para bindings vigentes; query plans explicados documentados em `tools/db-performance/`.
- **Risco**: roster fechado vira cerimônia — devs colam fake `AdrReferenceCode`.
  **Mitigação**: fitness test ArchUnitNET em scripts de seed; checklist de revisão de PR.
- **Risco**: política cooperativa para `Ativo` ignorada — CEPS desativa sem notificar PROEG.
  **Mitigação**: prompt na admin UI + subscription Kafka dá visibilidade às áreas interessadas; monitorado como métrica operacional.
- **Risco**: performance do JOIN de fan-out de auditoria degrada com volume de log.
  **Mitigação**: audit log particionado por ano; queries por padrão escopadas a períodos recentes; queries frias contra partições arquivadas são caminhos lentos aceitos.

## Prós e contras das opções

### A — Endereçar parcialmente (snapshot apenas)

- **Prós**: Storage menor; lógica de snapshot mais simples.
- **Contras**: Auditoria legal "quem podia ver isto em 12/04/2026?" não responde se `AreasDeInteresse` mudou desde então. Mesma classe de risco de auditoria do histórico de `Proprietario`.

### B — YAGNI puro (nada agora)

- **Prós**: Zero estrutura extra hoje. Time foca em entregar funcionalidade.
- **Contras**: Quando o primeiro mandado de segurança chegar, a plataforma não consegue reconstruir o estado histórico. Custo de adicionar snapshot/histórico **post-prod com dados ao vivo** é muito maior que adicioná-los agora durante pre-prod.

### C — Endereçar todos (escolhida)

- **Prós**: Discussão acima.
- **Contras**: Discussão acima.

## Mais informações

- [ADR-0001](0001-monolito-modular-como-estilo-arquitetural.md) — Monolito modular.
- [ADR-0013](0013-motor-de-classificacao-como-servicos-de-dominio-puros.md) — Motor de classificação como pure domain services.
- [ADR-0019](0019-proibir-pii-em-path-segments-de-url.md) — Sem PII em URLs.
- [ADR-0033](0033-icurrentuser-abstraction-via-iusercontext.md) — `IUserContext`.
- RN08 de `docs/visao-do-projeto.md` — Congelamento de parâmetros na publicação do edital.
- LGPD Lei 13.709/2018 — Requisitos de retenção e auditoria.
- [ADR-0055](0055-organizacao-institucional-bounded-context.md) — OrganizacaoInstitucional bounded context.
- [ADR-0056](0056-parametrizacao-modulo-e-read-side-carve-out.md) — Módulo Parametrizacao e carve-out read-side.
- [ADR-0058](0058-obrigatoriedade-legal-validacao-data-driven.md) — ObrigatoriedadeLegal como validação data-driven.
- Diretiva do sponsor (2026-05-13): endereçar TODOS os seis pontos do Devil's Advocate nas ADRs.
