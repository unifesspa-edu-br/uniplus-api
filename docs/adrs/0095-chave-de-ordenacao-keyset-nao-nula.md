---
status: "accepted"
date: "2026-06-19"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0095: Chave de ordenação keyset não-nula via coluna gerada

## Contexto e enunciado do problema

A ordenação keyset multi-coluna ([ADR-0094](0094-keyset-ordenado-via-mr-sob-cursor-opaco.md)) usa uma chave de ordenação (ex.: `nome_normalizado`) seguida do `Id` de desempate. O seek monta um `WHERE` que compara a chave com a âncora da página anterior.

`NULL` é um valor especial no banco: não se compara a ele (`coluna > NULL` é desconhecido, invalidando a cláusula e devolvendo **zero resultados**). Uma coluna nullable na chave de keyset quebra o seek de forma silenciosa. No `Geo`, `nome_normalizado` é derivado do `Nome` (não-nulo), mas está tipado como `string?` no domínio e pode, em teoria, ser nulo (ex.: nome só com caracteres não normalizáveis).

A pergunta é como garantir que a chave de ordenação seja sempre não-nula para o seek, sem perder linhas de eventual chave nula e sem degradar a promessa pública de ordenação alfabética por nome. Coalescer `nome_normalizado` para `''` preserva linhas, mas coloca municípios/UFs válidos sem normalização antes de todos os nomes; isso é estável, porém não é alfabeticamente correto.

## Drivers da decisão

- **Correção do seek** — `NULL` na chave invalida o `WHERE` e some com resultados sem erro visível.
- **Ordem alfabética real** — linhas com `nome_normalizado` nulo ainda devem ordenar pelo `nome` público normalizado, não por um sentinela vazio.
- **Separação de semânticas** — `nome_normalizado` continua sendo coluna de busca/autocomplete; ordenação ganha chave própria.
- **Regra geral reutilizável** — vale para qualquer keyset ordenado futuro, não só estado/cidade.
- **Eficiência** — a expressão de fallback não deve ser recalculada em cada seek quando o banco pode materializar a chave e indexá-la.

## Opções consideradas

- **A**: Tratar `NULL` explicitamente no seek (`NULLS LAST` + ramos condicionais no `WHERE`).
- **B**: Coalescer a chave para um sentinela não-nulo (`''`) no keyset, com índice funcional correspondente.
- **C**: Tornar a coluna física `NOT NULL` (backfill + restrição).
- **D**: **Criar uma coluna gerada de ordenação (`nome_ordenacao`) com fallback normalizado para `nome`, e indexar `(nome_ordenacao, id)`.**

## Resultado da decisão

**Escolhida:** "D — coluna gerada de ordenação (`nome_ordenacao`) com fallback normalizado para `nome`, indexada com `id`", porque preserva simultaneamente a correção do seek e a semântica pública de ordem alfabética por nome.

- A regra geral é: **uma coluna nullable nunca entra direto num keyset**. O keyset ordenado deve usar uma chave não-nula.
- Em Estado/Cidade, essa chave é `nome_ordenacao`, coluna gerada pelo banco a partir de `COALESCE(NULLIF(nome_normalizado, ''), nome)` com `lower(translate(...))` para remover acentos no fallback.
- `nome_normalizado` mantém a semântica de busca/autocomplete e pode continuar nulo quando a fonte não traz o texto sem acento.
- O índice de suporte é B-tree sobre `(nome_ordenacao, id)`, casando o `ORDER BY`/`WHERE` gerado pelo motor de seek.
- A âncora do cursor carrega `SortKey = nome_ordenacao` e `After = Id`; cursores ordenados sem `SortKey` são inválidos no boundary HTTP.

## Consequências

### Positivas

- Seek sempre válido — sem o modo de falha "zero resultados" por `NULL`.
- Linhas sem `nome_normalizado` ainda aparecem em ordem alfabética pelo `nome` público.
- Busca textual e ordenação não compartilham a mesma coluna por conveniência acidental.
- O seek usa índice simples sobre coluna gerada, evitando recalcular a expressão de fallback por página.

### Negativas

- Há uma coluna gerada adicional em Estado/Cidade.
- A expressão de normalização do fallback precisa ficar sincronizada com a normalização usada pelo ETL/testes.

### Neutras

- Para estado/cidade a fonte normalmente traz `*_sem_acento`; o fallback cobre dados legados, incompletos ou importações tolerantes sem reescrever `nome_normalizado`.

## Confirmação

- **Teste de integração**: cidades com `nome_normalizado` nulo aparecem na listagem sem filtro e ordenam pelo fallback normalizado de `nome`.
- **Fitness de DDL**: `nome_ordenacao` é coluna gerada e o índice `(nome_ordenacao, id)` existe e é B-tree (inspeção de `information_schema.columns` e `pg_indexes.indexdef`).
- **Teste de unidade do boundary HTTP**: endpoints ordenados rejeitam cursor de continuação sem `SortKey`.

## Prós e contras das opções

### A — tratar `NULL` no seek (`NULLS LAST` + ramos)

- Bom, porque não precisa de coalesce nem índice funcional.
- Ruim, porque o seek com `NULLS LAST` em keyset é frágil (ramos condicionais no `WHERE` por causa do `NULL`), difícil de casar com índice.

### B — coalesce para `''` + índice funcional

- Bom, porque o seek fica sempre válido e a regra é simples e reutilizável.
- Ruim, porque preserva estabilidade, mas não a ordem alfabética por nome: uma cidade chamada `Zzz` sem `nome_normalizado` ordenaria antes de todos os nomes normalizados.

### C — coluna `NOT NULL`

- Bom, porque permitiria um índice de coluna simples.
- Ruim, porque altera a semântica de `nome_normalizado`, que é coluna de busca derivada da fonte e pode legitimamente estar ausente na importação tolerante.

### D — coluna gerada `nome_ordenacao` + índice B-tree (escolhida)

- Bom, porque separa busca de ordenação, preserva ordem alfabética real e mantém o seek eficiente.
- Ruim, porque adiciona uma coluna técnica gerada e exige fitness de DDL para não divergir da expressão esperada.

## Mais informações

- Aplica a regra de chave não-nula ao keyset ordenado da [ADR-0094](0094-keyset-ordenado-via-mr-sob-cursor-opaco.md).
- Origem: story #700 (ordenação alfabética server-side de estados/cidades).
