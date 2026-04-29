---
status: "accepted"
date: "2026-04-28"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0008: Redis como cache distribuído

## Contexto e enunciado do problema

Mesmo com PostgreSQL como banco primário (ver ADR-0007), o `uniplus-api` precisa de cache distribuído para queries de leitura repetidas (lista de editais ativos, cursos disponíveis, vagas por modalidade), rate limiting de endpoints públicos (inscrição, consulta de resultado) e dados temporários durante processamento em lote (locks durante execução do motor de classificação).

Cache local em memória do .NET (`IMemoryCache`) não atende — múltiplas réplicas no Kubernetes precisariam compartilhar estado.

## Drivers da decisão

- Latência sub-milissegundo para leitura.
- Compartilhamento de estado entre réplicas no Kubernetes.
- Custo zero de licenciamento.
- Self-hosted, sem cloud-managed services.

## Opções consideradas

- Redis 8.x
- Memcached
- Apenas cache in-memory do .NET (`IMemoryCache`)

## Resultado da decisão

**Escolhida:** Redis 8.x como cache distribuído do `uniplus-api`.

Casos de uso e políticas:

- **Cache de queries frequentes** com TTL longo (editais ativos, cursos, vagas) — invalidação por evento de domínio.
- **Session state complementar** para fluxo de inscrição em andamento — TTL curto.
- **Rate limiting de endpoints públicos** via contadores Redis (ver ADR-0015).
- **Locks distribuídos** durante execução do motor de classificação para garantir uma única execução por edital concorrente.

A aplicação degrada graciosamente sem Redis — cai para PostgreSQL com perda de performance, mas não de correção. Isso é uma propriedade exigida do design.

## Consequências

### Positivas

- Latência sub-milissegundo para hot reads.
- Compatível com múltiplas réplicas no Kubernetes.
- Open source, custo zero.
- Estruturas de dados ricas (string, hash, sorted set) cobrem cache, rate limiting e locks com a mesma dependência.

### Negativas

- Mais um serviço para operar (mesmo que comum no ecossistema).
- Single-node perde dados em restart — mitigado por Redis Sentinel/Cluster ou persistence (RDB + AOF).
- Memória do Redis precisa ser dimensionada para o working set.

### Riscos

- **Redis como SPOF de cache.** Mitigado pelo design degradar para PostgreSQL.
- **Inconsistência cache vs. banco.** Mitigado por invalidação por domain event publicado pelo bus interno (ver ADR-0004, ADR-0005).

## Confirmação

- Health check `/health/cache` valida conectividade e RT do Redis.
- Métricas Prometheus emitidas pelo cliente .NET — hit ratio, latência p95, errors (ver ADR-0018).

## Mais informações

- ADR-0007 define PostgreSQL como banco primário (fallback do cache).
- ADR-0015 define rate limiting nativo do .NET com contadores Redis.
- ADR-0017 define K8s + Helm para o deploy.
- **Origem:** revisão da ADR interna Uni+ ADR-010 (não publicada) — split: a parte "object storage" foi extraída para a ADR-0009.
