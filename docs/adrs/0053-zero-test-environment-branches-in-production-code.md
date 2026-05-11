---
status: "accepted"
date: "2026-05-11"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "Council multi-advisor 2026-05-11 (architect-advisor, pragmatic-engineer, devils-advocate)"
informed: []
---

# ADR-0053: Zero ramos de ambiente de teste em código de produção — fitness test enforça `IsEnvironment(literal)` e `EnvironmentName == literal` banidos em `src/`

## Contexto e enunciado do problema

A documentação oficial do `Microsoft.AspNetCore.Mvc.Testing` sugere `WebApplicationFactory<Program>.UseEnvironment("Testing")` pareado com `if (env.IsEnvironment("Testing")) { … }` no `Program.cs` para trocar dependências durante testes de integração. O padrão é conveniente para tutoriais mas introduz três problemas estruturais quando aplicado a uma base Clean Architecture como `uniplus-api`:

1. **String mágica como controle de fluxo** — `"Testing"` é convenção, não contrato. Um typo (`"Testng"`) compila, desliga silenciosamente o override e o teste roda contra o wiring de produção. O sistema de tipos não protege.
2. **Produção conhecendo cenários de teste** — `Program.cs` passa a interrogar "estou sendo testado?", invertendo a direção de dependência que Clean Architecture impõe. A camada Frameworks (Hosting) vira *aware* de Test Concerns.
3. **Dois codepaths a manter** — cada ramo `if env.IsEnvironment("…")` cria uma "versão de teste" do comportamento que drifta de produção. Bugs se escondem no lado que o binário deployado não exercita.

A base `uniplus-api` já adotou o padrão alternativo desde a Story #28 e o consolidou com a #117/#116: toda customização de teste vive em `tests/Unifesspa.UniPlus.IntegrationTests.Fixtures/Hosting/ApiFactoryBase.cs` e subclasses. A fixture:

- Remove `WolverineRuntime` e `MigrationHostedService<T>` da pipeline de `IHostedService`
- Filtra health checks de infra externa (`postgres`, `redis`, `minio`, `kafka`)
- Plugga `TestAuthHandler` como auth scheme via `ConfigureTestServices`
- Aceita overrides de configuração via `GetConfigurationOverrides()` abstract method per-factory

A auditoria realizada em 2026-05-11 (compozy task `production-test-isolation`) confirmou que `src/` tem **zero** chamadas a `IsEnvironment(...)` hoje. Existe exatamente um match equivalente: `src/selecao/Unifesspa.UniPlus.Selecao.API/Program.cs:121-122` compara `EnvironmentName == "Test"` por string literal para decidir se a falta de Schema Registry no Kafka vira `LogWarning` ou `InvalidOperationException`. A regra binding desta ADR força refator desse débito.

Há ainda uma postura institucional adjacente que esta ADR consolida: **ambientes HML/sanidade são semanticamente idênticos a Production**. O binário deployado é o mesmo; o que muda entre tiers é exclusivamente a configuração injetada pelo Vault (connection strings, OIDC client IDs, sampling ratios, segredos). Não existe motivo legítimo para `IsEnvironment("Hml")`, `IsEnvironment("Sanidade")` ou `IsEnvironment("Staging")` aparecerem em código de produção; permiti-los convida drift entre tiers.

A ADR-0012 já estabeleceu ArchUnitNET como biblioteca oficial de fitness tests do projeto, e o padrão `DominioNaoUsaGuidNewGuidTests.cs` (em `tests/Unifesspa.UniPlus.ArchTests/SolutionRules/`) demonstra que regex textual sobre `src/` é viável para casos onde ArchUnitNET não enxerga (string literais, comparações via `==`). Os dois mecanismos serão combinados.

## Drivers da decisão

- **Disciplina arquitetural mecânica > convenção humana.** Convenções sem gate automático driftam. Um fitness test em CI é o que torna a regra real.
- **Cobertura de ambas as síntaxes do antipattern.** Banir só `IsEnvironment(literal)` deixaria buraco gigante: a violação que existe hoje (`Program.cs:121`) é via `EnvironmentName == "Test"`, não via `IsEnvironment`. O gate precisa cobrir as duas.
- **Distinguir composition root legítimo de domínio.** `IsDevelopment()` para HSTS/Swagger UI/dev-only validation guards é prática idiomática .NET — não pode ser banido. A linha está entre "infraestrutura do composition root" (permitido) e "decisão de comportamento/domínio" (banido).
- **Aderência a Clean Architecture e 12-factor.** A Camada Frameworks (Hosting) recebe configuração de fora; jamais decide comportamento por interrogar o ambiente em que roda. Factor III (Config) impõe externalização — `if env.IsEnvironment("X")` é exatamente o que ele proíbe.
- **Compatibilidade com posturas de deploy do CTIC.** HML/sanidade/Prod = mesmo binário com Vault diferente. Esta ADR formaliza essa premissa para evitar futuras introduções de `IsEnvironment("Hml")` por novos contribuidores.

## Opções consideradas

- **A. ADR + fitness test combinado (ArchUnitNET + regex textual)** — formaliza a regra E enforça em CI.
- **B. Apenas ADR sem fitness test** — documenta convenção, depende de revisão humana.
- **C. `BannedApiAnalyzers` (Roslyn) em vez de ArchUnitNET** — feedback in-IDE compile-time.
- **D. Whitelist de strings permitidas em `IsEnvironment(...)`** — permitir `"Development"`/`"Production"`, banir custom.
- **E. `Microsoft.FeatureManagement` adoption** — substitui qualquer flag ambient-dependent.

## Resultado da decisão

**Opção A — ADR + fitness test combinado (ArchUnitNET + regex textual)**.

A regra binding institui:

### Banido em `src/`

- `IHostEnvironment.IsEnvironment(string)` com **qualquer** argumento literal (não apenas `"Testing"`). O bug class é "produção interroga ambiente para decidir comportamento", independente do valor específico.
- Comparação direta `env.EnvironmentName == "..."` ou `EnvironmentName.Equals("...")` com literal.
- Qualquer `if`/`switch`/ternário cuja condição contenha o nome do ambiente como literal.

### Permitido em `src/`

- `env.IsDevelopment()` em composition root e adapters: `Program.cs`, `Infrastructure.Core/DependencyInjection/*.cs`, `Infrastructure.Core/Cors/*.cs`, `Infrastructure.Core/Authentication/*.cs`, `Infrastructure.Core/Observability/*.cs`. Casos legítimos hoje: validação de obrigatoriedade de config (`Storage:Endpoint`, `Redis:ConnectionString`, `Cors:AllowedOrigins`, HTTPS no `Auth:Authority`, sampler OTel). Domain e Application **não** chamam `IsDevelopment()` — fitness test enforça via dependência cruzada.
- `env.IsProduction()` quando a afirmativa for mais natural que a negação.
- `env.EnvironmentName` como valor **read-only** passado para campos de log estruturado ou OTel resource attributes (`deployment.environment`). Sem `if` sobre o valor.

### Customização de teste

Toda manipulação de DI, configuração ou auth para testes vive em `tests/Unifesspa.UniPlus.IntegrationTests.Fixtures/Hosting/ApiFactoryBase.cs` e suas subclasses. A fixture é o **port canônico** entre código de produção e cenários de teste:

- `services.Remove<X>` para serviços indesejados no host de teste
- `services.Replace<X>` ou `ConfigureTestServices(...)` para swaps
- `ConfigureAppConfiguration(...)` com `AddInMemoryCollection(GetConfigurationOverrides())`
- Virtual extension points (`DisableWolverineRuntimeForTests`, `InfraHealthCheckNamesToRemoveForTests`, `ConfigureTestAuthentication`) para especialização per-suite

### Colapso semântico de ambientes deployados

HML, sanidade, staging e Production compartilham o mesmo binário com `ASPNETCORE_ENVIRONMENT=Production`. Diferenças entre tiers vêm exclusivamente da configuração injetada pelo Vault. Esta ADR documenta a premissa para vetar futuras introduções de `IsEnvironment("Hml")` por novos contribuidores.

### Enforcement

Fitness test em `tests/Unifesspa.UniPlus.ArchTests/SolutionRules/SemBranchingPorAmbienteEmProducaoTests.cs` faz scan textual com state-machine de comments + pré-processamento que strip strings literais (para evitar que sequências como `"*/*"` em atributos MIME ou `"// foo"` em mensagens de erro confundam o detector de comments).

Os 7 patterns banidos cobrem as síntaxes equivalentes do antipattern:

- `IsEnvironment\s*\(\s*"[^"]+"\s*\)` — chamada da API canônica com literal
- `EnvironmentName\s*==\s*"[^"]+"` — comparação `==` literal à direita
- `"[^"]+"\s*==\s*\bEnvironmentName\b` — comparação `==` literal à esquerda
- `EnvironmentName\.Equals\s*\(\s*"[^"]+"` — chamada `.Equals(literal)`
- `string\.Equals\s*\(\s*EnvironmentName\s*,\s*"[^"]+"` — `string.Equals(EnvironmentName, literal, ...)`
- `EnvironmentName\s+is\s+"[^"]+"` — pattern matching C# 8+
- `switch\s*\([^)]*\bEnvironmentName\b` — switch expression/statement com cases literais

A abordagem é puramente textual (regex). ArchUnitNET (lib oficial de fitness tests do projeto, ADR-0012) opera por **tipo**, não por chamada de método com argumento literal — `IsEnvironment(string)` e `string.Equals(string, string)` são chamadas resolvidas via overload, não tipos importáveis. O scanner textual mira exatamente esse buraco que ArchUnitNET não enxerga.

Glob exclui: `obj/`, `bin/`, `*.g.cs`, `*.Designer.cs`. State-machine de comments rastreia `/* ... */` multi-linha + filtro de linhas iniciadas por `//`. Strings literais são neutralizadas antes do scanner.

### Refator de débito existente

`src/selecao/Unifesspa.UniPlus.Selecao.API/Program.cs:121-122` (`EnvironmentName == "Test"` no guard de Schema Registry) é refatorado nesta mesma entrega para `IsDevelopment()`, que é semanticamente equivalente para o caso ("local sem PG/Kafka real") e satisfaz o gate. Config flag `Kafka:SchemaRegistry:Required` ficou descartada por adicionar superfície de configuração sem ganho — a decisão binária dev/prod é exatamente o que `IsDevelopment()` expressa.

## Consequências

### Positivas

- Código de produção é **indiferente ao ambiente para comportamento**. Binário deployado é único entre tiers; diferenças são exclusivamente configuracionais.
- Customização de teste é **explícita e descobrível** — qualquer dev lendo a fixture vê todo o delta em relação à produção em C# tipado, linha por linha.
- Prevenção de drift é mecânica — fitness test roda em todo PR; violação falha CI em <1 min.
- Sem string mágica em paths de produção — typos não passam silenciosos.
- Boundary Clean Architecture preservada — Camada Frameworks (Hosting) não inverte dependência para conhecer Test concerns.
- Postura HML=Prod fica documentada e enforced — futuros contribuidores sabem que `IsEnvironment("Hml")` é antipattern, não esquecimento.

### Negativas

- `Program.cs:121-122` precisa ser refatorado no mesmo PR — débito real existente. Aceito.
- Onboarding requer leitura desta ADR + `Middleware/README.md` (issue #116) + `ApiFactoryBase` para entender o pattern alternativo. Mitigado por cross-link no `CONTRIBUTING.md`.
- Allowlist implícita (regex não match em `IsDevelopment()`/`IsProduction()`) pode parecer permissiva demais para puristas. Aceito — explícito por path seria rigidez sem ROI; a comunidade .NET unanimemente aceita `IsDevelopment()` no composition root.

### Neutras

- Custo de manutenção do fitness test é próximo de zero após criado — só falha quando alguém viola a regra.
- O scan textual depende do `SolutionRootLocator` já estabelecido — sem nova infraestrutura.
- `BannedApiAnalyzers` fica como defense-in-depth opcional (Phase 4 da ADR) — não impacta V1.

## Confirmação

Fitness test `SemBranchingPorAmbienteEmProducaoTests` em `tests/Unifesspa.UniPlus.ArchTests/SolutionRules/` roda em todo `dotnet test` da solution. Pipeline CI já invoca `dotnet test` no job `Unit + arch tests`. Violação falha o build do PR.

Métricas de acompanhamento:

- `IsEnvironment(literal)` matches em `src/` — alvo: 0 (validado pelo teste)
- `EnvironmentName == literal` matches em `src/` — alvo: 0
- Tempo até detecção de violação em PR — alvo: <5 min (job CI)
- Recidiva pós-merge — alvo: 0 em 12 meses; re-avaliar regra caso recidiva real ocorra

## Prós e contras das opções

### A. ADR + fitness test combinado (ArchUnitNET + regex textual)

- Bom, porque torna a regra mecânica e cobre ambas as síntaxes do antipattern
- Bom, porque reusa infraestrutura já estabelecida (`SolutionRootLocator`, ADR-0012 ArchUnitNET, padrão `DominioNaoUsaGuidNewGuidTests`)
- Bom, porque custo de manutenção pós-criação é próximo de zero
- Ruim, porque exige ~80 LOC de teste novo + refator do débito existente

### B. Apenas ADR sem fitness test

- Bom, porque baixíssimo custo de implementação
- Ruim, porque convenção sem gate drifta (devils-advocate destacou na council)
- Ruim, porque ADR isolada vira "boa intenção" sem força operacional
- Ruim, porque nenhum onboarding/code review captura 100% das violações

### C. `BannedApiAnalyzers` (Roslyn) em vez de ArchUnitNET

- Bom, porque feedback in-IDE compile-time (squiggle vermelho)
- Bom, porque mensagem custom orienta o dev para `ApiFactoryBase`
- Ruim, porque pega apenas chamadas de método simbólicas — `EnvironmentName == "..."` (que é a violação real hoje) não é captada
- Ruim, porque exige adoção de NuGet package adicional fora do ecossistema de fitness tests já estabelecido (ADR-0012)
- Ruim, porque produz dois sistemas paralelos de regras arquiteturais (analyzer + fitness test)

### D. Whitelist de strings permitidas em `IsEnvironment(...)`

- Bom, porque flexibilidade percebida — devs ainda podem usar a API "oficial" do framework
- Ruim, porque permite o antipattern at runtime; uma vez que `IsEnvironment("Development")` é OK, a próxima "exceção" cresce
- Ruim, porque `IsDevelopment()`/`IsProduction()` já são equivalentes type-safe — não há ganho real

### E. `Microsoft.FeatureManagement` adoption

- Bom, porque industry-standard para flags funcionais (Andrew Lock, Milan Jovanović 2024-2026)
- Bom, porque desacopla flag de ambiente; runtime evaluation com filtros (Time, Percentage, Targeting)
- Ruim, porque overkill: 100% dos usos legítimos hoje são decisões de composition root (HSTS, Swagger, validation guards), não feature gates
- Ruim, porque adiciona NuGet package + camada de persistência de estado de flag + runtime evaluation cost para zero ganho de cenários reais
- **Re-avaliar** como follow-up quando real feature-gate need aparecer

## Mais informações

- ADR-0012 — ArchUnitNET como biblioteca oficial de fitness tests (estabelece a ferramenta)
- ADR-0033 — `IUserContext` como abstração canônica (sibling estrutural — ports keep production code unaware)
- ADR-0050 — Registry GHCR e tagging (HML/Prod semantic collapse rationale)
- [Microsoft Learn — Integration tests in ASP.NET Core](https://learn.microsoft.com/aspnet/core/test/integration-tests)
- [Andrew Lock — Supporting integration tests with WebApplicationFactory in .NET 6](https://andrewlock.net/exploring-dotnet-6-part-6-supporting-integration-tests-with-webapplicationfactory-in-dotnet-6/)
- [12-Factor App — Config (Factor III)](https://12factor.net/config)
- [InfoQ — Fitness Functions for Your Architecture (Neal Ford)](https://www.infoq.com/articles/fitness-functions-architecture/)
- **Origem:** sessão `/cy-idea-factory` 2026-05-11 — idea spec em `.compozy/tasks/production-test-isolation/_idea.md`, ADR draft em `.compozy/tasks/production-test-isolation/adrs/adr-001.md`
- Issue rastreadora: #414
- PR #413 (issue #116) — fixture pattern `ApiFactoryBase` já materializado
