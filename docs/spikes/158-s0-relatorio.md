# Relatório do Spike S0 — Outbox Wolverine (#158)

- **Branch:** `spikes/158-s0-handler-inmemory`
- **Data:** 2026-04-25
- **Status:** concluído com decisão de avançar para S2 — V0 reprovado por inviabilidade técnica do framework
- **Plano de referência:** [`docs/spikes/158-plano-validacao-outbox-wolverine.md`](158-plano-validacao-outbox-wolverine.md)

## Resumo executivo

A variante **V0 — "stack base sem routing durável" — é inviável em Wolverine
5.32.1-pr2586**. Com `PublishDomainEventsFromEntityFrameworkCore` configurado,
o `EfCoreEnvelopeTransaction` exige message persistence backend e lança
`InvalidOperationException` quando nenhum está registrado. O fix do
`DomainEventScraper` (PR `JasperFx/wolverine#2586`, incorporado pelo PR
uniplus-api#160) só pode ser exercido com algum backend ativo. Assim, S0
isolado nos termos do plano não tem como ser executado.

A sanidade do scraper passa a ser validada implicitamente em **S2 (transporte
PostgreSQL)**, que se torna o próximo passo concreto.

Esta fase também desentupiu três obstáculos ortogonais que teriam bloqueado
qualquer spike subsequente: bug de modelagem em `NomeSocial`, override frágil
de connection string no `WebApplicationFactory`, e visibilidade dos handlers
para a discovery convencional do Wolverine. Os três estão resolvidos no
spike-fixture e um deles (NomeSocial) foi corrigido em código de produção.

## Cenário pretendido

Conforme [§S0 do plano](158-plano-validacao-outbox-wolverine.md):

- `Edital.Publicar()` adiciona `EditalPublicadoEvent` em `EntityBase.DomainEvents`.
- Um handler Wolverine de comando chama `db.SaveChangesAsync()`.
- O fix do `DomainEventScraper` materializa a coleção, drena os eventos e
  publica-os no pipeline.
- Um handler local de teste recebe `EditalPublicadoEvent` e registra evidência.
- **Não** se exige outbox durável, transporte externo ou tabelas Wolverine.
- Falha bloqueante esperada: exceção `Collection was modified` ou ausência de
  recebimento.

## Estrutura criada

```
tests/Unifesspa.UniPlus.Selecao.IntegrationTests/Outbox/Capability/
  AssemblyInfo.cs                       — [assembly: WolverineModule<OutboxSpikeWolverineExtension>]
  OutboxCapabilityCollection.cs         — definição da xUnit collection do spike
  OutboxCapabilityFixture.cs            — Postgres em Testcontainers + lifecycle
  OutboxCapabilityApiFactory.cs         — WebApplicationFactory custom
  OutboxSpikeWolverineExtension.cs      — IWolverineExtension auto-loaded
  SpikeMessages.cs                      — PublicarEditalSpikeCommand + DomainEventCollector
  SpikeHandlers.cs                      — handlers Wolverine de teste
  OutboxCapabilityMatrixTests.cs        — testes da matriz S0–S9 (S0 implementado)
```

Convenção: todos os testes recebem `[Trait("Category", "OutboxCapability")]` e
podem ser filtrados via `dotnet test --filter "Category=OutboxCapability"`,
isolando-os dos testes regulares de API.

### Configuração Wolverine adotada para o spike

```csharp
[assembly: WolverineModule<OutboxSpikeWolverineExtension>]

public sealed class OutboxSpikeWolverineExtension : IWolverineExtension
{
    public void Configure(WolverineOptions options)
    {
        options.PublishDomainEventsFromEntityFrameworkCore<EntityBase>(
            entity => entity.DomainEvents);

        options.Discovery.IncludeAssembly(typeof(OutboxSpikeWolverineExtension).Assembly);
    }
}
```

A extension é carregada automaticamente pelo Wolverine via
`[WolverineModule<T>]`, sem precisar tocar no `Program.cs` da API. Confirmado
pelos logs de execução: `Searching assembly Unifesspa.UniPlus.Selecao.IntegrationTests
for Wolverine message handlers`.

## Achados intermediários (resolvidos)

### A1 — `NomeSocial`: parâmetro do construtor incompatível com EF Core 10

`db.Database.EnsureCreatedAsync()` falhava em `SelecaoDbContext` com:

```
System.InvalidOperationException : No suitable constructor was found for the type 'NomeSocial'.
The following constructors had parameters that could not be bound to properties of the type:
  Cannot bind 'nomeSocial' in 'NomeSocial(string nomeCivil, string nomeSocial)'
```

Causa raiz: em
`src/shared/Unifesspa.UniPlus.Kernel/Domain/ValueObjects/NomeSocial.cs`, o
construtor privado era `NomeSocial(string nomeCivil, string? nomeSocial)`, mas
a propriedade que armazenava o valor se chama `Nome`. EF Core 10 tenta bindar
o parâmetro `nomeSocial` em uma propriedade `NomeSocial` — que não existe na
classe — e desiste de criar o objeto.

**Correção aplicada (escopo do spike, mas é fix ortogonal):** renomear o
parâmetro privado de `nomeSocial` para `nome`, alinhando com a propriedade.
Mudança puramente cosmética (construtor é `private`, só `NomeSocial.Criar` o
chama), elimina a ambiguidade e desbloqueia qualquer teste de integração
futuro do módulo Seleção.

Verificado que **nenhum outro VO** tem o mesmo padrão (`Cpf`, `Email`,
`NotaFinal`, `NumeroEdital`, `FormulaCalculo`, `PeriodoInscricao` — todos com
parâmetros e propriedades alinhados).

Implicação mais ampla: o módulo Seleção **não tinha cobertura de teste de
integração que materializasse o schema completo** — não há migrations EF e os
testes existentes (`AuthEndpointsTests`, `ProfileEndpointsTests`) só exercem
endpoints que não tocam o banco. O spike #158 foi o primeiro caminho a
materializar o modelo contra Postgres real, e por isso foi quem expôs o bug.

### A2 — Connection string do test factory não chegava ao `DbContext`

Tentativa inicial: `ApiFactoryBase` adiciona overrides via
`builder.ConfigureAppConfiguration(... AddInMemoryCollection(...))`. Em
.NET 10 com minimal hosting, o `Program.cs` do `Selecao.API` lê
`builder.Configuration.GetConnectionString("SelecaoDb")` **imediatamente** ao
fazer `AddSelecaoInfrastructure(connectionString)`. Quando o
`ConfigureAppConfiguration` do test factory é aplicado, o valor já foi
capturado.

Resultado: `EnsureCreatedAsync` tentava conectar em `localhost:5432`
(`appsettings.Development.json`) em vez do container Testcontainers
(`Host=localhost;Port=PORTA_RANDOMICA;...`).

**Solução adotada no spike:** em `ConfigureTestServices`, remover o
`DbContextOptions<SelecaoDbContext>` e o `SelecaoDbContext` registrados pelo
Program.cs e re-registrar com a connection string efêmera:

```csharp
services.RemoveAll<DbContextOptions<SelecaoDbContext>>();
services.RemoveAll<SelecaoDbContext>();
services.AddDbContext<SelecaoDbContext>(opts =>
    opts.UseNpgsql(_connectionString));
```

Isso pula os interceptors `SoftDelete` e `Auditable` do registro de produção,
o que é aceitável para o spike (interceptors não afetam drenagem de domain
events). **Para testes de integração reais que dependam dos interceptors,
será preciso solução mais robusta** (provavelmente refatorar
`AddSelecaoInfrastructure` para receber `IConfiguration` em vez de string já
resolvida, ou usar `IOptions<DbContextOptions<...>>`).

### A3 — Discovery do Wolverine ignora tipos `internal`

Após resolver A1 e A2, Wolverine logava `Wolverine found no handlers` mesmo
escaneando o assembly de testes. A causa foi tornar `PublicarEditalSpikeCommand`,
`PublicarEditalSpikeHandler`, `EditalPublicadoSpikeHandler` e
`OutboxSpikeWolverineExtension` como `internal sealed`. A discovery
convencional do Wolverine 5.x só enxerga tipos `public`.

**Correção:** todos os tipos do spike viraram `public sealed`, com
`[SuppressMessage("CA1515", ...)]` justificando que a "API pública" do
projeto de testes é o que xUnit e Wolverine precisam enxergar via reflection.

## Achado principal — V0 inviável

Após resolver A1, A2 e A3, o teste avançou até o handler:

```
[ERR] Invocation of PublicarEditalSpikeCommand { Numero = 001/2026, ... } failed!
System.InvalidOperationException: This Wolverine application is not using
Database backed message persistence. Please configure the message persistence
   at Wolverine.EntityFrameworkCore.Internals.EfCoreEnvelopeTransaction..ctor(
       DbContext dbContext, MessageContext messaging,
       IEnumerable<IDomainEventScraper> scrapers)
   at Internal.Generated.WolverineHandlers.PublicarEditalSpikeCommandHandler1137929320
       .HandleAsync(MessageContext context, CancellationToken cancellation)
```

Análise:

1. Wolverine descobriu o handler (`PublicarEditalSpikeCommandHandler1137929320`
   é o tipo gerado dinamicamente).
2. Code generation rodou.
3. O handler invocado tentou abrir um `EfCoreEnvelopeTransaction` (porque
   `PublishDomainEventsFromEntityFrameworkCore` está habilitado e o pipeline
   precisa coordenar `SaveChanges` com o envelope storage).
4. O construtor de `EfCoreEnvelopeTransaction` valida que existe message
   persistence backend e falha se nenhum estiver configurado.

Inspeção do XML doc dos pacotes
(`vendors/nuget-local/WolverineFx*.5.32.1-pr2586.nupkg`) confirma que **não
existe `InMemoryPersistence`/`MemoryPersistence`/`UseLightweight` ou
equivalente in-memory** em Wolverine 5.x. A integração EF + scraper exige
backend durável.

### Conclusão sobre a tabela V0–V7

A reprova de V0 não é por bug do projeto nem do fix — é uma característica do
framework. Ela tem implicação para todas as variantes "in-memory" do plano:

| Variante | Implicação |
|---|---|
| V0 (stack base sem routing durável) | **Não factível na 5.32.1-pr2586** — exige persistence |
| V1 (`UseDurableLocalQueues` sem routing) | Mesma exigência — durabilidade local depende de envelope storage |
| V2/V3/V3a/V3a' (variações in-memory) | Mesma exigência |

Variantes que exercem persistence backend (V4 PostgreSQL transport, V5 Kafka
transport, V6 Kafka indisponível, V7 restart recovery) continuam factíveis e
são exatamente o que S2–S6 endereçam.

## Comandos executados

```bash
# Build do projeto de testes (verde após correções)
dotnet build tests/Unifesspa.UniPlus.Selecao.IntegrationTests/Unifesspa.UniPlus.Selecao.IntegrationTests.csproj

# Execução do spike S0
dotnet test tests/Unifesspa.UniPlus.Selecao.IntegrationTests/Unifesspa.UniPlus.Selecao.IntegrationTests.csproj \
  --filter "Category=OutboxCapability" \
  --logger "console;verbosity=normal"
```

Saída resumida do `dotnet test` na execução final (após resolver A1, A2, A3):

```
[INF] Searching assembly Unifesspa.UniPlus.Selecao.IntegrationTests for Wolverine message handlers
[INF] Searching assembly Unifesspa.UniPlus.Selecao.API for Wolverine message handlers
... (codegen)
[ERR] Invocation of PublicarEditalSpikeCommand failed!
System.InvalidOperationException: This Wolverine application is not using Database backed message persistence.
```

## Versões e ambiente

- **Branch base:** `main` em `54f1a8b` (PR uniplus-api#161 mergeado).
- **Branch do spike:** `spikes/158-s0-handler-inmemory`.
- **Pacotes Wolverine:** `5.32.1-pr2586` em `vendors/nuget-local/`
  (`WolverineFx`, `WolverineFx.EntityFrameworkCore`, `WolverineFx.RDBMS`).
- **Pacote Testcontainers:** `Testcontainers.PostgreSql` 4.11.0 (já no
  `Directory.Packages.props`).
- **Postgres do spike:** imagem `postgres:18-alpine` em container efêmero.
- **Runtime:** .NET 10 / C# 14, Linux 6.19.14-arch1-1, Docker 28.3.3.
- **Conta gh ativa durante a sessão:** `marmota-alpina` (validado antes de
  qualquer push, conforme regra `require_last_push_approval`).

## Atualização da matriz V0–V7

A linha V0 do plano (`docs/spikes/158-plano-validacao-outbox-wolverine.md`)
deve ser atualizada para:

| Variante | Configuração | Esperado após fix | Observado | Status |
|---|---|---|---|---|
| V0 | Stack base sem routing durável | `DomainEvents` podem ser drenados, mas sem envelope durável observável | Wolverine 5.32.1-pr2586 lança `InvalidOperationException` no `EfCoreEnvelopeTransaction` antes do scraper rodar — o ciclo exige message persistence configurada (não há `InMemoryPersistence` em 5.x) | **Reprovado por inviabilidade técnica do framework**, não por bug do projeto |

A atualização da tabela no documento do plano será feita em commit separado
após decisão sobre o caminho de S2.

## Próximo passo recomendado

**S2 — Transporte PostgreSQL** ([§S2 do plano](158-plano-validacao-outbox-wolverine.md#s2---transporte-postgresql)).

Pré-requisitos para S2:

1. Adicionar `WolverineFx.Postgresql` ao `Directory.Packages.props` (versão a
   decidir — pacote oficial 5.32.1 vs gerar `5.32.1-pr2586` no fork local
   conforme alerta do plano §"Snapshot da versão em validação").
2. Estender `OutboxSpikeWolverineExtension` com
   `UsePostgresqlPersistenceAndTransport(connectionString, "wolverine_spike")`
   — o que exige passar a connection string do Postgres do Testcontainers
   para a extension (provavelmente via singleton mutável ou DI customizada,
   já que `[WolverineModule<T>]` instancia sem parâmetros).
3. Renomear o teste S0 (e adicionar S2) para refletir que a sanidade do
   scraper agora é uma assertiva embutida do S2, não um spike isolado.

A sanidade do scraper continua sendo um critério de S2: se evento for entregue
ao handler local sem `Collection was modified`, o fix do PR
`JasperFx/wolverine#2586` está validado funcionalmente — apenas dentro do
contexto persistente.

## Referências

- [Plano de validação do outbox Wolverine (#158)](158-plano-validacao-outbox-wolverine.md)
- [ADR-022 — backbone CQRS Wolverine](https://github.com/unifesspa-edu-br/uniplus-docs/blob/main/docs/adrs/ADR-022-backbone-cqrs-wolverine.md)
- [ADR-024 — outbox Wolverine + EF não adotado em #135](https://github.com/unifesspa-edu-br/uniplus-docs/blob/main/docs/adrs/ADR-024-outbox-wolverine-ef-nao-adotado-em-135.md)
- [PR uniplus-api#160 — feed local com fix do scraper](https://github.com/unifesspa-edu-br/uniplus-api/pull/160)
- [PR uniplus-api#161 — plano de validação](https://github.com/unifesspa-edu-br/uniplus-api/pull/161)
- [JasperFx/wolverine#2585](https://github.com/JasperFx/wolverine/issues/2585) e [#2586](https://github.com/JasperFx/wolverine/pull/2586) — issue e fix do `DomainEventScraper`.
