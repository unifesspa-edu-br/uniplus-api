---
status: "accepted"
date: "2026-05-05"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0044: Roteamento produtivo de domain events — queue PostgreSQL interna + tópico Kafka opcional

## Contexto e enunciado do problema

`EditalPublicadoEvent` é drenado do agregado via cascading messages (ADR-0041) e cai no outbox. A pergunta seguinte: para onde o Wolverine entrega esses eventos?

Possibilidades:
- **Listener interno PG queue** (`ToPostgresqlQueue`) — entrega in-process via mesma instância Wolverine; baixíssimo overhead; ideal para subscribers do mesmo módulo (auditoria, logging, projection).
- **Tópico Kafka** (`ToKafkaTopic`) — entrega cross-process, durável, persistente; necessário para integração cross-módulo ou consumidores externos.
- **Sem rota** — evento fica no outbox sem subscriber, criando lixo.

A pergunta: qual a estratégia padrão?

## Drivers da decisão

- **Subscribers intra-módulo existem hoje** (`EditalPublicadoEventHandler` de logging em `Selecao.Application`). Precisam de PG queue.
- **Cross-módulo ainda não existe** (Ingresso não consome `EditalPublicadoEvent`), mas existirá quando "chamada de vagas" for implementada. Precisa de Kafka.
- **Ambiente sem Kafka**: dev local sem broker; CI sem Kafka container; HML pode rodar sem Kafka enquanto Ingresso não consumir. Forçar Kafka faz Wolverine entrar em retry indefinido.
- **YAGNI**: eventos sem subscriber real não devem ser roteados para Kafka apenas por simetria.

## Opções consideradas

- **A. Sempre rotear para PG queue + Kafka.**
- **B. PG queue sempre; Kafka opcional condicionado a `Kafka:BootstrapServers` configurado.**
- **C. Sem roteamento default — cada evento decide caso a caso.**

## Resultado da decisão

**Escolhida:** "B — PG queue sempre, Kafka opcional condicional".

`Selecao.API/Program.cs` configura no callback de `UseWolverineOutboxCascading`:

```csharp
opts.PublishMessage<EditalPublicadoEvent>().ToPostgresqlQueue("domain-events");
opts.ListenToPostgresqlQueue("domain-events");

if (!string.IsNullOrWhiteSpace(builder.Configuration["Kafka:BootstrapServers"]))
{
    opts.PublishMessage<EditalPublicadoEvent>().ToKafkaTopic("edital_events");
}
```

PG queue é unconditional: o subscriber intra-módulo precisa do evento para auditoria/logging em todo ambiente. Kafka é condicional a configuração — quando `Kafka:BootstrapServers` está vazio (dev local sem broker, test fixture, deploy HML pré-Ingresso), Wolverine não tenta abrir transport Kafka.

`Helper UseWolverineOutboxCascading` faz o `opts.UseKafka(...).AutoProvision()` quando `Kafka:BootstrapServers` está populado — auto-provision dos tópicos é OK em ambiente de produção controlado.

## Consequências

### Positivas

- Subscribers intra-módulo funcionam em todos os ambientes (incluindo CI).
- Kafka pode ser ligado/desligado por configuração — switch operacional.
- Sem retry indefinido contra broker inexistente.
- Quando Ingresso consumir `EditalPublicadoEvent`, basta apontar `Kafka:BootstrapServers` e o publishing entra automaticamente.

### Negativas

- Eventos roteados para PG queue + Kafka simultaneamente são entregues duplamente para subscribers que escutam em ambos os transports. Hoje esse caso não existe (subscriber intra escuta PG only; subscriber cross-módulo escutará Kafka only). Documentar quando o cenário materializar.
- Configuração Kafka é via env var/appsettings — não há flag explícita "use kafka yes/no". Mitigado por convenção: deixar `Kafka:BootstrapServers` vazio = desliga.

### Neutras

- A decisão pode ser revisitada se o Wolverine ganhar transports adicionais (RabbitMQ, Service Bus). Hoje só PG e Kafka são considerados.

## Confirmação

- `Selecao.API/Program.cs` configura ambos os routes condicionais.
- `WolverineOutboxConfiguration.UseWolverineOutboxCascading` faz `UseKafka` apenas quando `Kafka:BootstrapServers` é não-whitespace.
- `CascadingFixture` força `Kafka__BootstrapServers` em whitespace (issue #197 sentinela).
- `OutboxCascadingMatrixTests` (V8/V9) cobre cenários sem Kafka; `OutboxCascadingKafkaTests` (V10) cobre com Kafka.

## Prós e contras das opções

### A — Sempre PG + Kafka

- Bom: simetria.
- Ruim: dev local + CI sem Kafka entram em retry; force-kafka quebra ambientes pré-Ingresso.

### B — PG sempre + Kafka condicional (escolhida)

- Bom: subscribers intra funcionam sempre; Kafka é switch operacional.
- Ruim: convenção de "vazio desliga" exige documentação.

### C — Sem default

- Bom: explicitude máxima.
- Ruim: cada evento precisa de roteamento manual; eventos esquecidos viram lixo no outbox.

## Mais informações

- [JasperFx Wolverine — PostgreSQL Transport](https://wolverinefx.io/guide/messaging/transports/postgresql.html)
- [JasperFx Wolverine — Kafka Transport](https://wolverinefx.io/guide/messaging/transports/kafka.html)
- ADR-0026 — Outbox transacional via Wolverine
- ADR-0040 — Helper `UseWolverineOutboxCascading`
- ADR-0041 — Padrão de retorno cascading
- Origem: PR [#172](https://github.com/unifesspa-edu-br/uniplus-api/pull/172) e [#173](https://github.com/unifesspa-edu-br/uniplus-api/pull/173); issue [#185](https://github.com/unifesspa-edu-br/uniplus-api/issues/185)
