---
status: "accepted"
date: "2026-07-13"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "P.O. CEPS"
informed:
  - "Equipe Uni+"
---

# ADR-0110: A retificação é uma sessão editorial sobre a configuração, não um estado do certame

## Contexto e enunciado do problema

Hoje **a retificação não consegue alterar a configuração**. Ela emite um novo ato, sucede a `VersaoConfiguracao` e devolve 204 — mas o conteúdo congelado sai **igual ao anterior**: os seis `Definir*` são recusados pós-publicação (`ProcessoSeletivo.cs:657-667`), o `Status` nunca volta de `Publicado`, e `Retificar()` recanonicaliza **a mesma configuração viva**. Entre a versão N e a N+1 mudam apenas `periodo` e `hashesEdital` (e acrescenta-se o bloco `retificacao`) — os outros **15 blocos** saem idênticos.

É uma porta trancada por dentro: o domínio recusa a mutação com a mensagem *"utilize a retificação"*, e a retificação não sabe mutar.

O custo é concreto: **nenhum certame publicado consegue receber** documentos exigidos (#554), formulário (#559) ou cascata de remanejamento (#575) — as três dimensões que faltam para fechar o Módulo Seleção.

Cinco stories vão escrever sobre a retificação. O artefato que elas tocam — a `VersaoConfiguracao` — é **append-only**: uma decisão errada aqui **não se corrige adiante**, fica congelada num documento com peso jurídico. Esta ADR fixa as decisões **antes** de a primeira linha de código ser escrita.

## Drivers da decisão

- **O certame em curso não pode enxergar o rascunho.** O que o candidato vê é a versão congelada vigente (ADR-0075/0076) — e continua sendo, durante toda a edição.
- **O descarte não pode destruir configuração em silêncio.** Reidratar mal é pior do que não reidratar.
- **Dois administradores não podem se sobrescrever sem saber.**
- **O passado não se muta.** Versões congeladas não são tocadas, recalculadas nem reinterpretadas.

## Opções consideradas

**Sobre o estado (D3):**

- **`EmRetificacao` como status do certame.** Allowlist limpa por status, auditoria explícita. **Rejeitada:** o `Status` é exposto no DTO público — um certame **juridicamente publicado** passaria a exibir um status que sugere o contrário, e um rascunho abandonado o deixaria assim **indefinidamente**. Contraria o princípio já fixado na modelagem: o `status` marca o estado do **ato**, não a atividade em curso.
- **`EmRetificacao` interno, mapeado para `Publicado` no DTO.** **Rejeitada:** cria uma divergência entre modelo e contrato que alguém esquece de manter — e aí vaza.

**Sobre a identidade na reidratação (D2):**

- **Congelar os ids das entidades-filhas no envelope.** Resolveria a estabilidade. **Rejeitada:** mudaria a **forma** do envelope (bump para `1.2`), o inflaria, e faria a identidade técnica voltar para dentro do hash — exatamente o que a **D9 da ADR-0109** acabou de expulsar.
- **Mapa externo de ids.** **Rejeitada:** estado paralelo ao congelado, sem dono e sem invariante que o mantenha coerente.

**Sobre a concorrência (D5):**

- **Last-write-wins declarado.** Mais barato. **Rejeitada:** uma perda silenciosa de edição num documento com peso jurídico é indefensável depois do fato.
- **Lock exclusivo (um rascunho, um dono).** Simples de entender. **Rejeitada:** trava o time se o dono some — e a primeira consequência é o pedido de "forçar liberação", que reintroduz o problema por outra porta.

**Sobre o descarte (D2/D8):**

- **Desfazer incremental.** **Rejeitada — impossível:** todo `Definir*` **substitui a coleção inteira** e as relações EF são `Cascade`. Não há delta a reverter.

**Sobre o atalho atômico (D7):**

- **Reimplementar `POST /retificacoes` como abre+fecha.** **Rejeitada:** ele já tem lock, cadeia, idempotência e outbox, e funciona. Reescrevê-lo trocaria risco zero por risco de regressão, sem ganho.

## Resultado da decisão

Fecham-se nove decisões (D1–D9) que governam as cinco stories da Feature. A retificação passa a ser uma **sessão editorial** sobre a configuração viva — com portador próprio, precondição de concorrência e descarte verificável — **sem** que o certame publicado mude de estado.

### D1 — Registro de **codecs**: capacidades por versão

Um *decoder* por versão **não basta**. O round-trip de uma `1.1` exige **recanonicalizá-la com o encoder `1.1`** — e o canonicalizer só emite `SchemaVersionAtual` (`SnapshotPublicacaoCanonicalizer.cs:62`). No dia da `1.2`, provar o round-trip de uma `1.1` ficaria **impossível**, e o descarte de um certame retificado antes daquele bump deixaria de ser verificável. **O encoder de uma versão não é aposentado quando ela deixa de ser a corrente** — ele continua sendo necessário para provar o round-trip dela.

Cada versão registrada declara **capacidades** — não um estado exclusivo:

| Capacidade | Significa | `1.0` | `1.1` |
|---|---|---|---|
| **Encoder** | sabe **produzir** os bytes daquela forma | não | **sim** |
| **Decoder** | sabe **ler** os bytes de volta em entidades | não | **sim** |
| **Recusa nomeada** | o sistema **sabe que a versão existe** e a rejeita com motivo | **sim** | — |

E **uma** versão, separadamente, é a **corrente de emissão** (`SchemaVersionAtual`) — hoje a `1.1`. Quando a `1.2` chegar, a `1.1` **perde** a emissão corrente mas **mantém** encoder e decoder.

- **Reidratável** ⟺ tem encoder **e** decoder. (Encoder sem decoder não reidrata; decoder sem encoder não prova round-trip.)
- **`1.0`** é **conhecida e recusada**, com motivo nomeado: ela pode conter `atendimento`/`classificacao` como `nao_construido` — o fallback silencioso que a **D8 da ADR-0109** matou. Não há o que reidratar.
- Versão **fora do registro** → recusa com erro nomeado.

**O decoder devolve um envelope neutro completo** — grafo das 6 dimensões **+ `DadosEdital` + hash do documento + `RetificacaoInfo`** — porque o CA de round-trip precisa de todos eles, não só do grafo.

**Fitness tests:** (a) a versão **corrente de emissão** tem encoder **e** decoder; (b) toda versão do registro tem as suas capacidades declaradas — nenhuma cai no vazio. Uma `1.2` sem codec **quebra o build**.

### D2 — Identidade e auditoria na reidratação

| Campo | No envelope? | Decisão |
|---|---|---|
| `etapa.Id` | **Sim** (`:149`) — `criteriosDesempate.args.etapaRef` e `regrasEliminacao.args.etapaRef` apontam para ele | **Preservado.** `EtapaProcesso.Criar` não aceita `Id` → **factory de reidratação** |
| `Id` das demais filhas | **Não** | **Regenerados.** Precisão: *nenhuma referência de **negócio** exige estabilidade deles*; as **FKs internas** (ex.: `ModalidadeSelecionada.ConfiguracaoDistribuicaoVagasId`) são **reconstruídas junto com o grafo**. São expostos no DTO público, e o ADR declara que **o contrato não promete estabilidade** |
| `CreatedAt` / `UpdatedAt` (todas herdam de `EntityBase`) | **Não** | **Parcialmente perdidos, com precisão:** as filhas **recriadas** recebem `CreatedAt` = instante do **descarte** e `UpdatedAt` = `null`; as **etapas reconciliadas** por `Id` **preservam** o `CreatedAt` original (são a mesma instância *tracked*). É perda de informação — **declarada**, não silenciosa. A auditoria com peso jurídico vive na `VersaoConfiguracao` (append-only, `IForensicEntity`), que **não** é tocada |

Rejeitadas: congelar os ids no envelope (mudaria a forma para `1.2`, e faria a identidade técnica voltar para dentro do hash — o oposto da D9); mapa externo de ids (estado paralelo, sem dono).

**Reconciliação EF (armadilha real):** o descarte **não** substitui entidades *tracked* por instâncias novas com o mesmo `Id` — isso colide com o identity map. `DefinirEtapasCommandHandler:38` **já** reconcilia etapas por `Id` na instância *tracked*, preservando o `etapa_ref`. O descarte **reusa esse padrão**.

### D3 — **Nenhum status novo**

`StatusProcesso` continua com os **cinco** valores que já tem. **Nenhum é acrescentado** — em particular, `EmRetificacao` **não** é criado.

Princípio já fixado na modelagem: **o `status` marca o estado do ato, não a atividade em curso**; o progresso vive em **dimensão própria** de cada incremento. Um certame com retificação aberta **está publicado** — juridicamente, e para o candidato. A retificação é estado **editorial**, e o seu portador é a **existência do `RascunhoRetificacao`**.

### D4 — Allowlist que **falha fechada**

`MutacaoBloqueadaPosPublicacao()` (`:657-667`) libera a mutação para **qualquer** status ≠ `Publicado` — inclusive `Nenhum`, `Encerrado`, `Cancelado`. **Denylist de um elemento**, que **falha aberta**.

```text
MutacaoPermitida()  ⟺  Status == Rascunho  ||  (Status == Publicado && rascunho aberto)
```

**Armadilha:** `Rascunho == null` significa **tanto** "não existe" **quanto** "não foi carregado" — e um comando futuro que use `ObterPorIdAsync` recusaria uma edição legítima (fail-closed **indevido**). Mitigação: **carregamento de mutação explícito** (`ObterParaMutacaoAsync`, que inclui o rascunho) + **fitness test** que prova que todo handler de mutação o usa.

### D5 — A revisão protege a **sessão editorial inteira**

Uma `Revisao` que governasse só o `PUT` do motivo seria decorativa: os **seis** `Definir*` (`ProcessoSeletivoController.cs:120+`) são as rotas que **de fato** alteram a configuração.

**Precondição de toda mutação com rascunho aberto:** os seis `Definir*`, o motivo, o descarte e o fechamento. Toda mutação aceita **incrementa** a revisão.

**ETag forte, com identidade de sessão.** A `Revisao` sozinha sofre **ABA**: descartar e reabrir reinicia a contagem, e um tag antigo validaria **outra** sessão.

```text
ETag: "{RascunhoId}:{Revisao}"        ← forte (sem W/), aspas obrigatórias
```

**Ciclo de vida do ETag — completo:**

| Momento | Header |
|---|---|
| `POST` de abertura (201) | Devolve `ETag` da sessão recém-criada — o cliente já sai apto a mutar |
| `GET` do rascunho (200) | Devolve `ETag` corrente |
| **Toda mutação aceita** (`Definir*`, `PUT` do motivo — 204) | Devolve o **novo** `ETag` (revisão incrementada), para que o cliente encadeie sem re-`GET` |
| `DELETE` / fechamento (204) | **Não** devolve `ETag` — a sessão deixou de existir |

**Onde a precondição é avaliada: no HANDLER, sob o lock — não no filtro.**

Os seis `Definir*` servem **duas** situações: editar um processo em **`Rascunho`** (pré-publicação — **sem** rascunho de retificação, e portanto **sem** `ETag` a fornecer) e editar **durante** uma retificação (**com** rascunho, e **com** precondição obrigatória). A obrigatoriedade do `If-Match` é, portanto, **condicional ao estado do agregado**.

O `IdempotencyFilter` é um **resource filter**: roda **antes** do controller e **não carrega o agregado** — ele **não tem como saber** se há rascunho aberto. Exigir a precondição ali é impossível; exigi-la **sempre** quebraria a edição de um processo em rascunho.

Divisão de responsabilidade:

- **Transporte:** valida apenas a **sintaxe** do `If-Match`, quando presente.
- **Handler, sob o `FOR UPDATE`:** carrega o agregado, descobre se há rascunho aberto e **só então** exige a precondição. `428` e `412` saem como `DomainError` **nomeado**, mapeado a status pelo registro de erros do módulo (fonte única — ADR-0024), como todo erro de domínio do projeto.

**Contrato de erro (códigos estáveis, ADR-0024):**

| Situação | Status | `code` |
|---|---|---|
| `If-Match` **ausente** com rascunho aberto | **428** | `uniplus.selecao.precondicao_requerida` |
| `If-Match` **defasado** (nenhuma tag forte casa) | **412** | `uniplus.selecao.precondicao_falhou` |
| `If-Match` **sintaticamente inválido** | **400** | `uniplus.selecao.precondicao_malformada` |

**Comparação (`If-Match`, RFC 9110 §13.1.1 — comparação forte):**

- `If-Match: "{id}:{rev}"` → casa exatamente, ou **412**;
- `If-Match: *` → casa **se houver** rascunho aberto (é o "qualquer representação existente");
- **lista** de tags → casa se **alguma** casar;
- **weak tag** (`W/"..."`) → **não é erro de sintaxe**: a gramática a aceita, mas ela **nunca casa** na comparação forte. Se nenhuma tag **forte** casar → **412** (não 400);
- `*` **misturado** com outras tags, ou tag sem aspas → **400** (aí sim é sintaxe inválida).

**CORS — o `ETag` precisa ser exposto.** A política atual (`CorsConfiguration.cs`) declara `If-Match` entre os headers de **request**, mas **não expõe** `ETag` na **resposta**. Sem `WithExposedHeaders("ETag")`, o navegador **não deixa o frontend lê-lo** — e a sessão editorial seria inoperável no browser, apesar de correta no servidor. Ajuste declarado.

### D6 — `If-Match` × `Idempotency-Key` (colisão real, resolvida)

O `IdempotencyFilter` é um **resource filter**: ele roda **antes** do controller (`:79`), identifica a requisição pelo **hash do body** (`:150`), e **grava qualquer resposta < 500** (`:214` só apaga em `>= 500` ou cancelamento).

**Consequência se nada for feito:** um **412** ficaria **gravado** sob a `Idempotency-Key`. O cliente corrigiria o `If-Match`, retentaria com a mesma key — e receberia o **412 em replay**, por 24h. A precondição envenenaria a idempotência.

**Ordem de avaliação — declarada.** "Precondição antes da idempotência" seria impossível: o filtro é um *resource filter* e o replay, por definição, **não** reavalia a precondição — é a mesma requisição, já executada.

1. **Sintaxe/presença de headers** (`Idempotency-Key`, `If-Match`) — falha aqui é **400** ou **428**;
2. **Lookup** — `HitMatch` faz **replay** da resposta gravada, **sem** reavaliar a precondição (é a **mesma** requisição, já executada);
3. **Miss** → reserva;
4. **Sob o lock do agregado**: checks normais do recurso **e** comparação do `If-Match`;
5. **412/428 liberam a reserva** (não completam a entrada). Sucesso e demais `< 500` **completam**.

**Exceção formal à ADR-0027 — registrada na ADR-0110.** A ADR-0027 declara que *"tanto 2xx quanto 4xx são cacheados"*, pela convenção Stripe (anti-abuso). **412 e 428 são a exceção**, e a razão é que eles não são resultado da operação — **a operação não executou**. A motivação anti-abuso da ADR-0027 protege contra *retry com keys diferentes até achar uma sequência que muda o estado*; cachear um 412 não impede isso (o atacante troca a key de qualquer modo) e **prende o cliente legítimo** que apenas releu o ETag. Para o **428**, a RFC 6585 §3 é explícita: a resposta depende de um header **ausente** — armazená-la é incoerente.

**O ETag precisa entrar nos headers persistidos.** O filtro hoje guarda **apenas** `Content-Type` e `Location`, e **exclui o ETag por escrito** (`:468` — "ETag dinâmico"). Sem isso, o **replay de uma abertura ou de uma mutação não devolveria ETag**, e o cliente ficaria sem a precondição da próxima chamada. Ajuste declarado, em código **compartilhado** (`Infrastructure.Core`).

### D7 — Concorrência do atalho atômico

`POST /{id}/retificacoes` é serializado pelo `FOR UPDATE` (`ProcessoSeletivoRepository.cs:53`). Duas chamadas com **`Idempotency-Key` distintas** produzem **v2 e v3** — duas retificações deliberadas, cada uma com o seu ato e o seu motivo, em cadeia linear. É **correto** e **permanece**. Com a **mesma key**, é **uma** operação lógica: replay da resposta gravada.

**Com rascunho aberto, o atalho recusa** (`RetificacaoJaAberta`) — **invariante do domínio**, não `if` do handler.

### D8 — Coerência dos três blocos derivados

`distribuicao`, `modalidades` e `ofertas` derivam da **mesma** coleção e são serializados em **três** blocos. A reidratação os recombina por `ofertaCursoOrigemId` — e a coerência exigida é **igualdade exata de conjuntos**, não mera inclusão:

> O conjunto de `ofertaCursoOrigemId` em **`distribuicao`**, em **`ofertas`** e o conjunto **distinto** dos que aparecem em **`modalidades`** têm de ser **o mesmo** — sem ausências, sem extras, sem duplicatas.

Qualquer divergência → **recusa com erro nomeado**. Recombinar em silêncio um envelope incoerente reconstruiria um agregado que **nunca existiu**.

### D9 — Matriz HTTP, em **duas camadas**

A matriz não pode listar `404` primeiro: o `IdempotencyFilter` é um **resource filter** e roda **antes** do controller. Lookup, replay, mismatch de body e limite de tamanho acontecem **fora** do domínio.

**Camada 1 — transporte / idempotência (antes do controller):**

| # | Condição | Resposta | Código de erro |
|---|---|---|---|
| 1 | `Idempotency-Key` ausente onde é obrigatória | **400** | `key_ausente` |
| 2 | `Idempotency-Key` malformada | **400** | `key_malformada` |
| 3 | *(o transporte só valida sintaxe — a obrigatoriedade é do handler, D5)* | — | — |
| 4 | `If-Match` **sintaticamente inválido** | **400** | `uniplus.selecao.precondicao_malformada` |
| 5 | Body acima do limite | **413** | `body_muito_grande` |
| 6 | Mesma key, **body diferente** | **422** | `body_mismatch` |
| 7 | Mesma key **ainda em processamento** | **409** | `processing_conflict` |
| 8 | Mesma key, mesmo body, **já concluída** | **replay** da resposta gravada (com `ETag`, D6) | — |

**Camada 2 — domínio (cache miss, sob o lock):**

| # | Condição | Resposta |
|---|---|---|
| 9 | Processo inexistente | **404** |
| 10 | Rascunho **inexistente** (`PUT` / `DELETE` / fechamento) | **409** `RetificacaoNaoAberta` |
| 11 | Rascunho **já existe** (`POST` de abertura) | **409** `RetificacaoJaAberta` |
| 12a | `If-Match` **ausente** com rascunho aberto | **428** `precondicao_requerida` — **não armazenável** |
| 12b | `If-Match` **defasado** — nenhuma tag **forte** casa | **412** `precondicao_falhou` — **não armazenável** |
| 13 | Regra de negócio (conformidade, cadeia, ato, validação) | **422** |
| 14 | Sucesso | **201** (abertura, com `ETag`) · **200** (`GET`, com `ETag`) · **204** (`PUT` com `ETag`; `DELETE` e fechamento **sem**) |

**Precedências deliberadas:**

- **10/11 antes de 12** (rascunho inexistente/duplicado **antes** de precondição defasada). A RFC 9110 §13.2.1 manda executar os *checks normais* primeiro e **ignorar precondições** quando esses checks já produziriam falha. Responder **412** para um rascunho que **não existe** mandaria o cliente recarregar um ETag inexistente.
- **3 antes de 10** (428 por `If-Match` ausente **precede** a checagem do rascunho). É falha de **protocolo**, detectável sem tocar no recurso — e o filtro roda antes do controller de qualquer modo. O cliente que não manda a precondição obrigatória recebe **428**, mesmo que não haja rascunho.
- **Validação de payload é 422**, não 400. `400` fica para **JSON/binding/header malformado** (`GlobalExceptionMiddleware.cs:53`); FluentValidation e regra de domínio produzem **422**.

`_links` só em representação single (ADR-0029) — **204 não carrega HATEOAS**.

## Consequências

**Positivas.** As três dimensões que faltam para fechar o Módulo Seleção deixam de estar bloqueadas. O guard de mutação passa a **falhar fechado** (hoje `Encerrado` e `Cancelado` são silenciosamente mutáveis). O certame publicado ganha imunidade **provada** — não presumida — contra o rascunho. A concorrência entre administradores passa a ser **detectada** em vez de resolvida por sorteio.

**Negativas.** O descarte **regenera** a auditoria (`CreatedAt`/`UpdatedAt`) e os ids técnicos das entidades-filhas. É perda de informação — **declarada** (D2), não silenciosa. A auditoria com peso jurídico permanece intacta na `VersaoConfiguracao`.

Um **rascunho abandonado** deixa o certame servindo a versão vigente indefinidamente. Não há expiração automática nesta fatia — é política **declarada**, não esquecimento.

E o `IdempotencyFilter` — código **compartilhado** — precisa mudar (D6). Não é dano colateral: é a consequência de introduzir precondições num sistema que só conhecia idempotência.

**Neutras.** Versões já congeladas não são recalculadas nem reinterpretadas. A `1.0` é **conhecida e recusada**, com motivo nomeado — não silenciada.

## Fora de escopo

- As três dimensões que a Feature desbloqueia (#554, #559, #575) — dependem daqui; não são decididas aqui.
- Transições `Encerrado` / `Cancelado` — não há comando na `main`, e esta ADR não os cria. Ela apenas impede que esses estados sejam **mutáveis por omissão** (D4).
- Expiração de rascunho abandonado.
- Diff visual "o que mudou" entre a versão vigente e o rascunho — frontend; aqui entregam-se os dados que o tornam possível.
