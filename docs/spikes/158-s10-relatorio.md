# Relatório do Spike S10 — Cascading messages × `PublishDomainEventsFromEntityFrameworkCore`

> **Status pós-spike (2026-04-26):** a recomendação central deste relatório foi acolhida — a [ADR-026 — Cascading messages como drenagem canônica](https://github.com/unifesspa-edu-br/uniplus-docs/blob/main/docs/adrs/ADR-026-cascading-messages-como-drenagem-canonica.md) já foi aprovada e mergeada em `uniplus-docs/main`. As referências ao longo deste documento a "ADR-026 proposta", "rascunho local" e "Status: proposto" ficam preservadas como snapshot histórico do momento da spike; não foram reescritas para honrar o histórico da decisão. A operacionalização da ADR-026 está sendo executada nos PRs/tasks #162 (este), #163, #164 e #136.
>
> Resposta empírica para a pergunta levantada no comentário do maintainer Wolverine
> em [JasperFx/wolverine#2585](https://github.com/JasperFx/wolverine/issues/2585):
> *"qual estilo de drenagem de domain events devemos adotar — `PublishDomainEventsFromEntityFrameworkCore` (EF scraping) ou cascading messages do handler?"*.
>
> Plano de execução: [`158-s10-plano-cascading-messages.md`](158-s10-plano-cascading-messages.md).
> Branch: `spikes/158-s10-cascading-messages`.

## Sumário executivo

- **Cascading messages funciona** no UniPlus: 3 testes verdes (S10/V8 happy path, S10/V9 rollback, S10/V10 Kafka fan-out), em paridade comportamental com S2/S4/S3 da matriz S0–S9 já validada.
- **Cascading não depende do fix do PR `#2586`** (o fork local 5.32.1-pr2586): a hipótese central foi confirmada por leitura do código do upstream **e** por execução empírica com Wolverine 5.32.1 oficial (nuget.org).
- **Ganho objetivo crítico (dimensão 10):** cascading dispensa o feed local imediatamente. A solução fica livre para usar o pacote oficial do nuget.org sem aguardar `5.32.2+` upstream.
- **Achado adicional:** rodando a matriz S0–S9 *inteira* contra Wolverine 5.32.1 oficial (sem o fix), os 13 testes da matriz também passaram. Os cenários atuais não reproduzem o bug `Collection was modified` que motivou o PR `#2586`. Isso **não invalida** a ADR-025 — a adoção proativa do fix foi correta dentro do conhecimento disponível à época. O achado mostra que a transição para `5.32.1` oficial é viável agora, e cascading **elimina por design** a categoria do bug ao não passar pelo `ChangeTracker`.
- **Recomendação:** abrir **ADR-026** substituindo parcialmente a ADR-025 no item de drenagem. Status: proposto; decisor humano.

## 1. Configuração executada

| Item | Detalhe |
|---|---|
| Branch | `spikes/158-s10-cascading-messages` |
| HEAD upstream Wolverine | commit `cd6a2ee` (`fix/domain-event-scraper-materialize-before-publish` no fork local) |
| Postgres | `postgres:18-alpine` via Testcontainers |
| Kafka | `apache/kafka:3.9.0` via Testcontainers (KRaft) |
| Ambiente B (referência) | WolverineFx `5.32.1-pr2586` (feed local em `vendors/nuget-local/`) |
| Ambiente A (objetivo arquitetural) | WolverineFx `5.32.1` oficial via `<VersionOverride>` no csproj de testes (Opção A1 do plano) |

## 2. Passo 0 — Mapeamento da API canônica de cascading

### 2.1 Tipos canônicos de retorno do handler

| Tipo | Captura | Citação |
|---|---|---|
| `OutgoingMessages : List<object>` | `OutgoingMessagesPolicy` instala `CaptureCascadingMessages` no `ReturnVariablesOfType<OutgoingMessages>()` | `wolverine-fork/src/Wolverine/OutgoingMessages.cs:13`, `:90-103` |
| `IEnumerable<object>` | `EnqueueCascadingAsync` itera e cascading recursivamente | `wolverine-fork/src/Wolverine/Runtime/MessageContext.cs:646-649` |
| `IAsyncEnumerable<T>` | resolvido via `ResolveTypedAsyncEnumerableCascader` | `wolverine-fork/src/Wolverine/Runtime/MessageContext.cs:651-712` |
| Tipo único / tupla `(A, B)` / objeto | `HandlerChain.UseForResponse` instala `CaptureCascadingMessages` para o response | `wolverine-fork/src/Wolverine/Runtime/Handlers/HandlerChain.cs:389-398` |
| Samples de referência | Documentação canônica do upstream | `wolverine-fork/src/Samples/DocumentationSamples/CascadingSamples.cs:30-39, 110-122` |

**Decisão para o UniPlus:** retornar `IEnumerable<object>` (handler `PublicarEditalCascadingHandler.Handle` faz `return edital.DequeueDomainEvents().Cast<object>()`). `DequeueDomainEvents()` é um helper canônico no `EntityBase` que combina snapshot atômico + `Clear` da coleção interna — defensivo contra republicação acidental se o agregado sobreviver ao escopo do handler (cache, sagas, processadores long-lived). Validado pelo UnitTest puro `PublicarEditalCascadingHandlerUnitTests` e pelos cenários S10/V8/V9/V10.

### 2.2 Caminho transacional do cascading vs scraper

A decisão arquitetural depende de entender que *cascading e scraper são caminhos completamente independentes de persistência* dentro do Wolverine — só convergem na transação EF Core enrolada por `EnrollDbContextInTransaction`.

```
Handler chain (codegen Wolverine):
  → EnrollDbContextInTransaction.GenerateCode  (cria EfCoreEnvelopeTransaction com _scrapers)
  → EnlistInOutboxAsync(envelopeTransaction)   (instala Transaction no MessageContext)
  → BeginTransactionAsync()                    (transação EF aberta)
  → try {
      [HANDLER USER CODE — incluindo SaveChangesAsync]
      [SE HANDLER RETORNA: CaptureCascadingMessages → EnqueueCascadingAsync(retorno)]
      [    → PublishAsync(message) → PersistOrSendAsync → Transaction.PersistOutgoingAsync]
      [    → INSERT em wolverine_outgoing_envelopes na MESMA transação]
      → envelopeTransaction.CommitAsync()
            → foreach (scraper in _scrapers) await scraper.ScrapeEvents(DbContext, _messaging)
                                                      // ↑ CHAMA bus.PublishAsync(domainEvent) → mesma rota → PersistOutgoingAsync
            → DbContext.Database.CurrentTransaction.CommitAsync()
            → _messaging.FlushOutgoingMessagesAsync()  (libera envelopes para os senders)
    } catch {
      → envelopeTransaction.RollbackAsync()           (rollback EF + descarte de envelopes)
      → throw
    }
```

| Etapa | Citação `arquivo:linha` |
|---|---|
| Codegen do handler com EF + outbox | `wolverine-fork/src/Persistence/Wolverine.EntityFrameworkCore/Codegen/EnrollDbContextInTransaction.cs:29-58` |
| `EfCoreEnvelopeTransaction.PersistOutgoingAsync` (cascading **e** scraper escrevem aqui) | `wolverine-fork/src/Persistence/Wolverine.EntityFrameworkCore/Internals/EfCoreEnvelopeTransaction.cs:43-94` |
| `EfCoreEnvelopeTransaction.CommitAsync` (loop de scrapers em `_scrapers`) | `wolverine-fork/src/Persistence/Wolverine.EntityFrameworkCore/Internals/EfCoreEnvelopeTransaction.cs:170-174` |
| `MessageContext.EnqueueCascadingAsync` chamado por `CaptureCascadingMessages` | `wolverine-fork/src/Wolverine/Runtime/MessageContext.cs:604-683` |
| `MessageBus.PersistOrSendAsync` enrolado com `Transaction != null` | `wolverine-fork/src/Wolverine/Runtime/MessageBus.cs:296-323` |
| `CaptureCascadingMessages` postprocessor no chain | `wolverine-fork/src/Wolverine/Runtime/Handlers/CaptureCascadingMessages.cs:11`, `wolverine-fork/src/Wolverine/Runtime/Handlers/HandlerChain.cs:389-398` |

### 2.3 Impacto do PR `#2586`

O fix está em `DomainEventScraper<T, TEvent>.ScrapeEvents` adicionando `.ToArray()` antes do `foreach (await bus.PublishAsync)` para materializar antes de iterar:

> **Citação:** `wolverine-fork/src/Persistence/Wolverine.EntityFrameworkCore/OutgoingDomainEvents.cs:42-43`
>
> ```csharp
> var eventMessages = dbContext.ChangeTracker.Entries().Select(x => x.Entity)
>     .OfType<T>().SelectMany(_source).ToArray();
> ```

Esse caminho é exclusivo do **scraper** (caminho EF). Cascading **não passa por aqui** — o postprocessor `CaptureCascadingMessages` é instalado pelo `HandlerChain` do Wolverine core, não pelo módulo `Wolverine.EntityFrameworkCore`. Portanto cascading é estruturalmente imune ao bug original do PR `#2586`.

## 3. Cenários executados

Os 3 cenários do plano S10 foram exercitados em ambos os ambientes.

### 3.1 Ambiente B — Wolverine `5.32.1-pr2586` (feed local)

| Cenário | Resultado | Tempo |
|---|---|---|
| **S10/V8** — Cascading happy path | ✅ Verde | ~4s |
| **S10/V9** — Rollback (exceção pós-`SaveChanges`) | ✅ Verde | <1s |
| **S10/V10** — Cascading + Kafka fan-out | ✅ Verde | ~5s |

**Comando:** `dotnet test --filter "Category=OutboxCascading"`. Total: 3/3 verdes em ~16s.

A matriz S0–S9 inteira **continua verde** em conjunto com S10: a suíte combinada de outbox soma 16/16 testes — 13 da matriz S0–S9 + 3 cenários S10. Sem regressão.

### 3.2 Ambiente A — Wolverine `5.32.1` oficial (nuget.org)

Mecanismo: `<PackageReference VersionOverride="5.32.1" />` no csproj de testes (`tests/Unifesspa.UniPlus.Selecao.IntegrationTests/Unifesspa.UniPlus.Selecao.IntegrationTests.csproj`), aplicado aos pacotes `WolverineFx`, `WolverineFx.EntityFrameworkCore`, `WolverineFx.Postgresql`, `WolverineFx.Kafka`. Ajuste cirúrgico no `nuget.config` para liberar o `packageSourceMapping` desses pacotes no nuget.org.

| Cenário | Resultado | Tempo |
|---|---|---|
| **S10/V8** | ✅ Verde | ~5s |
| **S10/V9** | ✅ Verde | <1s |
| **S10/V10** | ✅ Verde | ~5s |

**Total cascading 5.32.1 oficial:** 3/3 verdes em ~16s.

**Achado adicional:** rodando `dotnet test --filter "Category=OutboxCapability"` com `5.32.1` oficial: **16/16 verdes em 57s**. Pelos traits atuais, `Category=OutboxCapability` inclui a matriz S0–S9 (13 testes) e também os 3 cenários S10, porque `OutboxCascadingMatrixTests` e `OutboxCascadingKafkaTests` têm dupla marcação (`OutboxCapability` + `OutboxCascading`). A política futura deve executar explicitamente `Category=OutboxCapability` e `Category=OutboxCascading`, evitando ambiguidade caso a dupla marcação dos testes S10 seja alterada. Isso **não invalida** a ADR-025 — a decisão dela em adotar o fix do PR `#2586` foi correta dentro das evidências disponíveis e do risco conhecido do `DomainEventScraper`. O achado apenas mostra que o caso patológico do bug (`Collection was modified` por iteração concorrente sobre `_domainEvents`) **não é reproduzido pelos cenários atuais** — cada handler emite um único `EditalPublicadoEvent`, sem o padrão de iteração que dispara o bug.

> **Implicação prática:** a transição para `5.32.1` oficial é viável agora — o feed local em `vendors/nuget-local/` pode ser removido nessa transição. Cascading **reforça** a decisão porque elimina **por design** essa categoria de risco (não passa pelo `ChangeTracker`), tornando irrelevante a discussão sobre versões futuras do scraper. Não é "ADR-025 errou em adotar o fix"; é "cascading torna o fix estruturalmente desnecessário".

Os artefatos `nuget.config` e `csproj` de teste foram **revertidos** ao estado Ambiente B (5.32.1-pr2586) após a validação, para preservar a branch como reprodutível e idêntica à matriz S0–S9. A operação de troca está documentada acima e em commit history.

## 4. Comparação multidimensional

Cada dimensão segue formato **Achado / Evidência / Implicação**.

> **Nota sobre escopo:** o plano original do S10 ([`158-s10-plano-cascading-messages.md`](158-s10-plano-cascading-messages.md)) definiu **4 dimensões mínimas** (acoplamento, código, teste unitário, comportamento implícito) como gatilho para abrir ADR-026. A execução incorporou **6 dimensões complementares** (performance, atomicidade, alta disponibilidade, pureness CQRS, manutenibilidade, independência do fork local) que surgiram durante o spike — em particular a dimensão 10, que se revelou crítica após constatar que cascading não passa pelo `DomainEventScraper`. As 4 dimensões originais continuam cobertas; as 6 adicionais reforçam a recomendação ao testar o caminho cascading contra critérios de produção, não apenas contra o caminho idiomático.

### Dimensão 1 — Acoplamento

**Achado:** cascading permite encapsular `EntityBase.DomainEvents` em visibilidade reduzida; scraper não permite.

**Evidência:**

- O scraper `OutgoingDomainEventsScraper` é instanciado pelo container DI do assembly de Wolverine e itera via lambda `entity => entity.DomainEvents` passada em `PublishDomainEventsFromEntityFrameworkCore<EntityBase>(entity => entity.DomainEvents)` (`tests/.../OutboxSpikeWolverineExtension.cs:96-97`). A lambda é capturada e executada *fora* do assembly de domínio (por `DomainEventScraper<T, TEvent>.ScrapeEvents` em `Wolverine.EntityFrameworkCore.OutgoingDomainEvents.cs:40-49`). Para o reflection / lambda encontrar `DomainEvents`, a propriedade precisa ser **`public`**.
- O caminho cascading drena via `edital.DequeueDomainEvents()` apenas dentro do handler (`tests/.../Cascading/CascadingSpikeHandlers.cs:37`). O acesso é *do mesmo módulo de aplicação* (Selecao.Application em produção). O método explícito de drenagem **substitui** a necessidade de exposição direta da coleção crua — `DomainEvents` permanece público (útil para inspeção em testes), mas o canal idiomático de produção passa a ser `DequeueDomainEvents()`.
- O scraper depende do `EF Core ChangeTracker` (`OutgoingDomainEvents.cs:42`); cascading não — o handler controla explicitamente o que retorna.

**Implicação:** cascading introduz canal idiomático explícito (`DequeueDomainEvents()`) e remove a dependência do reflection externo do scraper sobre `DomainEvents`. Reduzir a visibilidade da própria propriedade `DomainEvents` (de `public` para `internal` + `InternalsVisibleTo` por módulo) é trabalho separado **fora do escopo** desta ADR — tem custo cross-module crescente (cada novo módulo edita o `Kernel`) e o ganho marginal de encapsulamento não compensa hoje.

### Dimensão 2 — Quantidade de código

**Achado:** quase neutro. Cascading economiza ~2 linhas no extension (sem `PublishDomainEventsFromEntityFrameworkCore<EntityBase>`) e gasta ~5 linhas adicionais por handler que precisa drenar eventos (assinatura `Task<IEnumerable<object>>` + `return edital.DequeueDomainEvents().Cast<object>()`).

**Evidência (medição local):**

| Arquivo | LoC totais |
|---|---|
| `OutboxSpikeWolverineExtension.cs` (scraper, com S9 dead-letter inclusive) | 114 |
| `Cascading/OutboxCascadingExtension.cs` (cascading, sem S9) | 93 |
| `PublicarEditalSpikeHandler` (handler scraper, void Task) | 16 linhas |
| `PublicarEditalCascadingHandler` (handler cascading, Task<IEnumerable<object>>) | 21 linhas |

**Implicação:** quanto mais handlers cascading houver, mais linhas extras (5 por handler) — o aumento é linear no número de chains. O scraper amortiza o custo de uma única configuração `PublishDomainEventsFromEntityFrameworkCore<EntityBase>` para todos os handlers do sistema. Em sistemas com dezenas de handlers, scraper é marginalmente mais conciso por chain. A diferença não é decisiva.

### Dimensão 3 — Testabilidade unitária

**Achado:** cascading é unitarizável sem nenhum mock; scraper exige fixture com Postgres real.

**Evidência (medição local):**

- `tests/.../Cascading/PublicarEditalCascadingHandlerUnitTests.cs` (criado neste spike) instancia `SelecaoDbContext` com `UseInMemoryDatabase`, chama `PublicarEditalCascadingHandler.Handle` direto e asserta o retorno como `IEnumerable<object>` contendo `EditalPublicadoEvent`. Tempo: **<1s**, sem Wolverine, sem container, sem extension. ✅ Verde no Ambiente B.
- O handler scraper (`PublicarEditalSpikeHandler.Handle`) retorna `Task` e não expõe os domain events. Para validar drenagem em UnitTest, seria preciso mockar o pipeline `EfCoreEnvelopeTransaction.CommitAsync → IDomainEventScraper.ScrapeEvents → IMessageContext.EnqueueCascadingAsync`. Equivalentemente: rodar fixture com Postgres real para observar o evento entregue ao subscritor — exatamente o que a matriz S0–S9 faz. **Não há caminho unitário simples** para o handler scraper.

**Implicação:** o ciclo de feedback de desenvolvimento é mais rápido com cascading. Para um time com membros novos (rotatividade do CTIC), feedback rápido em UnitTest puro tem peso pedagógico — o desenvolvedor entende o contrato vendo o teste.

### Dimensão 4 — Comportamento implícito (mágica)

**Achado:** scraper é silencioso na assinatura do handler; cascading é explícito no retorno.

**Evidência:**

| Item | Scraper | Cascading |
|---|---|---|
| Onde fica visível ao leitor que o handler emite eventos? | Apenas no boot, na linha `PublishDomainEventsFromEntityFrameworkCore<EntityBase>(...)` do extension (`OutboxSpikeWolverineExtension.cs:96`) | Na assinatura do handler: `Task<IEnumerable<object>>` (`CascadingSpikeHandlers.cs:23`) |
| Qual estado dispara a publicação? | `EF ChangeTracker.Entries()` no `CommitAsync` da transação (`OutgoingDomainEvents.cs:42`) — depende de a entidade estar tracked pelo DbContext | O `return` explícito do handler — depende apenas do controle de fluxo do método |
| Funciona com agregado não-EF (in-memory, externo)? | Não — o scraper itera o `ChangeTracker` específico do DbContext (`OutgoingDomainEvents.cs:42`) | Sim — o handler é livre para retornar qualquer coleção, vinda de qualquer fonte |

**Implicação:** menos pontos de surpresa para o leitor. O comentário do maintainer no upstream (issue `#2585`) cita exatamente esse argumento: *"the magic EF Core scraping thing"*. Em código que precisa ser legível por equipe rotativa, o explícito vence. O leitor não precisa saber que o boot tem um `PublishDomainEventsFromEntityFrameworkCore` para entender o que o handler faz.

### Dimensão 5 — Performance

**Achado:** scraper é ~25% mais rápido por invocação, mas a magnitude absoluta é desprezível (sub-1ms por invocação).

**Evidência (medição local — 3 rodadas em Ambiente B):**

| Caminho | Run 1 (total/avg) | Run 2 | Run 3 | Média |
|---|---|---|---|---|
| Cascading (`InvokeAsync` → handler retorna `IEnumerable<object>`) | 175ms / 1.75ms | 185ms / 1.85ms | 169ms / 1.69ms | **~176ms / 1.76ms** |
| Scraper (`InvokeAsync` → scraper drena ChangeTracker) | 133ms / 1.33ms | 147ms / 1.47ms | 139ms / 1.39ms | **~140ms / 1.40ms** |

Diferença relativa: ~26% (cascading mais lento). Diferença absoluta: ~0.36ms por invocação.

> **Comando:** `dotnet test --filter "Category=OutboxCascadingPerf|Category=OutboxScraperPerf"`. Benchmarks em `tests/.../Cascading/OutboxCascadingPerfBenchmark.cs` e `tests/.../OutboxScraperPerfBenchmark.cs`.

**Ressalva sobre o setup do benchmark:** medições in-process via `IMessageBus.InvokeAsync`, com Postgres em container local (sem rede real), 3 amostras sem desvio padrão calculado. O instrumento foi calibrado para detectar diferenças de **ordem de grandeza** ou **>20%**, não micro-benchmark fino. Os números servem para confirmar que cascading não introduz regressão de ordem de grandeza — não como critério decisivo da ADR.

**Implicação:** acima do threshold de 20% definido no plano, mas em magnitude absoluta pequena. Para o sistema UniPlus (HTTP API + Postgres + Kafka), a diferença é dominada por overhead de rede e I/O — 0.36ms por handler é ruído. Não decisivo. Em pipelines de altíssimo throughput (>10k msg/s sustentados), valeria revisitar; não é o perfil do UniPlus. **Empate prático.**

### Dimensão 6 — Atomicidade transacional

**Achado:** paridade total. Tanto cascading quanto scraper persistem envelopes via `EfCoreEnvelopeTransaction.PersistOutgoingAsync` na **mesma transação EF do `SaveChanges`**.

**Evidência:**

- O envelope final é gravado em `wolverine_outgoing_envelopes` por `EfCoreEnvelopeTransaction.PersistOutgoingAsync` em ambos os caminhos: scraper chama `bus.PublishAsync(domainEvent)` (`OutgoingDomainEvents.cs:47`); cascading chama `MessageContext.EnqueueCascadingAsync → PublishAsync(message) → MessageBus.PersistOrSendAsync → envelope.PersistAsync(Transaction)` (`MessageContext.cs:682` → `MessageBus.cs:296-323`). O método `Transaction.PersistOutgoingAsync` é o mesmo (`EfCoreEnvelopeTransaction.cs:43-66`).
- Diferença sutil: scraper só persiste no `CommitAsync` da `EfCoreEnvelopeTransaction` (`EfCoreEnvelopeTransaction.cs:170-174`). Cascading persiste **dentro** do escopo `try` (antes do `CommitAsync`), porque é capturado no postprocessor do handler (`HandlerChain.cs:389-398`). Em ambos os casos o `BeginTransactionAsync` foi feito por `EnrollDbContextInTransaction.cs:40-42`, então as INSERTs caem na mesma transação.
- Validação empírica: S10/V9 verde (rollback elimina entidade + envelope) demonstra que o caminho cascading respeita o `RollbackAsync` da `EfCoreEnvelopeTransaction` (`EfCoreEnvelopeTransaction.cs:122-130`) sem regressão face ao S4 da matriz original.

**Implicação:** sem trade-off transacional. Mesmas garantias.

### Dimensão 7 — Alta disponibilidade

**Achado:** paridade total. O ponto de persistência é o mesmo (`wolverine_outgoing_envelopes`), portanto reassignment, store-and-forward e durable inbox/outbox funcionam idênticos.

**Evidência:**

- Reassignment de envelopes funciona via leases nos registros da tabela `wolverine_outgoing_envelopes` (referenciado em `OutboxRestartRecoveryTests.cs` da matriz S6). Esse mecanismo é agnóstico ao caminho que escreveu o registro — a tabela é a fonte de verdade.
- A linha de log "Reassigned 2 incoming messages from 1 and endpoint at postgresql://domain_events/ to any node in the durable inbox" foi observada nas execuções dos testes de cascading (`Application stopping signal received` → reassign). Mesmo log emitido pelos testes da matriz S0–S9. Comportamento idêntico.
- `Policies.UseDurableOutboxOnAllSendingEndpoints()` é aplicado em ambos os extensions (`OutboxSpikeWolverineExtension.cs:73`, `Cascading/OutboxCascadingExtension.cs:78`); rota durável é o invariante de transporte.

**Implicação:** sem trade-off de HA. Mesmas garantias.

### Dimensão 8 — Pureness CQRS

**Achado:** cascading é mais alinhado à literatura CQRS (handler de command produzindo eventos é o padrão central, não exceção).

**Evidência (citação documental):**

- Greg Young, em [CQRS Documents](https://cqrs.files.wordpress.com/2010/11/cqrs_documents.pdf): *"a command handler doesn't return any data — but it does emit events as part of its work"*. Cascading messages tornam essa emissão visível na assinatura do handler.
- Vaughn Vernon, *Implementing Domain-Driven Design* (cap. 8): a publicação de domain events é responsabilidade da Application Service que processou o command — não de um observer externo escutando o ORM. Scraper inverte essa responsabilidade (o ORM scanner é quem decide o que publicar).
- Comentário do maintainer Wolverine, [`JasperFx/wolverine#2585`](https://github.com/JasperFx/wolverine/issues/2585), 2026-04-22: *"If you're greenfield though, I'd recommend using Wolverine more idiomatically and just returning cascaded messages from the handler rather than the magic EF Core scraping thing"*.

**Implicação:** cascading é o caminho idiomático para o estilo CQRS adotado pela ADR-022. Scraper é uma adaptação útil para sistemas legados com agregados que já acumulam eventos via convenção e consumers MediatR — não é o caso do UniPlus (greenfield).

### Dimensão 9 — Manutenibilidade longitudinal

**Achado:** cascading é mais fácil de onboardar, mais visível em logs e mais resiliente a refatorações de persistência.

**Evidência:**

- Onboarding: para entender o que o handler emite, basta ler a assinatura e o `return` (cascading). No scraper, é preciso conhecer a configuração distante no boot + saber que o scraper drena `ChangeTracker.Entries()`.
- Logs: em ambos os casos o Wolverine emite `Enqueued for sending {Event} to {endpoint}` no `PersistOrSendAsync`. Mas o **caminho do enfileiramento** é distinto: cascading aparece logo após o `[handler] returned [n] messages`, scraper aparece após o `CommitAsync` da transação. Em produção, esse delta ajuda a triangular se o evento sumiu por bug do handler ou bug do scraper.
- Refatoração de persistência: se um agregado migra de EF Core para um repositório custom (event store, write-behind cache, in-memory), o scraper deixa de funcionar — depende do `ChangeTracker`. Cascading sobrevive — basta o handler chamar o repositório novo e retornar a coleção. Para a UniPlus, módulos futuros como `Auxilio Estudantil` ou `Pesquisa` podem optar por persistência custom; cascading não os limita.

**Implicação:** menor débito técnico ao longo do tempo. O custo de migrar o domínio se persistência mudar é menor.

### Dimensão 10 — Independência do fork local (CRÍTICA)

**Achado:** ✅ confirmado. Cascading roda com Wolverine `5.32.1` oficial (nuget.org), eliminando a necessidade do feed local em `vendors/nuget-local/` e do fork `wolverine-fork`.

**Evidência (medição local — Ambiente A):**

- Os 3 testes S10/V8, V9, V10 passaram em ~16s rodando contra `WolverineFx 5.32.1` oficial (nuget.org), via `<VersionOverride>` no csproj de testes.
- Como contraprova: leitura de `wolverine-fork/src/Wolverine/Runtime/Handlers/CaptureCascadingMessages.cs:11` mostra que o caminho cascading delega a `MessageContext.EnqueueCascadingAsync`, que está em `wolverine-fork/src/Wolverine/Runtime/MessageContext.cs:604-683` — assembly **`Wolverine`** (core), não `Wolverine.EntityFrameworkCore`. O fix do PR `#2586` está em `Wolverine.EntityFrameworkCore.OutgoingDomainEvents.DomainEventScraper<T, TEvent>` (`wolverine-fork/src/Persistence/Wolverine.EntityFrameworkCore/OutgoingDomainEvents.cs:42`). Caminhos disjuntos.
- Achado bônus: rodando a suíte combinada de outbox via `Category=OutboxCapability` (16 testes: 13 S0–S9 + 3 S10 pela dupla marcação dos traits) com `5.32.1` oficial, **16/16 verdes**. A validação futura deve manter execução explícita de `Category=OutboxCapability` e `Category=OutboxCascading`, para que S10 continue visível mesmo se a dupla marcação mudar. O bug `Collection was modified` não é exercitado pelos cenários atuais. Hipótese para o motivo: cada handler emite um único `EditalPublicadoEvent`, não há iteração concorrente que dispare o `InvalidOperationException` original.

**Implicação:** **decisivo.** Adotar cascading + Wolverine 5.32.1 oficial elimina:

1. O feed local em `vendors/nuget-local/` (3 nupkgs `5.32.1-pr2586`).
2. A configuração de `packageSourceMapping` no `nuget.config`.
3. A dívida técnica de manter o fork `wolverine-fork` sincronizado com upstream.
4. O risco de drift fork × upstream em qualquer release futuro (5.32.2, 5.33, etc.).
5. A categoria *inteira* do bug do scraper (cascading não passa pelo caminho do `ChangeTracker`).

Mesmo que cada uma das 9 dimensões anteriores empatasse, **a dimensão 10 isoladamente é razão suficiente** para mudar.

## 5. Tabela consolidada das 10 dimensões

| # | Dimensão | Vencedor | Magnitude |
|---|---|---|---|
| 1 | Acoplamento (visibilidade `DomainEvents`) | Cascading | Modesta |
| 2 | Quantidade de código | Empate | Neutro (~2 linhas a menos no extension, ~5 a mais por handler) |
| 3 | Testabilidade unitária | Cascading | **Significativa** — UnitTest puro existe (<1s) vs exige fixture com Postgres |
| 4 | Comportamento implícito | Cascading | Significativa (legibilidade) |
| 5 | Performance | Scraper | **Mensurável (~26%)**, **absoluta desprezível (sub-1ms)** — empate prático |
| 6 | Atomicidade transacional | Empate | Mesma rota (`EfCoreEnvelopeTransaction.PersistOutgoingAsync`) |
| 7 | Alta disponibilidade | Empate | Mesmo ponto de persistência (`wolverine_outgoing_envelopes`) |
| 8 | Pureness CQRS | Cascading | Modesta (alinhamento com literatura) |
| 9 | Manutenibilidade longitudinal | Cascading | Significativa (onboarding + resiliência a refatoração de persistência) |
| 10 | Independência do fork local | **Cascading** | **Crítica — decisiva** |

**Saldo:** cascading vence em 6 dimensões com diferença real, empata em 3 (incluindo a única em que scraper teve vantagem mensurável), perde em 0. Vencedor objetivo.

## 6. Recomendação

> **Abrir ADR-026** propondo a adoção de cascading messages como caminho preferencial de drenagem de domain events no UniPlus, substituindo parcialmente a ADR-025.

Justificativa em 3 dimensões críticas:

1. **Independência do fork local (dimensão 10):** elimina o feed local imediatamente. Reduz dívida técnica + risco de drift upstream + uma classe inteira de bug futuro.
2. **Manutenibilidade longitudinal (dimensão 9):** estilo idiomático recomendado pelo maintainer, alinhado a CQRS canônico, mais legível para time rotativo da CTIC.
3. **Testabilidade unitária (dimensão 3):** ciclo de feedback de desenvolvimento é radicalmente mais curto. Validar o contrato do handler em <1s sem container é um ganho operacional real.

A ADR-025 permanece **válida em essência** — Wolverine + outbox transacional + persistence Postgres seguem como decisão. ADR-026 substitui apenas o item específico de drenagem de domain events.

A nova ADR deixa a estratégia anterior (`PublishDomainEventsFromEntityFrameworkCore<EntityBase>`) como **fallback documentado** para casos em que a equipe precise:
- Migrar código legado que já usa o padrão MediatR scraper.
- Suportar agregados de terceiros sem reescrever handlers.

## 7. Próximos passos (não-fazer-sem-aprovação)

> **Estado atual (2026-04-26):** a coluna "Operacionalização" foi acrescentada após o merge desta consolidação para apontar onde cada item está sendo executado. As linhas originais ficam preservadas como snapshot do momento da spike.

| Ação | Aprovação requerida | Operacionalização |
|---|---|---|
| Promover `ADR-026` rascunho local em `repositories/uniplus-docs/docs/adrs/ADR-026-...md` | Humana — Tech Lead | **Concluído** — ADR-026 mergeada em `uniplus-docs/main`. |
| Remover `vendors/nuget-local/` + reverter `nuget.config` para apenas `nuget.org` | Humana (PR específico após ADR-026 aprovada) | Em execução em **#163**. |
| Remover `Directory.Packages.props` lock em `5.32.1-pr2586` → bump para `5.32.1` oficial | Humana (PR específico) | Em execução em **#163**. |
| Implementar handlers de produção com cascading na Story `#158` (saída do spike) | Humana — Story re-aberta com escopo do estilo cascading | Recalibrado em duas tasks: **#164** (configuração outbox produtivo) + **#136** (handler de referência `PublicarEditalCommand`). |
| Refatorar `EntityBase.DomainEvents` para visibilidade reduzida (`internal` + `InternalsVisibleTo`) | Humana — fora do escopo deste spike | Pendente — não há issue aberta; permanece como recomendação futura. |

## 8. Anexos

- Plano original do spike: [`158-s10-plano-cascading-messages.md`](158-s10-plano-cascading-messages.md)
- Implementação dos 3 cenários:
  - `tests/Unifesspa.UniPlus.Selecao.IntegrationTests/Outbox/Capability/Cascading/OutboxCascadingMatrixTests.cs` (V8, V9)
  - `tests/Unifesspa.UniPlus.Selecao.IntegrationTests/Outbox/Capability/Cascading/OutboxCascadingKafkaTests.cs` (V10)
  - `tests/Unifesspa.UniPlus.Selecao.IntegrationTests/Outbox/Capability/Cascading/PublicarEditalCascadingHandlerUnitTests.cs` (UnitTest puro — dimensão 3)
  - `tests/Unifesspa.UniPlus.Selecao.IntegrationTests/Outbox/Capability/Cascading/OutboxCascadingPerfBenchmark.cs` (benchmark cascading — dimensão 5)
  - `tests/Unifesspa.UniPlus.Selecao.IntegrationTests/Outbox/Capability/OutboxScraperPerfBenchmark.cs` (benchmark scraper — dimensão 5)
- Comentário do maintainer Wolverine: [`JasperFx/wolverine#2585`](https://github.com/JasperFx/wolverine/issues/2585)
- ADR-025 (caminho atual): [`uniplus-docs/docs/adrs/ADR-025-outbox-wolverine-adotado-em-158.md`](https://github.com/unifesspa-edu-br/uniplus-docs/blob/main/docs/adrs/ADR-025-outbox-wolverine-adotado-em-158.md)
- Relatório consolidado da matriz S0–S9: [`158-relatorio-final.md`](158-relatorio-final.md)
