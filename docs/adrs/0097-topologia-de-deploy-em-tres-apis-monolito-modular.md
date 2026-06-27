---
status: "accepted"
date: "2026-06-26"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "Backend (CTIC)"
informed:
  - "Equipe Uni+"
---

# ADR-0097: Topologia de deploy em 3 APIs — módulos internos como libraries co-hospedadas

## Contexto e enunciado do problema

O [ADR-0001](0001-monolito-modular-como-estilo-arquitetural.md) escolheu o **monolito
modular** como estilo arquitetural, mas registrou a topologia de deploy como "cada
módulo é uma aplicação .NET independente, **deployável separadamente** no Kubernetes,
com comunicação inter-módulos exclusivamente por eventos assíncronos". À medida que o
backend cresceu (Selecao, Ingresso, Configuracao, OrganizacaoInstitucional, além de
Geo e Portal), essa granularidade de deploy "uma imagem por módulo" passou a cobrar
custos que não se justificam para a escala da equipe e da operação on-premises:

1. **Multiplicação de processos e imagens** — 6 `Program.cs`/`Dockerfile`, 6 health
   checks, 6 pipelines de boot, 6 instâncias de Wolverine + outbox, 6 conjuntos de
   migrations on startup. Operar e observar isso na infra enxuta da Unifesspa é
   desproporcional ao tráfego real.
2. **Leitura cross-módulo cara por construção** — toda consulta de um módulo a dados
   de outro precisaria sair pela rede (HTTP/eventos), mesmo quando ambos os módulos
   coabitam o mesmo nó. Um spike (P5) provou que **leitura cross-módulo in-process**
   é viável e suficiente para os casos de leitura atuais, mantendo as fronteiras.
3. **Colisão de `appsettings` no co-hosting** — ao referenciar os 4 `.API` (projetos
   `Sdk.Web`) num host único, cada um arrastava seu `appsettings*.json` para o publish,
   colidindo no mesmo destino (NETSDK1152). A "solução" era um target MSBuild de
   remoção — uma gambiarra que tratava o sintoma, não a causa.
4. **Bancos fragmentados** — um banco PostgreSQL por módulo, quando os 4 módulos
   internos poderiam coabitar um banco único com schema-por-módulo, simplificando
   provisionamento, backup e transações de outbox.

A questão: **qual a unidade de deploy correta para o monolito modular do Uni+?**

## Drivers da decisão

- Simplicidade operacional na infra on-premises da Unifesspa (equipe pequena, sem SRE dedicado).
- Preservar as **fronteiras de módulo** (bounded contexts) independentemente da granularidade de deploy.
- Eliminar gambiarras na raiz (colisão de `appsettings`, instâncias redundantes de Wolverine).
- Manter Geo (banco isolado com PostGIS, read-mostly) e Portal como deployables autônomos quando fizer sentido.

## Opções consideradas

- **3 APIs executáveis** (Geo, Portal, UniPlus) com os 4 módulos internos como class libraries co-hospedadas na API UniPlus.
- **N APIs por módulo** (topologia original do ADR-0001): uma imagem deployável por módulo.
- **Monolito único** (uma só imagem para tudo, inclusive Geo/Portal) sem distinção de deployable.

## Resultado da decisão

**Escolhida:** **3 APIs executáveis** — **Geo**, **Portal** e **UniPlus**.

- A **API UniPlus** (`Unifesspa.UniPlus.Host`) é o composition root que **compõe os 4
  módulos internos** — Selecao, Ingresso, Configuracao, OrganizacaoInstitucional — num
  **único processo**. Esses módulos deixam de ser executáveis: seus projetos `.API`
  passam a **class libraries** (`Sdk` padrão + `FrameworkReference` do
  `Microsoft.AspNetCore.App`), sem `Program.cs` nem `appsettings*.json` próprios.
  Controllers, `Add{Modulo}Module` e wiring de mensageria continuam vivendo no `.API`
  de cada módulo — apenas o entry point é único (o host).
- **Geo** e **Portal** permanecem **deployables autônomos**, com `Program.cs`,
  `Dockerfile` e banco próprios (Geo usa `uniplus_geo` com PostGIS — ADR-0090/0091;
  Portal usa `uniplus_portal`).
- O monolito usa um **banco único `uniplus`** com **schema-por-módulo**
  (`HasDefaultSchema`) + schema `wolverine` para o outbox. As 5 connection strings do
  host apontam para o mesmo banco; os schemas são materializados pelas migrations on
  startup. Uma **única** instância de Wolverine + outbox serve os 4 módulos.
- **Leitura cross-módulo in-process** é permitida ao host (composition root); a escrita
  e os eventos de domínio seguem o outbox transacional (ADR-0004) e cascading messages
  (ADR-0005).

As **fronteiras de módulo continuam valendo** e são travadas por fitness tests: o R8
(`CrossModuleReadIsolationTests`, ADR-0056) proíbe um módulo depender do `Domain`/
`Application`/`Infrastructure`/`API` de outro — só o **host** é isento (é o único
autorizado a compor todos). A ordem migrations→Wolverine é travada por
`MigrationBeforeWolverineRuntimeOrderTests` (#419), que agora assere o conjunto exato
de DbContexts co-hospedados pelo host.

## Consequências

**Positivas:**

- Operação reduz de 6 para 3 processos/imagens; um boot, um health agregado, um outbox.
- Colisão de `appsettings` resolvida na raiz — libraries não emitem `appsettings`, o
  target MSBuild de remoção foi excluído (não é mais necessário).
- Leitura cross-módulo trivial vira chamada in-process; escrita permanece desacoplada por eventos.
- Banco único simplifica provisionamento e backup dos módulos internos.

**Negativas / trade-offs:**

- O deploy dos 4 módulos internos passa a ser **acoplado** (sobem/caem juntos). Aceito:
  os 4 compartilham janela de disponibilidade (processos seletivos) e equipe.
- A fronteira de módulo passa a ser garantida **só por fitness tests** (não mais pelo
  limite de processo). Mitigado: o R8 é parte do CI e falha o build em violação.
- Se um módulo interno precisar de escala independente no futuro, será necessário
  extraí-lo de volta para um executável — reversão suportada pela estrutura em camadas
  (basta reintroduzir `Program.cs`/`Dockerfile` e o deploy próprio).

## Validação

Spike `spike/monolito-modular`, validado end-to-end:

- `dotnet build` + `dotnet test` da solution verdes (incl. fitness tests R8 e ordem
  migrations→Wolverine adaptados à topologia).
- Stack Docker (`docker-compose.yml` + `docker-compose.override.yml`) sobe a API
  UniPlus contra o banco único `uniplus` + infra (Kafka/Apicurio/Redis/MinIO/Keycloak).
- OIDC validado: token via Keycloak (realm `unifesspa-dev-local`) + validação JWT real.
- Newman (coleção Organizacao apontada à API UniPlus): token + negativos de auth
  (401/400/422) + ciclo de vida CRUD + idempotência + soft-delete, tudo através do
  monolito.

## Relação com outras ADRs

- **Refina** o [ADR-0001](0001-monolito-modular-como-estilo-arquitetural.md): mantém o
  estilo "monolito modular", mas altera a **unidade de deploy** dos módulos internos de
  "uma imagem por módulo" para "co-hospedados na API UniPlus".
- **Depende de** ADR-0004 (outbox transacional), ADR-0005 (cascading messages),
  ADR-0056 (fitness tests de isolamento cross-módulo), #419 (ordem migrations→Wolverine).
- **Não altera** Geo (ADR-0090/0091) nem Portal, que seguem deployables autônomos.
