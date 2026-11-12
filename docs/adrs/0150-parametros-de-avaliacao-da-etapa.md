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

Na forma `HUMANA`, a etapa declara a **regra de consolidação**. Ela é uma regra do catálogo (`rol_de_regras`), de tipo próprio, referenciada por código, versão e hash, como as regras da classificação. As primeiras são:

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
- [ADR-0123](0123-tipos-etapa-configuraveis.md), [ADR-0113](0113-fase-x-etapa-eixo-temporal-e-eixo-de-pontuacao.md), [ADR-0110](0110-retificacao-como-sessao-editorial.md) e [ADR-0013](0013-motor-de-classificacao-como-servicos-de-dominio-puros.md).
- Issues #1622 (itens de avaliação da etapa) e #1046 (dupla correção).

## Emenda 1 (2026-11-12) — correção independente obrigatória, procedimento de recurso e corretores da regra

A decisão permanece: a etapa declara a forma de avaliação e a regra de consolidação, e os itens têm id estável. Esta emenda **prevalece sobre o texto que admite avaliação única na redação**, no parágrafo "O tipo de etapa não restringe a forma nem a regra".

- **Sinalizador "exige correção independente".** O cadastro do tipo de etapa ganha o sinalizador "exige correção independente", ligado nos tipos redação, carta de intenção e provas discursivas (UNI-REQ-0175, UNI-REQ-0097). A publicação do edital **recusa a avaliação única** em etapa de tipo com o sinalizador. O sinalizador é o que a ADR já previa para o caso de o PO decidir que a correção independente é obrigatória; o tipo, nesses casos, restringe a regra, como já restringe o caráter.
- **Tipo de regra "procedimento de recurso".** O rol de regras ganha o tipo **procedimento de recurso**, de lista fechada, versionado e congelado na publicação, como as demais regras. O edital o declara **por matéria do ciclo**, identificada pelo produtor (fase ou etapa), pelo tipo de decisão e por um alvo opcional.
- **Corretores da regra de consolidação.** O número de **corretores distintos** é dado da versão da regra de consolidação: `CONSOLIDACAO-CORRECAO-INDEPENDENTE`, 3; `CONSOLIDACAO-AVALIACAO-UNICA`, 1; sem consolidação humana, 0.

Nenhum outro trecho desta ADR muda.
