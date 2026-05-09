---
status: "accepted"
date: "2026-05-09"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0051: Apicurio Schema Registry com Avro e Wolverine — schemas no Domain, registro idempotente, OAuth client_credentials

## Contexto e enunciado do problema

A [ADR-0014](0014-kafka-como-bus-assincrono-inter-modulos.md) estabelece Kafka como bus assíncrono inter-módulos do Uni+. A [ADR-0044](0044-roteamento-domain-events-pg-queue-kafka-opcional.md) define que `EditalPublicadoEvent` é roteado para PG queue intra-módulo (sempre) e Kafka cross-módulo (opcional, condicional a `Kafka:BootstrapServers` configurado).

Faltava decidir o **wire format** dos eventos no Kafka. O `uniplus-infra` deployou o Apicurio Schema Registry (chart `apps/apicurio-registry/`, modo Confluent-compat) precisamente para isso. Esta ADR fecha o lado producer (uniplus-api) e estabelece pattern para subsequentes eventos.

Decisões pendentes resolvidas:

1. **Localização dos schemas:** `contracts/events/<modulo>/` (cross-cutting) ou `src/<modulo>/Domain/Events/Schemas/` (local ao bounded context)?
2. **Forma de codegen:** classes `ISpecificRecord` geradas via tool em build (Avro Tools) ou escritas à mão?
3. **Auth contra Apicurio:** static credentials, basic, ou OAuth `client_credentials` com Keycloak?
4. **Quando registrar o schema no Apicurio:** runtime (Confluent serdes lazy) ou startup (idempotente)?
5. **Cliente HTTP do Schema Registry:** múltiplas instâncias (uma por consumer Wolverine + uma para o hosted service) ou única compartilhada?

## Drivers da decisão

- **Manter Domain puro:** ADR-0002 — Domain depende apenas de SharedKernel; não pode ganhar referência a `Apache.Avro` nem a `Confluent.SchemaRegistry`.
- **Cross-module sem build-time coupling:** ADR-0014 estabelece que módulos não compartilham assemblies de Domain. Schema Registry resolve isso via `schema-id` no envelope da mensagem — consumer pull do Apicurio em runtime.
- **Operacional sob falha do Apicurio:** subscribers existentes (`EditalPublicadoEventHandler` na PG queue) precisam continuar funcionando mesmo se Apicurio estiver offline no boot. Hosted service não pode travar `IHost.StartAsync`.
- **Single source of truth:** drift entre `.avsc` no Git e schema publicado no Apicurio = bug silencioso. Producer e hosted service devem ler do **mesmo** local.
- **Auth coerente com OIDC clients M2M:** [uniplus-infra#163](https://github.com/unifesspa-edu-br/uniplus-infra/issues/163) já provisionou clients confidenciais `uniplus-api-{portal,selecao,ingresso}` com mappers `aud=apicurio-registry` + `groups=["/users/uniplus"]` precisamente para esse fluxo `client_credentials`.

## Opções consideradas

### 1. Localização dos schemas

- **A. `contracts/events/<modulo>/` (cross-cutting).** Diretório fora dos módulos, simétrico aos baselines OpenAPI em `contracts/openapi.*.json`.
- **B. `src/<modulo>/Domain/Events/Schemas/` (local ao bounded context).** Schema vive perto do evento de domínio.

**Escolhida: B.** Schemas Avro são parte do contrato de saída do bounded context; vivem com o evento, no Domain. O bind como `<EmbeddedResource>` evita dependência de `Apache.Avro` no Domain — o `.avsc` é só texto.

### 2. Forma de codegen das classes `ISpecificRecord`

- **A. Codegen via Apache.Avro.Tools em build target MSBuild.** Standard. Adiciona complexidade de build (custom target).
- **B. Classe escrita à mão seguindo `ISpecificRecord`.** Boilerplate ~80 linhas por evento. Schema lido do embedded resource via `Schema.Parse(...)` no static initializer.

**Escolhida: B para o 1º caso.** Custo de boilerplate baixo (1 classe), evita complexidade de codegen no MSBuild para um único subject. Trigger de reavaliação: ≥3 schemas Avro no projeto, considerar codegen automatizado.

### 3. Auth contra Apicurio

- **A. None / desligada.** Apenas em dev local (compose).
- **B. Basic auth.** Aceitável em lab simples.
- **C. OAuth `client_credentials` (Bearer JWT) contra Keycloak.** Coerente com clients M2M já provisionados em [uniplus-infra#163](https://github.com/unifesspa-edu-br/uniplus-infra/issues/163) e role mapping em `apps/apicurio-registry/values.yaml`.

**Escolhida: C para standalone/HML/PROD; A para dev.** Suporte a B mantido para casos edge (lab simples).

### 4. Quando registrar o schema

- **A. Runtime (Confluent serdes lazy).** Default da lib — registra no primeiro `Produce`. Race condition possível se múltiplos producers iniciam ao mesmo tempo. Sem schema = sem mensagem.
- **B. Startup (hosted service idempotente).** Schema disponível no Apicurio antes da primeira mensagem. Race-free. Apicurio offline ≠ host trava (fail-graceful).
- **C. CLI/script pré-deploy.** Schema fora do ciclo de vida da app. Operacional separado.

**Escolhida: B + A (defesa em profundidade).** Hosted service tenta registrar no startup; se falhar (Apicurio offline), loga warning e segue. O Confluent serdes faz fallback automático em runtime. Cobre os 3 cenários: cluster novo, cluster com Apicurio temporariamente fora, cluster steady-state.

### 5. Cliente HTTP do Schema Registry — múltiplas instâncias ou única

- **A. Múltiplas (default ingenuo).** Cada consumer/producer cria seu cliente. Cache duplicado, possível inconsistência transiente entre caches.
- **B. Única compartilhada.** Singleton no DI consumido pelo hosted service; mesma instância capturada no closure do `UseWolverineOutboxCascading` para o `SchemaRegistryAvroSerializer`. Single cache, single auth flow.

**Escolhida: B.** Padrão `SchemaRegistryServiceCollectionExtensions.CreateClient(...)` em `Program.cs` cria o cliente uma vez, registra como singleton **antes** de `AddSchemaRegistry()` (que usa `TryAddSingleton` e respeita o registro prévio). O Wolverine routing captura no closure.

## Resultado da decisão

**Pacote completo:** schemas Avro vivem em `src/<modulo>/Domain/Events/Schemas/<Evento>.avsc` como `EmbeddedResource`. Classes `ISpecificRecord` escritas à mão em `src/<modulo>/Infrastructure/Messaging/Avro/<Evento>.cs` no namespace que casa **exatamente** com o do schema (Apache.Avro NET resolve via `Type.GetType("<namespace>.<name>")`). Cascading handler em `Selecao.Infrastructure` mapeia `<Evento>Event` (intra-módulo) para `<Evento>` (Avro, cross-módulo).

Auth via `OAuthBearerAuthenticationHeaderValueProvider` (em `Infrastructure.Core`) que faz `client_credentials` contra Keycloak, cacheia o JWT até `exp - RefreshSkewSeconds`, renova proativamente. `AuthType=None` permite dev local sem auth.

Hosted service `SchemaRegistrationHostedService` (em `Infrastructure.Core`) registra todos os subjects declarados via `AddSchemaRegistry().AddSchema(...)` no startup, **fail-graceful**: erro de auth/network loga warning e retorna. Confluent serdes lida com fallback em runtime.

Cliente `ISchemaRegistryClient` é singleton único, criado em `Program.cs`, compartilhado entre hosted service (DI) e routing Wolverine (closure).

**Convenções de naming**:

- Schema namespace: `unifesspa.uniplus.<modulo>.events` (lowercase). Casa com o C# namespace da classe Avro — Apache.Avro NET é case-sensitive.
- Schema name: `<Evento>` (sem suffix `Avro`).
- Topic Kafka: `<dominio>_events` (snake_case, ADR-0014).
- Subject SR: `<topic>-value` (Confluent SR convention).
- Compatibility default: `BACKWARD`.

**Estado atual** (snake-case Avro):

```text
src/selecao/Selecao.Domain/Events/Schemas/EditalPublicado.avsc  ─── namespace=unifesspa.uniplus.selecao.events, name=EditalPublicado
src/selecao/Selecao.Infrastructure/Messaging/Avro/EditalPublicado.cs  ─── namespace casa com schema
src/selecao/Selecao.Infrastructure/Messaging/EditalPublicadoToAvroMapper.cs
src/selecao/Selecao.Infrastructure/Messaging/EditalPublicadoToKafkaCascadeHandler.cs
```

**Resource name canônico:** `Unifesspa.UniPlus.Selecao.Domain.Events.Schemas.EditalPublicado.avsc` (assembly + path no .csproj). Lido pelo hosted service via `Assembly.GetManifestResourceStream(...)`.

## Consequências

### Positivas

- **Domain puro:** sem dep de Avro lib (apenas embedded resource).
- **Cross-module sem coupling:** Ingresso futuro consume sem referenciar `Selecao.Domain` — pull do Apicurio.
- **Compatibility enforced:** Apicurio rejeita mudanças que quebram BACKWARD; schema evolution é validada por mecanismo externo.
- **Operacional resiliente:** Apicurio offline no boot não trava o host; runtime Confluent recupera quando o Apicurio volta.
- **Configurável por env:** mesmo binário roda dev (sem auth), lab (basic), standalone/HML/PROD (OAuth) sem code change.
- **Wire format Confluent:** consumers em qualquer linguagem (Java, Python, Go) funcionam pelo padrão `[magic][schema-id][payload]`.

### Negativas

- **Dois assemblies para 1 evento:** `EditalPublicadoEvent` (Domain) + `EditalPublicado` (Avro) com mapper trivial. Pode parecer ruído mas mantém Clean Architecture.
- **Namespace lowercase em C#:** classes Avro fogem da convenção PascalCase para casar com schema namespace. CA1707 suprimido na classe (justificado).
- **Boilerplate `ISpecificRecord` à mão:** ~80 linhas por evento. Aceitável para 1 caso; reavaliar codegen com 3+ schemas.
- **2 caches no client (mitigado):** ao seguir o padrão de single client em `Program.cs` o cache é único; se algum dev futuro registrar um cliente novo, ficam dois.

### Neutras

- **Schema evolution policy formal não definida nesta ADR.** BACKWARD é o default Confluent — adequada para o caminho "consumers leem dados antigos com schema novo". FORWARD ou FULL serão decisão por subject quando houver caso real.
- **Ingresso ainda não consume.** Routing existe, mas o evento `EditalPublicado` em Kafka não tem subscriber cross-módulo até "chamada de vagas" entrar em escopo.

## Confirmação

- `src/selecao/Selecao.Domain/Events/Schemas/EditalPublicado.avsc` é embedded resource (verificável via `dotnet build` + `unzip -l Selecao.Domain.dll`).
- `Selecao.API/Program.cs` cria singleton `ISchemaRegistryClient` antes de `AddSchemaRegistry()`; Wolverine routing usa a mesma instância no closure.
- Smoke unit tests em `tests/Unifesspa.UniPlus.Selecao.IntegrationTests/Messaging/EditalPublicadoAvroTests.cs` cobrem schema parse + mapping + round-trip Avro binary (`SpecificDefaultWriter` ↔ `SpecificDefaultReader`).
- `tests/Unifesspa.UniPlus.Infrastructure.Core.UnitTests/Messaging/SchemaRegistry/SchemaRegistrySettingsValidatorTests.cs` cobre coerência de auth (None/Basic/OAuthBearer + cross-field).
- `docker-compose.yml` ganhou serviço `apicurio` em modo Confluent-compat para smoke local; `docker-compose.override.yml` injeta `SchemaRegistry__Url=http://apicurio:8081/apis/ccompat/v7` nas APIs.

## Mais informações

- ADR-0014 — Kafka como bus assíncrono inter-módulos (regra "cross-module via Kafka apenas")
- ADR-0044 — Roteamento de domain events PG queue + Kafka opcional (callback de routing onde se conecta o Avro serializer)
- ADR-0005 — Cascading messages (mecanismo via qual o `EditalPublicado` Avro é encadeado a partir do `EditalPublicadoEvent`)
- [uniplus-infra#163](https://github.com/unifesspa-edu-br/uniplus-infra/issues/163) — clients OIDC `uniplus-api-{portal,selecao,ingresso}` no realm
- [uniplus-infra#152](https://github.com/unifesspa-edu-br/uniplus-infra/issues/152) — chart Apicurio Registry standalone
- [Apicurio Registry — Confluent compat](https://www.apicur.io/registry/docs/apicurio-registry/3.x/getting-started/assembly-using-the-registry-client.html)
- [Confluent .NET — Schema Registry serdes](https://github.com/confluentinc/confluent-kafka-dotnet/wiki/Schema-Registry)
- [JasperFx Wolverine — Kafka transport, Schema Registry serializers](https://wolverinefx.io/guide/messaging/transports/kafka.html)
- `docs/guia-apicurio-schema-registry.md` — guia operacional com troubleshooting
- Origem: issue [#358](https://github.com/unifesspa-edu-br/uniplus-api/issues/358) — Story "Integração Apicurio Schema Registry"
