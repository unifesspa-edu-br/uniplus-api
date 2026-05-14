---
status: "accepted"
date: "2026-05-14"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0059: Decomposição da Sprint 3 e estratégia paralela para entrega de Parametrizacao

## Contexto e enunciado do problema

A estratégia do módulo Parametrizacao (per ADRs [0055](0055-organizacao-institucional-bounded-context.md), [0056](0056-parametrizacao-modulo-e-read-side-carve-out.md), [0057](0057-areas-rbac-snapshot-historia-invariantes.md) e [0058](0058-obrigatoriedade-legal-validacao-data-driven.md)) envolve seis sub-features identificadas:

1. Módulo `OrganizacaoInstitucional` (foundation).
2. Módulo `Parametrizacao` + 4 catálogos cross-cutting (Modalidade, NecessidadeEspecial, TipoDocumento, Endereco).
3. RBAC por áreas com snapshot/histórico infrastructure.
4. Carve-out read-side cross-módulo (`IXxxReader` + `.Contracts` + ArchUnitNET).
5. `ObrigatoriedadeLegal` validação data-driven em Selecao.
6. Catálogos Selecao-específicos (TipoEdital, TipoEtapa, CriterioDesempate, LocalProva) — promove enums para entidades + endpoints do wizard.

Escopo estimado: ~25–30 stories total. O sponsor confirmou que o escopo completo está commitado para Sprint 3 com **3 desenvolvedores backend dedicados** na camada de API (coordenação com frontend é assíncrona via PRD separado em `uniplus-web`). Com 3 devs dedicados em sprint de 2 semanas e razão alta de boilerplate (CRUD por catálogo segue o mesmo template), a capacidade do time fica materialmente acima da baseline de ~12 stories/sprint (que assume time compartilhado, multi-domínio).

A questão de estratégia não é "o que entregar" (escopo completo confirmado), mas "como decompor e paralelizar entre 3 devs para minimizar conflitos de merge, maximizar transfer de aprendizado e garantir que a foundation esteja sólida antes que catálogos dependam dela".

Três estratégias foram consideradas no brainstorming:

- Foundation primeiro, depois catálogos em paralelo (escolhida).
- Três vertical slices em paralelo desde o dia 1 (Dev A: OrganizacaoInstitucional + Áreas; Dev B: Parametrizacao + 4 catálogos; Dev C: endpoints Selecao + ObrigatoriedadeLegal).
- Rotação em par com 1 dev dedicado a infra RBAC (Dev C dono de infra RBAC; Dev A+B rotacionam em par entre catálogos Parametrizacao e endpoints Selecao).

## Drivers da decisão

- **Minimizar drift de decisões load-bearing**: junction table, history table, fitness test boundaries são decisões cross-cutting que se beneficiam de design coletivo.
- **Paralelismo real depois da fundação**: cada lane na semana 2 deve ter superfície de código independente para reduzir conflitos de merge.
- **Aprender de uma vez**: o primeiro `IXxxReader` canônico deve ser construído com os 3 devs alinhados, para servir de template aos demais.
- **Capacidade do time**: 3 devs dedicados × 2 semanas com boilerplate alto sustenta ~25–30 stories.
- **Riscos por lane**: Lane B (10 wizard endpoints) é a mais larga; Lane C (ObrigatoriedadeLegal) é a mais complexa em design; mitigações por lane são necessárias.

## Opções consideradas

- **A**: Foundation primeiro (semana 1, 3 devs juntos) + lanes paralelas (semana 2, 1 dev por lane).
- **B**: Três vertical slices em paralelo desde o dia 1 (cada dev pega uma sub-feature inteira; vai do Domain à API independentemente).
- **C**: Rotação em par com 1 dev dedicado a infra RBAC (3 dias por par entre catálogos e endpoints).

## Resultado da decisão

**Escolhida:** "A — foundation primeiro, depois lanes paralelas", porque é a única opção que combina design coletivo nas decisões cross-cutting load-bearing (week 1) com paralelismo real depois (week 2) — sem o custo de overhead de pair programming nem o risco de drift por vertical slices isolados.

### Fase 1 — Semana 1 (foundation, 3 devs colaborando)

Todos os três devs trabalham como sub-time único na foundation load-bearing:

- Esqueleto do módulo `OrganizacaoInstitucional` + entidade `AreaOrganizacional` + identifier tipado `AreaCodigo` + invariante de roster fechado + migration de seed (5 áreas).
- Esqueleto do módulo `Parametrizacao` (5 projetos: Domain, Application, Infrastructure, API, Contracts).
- Integração de `EntityBase` com o conceito de áreas (sem `TenantId`, sem `HasQueryFilter` por tenant — apenas adição de `Proprietario: AreaCodigo?` e `AreasDeInteresse: IReadOnlySet<AreaCodigo>` onde aplicável).
- Infraestrutura de junction table para `AreasDeInteresse` com validade temporal (Pattern 3 de [ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md)).
- Tabela history SCD Type 2 para mudanças de `Proprietario` (Pattern 2).
- Documentação do pattern `IXxxReader` em `Infrastructure.Core` + primeira implementação canônica como exemplar.
- Fitness tests ArchUnitNET para o carve-out cross-módulo ([ADR-0056](0056-parametrizacao-modulo-e-read-side-carve-out.md)).
- Dois novos bancos PostgreSQL provisionados: `uniplus_parametrizacao`, `uniplus_organizacao` com usuários isolados.
- Naming convention snake_case aplicada desde a concepção (per [ADR-0054](0054-naming-convention-e-strategy-migrations.md)).

**Critério de saída da Fase 1:** toda a foundation mergeada na main; primeiro `IAreaOrganizacionalReader` funcionando end-to-end como prova do pattern; fitness tests verdes no CI.

### Fase 2 — Semana 2 (catálogos em lanes paralelas, 1 dev por lane)

Cada dev assume uma das três lanes, trabalhando de forma independente com baixo risco de merge.

**Lane A — Dev 1 — Catálogos Parametrizacao:**

- `Modalidade` entidade + EF config (com `CatalogVisibilityConfiguration<Modalidade>`) + migration adicionando tabelas `modalidades` + `modalidade_areas_de_interesse` + JSON seed (12 entradas Lei 12.711) + `IModalidadeReader`.
- `NecessidadeEspecial` analogous.
- `TipoDocumento` analogous.
- `Endereco` analogous (admin POST restrito a `plataforma-admin` per [ADR-0056](0056-parametrizacao-modulo-e-read-side-carve-out.md)).

**Lane B — Dev 2 — Promoções Selecao e endpoints do wizard:**

- Promove enums para entidades: `TipoEdital`, `TipoEtapa`, `CriterioDesempate` (cada um com admin CRUD, mantidos em `Selecao.Domain`).
- Refatora `LocalProva` para referenciar `Endereco.Id` via `IEnderecoReader` com snapshot-on-bind; adiciona campos exam-specific.
- Novos endpoints do wizard em `EditalController` (ou controllers irmãos): `PATCH /api/editais/{id}/identificacao`, `PUT .../vagas-modalidades`, `PUT .../etapas`, `PATCH .../formula`, `PATCH .../bonus`, `PUT .../desempate`, `PUT .../eliminacao`, `PUT .../documentos-modalidade`, `PUT .../locais-prova`, `PUT .../atendimento-especial`.

**Lane C — Dev 3 — ObrigatoriedadeLegal e conformidade:**

- Entidade `ObrigatoriedadeLegal` em `Selecao.Domain` com discriminated union de 8 tipos de predicado per [ADR-0058](0058-obrigatoriedade-legal-validacao-data-driven.md).
- Domain service puro `ValidadorConformidadeEdital`.
- Tabelas append-only `ObrigatoriedadeLegalHistorico` + `EditalGovernanceSnapshot`.
- Endpoints admin CRUD em `Selecao.API` para gestão de regras.
- Novos endpoints: `GET /api/editais/{id}/conformidade` (current) + `GET /api/editais/{id}/conformidade-historica` (snapshot).
- Migration de seed: 14 regras do protótipo, classificadas por governança de áreas.

**Critério de saída da Fase 2:** todos os 4 catálogos cross-cutting operacionais com admin CRUD + read endpoints; 4 catálogos Selecao-específicos promovidos + 10 endpoints de wizard operacionais; ObrigatoriedadeLegal data-driven + endpoints de conformidade funcionando; testes de integração verdes.

### Cross-cutting durante toda a sprint

- Sync diário de 15 minutos entre os 3 devs para questões cross-lane (especialmente entre `IModalidadeReader` da Lane A e `EditalController` da Lane B que o consome).
- PRs revisados por pelo menos um dev de outra lane (revisor da Lane A para PR da Lane B envolvendo readers, etc.).
- `@marciliomarques` (FullStack CTIC) aprova cross-account per prática padrão de revisão do projeto.

## Consequências

### Positivas

- Foundation é construída uma vez pelo sub-time completo, eliminando drift cross-lane em decisões load-bearing.
- Paralelismo da semana 2 é maximizado — cada dev é dono de uma lane independente com risco mínimo de merge.
- Fronteiras das lanes seguem fronteiras naturais de domínio (catálogos Parametrizacao vs primitivas Selecao vs ObrigatoriedadeLegal).
- Sync diário pega questões de integração cross-lane cedo, sem custo de pair programming.
- A primeira implementação canônica de `IXxxReader` na Fase 1 serve de template para as lanes da semana 2.
- Sprint 3 termina com o backend do wizard pronto para integração frontend na Sprint 4.

### Negativas

- A Fase 1 tem custo de coordenação "stop the world" — 3 devs trabalhando como um sub-time por uma semana perdem alguma throughput individual.
- Lane B (Selecao) tem trabalho mais diverso (promoções de catálogo + 10 endpoints de wizard) que Lane A ou Lane C — possível desbalanço de carga.
- ObrigatoriedadeLegal (Lane C) é a lane individual mais complexa; risco de extrapolar a fronteira do sprint se o escopo for subestimado.

### Neutras

- Estratégia assume que os 3 devs estão dedicados — substituições parciais podem comprometer o paralelismo da semana 2.

## Confirmação

- **Risco**: Fase 1 extrapola para a semana 2.
  **Mitigação**: foundation está bem-especificada pelas ADRs 0055–0058. Sync diário no dia 3 da semana 1 — se estiver atrasado, escopar a foundation para apenas OrganizacaoInstitucional + esqueleto Parametrizacao + primeiro reader, adiar fitness tests ArchUnitNET para a semana 2 conforme Dev 1 cobre.
- **Risco**: Lane B subestimada — 10 endpoints PATCH/PUT é superfície ampla.
  **Mitigação**: endpoints seguem pattern já estabelecido pelo `EditalController` (Criar, Listar, Obter, Publicar) — são templatáveis. Se Dev 2 atrasar, Dev 3 pega os endpoints mais simples (PATCH fórmula, bônus) na ociosidade da Lane C.
- **Risco**: Lane C estoura prazo pela complexidade da discriminated union + snapshot + histórico + evaluator + seed de 14 regras.
  **Mitigação**: mínimo viável é 4 das 8 variantes implementadas (cobrindo `ETAPA_OBRIGATORIA`, `MODALIDADES_MINIMAS`, `DOCUMENTO_OBRIGATORIO_PARA_MODALIDADE`, `DESEMPATE_DEVE_INCLUIR` — as mais usadas no seed de 14 regras). Outras 4 variantes lançam stub-throw com follow-up documentado.
- **Risco**: Time frontend começa a consumir endpoints antes deles estarem estáveis.
  **Mitigação**: OpenAPI spec é contract-first ([ADR-0030](0030-openapi-3-1-contract-first-microsoft-aspnetcore-openapi.md)); frontend pode consumir do spec enquanto o backend estabiliza; "API freeze" milestone explícito no merge do PR final da Lane B.

## Prós e contras das opções

### A — Foundation primeiro + lanes paralelas (escolhida)

- **Prós**: Foundation construída uma vez por todo o time, sem drift. Paralelismo real na semana 2. Cada lane independente em superfície de código. Daily sync pega integração cross-lane cedo. Template canônico construído cedo.
- **Contras**: Coordenação stop-the-world na semana 1 reduz throughput individual. Possível desbalanço entre lanes (B mais larga, C mais complexa).

### B — Três vertical slices em paralelo desde o dia 1

- **Prós**: Paralelismo máximo; cada dev tem ownership total de sua slice; sem espera pela foundation.
- **Contras**: Risco grave de conflitos de merge em infraestrutura compartilhada (pattern `IXxxReader`, fitness tests, identifier `AreaCodigo`, extensões de `EntityBase`). Dev B e Dev C ambos dependem do `AreaCodigo` e `IAreaOrganizacionalReader` do Dev A desde o dia 1 — ou reinventam localmente (drift) ou bloqueiam no Dev A (mata o paralelismo). A infraestrutura de áreas/snapshot/history é compartilhada por todas as entidades; um dev definindo isolado cria decisões load-bearing que os três precisam adotar.

### C — Rotação em par com 1 dev dedicado a infra RBAC

- **Prós**: Infra RBAC tem dono único que mantém consistência de design. Pair programming espalha conhecimento.
- **Contras**: Overhead de pair programming reduz throughput efetivo (~30% perda empírica com sprints de 2 semanas sob pressão de prazo). Dev C vira dependência sequencial para qualquer entidade que precise de integração de áreas. O custo de rotação (~1 dia de context-switching por troca de par) é significativo para um sprint de 2 semanas.

## Mais informações

- [ADR-0055](0055-organizacao-institucional-bounded-context.md) — OrganizacaoInstitucional bounded context.
- [ADR-0056](0056-parametrizacao-modulo-e-read-side-carve-out.md) — Módulo Parametrizacao e carve-out read-side.
- [ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md) — RBAC por áreas com snapshot/histórico.
- [ADR-0058](0058-obrigatoriedade-legal-validacao-data-driven.md) — ObrigatoriedadeLegal como validação data-driven.
- [ADR-0054](0054-naming-convention-e-strategy-migrations.md) — Snake_case + 3-DB isolation (informa as migrations dos novos módulos).
- `docs/guia-banco-de-dados.md` — Convenções de DB (3 → 5 bancos isolados, pattern estendido).
- Memória do projeto: `project_code_review_practice.md` — Prática de revisão cross-account.
