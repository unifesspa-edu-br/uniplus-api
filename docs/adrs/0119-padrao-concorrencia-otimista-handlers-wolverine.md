---
status: "accepted"
date: "2026-08-03"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed: []
---

# ADR-0119: Padrão de conflito de concorrência otimista em handlers Wolverine — propagar sem catch quando o endpoint não é idempotency-protected

## Contexto e enunciado do problema

`UseEntityFrameworkCoreTransactions()` + `AutoApplyTransactions()` (ADR-0004) faz o
Wolverine chamar `SaveChangesAsync()` no `DbContext` do escopo DEPOIS que o handler
retorna, para persistir os envelopes do outbox — independente de o handler ter
devolvido sucesso ou falha. O PR #1019 (issue #1018, Termo de Consentimento)
encontrou um bug real: um handler que captura `DbUpdateConcurrencyException` (xmin)
do próprio `SaveChangesAsync` e devolve `Result.Failure` sem `ChangeTracker.Clear()`
deixa as entidades `Added`/`Modified` da tentativa fracassada ainda rastreadas; a
chamada automática do Wolverine tenta gravá-las de novo, a mesma exceção estoura de
novo — fora de qualquer `catch` do handler, vazando como 500 em vez do 409 que o
handler já tinha traduzido.

O fix aplicado no PR #1019 (`IConfiguracaoUnitOfWork.DescartarAlteracoesNaoSalvas()`
→ `ChangeTracker.Clear()`, chamado no catch) resolve o problema, mas exige repetir o
padrão em cada handler que trata concorrência — conhecimento tribal, sujeito a ser
esquecido (como de fato ocorreu nos handlers de `CalendarioDiasUteis`, escritos antes
desse achado — issue #1027).

## Drivers da decisão

- Não repetir `try/catch + ChangeTracker.Clear()` por handler quando existe
  alternativa mais simples e menos sujeita a esquecimento.
- Investigação empírica (issue #1027) via `IMessageBus.InvokeAsync` real contra
  Postgres, com contagem de `SaveChangesAsync` por instância de `DbContext` (não por
  interleaving de log): quando o handler **não** captura `DbUpdateConcurrencyException`
  e deixa propagar, o `SaveChangesAsync` automático do outbox **nunca é tentado** — o
  lado que lança a exceção contribui com exatamente 1 chamada de `SaveChangesAsync`
  (a própria, que lança); o lado que completa normalmente contribui com 2 chamadas
  (a própria mais o flush automático do outbox). Reproduzido de forma consistente em
  execuções repetidas.
- Essa investigação NÃO encontrou orientação oficial do time Wolverine para este
  cenário específico — nem a doc de transactional middleware, nem os exemplos de
  source, nem a issue `JasperFx/wolverine#1735` (a mais próxima pelo título) cobrem
  "handler captura exceção do próprio `SaveChangesAsync` e devolve `Result`". A
  convenção `OnException`/`OnExceptionAsync` do Wolverine existe, mas é documentada
  para endpoints Wolverine.Http — não para message handlers atrás de
  `ICommandBus`/`IQueryBus` (ADR-0003), que é o padrão deste projeto. A decisão desta
  ADR se apoia em evidência empírica reproduzida neste codebase, não em confirmação
  oficial do framework.
- **Achado que restringe o escopo da decisão:** `IdempotencyFilter<TDbContext>`
  (`Infrastructure.Core/Idempotency/IdempotencyFilter.cs`) não verifica
  `ResourceExecutedContext.Exception`/`ExceptionHandled` — quando o `next()` do
  resource filter retorna com uma exceção pendente (não lançada sincronamente, apenas
  registrada no context pelo `ResourceInvoker` do ASP.NET Core MVC), o filtro segue o
  caminho de sucesso: lê `httpContext.Response.StatusCode` (ainda no valor default,
  já que nenhum `IActionResult` foi produzido), não bate em nenhuma das condições de
  "não cachear" (`>= 500`, precondição, `Canceled`), e chama `_store.CompleteAsync`
  cacheando uma entrada **200 com corpo vazio** sob a Idempotency-Key — só depois disso
  o `ResourceInvoker` relança a exceção, que aí sim chega ao `GlobalExceptionMiddleware`.
  Um replay subsequente da mesma chave devolveria a entrada cacheada incorreta em vez
  de refletir o 409 que o cliente originalmente recebeu (ou de reexecutar). Isso é
  debito PRÉ-EXISTENTE do `IdempotencyFilter`, não introduzido por esta ADR — mas
  significa que "propagar sem capturar" só é seguro para endpoints **sem**
  `[RequiresIdempotencyKey]`, até que esse gap seja corrigido (rastreado em issue
  separada).

## Opções consideradas

- **A. Formalizar `try/catch + ChangeTracker.Clear()` como padrão obrigatório em todo handler.** Repetitivo, sujeito a esquecimento (é exatamente o que já aconteceu).
- **B. Deixar `DbUpdateConcurrencyException` propagar sem catch local; mapear centralmente uma única vez no `GlobalExceptionMiddleware` → 409.** Elimina a repetição e a necessidade de `ChangeTracker.Clear()`, mas só é seguro no caminho síncrono HTTP request/reply, e é inseguro para endpoints idempotency-protected enquanto o `IdempotencyFilter` não checar `ResourceExecutedContext.Exception`.
- **C. Híbrido — a escolha desta ADR.** Ver "Resultado da decisão".

## Resultado da decisão

**Escolhida:** "C — híbrido, condicionado a `[RequiresIdempotencyKey]`":

- **Endpoint SEM `[RequiresIdempotencyKey]`** (ex.: `DELETE`, naturalmente idempotente por semântica HTTP, como os dois handlers `Remover` migrados nesta ADR): o handler **não captura** `DbUpdateConcurrencyException` — deixa propagar. O `GlobalExceptionMiddleware` (`Infrastructure.Core/Middleware/GlobalExceptionMiddleware.cs`) mapeia centralmente para `409 Conflict`, `code=uniplus.concorrencia.conflito`, um único branch para todo o monólito modular.
- **Endpoint COM `[RequiresIdempotencyKey]`**: o handler **continua capturando** `DbUpdateConcurrencyException` localmente e chamando `unitOfWork.DescartarAlteracoesNaoSalvas()` (`ChangeTracker.Clear()`) antes de devolver `Result.Failure` — o padrão do PR #1019, mantido explicitamente enquanto o gap do `IdempotencyFilter` não for corrigido.
- **Escopo restrito ao caminho síncrono HTTP request/reply.** Um handler Wolverine invocado por consumidor durável/background (sem `HttpContext`, fora do `GlobalExceptionMiddleware`) segue as políticas de retry/dead-letter do próprio Wolverine quando lança — comportamento correto para esse contexto, não coberto por esta ADR.
- **Fora do escopo desta ADR:** a exclusion constraint `DEFERRABLE INITIALLY DEFERRED` de `MarcarVigenteCalendarioDiasUteisCommandHandler` (`ex_calendario_dias_uteis_vigente_unico`). Diferente de `DbUpdateConcurrencyException`, essa violação chega como `Npgsql.PostgresException` bruta no `COMMIT` externo do outbox — mecanismo distinto (transação Postgres abortada, não apenas `ChangeTracker` desatualizado) que não foi empiricamente verificado nesta investigação. Esse handler mantém o catch local sem `ChangeTracker.Clear()` (débito pré-existente, não deste PR) até investigação própria — rastreado em `uniplus-api#1032`.

Handlers migrados nesta ADR: `RemoverTermoConsentimentoCommandHandler` e
`RemoverCalendarioDiasUteisCommandHandler` — ambos atrás de endpoints `DELETE` sem
`[RequiresIdempotencyKey]`. `PromoverVersaoTermoConsentimentoCommandHandler`,
`EditarRascunhoTermoConsentimentoCommandHandler`,
`MarcarRevisadoTermoConsentimentoCommandHandler` e
`MarcarVigenteCalendarioDiasUteisCommandHandler` permanecem no padrão catch +
`ChangeTracker.Clear()` — todos atrás de `[RequiresIdempotencyKey]`.

## Consequências

### Positivas

- Dois handlers deixam de precisar do padrão catch + `ChangeTracker.Clear()`; novos handlers `DELETE`/não-idempotentes com concorrência otimista não precisam repeti-lo — o `GlobalExceptionMiddleware` cobre por padrão.
- Mapeamento de erro para conflito de concorrência centralizado num único lugar para todo o monólito modular, não replicado por módulo.
- A decisão é rastreável a uma investigação empírica reproduzível, não a suposição.

### Negativas

- **Breaking de contrato para quem já leu o corpo 409 dos dois endpoints migrados**: `type`, `title`, `code` e `detail` mudam de valores por-módulo (`uniplus.configuracao.termo_consentimento.conflito_de_concorrencia`,
  `uniplus.configuracao.calendario_dias_uteis.conflito_de_concorrencia`) para o valor
  genérico `uniplus.concorrencia.conflito`. Mitigado por não haver consumidor externo
  ainda integrado nesta data (issue #1018 mergeada na véspera desta ADR, telas do
  frontend ainda não construídas — issues uniplus-web#498/#499).
- Dois padrões coexistem no mesmo módulo (propagar vs. catch local), diferenciados por
  `[RequiresIdempotencyKey]` — exige que quem escreve um novo handler saiba qual dos
  dois se aplica; documentado no `CLAUDE.md`.
- O gap do `IdempotencyFilter` (não checar `ResourceExecutedContext.Exception`) é mais
  amplo que esta ADR — afeta qualquer exceção não capturada em qualquer endpoint
  idempotency-protected do monólito, não só concorrência. Rastreado em issue separada.

### Neutras

- `MarcarVigenteCalendarioDiasUteisCommandHandler` continua com dois catches
  distintos (exclusion constraint mantido; `DbUpdateConcurrencyException` também
  mantido, já que o endpoint é idempotency-protected) — não há simplificação de código
  nesse handler específico nesta ADR.

## Confirmação

A prova é composta por duas camadas independentes, não por um único teste HTTP de
ponta a ponta (corrida via HTTP não reproduz a janela real de forma confiável neste
código — ver nota nos arquivos de teste):

- `GlobalExceptionMiddlewareTests` prova a camada de mapeamento: `DbUpdateConcurrencyException`
  sintética → 409 (status, content-type, `code`, `detail`, `instance`, `traceId`).
- `RemoverTermoConsentimentoCommandHandlerTests`/`RemoverCalendarioDiasUteisCommandHandlerTests`
  provam que o handler propaga `DbUpdateConcurrencyException` em vez de capturar (mock).
- `TermoConsentimentoConcorrenciaTests`/`CalendarioDiasUteisConcorrenciaTests` provam a
  mecânica real contra Postgres: uma escrita concorrente forçada de forma determinística
  (lock de linha via transação segurada, não `Task.WhenAll` sem sincronização) faz o
  handler propagar `DbUpdateConcurrencyException` limpa, sem o outbox tentar salvar de
  novo — via `IMessageBus`, não HTTP, então não exercitam o `GlobalExceptionMiddleware`
  em si (isso é responsabilidade exclusiva de `GlobalExceptionMiddlewareTests`).

## Prós e contras das opções

### A — catch + Clear() em todo handler

- Bom, porque não depende de nenhuma condição externa (idempotência, tipo de endpoint).
- Ruim, porque é o padrão que já falhou por esquecimento uma vez (issue #1027 nasceu
  exatamente disso).

### B — propagar sempre, sem condicionar a idempotência

- Bom, porque é o mais simples de enunciar.
- Ruim, porque corrompe o cache de idempotência (200 vazio) para qualquer endpoint
  `[RequiresIdempotencyKey]` até o `IdempotencyFilter` ser corrigido — regressão real,
  não hipotética.

### C — híbrido condicionado a idempotência (escolhida)

- Bom, porque não introduz a regressão de B nem mantém 100% do débito de A.
- Ruim, porque exige que quem escreve o handler saiba qual padrão usar — mitigado por
  documentação no `CLAUDE.md`.

## Mais informações

- [ADR-0004](0004-outbox-transacional-via-wolverine.md) — outbox transacional via Wolverine, `AutoApplyTransactions`.
- [ADR-0024](0024-mapeamento-domain-error-http.md) — mapeamento `DomainError` → HTTP, mesmo espírito de centralização aplicado aqui a uma exceção de infraestrutura.
- [ADR-0027](0027-idempotency-key-store-postgresql.md) — Idempotency-Key; o gap do `IdempotencyFilter` descrito nesta ADR é um risco para a garantia de cache verbatim de 4xx que a ADR-0027 estabelece.
- **Emenda à ADR-0046** (validação sem exceção): a ADR-0046 cita `DbUpdateException` como exemplo de falha inesperada → 500. Esta ADR refina isso: `DbUpdateConcurrencyException` (subtipo específico, ligado ao token de concorrência otimista xmin) é tratado como conflito esperado → 409 quando propagado por um handler sem `[RequiresIdempotencyKey]`. Não é contradição — é uma decisão posterior e mais específica sobre um subtipo que a ADR-0046 não distinguia.
- Wolverine — documentação consultada e resultado da pesquisa: [Transactional Middleware](https://wolverinefx.io/guide/durability/efcore/transactional-middleware.html) (não cobre o cenário), [Error Handling](https://wolverinefx.net/guide/handlers/error-handling.html) (cobre retry/DLQ pós-falha, não interceptação pré-outbox), `JasperFx/wolverine#1735` (assunto relacionado mas distinto — `ExecuteUpdate`/`ExecuteDelete` bypassando o outbox).
