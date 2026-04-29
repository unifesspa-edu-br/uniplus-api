---
status: "accepted"
date: "2026-04-28"
decision-makers:
  - "Tech Lead (CTIC)"
  - "P.O. CEPS"
  - "P.O. CRCA"
---

# ADR-0001: Monolito modular como estilo arquitetural

## Contexto e enunciado do problema

O `uniplus-api` precisa suportar dois bounded contexts operacionais — Seleção (CEPS) e Ingresso (CRCA) — com equipes distintas, sem o overhead operacional de microservices. A equipe é pequena, a infraestrutura da Unifesspa é própria e enxuta, e os módulos têm requisito de disponibilidade para a janela de processos seletivos.

A plataforma Uni+ prevê módulos futuros (Auxílio Estudantil, Pesquisa, Mobilidade Acadêmica) que devem ser adicionados sem reescrita arquitetural.

## Drivers da decisão

- Equipe pequena (1 tech lead, 4 fullstacks, 1 Q.A.) sem experiência prévia em distributed systems.
- Infraestrutura on-premises da Unifesspa, sem cloud pública e sem orçamento para cloud-managed services.
- Necessidade de bounded contexts claros entre Seleção e Ingresso para autonomia das equipes (CEPS, CRCA).
- Roadmap de 3–5 anos com adição incremental de módulos.

## Opções consideradas

- Monolito modular (deploy independente por módulo, comunicação via eventos)
- Microservices from day one
- Monolito tradicional sem fronteiras formais
- Monorepo por camada técnica (sem bounded contexts)

## Resultado da decisão

**Escolhida:** monolito modular — cada módulo é uma aplicação .NET independente, deployável separadamente no Kubernetes, com comunicação inter-módulos exclusivamente por eventos assíncronos.

A estrutura do monorepo reflete essa separação:

```text
src/
  shared/                       # SharedKernel, Infrastructure.Common
  selecao/                      # Domain, Application, Infrastructure, API
  ingresso/                     # Domain, Application, Infrastructure, API
```

Regras invariantes:

- Módulos comunicam-se exclusivamente via eventos (Kafka) — nunca por chamadas diretas.
- Cada módulo tem suas próprias 4 camadas (ver ADR-0002).
- SharedKernel apenas para value objects, domain errors e contratos de eventos base.
- Adição de novos módulos não exige alteração nos módulos existentes.

## Consequências

### Positivas

- Operação simples — um deploy por módulo, sem service mesh ou sidecars.
- Bounded contexts visíveis na estrutura de pastas e nos namespaces.
- Custo de infraestrutura inferior ao de microservices.
- Evolução para microservices fica acessível, sem ser pré-requisito.
- Cada setor (CEPS, CRCA) detém autonomia sobre seu módulo.

### Negativas

- Módulos podem compartilhar processo se deployados juntos — falha em um pode afetar outro caso o operador os una.
- Escala horizontal é por módulo, não por aggregate root.
- Disciplina de CI/CD é necessária para garantir que deploys sejam realmente independentes.

### Neutras

- Monorepo único versus repositórios separados é decisão ortogonal — atualmente o monorepo simplifica refatorações cross-module.

## Confirmação

- Fitness test ArchUnitNET garante que tipos de `Selecao.*` não dependem de `Ingresso.*` e vice-versa (ver ADR-0012).
- Pipeline de CI valida que cada módulo compila e testa isoladamente.

## Mais informações

- ADR-0002 detalha a organização de camadas dentro de cada módulo.
- ADR-0014 define Kafka como bus assíncrono inter-módulos.
- ADR-0012 define ArchUnitNET como ferramenta de fitness tests.
- **Origem:** revisão da ADR interna Uni+ ADR-001 (não publicada).
