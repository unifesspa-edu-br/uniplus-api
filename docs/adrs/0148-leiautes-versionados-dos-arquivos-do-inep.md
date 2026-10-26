---
status: "accepted"
date: "2026-10-26"
decision-makers:
  - "Tech Lead"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0148: Os arquivos de notas seguem leiautes versionados, e o administrador escolhe o leiaute na importação

## Contexto e enunciado do problema

As notas de uma etapa podem chegar por arquivo. O primeiro caso é o do ENEM. Para cada edição, o CEPS envia ao INEP a lista de CPFs dos inscritos que escolheram aquela edição. O INEP devolve um arquivo de texto com o nº de inscrição, o CPF, as notas das cinco áreas e a situação do candidato em cada prova. Outros casos virão, como o arquivo da leitora óptica de uma prova objetiva própria.

O arquivo do INEP não tem cabeçalho, e a leitura é feita pela posição da coluna. O leiaute muda entre edições. Nas amostras do CEPS, as edições até 2021 têm 23 campos e as de 2022 em diante têm 20, porque deixaram de vir colunas do meio. Ler um arquivo com o leiaute errado não dá erro: a nota de uma área é gravada como se fosse de outra.

A Unifesspa usa as edições do ENEM de 2024 em diante. Nada garante que o leiaute de 2026 ou de 2027 continue igual.

## Drivers da decisão

- Um leiaute novo entra sem mudar a importação, o cruzamento com as inscrições nem o cálculo da nota.
- A nota de uma área nunca é gravada como nota de outra por causa de um arquivo de outro formato.
- Cada nota importada é rastreável ao leiaute com que entrou.
- Do arquivo, só é gravado o que a avaliação usa e o que a conferência com o INEP exige.

## Opções consideradas

- **A.** Um leitor único, que segue o leiaute atual e é alterado a cada mudança do arquivo.
- **B.** Mapeamento de colunas cadastrado pelo administrador na tela.
- **C.** Catálogo de leiautes versionados no código, compostos de colunas, e o administrador escolhe o leiaute na importação.

## Resultado da decisão

**Escolhida:** "C".

Cada leiaute tem código e versão, e o hash da sua definição o identifica, no mesmo molde da identidade das regras do Seleção. O leiaute declara:

- o separador, a codificação e o número de campos;
- para cada posição, o campo do Uni+ e o leitor do valor (CPF, nota, data, código de situação);
- quais colunas o Uni+ não usa. Essas colunas são ignoradas e não são gravadas.

Um leiaute novo é composto dos leitores que já existem, e só um formato que nenhum deles cobre pede um leitor novo. Todos os leiautes produzem o mesmo conjunto fechado de campos, e só esses campos chegam ao cruzamento com as inscrições e à avaliação. Nenhum consumidor conhece o leiaute.

Na importação, o administrador escolhe o leiaute entre os ativos. O arquivo é conferido contra a estrutura do leiaute escolhido, e o arquivo que não confere é recusado inteiro, sem gravar nenhuma nota. O lote registra o código, a versão e o hash do leiaute usado.

O arquivo de envio ao INEP segue a mesma regra. O leiaute que deixa de valer é desativado e sai da escolha, e o lote que o usou continua a citá-lo.

Os primeiros leiautes são o de envio e o de retorno do ENEM, conferidos com o retorno da edição de 2024.

## Consequências

### Positivas

- Uma mudança de leiaute vira uma versão nova, sem tocar a importação nem o cálculo.
- O arquivo cuja estrutura não confere com o leiaute escolhido é recusado, em vez de gravar nota trocada.
- Cada nota aponta o leiaute e a versão com que foi lida.

### Negativas

- Leiaute novo exige entrega de código. Antes dela, o CEPS não importa arquivo de formato desconhecido.
- A conferência é estrutural. Dois leiautes com o mesmo número de campos e as mesmas colunas numéricas em posições trocadas só se distinguem pela escolha correta do administrador.

### Neutras

- A leitura posicional continua, porque o arquivo do INEP não tem cabeçalho. O leiaute só a torna explícita e versionada.

## Confirmação

- Cada leiaute tem teste com arquivo sintético, gerado com as personas fictícias do portal, que prova a posição de cada área e de cada código de situação, inclusive na linha de quem faltou a uma prova.
- Um arquivo com o número de campos de outro leiaute é recusado, sem nota gravada.
- O cruzamento com as inscrições e a avaliação não referenciam nenhum leiaute.

## Prós e contras das opções

### A. Leitor único

- Bom, porque é o menor código hoje.
- Ruim, porque cada mudança altera o leitor de todas as edições, e um arquivo antigo passa a ser lido com o leiaute novo.

### B. Mapeamento cadastrado na tela

- Bom, porque o CEPS cadastra um leiaute novo sem entrega de código.
- Ruim, porque um erro de posição troca a nota de uma área pela de outra sem nenhum aviso, e o mapeamento não tem teste antes do uso.

### C. Leiautes versionados no código

- Bom, porque cada versão tem teste antes de chegar ao administrador, e a conferência recusa o arquivo de outra estrutura.
- Ruim, porque leiaute novo depende de entrega de código. Isso é aceitável para uma mudança que vem no máximo uma vez por edição.

## Mais informações

- [ADR-0013](0013-motor-de-classificacao-como-servicos-de-dominio-puros.md) deixa ao administrador a composição de primitivas existentes, sem código. Esta ADR se afasta disso de propósito: até a composição de leitores é código, porque um erro de posição é silencioso e precisa de teste antes do uso.
- [ADR-0129](0129-identidade-da-origem-decide-conformidade.md) admite identificar pelo código um catálogo governado no código e sem edição, que é o caso do leiaute.
- [ADR-0149](0149-nota-da-etapa-por-inscricao.md) define onde as notas importadas são gravadas.
- Issues #1602 (importação das notas do ENEM) e unifesspa-edu-br/uniplus-web#848 (tela de importação).
