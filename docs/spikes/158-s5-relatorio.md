# Relatório do Spike S5 — Outbox Wolverine (#158)

- **Branch:** `spikes/158-s5-kafka-indisponivel`
- **Branch base:** `spikes/158-s3-transporte-kafka` (`dc88a42`)
- **Data:** 2026-04-25
- **Status:** **AC3 (parte 1) comprovado**, parte 2 (despacho após retorno do broker) **bloqueada por limitação técnica do Testcontainers Kafka** — documentada como pendência. 5 testes verdes na matriz.
- **Plano de referência:** [`docs/spikes/158-plano-validacao-outbox-wolverine.md`](158-plano-validacao-outbox-wolverine.md)

## Resumo executivo

Com Kafka offline:

- **Entidade persiste** no Postgres (commit transacional não falha por causa do broker).
- **Queue PG continua entregando** ao handler subscritor local.
- **Envelope Kafka fica em `wolverine.wolverine_outgoing_envelopes`** aguardando despacho.

Critério **AC3 do plano (parte 1)** validado: indisponibilidade temporária do Kafka **não derruba** o commit do domínio nem perde a mensagem. A janela é coberta pelo outbox durável.

A **parte 2 (despacho após retorno do broker)** **não foi exercida** no spike porque a imagem `confluentinc/cp-kafka:7.6.1` em modo Zookeeper não suporta `StopAsync`+`StartAsync` via Testcontainers (`NodeExists` no ZK ao re-registrar broker). Documentada como pendência abaixo, com caminhos sugeridos.

## Achados desta fase

### D1 — Wolverine **não** usa outbox durável por default em senders externos

Sem configuração explícita, `PublishMessage<T>().ToKafkaTopic(...)` envia direto ao producer Confluent (buffering in-memory). Quando Kafka cai, mensagem fica **na fila in-memory do producer client**, não em `wolverine_outgoing_envelopes`. Se o processo morrer, mensagem é perdida.

**Solução aplicada:**

```csharp
options.Policies.UseDurableOutboxOnAllSendingEndpoints();
```

Força TODOS os senders externos (PG queue, Kafka topic) a passarem por `wolverine_outgoing_envelopes` antes do despacho. Validação experimental: após adicionar a política, contagem em `wolverine_outgoing_envelopes` para `EditalPublicadoEvent` é >0 quando Kafka está fora, e os 5 testes anteriores (S2, S4-PG, S3, S4-Kafka) continuam verdes.

**Implicação para produção (#158):** essa política é **obrigatória** para o caminho do outbox transacional em Kafka. Não é o default.

### D2 — `body` da tabela `wolverine_outgoing_envelopes` é binário

Wolverine usa serialização binária por default. Tentar `convert_from(body, 'UTF8')` para filtrar por payload falha com `Npgsql.PostgresException : 22021: invalid byte sequence for encoding "UTF8": 0xaf`. Para inspeção, filtrar apenas por `message_type` ou desserializar lado-cliente.

Implicação para S9 (operação): runbook de inspeção/replay precisa indicar que payload é binário e como deserializar (provavelmente com `Wolverine.Persistence.IMessageStore` API).

### D3 — `cp-kafka:7.6.1` (Zookeeper) não suporta restart via Testcontainers

`KafkaContainer.StopAsync()` para o broker. `KafkaContainer.StartAsync()` no mesmo container falha com:

```
org.apache.zookeeper.KeeperException$NodeExistsException: KeeperErrorCode = NodeExists
  at kafka.zk.KafkaZkClient.registerBroker(KafkaZkClient.scala:106)
  at kafka.server.KafkaServer.startup(KafkaServer.scala:368)
```

O ZK preserva o ephemeral node `/brokers/ids/1` da sessão anterior, e o novo broker (com mesmo ID) não consegue se registrar. Sessão TTL do ZK precisaria expirar antes do restart, ou o ephemeral node ser limpo manualmente.

**Caminhos para destravar a parte 2:**

| Estratégia | Custo | Trade-off |
|---|---|---|
| Imagem KRaft (`confluentinc/cp-kafka:7.x` em modo `KAFKA_PROCESS_ROLES=broker,controller`) | Médio — config customizada do `Testcontainers.Kafka` | Sem ZK, restart é mais limpo |
| `Toxiproxy` na frente do broker (bloqueia conexão sem parar processo) | Médio — adicionar Toxiproxy.Net SDK + container | Não simula restart real do broker |
| Pular o restart e validar ENVIANDO direto via Confluent admin para tópico revogado/recriado | Baixo | Não testa caminho real — só simulação |
| Usar dois brokers em rede | Alto | Complexidade desproporcional para spike |

Recomendação: **KRaft mode na próxima iteração de S5/S6** — é o padrão a partir do Kafka 4.x e o projeto já usa Kafka 4.2 em produção (linha 14 de `CLAUDE.md`). Coerente com o ambiente real.

### D4 — Fixture isolada para spikes destrutivos

Como o broker não volta após `StopAsync`, S5 não pode compartilhar fixture com os outros testes da matriz (deixaria a fixture quebrada). Criada `OutboxKafkaResilienceFixture` com containers próprios e `OutboxKafkaResilienceCollection`. Padrão a repetir para S6 (restart recovery), que também derrubará host.

`xunit.runner.json` desabilita `parallelizeTestCollections` para evitar disputa pelos campos estáticos `OutboxSpikeWolverineExtension.PostgresqlConnectionString` / `KafkaBootstrapServers` entre as duas fixtures.

## Resultados

```bash
dotnet test tests/Unifesspa.UniPlus.Selecao.IntegrationTests --filter "Category=OutboxCapability"
```

```
Aprovado S2/V4 — PG transport entrega EditalPublicadoEvent ao handler local
Aprovado S4/V4 — rollback PG: entidade ausente, envelope ausente
Aprovado S3/V5 — Kafka transport publica EditalPublicadoEvent no tópico
Aprovado S4/V5 — rollback Kafka: tópico não recebe mensagem fantasma
Aprovado S5/V6 (parte 1) — Kafka offline: entidade persiste, queue PG entrega,
   envelope Kafka fica pendente em wolverine_outgoing_envelopes

Total de testes: 5
     Aprovados: 5
Tempo total: 33 s
```

## Atualização da matriz V0–V7

| Variante | Configuração | Esperado | Observado | Status |
|---|---|---|---|---|
| V6 | Kafka indisponível no commit | Entidade confirma, envelope recuperável, despacho ao retorno | Parte 1 ✅ (S5/V6); parte 2 (despacho ao retorno) **bloqueada por D3** — pendente | **Parcial — recomenda-se KRaft em próxima iteração** |

## Próximo passo recomendado

**S6 — Restart recovery**. Pré-requisitos:

1. Mesma decisão sobre imagem Kafka (KRaft vs ZK).
2. Capacidade de derrubar **o host Wolverine** (não o broker) durante o test — `_factory.DisposeAsync()` + recriar.
3. Validar que `wolverine_incoming_envelopes` retém mensagens não processadas e que novo host as processa.

S6 expõe `AC3 (parte recuperação)` — combinado com S5/parte 2 (que ficou pendente), forma o quadro completo do AC3.

## Versões e ambiente

- `WolverineFx.*` 5.32.1-pr2586 do feed local.
- `Testcontainers.PostgreSql` 4.11.0; `Testcontainers.Kafka` 4.11.0.
- Kafka image: `confluentinc/cp-kafka:7.6.1` (Zookeeper) — limitação detectada em D3.
- Postgres image: `postgres:18-alpine`.
- Runtime: .NET 10 / C# 14, Linux 6.19.14-arch1-1, Docker 28.3.3.
- Conta gh ativa: `marmota-alpina`.

## Referências

- [Plano de validação do outbox Wolverine (#158)](158-plano-validacao-outbox-wolverine.md)
- [Relatório S0](158-s0-relatorio.md), [S2](158-s2-relatorio.md), [S3](158-s3-relatorio.md)
