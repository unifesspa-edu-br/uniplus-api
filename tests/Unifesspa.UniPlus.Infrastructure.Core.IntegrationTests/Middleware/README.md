# Smoke tests do pipeline de middleware

Suíte canônica de smoke tests para o pipeline de middleware shared do
`Infrastructure.Core`. Cobre o que os testes unitários (com
`DefaultHttpContext`) não conseguem: ordem de registro em `Program.cs`,
dependências de DI cruzadas e comportamento do servidor HTTP real.

**Origem:** issue [#117](https://github.com/unifesspa-edu-br/uniplus-api/issues/117)
(Story pai), [#116](https://github.com/unifesspa-edu-br/uniplus-api/issues/116)
(primeira Task — `CorrelationIdMiddleware`).

## Convenção para adicionar um novo smoke

A `ApiFactoryBase<TEntryPoint>` em `tests/Unifesspa.UniPlus.IntegrationTests.Fixtures/Hosting/`
faz a maior parte do trabalho — Wolverine, MigrationHostedService,
OpenTelemetry exporter e health checks de infra externa já são removidos
ali. Para um middleware novo, geralmente bastam **3 passos** (~20 linhas):

### 1. Reuso da fixture canônica

A fixture `InfraCoreApiFactory` (neste diretório) hospeda o `Program.cs`
do Selecao.API e fornece os overrides mínimos de configuração. Reusar
quando o smoke não exigir setup específico:

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

```csharp
public sealed class RateLimitApiFactory : ApiFactoryBase<Program>
{
    protected override IEnumerable<KeyValuePair<string, string?>> GetConfigurationOverrides() =>
    [
        new("ConnectionStrings:SelecaoDb", "Host=localhost;Port=5432;Database=u;Username=u;Password=u"),
        new("Auth:Authority", "http://localhost/test-realm"),
        new("Auth:Audience", "uniplus"),
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

## Por que o Selecao.API e não os 3

Os 3 módulos (Selecao/Ingresso/Portal) cabeiam o middleware do
`Infrastructure.Core` identicamente no `Program.cs`. Smoke contra um
deles prova o pattern para o caso atual com custo de manutenção mínimo
(1 fixture, 1 ProjectReference).

**Risco residual aceito:** drift de wiring entre módulos não é
exercitado por este smoke. Hoje não há arch test enforçando paridade
da ordem de middleware entre `Selecao`, `Ingresso` e `Portal`, e o
CodeQL default não prova essa invariante. Mitigações disponíveis se o
custo do drift aparecer:

- Promover esta suíte a matriz de 3 fixtures (uma por API).
- Criar arch test em `Unifesspa.UniPlus.ArchTests` cabreando "todos os
  `Program.cs` chamam `UseMiddleware<CorrelationIdMiddleware>` na mesma
  posição relativa a `UseAuthentication`/`UseRouting`".

Issue #117 documenta a decisão CA-01 revisitada e mantém esses dois
caminhos como follow-up explícito.

## Performance esperada

A fixture `InfraCoreApiFactory` sobe em <2s. Cada smoke individual leva
~5-300ms. Suíte inteira deve fechar em poucos segundos — se passar de 30s
algo está errado (alguma dep externa real foi reativada).
