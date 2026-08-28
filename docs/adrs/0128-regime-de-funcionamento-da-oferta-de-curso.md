---
status: "accepted"
date: "2026-08-28"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "Product Owner (CEPS)"
informed:
  - "Equipe Uni+"
---

# ADR-0128: Regime de funcionamento da oferta de curso — dimensão própria, conferida contra o regime de turno

## Contexto e enunciado do problema

A [ADR-0126](0126-regime-de-turno-da-oferta-de-curso.md) deu à `OfertaCurso` o **regime de turno** (`REGULAR` ou `INTEGRAL`) e a coleção de **turnos**, encerrando a confusão entre "quantos períodos" e "quais períodos".

Sobra uma característica da oferta que nenhum desses campos exprime: se ela funciona de forma **intensiva** ou **extensiva**. A decisão do Product Owner de 26 de agosto de 2026, registrada no requisito `UNI-REQ-0138`, estabelece os dois valores e uma única regra de compatibilidade com o regime de turno: a oferta `INTENSIVA` só existe sob regime de turno `INTEGRAL`.

O risco central é de vocabulário. A `OfertaCurso` já carrega quatro domínios fechados — programa de oferta, formato pedagógico, regime de turno e turno —, e três deles têm tokens que soam próximos do novo par. `REGULAR` já significa duas coisas distintas na mesma entidade (programa de oferta e regime de turno). Um quinto domínio mal posicionado transformaria a leitura da oferta num exercício de adivinhação.

## Drivers da decisão

- A característica é declarada pelo operador, não derivável de nenhum campo existente: nem a quantidade de turnos, nem o regime de turno, nem o formato pedagógico, nem o programa determinam se a oferta é intensiva.
- A combinação `INTENSIVO` + `REGULAR` precisa ser recusada, nunca acomodada por conversão de uma das duas dimensões.
- Os erros públicos precisam distinguir "não informou" de "informou token inválido" de "informou combinação incompatível" — são três situações com orientação diferente para quem opera o cadastro.
- O banco deve recusar por conta própria o que o agregado recusa, como já ocorre com programa, formato, regime de turno, cardinalidade de turnos e base legal.
- Nenhuma oferta existente pode receber classificação por omissão: um valor atribuído pela migration seria indistinguível de um valor declarado.

## Opções consideradas

- **Campo próprio `RegimeDeFuncionamento`, conferido contra o regime de turno** (esta ADR).
- **Ampliar o domínio de `RegimeDeTurno`** com os tokens `INTENSIVO` e `EXTENSIVO`.
- **Derivar o regime de funcionamento** do regime de turno ou da quantidade de turnos.

## Resultado da decisão

**Escolhida:** "Campo próprio `RegimeDeFuncionamento`, conferido contra o regime de turno".

A `OfertaCurso` ganha um quinto domínio fechado, obrigatório, com dois tokens canônicos: `INTENSIVO` e `EXTENSIVO`. O parsing é por allowlist textual explícita, como o dos demais — sem `Enum.TryParse`, que aceitaria tokens numéricos e nomes PascalCase fora do contrato.

O vocabulário é **declarado, nunca inferido**. O agregado confere as duas dimensões entre si e recusa a combinação incompatível; não promove `REGULAR` a `INTEGRAL` nem rebaixa `INTENSIVO` a `EXTENSIVO` para tornar o payload aceitável. As três combinações válidas são `EXTENSIVO`+`REGULAR`, `EXTENSIVO`+`INTEGRAL` e `INTENSIVO`+`INTEGRAL`.

A compatibilidade mora em um único lugar — `RegimesDeFuncionamento.RegimeDeTurnoExigido`, que responde qual regime de turno cada regime de funcionamento exige, ou `null` quando não restringe. O agregado consulta esse método, e a expressão do CHECK de banco é derivada dele: se o vocabulário crescer, a regra e o CHECK crescem juntos.

A compatibilidade só é avaliada quando **os dois** regimes foram reconhecidos. Token inválido em uma das dimensões produz um erro só — o da própria dimensão —, nunca um erro de incompatibilidade derivado de outro erro.

### Fronteiras que o campo não cruza

O regime de funcionamento **não é** regime de turno, **não é** programa de oferta, **não é** formato pedagógico e **não é** turno. As cinco dimensões são independentes; a única relação entre quaisquer duas delas é a exigência de `INTEGRAL` pelo `INTENSIVO`. A cardinalidade, a unicidade e a ordem canônica dos turnos permanecem exatamente como a ADR-0126 as definiu.

Esta ADR **complementa** a ADR-0126 — não a supersede nem reabre nenhum ponto dela.

## Consequências

### Positivas

- A oferta passa a declarar uma característica que antes não tinha onde morar, sem sobrecarregar nenhum vocabulário existente.
- As três situações de recusa têm códigos públicos distintos, todos associados ao campo `regimeDeFuncionamento`.
- O banco recusa token fora do domínio e a combinação `INTENSIVO` + `REGULAR`, além do que o agregado já recusa.
- A leitura individual, a listagem e a projeção cross-módulo devolvem o valor declarado, sem dedução.

### Negativas

- Exigir um campo novo no payload de criação e de atualização é mudança incompatível. A [ADR-0028](0028-versionamento-de-api-por-media-type.md) abre exceção de janela mínima zero em estágio greenfield, reinvocada aqui pelo mesmo motivo da ADR-0126: `oferta-curso` não tem integrador institucional, e o único consumidor é o frontend, que regenera os clients a partir do contrato. O media type permanece `v1`.
- O rollout precisa ser coordenado com o frontend: enquanto ele não enviar o campo, a criação e a atualização de oferta respondem 422.
- A coluna nasce `NOT NULL` sem valor default. Em ambiente com oferta preexistente, a migration falha (`23502`) até que o dado seja classificado explicitamente — é a recusa desejada, não um defeito.

### Neutras

- A `OfertaCurso` passa a ter cinco domínios fechados. O número não é problema por si; o cuidado é que cada um tenha nome que diga o que é.

## Confirmação

- Testes de domínio cobrem as três combinações válidas, a inválida, ausência, token fora do domínio e a não-derivação de erro a partir de erro.
- Testes provam que a recusa não muta o agregado: nem o regime de funcionamento, nem o regime de turno, nem os turnos.
- Dois CHECK constraints em `oferta_curso` espelham as invariantes: domínio do regime de funcionamento e compatibilidade com o regime de turno.
- Teste de persistência prova que a coluna é `NOT NULL` sem default — omitir o regime é `23502`, não um valor presumido.
- Testes de contrato cobrem os três wire codes públicos e a leitura das duas dimensões separadamente.

## Prós e contras das opções

### Campo próprio conferido contra o regime de turno

- Bom, porque cada vocabulário continua significando uma coisa só.
- Bom, porque a incompatibilidade vira recusa explícita, com código próprio.
- Ruim, porque acrescenta mais um campo obrigatório ao payload e mais uma coordenação de rollout com o frontend.

### Ampliar o domínio de `RegimeDeTurno`

- Bom, porque não acrescenta campo ao payload.
- Ruim, porque `INTENSIVO` não diz quantos turnos a oferta ocupa — é justamente o defeito que a ADR-0126 corrigiu ao tirar `INTEGRAL` do domínio de turno.
- Ruim, porque tornaria impossível representar a oferta extensiva integral, que é combinação válida.

### Derivar o regime de funcionamento de outro campo

- Bom, porque dispensa um campo no payload.
- Ruim, porque não há de onde derivar: `EXTENSIVO`+`INTEGRAL` e `INTENSIVO`+`INTEGRAL` são indistinguíveis por qualquer outro atributo da oferta.
- Ruim, porque o operador perderia a possibilidade de declarar intenção e ter o sistema conferindo contra ela.

## Mais informações

- Requisito canônico: `UNI-REQ-0138` — regime de funcionamento da oferta de curso. Publicação no registro canônico em andamento ([uniplus-developers#199](https://github.com/unifesspa-edu-br/uniplus-developers/issues/199)); a decisão do Product Owner que o origina é de 26 de agosto de 2026.
- `UNI-REQ-0137` — regime de turno da oferta de curso, cuja cardinalidade esta decisão não altera.
- [ADR-0126](0126-regime-de-turno-da-oferta-de-curso.md) — complementada, não superseded.
- [ADR-0066](0066-ofertacurso-modelo-tres-niveis-emec-por-campus.md) — modelo de três níveis da oferta, vigente.
- [ADR-0028](0028-versionamento-de-api-por-media-type.md) — exceção greenfield reinvocada para manter o media type `v1`.
- [ADR-0125](0125-dominio-fonte-unica-validacao-sem-fluentvalidation-duplicado.md) — as violações acumulam no mesmo `Result`, sem retorno antecipado.
