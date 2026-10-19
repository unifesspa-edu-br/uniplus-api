---
status: "accepted"
date: "2026-10-18"
decision-makers:
  - "Tech Lead"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0145: O recurso também pertence à etapa, e cada janela declara se admite instância superior

## Contexto e enunciado do problema

A [ADR-0113](0113-fase-x-etapa-eixo-temporal-e-eixo-de-pontuacao.md) deu à fase o ato produzido e a regra de recurso, e à etapa o peso, o caráter e a nota mínima. Os editais pedem mais do que isso. A fase já declara vários atos, cada um com o seu papel, mas cada prova ou procedimento tem o seu próprio resultado e o seu próprio público, e o recurso acompanha esse resultado, não a fase. Há edital com recurso contra o gabarito e, depois, contra o resultado de uma prova. E há edital cujo procedimento de habilitação tem etapas para públicos diferentes, cada uma com resultado preliminar e recurso próprios. É o caso de um edital cuja habilitação tem três etapas para públicos diferentes, cada uma com resultado preliminar e recurso de dois dias úteis, e em que numa delas a banca recursal encerra o recurso, sem instância superior. O catálogo de fases canônicas já traz Habilitação, Heteroidentificação e Avaliação biopsicossocial, e o cronograma da Seleção as declara; esta ADR não afirma que a habilitação inteira pertence à Seleção.

O código já dá produtos e janelas de recurso à etapa, desde as migrations de produtos e de recursos da etapa. Nenhuma ADR registra a decisão, e quem lê a ADR-0113 conclui que recurso é só da fase e pode "corrigir" o código ou recusar a configuração que o edital exige. Esta ADR registra o que o código já implementa; só a instância superior por janela é decisão nova.

## Drivers da decisão

- O certame tem de ser exprimível como o edital o escreve, com o prazo de interposição congelado por janela (UNI-REQ-0080).
- A janela de recurso é configuração congelada com o processo, não ramificação em código.
- O prazo corre de um instante que a configuração precisa nomear sem ambiguidade.
- Nem todo recurso tem instância superior, e o edital diz quando não tem.

## Opções consideradas

- **A.** Manter o recurso só na fase e representar cada etapa recorrível como uma fase própria.
- **B.** A etapa declara os atos que publica e as janelas de recurso que abre; a regra de prazo é a mesma da fase.
- **C.** Janela de recurso por etapa sem regra de prazo compartilhada com a fase.

## Resultado da decisão

**Escolhida:** "B". A regra de recurso não é exclusiva da fase. A etapa declara:

- **Produtos.** Os atos que a etapa publica, cada um com o papel de preliminar ou definitivo, ou sem papel quando o ato não é resultado. O produto declara intenção; o ato publicado é fato de Publicações.
- **Janelas de recurso.** Zero ou mais por etapa, cada uma com prazo e suspensividade, nos mesmos termos da regra de recurso da fase. O domínio usa a mesma regra do catálogo (`RECURSO-PRAZO-ANCORADO-EM-ATO`) e os mesmos argumentos de prazo e suspensividade nas duas âncoras.
- **Duas âncoras.** O prazo corre do **resultado preliminar publicado pela própria etapa**, no instante de publicação declarado pelo ato, absoluto e com deslocamento em relação a UTC (UNI-REQ-0115), ou da **ciência individual** do candidato, caso em que o instante inicial é a ciência de cada candidato. A âncora em ciência exige que a etapa prometa parecer individual, porque é dele que o candidato toma ciência.
- **Uma janela por fato recorrível.** Cada resultado preliminar e a ciência individual abrem uma janela só; dois prazos para o mesmo fato seriam ambíguos.
- **Etapa com nota do ENEM.** Ninguém publica nem atribui a nota na etapa: o resultado é publicado pela fase, e o recurso coletivo sobre a lista geral é o da regra de recurso da fase. Na etapa cabe só o recurso individual, ancorado na ciência do candidato.

**Instância superior (decisão do líder técnico de 18/10/2026).** Cada janela de recurso, da fase ou da etapa, declara se admite recurso em instância superior. Sem instância superior, a janela não tem tempo de bloqueio superior, e o registro de recurso superior contra ela é recusado. A declaração vale para a janela, não para o processo: o mesmo edital pode ter janelas com e sem instância superior. Ela convive com a suspensividade da instância superior assim: se a janela não admite instância superior, a suspensividade superior é proibida; se admite, a suspensividade superior vazia continua significando que a pendência não bloqueia.

A declaração é um campo novo congelado no envelope da publicação, o que muda o envelope canônico e o hash ([ADR-0100](0100-canonicalizacao-hash-snapshot-publicacao.md)). A implementação e a nova versão do envelope são da uniplus-api#1991.

**Requisitos alterados.** A decisão altera o UNI-REQ-0080, porque o que se congela passa a ser por janela, da fase ou da etapa, com a declaração de instância superior; o UNI-REQ-0081, cujo registro da instância superior passa a valer só para a janela que a admite; e exige ajustar o UNI-REQ-0115, que hoje trata só da âncora no instante de publicação do ato e não da âncora por ciência individual. As atualizações são da uniplus-developers#339.

## Consequências

### Positivas

- O edital com recurso por prova e por procedimento de habilitação se exprime sem fase artificial.
- A conferência de código tem ADR contra a qual conferir, e a janela da etapa deixa de parecer excesso.
- O "não caberá novo recurso" do edital vira dado da janela, e o sistema o faz cumprir.

### Negativas

- O recurso passa a ter dois donos possíveis, fase e etapa; cada conferência de publicação precisa olhar os dois.
- Toda janela passa a exigir a declaração de instância superior.

## Confirmação

O domínio recusa janela de etapa com âncora inválida ou duplicada, e o processo recusa produto e recurso em ato na etapa com nota do ENEM. A recusa de recurso superior sem instância superior declarada é implementada em uniplus-api#1991.

## Prós e contras das opções

### A. Recurso só na fase

- Bom, porque mantém um dono único.
- Ruim, porque obriga a inventar fase para cada ciclo recursal e quebra o cronograma, que é o eixo do tempo.

### B. Produto e janela na etapa, regra de prazo compartilhada

- Bom, porque a etapa publica e recorre como o edital descreve, e o prazo é tipado uma só vez.
- Ruim, porque o recurso passa a ter dois donos.

### C. Janela da etapa com regra de prazo própria

- Bom, porque permite evoluir etapa e fase em separado.
- Ruim, porque duplica conhecimento: as mesmas invariantes de prazo e suspensividade em dois lugares.

## Mais informações

- [ADR-0113](0113-fase-x-etapa-eixo-temporal-e-eixo-de-pontuacao.md) (Emenda 4), [ADR-0100](0100-canonicalizacao-hash-snapshot-publicacao.md).
- UNI-REQ-0080, UNI-REQ-0081, UNI-REQ-0115.
- Implementação da instância superior: uniplus-api#1991.
