---
status: "accepted"
date: "2026-05-14"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0055: OrganizacaoInstitucional como bounded context dedicado com roster fechado

## Contexto e enunciado do problema

O `uniplus-api` é uma plataforma com uma instância por instituição: a Unifesspa hospeda sua própria instância; qualquer outra IFES que adote a plataforma instala e opera seu próprio deployment. **Não há aspecto multi-tenant SaaS** — sem isolamento row-level por instituição, sem coluna `TenantId` em `EntityBase`, sem `HasQueryFilter` por tenant, sem claim de tenant no JWT.

Dentro de uma instância da plataforma, a plataforma precisa atender múltiplas **áreas organizacionais** com responsabilidades administrativas distintas e propriedade sobre subconjuntos diferentes dos dados de referência:

- **CEPS** (Centro de Processos Seletivos) — dono de Seleção; edita `TipoEdital`, `TipoEtapa`, `CriterioDesempate`, `LocalProva` para provas, `ObrigatoriedadeLegal` de editais.
- **CRCA** (Centro de Registro e Controle Acadêmico) — dono de Ingresso; edita `TipoDocumento` para matrícula, configurações específicas de enrollment.
- **PROEG** (Pró-Reitoria de Ensino de Graduação) — supervisora sobre CEPS/CRCA; edita parâmetros de política entre áreas quando aplicável.
- **PROGEP** (Pró-Reitoria de Gestão de Pessoas) — domínio separado, módulo futuro.
- **Plataforma / CTIC** — edita dados de referência universais (modalidades Lei 12.711/2023, regulamentações federais de acessibilidade), configurações platform-wide, e roles que atravessam áreas.
- **Futuras áreas** — a plataforma deve aceitar novas áreas sem mudança arquitetural.

O risco que esta ADR mitiga: sem um conceito explícito de área, a plataforma ou (a) concede todos os roles admin com acesso platform-wide flat (CEPS edita `TipoDocumento` do CRCA, audit trail diz "admin" sem atribuição de área, edições acidentais entre áreas viram rotina), ou (b) hardcoda mapeamentos área→módulo em filtros de autorização nos controllers (cada nova área exige mudança de código). Nenhum é aceitável.

## Drivers da decisão

- **Governança real, não cosmética**: áreas têm responsabilidades distintas que precisam de auditoria explícita; "quem editou o quê" deve responder a "qual área".
- **Configurável sem deploy**: o princípio "quando a lei muda, edita o catálogo — sem deploy" exige que adicionar uma área nova (PROAE, PROEX, etc.) seja operação de dados, não de código.
- **Coerência com ADR-0001** (monolito modular): áreas têm ciclo de vida próprio (CRUD, audit, soft-delete, eventos) — pertencem a um bounded context, não a `Kernel`.
- **Closed roster controlado**: adicionar uma nova área é decisão arquitetural deliberada (afeta RBAC, governance, audit); a entidade tem mecanismo que torna a adição rastreável.
- **`Kernel` permanece enxuto**: tipos com lifecycle (CRUD, eventos, soft-delete) violam a disciplina "Kernel = primitivos sem comportamento próprio".

## Opções consideradas

- **A**: `AreaOrganizacional` como enum C# fechado em `Kernel.Domain` (5 valores hardcoded; adicionar área exige PR + recompilação).
- **B**: `AreaOrganizacional` como entidade em `Kernel.Domain.Entities` (mesmo shape mas localizada em Kernel).
- **C**: `AreaOrganizacional` co-hospedada com catálogos no módulo `Parametrizacao`.
- **D**: `AreaOrganizacional` em Keycloak groups apenas, sem entidade no banco.
- **E**: Bounded context dedicado `OrganizacaoInstitucional` com `AreaOrganizacional` como agregado, invariante de roster fechado via `AdrReferenceCode`, e `AreaCodigo` como identificador strongly-typed.

## Resultado da decisão

**Escolhida:** "E — bounded context dedicado `OrganizacaoInstitucional`", porque é a única opção que reconhece áreas como conceito de domínio com lifecycle (CRUD, eventos, audit), preserva a disciplina de `Kernel` (primitivos apenas), respeita o princípio de configurável-sem-deploy e isola governance de catálogos (CRUD admin é distinto de áreas).

### Estrutura do módulo

```text
src/organizacao-institucional/
├── Unifesspa.UniPlus.OrganizacaoInstitucional.Domain/
│   └── Entities/
│       └── AreaOrganizacional.cs
├── Unifesspa.UniPlus.OrganizacaoInstitucional.Application/
│   ├── Commands/                    (admin: registrar via ADR, atualizar, soft-delete)
│   ├── Queries/
│   └── DTOs/
├── Unifesspa.UniPlus.OrganizacaoInstitucional.Contracts/
│   ├── IAreaOrganizacionalReader.cs (leitor cross-módulo)
│   ├── AreaCodigo.cs                (record struct sobre string, strongly-typed)
│   ├── AreaOrganizacionalView.cs    (DTO read-only para consumo cross-módulo)
│   └── ReferenceDataAttribute.cs
├── Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure/
│   └── Persistence/
│       ├── OrganizacaoInstitucionalDbContext.cs (banco `uniplus_organizacao`)
│       └── Configurations/
└── Unifesspa.UniPlus.OrganizacaoInstitucional.API/
    └── Controllers/
        └── AreasOrganizacionaisController.cs
```

### Entidade `AreaOrganizacional`

```text
Id: Guid (v7)
Codigo: string (unique, uppercase, ex.: "CEPS", "CRCA", "PROEG", "PROGEP", "PLATAFORMA")
Nome: string (ex.: "Centro de Processos Seletivos")
Tipo: enum { ProReitoria, Centro, Coordenadoria, Plataforma, Outra }
Descricao: string
AdrReferenceCode: string (ex.: "0055-organizacao-institucional-bounded-context")
— herda EntityBase (audit + soft delete)
```

### Invariante de roster fechado

- Adicionar uma nova `AreaOrganizacional` é **decisão deliberada documentada em ADR** desta base (não apenas dado entrado pela UI admin).
- A admin UI **suporta** criação de áreas novas via endpoint, mas a operação exige role `plataforma-admin` E campo `AdrReferenceCode` não-vazio — o campo é persistido e fitness test no CI valida que cada `AdrReferenceCode` aponta para arquivo existente em `docs/adrs/`.
- Não é "áreas são imutáveis em código" (opção A — enum em Kernel). É "adicionar área é ato de governança com audit trail, não operação CRUD livre".

### `AreaCodigo` — identificador strongly-typed

`AreaCodigo` é `readonly record struct` em `OrganizacaoInstitucional.Contracts`:

- Construtor privado; factory `From(string)` retorna `Result<AreaCodigo>` com validação (2-32 chars, uppercase, alfanum + underscore, sem leading digit).
- Usado em todo o sistema: `Proprietario: AreaCodigo?` em entidades de catálogo, elementos de `AreasDeInteresse: IReadOnlySet<AreaCodigo>`, claims JWT derivados expõem `IReadOnlyCollection<AreaCodigo>`.
- Previne typo bugs (`"ceps"` vs `"CEPS"`) e centraliza o ponto de conversão.

### Acesso cross-módulo

`IAreaOrganizacionalReader` em `OrganizacaoInstitucional.Contracts` expõe:

- `Task<IReadOnlyList<AreaOrganizacionalView>> ListarAtivasAsync(CancellationToken)`
- `Task<AreaOrganizacionalView?> ObterPorCodigoAsync(AreaCodigo codigo, CancellationToken)`

Outros módulos consomem via DI in-process (ver [ADR-0056](0056-parametrizacao-modulo-e-read-side-carve-out.md)). Write-side cross-módulo permanece Kafka-only (ADR-0001).

### Convenção de roles Keycloak

Cada `AreaOrganizacional.Codigo` projeta-se em 2 roles convencionais:

- `{codigo-lowercase}-admin` — autoridade de escrita.
- `{codigo-lowercase}-leitor` — autoridade de leitura.

Adicionar uma área (com ADR) implica provisionar os roles correspondentes via o script de automação de identidade da plataforma. É parte do runbook de criação de área no `uniplus-infra`.

### O que entra no módulo (V1)

Apenas `AreaOrganizacional`. O módulo é deliberadamente enxuto.

### O que pode entrar depois (deferido)

- `HierarquiaInstitucional` — sub-áreas, parent-child (quando 2+ áreas pedirem subdivisão interna).
- `CargoInstitucional` — posições/papéis dentro de áreas (quando authz precisar de granularidade role-within-area além de admin/leitor).
- `VigenciaDelegacao` — delegação time-bounded de admin authority (quando handovers temporários entre áreas virarem rotina).

Cada extensão exige sua própria ADR com trigger condition documentado; nenhuma está em escopo V1.

## Consequências

### Positivas

- Cada área é dona de suas configurações de catálogo com provenance auditável.
- Novas áreas (e.g., futura PROAE para ações afirmativas) podem ser adicionadas via ADR + admin CRUD, sem mudança de código nem deploy.
- `EntityBase` permanece limpo — sem multi-tenancy plumbing platform-wide.
- Contratos V1 (cursor, Kafka envelope, idempotency, audit) não sofrem amendments.
- Roles Keycloak são previsíveis e self-documenting (convenção `{area}-admin`).
- Catálogos universais (Modalidade Lei 12.711) permanecem platform-administered com semântica explícita.
- Cooperação inter-área é o default (leituras entre áreas permitidas) — bate com a realidade operacional da Unifesspa.

### Negativas

- Entidades de catálogo área-scoped carregam campo extra `AreaProprietariaCodigo` (~16 bytes por linha).
- Lógica de autorização middleware é levemente mais complexa (lê área do recurso antes de checar role do caller).
- `plataforma-admin` editando dados área-scoped deve ser logado explicitamente com atribuição `on-behalf-of` — lógica audit extra.
- Frontend admin UI deve filtrar catálogos editáveis baseado nos roles do caller — coordenação UX leve.

### Neutras

- A entidade carrega `AdrReferenceCode` que aponta para arquivo neste repositório de ADRs; o link é validado em build time, não runtime.

## Confirmação

1. **Fitness test ArchUnitNET**: `AreasOrganizacionais_AdrReferenceCodes_DevemReferenciarArquivoExistente` lê `seeds/seed-areas-organizacionais.json` em test time e valida que cada `adrReferenceCode` corresponde a arquivo em `docs/adrs/`. Falha o build em drift.
2. **Provisioning script**: `tools/provision-area.sh` automatiza: criar áreas via API + criar roles Keycloak + atribuição inicial de admin user. Idempotente.
3. **Revisão arquitetural de PR**: PRs que tentem adicionar uma nova `AreaOrganizacional` sem ADR correspondente são rejeitados.

## Prós e contras das opções

### A — Enum C# fechado em `Kernel.Domain`

- **Prós**: Máxima simplicidade. Zero novo módulo. Exhaustiveness em compile-time.
- **Contras**: Cada nova área = mudança de código + deploy + binary release. Não consegue armazenar metadata (`Nome`, `Tipo`, `Descricao`). Não pode auditar quem adicionou ou quando. Não pode soft-delete (valores de enum são imortais). Adicionar PROAE em 2027 = nova release, não `POST /areas` com audit. Viola o princípio "configurável sem deploy" para um conceito de governança essencial.

### B — Entidade em `Kernel.Domain.Entities`

- **Prós**: Sem novo módulo skeleton. Todo módulo já referencia `Kernel`, então DI é trivial.
- **Contras**: `Kernel` é reservado para **primitivos sem lifecycle e sem comportamento** (`Cpf`, `Email`, `EntityBase`, `Result<T>`). `AreaOrganizacional` tem ciclo CRUD completo, autorização admin, audit, soft-delete e domain events. Hospedá-la em Kernel infla a superfície de Kernel com infra/admin concerns e gradualmente erode a disciplina "primitivos apenas".

### C — Co-hospedada em `Parametrizacao`

- **Prós**: Compartilha infra CRUD com catálogos irmãos. Um csproj a menos.
- **Contras**: Áreas não estão no mesmo plano que `Modalidade` ou `LocalProva`. Áreas têm **integridade referencial** com RBAC, JWT, Keycloak, audit e o `Proprietario` / `AreasDeInteresse` de todo outro catálogo. São o **sujeito** da autorização, não um **objeto** governado por ela. Co-hospedagem confunde as camadas conceituais.

### D — Apenas Keycloak groups, sem entidade no banco

- **Prós**: Sem schema de DB para áreas; Keycloak é fonte da verdade.
- **Contras**: Adicionar áreas é tarefa de admin Keycloak (out-of-band para admins de plataforma). Audit trail não consegue JOIN com metadata de áreas para relatórios. Display name (`"Centro de Processos Seletivos"` vs código `"CEPS"`) vive em atributos Keycloak, awkward para query. A `AreaOrganizacional` é referenciada vezes suficientes que ter como entidade simplifica o resto do sistema.

### E — Bounded context dedicado (escolhida)

- **Prós**: Separa governança organizacional de dados de catálogo. `AreaCodigo` strongly-typed elimina string-typo bugs. Roster expansion auditável (`AdrReferenceCode` enforçado). `AreaOrganizacional` tem ciclo de vida completo (audit, soft-delete, eventos) sem poluir `Kernel`. Conceitos futuros de hierarquia/cargo/delegação têm casa limpa — sem extração depois. Acesso cross-módulo via `IAreaOrganizacionalReader` mantém boundary consistente com o resto da plataforma.
- **Contras**: Um módulo adicional na solution (5 csproj). DI wiring em cada consumer. Build time cresce ~3-5 segundos. Novos devs precisam aprender que "áreas NÃO estão em Parametrizacao" — contraintuitivo para quem está acostumado a "todos os dados de referência em um módulo".

## Mais informações

- [ADR-0001](0001-monolito-modular-como-estilo-arquitetural.md) — Monolito modular: áreas como bounded context distinto.
- [ADR-0010](0010-audience-uniplus-em-tokens-oidc.md) — Audience Keycloak: roles `{area}-admin`/`{area}-leitor` projetados nesta convenção.
- [ADR-0019](0019-proibir-pii-em-path-segments-de-url.md) — Sem PII em URLs (informa postura LGPD; `DeletedBy` é JWT sub, nunca CPF).
- [ADR-0033](0033-icurrentuser-abstraction-via-iusercontext.md) — `IUserContext` é estendido com `AreasAdministradas: IReadOnlyCollection<AreaCodigo>` derivada dos roles JWT.
- [ADR-0034](0034-problemdetails-em-401-403-via-jwtbearer-events.md) — ProblemDetails em 401/403 herdado.
- [ADR-0056](0056-parametrizacao-modulo-e-read-side-carve-out.md) — Módulo Parametrizacao e carve-out read-side cross-módulo.
- [ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md) — RBAC baseado em áreas com snapshot, histórico e invariantes.
- [ADR-0058](0058-obrigatoriedade-legal-validacao-data-driven.md) — `ObrigatoriedadeLegal` como validação data-driven com citação legal.
- Confirmação do sponsor (2026-05-13): roster fechado com adições via ADR; single-deployment per institution.
