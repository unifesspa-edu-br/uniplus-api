---
status: "proposed"
date: "2026-10-11"
decision-makers:
  - "Tech Lead"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0144: A regra de aplicabilidade pertence à modalidade

## Contexto e enunciado do problema

O conjunto de modalidades em que o candidato concorre é derivado das respostas dele (UNI-REQ-0076). É preciso decidir onde mora a regra que diz quem concorre a cada modalidade: na configuração de cada processo seletivo, na regra de distribuição de vagas ou na própria modalidade.

## Drivers da decisão

- Uma fonte única para cada regra (UNI-REQ-0075, UNI-REQ-0076).
- A regra é configuração congelada na publicação, não ramificação em código (UNI-REQ-0076).
- Toda modalidade ofertada tem a sua regra, inclusive a de rol aberto (UNI-REQ-0011).

## Opções consideradas

- **A.** Cada processo grava a própria cópia da matriz de derivação da modalidade.
- **B.** A regra de distribuição de vagas declara a derivação do seu rol.
- **C.** Cada modalidade carrega no cadastro a própria regra de aplicabilidade.

## Resultado da decisão

**Escolhida:** "C". A regra de aplicabilidade pertence à modalidade no cadastro e é congelada com ela quando o processo a oferta. A derivação da modalidade do candidato é a união das regras das modalidades ofertadas.

## Consequências

### Positivas

- A regra existe em um lugar só, e uma modalidade nova traz a sua.
- O processo não tem como ofertar modalidade sem regra.

### Negativas

- A modalidade institucional criada pelo administrador exige que ele declare a regra.

## Prós e contras das opções

### A. Cópia por processo

- Bom, porque permite ajuste por processo.
- Ruim, porque separa a oferta da regra e permite publicar modalidade sem regra.

### B. Regra na distribuição de vagas

- Bom, porque une oferta e regra.
- Ruim, porque repete a regra da mesma modalidade em cada distribuição que a admite e não cobre o rol aberto.

### C. Regra na modalidade

- Bom, porque cada modalidade responde pela própria aplicabilidade.
- Ruim, porque exige que toda modalidade cadastrada declare a regra.

## Mais informações

- [ADR-0136](0136-catalogo-de-fatos-do-candidato-administravel.md), [ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md).
