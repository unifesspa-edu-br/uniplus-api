# forbidden-deps

Guarda contra reintrodução de dependências de teste vetadas pelo projeto Uni+. Script bash sem dependências externas — roda igual no laptop do dev e no CI.

## Uso

```bash
bash tools/forbidden-deps/check.sh           # varre o repo a partir de .
bash tools/forbidden-deps/check.sh tests     # restringe a um subdiretório
```

Saída:

- `ok   {pacote} — sem ocorrências ativas (referência: ADR-XXXX)` — pacote permanece banido sem violações.
- `FAIL {pacote} (N ocorrência(s))` — pacote proibido foi reintroduzido; lista os arquivos/linhas e referencia a ADR que vetou seu uso.

Exit code != 0 quando houver pelo menos uma ocorrência ativa.

## Pacotes vetados

| Pacote | Motivo | Referência |
|--------|--------|-----------|
| `FluentAssertions` | Biblioteca de assertions Xceed comercial paga a partir da v8. Usar [`AwesomeAssertions`](https://www.nuget.org/packages/AwesomeAssertions) (Apache-2.0). | [ADR-0021](../../docs/adrs/0021-adocao-awesomeassertions-como-biblioteca-de-assertions.md) |

## Como adicionar nova proibição

1. Acrescente uma entrada ao array `FORBIDDEN` em [`check.sh`](check.sh) no formato `'nome~regex_para_cs~regex_para_csproj_e_props~motivo~ADR'`. O separador é `~` (escolhido por não colidir com sintaxe de regex de alternation `|` nem com nomes de pacote .NET).
2. Atualize a tabela acima com o novo pacote.
3. Escreva (ou referencie) uma ADR registrando a decisão de banir o pacote — toda proibição precisa ter uma ADR vinculada para rastreabilidade.

Os regex devem ser **específicos da forma efetiva de uso** (ex.: `using XYZ;`, `Include="XYZ"`), não casos genéricos de menção, para evitar falso positivo em comentários, strings literais ou nomes de pacote semelhantes.

## Escopo da varredura

- **Inclui:** `*.cs`, `*.csproj`, `*.props`.
- **Exclui:** `docs/` (ADRs e documentos podem citar o nome do pacote legitimamente como contexto histórico), `bin/`, `obj/`, `.git/`.

Esse recorte é intencional: o objetivo é impedir o uso de produção/teste do pacote, não censurar a discussão sobre ele em documentação técnica.

## Integração com CI

O check roda como job dedicado em [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml) sob o nome **`Forbidden dependencies`**. Falha no CI bloqueia o merge.
