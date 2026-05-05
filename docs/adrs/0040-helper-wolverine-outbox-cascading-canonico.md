---
status: "accepted"
date: "2026-05-05"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0040: `WolverineOutboxConfiguration.UseWolverineOutboxCascading` como ponto canônico de configuração

## Contexto e enunciado do problema

Cada módulo (`Selecao.API`, `Ingresso.API`) precisa configurar o mesmo conjunto de invariantes Wolverine: persistência outbox em PostgreSQL no schema `wolverine`, atomicidade write+evento via `UseEntityFrameworkCoreTransactions`, durable outbox em todos os endpoints, middleware Command+Query, schema NÃO auto-criado em runtime (ADR-0039), Kafka opcional, roteamento específico do módulo via callback.

Sem helper, cada `Program.cs` repetiria 30+ linhas de configuração idênticas — e qualquer drift entre módulos vira bug oculto (um módulo persiste outbox, outro não; um habilita Kafka, outro não).

## Drivers da decisão

- **Invariantes compartilhadas**: persistência, atomicidade, durabilidade, middleware são iguais em todos os módulos.
- **Customização específica**: cada módulo tem seu próprio routing (`PublishMessage<EditalPublicadoEvent>`, futuros eventos do Ingresso).
- **Test surface**: a fixture cascading reusa o mesmo helper que produção — divergência produção/teste mascara bugs específicos de configuração.

## Opções consideradas

- **A. Configuração inline em cada `Program.cs`.**
- **B. Helper estático `UseWolverineOutboxCascading(this IHostBuilder, IConfiguration, string connectionStringName, ...)` em `Infrastructure.Core/Messaging/`.**
- **C. Builder fluente `new WolverineOutboxBuilder(...).WithKafka(...).WithRouting(...)`.**

## Resultado da decisão

**Escolhida:** "B — Helper estático no `Infrastructure.Core/Messaging/`", porque centraliza as invariantes em um único ponto sem inflar com builder pattern desnecessário. Cada módulo passa a connection string name + um callback `configureRouting` que recebe `WolverineOptions` para eventos específicos.

Assinatura:

```csharp
public static IHostBuilder UseWolverineOutboxCascading(
    this IHostBuilder host,
    IConfiguration configuration,
    string connectionStringName,
    string kafkaConfigKey = DefaultKafkaConfigKey,
    Action<WolverineOptions>? configureRouting = null)
```

Connection string e Kafka bootstrap são lidos lazy dentro do callback de `UseWolverine`, no startup do host — momento em que os providers de configuração já materializaram (env vars, appsettings). Esse padrão é compatível com o test fixture que injeta override via env var (ADR-0038).

## Consequências

### Positivas

- Drift entre módulos é estruturalmente impossível — todos passam pelo mesmo helper.
- Test fixture reusa o helper produtivo (`CascadingApiFactory` herda essa configuração via `Program.cs` real).
- Mudanças nas invariantes (ex.: novo middleware obrigatório, política de retry) propagam para todos os módulos via uma única edição.

### Negativas

- Helper precisa expor parâmetros suficientes para cobrir variações reais sem virar god-object. Mitigado por design: connection string name + Kafka key + routing callback são os três únicos eixos de variação.
- Adicionar nova invariante exige mudança no helper, que afeta todos os módulos. Aceitável — mudanças em invariantes deveriam mesmo passar por revisão cross-módulo.

### Neutras

- Helper hoje vive em `Infrastructure.Core` (compartilhado entre módulos). Decisão consciente de manter código compartilhado sob `shared/`.

## Confirmação

- `src/shared/Unifesspa.UniPlus.Infrastructure.Core/Messaging/WolverineOutboxConfiguration.cs` — implementação canônica.
- `Selecao.API/Program.cs` consome o helper com routing específico (`PublishMessage<EditalPublicadoEvent>`).
- `Ingresso.API/Program.cs` consome o helper sem routing por enquanto (não há eventos cross-módulo do Ingresso ainda).
- `WolverineRuntimeRemovalSentinelTests` (issue #194) usa o mesmo helper para validar invariantes do registro de IHostedService.

## Prós e contras das opções

### A — Configuração inline em cada Program.cs

- Bom: zero indireção, código local.
- Ruim: 30+ linhas duplicadas; drift entre módulos vira bug silencioso.

### B — Helper estático (escolhida)

- Bom: invariantes centralizadas; custom via callback.
- Ruim: helper precisa balancear flexibilidade vs simplicidade.

### C — Builder fluente

- Bom: descobrível via IntelliSense; chainable.
- Ruim: overengineering para 3 eixos de variação; test surface maior.

## Mais informações

- [JasperFx Wolverine — Outbox + EF Core](https://wolverinefx.io/guide/durability/efcore.html)
- ADR-0026 — Outbox transacional via Wolverine
- ADR-0038 — Override de configuração em testes
- ADR-0039 — Provisioning do schema via deploy
- ADR-0044 — Roteamento produtivo de domain events (per-módulo)
- Origem: PR [#172](https://github.com/unifesspa-edu-br/uniplus-api/pull/172); issue [#181](https://github.com/unifesspa-edu-br/uniplus-api/issues/181)
