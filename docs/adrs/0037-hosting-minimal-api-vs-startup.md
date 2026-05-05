---
status: "accepted"
date: "2026-05-05"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0037: Hosting via `WebApplication.CreateBuilder` (minimal hosting) mantido vs migração para Generic Host + `Startup.cs`

## Contexto e enunciado do problema

Os módulos `Selecao.API` e `Ingresso.API` foram bootstrapped no padrão moderno de hosting do ASP.NET Core 10: `WebApplication.CreateBuilder(args)` + configuração inline em `Program.cs` (top-level statements), sem classe `Startup`. Esse modelo é o default desde .NET 6 e a recomendação ativa do time .NET.

Durante a spike S10 cascading, descobrimos um gap conhecido em [`dotnet/aspnetcore#37680`](https://github.com/dotnet/aspnetcore/issues/37680): overrides de configuração feitos via `IWebHostBuilder.ConfigureAppConfiguration(...)` em `WebApplicationFactory<TEntryPoint>.ConfigureWebHost` **não propagam** para o `WebApplicationBuilder.Configuration` em apps minimal hosting. O test host precisa injetar overrides via env vars (`ConnectionStrings__SelecaoDb`, `Kafka__BootstrapServers`) ou via `ConfigureAppConfiguration` no Generic Host (não adoptado aqui).

A pergunta: o gap justifica migrar para Generic Host + classe `Startup` (modelo legado, que aceita o override via `IWebHostBuilder` corretamente)?

## Drivers da decisão

- **Gap concreto** com workaround custo-baixo: env vars + helper que lê configuração no callback do `UseWolverine` resolvem 100% do test host.
- **Custo de migrar é alto**: dois `Program.cs` + dois `Startup.cs` + reescrever DI + revisitar lifecycle de hosted services + retreinar contribuidores em modelo legado.
- **Direção do .NET**: Generic Host + Startup é considerado legado pelo time ASP.NET. Migrar contra a maré significa carregar dívida arquitetural por anos.
- **Test surface**: o workaround (env vars + `DisableParallelization` na collection) tem 1 linha de comentário inline e está coberto por sentinela em CI (issue #197 + #205).

## Opções consideradas

- **A. Migrar para Generic Host + `Startup.cs`.**
- **B. Manter `WebApplication.CreateBuilder` + workaround env vars no test host.**
- **C. Híbrido: produção em minimal hosting, test fixture com Generic Host customizado.**

## Resultado da decisão

**Escolhida:** "B — Manter minimal hosting", porque o custo de A é desproporcional ao gap real, e C cria divergência produção-vs-teste que mascararia bugs específicos de hosting.

O gap é um custo arquitetural conhecido e contido — não vaza para domínio nem application. Vive em duas linhas (dois `Program.cs`) + um helper (`UseWolverineOutboxCascading`). O workaround foi documentado nas fixtures, validado por sentinela (issue #197), e o overhead de "lembrar" é amortizado pelo número de contribuidores que vão tocar em test host (raros).

## Consequências

### Positivas

- Mantém-se o pattern moderno alinhado com a direção do .NET.
- Não há migração custosa, sem janela de instabilidade.
- Contribuidores leem `Program.cs` linear, sem indireção via classe.

### Negativas

- Test fixtures precisam usar env vars para injetar overrides de configuração — comportamento "diferente" do Generic Host. Documentado em comentários e em ADR para evitar que novos contribuidores tentem `ConfigureAppConfiguration` e percam tempo até descobrir o gap.
- Dependência de [`dotnet/aspnetcore#37680`](https://github.com/dotnet/aspnetcore/issues/37680) ser resolvido upstream, ou de o workaround continuar válido. Se Microsoft mudar o comportamento e env vars deixarem de funcionar, este ADR vira gatilho para reavaliação.

### Neutras

- O helper `UseWolverineOutboxCascading` que lê configuração no callback é simétrico ao pattern do próprio Wolverine — não é uma adaptação especial só para teste.

## Confirmação

- `Program.cs` de Selecao e Ingresso são top-level statements + `WebApplication.CreateBuilder`.
- `CascadingFixture` injeta overrides via env vars; `CascadingFixtureConfigurationTests` (issue #197) sentinela a chegada efetiva no `IConfiguration` runtime.
- `CascadingCollection` mantém `DisableParallelization = true` (issue #179) para garantir que env vars não sejam mutadas concorrentemente entre testes do mesmo processo.

## Prós e contras das opções

### A — Migrar para Generic Host + Startup.cs

- Bom: `IWebHostBuilder.ConfigureAppConfiguration` no test host funciona "out of the box".
- Ruim: contradiz a direção do .NET; custo alto de migração; perde top-level statements.

### B — Manter minimal hosting (escolhida)

- Bom: pattern moderno; custo de migração zero.
- Ruim: workaround env vars nos test fixtures.

### C — Híbrido (produção minimal, teste Generic Host)

- Bom: resolve o gap em teste sem mudar produção.
- Ruim: divergência produção/teste; fixture custom complexa; mascara bugs específicos de minimal hosting.

## Mais informações

- [Microsoft Learn — App startup (.NET 10)](https://learn.microsoft.com/aspnet/core/fundamentals/host/web-host?view=aspnetcore-10.0)
- [`dotnet/aspnetcore#37680`](https://github.com/dotnet/aspnetcore/issues/37680) — `WebApplicationFactory.ConfigureWebHost` não propaga
- ADR-0038 — Override de configuração em testes via env vars
- Origem: spike S10 cascading; issue [#178](https://github.com/unifesspa-edu-br/uniplus-api/issues/178); PR [#172](https://github.com/unifesspa-edu-br/uniplus-api/pull/172)
