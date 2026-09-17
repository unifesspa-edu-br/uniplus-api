---
status: "proposed"
date: "2026-09-17"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "Backend (CTIC)"
informed:
  - "Equipe Uni+"
---

# ADR-0133: A divulgação do certame é materializada quando o ato normativo se registra

## Contexto e enunciado do problema

A [ADR-0131](0131-portal-como-bff-publico-de-dominio.md) fixou de onde o Portal tira o que mostra — um contrato público de leitura publicado por Seleção — e fixou também **quando** um certame aparece: *"um processo aparece no portal quando tem versão de configuração vigente e o ato que a criou está registrado"*. Na seção de confirmações, ela acrescenta que processo inexistente, processo em rascunho e processo cuja publicação teve o registro do ato recusado recebem a mesma resposta de não encontrado.

Esse critério foi implementado e exercitado. O que a implementação revelou não foi um defeito de escrita: foi uma consequência estrutural do critério.

**A existência do ato é fato de Publicações; o dado que ordena, filtra e conta vive em Seleção; e a visibilidade depende dos dois.** A fronteira entre os módulos corta uma invariante no meio, e toda dificuldade encontrada é a mesma aparecendo em lugares diferentes:

- a leitura do certame descia a linhagem de versões para achar a mais nova com ato registrado;
- essa descida exigia consultar Publicações **no caminho da requisição**, numa rota anônima de pico previsível — com o acoplamento de disponibilidade que isso traz;
- a vitrine ordenava e contava por uma coluna denormalizada em Seleção que descrevia a publicação mais nova, **inclusive a retificação cujo ato ainda não registrou** — o item exibia um prazo e era posicionado por outro;
- a página da vitrine encolhia depois de formada, porque o descarte de quem não tem ato só podia acontecer fora do SQL;
- o título do certame não existia no contrato, porque não vive na configuração congelada e não havia instante em que congelá-lo.

Há ainda uma tensão de mérito. O critério da ADR-0131 retira do ar um certame **que já era público** quando o ato de uma retificação não se confirma — e a recusa de mérito é terminal, então a retirada é indefinida. Publicação é ato público. Torná-la invisível fere a transparência: é para isso que existe retificação de ato, e não supressão.

## Drivers da decisão

- **Transparência da publicação.** O que foi publicado com ato normativo não sai do ar porque um ato posterior falhou.
- **Não divulgar certame sem ato correspondente** — o driver original da ADR-0131, que permanece.
- **Leitura pública barata e previsível.** É a primeira superfície anônima de alto valor do sistema, com pico na abertura de inscrições.
- **Sem acoplamento de disponibilidade entre módulos no caminho da requisição.**
- **Ordenação, filtro e contagem coerentes entre si e com o conteúdo servido.**

## Opções consideradas

- **A. Manter o critério e a leitura sob demanda** — descer a linhagem a cada requisição e perguntar a Publicações.
- **B. Espelhar o desfecho do registro em Seleção**, mantendo a projeção sob demanda.
- **C. Materializar a projeção pública quando o ato se registra** — a alternativa que a ADR-0131 já nomeia nas suas opções, avaliada e não escolhida à época.

## Resultado da decisão

**Escolhida: "C — materializar a projeção pública quando o ato se registra".**

Publicações, ao registrar o ato com sucesso, emite o desfecho na mesma transação que grava o ato. Seleção consome, projeta o certame a partir da versão de configuração correspondente e grava uma linha numa tabela própria.

### A existência da linha é a publicidade

Não há coluna de "visível". **Não existir e não ser público são a mesma coisa**, e é dessa identidade que decorre o resto:

- **Abertura sem ato confirmado**: não há linha, o certame não é divulgado. Ele nunca foi público, e divulgá-lo sem ato normativo é o que o critério existe para impedir — o driver da ADR-0131 é preservado inteiro.
- **Retificação cujo ato não se confirma**: a linha não avança, e o certame permanece no ar com o conteúdo anterior. Isso **emenda** o critério da ADR-0131, que mandava retirá-lo.

A emenda não encobre a retificação. Enquanto o ato dela não existe, **ela não tem publicidade nenhuma** — o que se serve é o último estado que tem ato normativo. A recusa de mérito continua terminal, na fila morta, como estado que alguém reconcilia; o público não paga por ela com a ausência do certame.

### O que a leitura pública passa a ser

Uma consulta de tabela única. Ordenar por prazo, filtrar por situação, contar por situação e servir o detalhe olham a mesma linha, e por isso não podem discordar entre si nem do conteúdo entregue.

Some, por consequência: a resolução de linhagem, o leitor cross-módulo no caminho da requisição com o seu opt-in de service location, a coluna denormalizada de prazo com o seu índice e a invariante de mantê-la, a interpretação do documento congelado a cada linha de cada página, e o descarte posterior de candidatos.

### O título é congelado no instante da divulgação

O título do certame é atributo do processo, não da configuração congelada. A materialização abre um instante que antes não existia — aquele em que o certame passa a ser público — e é nele que o título é lido e congelado. Uma edição posterior do nome só alcança o público quando o ato da retificação correspondente se confirma, como todo o resto do conteúdo.

### Reconstruível, nunca fonte

A tabela é derivada: tudo nela sai da versão de configuração congelada, que permanece a fonte de verdade. Perdê-la custa reprojetar, nunca dado.

## Consequências

### Positivas

- O certame publicado não sai do ar por falha em ato posterior.
- A leitura pública deixa de depender da disponibilidade de outro módulo.
- Ordenação, contadores e conteúdo passam a ser coerentes por construção, não por regra.
- O documento projetado tem forma fixa: um bloco novo na configuração congelada não alcança o público nem por descuido.
- O custo de interpretar o documento congelado passa a ser pago uma vez por publicação, em vez de uma vez por requisição.

### Negativas

- **Há um instante em que o certame está publicado e ainda não é público** — entre o commit da publicação e o dreno da mensagem de registro. Ele é o mesmo instante que o critério anterior já produzia; o que muda é que ele deixa de reaparecer a cada retificação.
- **Estado derivado a manter.** Uma mudança na forma da projeção exige reprojetar o que já está divulgado, e a versão do formato precisa subir junto.
- **Uma escrita a mais no consumo do registro do ato**, não na transação de publicação.
- A recusa de mérito continua exigindo reconciliação humana, e agora o sintoma visível é mais silencioso: o certame fica no conteúdo antigo em vez de sumir. Exige alarme sobre a fila morta.

### Neutras

- O contrato público não muda de forma por causa desta decisão.

## Confirmação

Exercitado contra o host real, com Postgres e fila durável:

- o retorno da publicação volta e o certame ainda não é público; a linha nasce quando o ato chega pela fila;
- a retificação avança a divulgação quando o ato dela se registra;
- **a retificação cujo ato é recusado por mérito não avança a linha, e o certame permanece no ar com o conteúdo anterior.**

O terceiro cenário é a propriedade que decide esta ADR, e é o que a separa da anterior.

## Emenda à ADR-0131

O critério de visibilidade daquela decisão passa a ler-se: **um processo aparece no portal quando existe divulgação materializada para ele**, o que ocorre quando o ato que criou alguma das suas versões se registrou.

A cláusula da seção de confirmações que manda o processo cuja publicação teve o registro recusado receber a mesma resposta de inexistente **vale para a abertura e deixa de valer para a retificação**: na abertura não há divulgação a servir; na retificação há, e ela permanece.

Permanece intacto o que aquela decisão fixou sobre o Portal ser um BFF por experiência, sobre o contrato de Seleção ser um Open Host Service com tipos declarados, sobre o critério de visibilidade viver no contrato e não no Portal, e sobre revalidação obrigatória nas respostas públicas.

## Referências

- [ADR-0131](0131-portal-como-bff-publico-de-dominio.md) — o Portal consome um contrato público de Seleção
- [ADR-0108](0108-registro-do-ato-por-mensagem-duravel.md) — o registro do ato viaja no outbox
- [ADR-0105](0105-modulo-publicacoes-registro-central-dos-atos.md) — Publicações é o registro central dos atos
- [ADR-0004](0004-outbox-transacional-via-wolverine.md) — outbox transacional
