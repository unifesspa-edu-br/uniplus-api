---
status: "accepted"
date: "2026-04-28"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "Council multi-advisor 2026-04-24"
---

# ADR-0003: Wolverine como backbone CQRS in-process

## Contexto e enunciado do problema

O `uniplus-api` precisa de um backbone para despachar commands, executar handlers de domain events e (em ADR separada) suportar outbox transacional sobre `EntityFrameworkCore`. A escolha do framework é independente da decisão de adotar Clean Architecture com CQRS (ADR-0002).

Três fatos forçam a decisão:

1. **MediatR** migrou para licenciamento comercial em 02/07/2025 (Lucky Penny Software, dual-license RPL 1.5 + tiers comerciais por tamanho de equipe). Procurement de autarquia federal não absorve licenças comerciais por desenvolvedor.
2. **MassTransit v9** virou comercial em Q1/2026 (Massient, Inc.); v8 fica em Apache 2.0 mas com EOL anunciado para fim de 2026.
3. **Construir in-house** (dispatcher + outbox + retries + sagas) foi explicitamente rejeitado pela governança como "alto custo de manutenção e reinvenção da roda".

A equipe não tem experiência prévia com Wolverine, Brighter ou Rebus — a curva de aprendizado é simétrica entre os candidatos.

## Drivers da decisão

- Licenciamento permissivo (MIT/Apache 2.0/BSD) sem tier comercial.
- Suporte first-class a outbox transacional sobre EF Core (decisão técnica de outbox em ADR-0004).
- Cadência ativa de releases e bus factor saudável (horizonte de projeto: 3–5 anos).
- Substituibilidade — código de aplicação não pode importar tipos do framework diretamente.

## Opções consideradas

- Wolverine (MIT, JasperFx)
- Brighter (Apache 2.0, comunidade independente)
- Continuar em MediatR sob RPL 1.5
- Implementar dispatcher + outbox in-house

## Resultado da decisão

**Escolhida:** Wolverine (MIT, JasperFx) como backbone CQRS in-process, sobre `EntityFrameworkCore` 10 + PostgreSQL.

Apenas dois contratos vivem em `Application.Abstractions/Messaging/`:

```csharp
public interface ICommandBus
{
    Task<TResponse> Send<TResponse>(
        ICommand<TResponse> command,
        CancellationToken ct = default);
}

public interface IDomainEventDispatcher
{
    Task Publish(
        IDomainEvent domainEvent,
        CancellationToken ct = default);
}
```

Implementações ficam em `Infrastructure.Core/Messaging/` e encapsulam o `IMessageBus` do Wolverine. Código de aplicação e domínio **não importam `Wolverine.*` diretamente**.

Capacidades avançadas (sagas, process managers, scheduled messages, event sourcing) ficam **deliberadamente fora** desta decisão e só entram no projeto quando um caso de uso concreto exigir, com emenda a esta ADR.

## Consequências

### Positivas

- Time pode produzir novos commands e handlers seguindo um único exemplo funcional.
- Outbox transacional é capacidade nativa do framework (ver ADR-0004).
- Contrato mínimo de duas abstrações mantém código de aplicação portável para Brighter como escape-hatch.
- Padrão CQRS preservado (ADR-0002 intacto).

### Negativas

- Zero experiência prévia da equipe — curva de aprendizado paga em trabalho real.
- Dependência da cadência de manutenção da JasperFx Software.
- Ecossistema CritterStack (Marten, CritterWatch) não pode virar dependência implícita — apenas Wolverine standalone.

### Riscos

- **Bus factor da JasperFx.** Mitigado pelo encapsulamento via `ICommandBus`/`IDomainEventDispatcher` — migração para Brighter custa 2–6 semanas conforme uso de features avançadas. Brighter fica nomeado como escape-hatch.
- **Features avançadas vazando para handlers.** Mitigado pela regra arquitetural enforçada por ArchUnitNET (ver ADR-0012) que proíbe imports de `Wolverine.*` fora de `Infrastructure.Core`.
- **Drift do CritterWatch como dependência implícita.** Observabilidade fica em OpenTelemetry (ver ADR-0018); `CritterWatch` é proibido como dependência load-bearing.

## Confirmação

- Fitness test ArchUnitNET `ApplicationLayer_DoesNotDependOnWolverine` falha o build se houver import fora de `Infrastructure.Core` (ver ADR-0012).
- Fitness test `SolutionDoesNotReferenceMediatR` falha o build se houver qualquer import de `MediatR.*`.

## Prós e contras das opções

### Wolverine

- Bom, porque colapsa CQRS, outbox e Kafka transport em um framework coerente.
- Ruim, porque o ecossistema CritterStack pode pressionar adoção implícita de outras peças.

### Brighter

- Bom, porque tem bus factor maior e zero risco de tier comercial.
- Ruim, porque sagas exigem projeto separado (Darker), aumentando o footprint mental.

### MediatR sob RPL 1.5

- Bom, porque time já familiarizado.
- Ruim, porque licenciamento RPL 1.5 carece de avaliação jurídica e qualquer tier pago futuro é inviável para procurement.

### Dispatcher in-house

- Bom, porque elimina dependência externa.
- Ruim, porque outbox + retries + idempotência + Kafka producer + estado de saga reinventa um framework mantido — incompatível com equipe pequena em horizonte longo.

## Mais informações

- ADR-0004 define o outbox transacional sobre Wolverine + EF Core.
- ADR-0005 define a estratégia de drenagem de domain events via cascading messages.
- ADR-0012 define ArchUnitNET como ferramenta de enforcement.
- ADR-0018 define OpenTelemetry como observabilidade obrigatória.
- [Wolverine documentation](https://wolverinefx.net/)
- [Brighter (escape-hatch nomeado)](https://github.com/BrighterCommand/Brighter)
- **Origem:** revisão das ADRs internas Uni+ ADR-002 (parte CQRS) e ADR-022 (não publicadas).
