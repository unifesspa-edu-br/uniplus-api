# adr-lint

Validador MADR 4.0 para ADRs do projeto Uni+. Script bash sem dependências externas.

## Uso

```bash
bash tools/adr-lint/validate.sh                 # valida docs/adrs
bash tools/adr-lint/validate.sh docs/adrs       # explícito
```

Saída:

- `ok   NNNN-titulo.md` — ADR válido.
- `FAIL NNNN-titulo.md` — ADR com erros (listados abaixo).

Exit code != 0 quando houver pelo menos um erro.

## Regras enforçadas

1. Nome de arquivo no padrão `NNNN-slug.md` (4 dígitos, slug ASCII em minúsculas).
2. Frontmatter YAML presente entre dois `---` no topo.
3. Campos obrigatórios: `status`, `date`, `decision-makers`.
4. `status` ∈ `{proposed, accepted, rejected, deprecated}` ou `superseded by ADR-NNNN`.
5. `date` em formato ISO `YYYY-MM-DD`.
6. H1 do arquivo coincide com o número do nome (`# ADR-NNNN: ...`).
7. Exatamente UMA seção `## Resultado da decisão` por arquivo (regra "1 ADR = 1 decisão").
8. Seções obrigatórias presentes: `Contexto e enunciado do problema`, `Opções consideradas`, `Resultado da decisão`, `Consequências`.

Mais detalhes em [`docs/adrs/_template.md`](../../docs/adrs/_template.md).

## Hygiene de markdown

Adicionalmente, rode `markdownlint-cli2` para verificações gerais:

```bash
npx markdownlint-cli2 'docs/adrs/**/*.md'
```

Configuração em `docs/adrs/.markdownlint-cli2.jsonc`.
