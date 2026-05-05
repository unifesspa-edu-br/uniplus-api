---
status: "accepted"
date: "2026-05-05"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0038: Override de configuração em testes integrados via env vars + `DisableParallelization` na collection

## Contexto e enunciado do problema

Test fixtures de integração que startam o `WebApplicationFactory<Program>` produtivo precisam injetar valores específicos no `IConfiguration` runtime (connection string do testcontainer Postgres, `Kafka:BootstrapServers` em whitespace para desligar transporte). O caminho idiomático seria `IWebHostBuilder.ConfigureAppConfiguration` em `WebApplicationFactory.ConfigureWebHost`. Esse caminho não funciona para apps minimal hosting (`WebApplication.CreateBuilder`) — gap conhecido em [`dotnet/aspnetcore#37680`](https://github.com/dotnet/aspnetcore/issues/37680) (ver ADR-0037 para a decisão de não migrar).

A pergunta: qual mecanismo de override usar?

## Drivers da decisão

- **Aderência ao runtime real**: o override precisa chegar em `WebApplicationBuilder.Configuration` exatamente como em produção, não em um wrapper de teste.
- **Cross-suite isolation**: env vars são por processo, não por suite. Se uma suite seta uma env var e outra suite paralela espera o appsettings default, há interleave.
- **Compat cross-runtime**: `Environment.SetEnvironmentVariable(name, string.Empty)` em runtimes < .NET 9 apaga a variável (em vez de definir como vazia), regredindo para o appsettings.

## Opções consideradas

- **A. `Environment.SetEnvironmentVariable` na fixture + `DisableParallelization=true` na collection.**
- **B. Reflection sobre `WebApplicationBuilder.Configuration` para injetar `InMemoryCollection`.**
- **C. Custom `WebApplicationFactory` que substitui `IConfiguration` inteiro.**

## Resultado da decisão

**Escolhida:** "A — env vars + `DisableParallelization=true` na collection que precisa do override".

Env vars são lidas pelo `WebApplicationBuilder` na construção (via `EnvironmentVariablesConfigurationProvider`) sem precisar de nenhum hook customizado. O custo é o cuidado com cross-suite — mitigado por aplicar `[CollectionDefinition(DisableParallelization = true)]` apenas na collection que de fato seta env vars (`CascadingCollection`). Outras collections continuam paralelizando normalmente.

Para o caso `Kafka:BootstrapServers`, o helper produtivo desliga o transporte quando `IsNullOrWhiteSpace`. A fixture seta um espaço (whitespace) em vez de string vazia: em runtimes anteriores a .NET 9, `SetEnvironmentVariable(name, string.Empty)` apaga a variável (regredindo para o appsettings que tem `localhost:9092`); um espaço cobre os dois cenários sem regressão cross-runtime.

## Consequências

### Positivas

- Override chega em `IConfiguration` runtime via path padrão (sem reflection, sem wrapper).
- Protegido contra cross-suite interleave por `DisableParallelization` localizado.
- Comportamento idêntico cross-runtime via whitespace para "vazio".

### Negativas

- Suites na mesma collection não paralelizam — perda de tempo de CI quando há muitos facts. Mitigado: a `CascadingCollection` só agrupa testes que precisam do PG efêmero compartilhado (10+ facts hoje).
- Fixture precisa restaurar env vars previas em `DisposeAsync` para não vazar entre runs do mesmo processo (pytest-watch, dotnet watch test). Captura prévia + try/catch foi reforçada em PR #327 (issue #195).

### Neutras

- A decisão pode ser revertida sem custo se o gap upstream for resolvido — basta substituir `SetEnvironmentVariable` por `ConfigureAppConfiguration` na fixture.

## Confirmação

- `CascadingFixture.InitializeAsync` em `tests/Unifesspa.UniPlus.Selecao.IntegrationTests/Outbox/Cascading/` seta `ConnectionStrings__SelecaoDb` e `Kafka__BootstrapServers`.
- `CascadingCollection` aplica `[CollectionDefinition(DisableParallelization = true)]`.
- `CascadingFixtureConfigurationTests` (PR #327, issue #197) sentinela que a configuração efetiva chegou correta.
- Captura prévia + restore em catch implementados no PR #327 (issue #195).

## Prós e contras das opções

### A — env vars + DisableParallelization (escolhida)

- Bom: padrão idiomático para apps minimal hosting; sem reflection; restore deterministico.
- Ruim: `DisableParallelization` reduz throughput de testes paralelizáveis.

### B — Reflection sobre `WebApplicationBuilder.Configuration`

- Bom: não depende de env vars; cada fixture isolada.
- Ruim: reflection é frágil contra refactors do .NET; perde-se simetria com produção.

### C — Custom `WebApplicationFactory` que substitui `IConfiguration` inteiro

- Bom: escopo total; sem cross-suite leak.
- Ruim: divergência produção/teste alta; reescreve provider chain.

## Mais informações

- ADR-0037 — Hosting minimal API mantido
- [`dotnet/aspnetcore#37680`](https://github.com/dotnet/aspnetcore/issues/37680)
- Origem: spike S10 cascading commit `bf052ad`; issue [#179](https://github.com/unifesspa-edu-br/uniplus-api/issues/179); PR [#172](https://github.com/unifesspa-edu-br/uniplus-api/pull/172)
