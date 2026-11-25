---
status: "accepted"
date: "2026-10-26"
decision-makers:
  - "Tech Lead"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0150: A etapa declara a forma de avaliação e a regra de consolidação, e os itens de avaliação têm identidade estável

## Contexto e enunciado do problema

O processo seletivo é configurável. Um processo com prova, entrevista, redação ou carta de intenção precisa declarar, já na configuração, tudo o que o lançamento e a apuração da nota vão usar (ADR-0149). Hoje a etapa declara caráter, peso e nota mínima, e o tipo de etapa diz se a nota vem do ENEM. Faltam três coisas:

- como a nota é obtida;
- como várias avaliações humanas viram uma nota;
- quais são os critérios ou blocos em que a etapa se divide.

A dupla correção (UNI-REQ-0097) é "configurada" no componente de nota, e "a fonte da regra é o edital". Ela não é uma terceira forma ao lado da prova objetiva e da avaliação humana: ela qualifica a avaliação humana.

## Drivers da decisão

- Dois editais podem avaliar a mesma espécie de etapa de formas diferentes.
- A regra de apuração muda sem retroagir a edital publicado.
- A nota e o recurso conseguem apontar para um critério específico, mesmo depois de uma retificação.

## Opções consideradas

- **A.** A forma de avaliação, com a dupla correção como um dos valores, como atributo do tipo de etapa.
- **B.** Na etapa, dois eixos: a forma de avaliação e, para a avaliação humana, a regra de consolidação do catálogo de regras.

## Resultado da decisão

**Escolhida:** "B".

A etapa declara a **forma de avaliação**:

- `OBJETIVA`: gabarito ou leitora, sem avaliador;
- `HUMANA`: pessoas da banca da etapa lançam as notas.

Na forma `HUMANA`, a etapa declara a **regra de consolidação**. Ela é uma regra do catálogo (`regras_do_catalogo`), de tipo próprio, referenciada por código, versão e hash, como as regras da classificação. As primeiras são:

- a avaliação única;
- a correção independente da UNI-REQ-0097.

Uma regra nova ou uma mudança de regra vira versão nova. A etapa do ENEM não declara forma nem regra, e o tipo de etapa não muda: a origem no ENEM continua sendo identidade do tipo (ADR-0123).

O tipo de etapa não restringe a forma nem a regra. Uma redação com avaliação única é decisão do edital. Se o PO decidir que a correção independente é obrigatória em alguma espécie de etapa, o tipo ganha um sinalizador que restringe a regra, como já restringe o caráter.

Os **itens de avaliação** da etapa (critérios, blocos) são componentes de nota da UNI-REQ-0087. O id de cada item é congelado no snapshot e preservado na retificação, porque o lançamento e o recurso apontam para ele. É o mesmo motivo que já faz o envelope congelar o id da etapa, e esta ADR emenda a decisão D2 da ADR-0110, que rejeita congelar outros ids de filha, para incluir os itens. O id do item entra no envelope e no hash como o id da etapa, e a ordem dos itens não depende do id, de modo que a regra de ordenação da ADR-0109 (D9) continua valendo. Na retificação, o item é reconciliado pelo id, como a etapa.

## Consequências

### Positivas

- A configuração diz tudo o que a apuração precisa, e o snapshot congela isso.
- A dupla correção e as regras futuras evoluem por versão, sem retroagir.
- O recurso contra um critério continua apontando para ele depois de uma retificação.

### Negativas

- A combinação livre entre tipo e forma admite configurações que a prática pode não usar.
- O envelope passa a congelar mais um id de filha, que também entra no hash.

## Confirmação

- A etapa com nota que não é do ENEM não publica sem forma de avaliação, e na forma `HUMANA` não publica sem regra de consolidação.
- A referência da regra de consolidação só aceita regra do tipo de consolidação.
- O id de um item é o mesmo no snapshot de duas versões do mesmo processo retificado.

## Prós e contras das opções

### A. Forma de avaliação no tipo de etapa

- Bom, porque o administrador escolhe menos coisas por etapa.
- Ruim, porque o tipo passa a afirmar o que o edital decide, a mesma segunda fonte de verdade que a ADR-0113 tirou do cadastro de fases. Além disso, um tipo criado pela API não teria como declarar a forma.

### B. Forma e regra de consolidação na etapa

- Bom, porque segue o edital e reaproveita o versionamento das regras.
- Ruim, porque a etapa ganha mais campos para preencher.

## Mais informações

- [ADR-0149](0149-nota-da-etapa-por-inscricao.md): lançamentos e avaliação apurada.
- [ADR-0123](0123-tipos-etapa-configuraveis.md), [ADR-0113](0113-fase-x-etapa-eixo-temporal-e-eixo-de-pontuacao.md), [ADR-0110](0110-retificacao-como-sessao-editorial.md) e [ADR-0013](0013-classificacao-dos-candidatos-como-servicos-de-dominio-puros.md).
- Issues #1622 (itens de avaliação da etapa) e #1046 (dupla correção).

## Emenda 1 (2026-11-12) — correção independente obrigatória, procedimento de recurso e corretores da regra

A decisão permanece: a etapa declara a forma de avaliação e a regra de consolidação, e os itens têm id estável. Esta emenda **prevalece sobre o texto que admite avaliação única na redação**, no parágrafo "O tipo de etapa não restringe a forma nem a regra".

- **Sinalizador "exige correção independente".** O cadastro do tipo de etapa ganha o sinalizador "exige correção independente", ligado nos tipos redação, carta de intenção e provas discursivas (UNI-REQ-0175, UNI-REQ-0097). A publicação do edital **recusa a etapa de tipo com o sinalizador cuja regra de consolidação tem menos de dois corretores distintos** (UNI-REQ-0393), e a avaliação única, de um só corretor, é o caso comum. O número vem da versão da regra no catálogo, lido ao congelar, e a regra cujo número não foi lido é recusada, sem presumir valor; a gravação da etapa não recusa a regra. O sinalizador é o que a ADR já previa para o caso de o PO decidir que a correção independente é obrigatória; o tipo, nesses casos, restringe a regra, como já restringe o caráter.
- **Tipo de regra "procedimento de recurso".** O rol de regras ganha o tipo **procedimento de recurso**, de lista fechada, versionado e congelado na publicação, como as demais regras. O edital o declara **por matéria do ciclo**, identificada pelo produtor (fase ou etapa), pelo tipo de decisão e por um alvo opcional.
- **Corretores da regra de consolidação.** O número de **corretores distintos** é dado da versão da regra de consolidação: `APURACAO_CORRECAO_INDEPENDENTE`, 3; `APURACAO_AVALIACAO_UNICA`, 1; sem consolidação humana, 0.

Nenhum outro trecho desta ADR muda.

## Emenda 2 (2026-11-16) — a regra de consolidação segue a natureza do conceito da etapa

**Contexto.** A etapa passa a ser avaliada por um conceito, numérico ou categórico (ADR-0169), e combinar notas por média não faz sentido para "apto" e "inapto". Esta emenda especifica a disposição "Corretores da regra de consolidação" da emenda 1: o número de corretores distintos, que ali é dado da versão, passa a ser um de vários metadados que a versão declara de si. Não altera as quantidades fixadas na emenda 1.

**Decisão.** Distingue-se o que a versão da regra declara do que o edital escolhe.

- **A versão da regra declara, no `declaracao_da_regra`, e fixa por versão:** as naturezas de conceito a que se aplica (`naturezas_do_conceito`) e os corretores distintos. A avaliação única aceita as duas naturezas e a correção independente só a numérica. A consolidação categórica, regra nova do rol, declara também o critério (unanimidade, maioria simples ou maioria absoluta) e o tratamento do empate e da falta de unanimidade. Variar um desses valores é versão nova da regra, nunca parâmetro do edital nem valor fixo no código.
- **O edital escolhe** qual regra e qual versão do rol a etapa referencia. A etapa com regra de consolidação declara conceito, e a regra cujas naturezas não incluem a do conceito é recusada.

**Consequências.** A versão nova de uma regra pode ampliar as naturezas sem alcançar a versão que um edital publicado já congelou. A etapa com regra de consolidação e sem conceito é pendência tolerada em rascunho e cobrada na publicação e na retificação. Cada código de recusa novo tem página no portal de desenvolvedores.

## Emenda 3 (2026-11-16) — a regra de consolidação de conceito categórico declara critério, corretores e tratamento

**Contexto.** A emenda 2 prevê a consolidação categórica com critério e tratamento declarados pela versão, sem fixar o formato nem o conjunto de tratamentos. A apuração do valor do conceito precisa desses dados lidos do catálogo, e nenhum deles pode ficar fixo no código, porque as bancas variam o critério, o número de corretores e o que fazer quando os corretores não chegam a um valor.

**Decisão.** O rol ganha três regras de consolidação, `APURACAO_CONCEITO_UNANIMIDADE`, `APURACAO_CONCEITO_MAIORIA_SIMPLES` e `APURACAO_CONCEITO_MAIORIA_ABSOLUTA`, aplicáveis só ao conceito categórico. A versão da regra declara no `declaracao_da_regra`:

- `criterio`: `UNANIMIDADE`, `MAIORIA_SIMPLES` ou `MAIORIA_ABSOLUTA`;
- `corretores_distintos`: o número de corretores que a versão exige;
- `naturezas_do_conceito`: as naturezas a que a versão se aplica;
- `quando_nao_consolida`: o tratamento quando o critério não converge (falta de unanimidade ou empate), `NOVO_CORRETOR` ou `DECISAO_DA_BANCA`.

O edital só escolhe qual regra e qual versão referenciar. Mudar o critério, os corretores ou o tratamento é versão nova da regra (ADR-0112), sem alcançar a versão que um edital publicado já congelou. O conjunto de tratamentos é fechado e só cresce por emenda desta ADR. A leitura do esquema falha, e nada é presumido, quando uma das chaves falta, tem tipo errado ou token desconhecido; as regras numéricas não declaram critério nem tratamento.

**Consequências.** Critério, número de corretores e tratamento viram dado congelado com a versão da regra, e a apuração decide pelo snapshot, não pelo cadastro vivo. Um tratamento novo exige emenda, o que mantém o conjunto auditável. Na unanimidade, acrescentar corretores não resolve a divergência, então a versão semeada declara `DECISAO_DA_BANCA`; as versões de maioria declaram `NOVO_CORRETOR`, e a banca que prefere outro tratamento precisa de versão própria da regra. Quando o tratamento é `NOVO_CORRETOR`, o mínimo de integrantes da banca (UNI-REQ-0387) conta os corretores distintos mais um, porque o novo corretor é pessoa da banca fora dos corretores originais; nos demais casos, o mínimo conta só os corretores distintos.

## Emenda 4 (2026-11-23) — forma do conceito

A natureza do conceito passa a se chamar **forma do conceito**, e o enum é um só, no projeto compartilhado de regras, lido pela Configuração e pela Seleção. O código `CATEGORICO` passa a `POR_OPCOES`; `NUMERICO` não muda. A chave que a versão da regra declara no `declaracao_da_regra` para dizer a que formas se aplica passa de `naturezas_do_conceito` a `formas_do_conceito`, com os mesmos códigos do enum. O restante desta ADR não muda.
