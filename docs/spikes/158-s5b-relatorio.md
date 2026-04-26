# Relatório do Spike S5b — Outbox Wolverine (#158)

- **Branch:** `spikes/158-s5b-kafka-kraft`
- **Branch base:** `spikes/158-relatorio-final` (`5fa3ac4`)
- **Data:** 2026-04-25
- **Status:** **AC3 COMPLETO** — parte 2 do S5 (despacho ao retorno do broker) destravada com Kafka KRaft. 13/13 testes verdes.
- **Plano de referência:** [`docs/spikes/158-plano-validacao-outbox-wolverine.md`](158-plano-validacao-outbox-wolverine.md)

## Resumo executivo

A pendência registrada em `158-s5-relatorio.md` foi resolvida nesta sessão.
Trocando a imagem `confluentinc/cp-kafka:7.6.1` (Zookeeper) para
`apache/kafka:3.9.0` (KRaft puro), o `KafkaContainer` do Testcontainers
suporta `StopAsync` + `StartAsync` sem o `NodeExists` do ZK que travou
S5 originalmente.

Cenário validado:

1. Para o broker.
2. Dispara comando — entidade persiste, queue PG entrega, **envelope Kafka
   fica em `wolverine_outgoing_envelopes`**.
3. Religa o broker.
4. Wolverine despacha o envelope retido — **consumer externo Kafka recebe
   o `EditalPublicadoEvent` em <8s** após o restart.

**AC3 do plano agora completamente comprovado:**

| Cenário do AC3 | Status | Spike |
|---|---|---|
| Envelope persiste em storage durável durante indisponibilidade temporária | ✅ | S5/V6 parte 1 |
| Despacho automático ao retorno do broker externo | ✅ | **S5/V6 parte 2 (este relatório)** |
| Reassignment + processamento por outro host após restart | ✅ | S6/V7 |

## Achados desta fase

### I1 — `apache/kafka:3.9.0` (KRaft) suporta restart limpo

A imagem oficial do Apache Kafka em modo KRaft (sem Zookeeper) permite
`StopAsync` + `StartAsync` no mesmo container sem erro. Coerente com
**Apache Kafka 4.2 KRaft** já adotado em produção pelo projeto (linha 14
de `CLAUDE.md`).

### I2 — Porta fixa é necessária para reuso do producer Wolverine

Por default, `Testcontainers.Kafka` reatribui porta de host no restart do
container. O producer Confluent dentro do Wolverine resolve `BootstrapServers`
no startup do host e não atualiza dinamicamente. Resultado: producer
continua tentando porta antiga, despacho ao retorno falha por timeout.

**Solução:** `KafkaBuilder.WithPortBinding(19092, 9092)` fixa a porta de
host. Restart preserva o endereço, producer reconecta automaticamente.

```csharp
private const int HostPort = 19092;

private readonly KafkaContainer _kafka = new KafkaBuilder("apache/kafka:3.9.0")
    .WithPortBinding(HostPort, 9092)
    .Build();
```

A porta `19092` foi escolhida para evitar colisão com Kafka local de
desenvolvimento que costuma usar 9092. A collection do S5b é isolada e
roda sequencialmente (xunit.runner.json desabilita
`parallelizeTestCollections`).

**Implicação para testes futuros que precisem de Kafka resiliente:** usar
sempre porta fixa quando o cenário envolver restart do broker.

### I3 — Wolverine retry de outbox para Kafka funciona automaticamente

Sem código de retry explícito, a mensagem retida em
`wolverine_outgoing_envelopes` foi despachada em <8s após o broker voltar.
O `DurabilityAgent` do Wolverine drena periodicamente envelopes pendentes
e reentrega quando o destino fica acessível. **Comportamento out-of-the-box.**

### I4 — Tempo total do test (~13s) inclui restart do broker

O cenário completo (parar broker, publicar, religar, esperar despacho) leva
cerca de 13s no ambiente local. Em CI o tempo pode ser maior dependendo do
host. Tolerância configurada em 180s no teste — folga adequada.

## Resultados

```bash
dotnet test tests/Unifesspa.UniPlus.Selecao.IntegrationTests --filter "Category=OutboxCapability"
```

```
Aprovado S2/V4 — PG transport entrega EditalPublicadoEvent ao handler local
Aprovado S4/V4 — rollback PG: entidade ausente, envelope ausente
Aprovado S3/V5 — Kafka transport publica EditalPublicadoEvent no tópico
Aprovado S4/V5 — rollback Kafka: tópico não recebe mensagem fantasma
Aprovado S5/V6 (parte 1) — Kafka offline: envelope pendente em storage
Aprovado S5/V6 (parte 2) — Kafka volta: envelope retido é despachado
Aprovado S6/V7 — restart: novo host processa mensagem pendente
Aprovado S7 (variante A) — EF retry ON levanta conflito esperado
Aprovado S7 (variante B) — EF retry OFF é a recomendação aplicada
Aprovado S8 — schema 'wolverine' contém todas as tabelas esperadas
Aprovado S8 — superfície completa observada documentada
Aprovado S9 — handler que sempre falha gera entrada em dead letters
Aprovado S9 — query SQL de dead letters retorna snapshot inspecionável

Total de testes: 13
     Aprovados: 13
Tempo total: 46 s
```

## Atualização da matriz V0–V7

| Variante | Status atual |
|---|---|
| V6 | **Aprovado** completo (parte 1 + parte 2) |

Combinado com S6/V7, AC3 do plano fica **fechado** — não há mais pendências
em recuperação/resiliência.

## Implicação para o relatório final

`158-relatorio-final.md` precisa ser atualizado:

- Status final por AC: AC3 passa de "✅ + pendência menor" para **"✅ comprovado"**.
- Matriz V0–V7: V6 passa de "⚠️ Parcial" para **"✅ Aprovado"**.
- Decisões pendentes: item 4 (S5 parte 2) é removido — concluído nesta sessão.
- Trabalhos seguintes: item 4 (re-executar S5 parte 2 com KRaft) também
  é removido.
- Versões: imagem Kafka adicionada `apache/kafka:3.9.0` (em uso para S5b).
  Imagem `confluentinc/cp-kafka:7.6.1` permanece para os outros spikes
  (será unificada quando refatorarmos a fixture geral para também usar
  KRaft — recomendado).

Atualização será feita em commit subsequente ao deste relatório.

## Versões e ambiente

- `WolverineFx.*` 5.32.1-pr2586 do feed local.
- `Testcontainers.PostgreSql` 4.11.0; `Testcontainers.Kafka` 4.11.0.
- Postgres image: `postgres:18-alpine`.
- **Kafka image: `apache/kafka:3.9.0` (KRaft puro)** — alinhado com a
  arquitetura de produção (Kafka 4.2 KRaft).
- Runtime: .NET 10 / C# 14.
- Conta gh ativa: `marmota-alpina`.

## Referências

- [Plano de validação do outbox Wolverine (#158)](158-plano-validacao-outbox-wolverine.md)
- [Relatório S5 — parte 1 + pendência KRaft](158-s5-relatorio.md)
- [Relatório final consolidado](158-relatorio-final.md) (a ser atualizado)
- [Apache Kafka KRaft Mode](https://developer.confluent.io/learn/kraft/)
- [KIP-866 — ZooKeeper to KRaft Migration](https://cwiki.apache.org/confluence/display/KAFKA/KIP-866+ZooKeeper+to+KRaft+Migration)
