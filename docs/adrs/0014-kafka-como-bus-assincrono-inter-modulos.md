---
status: "accepted"
date: "2026-04-28"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0014: Kafka como bus assíncrono inter-módulos e para integrações externas

## Contexto e enunciado do problema

Os módulos do `uniplus-api` (Seleção, Ingresso, futuros) precisam trocar informações ao longo do ciclo de vida do processo seletivo:

- Quando a classificação é concluída em Seleção, Ingresso precisa iniciar as chamadas de vagas.
- Quando um candidato desiste da vaga em Ingresso, Seleção atualiza a lista de espera.
- Quando há remanejamento de cotas em Seleção, Ingresso reflete a nova classificação.

Comunicação síncrona (REST entre módulos) cria acoplamento temporal — falha em um módulo trava o outro. Esta ADR define o bus assíncrono. A garantia de atomicidade entre persistência e publicação é decisão separada (ver ADR-0004 — outbox transacional).

## Drivers da decisão

- Desacoplamento temporal entre módulos.
- Log de eventos auditável e replayable.
- Self-hosted, sem cloud-managed services.
- Compatibilidade com integrações externas (sistemas federais, Gov.br, SIGAA).

## Opções consideradas

- Apache Kafka 4.2 (modo KRaft, sem ZooKeeper)
- RabbitMQ
- Comunicação síncrona via REST entre módulos
- Shared database
- AWS SNS/SQS

## Resultado da decisão

**Escolhida:** Apache Kafka 4.2 (modo KRaft) como bus assíncrono inter-módulos do `uniplus-api` e como transport externo para integrações com sistemas federais.

Regras invariantes:

- **Cross-module comm via Kafka apenas** — módulos não se chamam por REST nem compartilham tabelas.
- **Event catalog documentado** — todo domain event publicado entre módulos é registrado em catálogo compartilhado no SharedKernel.
- **Idempotência nos consumers** — todo consumer processa o mesmo evento mais de uma vez sem efeitos colaterais (deduplicação por event ID).
- **Atomicidade salvar + publicar** garantida pelo outbox transacional Wolverine (ver ADR-0004) — eventos nunca são publicados diretamente pelo handler.
- **AutoProvision em dev/test apenas.** Em produção, provisão de tópicos é responsabilidade da plataforma, conforme governança Kafka da Unifesspa.
- **Retenção configurável** para permitir replay em caso de necessidade.
- **Convenção snake_case** para nomes de tópicos.

Consumo de Kafka topics produzidos por sistemas externos (Gov.br, SIGAA, SERPRO) fica fora do escopo desta ADR — cada integração deve ser objeto de ADR específica quando aparecer.

## Consequências

### Positivas

- Desacoplamento temporal total — módulos operam independentemente.
- Log persistente — replay possível para reconstrução de estado ou debugging.
- Escalabilidade horizontal de consumers via partições.
- Auditabilidade — todo evento inter-módulo registrado no Kafka.

### Negativas

- Complexidade operacional adicional — cluster Kafka requer monitoramento próprio.
- Eventual consistency entre módulos — propagação não é instantânea.
- Debug mais difícil que chamadas síncronas — exige correlação de logs entre producer e consumer (suportada pela observabilidade — ver ADR-0018).

### Riscos

- **Kafka como infraestrutura crítica.** Mitigado com cluster (3+ brokers) em Kubernetes e monitoramento ativo.
- **Mensagens perdidas.** Mitigado pelo outbox transacional Wolverine (ADR-0004) — evento permanece na tabela `wolverine_outgoing_envelopes` até publicação confirmada.
- **Consumer lag.** Mitigado por alertas Prometheus/Grafana sobre lag por tópico e consumer group.

## Confirmação

- Health check `/health/kafka` valida conectividade dos brokers.
- Pipeline de CI roda Testcontainers com Kafka KRaft para testes de contrato.
- Métricas de lag por consumer group observáveis em Grafana (ver ADR-0018).

## Prós e contras das opções

### Kafka

- Bom, porque é log de eventos persistente com replay e escala horizontal.
- Ruim, porque complexidade operacional é não-trivial.

### RabbitMQ

- Bom, porque modelo de filas é simples e maduro em .NET.
- Ruim, porque sem replay nativo de eventos — mensagens removidas após consumo. Inadequado para event log.

### REST entre módulos

- Bom, porque é trivial de implementar.
- Ruim, porque acopla temporalmente — cascata de falhas possível.

### Shared database

- Bom, porque elimina message broker.
- Ruim, porque acopla schemas; mudança em um módulo quebra o outro.

### AWS SNS/SQS

- Bom, porque é gerenciado e baixo overhead operacional.
- Ruim, porque vendor lock-in cloud + custo recorrente + dados fora da infraestrutura institucional.

## Mais informações

- ADR-0001 estabelece monolito modular com Kafka como única forma de comunicação cross-module.
- ADR-0004 define o outbox transacional que garante atomicidade salvar + publicar.
- ADR-0007 define PostgreSQL como persistência do outbox.
- **Origem:** revisão da ADR interna Uni+ ADR-004 (não publicada) — split: a parte "outbox pattern" foi movida para a ADR-0004, que detalha a integração nativa Wolverine + EF Core.
