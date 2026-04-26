# Relatório do Spike S7 — Outbox Wolverine (#158)

- **Branch:** `spikes/158-s7-retry-strategy`
- **Branch base:** `spikes/158-s6-restart-recovery` (`1a38d61`)
- **Data:** 2026-04-25
- **Status:** **AC5 do plano formalmente comprovado e documentado**. 8 testes verdes na matriz.
- **Plano de referência:** [`docs/spikes/158-plano-validacao-outbox-wolverine.md`](158-plano-validacao-outbox-wolverine.md)

## Resumo executivo

S7 transforma o achado já visto implicitamente em S2 ("não dá para usar
`EnableRetryOnFailure` com Wolverine `AutoApplyTransactions`") em **teste de
regressão explícito**:

- **Variante A** (EF retry ON + Wolverine AutoApplyTransactions): `bus.InvokeAsync`
  lança `InvalidOperationException : The configured execution strategy
  'NpgsqlRetryingExecutionStrategy' does not support user-initiated transactions`.
  **Esperado e comprovado.**
- **Variante B** (EF retry OFF + retry centralizado no Wolverine): mesma config
  de S2/S4/S3/S4-Kafka/S5/S6 — todos verdes. **Recomendação aplicada.**

A variante C do plano (wrap manual via `IExecutionStrategy.ExecuteAsync` em todo
handler) **não foi implementada** porque o próprio plano §S7 a marca como
"frágil" — usar a variante B é a recomendação. Mantida como achado se futura
refatoração precisar reverter.

## Decisão recomendada para produção

Consolidação para o ADR de adoção do outbox (a ser escrito após validação completa):

1. **`AddSelecaoInfrastructure(...)` em `Selecao.Infrastructure/DependencyInjection.cs`**
   precisa permitir desligar `EnableRetryOnFailure` quando o pipeline Wolverine
   estiver ativo. Opções:
   - Sobrecarga: `AddSelecaoInfrastructure(string conn, bool enableEfRetry = true)`.
     Migration de Selecao.API: `AddSelecaoInfrastructure(connStr, enableEfRetry: false)`.
   - Ou flag por config (`SelecaoDb:EnableRetryOnFailure: false` em
     `appsettings.Development.json` quando outbox estiver ativo).
   - Ou conditional na própria DI: se Wolverine outbox estiver registrado, pular
     `EnableRetryOnFailure`.

2. **`Ingresso.Infrastructure`** precisa do mesmo tratamento se for adotar o
   outbox transacional.

3. **Handlers que precisarem de retry específico** podem usar `Wolverine.Policies.OnException<T>().RetryTimes(...)` — política Wolverine, não EF.

4. **Operações longas que precisam de retry no DB** (não em handler Wolverine)
   podem manter EF retry — mas devem viver em DbContexts/scopes separados, fora
   do pipeline transacional do bus.

## Achados desta fase

### F1 — `EnableRetryOnFailure(maxRetryCount: 3)` é suficiente para reproduzir o conflito

Não precisa de `errorCodesToAdd` ou config customizada. Basta o retry strategy
estar registrado para o EF detectar transação user-initiated e bloquear.

### F2 — Mensagem do erro identifica o conflito

```
System.InvalidOperationException : The configured execution strategy
'NpgsqlRetryingExecutionStrategy' does not support user-initiated transactions.
Use the execution strategy returned by 'DbContext.Database.CreateExecutionStrategy()'
to execute all the operations in the transaction as a retriable unit.
```

A mensagem sugere `IExecutionStrategy.ExecuteAsync(...)` (variante C). Em
handlers Wolverine seria necessário envolver TODA chamada `SaveChanges` em
`strategy.ExecuteAsync(async () => { ... await db.SaveChangesAsync(); ... })`,
o que é **invasivo** e propenso a esquecimento.

### F3 — Reuso da fixture S6 viabiliza o S7 sem custo extra

O `OutboxRestartFixture` sobe PG + Kafka próprios e expõe `CriarFactory()`. S7
**não** usa o `CriarFactory()` — o teste cria sua própria
`ComEnableRetryOnFailureFactory` (private inner class) com config oposta.
Padrão útil: o spike de retry vive na mesma collection que o spike de restart,
mas com factory custom — não polui o ApiFactory padrão.

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

Total de testes: 8
     Aprovados: 8
Tempo total: 43 s
```

## Decisões pendentes da #158 (atualização)

| Caminho | Decisão |
|---|---|
| Caminho 1 — upgrade/fix Wolverine | **Resolvido** (5.32.1-pr2586 com fix do scraper) |
| Caminho 3 — retry EF vs retry Wolverine | **Resolvido** (variante B: EF retry OFF em DbContexts usados por handlers Wolverine; retry centralizado no Wolverine) |

Caminhos 2 (migration), 4 (plano B), e "transporte principal" continuam
abertos.

## Próximo passo recomendado

**S8 — Migration surface**: inspecionar quais tabelas o `AutoBuildMessageStorageOnStartup`
cria, comparar com `MapWolverineEnvelopeStorage(modelBuilder, "wolverine")`,
decidir entre migration EF, DbContext dedicado ou SQL versionado.

## Versões e ambiente

- `WolverineFx.*` 5.32.1-pr2586 do feed local.
- `Testcontainers.PostgreSql` 4.11.0; `Testcontainers.Kafka` 4.11.0.
- Postgres image: `postgres:18-alpine`.
- Runtime: .NET 10 / C# 14.
- Conta gh ativa: `marmota-alpina`.

## Referências

- [Plano de validação do outbox Wolverine (#158)](158-plano-validacao-outbox-wolverine.md)
- [Relatório S0](158-s0-relatorio.md), [S2](158-s2-relatorio.md), [S3](158-s3-relatorio.md), [S5](158-s5-relatorio.md), [S6](158-s6-relatorio.md)
