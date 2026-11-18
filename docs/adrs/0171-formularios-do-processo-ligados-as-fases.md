---
status: "accepted"
date: "2026-11-13"
decision-makers:
  - "Tech Lead"
consulted:
  - "CEPS (dono do processo)"
informed:
  - "Equipe Seleção"
  - "Equipe Configuração"
---

# ADR-0171: Formulários do processo identificados por código e ligados às fases

## Contexto e enunciado do problema

A [ADR-0137](0137-formularios-por-finalidade-e-modelos-por-tipo-de-processo.md) deu ao processo um formulário por finalidade fixa (inscrição, isenção da taxa e habilitação). O edital de Medicina 2027 pede mais de um formulário na mesma fase, como o segundo formulário da habilitação e formulários facultativos de dados complementares, e nenhum deles cabe numa finalidade. O UNI-REQ-0144 passou a descrever o processo como composto de quantos formulários forem necessários, cada um ligado a uma fase do cronograma e obrigatório ou facultativo.

Com a finalidade fixa, a mesma marca é ao mesmo tempo a identidade do formulário, a ligação dos campos com ele, a ligação com a fase e a regra de quais respostas um formulário pode usar de outro. Aceitar um segundo formulário numa fase exige mudar as quatro coisas juntas.

## Drivers da decisão

- O processo tem quantos formulários forem necessários, inclusive mais de um na mesma fase (UNI-REQ-0144).
- O que varia entre editais é configuração, não código: nenhuma lista fixa de usos de formulário.
- Os campos, termos e exigências de documento pertencem a um formulário, não a um uso.
- O snapshot congelado de cada versão publicada precisa identificar o formulário de forma estável.

## Opções consideradas

- Manter a finalidade e acrescentar novas finalidades a cada caso.
- Formulário identificado por código no processo, ligado a uma fase, obrigatório ou facultativo.

## Resultado da decisão

**Escolhida:** "Formulário identificado por código no processo, ligado a uma fase, obrigatório ou facultativo", porque a fase e a obrigatoriedade já dizem tudo o que a finalidade dizia, sem limitar quantos formulários cada fase tem.

- **Identidade:** cada formulário tem um código único no processo, que não muda depois de criado. Não há lista fixa de finalidades.
- **Fase:** o formulário fica ligado a uma fase do cronograma do processo e só é publicado se essa fase existe. Uma fase pode ter mais de um formulário.
- **Obrigatoriedade:** cada formulário é obrigatório ou facultativo. O que a fase é (inscrição, janela de isenção, habilitação) vem do cronograma, e não do formulário.
- **Pertença:** campos, grupos, termos e exigências de documento pertencem a um formulário do processo. A exigência de documento declara o formulário que a entrega, e não a finalidade, como pedia a emenda de 2026-10-04 da ADR-0137.
- **Reaproveitamento:** um formulário só usa respostas de formulário obrigatório já enviado, da mesma fase ou de fase anterior; a regra de alcance é uma só para todos os formulários.
- **Modelos:** o modelo de formulário declara se serve à fase que coleta inscrição, caso em que traz o conjunto básico, e se é obrigatório; o processo parte dessa declaração e pode ajustá-la.
- **Continua da ADR-0137:** regras no item, produtor único por fato, termos congelados, modelos aplicados ao processo por cópia (ADR-0061) e a emenda às ADRs 0109 e 0110, pela qual o codec único do envelope vale até a primeira publicação em produção.

## Consequências

### Positivas

- Um formulário novo numa fase é configuração, não mudança de código.
- A regra de alcance das respostas fica num único lugar, e a mudança dela não se espalha.

### Negativas

- A forma do envelope congelado muda, e os processos de homologação são recriados.
- As conferências que dependiam da finalidade passam a ser feitas pela fase e pela obrigatoriedade.

## Confirmação

- Teste de domínio: dois formulários na mesma fase são aceitos, e o formulário ligado a fase fora do cronograma é recusado na publicação.
- Teste de domínio: o snapshot identifica etapas, campos e termos pelo código do formulário, com hash determinístico.

## Prós e contras das opções

### Manter a finalidade

- Bom, porque não muda o modelo atual.
- Ruim, porque cada uso novo exige código e ADR, e a fase continua sem poder ter dois formulários do mesmo uso.

### Formulário por código ligado à fase

- Bom, porque a quantidade de formulários e o uso de cada um são configuração.
- Ruim, porque exige refazer a identidade do formulário no snapshot.

## Mais informações

- Substitui a [ADR-0137](0137-formularios-por-finalidade-e-modelos-por-tipo-de-processo.md).
- UNI-REQ-0017, UNI-REQ-0084, UNI-REQ-0144 e UNI-REQ-0145.
- [ADR-0061](0061-copia-por-valor-em-vez-de-referencia.md), [ADR-0109](0109-envelope-canonico-v2-do-congelamento.md) e [ADR-0110](0110-retificacao-como-sessao-editorial.md).
