# Spike da issue #1262 — resultados empíricos

Branch `spike/1262-idempotencia-provas`, commit `51d2d0a2`, worktree
`uniplus-api-wt-spike-1262`. Sete cenários pelo endpoint real contra Postgres
efêmero (Testcontainers). Cada teste reporta o status devolvido ao cliente **e**
a linha gravada em `idempotency_cache` — nenhuma suíte existente observa a
segunda, que é o que decide a política.

Os testes falham de propósito: carregam o relatório na mensagem da exceção.

## O que ficou provado

### P2 — a regressão da #1262 é real e atual (não prospectiva)

Mesmo usuário (`sub` fixo ⇒ mesmo `scope`), mesma `Idempotency-Key`, permissão
concedida entre as duas chamadas:

| | resultado |
|---|---|
| 1ª (sem a permissão) | **403** |
| linha no store | `Status=Completed, ResponseStatus=403`, `ExpiresAt` = +24 h |
| 2ª (COM a permissão) | **403** — replayado |
| motivo criado no banco | `False` |

O usuário ganha a concessão e continua recusado por 24 h. `Idempotency-Replayed:
true` no replay (P3) confirma que veio do cache, não de nova decisão.

Isso derruba a leitura intermediária de que "authorization filters precedem
resource filters, logo o 403 nunca é cacheado": em
`MotivosDecisaoIsencaoController` o `[Authorize]` da classe não exige papel, o
usuário está autenticado, o filtro reserva, e o `Forbid()` sai **de dentro da
action** — dentro do `next()`.

### P1 — um 422 de validação tranca a chave por 24 h

`tipoProcessoCodigo` vazio viola o `NotEmpty` do validator.

| | resultado |
|---|---|
| 1ª | **422** (correto) |
| linha no store | `Status=Processing, ResponseStatus=null`, `ExpiresAt` = +24 h |
| retry (mesma chave, mesmo corpo) | **409** `processing_conflict` |

Um corpo inválido tranca a chave. A `ValidationException` sobe até o
`GlobalExceptionMiddleware`, que roda **fora** do MVC, então chega ao filtro
como exceção pendente e cai no `return` que não grava nem apaga.

É o pior caso da política atual, e não estava na issue.

### P7 — o replay de um 401 perde o `WWW-Authenticate`

Identidade sem `jti` ⇒ `Challenge()` de dentro da action.

| | 1ª | replay |
|---|---|---|
| status | 401 | 401 |
| `WWW-Authenticate` | `Bearer` | **(ausente)** |

`SerializeCachedHeaders` persiste apenas `Content-Type`, `Location` e `ETag`. O
comentário do próprio controller diz que um 401 sem esse header "não se parece
com nenhuma outra falha de autenticação da API — o cliente perde justamente o
sinal de que precisa renovar a credencial". O cache produz exatamente isso.

### P4 — achado novo: corpo que não desserializa devolve 500 e tranca a chave

Predicado com discriminator polimórfico inexistente (`{"tipo": …}` em vez de
`{"$tipo": …}`):

| | resultado |
|---|---|
| 1ª | **500** `uniplus.internal.unexpected` |
| linha no store | `Status=Processing`, `ExpiresAt` = +24 h |
| retry | **409** `processing_conflict` |

Erro de forma do cliente vira erro do servidor **e** tranca a chave. É defeito
próprio, independente da política de cache — não estava no parecer nem na issue.

### P6 — a mecânica do double-save da ADR-0119 é real

Entidade colidente no tracker, `SaveChanges` estoura, **sem** descartar o
rastreamento:

| | resultado |
|---|---|
| 1º `SaveChanges` | `DbUpdateException` |
| tracker após a falha | `MotivoDecisaoIsencao=Added` (permanece) |
| 2º `SaveChanges` | `DbUpdateException` — repete |

Confirma que um `catch` que devolve `Failure` sem `DescartarAlteracoesNaoSalvas()`
deixa o `SaveChangesAsync` do `AutoApplyTransactions` repetir a exceção fora do
catch. O risco de 500 nos ~12 handlers apontados é real.

### P5 — mas esse 500 não foi alcançado por corrida real

48 tentativas (6 rodadas × 8 requests paralelos, mesmo `regraCodigo`), contra
`CriarObrigatoriedadeLegalCommandHandler`, que tem o catch **sem** descartar:

```
rodada 0..5: 201 409 409 409 409 409 409 409
```

Sempre 201 uma vez, 409 nas demais. **Nunca 500.**

Leitura honesta: o `catch` provavelmente não foi atingido nenhuma vez — o
check-then-act (`ExisteRegraCodigoAtivoAsync`) barra antes, e devolve o **mesmo**
`DomainError` do catch, então os dois caminhos são indistinguíveis pelo corpo. A
janela de corrida real é estreita demais para 48 tentativas.

Conclusão prática: a mecânica é real (P6) mas a alcançabilidade é rara. Isso
**enfraquece a urgência** de reordenar as fatias por causa desse risco, e mantém
o conserto (adicionar o descarte) como higiene legítima, não como bloqueio.

Não consegui forçar o catch deterministicamente: a outra constraint (`hash`)
inclui o `RegraCodigo` no cálculo, então códigos distintos nunca colidem por hash.

## O que NÃO ficou provado

**"Action lançou ⇒ transação ambiente reverte ⇒ é seguro liberar a chave."**

É a premissa mais carregada do desenho proposto (a linha `Action lançou ⇒
Liberar` da tabela de decisão). Não a provei: exigiria um handler que mute,
persista e então lance, e o discovery do Wolverine é por assembly explícito do
Host — o assembly de teste não entra, então um handler sintético exigiria mexer
na factory.

Continua valendo como critério de pronto do PR que implementar a tabela.

## Efeito sobre a ADR-0119

A ADR-0119 descreve o gap do filtro como "cacheia uma entrada **200 com corpo
vazio**". P1 e P4 mostram `Status=Processing, ResponseStatus=null` — ou seja, o
comportamento que ela descreve **não é mais o atual**; foi corrigido (commits
`9d3727e5` / `c6d54bc2`, issue #1028 fechada). A ADR e o `CLAUDE.md` do
repositório ainda afirmam um gap que não existe.

Isso confirma, por observação, que a condição de reavaliação que a própria
ADR-0119 fixou ("enquanto o gap do `IdempotencyFilter` não for corrigido") já
disparou.

## Adendo — a premissa não provada está declarada no código de produção

`DescartarRetificacaoCommandHandler.cs:126-131` documenta as duas metades da
premissa e o handler depende delas para não corromper dado:

> "A partir deste flush, NENHUM caminho pode retornar Failure: o Wolverine
> commita a transação ambiente ao término normal do handler
> (AutoApplyTransactions), **inclusive num `Result.Failure`** — que gravaria o
> DELETE sem o INSERT correspondente. Uma falha após o flush é bug (…) e
> **LANÇA, forçando o rollback da transação** em vez de commitar uma restauração
> meia-feita."

E age conforme: `:136-140` e `:144-148` lançam `InvalidOperationException`
deliberadamente, com a justificativa explícita de reverter a transação.

Segue sem prova empírica minha, mas deixa de ser suposição do desenho novo — é o
contrato que o código existente assume e usa como mecanismo.

## Alcance atual do gatilho da #1262

`IVerificadorDeAcesso` é injetado em **um** controller
(`MotivosDecisaoIsencaoController`, 4 actions), e é o único arquivo em
`src/*/[A-Za-z]*.API/Controllers/` com `Forbid()`/`Challenge()` dentro de action.

A regressão é real e atual, mas hoje alcança quatro endpoints. Cresce na direção
da ADR-0078 (mover a decisão de acesso para dentro da action), o que é argumento
para tratar a causa — a decisão voltar a preceder o filtro — e não só a política
de cache.

## Achado à parte — um teste cujo nome afirma o contrário do que ocorre

`tests/Unifesspa.UniPlus.Selecao.IntegrationTests/Outbox/Cascading/SessaoEditorialEndpointTests.cs:433-453`

`BadRequestSemIfMatch_ContinuaArmazenado`, com o display name "Um 400 cujo
defeito está no BODY continua cacheado":

- manda `Idempotency-Key: "chave inválida com espaços"` — chave **malformada**,
  que o filtro rejeita na validação do header, **antes de reservar** qualquer
  coisa. Esse 400 nunca chega ao cache: nada é armazenado, ao contrário do que o
  nome e o comentário afirmam;
- faz **uma única** requisição — nunca exercita replay, então não poderia
  observar "continua cacheado" mesmo que o caminho fosse outro;
- o defeito também não está "no BODY": está no header.

Não quebra (o 400 acontece), mas é asserção de gate que vira fato citado. Quem
ler o nome conclui que existe cobertura provando que 400 de corpo é cacheado —
não existe. Renomear e, se a cobertura for desejada, escrevê-la de fato.

## Conjunto de regressão para quem mexer na política

Testes que exercitam replay hoje, por arquivo (ocorrências):

| arquivo | ocorrências |
|---|---|
| `Outbox/Cascading/SessaoEditorialEndpointTests.cs` | 9 |
| `Outbox/Cascading/PublicarProcessoSeletivoEndpointTests.cs` | 5 |
| `Outbox/Cascading/RetificarProcessoSeletivoEndpointTests.cs` | 4 |
| `Outbox/Cascading/DefinirRegrasDerivacaoEndpointTests.cs` | 4 |
| `Outbox/Cascading/DefinirFatosColetadosEndpointTests.cs` | 3 |
| `ObrigatoriedadesLegais/ObrigatoriedadeLegalAdminEndpointTests.cs` | 3 |

Todos em `Selecao.IntegrationTests`. Nenhum outro módulo cobre replay, embora
`AddIdempotency` seja chamado por quatro — a regressão de um PR que mexa no
filtro precisa cobrir pelo menos um módulo além de Seleção.

## P8 — a fronteira transacional, provada (e ela inverte o desenho)

Filtro sintético lança em `OnActionExecuted`, isto é, **depois** que a action
retornou — logo depois do `ICommandBus.Send`:

| | resultado |
|---|---|
| status devolvido | 500 |
| **agregado no banco** | **True** |
| linha no store | `Status=Processing`, `ExpiresAt` = +24 h |

A transação ambiente **já commitou** quando a action retorna. `AutoApplyTransactions`
fecha a transação em torno da invocação do handler, não da action.

Consequências:

1. **"Action lançou ⇒ Liberar" é inseguro** e sai da tabela de decisão: liberar a
   chave nesse caminho faria o retry reexecutar uma mutação já persistida.
2. O `IAsyncActionFilter` que emitiria o sinal de execução **não resolve o caso
   para o qual foi desenhado** — ele sabe se a *action* rodou, não se a
   transação fechou. Sai do plano, junto com `HttpContext.Items` e a válvula.
3. A política atual nesse ramo (reter em `Processing`) está **correta**. O que
   está errado é a *duração*: 24 h. O `LeaseTtl` é a resposta, não o sinal.

Corrige também o adendo anterior deste relatório: a evidência documental de
`DescartarRetificacaoCommandHandler` prova a metade "lança **dentro do handler**
⇒ rollback", que é verdadeira e continua valendo. Ela não se estende à action, e
eu a havia estendido.
