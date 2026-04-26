# Relatório do Spike S6 — Outbox Wolverine (#158)

- **Branch:** `spikes/158-s6-restart-recovery`
- **Branch base:** `spikes/158-s5-kafka-indisponivel` (`5c9ccee`)
- **Data:** 2026-04-25
- **Status:** **AC3 (parte recuperação) comprovado** — `S6/V7` aprovado. 6 testes verdes na matriz.
- **Plano de referência:** [`docs/spikes/158-plano-validacao-outbox-wolverine.md`](158-plano-validacao-outbox-wolverine.md)

## Resumo executivo

Cenário validado: host derrubado mid-processamento, ownership reatribuído ao novo host, mensagem entregue.

Sequência:

1. Host A configurado com handler "lento" (`Task.Delay(60s)` antes de gravar no coletor).
2. `bus.InvokeAsync(PublicarEditalSpikeCommand)` faz commit transacional → envelope vai para `wolverine_outgoing_envelopes` → listener PG queue retira para `wolverine_incoming_envelopes` → handler começa, fica em `Task.Delay`.
3. `factoryA.DisposeAsync()` derruba host A. Ownership do envelope (`wolverine_node_assignments`) fica órfão.
4. `factoryB = _fixture.CriarFactory()` sobe novo host apontando para o **mesmo** Postgres.
5. Host B (com `AtrasoNoHandler = TimeSpan.Zero`) reaproveita o envelope órfão e o handler subscritor registra o evento no coletor.

Tempo total do test: ~6s. Wolverine reatribui mensagens de nós mortos automaticamente — o log mostra `Reassigned X incoming messages from node 1 ...` (visto também nos spikes anteriores).

**AC3 do plano comprovado** (parte recuperação). Combinado com S5/parte 1 (envelope persiste durante indisponibilidade temporária), o quadro do AC3 fica:

| Cenário do AC3 | Status |
|---|---|
| Envelope persiste em storage durável durante indisponibilidade temporária | **Comprovado** (S5 parte 1) |
| Despacho automático ao retorno do broker externo | Pendente (S5 parte 2 — limitação técnica do Testcontainers Kafka ZK) |
| Reassignment + processamento por outro host após restart | **Comprovado** (S6/V7) |

## Achados desta fase

### E1 — `Reassigned X incoming messages` é a chave da recuperação

O log do spike confirma o mecanismo Wolverine:

```
[INF] Reassigned 1 incoming messages from 1 and endpoint at postgresql://domain_events/ to any node in the durable inbox
```

Quando o nó (node id) morre, Wolverine no próximo nó vivo detecta envelopes com ownership vago e os redistribui. Isso depende de:

- `wolverine.wolverine_nodes` — registro de nós ativos.
- `wolverine.wolverine_node_assignments` — quem é dono de qual endpoint.
- `wolverine_incoming_envelopes` — envelopes recebidos mas não confirmados.

O reassignment é **automático**, sem ação operacional. Para produção isso significa que um pod morto não bloqueia mensagens — outras réplicas pegam.

### E2 — `factoryA.DisposeAsync()` aciona shutdown gracioso, não kill -9

`WebApplicationFactory.DisposeAsync()` chama `IHost.StopAsync` que dispara `IHostApplicationLifetime.StopApplication`. Os listeners Wolverine recebem cancellation e param ordeiramente. **Isso ainda valida o cenário de restart**, mas não é equivalente a `kill -9` ou crash de processo.

Para validar crash não-gracioso, precisaria executar o teste em processo separado e matá-lo via Process API. Custo desproporcional ao spike — anotado como melhoria futura (S9 ou hardening).

### E3 — Fixture com `CriarFactory()` builder permite múltiplos hosts

Padrão criado em `OutboxRestartFixture.CriarFactory()`: o fixture sobe os
containers (PG + Kafka) na inicialização e expõe um método para o teste
instanciar quantas factories quiser, todas apontando para o **mesmo** storage.
Padrão útil também para S9 (operação) caso queira simular múltiplas réplicas.

### E4 — `SpikeHandlerControl.AtrasoNoHandler` injetável simula latência

Útil para spike S6 e potencialmente S9. Permite que um teste configure
processamento lento sem mexer no código do handler. `EditalPublicadoSpikeHandler`
agora é `async` e respeita o token de cancelamento do Wolverine.

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
Aprovado S6/V7 — restart: mensagem pendente é processada por novo host

Total de testes: 6
     Aprovados: 6
Tempo total: 42 s
```

## Atualização da matriz V0–V7

| Variante | Configuração | Esperado | Observado | Status |
|---|---|---|---|---|
| V7 | Restart recovery | Mensagem pendente sobrevive ao restart e é entregue | S6/V7: host A morre mid-processamento, host B reatribui ownership e o handler subscritor recebe em <60s | **Aprovado** |

## Próximo passo recomendado

**S7 — Retry strategy** (detalhar):

1. Variante A: EF `EnableRetryOnFailure` ON + Wolverine `AutoApplyTransactions` → confirmar que dá `does not support user-initiated transactions` (já visto em S2 implícito).
2. Variante B: EF retry OFF + Wolverine retry → caminho atualmente em uso.
3. Decisão recomendada do plano: desligar `EnableRetryOnFailure` em DbContexts usados por handlers Wolverine.

S7 vai documentar formalmente a decisão de produção e gerar um teste de regressão para o conflito.

## Versões e ambiente

- `WolverineFx.*` 5.32.1-pr2586 do feed local.
- `Testcontainers.PostgreSql` 4.11.0; `Testcontainers.Kafka` 4.11.0.
- Postgres image: `postgres:18-alpine`.
- Kafka image: `confluentinc/cp-kafka:7.6.1`.
- Runtime: .NET 10 / C# 14.
- Conta gh ativa: `marmota-alpina`.

## Referências

- [Plano de validação do outbox Wolverine (#158)](158-plano-validacao-outbox-wolverine.md)
- [Relatório S0](158-s0-relatorio.md), [S2](158-s2-relatorio.md), [S3](158-s3-relatorio.md), [S5](158-s5-relatorio.md)
