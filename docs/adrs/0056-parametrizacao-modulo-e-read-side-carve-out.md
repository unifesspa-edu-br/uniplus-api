---
status: "accepted"
date: "2026-05-14"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0056: Módulo Parametrizacao e carve-out read-side cross-módulo via IXxxReader

## Contexto e enunciado do problema

O `uniplus-api` tem oito catálogos admin-editáveis identificados pelo protótipo do wizard de edital, com naturezas distintas:

| Catálogo | Natureza | Usado por |
|---|---|---|
| `TipoEdital` | Template de processo seletivo | Apenas Selecao |
| `TipoEtapa` | Stage de workflow de seleção | Apenas Selecao |
| `CriterioDesempate` | Primitiva do motor de classificação ([ADR-0013](0013-motor-de-classificacao-como-servicos-de-dominio-puros.md)) | Apenas Selecao |
| `LocalProva` | Local de aplicação de prova (capacidade, responsável exam-specific) | Apenas Selecao |
| `ObrigatoriedadeLegal` | Engine configurável de validação legal para editais | Apenas Selecao (ver [ADR-0058](0058-obrigatoriedade-legal-validacao-data-driven.md)) |
| `Modalidade` | Modalidade de cota federal (Lei 12.711/2023) | Selecao + Ingresso + módulos futuros |
| `NecessidadeEspecial` | Categorias de acessibilidade (LBI Lei 13.146/2015) | Selecao + Ingresso + módulos futuros |
| `TipoDocumento` | Tipos de documento para inscrição (CEPS) e matrícula (CRCA) | Selecao + Ingresso |

O sponsor esclareceu durante council R2: `LocalProva` é estritamente domínio Selecao (com metadata exam-specific), mas o **endereço físico em si** é reutilizável. O `LocalProva` do CEPS para uma prova em "Campus Marabá Folha 31" e um hipotético `LocalMatricula` do CRCA para evento de matrícula no mesmo site físico compartilham o **endereço** mas não os atributos venue-specific. Isso decompõe `LocalProva` (como desenhado no protótipo) em dois conceitos: o cross-cutting reutilizável `Endereco`, e o `LocalProva` Selecao-specific que **referencia** um `Endereco` e adiciona campos exam-specific.

A justificativa para um módulo `Parametrizacao` separado repousa em:

- **Coesão de governance shape** — `Modalidade`, `NecessidadeEspecial`, `TipoDocumento`, `Endereco` todos compartilham: `Proprietario` + `AreasDeInteresse` + vigência + base legal + snapshot-on-bind.
- **Reuso entre módulos sem acoplamento direto** — Selecao e Ingresso ambos consomem esses catálogos; colocá-los em qualquer consumer cria o anti-pattern que [ADR-0001](0001-monolito-modular-como-estilo-arquitetural.md) foi desenhada para prevenir.
- **Dados universais (modalidades Lei 12.711, regulamentações federais de acessibilidade) merecem uma casa que sinaliza seu escopo platform-wide.**

[ADR-0001](0001-monolito-modular-como-estilo-arquitetural.md) (monolito modular, cross-módulo via Kafka apenas) governa **mutações write-side cross bounded contexts** — eventos, mudanças de estado, dispatch de comandos. Para **consultas read-side de dados de referência** (renderizar dropdowns, validar codes), a opção de carve-out: in-process DI síncrono é permitido via interfaces `IXxxReader` publicadas em assemblies `.Contracts`, enforçado por fitness test.

## Drivers da decisão

- **Coesão sobre cardinalidade de módulos**: 4 catálogos com mesma shape governance merecem casa comum mais do que ficar espalhados.
- **Reuso explícito sem violar a fronteira do bounded context**: cross-módulo precisa de mecanismo controlado, não escape hatch implícito.
- **Latência aceitável nas leituras do wizard (dezenas de lookups por render)**: wizard de edital faz dezenas de lookups de `Modalidade` por renderização; passar tudo por Kafka projection seria operacionalmente caro e latência-visível ao usuário.
- **Auditabilidade da exceção**: fitness test deve detectar se cross-módulo dependencies violam o boundary.
- **Forward-compatibilidade**: pattern deve suportar extensão a Ingresso/Portal/módulos futuros sem revisitar a decisão.

## Opções consideradas

- **A**: Todos os 8 catálogos dentro de `Selecao`. Cross-módulo consumption via `Selecao.Contracts`.
- **B**: Cross-módulo apenas via Kafka projection — cada módulo materializa cópia local de `Modalidade` etc. via eventos.
- **C**: Catálogos cross-cutting em `SharedKernel`.
- **D**: REST admin genérica `/api/parametrizacao/{tipo}` com payload polimórfico, single resource.
- **E**: Módulo `Parametrizacao` dedicado para catálogos cross-cutting; carve-out read-side via `IXxxReader` em `.Contracts`; write-side permanece restrita a Kafka.

## Resultado da decisão

**Escolhida:** "E — módulo `Parametrizacao` dedicado com carve-out read-side via `IXxxReader`", porque é a única opção que respeita a disciplina do monolito modular (ADR-0001) com fitness tests enforcando o pattern, mantém latência do wizard em níveis aceitáveis (sub-millisecond warm) e preserva per-resource vendor MIME (ADR-0028).

### Estrutura do módulo

```text
src/parametrizacao/
├── Unifesspa.UniPlus.Parametrizacao.Domain/
│   └── Entities/                  ← Modalidade, NecessidadeEspecial, TipoDocumento, Endereco
├── Unifesspa.UniPlus.Parametrizacao.Application/
│   ├── Commands/                  (admin CRUD por catálogo)
│   ├── Queries/                   (listar, obter por código, obter por vigência)
│   ├── DTOs/
│   └── Events/                    (CatalogItemRegistradoEvent, AtualizadoEvent, DesativadoEvent)
├── Unifesspa.UniPlus.Parametrizacao.Contracts/
│   ├── IModalidadeReader.cs       ← interface publicada para leitura cross-módulo
│   ├── INecessidadeEspecialReader.cs
│   ├── ITipoDocumentoReader.cs
│   ├── IEnderecoReader.cs
│   ├── Dtos/
│   │   ├── ModalidadeView.cs
│   │   ├── NecessidadeEspecialView.cs
│   │   ├── TipoDocumentoView.cs
│   │   └── EnderecoView.cs
│   └── ReferenceDataAttribute.cs  ← marker
├── Unifesspa.UniPlus.Parametrizacao.Infrastructure/
│   └── Persistence/
│       ├── ParametrizacaoDbContext.cs ← schema "parametrizacao"
│       └── Configurations/
└── Unifesspa.UniPlus.Parametrizacao.API/
    └── Controllers/               (um controller por recurso: ModalidadeController, etc.)
```

### Catálogos em Parametrizacao (cross-cutting)

1. **`Modalidade`** — modalidades de cota Lei 12.711/2023. Universal (`AreasDeInteresse = []`), `Proprietario = null` (apenas plataforma-admin). Lei federal; nunca área-específica.
2. **`NecessidadeEspecial`** — categorias de acessibilidade LBI Lei 13.146/2015. Universal. `Proprietario = null`.
3. **`TipoDocumento`** — tipos de documento. Maioria cross-area (CEPS usa para inscrição, CRCA para matrícula); algumas entradas área-específicas (`AreasDeInteresse = ["CRCA"]` para docs matriculation-only); algumas universais (RG, CPF, etc.).
4. **`Endereco`** — endereço físico com município, código IBGE, UF, logradouro, CEP. Reutilizável por `LocalProva` (Selecao), futura `LocalMatricula` (Ingresso), `LocalEntrega` (qualquer módulo). `AreasDeInteresse` reflete quem usa atualmente cada endereço; `Proprietario` é a área que provisionou (tipicamente PLATAFORMA).

### Catálogos que ficam no módulo dono (domain-specific)

1. **`TipoEdital`** → Selecao (template de processo seletivo; primitiva pura de Selecao).
2. **`TipoEtapa`** → Selecao (definições de stage de workflow; specific to selection).
3. **`CriterioDesempate`** → Selecao (primitiva do motor de classificação per [ADR-0013](0013-motor-de-classificacao-como-servicos-de-dominio-puros.md)).
4. **`LocalProva`** → Selecao (referencia `Endereco` por `EnderecoOrigemId` snapshot per [ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md); adiciona campos exam-specific: `CapacidadeMaxima`, `ResponsavelExame`, `CondicoesAcessibilidade`. CEPS-administered).
5. **`ObrigatoriedadeLegal`** → ver [ADR-0058](0058-obrigatoriedade-legal-validacao-data-driven.md) para design completo; fica em Selecao para V1 com critérios de promoção a futuro módulo `Normativos` documentados.

### Carve-out cross-módulo read-side (refina ADR-0001)

ADR-0001 é refinada conforme segue:

**Leitura cross-módulo de dados de referência é permitida via in-process DI sob todas as condições abaixo:**

1. O módulo dono publica interface `I{Resource}Reader` em assembly `{Module}.Contracts` apenas.
2. A interface retorna DTOs read-only (`{Resource}View`), nunca entidades de `Domain`.
3. Métodos recebem **contexto explícito de áreas** como parâmetro — caller passa `IReadOnlyCollection<AreaCodigo> areasCaller`, nunca resolvido internamente de `IUserContext`. (Isso endereça cache correctness — ver [ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md).)
4. A implementação é registrada como Singleton respaldada por **distributed cache (Redis 8 via `IDistributedCache`, TTL 5 minutos)**. O cache armazena o **raw set por recurso** sob chave canônica (`parametrizacao:{recurso}`, ex.: `parametrizacao:modalidades`), não views per-caller; o filtro de áreas acontece após retrieval do cache, no reader, baseado no parâmetro `areasCaller` explícito. Cache L1 in-process (`IMemoryCache`) **NÃO** é adicionado em V1 — é otimização adiada gated por evidência de P95 latency ou throughput (ver Riscos).
5. Invalidação via subscriber de evento de domínio Kafka (`ModalidadeAtualizadaEvent` triggera `IDistributedCache.RemoveAsync("parametrizacao:modalidades")`). O `DEL` único é atômico cross-replicas — não há janela de inconsistência cross-pod.
6. Stampede protection em cache miss: reader adquire lease curto (~300ms) via Redis `SET NX EX` antes de bater no PostgreSQL, fazendo requests concorrentes coalescerem em uma única source query.
7. A entidade carrega atributo `[ReferenceData]` (declarado em `Parametrizacao.Contracts`).

**Mutações write-side ou queries de aggregate state permanecem Kafka-only** (sem mudança no espírito de ADR-0001).

**Background jobs e operações `plataforma-admin`** que legitimamente precisam de visibilidade unfiltered passam coleção `areasCaller` vazia E carregam atributo `[ExplicitlyUnscoped]` na call site. Fitness test ArchUnitNET falha o build se `[ExplicitlyUnscoped]` é usado sem assertion de role `plataforma-admin`.

### Eventos de catálogo — PG queue apenas em V1 (sem registro Apicurio)

Eventos de mutação de catálogo (`ModalidadeRegistradoEvent`, `ModalidadeAtualizadoEvent`, `ModalidadeDesativadoEvent`, e análogos para outros catálogos) são **intra-module** em V1: existem unicamente para invalidar a entry de `IDistributedCache` dentro do processo da API do módulo dono via subscriber Wolverine PG queue. Não há consumer cross-módulo subscrito a esses eventos no escopo Sprint 3. Per [ADR-0044](0044-roteamento-domain-events-pg-queue-kafka-opcional.md), esses eventos roteiam para PG queue apenas — sem publish Kafka, sem `.avsc` em `Parametrizacao.Domain/Events/Schemas/`, sem entry `SchemaRegistration.AddSchema(...)` em `Program.cs`, sem subject Apicurio.

**Trigger para promover um evento de catálogo a Kafka + Avro** (per [ADR-0051](0051-apicurio-schema-registry-avro-wolverine.md) pattern hand-written, com threshold ≥3 schemas-total para migração a codegen):

1. Um segundo módulo (Ingresso, Portal, Auxilio Estudantil) assina como consumer Kafka de um evento de catálogo — tipicamente porque mantém projeção própria ou invalida cache local que o processo Parametrizacao não alcança via Redis.
2. Promoção segue o pattern canônico documentado em `docs/guia-apicurio-schema-registry.md`: adicionar `.avsc` embedded resource, classe `ISpecificRecord` hand-written em `Infrastructure/Messaging/Avro/`, `<Evento>ToAvroMapper` em `Infrastructure/Messaging/`, cascade handler projetando domain event → record Avro, routing entry em `Program.cs` com `SchemaRegistryAvroSerializer`, e call `.AddSchema("<topic>-value", ...)`.
3. O threshold codegen-vs-hand-written (≥3 schemas total) é project-wide; uma vez que o projeto atinja 3 schemas Avro (hoje 1 — `EditalPublicado.avsc`; Sprint 3 adiciona zero), reavaliar switch para `Apache.Avro.Tools` MSBuild codegen per ADR-0051 §2.

Essa decisão explícita "PG queue apenas" mantém Sprint 3 livre de superfície Avro/Apicurio preservando forward-compatibilidade com o pattern estabelecido.

### Fitness tests (ArchUnitNET)

- Nenhum projeto de módulo pode referenciar assemblies `.Domain` ou `.Application` de outro módulo.
- Dependências cross-módulo são permitidas apenas contra assemblies `{Module}.Contracts`.
- Métodos `I{Resource}Reader` devem aceitar `IReadOnlyCollection<AreaCodigo> areasCaller` OU ser marcados `[ExplicitlyUnscoped]`.
- Entidades marcadas `[ReferenceData]` não podem ter dependências outbound sobre tipos de outros módulos.
- `Parametrizacao.Contracts` pode depender de `OrganizacaoInstitucional.Contracts` (para `AreaCodigo`).
- `LocalProva` em `Selecao.Domain` referencia `EnderecoOrigemId: Guid?` — sem dependência de domain em `Parametrizacao.Domain.Endereco`; validação da FK é por value-copy via `IEnderecoReader.ObterPorIdAsync` no command handler per [ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md).

### Contrato REST por catálogo

Cada catálogo em Parametrizacao é seu próprio recurso REST com vendor MIME (estende [ADR-0028](0028-versionamento-per-resource-content-negotiation.md)):

```text
application/vnd.uniplus.modalidade.v1+json
application/vnd.uniplus.necessidade-especial.v1+json
application/vnd.uniplus.tipo-documento.v1+json
application/vnd.uniplus.endereco.v1+json
```

Endpoints seguem contrato V1 estabelecido:

- `GET /api/parametrizacao/{recurso}` — leitura; filtrada pelas áreas do caller; sem cursor pagination (catálogos são reference data bounded ≤ 100 linhas; exceção deliberada a [ADR-0026](0026-paginacao-cursor-opaco-cifrado.md)).
- `GET /api/parametrizacao/{recurso}/{id:guid}` — single resource com HATEOAS `_links` ([ADR-0029](0029-hateoas-level-1-links.md)).
- `POST /api/admin/parametrizacao/{recurso}` — admin área-scoped per [ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md); `Idempotency-Key` required.
- `PUT /api/admin/parametrizacao/{recurso}/{id:guid}` — admin área-scoped; idempotente.
- `DELETE /api/admin/parametrizacao/{recurso}/{id:guid}` — soft delete apenas.

Admin endpoints de `Endereco` são restritos: apenas `plataforma-admin` pode criar novos endereços (endereços são infraestrutura institucional); outras áreas pedem adições via processo interno. Isso evita proliferação de endereços duplicados.

## Consequências

### Positivas

- Casa clara para reference data cross-cutting com governance consistente.
- `Endereco` reutilizável cross-módulo; infraestrutura física modelada uma vez.
- ADR-0001 refinada com carve-out preciso fitness-test-enforced — não violada ad hoc.
- Wizard latency permanece aceitável para reference reads (~1–5ms warm via Redis; reference data é invocada uma vez por screen load, não por linha — overhead negligível contra budgets típicos de request 100–300ms).
- **Consistência cross-pod é atômica** — invalidação Kafka triggera DEL Redis único, todas as réplicas imediatamente param de servir valor stale. Isso elimina a janela de não-determinismo que cache in-memory per-pod introduziria, material sob RN08 (parameter freeze) e obrigações de reproducibilidade judicial-audit.
- Adicionar novo consumer module é trivial: referencia `Parametrizacao.Contracts`, injeta `IModalidadeReader`. Sem nova infra.
- Per-resource vendor MIME preservado.
- Passing explícito de áreas faz background jobs e operações admin naturalmente expressáveis.

### Negativas

- Adiciona 2 novos módulos à solution (`Parametrizacao` + `OrganizacaoInstitucional`).
- `LocalProva` referencia `Endereco` por ID — seleção de endereço é step explícito no workflow do CEPS.
- Fitness tests ArchUnitNET devem ser adicionados; novo pattern para o time aprender.
- Reader é dependente de disponibilidade de Redis; outage degrada todos reference-data reads para PostgreSQL hits diretos. Mitigado por Redis ser dependência stack-wide já (sessions, idempotency cache) — failure mode é compartilhado, não específico a este carve-out.
- Propagação de invalidação de cache não é instantânea da perspectiva do usuário: write commits → outbox publishes → Kafka delivers → subscriber DEL Redis → próxima reader call repopulates. Latência típica end-to-end é sub-second; admin UI deve mostrar toast "alteração propagando" brevemente post-save.

### Neutras

- A escolha não impede que coleções carreguem metadados auxiliares — mas isso vai **apenas em headers HTTP** (per [ADR-0025](0025-wire-formato-sucesso-body-direto.md)).

## Confirmação

- ArchUnitNET fitness tests asseram cross-module boundaries (regras R1–R7 documentadas).
- Spectral rule no spec OpenAPI verifica que cada catálogo tem vendor MIME registrado.
- Smoke E2E (Newman) cobre criar/listar/obter/admin-CRUD em cada catálogo.
- Revisão arquitetural de PR: PRs que adicionem catálogo em módulo errado (ex.: Modalidade fora de Parametrizacao) são rejeitados.

## Prós e contras das opções

### A — Todos os 8 catálogos em Selecao

- **Prós**: Zero novo módulo. Caminho mais rápido para wizard.
- **Contras**: Quando Ingresso precisa de `Modalidade` (matriculation eligibility check, near-certainty em 6-12 meses dado roadmap), ou (i) Ingresso importa reader do Selecao (aceitável per carve-out, mas signal coupling) ou (ii) constrói duplicate local de `Modalidade` (drift inevitável). Futuros módulos compostam o problema. `Modalidade` e `NecessidadeEspecial` são explicitamente federal-universal — colocá-las em Selecao misrepresenta seu escopo.

### B — Kafka-only cross-módulo (Architect's opening original)

- **Prós**: Conformance estrito a ADR-0001. Zero coupling. Reads de cada módulo são self-contained.
- **Contras**: Complexidade operacional: ~50–200ms eventual consistency lag; debug de consumer rebalance; recovery de projection corruption; N módulos × M reference catalogs = N×M projections para manter. Time CTIC de ~10 devs absorve esse custo para sempre. Bug UX "acabei de criar Modalidade em admin tab, por que não aparece aqui?" é support ticket perene.

### C — SharedKernel grab-bag

- **Prós**: Sem novo projeto de módulo.
- **Contras**: SharedKernel vira gaveta de bagunça — value objects, base entities, reference data, DTOs comuns todos em um lugar. Aumenta coupling: cada módulo referencia SharedKernel; inflar bloata compilation graph e surface area.

### D — REST admin genérica `/api/parametrizacao/{tipo}` polimórfica

- **Prós**: Menos boilerplate; um controller para 4 catálogos.
- **Contras**: Descarta type safety no contrato. Quebra semântica HATEOAS link (cada tipo de recurso tem relações diferentes). Versioning per resource (ADR-0028) fica impossível. Client codegen produz unions weakly typed. ProblemDetails error codes não podem ser específicos.

### E — Módulo dedicado com carve-out (escolhida)

- **Prós**: Discussão acima. Trade-offs absorvidos pelas justificativas centrais.
- **Contras**: Discussão acima.

## Mais informações

- [ADR-0001](0001-monolito-modular-como-estilo-arquitetural.md) — Monolito modular (refinada aqui).
- [ADR-0013](0013-motor-de-classificacao-como-servicos-de-dominio-puros.md) — Motor de classificação como pure domain services.
- [ADR-0026](0026-paginacao-cursor-opaco-cifrado.md) — Paginação cursor (com exceção documentada para reference data bounded).
- [ADR-0027](0027-idempotency-key-store-postgresql.md) — Idempotency-Key store.
- [ADR-0028](0028-versionamento-per-resource-content-negotiation.md) — Versionamento per resource.
- [ADR-0029](0029-hateoas-level-1-links.md) — HATEOAS Level 1.
- [ADR-0033](0033-icurrentuser-abstraction-via-iusercontext.md) — IUserContext.
- [ADR-0044](0044-roteamento-domain-events-pg-queue-kafka-opcional.md) — Roteamento eventos PG queue + Kafka opcional.
- [ADR-0051](0051-apicurio-schema-registry-avro-wolverine.md) — Apicurio Schema Registry com Avro hand-written.
- Kamil Grzybek — `modular-monolith-with-ddd` (precedent de Open Host Service).
- Vaughn Vernon — *Implementing DDD* (Open Host Service vs Published Language).
- [ADR-0055](0055-organizacao-institucional-bounded-context.md) — OrganizacaoInstitucional bounded context.
- [ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md) — Áreas-based RBAC com snapshot, histórico e invariantes.
- [ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md) — Referência cross-módulo via snapshot copy.
- Esclarecimento do sponsor (2026-05-13): `LocalProva` é domínio Selecao; `Endereco` é cross-cutting.
