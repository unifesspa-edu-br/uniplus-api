---
status: "accepted"
date: "2026-07-12"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "P.O. CEPS"
informed:
  - "Equipe Uni+"
---

# ADR-0108: O domínio registra o ato por mensagem durável, não por chamada síncrona

> **Supersede a [ADR-0106](0106-orquestracao-sincrona-selecao-publicacoes-ato-primeiro.md)** no ponto do
> mecanismo (chamada síncrona in-process, "ato primeiro"). O que a ADR-0106 decidiu sobre *ownership* —
> Publicações é o registro central e não conhece os domínios ([ADR-0105](0105-modulo-publicacoes-registro-central-dos-atos.md)) —
> permanece integralmente.

## Contexto e enunciado do problema

A ADR-0106 decidiu que publicar um Edital registraria o ato em `Publicacoes` por **chamada síncrona
in-process**, com o ato gravado **antes** de qualquer escrita em Seleção, e registrou como consequência
aceita a possibilidade de um "ato órfão" quando a escrita de Seleção falhasse depois do registro. À época,
essa consequência parecia um resíduo tolerável.

Duas coisas mudaram — uma de fato, outra de conhecimento.

### 1. A vaga de linhagem tornou o órfão catastrófico

A [ADR-0107](0107-vaga-de-linhagem-unica-por-objeto.md), posterior, introduziu a **vaga de linhagem única
por objeto**: um ato de tipo `unico_por_objeto` — e `EDITAL_ABERTURA` é um deles — reserva a vaga daquele
objeto, e **a vaga é monotônica: ocupada, nunca se libera**. A própria ADR-0107 diz que um ato publicado
sobre o objeto errado "ocupa a vaga daquele objeto **para sempre**", e que o mecanismo de liberação é
assunto de story própria, ainda não decidido.

Compondo as duas ADRs: um ato registrado + uma falha na escrita de Seleção (crash entre os dois commits,
erro inesperado no `SaveChanges`) deixa a vaga do certame ocupada por uma linhagem cujo Edital não existe.
**O processo seletivo fica impublicável em definitivo.** A recuperação dependeria de o cliente retentar com
exatamente a mesma `Idempotency-Key`; se ele desistir, o certame morre.

Não é um resíduo. É perda de dado com efeito permanente.

### 2. O mecanismo escolhido não é suportado pelo framework

A tentativa natural de salvar o desenho — tornar o registro do ato e a publicação **atômicos**, numa
transação compartilhada — não é viável no pipeline. Verificado no código e na documentação oficial:

- `ICommandBus.Send` aninhado **cria um novo scope de DI**: uma conexão "compartilhada por escopo" não
  atravessa para o handler chamado.
- O handler chamado **abre e confirma a própria transação** (`AutoApplyTransactions`), comitando antes de o
  chamador terminar.
- O Wolverine **não suporta dois `DbContext` num mesmo handler**: *"There is no way to utilize more than one
  `DbContext` type in a single handler while using the Wolverine transactional middleware."*
- `Result.Failure` **não provoca rollback** — só exceção provoca. Metade dos caminhos de erro do projeto são
  `Result.Failure`.

E o mecanismo da ADR-0106 é, ele próprio, o que a documentação do Wolverine desaconselha de forma explícita:

> *"we pretty well never recommend calling `IMessageBus.InvokeAsync()` inline in any message handler to
> another message handler"*
>
> *"By and large, the Wolverine community will recommend you do most communication between modules through
> some sort of asynchronous messaging, either locally in process or through external message brokers."*

A ADR-0106 rejeitara a via assíncrona alegando que **não havia infraestrutura de retry/DLQ**. Essa premissa
é falsa: `PersistMessagesWithPostgresql(...).EnableMessageTransport()` e
`UseDurableOutboxOnAllSendingEndpoints()` já estão configurados, o projeto **já** drena domain events por
cascading messages ([ADR-0005](0005-cascading-messages-para-drenagem-de-domain-events.md)) numa fila
PostgreSQL durável, e o Wolverine traz retry e dead letter nativos.

## Opções consideradas

- **A. Chamada síncrona in-process, ato primeiro** (o que a ADR-0106 decidiu): Seleção chama Publicações e só
  escreve depois que o ato existe.
- **B. Transação compartilhada entre os dois `DbContext`**: registrar o ato e publicar no mesmo commit.
- **C. Mensagem durável no outbox**: Seleção grava o que publicou e enfileira, atomicamente, a requisição de
  registro do ato; Publicações a processa depois do commit.
- **D. Manter A e construir o mecanismo administrativo de liberação de vaga**: aceitar o órfão e criar a
  ferramenta que o desfaz.

## Resultado da decisão

**Escolhida: "C — mensagem durável no outbox".**

A opção **A** produz um modo de falha catastrófico depois da ADR-0107 (certame impublicável para sempre) e é,
além disso, o padrão que a documentação do Wolverine desaconselha explicitamente. A opção **B** é inviável no
pipeline — o framework não admite dois `DbContext` num handler, o `Send` aninhado cria outro scope de DI, e
`Result.Failure` não provoca rollback. A opção **D** aceitaria conviver com um dano permanente enquanto a
ferramenta não existe, e a ADR-0107 registra que a semântica institucional dessa liberação ("quem pode anular?
com que ato?") **ainda não foi decidida pelo CEPS** — não é algo que se resolva em código.

**O domínio que publica registra o ato enfileirando uma mensagem durável, na mesma transação em que grava o
que publicou.**

```text
Seleção, UMA transação (a que o Wolverine já abre):
    AtoId = Guid.CreateVersion7()            ← o domínio decide o id
    Edital + VersaoConfiguracao(AtoCriadorId = AtoId)
    + envelope RegistrarAtoNormativoRequisicao(AtoId, …) no OUTBOX
    COMMIT                                   ← ou tudo, ou nada

depois do commit, o Wolverine entrega da fila PG durável:
    Publicações registra o ato COM o id recebido
        sucesso → fim
        falha   → exceção → retry → dead letter (visível, reprocessável)
```

Quatro propriedades, e nenhuma delas é suposta — todas foram **medidas** contra o host real (spike #820,
branch `spike/820-outbox-durable-queue`):

1. **Atomicidade onde importa.** O envelope é persistido na mesma transação do agregado
   ([ADR-0004](0004-outbox-transacional-via-wolverine.md)). Ou o Edital e a mensagem existem, ou nenhum dos
   dois.
2. **O órfão que trava o certame deixa de ser possível.** A vaga só é reservada quando o ato é criado. O modo
   de falha residual é o **inverso** — Edital publicado com o ato ainda por registrar —, que é transitório,
   visível na dead letter e **recuperável**, e que **não impede** a publicação do certame.
3. **Idempotência por chave primária.** O `AtoId` é decidido pelo domínio e viaja na mensagem; uma reentrega
   (at-least-once) tenta gravar o mesmo id e não faz nada. Sem tabela de idempotência, sem hash do efeito.
4. **A falha aflora.** O handler de mensagem **lança** — um `Result.Failure` seria lido pelo Wolverine como
   sucesso, e o ato sumiria em silêncio.

`VersaoConfiguracao.AtoCriadorId` é referência **por valor, sem chave estrangeira**
([ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md)): o modelo **já** permitia a versão apontar
para um ato de outro módulo. Não é contorno — é o desenho, agora usado como sempre se pretendeu.

## Consequências

### Positivas

- Nenhuma falha em Publicações pode tornar um certame impublicável.
- A entrega é garantida pelo outbox durável; a falha é visível na dead letter, não silenciosa.
- Segue a orientação explícita do Wolverine, em vez de contrariá-la.
- Elimina três mecanismos que a ADR-0106 exigia só para compensar a falta de atomicidade: hash do efeito,
  propagação de chave canônica de idempotência e reconciliação de órfãos.

### Negativas

- **Consistência eventual.** Existe uma janela — tipicamente milissegundos — em que o Edital está publicado e
  o ato ainda não aparece na consulta unificada de Publicações. É o preço, e é qualitativamente menor do que
  um certame impublicável para sempre.
- Uma recusa legítima (tipo sem versão vigente, vaga já ocupada) só aflora **depois** do 2xx da publicação.
  Mitigação: pré-validação por **leitura** cross-módulo ([ADR-0056](0056-modulo-configuracao-e-read-side-via-reader.md))
  antes de publicar, recusando com 422 os casos previsíveis. Sobra a corrida rara → dead letter + alerta.
- O handler de mensagem vive em `Infrastructure`, não em `Application`: o middleware transacional do
  Wolverine só instala a transação quando enxerga o `DbContext` concreto. É o mesmo lugar em que
  `ProcessoPublicadoToKafkaCascadeHandler` já mora.

### Neutras

- Precondição explícita: os módulos co-hospedados no mesmo processo e no mesmo banco
  ([ADR-0097](0097-topologia-de-deploy-em-tres-apis-monolito-modular.md)). Se `Publicacoes` virar deployable
  separado, a fila local vira transporte externo — a **forma** da orquestração não muda, só o transporte.

## Confirmação

Medido no spike, contra o host real, e replicado na implementação:

- publicar registra o ato pela fila durável, com vínculo e vaga;
- registro recusado **não** reserva a vaga do certame, e o Edital permanece publicado;
- reentrega da mesma requisição não duplica o ato;
- a falha do handler chega à **dead letter** (verificado em `wolverine.wolverine_dead_letters`).
