---
status: "accepted"
date: "2026-04-28"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0004: Outbox transacional via Wolverine + EF Core sobre PostgreSQL

## Contexto e enunciado do problema

O `uniplus-api` precisa garantir atomicidade entre a persistência de uma alteração no agregado e a publicação de seus domain events para o bus interno (Wolverine PostgreSQL transport) e para o bus externo (Kafka). Sem outbox, há risco de salvar a entidade sem despachar o evento — gap silencioso que rompe a coerência entre módulos.

A primeira tentativa de adoção (uniplus-api#135) reprovou em spike por bug do `DomainEventScraper` na versão 5.32.1 do Wolverine — `Collection was modified during ScrapeEvents`. A reprovação está documentada em registro histórico interno.

A retomada na uniplus-api#158 executou matriz de spikes S0–S9 com fix do scraper carregado via fork local (`5.32.1-pr2586`) sobre o PR upstream `JasperFx/wolverine#2586`. Resultado: 13 testes verdes, AC1a/AC1b/AC2/AC3/AC5 comprovados; AC4 fechado pelo versionamento de schema via migrations.

A combinação de drenagem de domain events em si é decisão separada — ver ADR-0005, que adota cascading messages como caminho idiomático recomendado pelo maintainer Wolverine, removendo a dependência do fork local.

## Drivers da decisão

- Atomicidade transacional cross-boundary é não-negociável.
- Persistência e transport co-residem no PostgreSQL primário (sem broker dedicado para mensagens internas).
- Auditabilidade de processo seletivo — envelopes ficam rastreáveis em SQL.
- Princípio de menor privilégio para a role de banco em produção.

## Opções consideradas

- Outbox via integração Wolverine + EF Core (Persist + Transport PostgreSQL)
- Outbox via Hangfire job consultando tabela própria
- Publicação direta no Kafka pelo handler (sem outbox)

## Resultado da decisão

**Escolhida:** outbox transacional usando a integração nativa Wolverine + EF Core, com persistence e transport ambos no PostgreSQL primário.

Configuração canônica em `Program.cs` por módulo:

```csharp
builder.Host.UseWolverine(opts =>
{
    opts.UseEntityFrameworkCoreTransactions();
    opts.Policies.AutoApplyTransactions();

    opts
        .PersistMessagesWithPostgresql(connectionString, schemaName: "wolverine")
        .EnableMessageTransport(_ => { });

    opts.Policies.UseDurableOutboxOnAllSendingEndpoints();

    // Roteamento por domínio fica no extension de cada módulo.
});
```

Decisões contratuais derivadas:

1. **`Policies.UseDurableOutboxOnAllSendingEndpoints()` é obrigatório.** Sem isso, publicação Kafka cai em buffer in-memory do producer Confluent, perdendo durabilidade se o broker estiver indisponível ou o processo cair antes do flush.
2. **`EnableRetryOnFailure` no `DbContext` é proibido em DbContexts usados por handlers Wolverine.** `NpgsqlRetryingExecutionStrategy` é incompatível com `Policies.AutoApplyTransactions`. Retry é centralizado em `opts.OnException<TException>().RetryTimes(N)`.
3. **Schema versionado por migration EF.** `MapWolverineEnvelopeStorage(modelBuilder, "wolverine")` no `OnModelCreating` de cada `DbContext`. `AutoBuildMessageStorageOnStartup = AutoCreate.None` em produção.
4. **Tabelas de queue PG (`wolverine_queues.wolverine_queue_<nome>` e `_scheduled`) são provisionadas previamente** por SQL versionado, migration manual ou time de plataforma. Em produção, a role de banco do nó Wolverine **não tem permissão DDL**.
5. **AutoProvision Kafka apenas em dev/test.** Produção: provisão de tópicos sob governança da plataforma (ver ADR-0014).
6. **Convenção snake_case** para queues PG e tópicos Kafka — Wolverine normaliza `-` para `_` em SQL; padronizar evita surpresas em clientes admin.
7. **Retenção de dead letters** — `DurabilitySettings.DeadLetterQueueExpirationEnabled = true`, expiração padrão 30 dias. Default Wolverine (10 dias) não é adotado por ser inferior ao ciclo de triagem de processo seletivo.

## Consequências

### Positivas

- Atomicidade `SaveChanges + envelope storage` garantida pela transação Postgres — agregado e mensagem sobrevivem ou perecem juntos.
- Sem broker adicional para mensagens internas — PG transport reusa o banco primário.
- Auditoria SQL — envelopes inspecionáveis com queries diretas.
- Política de retenção alinhada ao ciclo de processo seletivo.

### Negativas

- Schema `wolverine` adicional no banco primário (8 tabelas + tabelas de queue).
- Operadores precisam provisionar `wolverine_queues` antes do deploy produtivo.
- Retry de EF concentrado em policies Wolverine — onboarding precisa cobrir essa restrição.

### Riscos

- **Crash não-gracioso (kill -9, OOM).** Mitigado pelo invariante transacional do Postgres — envelope storage e agregado vivem na mesma transação. Spike de hardening contra crash não-gracioso fica documentado como melhoria futura.
- **Drift entre migration e runtime.** Mitigado pela combinação `AutoCreate.None` em produção + role sem permissão DDL.
- **Versão do framework.** Decisão de drenagem em ADR-0005 elimina a dependência do fork local; atualizações futuras seguem política de drift documentada por ADR.

## Confirmação

- Suíte `Category=OutboxCapability` em `uniplus-api` cobre a matriz S0–S9 com 13 cenários verdes.
- Validador de startup falha o boot se `EnableRetryOnFailure` estiver ativo em DbContext usado por handler Wolverine.
- Validador de startup falha o boot se houver endpoint Kafka sem outbox durável marcado.
- Logs de boot via `LoggerMessage` listam queues, topics, mode (durable/buffered) e schema de persistência.

## Mais informações

- ADR-0003 define Wolverine como backbone CQRS.
- ADR-0005 define cascading messages como caminho de drenagem (substitui `PublishDomainEventsFromEntityFrameworkCore`).
- ADR-0014 define Kafka como bus inter-módulo.
- ADR-0007 define PostgreSQL como banco primário (provedor da persistência outbox).
- ADR-0018 define OpenTelemetry como observabilidade obrigatória — métricas e alertas Wolverine ficam ali.
- [Wolverine — Durable Outbox](https://wolverinefx.net/guide/durability/)
- [JasperFx/wolverine#2586 — fix do DomainEventScraper](https://github.com/JasperFx/wolverine/pull/2586)
- **Origem:** revisão da ADR interna Uni+ ADR-025 (não publicada). A ADR interna ADR-024, que documentou a primeira tentativa reprovada, permanece como histórico interno e não é canonizada aqui — só o estado atual.
