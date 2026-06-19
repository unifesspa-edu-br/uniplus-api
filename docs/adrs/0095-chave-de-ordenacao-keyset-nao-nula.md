---
status: "accepted"
date: "2026-06-19"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0095: Chave de ordenação keyset não-nula via coalesce

## Contexto e enunciado do problema

A ordenação keyset multi-coluna ([ADR-0094](0094-keyset-ordenado-via-mr-sob-cursor-opaco.md)) usa uma chave de ordenação (ex.: `nome_normalizado`) seguida do `Id` de desempate. O seek monta um `WHERE` que compara a chave com a âncora da página anterior.

`NULL` é um valor especial no banco: não se compara a ele (`coluna > NULL` é desconhecido, invalidando a cláusula e devolvendo **zero resultados**). Uma coluna nullable na chave de keyset quebra o seek de forma silenciosa. No `Geo`, `nome_normalizado` é derivado do `Nome` (não-nulo), mas está tipado como `string?` no domínio e pode, em teoria, ser nulo (ex.: nome só com caracteres não normalizáveis).

A pergunta é como garantir que a chave de ordenação seja sempre não-nula para o seek, sem perder linhas de eventual chave nula.

## Drivers da decisão

- **Correção do seek** — `NULL` na chave invalida o `WHERE` e some com resultados sem erro visível.
- **Determinismo** — linhas com chave nula ainda devem aparecer, em posição estável.
- **Regra geral reutilizável** — vale para qualquer keyset ordenado futuro, não só estado/cidade.

## Opções consideradas

- **A**: Tratar `NULL` explicitamente no seek (`NULLS LAST` + ramos condicionais no `WHERE`).
- **B**: **Coalescer a chave para um sentinela não-nulo (`''`) no keyset, com índice funcional correspondente.**
- **C**: Tornar a coluna física `NOT NULL` (backfill + restrição).

## Resultado da decisão

**Escolhida:** "B — coalescer a chave de ordenação para não-nulo (`''`) no keyset, com índice funcional sobre a mesma expressão", porque é o caminho robusto e portável: linhas com chave nula coalescem para o sentinela e ordenam de forma determinística (aparecem primeiro), sem ramos frágeis de `NULLS LAST`.

- A regra geral é: **uma coluna nullable nunca entra direto num keyset** — coalesça para um sentinela não-nulo (`COALESCE(chave, '')`).
- O índice de suporte é **funcional** sobre a mesma expressão coalescida (`(COALESCE(nome_normalizado, ''), id)`), casando o `ORDER BY`/`WHERE` gerado pelo motor de seek.
- Como `nome_normalizado` é minúscula + sem acento, a ordem por byte/colação default é a alfabética desejada; a colação do índice é a mesma do `ORDER BY` (a default da coluna), consistente por construção.

## Consequências

### Positivas

- Seek sempre válido — sem o modo de falha "zero resultados" por `NULL`.
- Linhas com chave nula ainda aparecem, em posição determinística.
- Regra simples e reutilizável para qualquer keyset ordenado.

### Negativas

- O índice é **funcional** (sobre a expressão coalescida), não um índice de coluna simples — exige SQL cru na migration e casar a expressão do `ORDER BY`.

### Neutras

- Para estado/cidade a chave é não-nula na prática (deriva do `Nome`), então o coalesce é defensivo; a regra protege colunas genuinamente nullable no futuro.

## Confirmação

- **Teste de integração**: uma cidade com `nome_normalizado` nulo ainda aparece na listagem sem filtro (ordenada no início), provando que o coalesce não a esconde.
- **Fitness de DDL**: o índice funcional `(COALESCE(nome_normalizado, ''), id)` existe e é B-tree (inspeção de `pg_indexes.indexdef`).

## Prós e contras das opções

### A — tratar `NULL` no seek (`NULLS LAST` + ramos)

- Bom, porque não precisa de coalesce nem índice funcional.
- Ruim, porque o seek com `NULLS LAST` em keyset é frágil (ramos condicionais no `WHERE` por causa do `NULL`), difícil de casar com índice.

### B — coalesce + índice funcional (escolhida)

- Bom, porque o seek fica sempre válido e a regra é simples e reutilizável.
- Ruim, porque o índice precisa ser funcional sobre a expressão coalescida.

### C — coluna `NOT NULL`

- Bom, porque permitiria um índice de coluna simples.
- Ruim, porque é uma mudança de schema mais invasiva e não cobre, por si só, colunas que precisam admitir nulo no domínio — a regra de coalesce é mais geral.

## Mais informações

- Aplica a regra de chave não-nula ao keyset ordenado da [ADR-0094](0094-keyset-ordenado-via-mr-sob-cursor-opaco.md).
- Origem: story #700 (ordenação alfabética server-side de estados/cidades).
