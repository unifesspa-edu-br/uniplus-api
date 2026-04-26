# Relatório do Spike S9 — Outbox Wolverine (#158)

- **Branch:** `spikes/158-s9-operacao`
- **Branch base:** `spikes/158-s8-migration-surface` (`14eba2e`)
- **Data:** 2026-04-25
- **Status:** **Runbook operacional consolidado**. 12/12 testes verdes — fim da matriz S0–S9.
- **Plano de referência:** [`docs/spikes/158-plano-validacao-outbox-wolverine.md`](158-plano-validacao-outbox-wolverine.md)

## Resumo executivo

S9 valida o caminho operacional do outbox:

- **Dead-letter automatizada via `[MoveToErrorQueueOn(typeof(InvalidOperationException))]`**: handler que sempre falha resulta em entrada em `wolverine.wolverine_dead_letters` em <30s.
- **Queries SQL inspecionáveis** documentadas para o runbook.
- **Replay** disponível via `IDeadLetters.MarkDeadLetterEnvelopesAsReplayableAsync(...)` (não exercido com teste, mas mapeado).

## Runbook operacional — Wolverine outbox no UniPlus

### 1. Inspecionar mensagens pendentes

```sql
-- Top 20 envelopes outgoing aguardando despacho
SELECT id, message_type, destination, owner_id, status,
       attempts, scheduled_time
  FROM wolverine.wolverine_outgoing_envelopes
 ORDER BY id DESC
 LIMIT 20;

-- Outgoing por tipo de mensagem
SELECT message_type, COUNT(*)
  FROM wolverine.wolverine_outgoing_envelopes
 GROUP BY message_type
 ORDER BY COUNT(*) DESC;

-- Incoming aguardando processamento
SELECT id, message_type, owner_id, status, attempts
  FROM wolverine.wolverine_incoming_envelopes
 ORDER BY id DESC
 LIMIT 20;
```

### 2. Inspecionar dead letters

```sql
-- Resumo por tipo
SELECT message_type, COUNT(*)
  FROM wolverine.wolverine_dead_letters
 GROUP BY message_type
 ORDER BY COUNT(*) DESC;

-- Detalhe das últimas falhas com contexto
SELECT id, message_type, exception_type, exception_message, sent_at
  FROM wolverine.wolverine_dead_letters
 ORDER BY sent_at DESC
 LIMIT 50;
```

### 3. Identificar nó "dono" da mensagem

```sql
-- Quem está processando o quê
SELECT n.assigned_node_id, ww.id, ww.message_type
  FROM wolverine.wolverine_node_assignments n
  JOIN wolverine.wolverine_incoming_envelopes ww
    ON ww.owner_id = n.assigned_node_id
 WHERE ww.status = 'Incoming';

-- Nós ativos
SELECT id, started, capabilities, description
  FROM wolverine.wolverine_nodes
 ORDER BY started DESC;
```

### 4. Reprocessar mensagem em dead-letter

**API Wolverine (recomendado):**

```csharp
// Injetar IDeadLetters via DI
public class ReplayController(IDeadLetters deadLetters)
{
    public async Task<int> Replay(string exceptionType)
    {
        return await deadLetters.MarkDeadLetterEnvelopesAsReplayableAsync(exceptionType);
    }
}
```

`MarkDeadLetterEnvelopesAsReplayableAsync` move envelopes da dead-letter
table para incoming, onde o `DurabilityAgent` os reprocessa. Sobrecarga
existe para filtrar por `Guid[]` (ids específicos) ou por exception type.

**Via SQL (último recurso, com cuidado):**

```sql
-- Marcar envelope específico como replayable
UPDATE wolverine.wolverine_dead_letters
   SET replayable = true
 WHERE id = '...';
```

### 5. Limpar mensagens antigas (retenção)

`Wolverine.DurabilitySettings.DeadLetterQueueExpirationEnabled` (opt-in)
+ `DeadLetterQueueExpiration` (default 10 dias) controla retenção
automática. Para o UniPlus, decidir o valor apropriado em produção
considerando LGPD e auditabilidade.

```csharp
// Configuração em Program.cs (a ser definida no ADR)
builder.Host.UseWolverine(opts =>
{
    opts.Durability.DeadLetterQueueExpirationEnabled = true;
    opts.Durability.DeadLetterQueueExpiration = TimeSpan.FromDays(30);
});
```

### 6. Logs e métricas em falhas de relay

Logs Wolverine relevantes durante os spikes:

```
[INF] Reassigned X incoming messages from node Y to any node in the durable inbox
[INF] Started message listener at postgresql://domain_events/
[INF] Started message listener at kafka://topic/edital_events
[ERR] Invocation of FalharAposSaveChangesSpikeCommand failed!
[DBG] Successfully sent 1 messages to kafka://topic/edital_events
```

OpenTelemetry: o pacote `Wolverine` emite métricas. Configurar coleta no
`Program.cs` da API quando outbox for adotado.

## Achados desta fase

### H1 — `[MoveToErrorQueueOn(...)]` move imediatamente

Sem retry, sem espera. A mensagem vai direto para `wolverine_dead_letters`
quando o tipo de exception bate. Útil para falhas determinísticas (validação
de payload, etc.). Para falhas transitórias, usar `[ScheduleRetry(...)]` ou
políticas Wolverine.

### H2 — Tópico Kafka residual entre testes da mesma collection

Os testes Kafka (S3, S4-Kafka) compartilham a `OutboxCapabilityFixture` —
o tópico `edital_events` acumula mensagens entre execuções. Inicialmente,
o S3 falhou com `Expected ... to be "007/2026", but "080/2026" differs`
porque o consumer leu uma mensagem residual de S8.

**Solução:** consumer externo agora itera com `EsperarMensagemEspecificaAsync`,
filtrando por `NumeroEdital` esperado. Padrão também adotado pelo S5.

### H3 — Filas Wolverine com hyphen viram underscore

`PostgresqlDeadLetterSpikeQueue = "spike-deadletter"` virou
`postgresql://spike_deadletter/` no listener (visto no log). Convenção
para todas as filas/tópicos do UniPlus: usar **snake_case** desde a
declaração para evitar surpresas.

### H4 — Replay via `IDeadLetters` é a API canônica

`Wolverine.Persistence.Durability.IDeadLetters` expõe:

- `DeadLetterEnvelopeByIdAsync(Guid, string? tenantId)`
- `SummarizeAllAsync(string serviceName, TimeRange, CancellationToken)`
- `MarkDeadLetterEnvelopesAsReplayableAsync(string exceptionType)`
- `MarkDeadLetterEnvelopesAsReplayableAsync(Guid[] ids)` (polyfill v2/v3)

API estável o suficiente para construir um endpoint admin no UniPlus se
necessário (`POST /admin/outbox/replay?type=...`). Decisão para depois do
ADR final.

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
Aprovado S6/V7 — restart: novo host processa mensagem pendente
Aprovado S7 (variante A) — EF retry ON levanta conflito esperado
Aprovado S7 (variante B) — EF retry OFF é a recomendação aplicada
Aprovado S8 — schema 'wolverine' contém todas as tabelas esperadas
Aprovado S8 — superfície completa observada documentada no test output
Aprovado S9 — handler que sempre falha gera entrada em wolverine_dead_letters
Aprovado S9 — query SQL de dead letters retorna snapshot inspecionável

Total de testes: 12
     Aprovados: 12
Tempo total: 47 s
```

## Próximo passo

**FIM DA MATRIZ S0–S9**. Próximas ações:

1. Relatório consolidado da Story #158 (junção das saídas dos 7 relatórios).
2. ADR formal de adoção do outbox Wolverine.
3. Implementação produtiva no `Selecao.API` e `Ingresso.API` baseada nas
   decisões registradas.
4. Issue separada: gerar pacote `WolverineFx.*` 5.32.2+ oficial e remover
   feed local quando upstream publicar.

## Versões e ambiente

- `WolverineFx.*` 5.32.1-pr2586 do feed local.
- `Testcontainers.PostgreSql` 4.11.0; `Testcontainers.Kafka` 4.11.0.
- Postgres image: `postgres:18-alpine`.
- Kafka image: `confluentinc/cp-kafka:7.6.1`.
- Runtime: .NET 10 / C# 14.
- Conta gh ativa: `marmota-alpina`.

## Referências

- [Plano de validação do outbox Wolverine (#158)](158-plano-validacao-outbox-wolverine.md)
- [Relatório S0](158-s0-relatorio.md), [S2](158-s2-relatorio.md), [S3](158-s3-relatorio.md), [S5](158-s5-relatorio.md), [S6](158-s6-relatorio.md), [S7](158-s7-relatorio.md), [S8](158-s8-relatorio.md)
- [Wolverine docs — Error handling](https://wolverinefx.net/guide/handlers/error-handling.html)
- [Wolverine docs — Dead letter queue](https://wolverinefx.net/guide/durability/dead-letter-storage.html)
