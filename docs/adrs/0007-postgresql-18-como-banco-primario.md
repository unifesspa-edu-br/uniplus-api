---
status: "accepted"
date: "2026-04-28"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0007: PostgreSQL 18 como banco de dados primário

## Contexto e enunciado do problema

O `uniplus-api` precisa de um SGBD OLTP confiável para dados de editais, inscrições, candidatos, notas e classificações. Os requisitos incluem transações ACID, capacidade de criptografia por coluna para dados pessoais (LGPD), suporte a JSON para formulários dinâmicos por tipo de processo seletivo e absorção de picos de inscrição (5000+ candidatos simultâneos).

## Drivers da decisão

- Conformidade ACID estrita (classificação e alocação não toleram eventual consistency).
- Custo zero de licenciamento (universidade pública, on-premises).
- Capacidade de criptografia por coluna sem ferramenta externa.
- Suporte JSON/JSONB para formulários dinâmicos.
- EF Core support maduro via Npgsql.

## Opções consideradas

- PostgreSQL 18
- SQL Server (edição Express ou paga)
- MySQL 8
- MongoDB
- CockroachDB

## Resultado da decisão

**Escolhida:** PostgreSQL 18 como banco relacional primário do `uniplus-api`, com schemas logicamente separados por módulo (Seleção, Ingresso) e schema dedicado para o outbox transacional Wolverine (ver ADR-0004).

Estratégias técnicas obrigatórias:

- **Criptografia por coluna via `pgcrypto`** para dados PII sensíveis (CPF, renda familiar, laudos médicos), em conformidade com a LGPD.
- **Soft delete** em todas as entidades — colunas `IsDeleted`, `DeletedAt`, `DeletedBy` em vez de exclusão física.
- **Audit trail em tabelas append-only** com hash chain para imutabilidade de alterações em dados de candidatos e classificações.
- **`pg_stat_statements` ativo** para identificação de queries lentas.
- **Backup via `pgBackRest` + WAL archiving** com point-in-time recovery testado.
- **Connection pool gerenciado pelo Npgsql** dimensionado para suportar picos.

## Consequências

### Positivas

- ACID estrito para dados de classificação e inscrição.
- Open source sem custo de licenciamento.
- `pgcrypto` e `pg_stat_statements` cobrem necessidades sem dependência externa.
- JSON/JSONB nativo viabiliza formulários dinâmicos por tipo de processo seletivo.
- EF Core + Npgsql é integração madura e estável.

### Negativas

- Tuning manual de `connection pool`, `shared_buffers`, `work_mem` é necessário para picos.
- Backup e HA exigem operação ativa (não é serviço gerenciado).
- Sem Transparent Data Encryption nativa — criptografia em repouso depende de FS-level (LUKS) ou `pgcrypto` por coluna.

### Riscos

- **Ponto único de falha.** Mitigado com replica read-only e failover automático via Patroni; PgBouncer como connection pooler.
- **Performance em picos.** Mitigado com connection pooling, cache Redis em frente das queries mais frequentes (ver ADR-0008) e índices adequados.

## Confirmação

- Migrations EF auditadas em PR — alterações de schema passam por revisão de DBA/plataforma.
- Health check `/health/db` valida conexão e retorno de query simples antes de aceitar tráfego.

## Mais informações

- ADR-0004 define schema `wolverine` para outbox transacional.
- ADR-0008 define Redis como cache distribuído.
- ADR-0011 define mascaramento de CPF em logs.
- ADR-0017 define K8s + Helm para deploy com cluster PG dedicado.
- **Origem:** revisão da ADR interna Uni+ ADR-009 (não publicada). Detalhes operacionais sensíveis (configuração de criptografia por coluna, política de backup) permanecem em documentação interna.
