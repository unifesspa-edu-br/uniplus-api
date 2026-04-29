---
status: "accepted"
date: "2026-04-28"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "Council multi-advisor 2026-04-24"
---

# ADR-0012: ArchUnitNET como biblioteca de fitness tests arquiteturais

## Contexto e enunciado do problema

O `uniplus-api` declara fitness tests arquiteturais como quality gate obrigatório de CI. Esses testes enforçam invariantes não-negociáveis: isolamento entre módulos (ADR-0001), conformidade de camadas (ADR-0002), encapsulamento de Wolverine dentro de `Infrastructure.Core` (ADR-0003) e ausência de qualquer referência a `MediatR.*`.

O ecossistema .NET tem dois candidatos: NetArchTest (BenMorris, MIT) e ArchUnitNET (TNG Technology Consulting, Apache 2.0).

| Sinal | NetArchTest | ArchUnitNET |
|-------|-------------|-------------|
| Última release | 23/05/2021 | cadência mensal em 2026 |
| Stewardship | mantenedor único | TNG (mesma org do ArchUnit/Java) |
| Expressividade | API type-centric com cobertura fraca de members, attributes e generics | predicados first-class para classes, interfaces, attributes, methods, fields, properties; slicing para detecção de ciclos |

A política de governança de dependências do projeto (definida na ADR-0003) prevê reavaliação quando a cadência de releases cai abaixo de 1 release por trimestre por dois trimestres consecutivos. Aplicado ao NetArchTest em 2026-04, o tripwire estaria disparado por sete trimestres consecutivos.

A equipe não tem experiência prévia com nenhuma das duas — curva de aprendizado simétrica.

## Drivers da decisão

- Cadência de manutenção compatível com horizonte de 3–5 anos do projeto.
- Stewardship organizacional, não mantenedor único.
- Expressividade da DSL para regras de members, attributes e generics (escopo futuro).
- Coerência com a própria política de governança de dependências.

## Opções consideradas

- ArchUnitNET (TNG, Apache 2.0)
- NetArchTest (BenMorris, MIT)
- Solução híbrida (NetArchTest stage 1 + ArchUnitNET stage 2+)
- Analyzers Roslyn customizados
- `NetArchTest.eNhancedEdition` (fork comunitário)

## Resultado da decisão

**Escolhida:** ArchUnitNET (TNG Technology Consulting, Apache 2.0) como biblioteca canônica única de fitness tests arquiteturais do `uniplus-api`.

`Directory.Packages.props` declara o pacote com pinning exato (não range flutuante):

```xml
<PackageVersion Include="TngTech.ArchUnitNET" Version="0.13.3" />
<PackageVersion Include="TngTech.ArchUnitNET.xUnit" Version="0.13.3" />
```

> **Nota sobre nomenclatura:** o publisher prefixa o package id com `TngTech.`, mas a namespace C# dentro dos assemblies é `ArchUnitNET.*` (sem prefixo). `<PackageReference>` usa `TngTech.ArchUnitNET`; `using ArchUnitNET.Fluent;` no código.

Cada módulo tem projeto `*.ArchTests` próprio (`Unifesspa.UniPlus.<Modulo>.ArchTests`) que chama a DSL diretamente. **Nenhuma camada wrapper especulativa.** Helpers emergem bottom-up — extrair se a mesma forma de regra for copiada em três ou mais fixtures.

Conjunto inicial mínimo de regras (todas verdes em `main` antes de qualquer outra entrega depender delas):

| # | Regra | Invariante |
|---|-------|------------|
| R1 | `Modulos_NaoSeReferenciam` | `Selecao.*` não depende de `Ingresso.*` e vice-versa |
| R2 | `ApplicationEDomain_NaoDependemDeWolverine` | `*.Application.*` e `*.Domain.*` não referenciam `Wolverine.*` |
| R3 | `Camadas_RespeitamDirecaoDeDependencia` | Domain → Application → Infrastructure → API (uma só direção) |
| R4 | `SolutionNaoTemMediatR` | nenhum tipo no monorepo referencia `MediatR.*` |

Política de upgrade:

- **Patch bumps** seguem o fluxo regular de dependências.
- **Minor bumps** abrem PR dedicado executando a suíte completa de testes de arquitetura.
- **Exit criterion:** se dois minor bumps consecutivos forçarem reescrita não-mecânica de regras, esta ADR é reaberta.

## Consequências

### Positivas

- Biblioteca canônica única — zero bikeshedding em PR review sobre escolha.
- DSL cobre as regras stage 2+ (semantic OOP, naming, regras estruturais) sem reescrita de fixtures.
- Stewardship organizacional, multi-target alinhado com release train do .NET 10.
- Pinning + política de upgrade + exit criterion tornam o risco residual auditável.

### Negativas

- Adoção de biblioteca com versão pré-1.0 — sinal de estabilidade mais fraco que 1.x+.
- Equipe paga curva de aprendizado de algumas horas para a DSL antes da primeira regra rodar.

### Riscos

- **ArchUnitNET estagnar.** Mitigado pelo exit criterion + pinning + isolamento por módulo (`*.ArchTests`).
- **Mudanças de API pré-1.0 disrompem regras existentes.** Mitigado pela política de PR dedicado para minor bump.
- **Proliferação de regras sem revisão.** Mitigado pela convenção de um projeto `*.ArchTests` por módulo + code review em qualquer PR que adicione duas ou mais fitness tests novas em uma única alteração.

## Confirmação

- Pipeline de CI executa `dotnet test` em cada projeto `*.ArchTests` e falha o build em caso de violação.
- PRs que adicionam regras novas passam por code review do tech lead.

## Prós e contras das opções

### NetArchTest

- Bom, porque licença MIT sem risco de tier comercial e API menor (curva mais rasa).
- Ruim, porque sem release há ~5 anos e cobertura fraca para regras stage 2+ que o projeto já escopou.

### Solução híbrida

- Bom, porque cada biblioteca aplicada onde é mais forte.
- Ruim, porque introduz duas DSLs, dois caminhos de upgrade e bikeshedding garantido em PR review.

### Analyzers Roslyn customizados

- Bom, porque elimina dependência externa.
- Ruim, porque equipe sem experiência em autoria de analyzers e prazo de Sprint 1 não comporta o setup.

### `NetArchTest.eNhancedEdition`

- Bom, porque é mais ativo que o upstream.
- Ruim, porque é fork comunitário com bus factor ainda mais estreito — agrava o problema de governança.

## Mais informações

- ADR-0001, ADR-0002, ADR-0003, ADR-0006 declaram invariantes que esta ADR enforça.
- [ArchUnitNET — repositório](https://github.com/TNG/ArchUnitNET)
- [ArchUnitNET — user guide](https://archunitnet.readthedocs.io/en/stable/guide/)
- **Origem:** revisão da ADR interna Uni+ ADR-023 (não publicada).
