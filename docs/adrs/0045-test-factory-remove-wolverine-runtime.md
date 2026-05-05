---
status: "accepted"
date: "2026-05-05"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0045: Test factory remove `WolverineRuntime` de `IHostedService` para suítes não-outbox

## Contexto e enunciado do problema

A maioria das suítes de integração (`AuthEndpointsTests`, `OpenApiEndpointTests`, `EditalEndpointTests` etc.) testa o pipeline HTTP sem exercitar mensageria — não precisa de PostgreSQL para outbox nem Kafka para domain events.

Iniciar o Wolverine em test host implicitamente dispara `MigrateAsync` contra o PG configurado em `PersistMessagesWithPostgresql`. Em ambiente de teste sem PG real (fixtures HTTP-only), isso vira timeout de 30+ segundos por suite, mascarando o erro real (não há PG) atrás de mensagens genéricas de connection.

A pergunta: como impedir o Wolverine de iniciar em test hosts que não precisam dele?

## Drivers da decisão

- **Test ergonomics**: timeout de 30s por suite torna iteração local insuportável.
- **Divergência produção/teste**: a remoção precisa ser cirúrgica — só `WolverineRuntime` (o IHostedService que startup), não os serviços que ele registra (`ICommandBus`, `IQueryBus`).
- **Heurística estável**: o `WolverineRuntime` é registrado via factory (`ImplementationFactory != null`, `ImplementationType == null`), o que torna inspeção por tipo concreto inviável.

## Opções consideradas

- **A. `DisableWolverineRuntimeForTests = true` por default; suites de outbox sobrescrevem para `false`.**
- **B. Sem default — cada test factory precisa decidir.**
- **C. Wolverine "test mode" oficial via API do JasperFx.**

## Resultado da decisão

**Escolhida:** "A — `DisableWolverineRuntimeForTests = true` por default em `ApiFactoryBase<T>`".

`tests/Unifesspa.UniPlus.IntegrationTests.Fixtures/Hosting/ApiFactoryBase.cs` expõe a propriedade `protected virtual bool DisableWolverineRuntimeForTests => true`. No `ConfigureWebHost`, quando `true`, remove descriptors via:

```csharp
ServiceDescriptor[] hostedToRemove = [.. services
    .Where(d => d.ServiceType == typeof(IHostedService)
        && d.ImplementationFactory is not null
        && d.ImplementationFactory.Method.DeclaringType?.Assembly.GetName().Name == "Wolverine")];
foreach (ServiceDescriptor svc in hostedToRemove)
    services.Remove(svc);
```

A heurística casa pelo assembly da factory (`Wolverine`), não pelo tipo concreto. Suites cascading (`CascadingApiFactory`) sobrescrevem para `false` e provisionam PG efêmero por fixture.

A opção C (Wolverine test mode oficial) seria preferível mas o JasperFx não expõe API pública para isso na versão 5.32.1. Quando expuser, este ADR é gatilho para migrar.

## Consequências

### Positivas

- Suites HTTP-only iniciam em segundos, não em 30+.
- Override sopra simples (`DisableWolverineRuntimeForTests = false`) para suites que precisam.
- Sentinela `WolverineRuntimeRemovalSentinelTests` (issue #194) protege contra refactor interno do JasperFx que quebre a heurística.

### Negativas

- Heurística depende do nome do assembly Wolverine. Se Wolverine renomear (ex.: `JasperFx.Wolverine`), a heurística vira no-op silencioso. Mitigação: sentinela CI falha cedo se isso acontecer.
- Suites HTTP-only que injetam `ICommandBus` e tentam enviar comandos vão funcionar até o ponto do envelope — mas nada o consome porque o runtime não roda. Aceitável: testes HTTP-only não devem despachar comandos; se precisarem, sobrescrever a flag.

### Neutras

- A propriedade `DisableWolverineRuntimeForTests` está documentada com `<remarks>` em 4 parágrafos (PR #328, issue #206) explicando porque, quando, heurística e sentinela.

## Confirmação

- `ApiFactoryBase<T>.DisableWolverineRuntimeForTests` em `tests/Unifesspa.UniPlus.IntegrationTests.Fixtures/Hosting/ApiFactoryBase.cs`.
- `CascadingApiFactory` sobrescreve para `false` (suite cascading).
- `WolverineRuntimeRemovalSentinelTests` em `tests/Unifesspa.UniPlus.Selecao.IntegrationTests/Hosting/` espelha a query da heurística.

## Prós e contras das opções

### A — Default `true` + override (escolhida)

- Bom: 90% dos casos via default; opt-out cirúrgico.
- Ruim: heurística depende de string `"Wolverine"`.

### B — Sem default

- Bom: explicitude.
- Ruim: cada test factory repete configuração; esquecer = timeout 30s sem causa óbvia.

### C — Test mode oficial

- Bom: API estável do framework.
- Ruim: não existe na versão 5.32.1.

## Mais informações

- ADR-0040 — Helper `UseWolverineOutboxCascading`
- Issue [#194](https://github.com/unifesspa-edu-br/uniplus-api/issues/194) — sentinela da heurística (PR [#325](https://github.com/unifesspa-edu-br/uniplus-api/pull/325))
- Issue [#206](https://github.com/unifesspa-edu-br/uniplus-api/issues/206) — docstring expandida (PR [#328](https://github.com/unifesspa-edu-br/uniplus-api/pull/328))
- Origem: PR [#172](https://github.com/unifesspa-edu-br/uniplus-api/pull/172); issue [#187](https://github.com/unifesspa-edu-br/uniplus-api/issues/187)
