# Retomada — idempotência: política de armazenamento e teto de duração

Ponto de parada de 2026-09-04. Este diretório existe para que o trabalho possa
ser retomado noutra máquina, sem depender de nada que viva só no ambiente local.

**Nada foi implementado.** Existem apenas provas (nesta branch) e planejamento
(neste diretório e nas issues).

## O que há aqui

| arquivo | conteúdo |
|---|---|
| `plano-de-implementacao.md` | plano completo, já corrigido por três revisões |
| `resultados-do-spike.md` | as dez provas, com os números medidos |
| `RETOMADA.md` | este arquivo |

O código das provas está em
`tests/Unifesspa.UniPlus.Selecao.IntegrationTests/SpikeIdempotencia/SpikeIdempotenciaTests.cs`,
nesta branch, em quatro commits.

## Como rodar as provas

Os testes **falham de propósito**: cada um carrega o relatório na mensagem da
exceção, porque o que interessa é o valor medido, não um verde.

```bash
~/.dotnet/dotnet build tests/Unifesspa.UniPlus.Selecao.IntegrationTests/Unifesspa.UniPlus.Selecao.IntegrationTests.csproj
~/.dotnet/dotnet test  tests/Unifesspa.UniPlus.Selecao.IntegrationTests/Unifesspa.UniPlus.Selecao.IntegrationTests.csproj \
  --no-build --filter "FullyQualifiedName~SpikeIdempotencia"
```

Exige Docker (Testcontainers) e o SDK de `~/.dotnet` — o `dotnet` do PATH não
satisfaz o `global.json`.

Esta branch **não se destina a merge**.

## O que ficou provado

1. **403 emitido de dentro da action é replayado** por 24 h, mesmo depois de a
   permissão ser concedida — é a issue #1262, confirmada.
2. **422 de validação tranca a chave** por 24 h; o retry recebe 409.
3. **Replay de 401 perde o `WWW-Authenticate`**.
4. **Discriminator polimórfico ausente devolve 500**; o desconhecido devolve 400.
5. **Exceção depois que a action retorna não reverte** — a transação fecha em
   torno da invocação do handler. Liberar a chave ali reexecutaria mutação
   gravada.
6. Segundo `SaveChanges` com rastreamento sujo repete a exceção.
7. Em 48 corridas reais, o `catch` de constraint **nunca** produziu 500.
8. Latência de escrita administrativa: p50 3,6 ms, p95 28,8 ms (local, sem carga).
9. Dá para resolver o teto efetivo de um endpoint a partir dos metadados da rota.

## Issues

| issue | assunto | estado |
|---|---|---|
| **#1262** | `4xx` de dentro do MVC deixa de ser armazenado | `ready-for-dev` |
| **#1409** | exceção que não mutou libera a reserva | `ready-for-dev` |
| **#1410** | discriminator ausente devolve 400 | `ready-for-dev` |
| **#1411** | `catch` de constraint descarta o rastreamento | `ready-for-dev` |
| **#1412** | teste cujo nome afirma o contrário do que exercita | `ready-for-dev` |
| **#1418** | falha após a reivindicação trava o documento | `ready-for-dev` |
| **#1420** | teto de duração de requisição | `ready-for-dev` |
| **#1422** | invariante de reexecução segura | `ready-for-dev` |
| **#1423** | prazo curto de reserva | **`blocked`** |
| `uniplus-web#709` | coerência da rotação de chave no cliente | aberta |

## Decisões já tomadas

- Executar as oito issues, com ciclo completo até o merge.
- Dividir #1422: invariante (a) fica nela; o inventário de (b) sai para issue
  nova, bloqueada junto de #1423.
- Sem ADR nova — emendas datadas na ADR-0027, consolidadas numa seção só.
- `504` declarado em todas as operações (**ver ressalva abaixo**).

## Pendente de decisão antes de começar

Levantado pela revisão adversarial do plano, na seção "Pontos de parada":

1. **`504` em todas as operações enquanto o `500` continua não declarado** —
   nenhum dos cinco baselines declara `5xx` hoje. O dado não estava à mesa
   quando a decisão foi tomada.
2. **O teto de 30 s colide** com o timeout do cliente de registro de esquemas;
   precisa ser estritamente maior, ou outro valor.
3. **Mudar o desenho de #1410** para a fronteira de desserialização.
4. **Forma do registro em #1422(b)** — a enumeração já existe via contratos.
5. **O teto do Geo**, em `unifesspa-geo-api`, sem dono.

## Correções que a revisão fez no plano — não reverter

- A ordem mudou para `#1412 → #1410 → #1409 → #1422 → #1262 → #1411 → #1418 →
  #1420(i,ii,iii)`. **#1409 vem antes de #1262**, senão dois testes se anulam.
- **#1410 sozinho não libera a chave** — o 400 dele sai do tratador global, e
  ali o filtro retém. O tipo precisa entrar na lista de #1409, ou o defeito
  colateral sobrevive à rodada inteira.
- A frente de **prazo curto de reserva foi retirada** (#1423): destruía o replay
  legítimo e dependia de invariante não verificada.
- O achado de que `IsHashConflict` é ramo morto **pode estar invertido** — medir
  `ConstraintName` antes de afirmar.
