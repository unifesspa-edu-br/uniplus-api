# Spike — Viabilidade de Event Sourcing com Marten (gate da ADR-0069)

- **Issue:** uniplus-api#540
- **ADR:** [ADR-0069 — Event Sourcing seletivo com Marten](../adrs/0069-event-sourcing-seletivo-marten-contextos-criticos.md) (status `proposed`)
- **Código do spike:** `spikes/EventSourcingSpike.slnx` (ilha isolada, fora do `UniPlus.slnx`)
- **Contexto de teste:** ciclo de vida do *Edital* event-sourced (`EditalEs`), como cobaia descartável — não toca o módulo Seleção de produção.

## Resumo executivo

O Event Sourcing seletivo com **Marten 8.37.1 + WolverineFx.Marten 5.39.3** é **tecnicamente viável** sob a stack atual (.NET 10, PostgreSQL 18, Npgsql 10.0.2, Wolverine 5.39). Os seis portões da ADR-0069 foram exercidos por **15 testes de integração/arquitetura verdes** (Testcontainers Postgres 18), build com **zero avisos** sob os analisadores estritos do projeto.

**Recomendação:** promover a ADR-0069 a `accepted` **mantendo as condições** já previstas (desacoplada do pacote primário; piloto = homologação documental), com **três decisões de produção** a fechar antes da implementação do piloto: (1) a **topologia de coabitação** entre o outbox do Marten e o outbox EF Core atual; (2) a **externalização das chaves** de cifragem para um cofre (Vault/KMS); (3) a **eliminação da correlação pseudônima residual** no log (identificador de titular / id de chave que sobrevivem ao shredding). A decisão de gate é do Tech Lead.

## Veredito por portão

| Gate | Pergunta | Veredito | Evidência (teste) |
|------|----------|----------|-------------------|
| T0 | Marten + WolverineFx.Marten funcionam sob .NET 10 / Npgsql 10? | ✅ Sim | `CompatibilidadeRuntimeTests` (append + projeção inline + stream cru) |
| G1 | `append` (Marten) + `envelope` (outbox Wolverine) commitam na mesma transação? | ✅ Sim | `AtomicidadeTests` (happy: entrega; rollback: nada commitado) |
| G2 | Marten coabita com o backbone PostgreSQL/Wolverine sem componente novo? | ✅ Sim (host-level) | `Coexistencia*`/`MultiNoLeaderElectionTests` — EF main + Marten ancillary, cluster com failover |
| G3 | Crypto-shredding atende LGPD sobre log append-only? | ⚠️ Conteúdo sim; resta correlação pseudônima | `CryptoShreddingTests` (revelável → esquecer → conteúdo irrecuperável; fato permanece) |
| G4 | Replay reconstrói estado e projeções? | ✅ Sim | `ReplayProjecaoTests` (replay == inline; rebuild via daemon) |
| G5 | Evolução de evento (upcasting) é viável? | ✅ Sim | `UpcastingTests` (v1 legado lido como v2) |
| G6 | A fronteira EF Core × Marten é enforçável? | ✅ Sim (no escopo do spike) | `FronteiraArchTests` (Domain/Application limpos de Marten/Wolverine) |

## Como cada portão foi provado

### T0 — Compatibilidade de stack (gate de continuidade)

`WolverineFx.Marten 5.39.3` tem target `net10.0` e fixa `Marten 8.35.0` (usamos `8.37.1`). A linhagem transitiva do Marten traz plugins Npgsql **v9** (`Npgsql.Json.NET`, `Weasel.Postgresql`), mas o pin de produção **Npgsql 10.0.2** satisfaz os mínimos (`>= 9.0.4`) e **funciona em runtime** — provado por append real + projeção inline contra Postgres 18.

### G1 — Atomicidade append + outbox

Os handlers usam o **Aggregate Handler Workflow** idiomático do Wolverine (`[WriteAggregate]` → `FetchForWriting`/append/`SaveChangesAsync`), retornando `(Events, OutgoingMessages)`. Com `AutoApplyTransactions` + `IntegrateWithWolverine`, o append e o envelope de integração compartilham a transação:

- **Happy:** publicar anexa `EditalPublicado` **e** entrega `EditalPublicadoIntegrado` pelo outbox durável (observado via `ColetorIntegracao`).
- **Rollback:** um handler que anexa + publica e então lança não commita **nem** o evento **nem** o envelope.

### G2 — Coabitação (host-level resolvida)

Inicialmente o spike provou apenas a coabitação no mesmo schema. A questão **host-level** — rodar o outbox EF Core de produção **junto** com o Marten num só host — foi então **fechada** (ver [proposta de topologia](adr-0069-coexistencia-marten-efcore-proposta.md)):

- A limitação "um message store por host" é **pré-Wolverine 5**; o projeto roda 5.39, que suporta múltiplos stores via **um 'main' + N 'ancillary'**. A config ingênua (dois 'main') falha com erro acionável (`InvalidWolverineStorageConfigurationException`).
- Topologia provada: **EF Core/Postgres como store 'main'** (`PersistMessagesWithPostgresql`, preserva ADR-0004) + **Marten como store 'ancillary'** (`AddMartenStore<IEditalEsStore>().IntegrateWithWolverine()`), que **compartilha o envelope storage operacional** do main.
- `CoexistenciaBootTests` (boot dos dois stores), `CoexistenciaTests` (handler EF main atômico+outbox; event store ancillary utilizável), `MultiNoLeaderElectionTests` (2 réplicas em `Balanced`, eleição de líder, failover com ejeção do nó morto e continuidade) e `ConcorrenciaStreamTests` (mecanismo de base da escala: dois writers no mesmo stream → concorrência otimista por versão imposta no banco; perdedor leva `EventStreamUnexpectedMaxEventIdException`, faz retry, sem lost update) provam a coabitação, o cluster e a concorrência por-stream. O caminho multi-réplica via handler `[WriteAggregate]` herda a garantia de concorrência mas não é exercitado ponta a ponta.
- **Caveat de teste (não é defeito):** um handler `[MartenStore]` cascateando pelo outbox falha quando **múltiplos apps Wolverine coexistem no mesmo processo de teste** (cache de geração de código do JasperFx compartilhado) — em produção há um app por processo. O store ancillary é, por isso, exercido por append direto no teste; o handler `[MartenStore]` funciona quando isolado.

### G3 — LGPD / crypto-shredding

PII do ator (nome, CPF) é cifrada com **AES-256-GCM** antes do append; o **conteúdo** sensível some no esquecimento. As chaves vivem num **unit-of-work próprio** (modela um cofre separado). Esquecer um titular apaga suas chaves; a leitura passa a retornar `null` (sem lançar) e **o fato/stream permanece íntegro** — o log append-only não é mutado. Cada evento referencia o `ChaveId` que o cifrou, então o esquecimento e o reaparecimento de um titular nunca corrompem a leitura de eventos antigos.

> **Caveat de privacidade (importante para o gate LGPD).** O `AtorCifrado` do spike persiste, **em claro no log append-only**, o `SujeitoId` (identificador estável do titular); além disso, como o spike reutiliza **uma chave por sujeito**, o `ChaveId` é **compartilhado** entre todos os eventos daquele titular. Após o shredding, o conteúdo (nome/CPF) é irrecuperável, mas **esses dois identificadores pseudônimos permanecem** e ainda permitem correlacionar "estes eventos foram do mesmo titular (agora esquecido)". Sob a LGPD, identificador pseudônimo estável ainda é dado pessoal. O spike prova a mecânica de shredding de **conteúdo**, não a eliminação de correlação. Ver recomendação de produção nas limitações.

### G4 — Replay e projeções

A projeção single-stream `EditalEs` (PII-free) é materializada **inline**. O replay puro (`AggregateStreamAsync`) reconstrói estado idêntico à projeção inline, e o **rebuild via daemon** reproduz o read model a partir dos eventos.

### G5 — Versionamento / upcasting

Um evento `v1` persistido por "código antigo" é lido como `v2` por "código novo" via `o.Events.Upcast<v1, v2>(...)`, sem reescrever o passado.

### G6 — Fronteira enforçável

Fitness tests ArchUnitNET garantem que **Domain** e **Application** não dependem de `Marten` nem de `Wolverine` (só a Infrastructure conhece a stack), e que eventos de domínio são `sealed`.

## Decisões de design tomadas no spike

1. **Handlers ES em Infrastructure.** O Aggregate Handler Workflow (`[WriteAggregate]`) acopla o handler a `Wolverine.Marten`. Para preservar a invariante da ADR-0003 (Application livre de Wolverine), os handlers event-sourced ficam na Infrastructure; Domain e Application permanecem limpos (verificado por G6).
2. **Tipos públicos para o Marten.** A geração dinâmica de código do Marten roda num assembly que não enxerga tipos `internal` — documentos e agregados precisam ser `public`.
3. **Chaves de PII em UoW próprio.** O protetor gere sessões Marten próprias via `IDocumentStore`; a chave é persistida independentemente do append. Isso é mais fiel à recomendação de produção (cofre externo) e elimina o acoplamento frágil de sessão.
4. **`ChaveId` por chave** (não por sujeito) no payload cifrado: torna o crypto-shredding correto sob esquecimento + reaparecimento e tolerante a corridas de criação.
5. **Relógio injetado** (`TimeProvider`, ADR-0068) e **filas locais duráveis** (`UseDurableLocalQueues`) para o evento de integração passar pelo outbox.

## Limitações e questões em aberto (para a decisão de gate)

1. **Coabitação host-level — RESOLVIDA.** Provada num só host via **EF Core 'main' + Marten 'ancillary'** (Wolverine 5 multi-store), com cluster (2 réplicas, leader election, failover) verde. Detalhes e topologia recomendada na [proposta de coabitação](adr-0069-coexistencia-marten-efcore-proposta.md). Resta apenas a **decisão de produção** sobre detalhes operacionais (schema de envelope compartilhado vs separado, DLQ/replay por store), não viabilidade.
2. **Provisioning de schema.** O spike usa `AutoCreate.All`; em produção o schema é responsabilidade do deploy (ADR-0039), não auto-create em runtime.
3. **Externalização de chaves.** No spike as chaves vivem numa coleção Marten; em produção devem residir em Vault/KMS, fora do banco dos eventos.
4. **Correlação pseudônima residual (LGPD).** O spike retém `SujeitoId` em claro e um `ChaveId` compartilhado por titular nos eventos, que sobrevivem ao shredding. Para o piloto recomenda-se: **(a)** não persistir identificador de titular em claro no evento; **(b)** usar **chave única por evento** (`ChaveId` aleatório por append) para que o id da chave não correlacione eventos; **(c)** manter o mapeamento `titular → [ChaveId]` apenas no cofre (deletável), de modo que o esquecimento elimine também a capacidade de correlação. Assim o log append-only fica, após o shredding, sem qualquer handle de correlação do titular.
5. **Projeções inline vs async.** O spike usa projeção inline (consistência forte). Streams longos ou muitas projeções podem exigir projeções assíncronas (consistência eventual explícita).
6. **Versionamento de eventos é disciplina permanente** (upcasters mantidos por anos).

## Conclusão

A viabilidade técnica está **comprovada**. O risco remanescente não é "se o Marten funciona", e sim **como integrá-lo à topologia de outbox de produção** — uma decisão de arquitetura, não de viabilidade. Recomenda-se promover a ADR-0069 a `accepted` com as condições acima e abrir o piloto de **homologação documental** já endereçando a topologia de coabitação como primeira tarefa de design.
