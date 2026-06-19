---
status: "accepted"
date: "2026-06-19"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0094: Ordenação keyset na API via biblioteca de seek sob cursor opaco

## Contexto e enunciado do problema

A paginação por cursor do Uni+ usa keyset bidirecional sobre `Id` ([ADR-0089](0089-navegacao-bidirecional-cursor-keyset-reverso.md)) embrulhado num cursor opaco AES-GCM ([ADR-0026](0026-paginacao-cursor-opaco-cifrado.md)). A ordem por `Id` (Guid v7) é total e estável, mas arbitrária para apresentação.

Os endpoints de reference data de estados/cidades do `Geo` ([ADR-0090](0090-modulo-geo-localidades.md)) precisam alimentar combos (`select` de UF e cidade) em ordem **alfabética por nome**, não por `Id`. Ordenar no cliente não dá ordem global correta sob paginação (cada página é reordenada isoladamente). É preciso um keyset **multi-coluna** — chave de ordenação (nome) + `Id` de desempate — preservando a estabilidade do cursor.

A pergunta é **construir** o seek SQL multi-coluna à mão (estender o `CursorKeyset` por-`Id`) ou **adotar** uma biblioteca de keyset como motor, mantendo a nossa camada de cursor.

## Drivers da decisão

- **Keyset é o padrão** recomendado (doc oficial do EF Core; Stripe/Slack/GitHub/Relay) — a ordenação precisa ser totalmente única (chave + `Id`).
- **Correção do seek multi-coluna** — a disjunção `(k > a) OR (k = a AND id > anchor)` com `ORDER BY k, id` é sutil; um motor provado reduz o risco.
- **Reuso da nossa camada de cursor** — manter AES-GCM, TTL, user-binding e `Link` (ADR-0026/0089) intactos.
- **Menor raio de explosão** — não mexer nos consumidores que paginam por `Id`.
- **Manutenibilidade** — multi-coluna disponível para endpoints futuros sem custo de manutenção próprio.

## Opções consideradas

- **A**: Estender o `CursorKeyset` próprio para keyset multi-coluna (construir o seek à mão).
- **B**: **Adotar `MR.EntityFrameworkCore.KeysetPagination` (MIT, nativa .NET 10 / EF Core 10) como motor de seek, sob a nossa camada de cursor opaco.**
- **C**: Paginação por offset com `ORDER BY nome` (descartada — instável sob inserções, custo crescente).

## Resultado da decisão

**Escolhida:** "B — biblioteca de keyset como motor de seek, sob o nosso cursor opaco", porque delega o seek SQL multi-coluna provado a um motor mantido e de baixo nível, mantendo a nossa camada de cursor (mais robusta que o padrão de mercado) como dona do contrato.

- A biblioteca gera **apenas** o `WHERE`/`ORDER BY`/Forward-Backward + `HasPrevious`/`HasNext`; a nossa camada (`CursorPayload` + `OkPaginatedOrdenadoAsync`) mantém AES-GCM, TTL, user-binding e `Link`.
- A chave de ordenação viaja no **payload opaco** (campo `SortKey`); a âncora de continuação é a tupla `(SortKey, Id)`.
- A integração fica num helper compartilhado reutilizável (`KeysetOrdenadoCursor`, em `Infrastructure.Core/Pagination`): qualquer endpoint futuro ganha ordenação chamando-o.
- **Só os readers ordenados** (Estado/Cidade) adotam o motor; os consumidores que paginam por `Id` permanecem no `CursorKeyset` atual (menor raio de explosão).

## Consequências

### Positivas

- Motor de seek provado e mantido; menos risco que construir a disjunção multi-coluna à mão.
- Fundação reutilizável para ordenar qualquer listagem futura, multi-coluna inclusa.
- Camada de cursor e contrato wire (cursor opaco) inalterados — o front continua tratando o cursor como string opaca.

### Negativas

- Nova dependência de terceiro (mitigada: MIT, mantida, nativa .NET 10, e isolada atrás do helper).
- O índice de suporte precisa casar a expressão de `ORDER BY` gerada pelo motor (ver [ADR-0095](0095-chave-de-ordenacao-keyset-nao-nula.md)).

### Neutras

- O `CursorPayload` e o `PageRequest` compartilhados ganham um campo opcional de chave de ordenação (`null` no caminho por-`Id`), sem mudar o contrato dos consumidores existentes.

## Confirmação

- **Testes de integração** (PostGIS): página 1 alfabética; navegação next/prev sem duplicata/salto e com ordem global estável; homônimos em UFs distintas desempatados por `Id`.
- **Fitness de contrato**: a baseline OpenAPI dos endpoints paginados permanece inalterada (o cursor segue opaco; o campo de ordenação não vaza como query param).

## Prós e contras das opções

### A — estender o `CursorKeyset` próprio

- Bom, porque não adiciona dependência e mantém todo o seek sob controle direto.
- Ruim, porque reescreve à mão a disjunção multi-coluna e o tratamento de Forward/Backward que um motor provado já resolve.

### B — biblioteca de keyset como motor (escolhida)

- Bom, porque delega o seek provado e entrega uma fundação multi-coluna reutilizável, sem tocar a camada de cursor nem os consumidores por-`Id`.
- Ruim, porque adiciona uma dependência e exige casar o índice de suporte à expressão gerada pelo motor.

### C — offset com `ORDER BY nome`

- Bom, porque é trivial de implementar.
- Ruim, porque é instável sob inserções (saltos/duplicatas entre páginas) e tem custo crescente — contraria o keyset já adotado (ADR-0026/0089).

## Mais informações

- Refina [ADR-0026](0026-paginacao-cursor-opaco-cifrado.md) e [ADR-0089](0089-navegacao-bidirecional-cursor-keyset-reverso.md); a regra de chave não-nula está na [ADR-0095](0095-chave-de-ordenacao-keyset-nao-nula.md).
- Origem: story #700 (ordenação alfabética server-side de estados/cidades), follow-up da revisão do PR #699.
