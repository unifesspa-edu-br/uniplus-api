---
status: "accepted"
date: "2026-05-11"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "Council multi-advisor 2026-05-11 (architect-advisor, pragmatic-engineer, devils-advocate)"
informed: []
---

# ADR-0053: Zero ramos de ambiente de teste em código de produção

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

## Drivers da decisão

- **Clean Architecture e 12-factor.** A Camada Frameworks (Hosting) recebe configuração de fora; jamais decide comportamento por interrogar o ambiente em que roda. Factor III (Config) impõe externalização — `if env.IsEnvironment("X")` é exatamente o que ele proíbe.
- **Cobertura de ambas as síntaxes do antipattern.** Banir só `IsEnvironment(literal)` deixaria buraco gigante: a violação que existe hoje (`Program.cs:121`) é via `EnvironmentName == "Test"`, não via `IsEnvironment`. A regra precisa cobrir as duas.
- **Distinguir composition root legítimo de domínio.** `IsDevelopment()` para HSTS/Swagger UI/dev-only validation guards é prática idiomática .NET — não pode ser banido. A linha está entre "infraestrutura do composition root" (permitido) e "decisão de comportamento/domínio" (banido).
- **Compatibilidade com posturas de deploy do CTIC.** HML/sanidade/Prod = mesmo binário com Vault diferente. Esta ADR formaliza essa premissa para evitar futuras introduções de `IsEnvironment("Hml")` por novos contribuidores.
- **Custo proporcional ao risco real.** `src/` está limpo após o refator desta entrega; recidivas dependeriam de um novo contribuidor escrevendo deliberadamente o antipattern. Code review humano + uma ADR clara + um codebase sem precedente são gates suficientes na fase atual. Enforcement automático (Roslyn analyzer) é o upgrade natural se uma recidiva real ocorrer.

## Opções consideradas

- **A. ADR + fitness test combinado (ArchUnitNET + regex textual)** — formaliza a regra E enforça em CI via scan de regex.
- **B. ADR normativa sem enforcement automático** — documenta regra binding; gate é code review + ausência de precedente no codebase.
- **C. `BannedApiAnalyzers` (Roslyn) em vez de ArchUnitNET** — feedback in-IDE compile-time.
- **D. Whitelist de strings permitidas em `IsEnvironment(...)`** — permitir `"Development"`/`"Production"`, banir custom.
- **E. `Microsoft.FeatureManagement` adoption** — substitui qualquer flag ambient-dependent.

## Resultado da decisão

**Opção B — ADR normativa sem enforcement automático**.

A regra binding institui:

### Banido em `src/`

- `IHostEnvironment.IsEnvironment(string)` com **qualquer** argumento literal (não apenas `"Testing"`). O bug class é "produção interroga ambiente para decidir comportamento", independente do valor específico.
- Comparação direta `env.EnvironmentName == "..."` ou `EnvironmentName.Equals("...")` com literal.
- Qualquer `if`/`switch`/ternário cuja condição contenha o nome do ambiente como literal.

### Permitido em `src/`

- `env.IsDevelopment()` em composition root e adapters: `Program.cs`, `Infrastructure.Core/DependencyInjection/*.cs`, `Infrastructure.Core/Cors/*.cs`, `Infrastructure.Core/Authentication/*.cs`, `Infrastructure.Core/Observability/*.cs`. Casos legítimos hoje: validação de obrigatoriedade de config (`Storage:Endpoint`, `Redis:ConnectionString`, `Cors:AllowedOrigins`, HTTPS no `Auth:Authority`, sampler OTel). Domain e Application **não** chamam `IsDevelopment()`.
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

### Refator de débito existente

`src/selecao/Unifesspa.UniPlus.Selecao.API/Program.cs:121-122` (`EnvironmentName == "Test"` no guard de Schema Registry) é refatorado nesta mesma entrega para `IsDevelopment()`, que é semanticamente equivalente para o caso ("local sem PG/Kafka real") e satisfaz a regra. Config flag `Kafka:SchemaRegistry:Required` ficou descartada por adicionar superfície de configuração sem ganho — a decisão binária dev/prod é exatamente o que `IsDevelopment()` expressa.

### Por que sem enforcement automático nesta versão

A council debateu Opção A (ADR + fitness test) extensivamente. A primeira implementação do scanner textual revelou que cobrir todas as síntaxes do antipattern em regex puro é frágil: cada nova variante de C# (raw strings com N aspas, interpolação aninhada, comentários multi-linha, pattern matching, named args) abre uma aresta teórica que outro revisor pode achar. A ferramenta correta para "scan completo" é um Roslyn analyzer (mesmo lexer do compilador, tokens classificados de graça), não regex.

Para o `src/` atual — zero violações pós-refator, zero raw strings complexas, code review humano ativo — um detector textual incompleto traria **falsa confiança** maior que o gap real que ele cobre. A regra fica documentada e binding; o gate é a combinação:

1. **Codebase sem precedente** — não há nenhuma chamada `IsEnvironment(literal)` em `src/` para servir de exemplo a copiar.
2. **Code review humano** — qualquer reintrodução é visível em diff (`IsEnvironment("…")` ou `EnvironmentName == "…"` são literais óbvios).
3. **Esta ADR linkada do `CONTRIBUTING.md`** — onboarding contextualizado.

**Upgrade natural se recidiva ocorrer:** Roslyn analyzer em projeto separado (`Unifesspa.UniPlus.Analyzers`) que usa `SyntaxKind.InvocationExpression` + `SemanticModel` para resolver `IHostEnvironment.IsEnvironment` e bater na lista de literais banidos. Custo estimado: ~200 LOC de boilerplate + wiring no `.csproj` de cada projeto. Decisão deferida até evidência empírica de recidiva.

## Consequências

### Positivas

- Código de produção é **indiferente ao ambiente para comportamento**. Binário deployado é único entre tiers; diferenças são exclusivamente configuracionais.
- Customização de teste é **explícita e descobrível** — qualquer dev lendo a fixture vê todo o delta em relação à produção em C# tipado, linha por linha.
- Sem string mágica em paths de produção — typos não passam silenciosos.
- Boundary Clean Architecture preservada — Camada Frameworks (Hosting) não inverte dependência para conhecer Test concerns.
- Postura HML=Prod fica documentada — futuros contribuidores sabem que `IsEnvironment("Hml")` é antipattern, não esquecimento.
- Custo de manutenção zero — sem suite de fitness tests a manter; sem regex frágil a evoluir.

### Negativas

- Sem gate automático em CI: uma recidiva dependeria de code review humano captar. Mitigado pela natureza literal e óbvia do antipattern e pelo codebase sem precedente.
- Onboarding requer leitura desta ADR + `Middleware/README.md` (issue #116) + `ApiFactoryBase` para entender o pattern alternativo. Mitigado por cross-link no `CONTRIBUTING.md`.
- Allowlist implícita (`IsDevelopment()`/`IsProduction()` permitidos) pode parecer permissiva demais para puristas. Aceito — explícito por path seria rigidez sem ROI; a comunidade .NET unanimemente aceita `IsDevelopment()` no composition root.

### Neutras

- O guard refatorado em `Program.cs:121` (`IsDevelopment()`) é semanticamente equivalente ao literal anterior para o cenário real (dev local sem Schema Registry vs HML/Prod com Schema Registry obrigatório).
- Roslyn analyzer fica como follow-up condicional, não como débito — só justificável se recidiva ocorrer.

## Confirmação

Conformidade verificada por:

- **Auditoria pontual no momento da ADR**: `grep -rE "IsEnvironment\s*\(|EnvironmentName\s*(==|!=|\.Equals)" src/` retorna zero matches após o refator deste PR.
- **Code review humano**: PRs que introduzam novos arquivos em `src/` passam pelo workflow `pr-author-org-member` + revisão cruzada. Reintroduções do antipattern são literais óbvios em diff.
- **Onboarding documentado**: `CONTRIBUTING.md` linka esta ADR na tabela de regras de Clean Architecture; `tests/Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests/Middleware/README.md` referencia como decisão binding.

Métricas de acompanhamento (avaliadas em revisões trimestrais de débito técnico):

- `IsEnvironment(literal)` matches em `src/` — alvo: 0
- `EnvironmentName == literal` matches em `src/` — alvo: 0
- Recidiva pós-ADR — alvo: 0 em 12 meses. **Trigger de re-avaliação:** uma única recidiva real abre a Opção A ou C (Roslyn analyzer) como follow-up obrigatório.

## Prós e contras das opções

### A. ADR + fitness test combinado (ArchUnitNET + regex textual)

- Bom, porque tornaria a regra mecânica em CI
- Ruim, porque análise textual de C# é fundamentalmente incompleta — cada regex que cobre uma síntaxe abre arestas teóricas em outras (raw strings, interpolação aninhada, named args, pattern matching). Whack-a-mole sem limite definido
- Ruim, porque ArchUnitNET opera por **tipo**, não por chamada com argumento literal — `IsEnvironment(string)` é overload resolvido, não tipo importável; a parte ArchUnitNET puro seria limitada de qualquer forma
- Ruim, porque detector textual incompleto dá **falsa confiança** maior que o gap real que ele cobre — convencer o revisor de que "o CI cobre" desincentiva atenção humana ao antipattern

### B. ADR normativa sem enforcement automático **(escolhida)**

- Bom, porque custo zero de manutenção
- Bom, porque honesto sobre o nível de proteção: regra documentada + code review humano, sem promessa de gate mecânico que não é robusto
- Bom, porque deixa porta aberta para Roslyn analyzer (Opção C estendida) como upgrade condicional se evidência empírica justificar
- Ruim, porque depende de disciplina humana — uma recidiva pode escapar de um code review distraído
- Mitigação: codebase sem precedente + antipattern literal e óbvio em diff + ADR linkada do `CONTRIBUTING.md`

### C. `BannedApiAnalyzers` (Roslyn) em vez de ArchUnitNET

- Bom, porque feedback in-IDE compile-time (squiggle vermelho)
- Bom, porque mensagem custom orienta o dev para `ApiFactoryBase`
- Ruim, porque pega apenas chamadas de método simbólicas — `EnvironmentName == "..."` (que é a violação real hoje) não é captada por `BannedApiAnalyzers` puro; precisaria de analyzer custom
- Ruim, porque exige adoção de NuGet package adicional fora do ecossistema atual
- **Re-avaliar** como upgrade da Opção B se recidiva ocorrer; analyzer custom (`Unifesspa.UniPlus.Analyzers`) com `SemanticModel` é a forma robusta

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

- ADR-0012 — ArchUnitNET como biblioteca oficial de fitness tests (estabelece a ferramenta para casos onde análise textual é suficiente)
- ADR-0033 — `IUserContext` como abstração canônica (sibling estrutural — ports keep production code unaware)
- ADR-0050 — Registry GHCR e tagging (HML/Prod semantic collapse rationale)
- [Microsoft Learn — Integration tests in ASP.NET Core](https://learn.microsoft.com/aspnet/core/test/integration-tests)
- [Andrew Lock — Supporting integration tests with WebApplicationFactory in .NET 6](https://andrewlock.net/exploring-dotnet-6-part-6-supporting-integration-tests-with-webapplicationfactory-in-dotnet-6/)
- [12-Factor App — Config (Factor III)](https://12factor.net/config)
- [InfoQ — Fitness Functions for Your Architecture (Neal Ford)](https://www.infoq.com/articles/fitness-functions-architecture/)
- **Origem:** sessão `/cy-idea-factory` 2026-05-11 — idea spec em `.compozy/tasks/production-test-isolation/_idea.md`, ADR draft em `.compozy/tasks/production-test-isolation/adrs/adr-001.md`
- Issue rastreadora: #414
- PR #413 (issue #116) — fixture pattern `ApiFactoryBase` já materializado
