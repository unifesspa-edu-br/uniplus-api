---
status: "proposed"
date: "2026-10-10"
decision-makers:
  - "Tech Lead"
consulted: []
informed: []
---

# ADR-0142: O Fator de Atenuação das Desigualdades de Acesso cita o fato que decide quem o recebe

## Contexto e enunciado do problema

Qualquer processo seletivo pode oferecer o Fator de Atenuação das Desigualdades de Acesso, até aqui chamado de bônus regional, conforme o edital e a necessidade da Unifesspa. O administrador configura o acréscimo no processo, sem amarra a tipo de processo (UNI-REQ-0015). A classificação dos candidatos aplica ou não o acréscimo a cada candidato observando o que foi configurado e os fatos dele (UNI-REQ-0046, [ADR-0013](0013-classificacao-dos-candidatos-como-servicos-de-dominio-puros.md)).

A configuração do acréscimo tinha a regra, o fator, o teto e o retrato da base legal com os municípios. Não dizia quem recebe o acréscimo. O critério muda de edital para edital: no de Medicina 2027, o acréscimo é de quem o pede no formulário e reside num município da área de abrangência (UNI-REQ-0150). Fixar o critério no código amarraria o sistema a um edital. Ler um fato de código conhecido amarraria o cálculo dos fatos derivados a um cadastro.

## Drivers da decisão

- O critério do acréscimo é configuração do processo, não código.
- A classificação dos candidatos decide pelo que está configurado e pelos fatos do candidato.
- Reaproveitar o que as regras do processo já têm: derivação, conferências, congelamento e editor ([ADR-0135](0135-projeto-compartilhado-de-regras-sobre-fatos.md), [ADR-0136](0136-catalogo-de-fatos-do-candidato-administravel.md)).
- O processo de resultado importado continua possível: a classificação já vem pronta.

## Opções consideradas

- A configuração do acréscimo cita um fato booleano do processo.
- A configuração do acréscimo guarda um predicado próprio sobre fatos.
- Um fato de sistema, de código fixo, que é lido no cálculo dos fatos derivados.

## Resultado da decisão

**Escolhida:** "a configuração do acréscimo cita um fato booleano do processo", porque o critério continua sendo uma regra do processo, montada com o mesmo mecanismo das derivações, e a classificação dos candidatos só precisa avaliar um fato.

- O fato citado é booleano, de escopo do candidato e conhecido até a fase da inscrição. Pode ser um campo do formulário de inscrição ou um derivado por regra do processo sobre os campos dele.
- A classificação dos candidatos aplica o acréscimo ao candidato em quem esse fato resolve verdadeiro. Indeterminado ou não aplicável não dá acréscimo.
- A comprovação é exigência documental do processo, na fase que a configuração escolher (inscrição ou habilitação). Indeferida, retira a vantagem pela consequência `REMOVE_VANTAGEM`.
- Campo sem resposta não dá acréscimo: o pedido não feito é não pedido. Por isso o campo citado não é obrigatório só por ser citado.
- Os municípios continuam parâmetro da base legal. Chegam ao formulário como opções do campo de município, e a regra de elegibilidade não repete a lista.
- Os fatos são do administrador, cadastrados conforme o edital.
- Com inscrição própria, a citação é exigida para publicar. Com resultado importado, não há o que citar.
- Citação que deixou de valer, porque o campo ou a derivação mudou, aparece no checklist e barra a publicação. O rascunho continua editável, como nos gatilhos de exigência.
- A citação entra no envelope publicado.

O predicado próprio duplicaria as conferências e o editor que as derivações já têm, sem regra padrão no catálogo. O fato de sistema amarraria a classificação dos candidatos a um critério só.

Os nomes técnicos do Fator de Atenuação das Desigualdades de Acesso usam a sigla FADA, inclusive no campo novo.

## Consequências

### Positivas

- Cada edital declara o próprio critério, e a classificação dos candidatos não muda.
- O critério da Medicina é cadastro: os fatos do modelo de formulário e a derivação do processo.

### Negativas

- Uma indireção a mais: o critério está na derivação citada, não na configuração do acréscimo.
- Um acréscimo por processo. Mais de um acréscimo no mesmo edital é decisão nova.

## Mais informações

- Issue #1878.
- [ADR-0013](0013-classificacao-dos-candidatos-como-servicos-de-dominio-puros.md): a classificação dos candidatos como serviços de domínio puros.
- [ADR-0136](0136-catalogo-de-fatos-do-candidato-administravel.md): os fatos do administrador e os derivados por regra.

## Emenda (2026-11-25) — consequência sobre o acréscimo

Pela [ADR-0179](0179-consequencia-do-descumprimento-declarada-na-parte-do-processo.md), `REMOVE_VANTAGEM` passa a `RETIRA_O_ACRESCIMO`, que retira somente o acréscimo do Fator de Atenuação das Desigualdades de Acesso e pode ser declarada em qualquer exigência.
