---
status: "accepted"
date: "2026-04-28"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0018: OpenTelemetry para instrumentação do `uniplus-api`

## Contexto e enunciado do problema

O `uniplus-api` precisa de observabilidade end-to-end durante picos de inscrição. Traces, métricas e logs precisam ser correlacionados por um único `traceId` que atravesse a API, handlers Wolverine, consumers Kafka e queries no PostgreSQL. Sem instrumentação estruturada, problemas de latência, erros silenciosos e gargalos no motor de classificação passam despercebidos até virarem falhas reportadas por candidatos.

A stack de armazenamento (Loki, Grafana, Tempo, Prometheus) é decisão de plataforma — esta ADR cobre o lado do `uniplus-api` (instrumentação backend). A instrumentação frontend (RUM) é decisão própria do `uniplus-web`.

## Drivers da decisão

- Padrão aberto sem vendor lock-in.
- Correlação `traceId` entre API, handlers, consumers e banco.
- Conformidade LGPD com PII masking no pipeline antes da exportação.
- Alertas proativos antes que problemas afetem candidatos.

## Opções consideradas

- OpenTelemetry SDK + OTLP exporter para stack LGTM (Loki, Grafana, Tempo, Prometheus)
- ELK stack (Elasticsearch, Logstash, Kibana)
- Datadog ou New Relic (SaaS)
- Jaeger para traces (sem stack unificada)
- Application Insights (.NET nativo)

## Resultado da decisão

**Escolhida:** OpenTelemetry como padrão de instrumentação do `uniplus-api`. Traces, métricas e logs exportados via OTLP para o OpenTelemetry Collector institucional, que roteia para Tempo (traces), Prometheus/Mimir (métricas) e Loki (logs).

Componentes obrigatórios do `uniplus-api`:

- **Auto-instrumentação .NET** — ASP.NET Core, EF Core, HttpClient, Npgsql, Wolverine.
- **Métricas custom de negócio** — Kafka consumer lag, duração de jobs de classificação, tempo de processamento de documentos, inscrições por minuto, taxa de rejeição na homologação.
- **Logging estruturado via Serilog** com `PiiMaskingEnricher` (ver ADR-0011) — campos sensíveis mascarados antes da exportação para Loki.
- **Sampling adaptativo** — head-based 10% para tráfego normal; tail-based 100% para erros e latência alta.
- **Métricas Wolverine** — `wolverine_outgoing_envelopes`, `wolverine_dead_letters`, `wolverine_node_assignments` (ver ADR-0004).

SLOs declarados:

- Latência API p95 < 2 s
- Disponibilidade ≥ 99,5%
- Taxa de erros < 0,5%

Alertas obrigatórios em Grafana:

- Crescimento sustentado de `wolverine_outgoing_envelopes` por mais de 5 min — sinal de lag de despacho.
- `wolverine_dead_letters` > 10/h por handler (threshold inicial — calibrar após 30 dias).
- Envelopes em `wolverine_node_assignments` com node owner morto há > 5 min — ownership órfão.
- Consumer lag Kafka acima de threshold por tópico/consumer group.

Política de retenção (definida em política institucional, registrada aqui para referência):

| Categoria | Retenção |
|-----------|----------|
| Logs de segurança (auth, autorização, auditoria) | 5+ anos |
| Logs de aplicação | 90 dias |
| Logs de debug | 30 dias |
| Traces | 30 dias |
| Métricas | 1 ano (downsampled após 90 dias) |

## Consequências

### Positivas

- Padrão aberto — instrumentação fica mesmo se backend de armazenamento mudar.
- Correlação `traceId` end-to-end — request frontend → consumer Kafka → query PostgreSQL.
- PII masking integrado (ADR-0011) — conformidade LGPD por construção.
- Alertas proativos antes de candidatos perceberem problemas.

### Negativas

- Configuração inicial complexa — receivers, processors e exporters do Collector exigem conhecimento.
- Curva de aprendizado para PromQL e LogQL.
- Volume de logs/traces pode ser significativo — exige sampling e política de retenção.

### Riscos

- **Overhead de instrumentação em hot paths.** Mitigado por sampling adaptativo (head-based 10% normal, tail-based 100% para erro/latência).
- **Storage crescendo indefinidamente.** Mitigado por retenção configurável e compactação automática no Loki; debug desabilitado em produção por default.
- **Collector como SPOF.** Mitigado por deploy em DaemonSet (um por nó) com buffer local em caso de indisponibilidade do backend.

## Confirmação

- Health check `/health/observability` valida que o exporter OTLP consegue enviar pelo menos um span de teste.
- Pipeline de CI executa testes que verificam emissão de traces e métricas em cenários conhecidos.
- Dashboards Grafana institucionais cobrem os SLOs declarados.

## Prós e contras das opções

### OpenTelemetry + LGTM

- Bom, porque é padrão aberto, OSS, sem vendor lock-in.
- Ruim, porque exige operação ativa da stack (Collector, Tempo, Prometheus, Loki, Grafana).

### ELK

- Bom, porque é stack tradicional madura.
- Ruim, porque Elasticsearch consome muita memória; licenciamento mudou para SSPL (não OSS puro); Loki é mais leve para logs.

### Datadog/New Relic

- Bom, porque elimina operação.
- Ruim, porque custo proibitivo, dados fora da infraestrutura, vendor lock-in.

### Jaeger

- Bom, porque é OSS específico para tracing.
- Ruim, porque Tempo integra melhor com Grafana e suporta backend S3/MinIO; Jaeger requer Elasticsearch ou Cassandra.

### Application Insights

- Bom, porque é nativo do .NET e tem auto-instrumentação rica.
- Ruim, porque é vendor lock-in Azure, sem correlação nativa com frontend Angular, custo de cloud.

## Mais informações

- ADR-0003 e ADR-0004 detalham capacidades Wolverine que esta instrumentação cobre.
- ADR-0011 define o `PiiMaskingEnricher` que opera no pipeline Serilog antes da exportação.
- ADR-0014 define Kafka como bus instrumentado.
- ADR-0017 define o cluster Kubernetes onde Collector e backends executam.
- **Origem:** revisão da ADR interna Uni+ ADR-014 (não publicada) — split: a parte "instrumentação frontend (RUM)" foi separada e cabe ao `uniplus-web`.
