---
status: "proposed"
date: "2026-09-19"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "Backend (CTIC)"
informed:
  - "Equipe Uni+"
---

# ADR-0134: O conflito declarado retentável não ocupa a chave de idempotência

## Contexto e enunciado do problema

A [ADR-0027](0027-idempotency-key-store-postgresql.md) fixa que a resposta de uma requisição idempotente é guardada e reproduzida em replay, e é explícita quanto ao alcance: **tanto 2xx quanto 4xx são cacheados**. A justificativa é anti-abuso — se a recusa não fosse guardada, repetir a mesma requisição inválida custaria ao servidor a execução inteira a cada tentativa.

O status 409 cabe nessa regra e não deveria caber inteiro, porque ele nomeia **duas coisas diferentes**:

- o conflito que descreve um **estado que permanece** — o código já está ocupado, a sigla já existe, o objeto já tem ato vivo do tipo. Repetir não muda nada, e guardar a resposta é exatamente o certo;
- o conflito que descreve uma **corrida que já passou** — duas escritas concorrentes sobre o mesmo agregado, duas publicações disputando o mesmo número de versão. A mensagem que o sistema devolve nesses casos manda, literalmente, recarregar e tentar de novo.

Para o segundo, guardar a resposta por 24 h **nega a única saída que o status oferece**. O cliente que preserva a chave — que é o que a semântica de idempotência manda fazer diante de uma falha que ele não sabe classificar — recebe em replay o conflito de ontem, para uma corrida que durou milissegundos.

## Por que a correção óbvia está errada

Descartar toda resposta 409 foi tentado e revertido. O contraexemplo é concreto e não é hipótese:

1. um `POST` com chave devolve 409 porque o código está ocupado, e o cliente registra que falhou;
2. alguém remove o registro que ocupava o código, liberando-o;
3. um replay tardio da requisição original — fila de retry, proxy, cliente que preserva a chave — **executa e cria o registro**.

A chave de idempotência teria autorizado a mutação que o cliente acredita não ter feito, que é precisamente o que ela existe para impedir.

## Opções consideradas

- **Guardar todo 409, como hoje.** Preserva a regra da ADR-0027 sem exceção, e mantém o cliente legítimo preso a uma corrida que durou milissegundos.
- **Descartar todo 409.** Tentada e revertida: libera o replay tardio a executar a mutação que o cliente acredita não ter feito.
- **Deduzir do código público por índice reverso.** Exigiria membro novo na porta do mapeador — que tem implementação escrita à mão em teste — e uma garantia de unicidade do código público que não existe hoje.
- **Declarar na classificação do erro e carimbar no envelope.** A informação fica onde a classificação já vive, e chega ao cliente na própria resposta.

## Resultado da decisão

**O descarte da reserva passa a alcançar o 409 que a própria resposta declara retentável, e só ele.**

A declaração vive na classificação do erro, ao lado do status, do código público e do título, e viaja no envelope da resposta como `retryable: true`. O filtro de idempotência lê o que a resposta declarou; ele não deduz nada do status.

### O critério que decide

> **A requisição idêntica, repetida, poderia agora dar certo?**

- Conflito de concorrência otimista: sim — o outro escritor terminou, e a mesma requisição se aplica.
- Conflito de unicidade de catálogo: não. Se a repetição idêntica desse certo, seria porque o obstáculo foi removido — e aí estaríamos no contraexemplo acima.

**A pergunta tem uma segunda metade, e ela não é redundante: dar certo tem de significar o efeito que o cliente pediu.** Há operação cujo sentido depende do estado que o cliente acabou de observar, e para ela "a mesma requisição, repetida, se aplica" é falso mesmo quando ela passa.

O caso concreto é a marcação de revisado de um termo de consentimento. A requisição não tem corpo: ela significa *"aprovo o texto que acabei de ler"*. Editar o rascunho **limpa a revisão anterior**, porque a aprovação vale para o texto exato que foi lido. Se a marcação perde a corrida para uma edição concorrente e a reserva for liberada, um retry automático da mesma requisição aprova o texto **novo** — que ninguém revisou.

Por isso a classificação **não é do código de erro sozinho, é do código no contexto das operações que o emitem**: um código compartilhado por várias operações só pode ser declarado retentável se a repetição preservar o efeito pedido em **todas** elas.

O segundo caso mostra que isto não é uma exceção isolada. O atalho atômico de retificação relê a versão corrente a cada execução: perdida a corrida para outra publicação, repetir a mesma requisição retificaria a versão do **vencedor**, criando um ato normativo a mais — publicado no Diário Oficial — que ninguém pediu. O conflito de número de versão duplicado, que é corrida legítima nos outros dois caminhos que o emitem, fica durável por causa deste.

Os dois casos têm a mesma forma, e ela dá o teste prático: **é seguro repetir a operação cujo alvo a requisição nomeia; é perigoso repetir aquela cujo alvo sai do estado corrente.** Marcar vigente um dataset, ativar um motivo, gravar um rascunho — todos nomeiam o que tocam, e repetir faz a mesma coisa. Aprovar "o texto que está lá" e retificar "a versão que está valendo" não nomeiam nada: o que elas tocam pode ter mudado exatamente por causa da corrida que as recusou.

### O default é durável

Declarar é ato explícito, e a assimetria é deliberada: classificar um durável como retentável libera uma mutação indevida; classificar um transitório como durável apenas preserva o comportamento anterior. A direção perigosa exige alguém escrever a declaração.

Pelo mesmo motivo, na ausência de declaração a resposta é guardada.

## Por que isto não reabre o abuso que a ADR-0027 fecha

Guardar a recusa nunca impediu abuso: quem quer repetir a requisição inválida troca a chave, e o custo do servidor é o mesmo. O cache prende o cliente **legítimo**, que preservou a chave justamente porque a ADR-0027 mandou. O mesmo raciocínio já sustentou a exceção anterior — as respostas de precondição, que também deixaram de ocupar a chave.

## Consequências

- O cliente que recebe um conflito de corrida pode repetir com a mesma chave, que é o que a mensagem de erro já lhe dizia para fazer.
- A distinção fica **pública na resposta e no contrato**: o campo chega ao cliente sem que ele precise consultar documentação, e o schema `ProblemDetails` dos contratos publicados o declara, para que cliente gerado o enxergue como propriedade tipada. A declaração é **opcional**: o campo só viaja quando é `true`, e exigi-lo presente faria um cliente correto ler a ausência — que é o caso comum — como violação de contrato.
- Duas das rotas com conflito retentável são `PUT` com corpo e sem `If-Match`. Uma entrega duplicada tardia pode sobrescrever em silêncio a edição de um concorrente. É consequência aceita — é o que o cliente pediu e o que a mensagem manda fazer —, não efeito despercebido.
- Um código mal classificado como retentável reintroduz o contraexemplo. É por isso que a classificação é declaração explícita, e não dedução do status.
