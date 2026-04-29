---
status: "accepted"
date: "2026-04-28"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0002: Clean Architecture com quatro camadas por módulo

## Contexto e enunciado do problema

Cada módulo de negócio do `uniplus-api` (Seleção, Ingresso e os futuros) tem regras complexas e sensíveis: cotas com remanejamento em cascata, fórmulas configuráveis por edital, múltiplas etapas de homologação, máquinas de estado de inscrição. Essas regras precisam ser testáveis isoladamente, sem dependência de banco, broker ou qualquer infraestrutura externa.

A equipe terá rotatividade (servidores públicos, estagiários), portanto a arquitetura precisa ser previsível: todo caso de uso segue o mesmo padrão estrutural.

Esta ADR trata exclusivamente da **organização de camadas** do módulo. A escolha do framework de mediação CQRS é decisão separada (ver ADR-0003).

## Drivers da decisão

- Regras de negócio densas e auditáveis (Lei de Cotas, RN08 — congelamento de parâmetros).
- Testes unitários do domínio devem rodar sem banco, sem container, sem broker.
- Previsibilidade estrutural para acomodar rotatividade de equipe.
- Necessidade de separar policy (domínio) de mecanismo (infraestrutura).

## Opções consideradas

- Clean Architecture com quatro camadas (Domain → Application → Infrastructure → API)
- Vertical Slice Architecture
- N-tier tradicional (Controller → Service → Repository)
- Hexagonal / Ports and Adapters

## Resultado da decisão

**Escolhida:** Clean Architecture com quatro camadas por módulo, com fluxo de dependências exclusivamente para dentro.

| Camada | Responsabilidade | Dependências |
|--------|------------------|--------------|
| Domain | entities, value objects, domain events, regras de negócio | nenhuma |
| Application | use cases, handlers, DTOs, interfaces de infraestrutura | Domain |
| Infrastructure | EF Core, persistência, integrações externas | Domain, Application |
| API | controllers, middleware, configuração | Application, Infrastructure |

Padrões obrigatórios na camada Application e Domain:

- **CQRS:** commands alteram estado; queries leem dados. Separação explícita.
- **Validação:** FluentValidation antes da execução do handler.
- **Result\<T\> pattern** com `DomainError` — sem exceções para fluxos esperados de negócio.
- **Entities** com factory methods e construtores privados — nunca `new` fora da própria classe.
- **Sealed classes** por padrão.
- **File-scoped namespaces** em todo o código.

## Consequências

### Positivas

- Domínio testável como função pura — regras de cotas e classificação rodam sem infraestrutura.
- Estrutura previsível por convenção — todo handler segue Command → Validator → Handler → Result.
- Fácil substituir implementações de infraestrutura sem tocar regra de negócio.
- Onboarding facilitado pela uniformidade entre módulos.

### Negativas

- Mais boilerplate por caso de uso (Command, Validator, Handler, DTO).
- Curva de aprendizado para quem não conhece Clean Architecture.
- Indireção pode dificultar leitura de fluxos por desenvolvedores juniores.

## Confirmação

- Fitness tests ArchUnitNET enforçam direção de dependência entre camadas (ver ADR-0012).
- Pull request review verifica conformidade com padrões obrigatórios.

## Prós e contras das opções

### Vertical Slice Architecture

- Bom, porque agrupa tudo que pertence à mesma feature.
- Ruim, porque a estrutura de cada slice fica a critério do autor — perde previsibilidade em equipe com rotatividade.

### N-tier tradicional

- Bom, porque é familiar a desenvolvedores .NET legados.
- Ruim, porque domínio acopla a infraestrutura — classificação dependeria de EF Core para ser testada.

### Hexagonal puro

- Bom, porque também isola domínio de infraestrutura.
- Ruim, porque sem CQRS as queries complexas competem com commands no mesmo modelo.

## Mais informações

- ADR-0003 define o framework de CQRS (Wolverine).
- ADR-0012 define ArchUnitNET para enforcement das regras de dependência.
- **Origem:** revisão da ADR interna Uni+ ADR-002 (não publicada) — split: o componente "Clean Architecture" desta ADR vem da ADR-002 antiga; o componente "framework CQRS" foi extraído para a ADR-0003 atual.
