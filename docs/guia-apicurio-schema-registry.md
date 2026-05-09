# Guia operacional — Apicurio Schema Registry no uniplus-api

> **Audiência:** desenvolvedores e operadores do uniplus-api.  
> **Pré-requisitos:** [ADR-0051](adrs/0051-apicurio-schema-registry-avro-wolverine.md) (decisões binding) +
> [ADR-0044](adrs/0044-roteamento-domain-events-pg-queue-kafka-opcional.md) (routing PG queue/Kafka) +
> RUNBOOKS §15 do `uniplus-infra` (Apicurio chart deploy + recuperação de `client_secret`).

## O que é

[Apicurio Schema Registry](https://www.apicur.io/registry/) é o **registro central de schemas Avro** dos eventos cross-módulo do Uni+. Em standalone roda em
`schema-registry.standalone.portaluni.com.br` (chart `apps/apicurio-registry/` no `uniplus-infra`), em **modo Confluent-compat** — o cliente .NET (`Confluent.SchemaRegistry`) usa o endpoint `/apis/ccompat/v7` exatamente como contra um Confluent Schema Registry vanilla.

**Por que Schema Registry:** garante compatibilidade entre producer e consumer. Ao invés de cada serviço carregar seu `.avsc` local e quebrar quando o outro evolui, o producer registra o schema no Apicurio e embute o `schema-id` no envelope da mensagem. Consumers recuperam o schema do Registry via `schema-id`, deserializam, e o servidor enforce regras de compatibility (default Uni+: **BACKWARD** — schemas novos podem ler dados antigos).

## Arquitetura

```
┌──────────────────┐         ┌────────────────────┐
│ Selecao.API      │  Kafka  │ Apicurio Registry  │
│                  │ -------→│ (uniplus-infra)    │
│ - PG queue       │         │ Confluent-compat   │
│ - Kafka producer │ ←-------│ /apis/ccompat/v7   │
│   (Avro serdes)  │  HTTPS  │                    │
└──────┬───────────┘         └─────────┬──────────┘
       │                               ▲
       │                               │
       │ Schema embedded               │ Schema fetch
       │ resource (Domain)             │ por schema-id
       │                               │
       │                     ┌─────────┴──────────┐
       │                     │ Ingresso.API       │
       │                     │ (consumer futuro)  │
       │                     └────────────────────┘
       ▼
┌──────────────────────────────────┐
│ Selecao.Domain                   │
│ Events/Schemas/EditalPublicado.avsc │
└──────────────────────────────────┘
```

**Single source of truth:** o `.avsc` vive como **embedded resource** em `Selecao.Domain`. A classe `unifesspa.uniplus.selecao.events.EditalPublicado` (`Selecao.Infrastructure`) e o `SchemaRegistrationHostedService` (`Infrastructure.Core`) leem o mesmo recurso via reflection — não há cópia em outro arquivo.

## Wiring no Program.cs

```csharp
// 1) Settings + cliente único (compartilhado entre hosted service e Wolverine).
SchemaRegistrySettings selecaoSrSettings = builder.Configuration
    .GetSection(SchemaRegistrySettings.SectionName)
    .Get<SchemaRegistrySettings>() ?? new SchemaRegistrySettings();

ISchemaRegistryClient? selecaoSrClient = null;
if (!string.IsNullOrWhiteSpace(selecaoSrSettings.Url))
{
    using ILoggerFactory bootstrapLoggerFactory = LoggerFactory.Create(b => b.AddSerilog());
    selecaoSrClient = SchemaRegistryServiceCollectionExtensions.CreateClient(
        selecaoSrSettings,
        bootstrapLoggerFactory);
    builder.Services.AddSingleton(selecaoSrClient);
}

// 2) Hosted service idempotente registra subjects no Apicurio no startup.
builder.Services.AddSchemaRegistry(builder.Configuration)
    .AddSchema(
        subject: "edital_events-value",
        schemaResourceName: unifesspa.uniplus.selecao.events.EditalPublicado.SchemaResourceName,
        resourceAssembly: typeof(EditalPublicadoEvent).Assembly);

// 3) Wolverine routing: cascade EditalPublicadoEvent → EditalPublicado (Avro) → Kafka.
builder.Host.UseWolverineOutboxCascading(builder.Configuration, "SelecaoDb",
    configureRouting: opts =>
    {
        opts.Discovery.IncludeAssembly(typeof(PublicarEditalCommand).Assembly);
        opts.Discovery.IncludeAssembly(typeof(EditalPublicadoToKafkaCascadeHandler).Assembly);

        opts.PublishMessage<EditalPublicadoEvent>().ToPostgresqlQueue("domain-events");
        opts.ListenToPostgresqlQueue("domain-events");

        bool kafkaConfigured = !string.IsNullOrWhiteSpace(builder.Configuration["Kafka:BootstrapServers"]);
        if (kafkaConfigured && selecaoSrClient is not null)
        {
            opts.PublishMessage<unifesspa.uniplus.selecao.events.EditalPublicado>()
                .ToKafkaTopic("edital_events")
                .DefaultSerializer(new SchemaRegistryAvroSerializer(selecaoSrClient));
        }
    });
```

**Pontos importantes:**

1. **Feature off:** `SchemaRegistry:Url` vazio → cliente não é construído, hosted service não é registrado, routing Avro é skippado. APIs sobem sem Apicurio (dev local sem o serviço).
2. **Cliente único:** o singleton no DI é a mesma instância capturada no closure do callback Wolverine. Evita 2 caches.
3. **Cascade:** o handler `EditalPublicadoToKafkaCascadeHandler` (em `Selecao.Infrastructure`) projeta o evento de domínio (`EditalPublicadoEvent`, intra-módulo) em `EditalPublicado` (Avro, cross-módulo). Application/Domain ficam puros — não conhecem `Apache.Avro`.
4. **Outbox cascade:** o `EditalPublicado` (Avro) é instalado no outbox **dentro** da transação do listener da PG queue. Se o publish para Kafka falhar, o Wolverine retenta a partir do outbox (at-least-once). Consumers cross-módulo deduplicam por `EventId` (UUID v7).

## Configuração — modos de auth

### Dev local (compose)

```yaml
SchemaRegistry__Url: "http://apicurio:8081/apis/ccompat/v7"
# AuthType padrão = None — Apicurio compose tem auth desligada.
```

### Standalone / HML / PROD (OIDC client_credentials contra Keycloak)

```yaml
SchemaRegistry__Url: "https://schema-registry.standalone.portaluni.com.br/apis/ccompat/v7"
SchemaRegistry__AuthType: "OAuthBearer"
SchemaRegistry__OAuth__TokenEndpoint: "https://standalone.portaluni.com.br/auth/realms/uniplus/protocol/openid-connect/token"
SchemaRegistry__OAuth__ClientId: "uniplus-api-selecao"  # ou portal/ingresso
SchemaRegistry__OAuth__ClientSecret: "${OIDC_CLIENT_SECRET}"  # via ESO 5 → Vault
```

O `OIDC_CLIENT_SECRET` chega via `ExternalSecret` do chart Helm da API (`apps/uniplus-api-selecao/templates/externalsecret.yaml` ESO 5 no `uniplus-infra`), que sintetiza um Secret K8s a partir de `secret/standalone/keycloak/clients/uniplus-api-selecao` no Vault. RUNBOOKS §15.6 do `uniplus-infra` documenta a recuperação inicial via `kcadm.sh`.

### Basic auth (lab simples)

```yaml
SchemaRegistry__Url: "http://schema-registry-lab/apis/ccompat/v7"
SchemaRegistry__AuthType: "Basic"
SchemaRegistry__BasicAuthUserInfo: "admin:${APICURIO_BASIC_AUTH_PASSWORD}"
```

## Comportamento operacional

### Startup

1. `SchemaRegistrySettingsValidator` (registrado via `IValidateOptions` + `ValidateOnStart`) verifica coerência da config — falha imediata em `IHost.StartAsync` se algo está inconsistente (e.g. `AuthType=OAuthBearer` sem `ClientSecret`).
2. `SchemaRegistrationHostedService` itera sobre todas as `SchemaRegistration`s no DI, lê o `.avsc` do embedded resource e chama `RegisterSchemaAsync(subject, schema, normalize=true)` — **idempotente:** Apicurio retorna o mesmo `schema-id` para schemas equivalentes.
3. Se Apicurio estiver offline, o hosted service **loga warning** e **retorna** — host segue subindo. O `SchemaRegistryAvroSerializer` da Confluent tenta novamente em runtime na primeira mensagem.

### Producer

- Wolverine entrega a mensagem ao transport Kafka conforme `PublishMessage<T>().ToKafkaTopic(...)`.
- O `SchemaRegistryAvroSerializer` consulta o `ISchemaRegistryClient` (cache local em RAM), obtém ou registra o `schema-id`, e empacota:

```
[magic byte: 0x00] [schema-id: 4 bytes BE] [payload Avro binary]
```

- Schema-id local é cacheado — chamadas subsequentes não batem na rede.

### Consumer

- Recebe o envelope, lê `schema-id`, faz `GetSchemaAsync(schemaId)` (cache local). Deserializa o payload conforme schema retornado.
- **Sem dependência** do assembly do producer: o consumer pode estar em outra linguagem (Java, Python) ou outro módulo .NET (Ingresso) sem referenciar `Selecao.Domain`.

### Compatibility check

- Apicurio aplica a regra de compatibility configurada por subject (default global do chart Uni+: **BACKWARD**).
- Quando o producer registra um schema novo (evolução), Apicurio compara com a versão anterior. Incompatível → 409 Conflict, hosted service loga error e segue (host não trava). Producer continua tentando registrar em runtime (Confluent serdes loga e retenta).

## Adicionando um novo evento

Padrão concreto. Para acrescentar `InscricaoCriadaEvent` (módulo Selecao):

1. **Schema:** crie `src/selecao/Unifesspa.UniPlus.Selecao.Domain/Events/Schemas/InscricaoCriada.avsc` — namespace = `unifesspa.uniplus.selecao.events`, name = `InscricaoCriada`. Já é embedded resource (csproj tem `*.avsc`).
2. **Classe Avro:** `src/selecao/Unifesspa.UniPlus.Selecao.Infrastructure/Messaging/Avro/InscricaoCriada.cs` — namespace `unifesspa.uniplus.selecao.events` (case-sensitive, casa com schema), implementa `ISpecificRecord`, propriedades batem com fields do schema.
3. **Mapper:** `Selecao.Infrastructure/Messaging/InscricaoCriadaToAvroMapper.cs` — função pura `InscricaoCriadaEvent → unifesspa.uniplus.selecao.events.InscricaoCriada`.
4. **Cascade handler:** classe estática que retorna o Avro a partir do evento (Wolverine descobre via `IncludeAssembly`).
5. **Routing:** em `Program.cs`, adicione `opts.PublishMessage<unifesspa.uniplus.selecao.events.InscricaoCriada>().ToKafkaTopic("inscricao_events").DefaultSerializer(new SchemaRegistryAvroSerializer(selecaoSrClient));`
6. **Registro:** adicione `.AddSchema("inscricao_events-value", InscricaoCriada.SchemaResourceName, typeof(InscricaoCriadaEvent).Assembly)` na chain de `AddSchemaRegistry`.

## Troubleshooting

| Sintoma | Causa provável | Resolução |
|---|---|---|
| API trava em `StartAsync` com erro de bind | `SchemaRegistrySettingsValidator` falhou | Logs mostram a falha exata. Conferir env vars `SchemaRegistry__*` |
| Hosted service loga `Falha ao registrar schema Avro no startup` | Apicurio offline, auth inválida, ou DNS não resolve | Logs incluem o subject; Apicurio side: `kubectl logs deploy/apicurio-registry`. Após Apicurio voltar, registro tenta novamente em runtime |
| Producer falha com `SchemaRegistryException: Schema being registered is incompatible` | Mudança no `.avsc` quebra BACKWARD compatibility | Re-pensar evolução — campos novos devem ter default; remoção exige etapa intermediária |
| Consumer recebe `AvroException: Unable to find type 'unifesspa...EditalPublicado'` | Namespace do `.avsc` não casa com namespace da classe C# | Apache.Avro NET resolve via `Type.GetType("&lt;ns&gt;.&lt;name&gt;")` — namespace lowercase é mandatório quando o schema usa lowercase |
| Token endpoint retorna 401 | Vault não populado ou client_secret rotacionado | RUNBOOKS §15.6 do `uniplus-infra` — re-recuperar via `kcadm.sh get clients/$CID/client-secret` |
| Wolverine envia `byte[]` JSON em vez de Avro | `selecaoSrClient` é `null` (URL vazia) ou Kafka não configurado | Conferir `SchemaRegistry__Url` + `Kafka__BootstrapServers` ambos populados |

## Compose local — smoke

```bash
# Sobe Apicurio (e Kafka, Postgres, etc)
docker compose -f docker/docker-compose.yml up -d apicurio kafka postgres

# Health
curl http://localhost:8081/q/health/ready

# Sobe APIs em dev
docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml up -d --build selecao-api

# Inspeciona o schema registrado
curl http://localhost:8081/apis/ccompat/v7/subjects/edital_events-value/versions/latest

# Publica via endpoint (cria + publica edital → cascade dispara → Avro vai para Kafka)
curl -X POST http://localhost:5202/api/editais ...
```

## Referências

- ADR-0051 — Apicurio Schema Registry: Avro + Wolverine (decisões binding)
- ADR-0014 — Kafka como bus assíncrono inter-módulos
- ADR-0044 — Roteamento de domain events (PG queue + Kafka opcional)
- [Confluent .NET Schema Registry docs](https://github.com/confluentinc/confluent-kafka-dotnet/wiki/Schema-Registry)
- [Apicurio Registry docs — Confluent compat mode](https://www.apicur.io/registry/docs/apicurio-registry/3.x/getting-started/assembly-using-the-registry-client.html)
- `uniplus-infra/apps/apicurio-registry/values.yaml` — chart Helm + role mapping `/users/uniplus → sr-developer`
- `uniplus-infra/docs/RUNBOOKS.md §15` — operação Apicurio standalone
