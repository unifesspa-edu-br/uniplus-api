---
status: "proposed"
date: "2026-05-11"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "Council Compozy (architect-advisor, pragmatic-engineer, devils-advocate, the-thinker)"
informed: []
---

# ADR-0052: Rastreabilidade cross-service via `traceparent` W3C + Serilog `ServiceName` enricher + Wolverine envelope middleware para `CorrelationId`

## Contexto e enunciado do problema

O `uniplus-api` é composto por três APIs .NET (Selecao, Ingresso, Portal) que se comunicam por HTTP síncrono e via Kafka (transport Wolverine, outbox transacional — ver ADR-0004 e ADR-0005). Durante picos de inscrição, oncall, suporte e auditoria LGPD precisam reconstruir o fluxo de uma requisição que atravessa essas APIs: do header recebido no edge até o handler que processou um command no consumer Kafka, hora ou horas depois.

O backbone OpenTelemetry já está instalado (ADR-0018) e wired em ambas as APIs via `AdicionarObservabilidade(...)`. O middleware `CorrelationIdMiddleware` (Infrastructure.Core, mergeado em PR #102) propaga o header HTTP `X-Correlation-Id` e injeta a propriedade `CorrelationId` no `Serilog.LogContext`. A pipeline Serilog OTLP (Story #105, mergeada) adiciona `service.name` e `service.namespace` no `Resource` do sink quando o nome do serviço é passado em `ConfigurarSerilog(...)`.

Apesar dessas peças, há três pontos de fricção que esta ADR resolve:

1. **`CorrelationId` não atravessa o Kafka.** Auditoria do código (`WolverineOutboxConfiguration.cs`) confirma que nenhum middleware Wolverine copia o `X-Correlation-Id` para os headers do envelope ou o reidrata no `LogContext` do consumer. Quando um command sai de Selecao → outbox → Kafka → handler em Ingresso, o `CorrelationId` Serilog **não chega no destinatário**. Sobra apenas o `traceparent` W3C — que, sob a política de amostragem `ParentBased(TraceIdRatioBased(0.1))` em produção (ADR-0018), aponta para um span pai que foi descartado em 90% das requisições. O ID flutua, o trace fica órfão e a investigação manual depende de regex sobre body de log.
2. **`ServiceName` não aparece em cada log line.** O valor `uniplus-{selecao,ingresso,portal}` vive no `Resource` do sink OTLP, que o Loki traduz para a label `service_name` no índice. Já em consumidores fora do pipeline OTLP — `Console` em desenvolvimento, exportações JSON para auditoria, agregadores que não consomem Resource attributes — a origem do log não é nominal e o operador tem que inferir pelo `k8s_pod_name`.
3. **Convenção indefinida entre `ServiceName` (Serilog Pascal) e `service.name` (OTel semantic conventions).** Diferentes módulos podem cunhar nomes próprios, criando drift entre logs e traces.

A issue `unifesspa-edu-br/uniplus-docs#65` (fechada, escopo migrado para o repo de contexto — ver `feedback_adrs_por_projeto` na memória do time) propunha três opções: (A) UUID puro com `ServiceName` estruturado, (B) header HTTP custom `X-Uniplus-Service-Origin` + header Kafka custom, (C) híbrida. Esta ADR escolhe e refina.

## Drivers da decisão

- Reutilizar o backbone já em produção (`OpenTelemetry SDK`, `Serilog OTLP sink`, `CorrelationIdMiddleware`, `Wolverine.Kafka` com `AddSource("Wolverine")`) — qualquer convenção que duplique mecanismos paga maintenance em todo refactor.
- Alinhamento com OpenTelemetry Semantic Conventions e o padrão W3C `traceparent`, que é o que ServiceMesh, eBPF tracers e instrumentações de banco/cache passam a falar nativamente.
- Conformidade LGPD: forense de até 90 dias precisa reconstruir o fluxo cross-service a partir dos logs apenas (retenção de spans é 30 dias por ADR-0018; logs em Loki ficam 90 dias por política institucional).
- Compatibilidade futura com a ADR de RUM do `uniplus-web` (instrumentação frontend), que deverá propagar `traceparent` nativo via OpenTelemetry JS SDK.
- LGPD (ADR-0011): qualquer enricher novo executa antes dos sinks, depois do `PiiMaskingEnricher`.

## Opções consideradas

- **Opção A' (escolhida)** — `traceparent` W3C primário + `ServiceNameEnricher` Serilog + Wolverine envelope middleware para `CorrelationId`.
- **Opção B** — Header HTTP custom `X-Uniplus-Service-Origin` + header Kafka custom, independentes do OTel stack.
- **Opção C** — Híbrida (A + B).
- **Opção A (versão inicial proposta no rascunho)** — A' sem o Wolverine envelope middleware; mantém `CorrelationIdMiddleware` inalterado. Rejeitada após council porque deixa a lacuna do Kafka aberta.

## Resultado da decisão

**Escolhida: Opção A'** — porque (1) reutiliza integralmente o que já está em produção sem introduzir um wire-format paralelo, (2) tapa a lacuna real (propagação de `CorrelationId` via Kafka) com um middleware Wolverine pequeno (~50 LOC) em vez de um header HTTP custom que duplicaria responsabilidades, (3) preserva a interoperabilidade com ServiceMesh/eBPF/futuro RUM via `traceparent` W3C, e (4) entrega `ServiceName` em cada log line para consumidores não-OTLP (Console em dev, JSON export para auditoria) com um enricher Serilog de uma linha.

A decisão tem três componentes obrigatórios, integrados:

### 1. `traceparent` W3C como mecanismo primário de correlação técnica

`traceparent` continua sendo propagado automaticamente pelas instrumentações OpenTelemetry já wired:

- `AddAspNetCoreInstrumentation()` para HTTP ingress
- `AddHttpClientInstrumentation()` para HTTP egress (incluindo chamadas para Gov.br, Keycloak admin, SIGAA)
- `AddSource("Wolverine")` para mensagens Kafka via Wolverine — o pacote Wolverine.Kafka injeta o `traceparent` em envelope headers nativamente quando o `ActivitySource` está registrado

Nenhum header HTTP ou Kafka é cunhado por Uni+. Não há `X-Uniplus-Service-Origin`.

### 2. `ServiceNameEnricher` Serilog populando `ServiceName` em cada log event

Novo enricher em `Infrastructure.Core/Logging/ServiceNameEnricher.cs`:

```csharp
public sealed class ServiceNameEnricher : ILogEventEnricher
{
    private readonly string _serviceName;
    public ServiceNameEnricher(string serviceName) => _serviceName = serviceName;
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory)
        => logEvent.AddOrUpdateProperty(factory.CreateProperty("ServiceName", _serviceName));
}
```

Registrado dentro de `ConfigurarSerilog(...)` quando `nomeServico != null`, **antes** do bloco `WriteTo.Console` e `WriteTo.OpenTelemetry`. O valor passado é exatamente o mesmo string consumido pelo `Resource` do OTel SDK (single source of truth — não duplica, sincroniza).

Nomenclatura: a propriedade Serilog é `ServiceName` (PascalCase, alinhada com `CorrelationId`). O atributo OTel Resource é `service.name` (dot-notation, semantic conventions). O sink Serilog OTLP traduz a property para o Resource attr no protocolo wire — não há divergência operacional.

Consumidores nomeados de `ServiceName` como Serilog property (fora do `service_name` label do Loki):

- **Console em desenvolvimento** — `docker logs` e `dotnet run` mostram cada line prefixada com o serviço, sem precisar inferir do PID.
- **Exportações de auditoria** que serializam log events para JSON e são consumidas por ferramentas não-OTel (CSV de TCU, dumps para o setor jurídico).

Caso esses consumidores deixem de existir no futuro, o enricher pode ser removido em ADR de revisão sem impacto no pipeline OTLP — a Resource attr permanece como verdade canônica.

### 3. Wolverine envelope middleware para propagar `CorrelationId` em Kafka

Lacuna identificada pelo Devil's Advocate do council. Solução em duas peças simétricas:

**Produtor (qualquer handler que envia via `IMessageBus`):** envelope outbox middleware copia `Serilog.LogContext` → header `uniplus.correlation-id` no envelope persistido em `wolverine_outgoing_envelopes`. Quando o dispatcher Wolverine drena e envia para Kafka, esse header viaja com o payload.

**Consumidor (handler que recebe da fila Kafka):** envelope middleware lê `uniplus.correlation-id` do envelope recebido, valida com a mesma regex de `CorrelationIdMiddleware` (`^[A-Za-z0-9\-_.]{1,128}$`), faz `LogContext.PushProperty("CorrelationId", valor)` no escopo do handler, e propaga também para `Activity.Current?.SetTag("correlation_id", ...)`. Se o header está ausente ou inválido (cenário fallback — produtor antigo, evento sem origem), o middleware gera um novo GUID e segue.

Por que não usar OTel Baggage. Baggage é outra solução possível para propagar contexto de aplicação cross-service. Não escolhida porque (a) Baggage só sobrevive enquanto o `ActivityContext` sobrevive — sob sampling agressivo o contexto pode ser descartado, (b) `CorrelationId` é dado de negócio independente do trace técnico (oncall lê o GUID em um e-mail do candidato antes de abrir o Grafana), (c) explicitar o header Wolverine deixa o contrato claro no código sem dependência implícita do SDK.

### `CorrelationIdMiddleware` HTTP — permanece inalterado

O middleware existente fica como está: preserva upstream se válido, gera GUID caso contrário, escreve no `Serilog.LogContext` e no `Activity.Current` tag, ecoa no response header `X-Correlation-Id`. Não há sunset agora. Quando a ADR de RUM do `uniplus-web` aterrissar e definir se o frontend emite `traceparent` nativo ou continua propagando `X-Correlation-Id`, uma ADR de revisão pode decidir.

## Consequências

### Positivas

- Reuso máximo do backbone em produção: nenhum header novo no contrato HTTP, nenhuma mudança no `CorrelationIdMiddleware`, nenhuma nova convenção de wire format.
- Tapa a lacuna real de `CorrelationId` em Kafka com escopo limitado — duas peças simétricas no Wolverine envelope, ~50 LOC, testáveis com TestContainers.
- `ServiceName` torna-se visível em todos os sinks Serilog atuais e futuros sem mudar o protocolo OTLP.
- Compatibilidade automática com ServiceMesh (Istio/Linkerd injetam `traceparent` nativo), eBPF tracers (lêem `traceparent` do kernel hook), pgBouncer e outras integrações que entendem W3C.
- Frontend RUM (futura ADR do `uniplus-web`) pode emitir `traceparent` browser-side via OpenTelemetry JS SDK e a correlação ponta a ponta é automática.
- LGPD: `ServiceName` é dado público (`uniplus-selecao`); `PiiMaskingEnricher` continua precedendo qualquer sink (ADR-0011); nenhum atributo novo expõe PII.

### Negativas

- Sob amostragem 10% em produção, traces de fluxos assíncronos (HTTP → outbox → Kafka → consumer) ficam órfãos em 90% dos casos — span pai foi descartado quando o consumer roda. Mitigação: `CorrelationId` propagado via Wolverine middleware fica em 100% das linhas de log, permitindo a investigação por `correlation_id` em LogQL mesmo sem trace correspondente em Tempo.
- Drift latente: alguém pode mudar o nome do serviço em `Program.cs` (`AdicionarObservabilidade("uniplus-selecao", ...)`) sem perceber que o enricher carrega o mesmo valor. Mitigação: fitness test no startup verifica que `ServiceName` Serilog property e `service.name` OTel Resource são idênticos.
- O enricher é um layer adicional na pipeline Serilog (após `PiiMaskingEnricher`, antes dos sinks). Custo de CPU desprezível, mas custo cognitivo: o pipeline cresce.
- Manter `CorrelationIdMiddleware` e propagação Wolverine simultânea ao `traceparent` é defesa em profundidade — não simplificação. Aceito como custo até a ADR de RUM definir o sunset.

### Neutras

- Propriedade `ServiceName` (Serilog) duplica `service.name` (OTel Resource) em conteúdo, intencionalmente. Independência entre pipelines (Console vs OTLP) é o motivo, não desleixo.
- Em testes (`ApiFactoryBase` e derivados), `AdicionarObservabilidade` precisa ser chamada com um nome padrão (`"test"` ou similar) para o enricher não emitir `null`. Convenção a documentar no `ApiFactoryBase`.

## Confirmação

Esta ADR é enforçável via três fitness tests obrigatórios:

1. **Enricher registrado e propriedade presente**: teste em `Infrastructure.Core.UnitTests/Logging/` que `ConfigurarSerilog(config, "uniplus-selecao")` produz um pipeline cujos log events contêm a property `ServiceName="uniplus-selecao"`.
2. **Cross-service end-to-end via Wolverine + Kafka**: integration test em `tests/Unifesspa.UniPlus.Selecao.IntegrationTests/` usando TestContainers (Postgres + Kafka): handler em Selecao publica command que vira evento Kafka, handler em Ingresso consome, assert que (a) `X-Correlation-Id` setado no HTTP de entrada da Selecao aparece no `LogContext` do handler de Ingresso, (b) `traceparent` no envelope Kafka reconstroi o span pai sob 100% sampling, (c) sob 10% sampling explicitamente forçado, `CorrelationId` continua visível mesmo quando o span pai foi descartado.
3. **Drift `ServiceName` Serilog × `service.name` OTel**: startup self-check em `AdicionarObservabilidade` que falha com `InvalidOperationException` se as duas referências divergirem (uma única string-source de verdade em produção, validada em test).

Smoke E2E visual (logs Loki → derivedFields → trace Tempo) é coberto pela issue `uniplus-infra#227`.

## Prós e contras das opções

### Opção A' — `traceparent` W3C primário + `ServiceNameEnricher` + Wolverine envelope middleware (escolhida)

- **Bom**, porque reutiliza o backbone em produção (OTel SDK + Wolverine.Kafka + Serilog OTLP) sem cunhar wire format paralelo.
- **Bom**, porque tapa explicitamente a lacuna real (`CorrelationId` em Kafka) sem inventar header HTTP custom.
- **Bom**, porque é compatível com ServiceMesh, eBPF tracers, futuro RUM frontend e qualquer integrador externo que fale W3C.
- **Bom**, porque LGPD-friendly: `ServiceName` é público, `PiiMaskingEnricher` continua precedendo sinks, retenção forense de 90 dias em logs é suficiente para reconstruir fluxos.
- **Ruim**, porque sob 10% sampling em prod, traces de fluxos assíncronos ficam órfãos em 90% dos casos — depende-se de `CorrelationId` (via envelope) para reconstrução forense quando a janela cai fora da retenção de spans.
- **Ruim**, porque mantém três camadas (`traceparent`, `CorrelationId`, `service.name` Resource) — defesa em profundidade, não simplicidade.

### Opção B — Header HTTP custom `X-Uniplus-Service-Origin` + Kafka header custom

- **Bom**, porque independente do OTel stack — funciona mesmo se Tempo está offline.
- **Bom**, porque visível para consumidores externos sem OTel SDK.
- **Ruim**, porque cria wire format paralelo que precisa ser mantido em todo client, integrador externo e RFC futura.
- **Ruim**, porque duplica responsabilidade do `traceparent` (que já está na pipeline) — drift entre os dois é certeza com o tempo.
- **Ruim**, porque ServiceMesh, eBPF tracers e tools que falam W3C não conhecem o header — toda integração paga uma camada de tradução.
- **Ruim**, porque cunhar um header `X-Uniplus-*` é decisão difícil de reverter quando consumers externos passam a depender dele.

### Opção C — Híbrida (A + B)

- **Bom**, porque defesa em profundidade extra: traceparent + correlationid + serviceorigin.
- **Ruim**, porque carrega todo o custo de B sem ganho proporcional — `CorrelationId` via Wolverine envelope já fornece o canal independente do OTel stack que B propunha.
- **Ruim**, porque pipeline cresce sem necessidade — três camadas viram quatro, e cada uma exige fitness test e operação consistente.

### Opção A (rascunho inicial pré-council)

- **Bom**, porque mais enxuta que A'.
- **Ruim**, porque deixa `CorrelationId` morrer no boundary Kafka — lacuna identificada pelo Devil's Advocate. Logs do consumer ficam sem âncora de negócio quando o span pai cai fora do sampling.
- **Rejeitada** após o council embedded.

## Mais informações

- ADR-0011 (mascaramento de CPF em logs) — `PiiMaskingEnricher` precede o `ServiceNameEnricher` na pipeline Serilog. Ordem documentada em `SerilogConfiguration.cs`.
- ADR-0018 (OpenTelemetry para instrumentação do backend) — fundação que esta ADR estende.
- ADR-0004 (outbox transacional via Wolverine) — Wolverine envelope é onde o middleware desta ADR opera.
- ADR-0005 (cascading messages para drenagem de domain events) — fluxos assíncronos cuja correlação esta ADR garante.
- ADR-0014 (Kafka como bus assíncrono inter-módulos) — transport sobre o qual o middleware envelope propaga `CorrelationId`.
- `uniplus-infra/docs/adrs/ADR-013-otel-collector-daemonset-standalone.md` — collector que consome os traces e logs deste pipeline.
- Story dependente: `uniplus-api#101` — implementação do enricher e do middleware Wolverine descritos aqui. Esta ADR substitui a referência a `unifesspa-edu-br/uniplus-docs#65` (fechada como `not planned`, escopo migrado para este repo).
- Smoke E2E visual: `uniplus-infra#227` — Loki → derivedFields → Tempo, com `CorrelationId` e `TraceId` visíveis no Grafana.
- **Origem**: revisão e refinamento via council embedded do Compozy (advisors `architect-advisor`, `pragmatic-engineer`, `devils-advocate`, `the-thinker`). O council descobriu a lacuna do `CorrelationId` em Kafka que o rascunho inicial (Opção A) não cobria — A' é a versão entregue após esse feedback.
