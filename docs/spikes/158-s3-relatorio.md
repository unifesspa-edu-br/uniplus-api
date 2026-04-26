# Relatório dos Spikes S3 e S4 (Kafka) — Outbox Wolverine (#158)

- **Branch:** `spikes/158-s3-transporte-kafka`
- **Branch base:** `spikes/158-s2-transporte-postgresql` (`fee75f5`) → `main` (`54f1a8b`)
- **Data:** 2026-04-25
- **Status:** **AC1a, AC1b e AC2 também comprovados com transporte Kafka real** — 4 testes aprovados (`S2/V4`, `S4/V4`, `S3/V5`, `S4/V5`)
- **Plano de referência:** [`docs/spikes/158-plano-validacao-outbox-wolverine.md`](158-plano-validacao-outbox-wolverine.md)
- **Relatórios anteriores:** [`158-s0-relatorio.md`](158-s0-relatorio.md), [`158-s2-relatorio.md`](158-s2-relatorio.md)

## Resumo executivo

Transporte Kafka do Wolverine entrega o invariante exigido pelo projeto, mantendo
o caminho PG (envelope storage + queue) coexistindo no mesmo host:

- **S3/V5 (Kafka caminho feliz):** comando via `IMessageBus.InvokeAsync` altera
  entidade, gera `AddDomainEvent`, e o `EditalPublicadoEvent` é publicado no
  tópico Kafka `edital_events` somente após commit. Consumer externo
  (`Confluent.Kafka`) recebe a mensagem com payload preservado.
- **S4/V5 (rollback Kafka):** exceção depois de `SaveChanges` e antes do retorno
  do handler **não publica** mensagem no tópico Kafka. Tópico permanece sem
  evento fantasma para o `NumeroEdital` de teste.

Todos os testes prévios da matriz continuam verdes (`S2/V4` e `S4/V4` PG)
mesmo com Kafka transport adicionado em paralelo no mesmo extension.

ACs do plano cobertos até esta fase:

| AC | Requisito | Status |
|---|---|---|
| AC1a | Persistência transacional do envelope com `SaveChanges` | **Comprovado** em PG (S2/V4) e Kafka (S3/V5) |
| AC1b | Entrega ao destino após commit | **Comprovado** em PG queue (S2/V4) e Kafka topic (S3/V5) |
| AC2  | Rollback elimina entidade e mensagem | **Comprovado** em PG (S4/V4) e Kafka (S4/V5) |
| AC3  | Recuperação após restart | Pendente — endereçado em S6 |
| AC4  | Migration auditável das tabelas Wolverine | Pendente — endereçado em S8 |
| AC5  | Retry EF/Wolverine sem conflito | Decisão de S7 já aplicada (desligar `EnableRetryOnFailure`) — endereçado em detalhe em S7 |

## Configuração validada

```csharp
options
    .PersistMessagesWithPostgresql(connectionString, schemaName: "wolverine")
    .EnableMessageTransport(_ => { });

options.PublishMessage<EditalPublicadoEvent>().ToPostgresqlQueue("domain-events");
options.ListenToPostgresqlQueue("domain-events");

// Kafka publish-only — verificação via consumer externo.
options.UseKafka(kafkaBootstrapServers).AutoProvision();
options.PublishMessage<EditalPublicadoEvent>().ToKafkaTopic("edital_events");

options.PublishDomainEventsFromEntityFrameworkCore<EntityBase>(
    entity => entity.DomainEvents);
```

`EditalPublicadoEvent` é publicado em **dois destinos simultaneamente**: a
queue PG (consumida pelo handler local de teste, via `ListenToPostgresqlQueue`)
e o tópico Kafka (verificado pelo consumer externo do teste). Não há listener
Wolverine para o tópico Kafka — o spike trata Kafka como publish-only para
isolar a verificação no consumer Confluent.

## Achados desta fase

### C1 — Conflito NU1605 também em `WolverineFx.Kafka` (esperado pelo plano)

Mesmo padrão do achado B1 (S2): `WolverineFx.Kafka 5.32.1` oficial transitivamente
exige `WolverineFx >= 5.32.1`, que falha contra o fork prerelease 5.32.1-pr2586.

**Solução aplicada:** gerar `WolverineFx.Kafka.5.32.1-pr2586.nupkg` a partir do
fork e adicionar ao feed local:

```bash
dotnet pack /home/jeferson/Projects/workspaces/wolverine-fork/src/Transports/Kafka/Wolverine.Kafka/Wolverine.Kafka.csproj \
  -c Release \
  -p:Version=5.32.1-pr2586 \
  -p:PackageVersion=5.32.1-pr2586 \
  -o /tmp/wolverine-pack
```

Mapping do `wolverine-pr2586` em `nuget.config` agora cobre 4 pacotes:
`WolverineFx`, `WolverineFx.EntityFrameworkCore`, `WolverineFx.RDBMS`,
`WolverineFx.Postgresql`, `WolverineFx.Kafka`. Todos saem juntos do feed local
quando upstream publicar 5.32.2+.

### C2 — Bump transitivo de `Confluent.Kafka` 2.13.2 → 2.14.0

`WolverineFx.Kafka 5.32.1-pr2586` → `Confluent.SchemaRegistry.Serdes.Avro 2.14.0`
→ `Confluent.Kafka >= 2.14.0`. O projeto tinha `Confluent.Kafka 2.13.2`
declarado em `Directory.Packages.props`. Bump para 2.14.0 (patch level, sem
breaking changes documentadas) destrava o restore.

`Infrastructure.Core` (consumidor original do `Confluent.Kafka`) continua
compilando e a solution inteira passa.

### C3 — `AutoProvision()` no Kafka transport é necessário em ambiente Testcontainers

Sem `AutoProvision`, o consumer externo lança imediatamente:

```
Confluent.Kafka.ConsumeException : Subscribed topic not available:
edital-events: Broker: Unknown topic or partition
```

E o publish do Wolverine fica pendurado, fazendo qualquer tracking baseado em
`IncludeExternalTransports()` cair em timeout.

**Solução:** `options.UseKafka(bootstrap).AutoProvision()`. Wolverine cria
tópicos faltantes na inicialização. Implicação para produção: `AutoProvision`
deve ser explícito conforme política de governança de tópicos — não cabe
copiar essa configuração para `Selecao.API` sem decisão de S8/S9.

### C4 — Nome do tópico Kafka: usar `_` em vez de `-`

Wolverine internamente trata `-` em nomes de queue/topic como inválido em
contextos SQL (queue PG `domain-events` vira tabela `domain_events`). Para
manter consistência e evitar surpresas com Kafka admin clients, padronizei o
tópico como `edital_events` no spike. Convenção a documentar em S9 (operação).

### C5 — `IncludeExternalTransports()` não combina com Kafka publish-only

O tracking via `host.TrackActivity().IncludeExternalTransports().InvokeMessageAndWaitAsync(...)`
do S2 anterior **deixou de funcionar** ao adicionar Kafka transport. Causa: o
tracking espera `MessageSucceeded` para **toda** mensagem `Sent`, e a
publicação Kafka neste spike é publish-only (sem listener Wolverine), portanto
nunca recebe `MessageSucceeded`. O tracking acumula:

```
| EditalPublicadoEvent | Sent (PG)                |
| EditalPublicadoEvent | Sent (Kafka)             |
| EditalPublicadoEvent | Received   (PG)          |
| EditalPublicadoEvent | ExecutionFinished (PG)   |
| EditalPublicadoEvent | MessageSucceeded  (PG)   |
... (Kafka MessageSucceeded nunca chega)
```

E cai em timeout.

**Solução adotada:** trocar tracking por **polling no `DomainEventCollector`**
para o caminho PG, e por **Confluent consumer poll** para o caminho Kafka.
Helper `OutboxCapabilityMatrixTests.EsperarEventoNoColetorAsync(collector,
numero, timeout)` tenta a cada 100 ms até receber o evento esperado ou
timeout. Mais robusto e independente do framework de tracking — escala para os
spikes seguintes.

Implicação para o relatório anterior (S2): o `158-s2-relatorio.md` documenta
o uso de `TrackActivity().IncludeExternalTransports()` que **não** funciona
mais com Kafka transport ativo. O spike S2 hoje passa porque também migrou
para polling. O guia de S9 deve recomendar polling/await-stream-position como
padrão de teste de integração com Wolverine.

### C6 — `Kafka` transport não rouba mensagens da queue PG

Validação implícita pelos resultados: `EditalPublicadoEvent` é roteado para
**ambos** os destinos simultaneamente. O handler subscritor recebe via PG
queue uma vez (não duas), e o consumer externo recebe via Kafka topic uma
vez. Wolverine trata os dois destinos como independentes — comportamento
desejado para o caso "fan-out para sistemas externos + processamento local".

## Resultados

```bash
dotnet test tests/Unifesspa.UniPlus.Selecao.IntegrationTests/Unifesspa.UniPlus.Selecao.IntegrationTests.csproj \
  --filter "Category=OutboxCapability"
```

```
Aprovado S2/V4 — PG transport entrega EditalPublicadoEvent ao handler local
Aprovado S4/V4 — rollback PG: exceção pós-SaveChanges deixa entidade ausente
Aprovado S3/V5 — Kafka transport publica EditalPublicadoEvent no tópico
Aprovado S4/V5 — rollback Kafka: exceção pós-SaveChanges não publica no tópico

Total de testes: 4
     Aprovados: 4
Tempo total: 21,6045 Segundos
```

## Atualização da matriz V0–V7

Linhas atualizadas em [`158-plano-validacao-outbox-wolverine.md`](158-plano-validacao-outbox-wolverine.md):

| Variante | Configuração | Esperado após fix | Observado | Status |
|---|---|---|---|---|
| V4 | PostgreSQL transport `ToPostgresqlQueue(...)` | Outbox durável sem Kafka | S2/V4 e S4/V4 verdes | **Aprovado** |
| V5 | Kafka transport com durable outbox PG | Caminho real com broker externo, mensagem só após commit | S3/V5 e S4/V5 verdes; tópico recebe payload deserializável após commit; rollback impede publicação | **Aprovado** |

## Cuidados / dívidas técnicas observadas

- **`AutoProvision` em produção**: não é seguro copiar do spike. Em produção
  os tópicos devem ser provisionados pelo time de plataforma; `AutoProvision`
  só faz sentido em dev/test.
- **Convenção de nome de tópico**: padronizar em `snake_case` (sem `-`) para
  evitar surpresas com clientes admin que validam DNS-style names.
- **Kafka publish-only**: o spike não testou Wolverine **consumindo** Kafka.
  Quando a aplicação real precisar consumir, S9 deve incluir cenário com
  `ListenToKafkaTopic`. Padrão "fan-out + local handler" coberto aqui é o uso
  imediato (publicação para sistemas externos).
- **`IncludeExternalTransports` indisponível**: a recomendação de S9 deve ser
  evitar tracking framework para integrar Wolverine com Kafka publish-only e
  preferir polling determinístico nos testes.
- **Bump de Confluent.Kafka**: cobrir em smoke test do `Infrastructure.Core`
  (consumidor histórico) — em particular fluxo de produção do Kafka usado
  pelo módulo Ingresso quando ele entrar.

## Próximo passo recomendado

**S5 — Kafka indisponível** ([§S5 do plano](158-plano-validacao-outbox-wolverine.md#s5---kafka-indisponível)).

Pré-requisitos para S5:

1. Capacidade de **derrubar** o broker Kafka durante o teste (parar o
   `KafkaContainer` ou bloquear conexão via Toxiproxy/iptables).
2. Verificar que `wolverine.wolverine_outgoing_envelopes` mantém envelope
   pendente enquanto Kafka está fora.
3. Religar Kafka, verificar que envelope é despachado e consumer externo
   recebe a mensagem.
4. Validar que não há duplicidade indevida (idempotency mínima).

S5 é o primeiro spike que toca **store-and-forward real** — exige tooling
para simular indisponibilidade de broker. Decidir se usamos
`Testcontainers.StopAsync` + `StartAsync` ou se introduzimos Toxiproxy.

## Versões e ambiente

- **Pacotes Wolverine no feed local:** `WolverineFx`, `WolverineFx.EntityFrameworkCore`,
  `WolverineFx.RDBMS`, `WolverineFx.Postgresql`, `WolverineFx.Kafka` — todos
  `5.32.1-pr2586`.
- **Pacote local gerado nesta fase:** `WolverineFx.Kafka.5.32.1-pr2586.nupkg`
  (fork em `fix/domain-event-scraper-materialize-before-publish`, `cd6a2ee`).
- **`Confluent.Kafka`:** bump 2.13.2 → 2.14.0 em `Directory.Packages.props`.
- **`Testcontainers.PostgreSql`:** 4.11.0; **`Testcontainers.Kafka`:** 4.11.0.
- **Kafka image:** `confluentinc/cp-kafka:7.6.1`.
- **Postgres image:** `postgres:18-alpine`.
- **Runtime:** .NET 10 / C# 14, Linux 6.19.14-arch1-1, Docker 28.3.3.
- **Conta gh ativa:** `marmota-alpina`.

## Referências

- [Plano de validação do outbox Wolverine (#158)](158-plano-validacao-outbox-wolverine.md)
- [Relatório S0 — V0 inviável](158-s0-relatorio.md)
- [Relatório S2 — V4 aprovado](158-s2-relatorio.md)
- [ADR-022 — backbone CQRS Wolverine](https://github.com/unifesspa-edu-br/uniplus-docs/blob/main/docs/adrs/ADR-022-backbone-cqrs-wolverine.md)
- [ADR-024 — outbox Wolverine + EF não adotado em #135](https://github.com/unifesspa-edu-br/uniplus-docs/blob/main/docs/adrs/ADR-024-outbox-wolverine-ef-nao-adotado-em-135.md)
- [PR uniplus-api#160 — feed local com fix do scraper](https://github.com/unifesspa-edu-br/uniplus-api/pull/160)
- [JasperFx/wolverine#2585](https://github.com/JasperFx/wolverine/issues/2585) e [#2586](https://github.com/JasperFx/wolverine/pull/2586)
