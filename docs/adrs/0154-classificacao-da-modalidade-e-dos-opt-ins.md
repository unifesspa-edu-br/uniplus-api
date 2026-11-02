---
status: "proposed"
date: "2026-10-31"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "Proteção de dados da Unifesspa (DPO) — consulta a enviar"
informed:
  - "Equipe Uni+"
---

# ADR-0154: Modalidade de concorrência e opções de concorrer classificadas como internas, com a classificação do derivado congelada

## Contexto e enunciado do problema

O catálogo de fatos semeado classifica como `Sensivel` a modalidade de concorrência (`MODALIDADE`), a modalidade da convocação (`MODALIDADE_CONVOCACAO`) e as opções "deseja concorrer" das reservas de pessoas com deficiência (`CONCORRER_PCD`), de pretos, pardos e indígenas (`CONCORRER_PPI`), de quilombolas (`CONCORRER_Q`) e das vagas por acréscimo (`CONCORRER_VAGA_POR_ACRESCIMO`), e como `Pessoal` as opções das reservas de egressos de escola pública (`CONCORRER_EP`) e por renda familiar per capita (`CONCORRER_RENDA`). O motivo registrado no seed é que as opções revelam a condição e a modalidade deriva delas. A modalidade da segunda opção de curso (`MODALIDADE_SEGUNDA_OPCAO`) é gravada na inscrição, mas não existe no catálogo.

Com a [ADR-0153](0153-projecao-por-protecao.md), a classificação decide quem vê cada item. Classificada como `Sensivel`, a modalidade some da operação de quem trabalha as inscrições sem acesso a dado sensível, embora seja publicada por obrigação legal nas listas de resultado, como a própria [ADR-0136](0136-catalogo-de-fatos-do-candidato-administravel.md) registra na seção "Governança". E, se só a modalidade mudasse, ela entregaria o que a opção de concorrer esconde.

Há também uma lacuna de congelamento. A Seleção congela a classificação do fato coletado, mas não a do fato derivado, que não é coletado; e a conferência da proteção das exigências lê o catálogo vivo para o fato não coletado, na publicação e na retificação. O fato coletado congela a classificação, mas não a hipótese legal nem a finalidade de tratamento, que a ADR-0153 exige no item.

O problema é classificar a modalidade e as opções de concorrer de forma coerente com o que elas revelam e com a publicação legal, sem enfraquecer a proteção da condição que dá direito à reserva, e fixar a classificação de todo fato no processo publicado.

## Drivers da decisão

- Classificar pelo que o valor revela, não pelo fato de que deriva.
- A modalidade e a opção de concorrer não podem divergir, para que uma não entregue o que a outra esconde.
- A condição (deficiência, cor ou raça, pertencimento étnico) e o documento que a comprova continuam sensíveis.
- O processo publicado não muda de classificação, hipótese legal ou finalidade por alteração posterior no catálogo.
- Toda classificação do catálogo passa pela aprovação da proteção de dados.

## Opções consideradas

- **A**: Manter a modalidade e as opções de concorrer como estão.
- **B**: Classificar a modalidade como `Interno` e manter as opções de concorrer como estão.
- **C**: Classificar a modalidade e as opções de concorrer como `Interno`, mantendo a condição e o comprovante na classificação própria.
- **D**: Herdar na modalidade a classificação mais restritiva das suas dependências, como o derivado do administrador.

## Resultado da decisão

**Escolhida:** "C — modalidade e opções de concorrer `Interno`", porque dizem em que reserva a pessoa concorre, e não qual é a condição dela.

### Reclassificação

- `MODALIDADE`, `MODALIDADE_SEGUNDA_OPCAO` (que entra no catálogo como fato de sistema) e `MODALIDADE_CONVOCACAO` passam a `Interno`.
- `CONCORRER_PCD`, `CONCORRER_PPI`, `CONCORRER_Q`, `CONCORRER_VAGA_POR_ACRESCIMO`, `CONCORRER_EP` e `CONCORRER_RENDA` passam a `Interno`. "Deseja concorrer" é a mesma declaração de intenção que a modalidade traduz.
- Continuam `Sensivel` os fatos com o valor real da condição: deficiência e seu tipo, cor ou raça, autodeclaração quilombola e indígena, atendimento especializado. Continuam `Pessoal` os de origem escolar e de renda.
- A hipótese legal continua sendo o cumprimento de obrigação legal, agora pelo art. 7º da LGPD, como a ADR-0136 exige abaixo de `Sensivel`.

O motivo:

- Concorrer numa reserva é declaração de intenção. Não comprova a condição, que só a homologação confirma, nem diz qual é ela: concorrer na reserva de pessoas com deficiência não diz qual é a deficiência.
- A publicação do resultado por modalidade é obrigação legal, como faz também o INEP.
- São três níveis distintos: a modalidade em que a pessoa concorre (`Interno`), o tipo de deficiência que ela declarou (`Sensivel`) e o laudo que a comprova (`Sensivel`, arquivo).
- O mesmo argumento vale para as reservas de pretos, pardos e indígenas e de quilombolas, que derivam da autodeclaração étnico-racial: concorrer na reserva não comprova a condição.

### Não é exceção à ADR-0136

A regra de que o derivado nunca é mais fraco que a dependência mais restritiva ("Classificação do derivado do administrador", cujo exemplo é o derivado de cor ou raça) vale para o derivado por regra que o administrador cadastra. Para o fato de sistema, a seção "Governança" fixa a classificação no seed, com a justificativa legal, e cita a modalidade publicada nas listas de resultado. Esta decisão é uma reclassificação justificada de fatos de sistema. Com a emenda da ADR-0153 a essa seção, a semente dá só o valor inicial da classificação, que passa pela aprovação da proteção de dados.

### O comprovante segue o tipo de documento

A proteção do arquivo vem do tipo de documento (UNI-REQ-0183): o laudo cadastrado como `Sensivel` exige a permissão de documentos sensíveis em qualquer exigência a que esteja ligado. A ligação entre o fato e o documento que o comprova é da configuração do processo. A modalidade só aciona a exigência e não conta como fato comprovado; o fato comprovado é a condição, e a conferência da publicação já recusa o laudo cadastrado abaixo dela. A modalidade `Interno` não entra nessa conta, e não há grafo de dependências nem cálculo por processo.

### Congelamento no processo

- A publicação congela a classificação, a hipótese legal e a finalidade de tratamento de cada fato derivado, copiadas do catálogo, como já faz com a classificação do fato coletado. A classificação congelada do derivado passa a ser a fonte da conferência da proteção das exigências, na publicação e na retificação, no lugar do catálogo vivo.
- Todo fato coletado congela também a hipótese legal e a finalidade de tratamento, pela mesma regra da classificação.
- O processo publicado mantém o que congelou até a retificação, e o processo finalizado nunca muda.

### Conferência da proteção de dados

A reclassificação parte da semente como valor inicial e passa pela mesma aprovação da proteção de dados que todo o catálogo, no fluxo da ADR-0153, junto com o restante do catálogo semeado. Depois de aprovada, só muda pela tela, com nova aprovação.

## Consequências

### Positivas

- Quem opera as inscrições vê a reserva em que cada pessoa concorre sem acesso a dado sensível, como já a vê o público no resultado.
- Modalidade e opção de concorrer têm a mesma classificação, sem um caminho que revele o que o outro esconde.
- A condição e o comprovante continuam sensíveis.
- O processo publicado deixa de depender do catálogo vivo para a classificação, a hipótese legal e a finalidade.

### Negativas

- A modalidade e as opções de concorrer passam a ser vistas por todo perfil com a permissão de dado interno, um grupo maior que o de hoje.
- A reclassificação depende da conferência da proteção de dados, que precisa acontecer antes da configuração do primeiro processo real.

### Neutras

- O comentário do seed que justifica a classificação sensível da modalidade é corrigido junto.

## Confirmação

- Teste do seed: as três modalidades e as seis opções de concorrer são `Interno`, com hipótese legal do art. 7º; os fatos da condição continuam `Sensivel`.
- Teste da publicação: a classificação, a hipótese legal e a finalidade de todo fato coletado e derivado são congeladas; alterar o catálogo depois não muda o processo publicado.
- Teste da conferência das exigências: na publicação e na retificação, usa a classificação congelada do derivado; a modalidade que só aciona a exigência não conta como fato comprovado; o laudo cadastrado abaixo da condição comprovada é recusado.

## Prós e contras das opções

### A — Manter como está

- Bom, porque não muda nada.
- Ruim, porque esconde da operação o que a lei manda publicar e trata intenção como condição.

### B — Só a modalidade `Interno`

- Bom, porque libera a informação operacional.
- Ruim, porque a modalidade passa a entregar o que a opção de concorrer esconde.

### C — Modalidade e opções `Interno`

- Bom, porque classifica pelo que o valor revela e mantém sensíveis a condição e o comprovante.
- Ruim, porque depende da conferência da proteção de dados.

### D — Herdar das dependências

- Bom, porque é regra mecânica.
- Ruim, porque aplica ao fato de sistema a regra do derivado do administrador e torna sensível o que a lei manda publicar.

## Mais informações

- [ADR-0136](0136-catalogo-de-fatos-do-candidato-administravel.md): seção "Governança" (classificação do fato de sistema fixada no seed, com a justificativa legal) e "Classificação do derivado do administrador" (que não se aplica a fato de sistema).
- [ADR-0153](0153-projecao-por-protecao.md): projeção por proteção e fluxo de aprovação da proteção de dados.
- [ADR-0081](0081-lgpd-by-design-dto-por-permissao.md): escala de classificação.
- [ADR-0144](0144-regra-de-aplicabilidade-pertence-a-modalidade.md): a regra que deriva a modalidade pertence a cada modalidade.
- UNI-REQ-0143 (catálogo de fatos) e UNI-REQ-0183 (proteção do tipo de documento).
- Lei 13.709/2018 (LGPD), arts. 5º, 7º e 11.
