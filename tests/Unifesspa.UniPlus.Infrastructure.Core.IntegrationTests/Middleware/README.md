# Smoke tests do pipeline de middleware

Suíte canônica de smoke tests para o pipeline de middleware shared do
`Infrastructure.Core`. Cobre o que os testes unitários (com
`DefaultHttpContext`) não conseguem: ordem de registro em `Program.cs`,
dependências de DI cruzadas e comportamento do servidor HTTP real.

**Origem:** issue [#117](https://github.com/unifesspa-edu-br/uniplus-api/issues/117)
(Story pai), [#116](https://github.com/unifesspa-edu-br/uniplus-api/issues/116)
(primeira Task — `CorrelationIdMiddleware`).

**Decisão arquitetural binding:** [ADR-0053](../../../docs/adrs/0053-zero-test-environment-branches-in-production-code.md)
proíbe `IsEnvironment("Testing")` e `EnvironmentName == "..."` em `src/`. A
fixture canônica deste diretório (`InfraCoreApiFactory : MonolitoApiFactory`)
é o **port** que substitui esse antipattern — toda customização de teste
vive aqui, não no `Program.cs`. A regra é normativa (sem enforcement
automático em CI nesta versão); code review humano + ausência de precedente
no codebase são os gates.

## Convenção para adicionar um novo smoke

A `ApiFactoryBase<TEntryPoint>` em `tests/Unifesspa.UniPlus.IntegrationTests.Fixtures/Hosting/`
faz a maior parte do trabalho — Wolverine, MigrationHostedService,
OpenTelemetry exporter e health checks de infra externa já são removidos
ali. Para um middleware novo, geralmente bastam **3 passos** (~20 linhas):

### 1. Reuso da fixture canônica

A fixture `InfraCoreApiFactory` (neste diretório) sobe a **API UniPlus**
(composition root do monólito) e fornece os overrides mínimos de configuração
pela `MonolitoApiFactory`. Reusar quando o smoke não exigir setup específico:

```csharp
public sealed class MeuMiddlewareSmokeTests : IClassFixture<InfraCoreApiFactory>
{
    private readonly InfraCoreApiFactory _factory;

    public MeuMiddlewareSmokeTests(InfraCoreApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Cenario_Esperado()
    {
        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/health");
        // asserts no header / corpo / status code...
    }
}
```

### 2. Fixture especializada (quando precisar de overrides extras)

Se o middleware exigir configuração específica (ex.: feature flag, header
adicional, scheme de auth diferente), derivar uma fixture nova:

A `MonolitoApiFactory` já fornece as 5 connection strings + Auth dummy e troca o
cache por um fake. Para overrides extras, sobrescrever o hook `OverridesAdicionais`
(aplicado depois dos defaults, vence chaves duplicadas):

```csharp
public sealed class RateLimitApiFactory : MonolitoApiFactory
{
    public RateLimitApiFactory()
        : base("Host=localhost;Port=5432;Database=uniplus;Username=u;Password=u", wolverineEnabled: false)
    {
    }

    protected override IEnumerable<KeyValuePair<string, string?>> OverridesAdicionais() =>
    [
        new("RateLimit:Enabled", "true"),
        new("RateLimit:RequestsPerMinute", "5"),
    ];
}
```

### 3. Cenários

Cada smoke deve cobrir **proteções de wiring que o unit test não pega**:

- Ordem de registro: middleware está antes/depois do que precisa?
- DI: o middleware resolve suas dependências no pipeline real?
- HTTP server: parsing de headers, encoding, content-type — algo que o
  `DefaultHttpContext` mente?

Use o `CorrelationIdMiddlewareSmokeTests` como template literal: 3 cenários
(feliz, feliz-com-input, defesa-de-segurança).

## O que NÃO é smoke aqui

- **Testes funcionais de domínio** (Edital, Inscrição, etc.) — esses ficam
  em `Selecao.IntegrationTests` ou `Ingresso.IntegrationTests`.
- **Testes que exigem PostgreSQL/Kafka/MinIO reais** — usar `Testcontainers`
  num `*.IntegrationTests` modular, não aqui.
- **Testes E2E** com browser/UI — fora do escopo do backend.

## Por que via host UniPlus

Com a topologia de 3 APIs, os 5 módulos de negócio (Selecao, Ingresso,
Configuracao, OrganizacaoInstitucional) viraram class libraries sem `Program.cs`
próprio — só executam dentro da **API UniPlus** (composition root). O middleware
do `Infrastructure.Core` é cabeado uma única vez, no `Program.cs` do host. Smoke
contra o host exercita o **wiring real de produção**, sem o risco antigo de drift
entre Programs por módulo (que deixaram de existir).

Geo e Portal permanecem deployables autônomos com `Program.cs` próprio. A
paridade do middleware shared entre os 3 executáveis (UniPlus/Geo/Portal),
quando o custo do drift aparecer, é melhor coberta por um arch test em
`Unifesspa.UniPlus.ArchTests` do que por uma matriz de fixtures aqui.

## Performance esperada

A fixture `InfraCoreApiFactory` sobe em <2s. Cada smoke individual leva
~5-300ms. Suíte inteira deve fechar em poucos segundos — se passar de 30s
algo está errado (alguma dep externa real foi reativada).
