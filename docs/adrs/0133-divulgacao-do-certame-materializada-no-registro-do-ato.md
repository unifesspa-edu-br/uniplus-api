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

A [ADR-0131](0131-portal-como-bff-publico-de-dominio.md) fixou de onde o Portal tira o que mostra e fixou também **quando** um certame aparece: *"um processo aparece no portal quando tem versão de configuração vigente e o ato que a criou está registrado"*. Na seção de confirmações, ela acrescenta que processo inexistente, processo em rascunho e processo cuja publicação teve o registro do ato recusado recebem a mesma resposta de não encontrado.

Esse critério amarra a visibilidade a um fato que vive em **Publicações**, enquanto tudo que a leitura pública precisa para ordenar, filtrar e contar vive em **Seleção**. A fronteira entre os módulos corta uma invariante no meio, e isso tem duas consequências estruturais.

**A primeira é de acoplamento.** Resolver visibilidade a cada leitura obriga a consultar o outro módulo no caminho da requisição — numa superfície anônima, com pico previsível na abertura de inscrições. Pior, o recorte por situação e a contagem por situação são agregações sobre um conjunto que Seleção não consegue definir sozinha: ela não sabe quais dos seus certames são visíveis. O resultado é que a ordem e os números podem discordar do conteúdo servido.

**A segunda é de mérito.** O critério retira do ar um certame **que já era público** quando o ato de uma retificação não se confirma. A recusa de mérito é terminal, então a retirada é indefinida. Publicação é ato público, e torná-la invisível fere a transparência: é para isso que existe retificação de ato, e não supressão.

## Drivers da decisão

- **Transparência da publicação.** O que foi publicado com ato normativo não sai do ar porque um ato posterior falhou.
- **Não divulgar certame sem ato correspondente** — o driver original da ADR-0131, que permanece.
- **Sem acoplamento de disponibilidade entre módulos no caminho da requisição**, numa superfície anônima de alto valor.
- **Ordem, recorte, contagem e conteúdo coerentes entre si**, sem depender de disciplina de quem escreve a consulta.

## Opções consideradas

- **A. Manter o critério e resolver a visibilidade a cada leitura**, consultando Publicações sob demanda.
- **B. Espelhar em Seleção o desfecho do registro**, mantendo a projeção pública calculada a cada leitura.
- **C. Materializar a projeção pública no instante em que o ato se registra** — alternativa que a ADR-0131 já nomeia entre as suas opções, avaliada e não escolhida à época.

## Resultado da decisão

**Escolhida: "C — materializar a projeção pública quando o ato se registra".**

Publicações confirma o registro do ato ao domínio que publicou, com a mesma durabilidade com que recebeu a requisição. Seleção, ao receber a confirmação, materializa a projeção pública do certame a partir da versão de configuração correspondente.

### A existência do registro é a publicidade

Não há estado de "visível" a consultar. **Não existir e não ser público são a mesma coisa**, e é dessa identidade que decorre o resto:

- **Abertura sem ato confirmado**: não há projeção, e o certame não é divulgado. Ele nunca foi público, e divulgá-lo sem ato normativo é o que o critério existe para impedir — o driver original da ADR-0131 é preservado inteiro.
- **Retificação cujo ato não se confirma**: a projeção não avança, e o certame permanece no ar com o conteúdo anterior. Isso **emenda** o critério da ADR-0131, que mandava retirá-lo.

A emenda não encobre a retificação. Enquanto o ato dela não existe, **ela não tem publicidade nenhuma** — o que se serve é o último estado que tem ato normativo. A recusa de mérito permanece terminal e permanece sendo estado que alguém reconcilia; o público deixa de pagar por ela com a ausência do certame.

### A leitura pública deixa de depender de outro módulo

Ordem, recorte por situação, contagem e detalhe passam a resolver sobre a mesma projeção. A coerência entre eles deixa de ser regra a seguir e passa a ser propriedade da forma: não há duas fontes de onde discordar.

O custo de interpretar a configuração congelada passa a ser pago uma vez por publicação, em vez de uma vez por leitura.

### O que a projeção congela

Além do que a configuração congelada carrega, a projeção congela o **título** do certame. Ele é atributo do processo, não da configuração, e antes não havia instante em que fixá-lo. A materialização cria esse instante: uma edição posterior do título só alcança o público quando o ato da retificação correspondente se confirma, como todo o resto do conteúdo.

### Derivado, nunca fonte

A projeção é reconstruível: tudo nela sai da versão de configuração congelada, que permanece a fonte de verdade. Perdê-la custa reprojetar, nunca dado.

## Consequências

### Positivas

- O certame publicado não sai do ar por falha em ato posterior.
- A leitura pública deixa de depender da disponibilidade de outro módulo.
- Ordem, contadores e conteúdo passam a ser coerentes por construção.
- A forma da projeção é fechada: um bloco novo na configuração congelada não alcança o público por descuido.

### Negativas

- **Há um intervalo em que o certame está publicado e ainda não é público** — entre a publicação e a confirmação do registro. É o mesmo intervalo que o critério anterior já produzia; o que muda é que ele deixa de reaparecer a cada retificação.
- **Estado derivado a manter.** Mudar a forma da projeção exige reprojetar o que já está divulgado, e versionar o formato.
- A recusa de mérito continua exigindo reconciliação humana, e agora o sintoma é mais silencioso: o certame fica no conteúdo antigo em vez de sumir. Exige alarme sobre o que morre na fila.

### Neutras

- O contrato público não muda de forma por causa desta decisão.

## Confirmação

As três propriedades que a decisão promete são verificáveis em integração, contra a infraestrutura real:

- a publicação retorna e o certame ainda não é público; ele passa a ser quando o registro do ato se confirma;
- a retificação avança a divulgação quando o ato dela se registra;
- **a retificação cujo ato é recusado por mérito não avança a divulgação, e o certame permanece no ar com o conteúdo anterior.**

O terceiro é o que separa esta decisão da anterior.

## Emenda à ADR-0131

O critério de visibilidade daquela decisão passa a ler-se: **um processo aparece no portal quando existe divulgação materializada para ele**, o que ocorre quando o ato que criou alguma das suas versões se registrou.

A cláusula da seção de confirmações que manda o processo cuja publicação teve o registro recusado receber a mesma resposta de inexistente **vale para a abertura e deixa de valer para a retificação**: na abertura não há divulgação a servir; na retificação há, e ela permanece.

Permanece intacto o que aquela decisão fixou sobre o Portal ser um BFF por experiência, sobre o contrato de Seleção ser um Open Host Service com tipos declarados, sobre o critério de visibilidade viver no contrato e não no Portal, e sobre revalidação obrigatória nas respostas públicas.

## Referências

- [ADR-0131](0131-portal-como-bff-publico-de-dominio.md) — o Portal consome um contrato público de Seleção
- [ADR-0108](0108-registro-do-ato-por-mensagem-duravel.md) — o registro do ato viaja no outbox
- [ADR-0105](0105-modulo-publicacoes-registro-central-dos-atos.md) — Publicações é o registro central dos atos
- [ADR-0004](0004-outbox-transacional-via-wolverine.md) — outbox transacional
