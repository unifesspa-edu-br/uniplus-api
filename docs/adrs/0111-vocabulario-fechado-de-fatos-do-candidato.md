---
status: "accepted"
date: "2026-07-15"
decision-makers:
  - "Tech Lead"
consulted:
  - "CEPS (dono do processo)"
informed:
  - "Equipe Seleção"
  - "Equipe Configuração"
---

# ADR-0111: Vocabulário fechado de fatos do candidato

## Contexto e enunciado do problema

O gatilho condicional de uma exigência documental e a condição de exibição de um
campo de formulário precisam avaliar um **predicado sobre o candidato** — "é
cotista da modalidade X", "solicitou atendimento Y", "tem renda per capita ≤ Z".
Hoje esse predicado não tem onde se apoiar: não existe, em nenhum módulo do
`uniplus-api`, um vocabulário fechado que diga **quais fatos sobre um candidato
podem ser citados**, **de que domínio** cada um é (categórico, booleano,
numérico) e **quais valores** cada um aceita.

O sintoma já está no código, não é hipotético. A regra de desempate
`DESEMPATE-PREDICADO-FATO` (semeada em `rol_de_regras`) tipa seus argumentos em
`ArgsDesempatePredicadoFato(string Fato, string Operador, string Valor)`, e o
próprio XML-doc dessa classe registra que o campo `Fato` é tipado como `string`
mas **não é validado contra vocabulário nenhum**, porque o catálogo ainda não
existe. Um administrador pode configurar um desempate com `Fato = "qualquercoisa"`
e o sistema aceita — o predicado é invalidável por construção.

Esta decisão fixa o **vocabulário fechado** e a sua **governança** — onde ele
vive, quem pode alterá-lo, e como uma mudança nele se comporta ao longo do tempo
sem invalidar predicados já congelados em versões publicadas (RN08). A construção
do cadastro (tabela, entidade, leitor cross-módulo) e do validador de predicado
ficam para stories seguintes; esta ADR não introduz código.

## Drivers da decisão

- Um predicado sobre o candidato precisa de um domínio decidível (o que torna a
  matriz operador × valor verificável).
- O mesmo vocabulário serve consumidores distintos (desempate, gatilho de
  documentos, condição de formulário) — não pode ser fixado por um deles.
- Um fato só é útil se existir código que **resolva** seu valor real; cadastrar
  um fato "solto" por tela produziria um fato inoperante.
- Um predicado publicado congela o **código** do fato por valor (RN08); mudar a
  semântica de um código existente reinterpretaria o passado.

## Opções consideradas

- **Cada consumidor fixa sua própria lista de fatos** (sem vocabulário central).
- **Vocabulário central editável por administrador** (padrão `TipoDocumento`/`Modalidade`).
- **Vocabulário central seed-governado, identidade imutável** (padrão `TipoRegra`/`RegraCatalogo`).

## Resultado da decisão

**Escolhida:** "Vocabulário central seed-governado, identidade imutável",
materializado como o catálogo `rol_de_fatos_candidato` no módulo `Configuracao`,
consumido por `Selecao` (e futuros módulos) via leitor síncrono. É a única opção
que ao mesmo tempo evita divergência entre consumidores, garante que todo fato
tenha código que o resolva, e protege predicados já congelados.

**Onde vive.** Em `Configuracao`, no schema `configuracao` do banco único
`uniplus`, consumido por outros módulos via leitor síncrono publicado em
`.Contracts` — o mesmo padrão de `ITipoDocumentoReader`/`TipoDocumentoView`, sob
a ADR-0056. A co-hospedagem no banco não elimina a fronteira do módulo: relações
internas permanecem intra-schema; quando o código do fato entra no envelope
canônico de publicação de um Processo Seletivo, entra **por valor** (o texto do
código, dentro do predicado já serializado), nunca por chave estrangeira
cross-schema: aplica-se a disciplina de snapshot-copy da ADR-0061 na topologia
de schema por módulo da ADR-0097.

**Quem governa.** **Seed-governado pelo time de desenvolvimento — não é CRUD
administrativo aberto.** É a decisão que mais diverge dos outros cadastros de
`Configuracao` (`Modalidade`, `TipoDocumento` são editáveis por admin), e por um
motivo concreto: um `TipoDocumento` novo é só um rótulo, mas um **fato** só é útil
se existir o código que sabe **resolver** o seu valor a partir dos dados do
candidato. Cadastrar `RENDA_PER_CAPITA` por uma tela genérica produziria um fato
inoperante — o mesmo defeito que esta decisão fecha. Por isso o catálogo segue o
padrão de `TipoRegra` (enum fixo) + `RegraCatalogo` (linhas seed-governadas, sem
CRUD) — o precedente real de governança por seed no repositório. Adicionar um fato
é uma tarefa de desenvolvimento (PR com o código de resolução + entrada no seed),
nunca uma operação de tela.

A ADR-0058 (`ObrigatoriedadeLegal`) informa a **forma** — um vocabulário fechado e
tipado que alimenta um avaliador —, mas **não** a governança: ela decide,
deliberadamente, o oposto ("quando a lei muda, edita o catálogo — sem deploy"), um
catálogo editável por administrador. O vocabulário de fatos diverge nesse ponto de
propósito: um fato editável por tela nasceria inoperante.

**Como versiona.** `Codigo`, `Dominio`, `Natureza` e `Cardinalidade` são
**imutáveis desde a semeadura** — só o código entra, por valor, num predicado
congelado (RN08); se o domínio de um código existente mudasse, um predicado antigo
seria reinterpretado com outra semântica (a classe de risco que a ADR-0073 já
resolveu para os fatos de atendimento). `ValoresDominio` de um fato
categórico-estático **só cresce**: acrescentar um valor é seguro; remover ou
renomear um valor já citado é proibido, porque orfanaria predicado publicado.
`Descricao` e `BaseLegal` são cosméticos e podem ser corrigidos. **Não há coluna
de versão própria** (diferente de `RegraCatalogo`): um fato é uma classificação,
não uma regra executável — uma mudança incompatível é um **código novo** (ex.:
`RENDA_PER_CAPITA_V2`), não uma nova versão do mesmo código.

**Domínios (enum fixado por esta ADR): três** — `Categorico`, `Booleano`,
`Numerico`. Não há um quarto domínio "texto": um fato sem domínio decidível não é
avaliável por predicado.

Um fato categórico é **estático** ou de **escopo-processo**, e essa distinção não
é um campo próprio — é a **nulidade de `ValoresDominio`**: quando o conjunto de
valores vive no próprio catálogo (ex.: `COR_RACA`, `SEXO`), `ValoresDominio` os
enumera; quando os valores válidos vêm da oferta congelada do próprio processo
(ex.: `MODALIDADE`, `CONDICAO_ATENDIMENTO`), `ValoresDominio` é **nulo** e quem os
resolve é o consumidor, contra o snapshot. Booleano e numérico têm `ValoresDominio`
sempre nulo. O nulo é significante — nunca confundido com lista vazia (proibida).

Ortogonais ao domínio, cada fato declara ainda dois eixos próprios, ambos
imutáveis desde a semeadura:

- **`Natureza`** (origem do dado): `BrutoInformado` (respondido pelo candidato),
  `DeVontade` (opção ou aceite manifestado, não afirmação de elegibilidade) ou
  `Derivado` (computado pelo motor). Sustenta a invariante — feita cumprir pelo
  avaliador — de que um fato `Derivado` não é insumo de si mesmo nem de um gatilho
  avaliado antes da etapa que o produz. Os nove fatos desta colheita são todos
  `BrutoInformado`; o eixo existe para fatos futuros de outra origem.
- **`Cardinalidade`**: `Escalar` (um único valor) ou `Multivalorado` (um
  conjunto). Torna a semântica dos operadores decidível sem que o avaliador
  conheça o catálogo por dentro (ver "Cardinalidade e átomo canônico do predicado",
  abaixo).

### Matriz de compatibilidade operador × domínio

| Domínio | Operadores aceitos | Forma do valor |
|---|---|---|
| `Booleano` | `IGUAL` | escalar `sim`/`não` |
| `Numerico` | `IGUAL`, `MAIOR_IGUAL`, `MENOR_IGUAL` | escalar **inteiro** — decimal é rejeitado |
| `Categorico` (estático ou de escopo-processo) | `IGUAL`, `EM` | `IGUAL`: um código do domínio; `EM`: lista **não vazia** de códigos do domínio |

A restrição "numérico é inteiro, rejeita decimal" aplica-se ao **valor configurado
no predicado** e evita comparação de ponto flutuante no avaliador. A resolução do
fato pode preservar uma razão exata quando a divisão natural não é inteira, como
em `RENDA_PER_CAPITA`.

**O operador `EM` e o consumidor de desempate.** O consumidor
`DESEMPATE-PREDICADO-FATO` guarda hoje o valor do predicado como string única
(`ArgsDesempatePredicadoFato.Valor`), que o envelope canônico serializa como
escalar — forma incapaz de representar a lista de `EM`. Tornar `EM` utilizável
nesse consumidor **depende da migração desse valor para tipo/lista**, escopo da
story do validador de predicado (a mesma que fecha o gap de validação de `Fato`
contra este vocabulário). Enquanto essa migração não ocorrer, o desempate só
representa `IGUAL`; os consumidores novos (gatilho de documentos, condição de
formulário) nascem já com o valor tipado e representam `EM` desde o início. A
matriz acima é o contrato do vocabulário; a migração do `args` do desempate é a
condição para que o desempate a honre por inteiro.

### Cardinalidade e átomo canônico do predicado

O campo `Cardinalidade` do catálogo registra se o fato é escalar ou
multivalorado. Os categóricos estáticos, o booleano e o numérico são escalares.
`MODALIDADE` e `CONDICAO_ATENDIMENTO`, contudo, são **multivalorados**: uma
inscrição pode ter mais de uma modalidade ou mais de uma condição de atendimento.
Para esses fatos,
`IGUAL X` significa que `X` pertence ao conjunto de valores do candidato e
`EM [X, Y, ...]` significa que a interseção entre os valores do candidato e a
lista configurada não é vazia. A lista de `EM` nunca é vazia. Não há redução
arbitrária do conjunto a um escalar.

Todos os consumidores usam o mesmo átomo tipado `{ Fato, Operador, Valor }`:
`Valor` é escalar para `IGUAL`, `MAIOR_IGUAL` e `MENOR_IGUAL`, e lista tipada
para `EM`. O contrato legado
`ArgsDesempatePredicadoFato(string Fato, string Operador, string Valor)` não
representa `EM`; a story do validador deve migrá-lo, junto com o codec do
envelope, para esse valor tipado antes de habilitar `EM` no desempate. Até essa
migração, um consumidor que só aceite string não pode configurar `EM`.

### O vocabulário (nove fatos)

Todos os nove são de `Natureza` `BrutoInformado`; a coluna abaixo mostra a
`Cardinalidade`, e a distinção estático × escopo-processo aparece na coluna
"Valores / fonte" (valores enumerados = estático; "fonte: cadastro vivo" =
escopo-processo, `ValoresDominio` nulo).

| Código | Domínio | Cardinalidade | Valores / fonte | Base legal / observação |
|---|---|---|---|---|
| `COR_RACA` | Categórico | Escalar | `BRANCA`, `PRETA`, `PARDA`, `AMARELA`, `INDIGENA`, `NAO_INFORMADO` (quesito IBGE, autodeclaração) | `NAO_INFORMADO` representa a opção válida de não declarar; o subconjunto `{PRETA, PARDA}` é o sujeito à banca de heteroidentificação da autodeclaração (Lei 12.711/2012 art. 3º e resoluções da Unifesspa que disciplinam o procedimento) |
| `QUILOMBOLA` | Booleano | Escalar | sim/não (autodeclaração) | Lei 12.711/2012 art. 3º (red. Lei 14.723/2023) — categoria autodeclarada **distinta** de cor/raça |
| `PCD` | Booleano | Escalar | sim/não | Lei 12.711/2012 art. 3º (red. Lei 14.723/2023) c/c Lei 13.409/2016 |
| `MODALIDADE` | Categórico | Multivalorado | fonte: `Modalidade.Codigo` (cadastro vivo, `Configuracao`) | Validado no instante de configuração contra os códigos vivos, via leitor cross-módulo |
| `CONDICAO_ATENDIMENTO` | Categórico | Multivalorado | fonte: `CondicaoAtendimentoEspecializado.Codigo` | Inclui o código reservado `PCD` (ADR-0067) |
| `RENDA_PER_CAPITA` | Numérico | Escalar | limiar inteiro em **centavos de real**; valor do candidato preserva a razão `renda_familiar_centavos / numero_integrantes` | Lei 12.711/2012 art. 1º, § único (red. Lei 14.723/2023) — comparação exata em moeda (ver nota abaixo) |
| `EGRESSO_ESCOLA_PUBLICA` | Booleano | Escalar | sim/não | Lei 12.711/2012 art. 1º |
| `FAIXA_ETARIA` | Numérico | Escalar | inteiro; anos completos calculados contra `dataReferenciaFatos` congelada | Consumido pela regra `DESEMPATE-IDOSO` (Lei 10.741/2003 art. 27); nunca é calculado contra o relógio da execução |
| `SEXO` | Categórico | Escalar | `FEMININO`, `MASCULINO`, `INTERSEXO` | Fato ativo: habilita gatilhos condicionados ao sexo — ex.: exigir o documento de reservista apenas quando `SEXO = MASCULINO` (Lei 4.375/1964, Serviço Militar). Dado sensível — ver nota LGPD |

### Fato categórico de escopo-processo: validação no cadastro é de configuração; o predicado publicado congela o código

Os dois fatos categóricos de escopo-processo (`MODALIDADE`,
`CONDICAO_ATENDIMENTO`) — aqueles cujo `ValoresDominio` é nulo — resolvem
seus valores válidos a partir de cadastros vivos de `Configuracao`
(`Modalidade.Codigo`, `CondicaoAtendimentoEspecializado.Codigo`), que **são
editáveis** — códigos não reservados podem ser renomeados ou removidos. O domínio
do catálogo é apenas o primeiro filtro: o valor também precisa pertencer à
configuração do **próprio processo**. `MODALIDADE` é validado contra as modalidades
selecionadas e congeladas no processo; `CONDICAO_ATENDIMENTO`, contra as condições
ofertadas e congeladas. Um valor existente globalmente, mas ausente desse recorte,
é recusado na configuração/publicação para que não crie um predicado
insatisfazível. Isso não cria risco para predicados já publicados, por causa de
duas fronteiras que a ADR fixa explicitamente:

1. **A validação contra o cadastro vivo e contra o recorte do processo ocorre no
   instante de configuração**, antes do congelamento — só se pode citar um código
   existente e efetivamente ofertado/selecionado naquele processo. É um gate de
   *configuração*, não de *runtime*.
2. **No congelamento, o código entra no predicado por valor** (RN08): a
   `VersaoConfiguracao` publicada carrega o texto do código dentro do predicado
   serializado. A avaliação de um predicado congelado compara esse código com o
   fato declarado pelo candidato — que, no caso de atendimento, também carrega
   identidade congelada (ADR-0073) — por **igualdade de código**, sem reler o
   cadastro vivo.

Portanto, renomear ou remover (soft-delete) um código do cadastro **depois** de um
edital publicado **não invalida** o predicado congelado: ele mantém o código
antigo e continua avaliável. **Um predicado congelado nunca é revalidado contra o
cadastro vivo** — fazê-lo violaria a RN08, reinterpretando o passado com um
vocabulário que mudou. É a mesma garantia que a ADR-0073 dá aos fatos de
atendimento, estendida ao predicado que os cita.

### `FAIXA_ETARIA` usa uma data de referência congelada

Idade é um fato temporal, não uma leitura do relógio. Cada consumidor que incluir
`FAIXA_ETARIA` em uma versão publicada deve registrar no próprio snapshot a
`dataReferenciaFatos` (`date`) que justifica aquele cálculo; a idade são os anos
completos entre a data de nascimento e essa data. Para `DESEMPATE-IDOSO`, a story
do resolvedor congela a data de referência da etapa de classificação juntamente
com a regra. Uma reclassificação posterior avalia a mesma data congelada, sem
substituí-la por "hoje". A ausência de `dataReferenciaFatos` torna o predicado
temporal inválido na publicação — não há fallback implícito.

### A escala de `RENDA_PER_CAPITA` é moeda, não fração de salário mínimo

O limiar legal é "renda per capita ≤ 1 salário mínimo" (Lei 12.711/2012 art. 1º,
§ único). Representar a renda como fração inteira do SM (ex.: centésimos, `100` =
1 SM) tornaria a menor unidade igual a 1% do SM — e, como o domínio numérico
rejeita decimal, o resolvedor teria de arredondar rendas entre `1,00` e `1,01`
SM, podendo admitir quem está acima do limiar ou rejeitar quem está abaixo, no
exato ponto que decide elegibilidade. Por isso `RENDA_PER_CAPITA` é medido em
**centavos de real** (moeda, onde a renda é de fato apurada). Como a divisão pelo
número de integrantes pode produzir fração de centavo, o resolvedor não pode
materializar a renda per capita arredondada: preserva a razão
`renda_familiar_centavos / numero_integrantes` e compara-a por multiplicação
cruzada com o limiar congelado em centavos
(`sm_referencia` da regra `RENDA-PER-CAPITA-LEI-12711`, RN08). Assim, o valor
configurado continua sendo inteiro, mas a avaliação do candidato é exata e não
arredonda na fronteira legal.

**Quando o fato é resolvível — um constraint para os consumidores.** A renda per
capita é derivada dos dados/documentos de renda, que são coletados numa etapa
posterior à inscrição. Um gatilho que **exige o próprio documento comprobatório
de renda** não pode condicionar-se a `RENDA_PER_CAPITA`: seria circular (o fato só
existe depois do documento que ele deveria disparar), e o resolvedor devolveria
*desconhecido* antes de a renda existir — podendo **pular em silêncio** um
documento obrigatório. Por isso, gatilhos que exigem **prova** de baixa renda devem
condicionar-se a um fato **declarado no ato da inscrição** — a modalidade de cota
escolhida (`MODALIDADE`), disponível antes de qualquer comprovação —, reservando
`RENDA_PER_CAPITA` para a validação **pós-prova**. Esta ADR registra o constraint; a
story do gatilho de documentos exigidos o honra ao escolher o fato disparador.

### `QUILOMBOLA` é fato distinto de `COR_RACA`

O texto vigente da Lei 12.711/2012 art. 3º (red. Lei 14.723/2023) reserva vagas a
"autodeclarados pretos, pardos, indígenas **e quilombolas**" — quatro categorias
de autodeclaração, não três-mais-subcategoria-de-cor. A entidade
`ReferenciaReservaDemografica` já reflete a separação (`PpiPercentual` e
`QuilombolaPercentual` como percentuais distintos). Um gatilho que precise
expressar "candidato quilombola" (ex.: um documento de autodeclaração quilombola)
não teria como fazê-lo via `COR_RACA EM [...]` sem erro semântico — quilombola não
é uma cor/raça. Por isso `QUILOMBOLA` entra como fato booleano próprio.

### `PCD` é fato armazenado independentemente, não derivado de `CONDICAO_ATENDIMENTO`

`PCD` (booleano) e `CONDICAO_ATENDIMENTO = PCD` descrevem estados relacionados por
caminhos diferentes, mas **não são o mesmo fato**, e respondem a perguntas legais
distintas:

- `PCD` → elegibilidade às modalidades reservadas `LB_PCD`/`LI_PCD` (cota, Lei
  12.711/2012 art. 3º c/c Lei 13.409/2016), apurada por documentação/laudo.
- `CONDICAO_ATENDIMENTO = PCD` → o candidato solicitou **atendimento
  especializado** citando a condição reservada `PCD` (acessibilidade na prova,
  base LBI — Lei 13.146/2015), protegida pela ADR-0067.

Os dois eixos podem coincidir, mas podem legitimamente divergir — um candidato
elegível à cota PcD pode não solicitar atendimento especializado, e vice-versa.
Por isso `PCD` é um fato **resolvido de sua própria fonte** (a declaração/laudo de
cota do candidato), **independente** de `CONDICAO_ATENDIMENTO`. Derivar um do outro
conflataria cota com acessibilidade e faria uma solicitação de atendimento
conceder cota (ou o inverso) por construção — o que é errado. Uma verificação de
consistência *soft* entre os dois eixos pode ser acrescentada pelo leitor no
futuro, mas é validação, não identidade: o vocabulário não força equivalência. A
escolha é conservadora: enquanto nenhum predicado publicado referenciar `PCD`, a
decisão independente-vs-derivada ainda pode ser revista. A partir do primeiro
congelamento, porém, mudar a resolução de `PCD` de forma que altere o valor
observável do fato é mudança incompatível — e, como qualquer outra, exige um
**código novo** (mantendo o resolvedor antigo para o código antigo), nunca uma
revisão silenciosa do mesmo código. É a mesma regra de "como versiona".

### Nota LGPD (para as stories seguintes)

`COR_RACA`, `QUILOMBOLA`, `PCD` e `SEXO` classificam **dado sensível** (LGPD art.
5º, II — origem racial ou étnica, saúde, e, no caso de `SEXO`, dado classificado
como sensível pela especificação de inscrição). Esta decisão não move nem armazena
dado de candidato — o catálogo é metadado de classificação, sem PII. Mas o leitor que resolver o **valor real** de cada fato a
partir de um candidato lida com dado sensível, e o mascaramento em log hoje **não
o cobre**: o `PiiMaskingEnricher` (ADR-0011) mascara padrões de CPF, não valores
estruturados como `COR_RACA=PRETA` ou `PCD=true`, que passariam íntegros. Por
isso, a story que constrói esse leitor tem **duas obrigações** que esta ADR
comissiona: (a) **não registrar o valor do fato em log** (o resolvedor trata o
valor como sensível por padrão) e, se algum log de diagnóstico precisar referi-lo,
(b) **estender os controles de mascaramento/cifra** para os valores de fato
sensível antes de eles serem resolvidos — não presumir que o enricher de CPF já
os protege.

## Consequências

### Positivas

- Predicados sobre o candidato passam a ter um domínio decidível — a matriz
  operador × valor torna-se verificável, e `DESEMPATE-PREDICADO-FATO` deixa de ser
  invalidável por construção.
- Um único vocabulário serve todos os consumidores, sem duplicação nem divergência.
- Predicados congelados ficam protegidos: a identidade imutável e o crescimento
  só-aditivo garantem que o passado nunca é reinterpretado.

### Negativas

- Acrescentar um fato exige um PR de desenvolvimento (código de resolução + seed),
  não uma tela — mais fricção do que os cadastros editáveis por admin. É o custo
  deliberado de impedir fatos inoperantes.
- Manter `PCD` e `CONDICAO_ATENDIMENTO = PCD` como eixos distintos admite estados
  divergentes, que um consumidor precisa tratar conscientemente.

### Neutras

- `SEXO` é fato ativo desde já: embora nenhuma regra semeada nesta colheita o cite,
  ele habilita gatilhos legítimos condicionados ao sexo (ex.: documento de
  reservista apenas para `MASCULINO`), e o validador de predicado o trata como
  qualquer outro fato categórico — sem status especial nem allow-list.

## Confirmação

- O seed do catálogo (story seguinte) materializa, linha a linha, as nove entradas
  desta ADR; um teste confere seed contra ADR.
- A entidade `FatoCandidato` não expõe mutação de
  `Codigo`/`Dominio`/`Natureza`/`Cardinalidade`, e `ValoresDominio` é só-aditivo —
  coberto por testes de unidade.
- O validador de `PredicadoDnf` (story seguinte) aplica a matriz operador ×
  domínio desta ADR.

## Prós e contras das opções

### Cada consumidor fixa sua própria lista

- Bom, porque não exige um cadastro central.
- Ruim, porque duplica trabalho e faz `#554` e `#559` divergirem no vocabulário.

### Vocabulário central editável por administrador

- Bom, porque reusa o padrão dos outros cadastros de `Configuracao`.
- Ruim, porque permite cadastrar um fato sem código que o resolva — um fato
  inoperante, exatamente o defeito que esta ADR fecha.

### Vocabulário central seed-governado, identidade imutável

- Bom, porque todo fato tem código que o resolve e a identidade congelada protege
  predicados publicados (RN08).
- Ruim, porque exige um PR para cada fato novo — fricção deliberada.

## Mais informações

- ADR-0056 (módulo Configuração e leitor cross-módulo `IXxxReader`).
- ADR-0058 (`ObrigatoriedadeLegal` como validação data-driven tipada — precedente da **forma** fechada de um vocabulário que alimenta um avaliador; sua governança é editável por catálogo e diverge desta ADR).
- ADR-0061 (referência cross-módulo por snapshot-copy; na topologia atual da
  ADR-0097, sem FK cross-schema).
- ADR-0067 (código reservado `PCD` em `CondicaoAtendimentoEspecializado`).
- ADR-0073 (identidade congelada dos fatos de atendimento especializado).
- ADR-0097 (banco único `uniplus`, schemas por módulo e fronteiras preservadas por
  contrato e fitness tests).
- RN08 (congelamento de parâmetros por processo).
- Lei 12.711/2012 arts. 1º e 3º (red. Lei 14.723/2023), Lei 13.409/2016,
  Lei 13.146/2015, Lei 10.741/2003 art. 27, LGPD (Lei 13.709/2018) art. 5º, II.

## Emenda 1 (2026-07-22) — operadores de exclusão, semântica sobre fato multivalorado e ordem canônica

Esta emenda reconcilia a ADR com o código que a implementa. Os operadores de exclusão `DIFERENTE` e
`NAO_EM` entraram no validador e no avaliador de predicado depois que esta ADR foi aceita, e a matriz
acima ficou para trás. A emenda promove a texto de decisão o que hoje só existe em implementação e
teste, e corrige quatro afirmações desta ADR que o código contradiz.

**Trechos substituídos.** A tabela da seção "Matriz de compatibilidade operador × domínio" é
substituída por 1.1. A frase "A lista de `EM` nunca é vazia", na seção "Cardinalidade e átomo canônico
do predicado", é substituída por 1.3. A forma do valor booleano é corrigida em 1.1. A contagem de fatos
é corrigida em 1.5. O restante da ADR permanece vigente sem alteração.

### 1.1 — Matriz de compatibilidade operador × domínio

| Domínio | Operadores aceitos | Forma do valor |
|---|---|---|
| `Booleano` | `IGUAL`, `DIFERENTE` | booleano JSON (`true`/`false`) |
| `Numerico` | `IGUAL`, `DIFERENTE`, `MAIOR_IGUAL`, `MENOR_IGUAL` | número JSON **inteiro** — decimal é rejeitado |
| `Categorico` (estático ou de escopo-processo) | `IGUAL`, `DIFERENTE`, `EM`, `NAO_EM` | `IGUAL`/`DIFERENTE`: um código do domínio; `EM`/`NAO_EM`: lista de códigos do domínio |

A regra que gera a tabela: **`DIFERENTE` acompanha `IGUAL` em todo domínio onde `IGUAL` vale** —
inclusive booleano e numérico —, e **`NAO_EM` acompanha `EM`**, valendo portanto só nos dois ramos
categóricos, já que booleano e numérico nunca admitiram `EM`. A forma do valor de um operador negativo
é a mesma do positivo que ele nega: escalar para `DIFERENTE`, lista para `NAO_EM`.

A forma do valor booleano é o **booleano JSON**, não os tokens textuais `sim`/`não` que a tabela
original registrava; `sim`/`não` é vocabulário de apresentação ao candidato, e não participa nem do
predicado configurado nem da canonicalização — que também usam o booleano JSON. A restrição de que numérico é inteiro e rejeita decimal
permanece, e passa a valer também para `DIFERENTE`.

### 1.2 — Semântica dos operadores sobre fato multivalorado

A seção "Cardinalidade e átomo canônico do predicado" define `IGUAL` e `EM` sobre fato multivalorado
(`MODALIDADE`, `CONDICAO_ATENDIMENTO`). Os negativos completam a tabela e são a **negação lógica** do
positivo correspondente, não operadores com regra própria:

| Operador | Fato escalar | Fato multivalorado |
|---|---|---|
| `IGUAL X` | o valor é `X` | `X` pertence ao conjunto do candidato |
| `DIFERENTE X` | o valor não é `X` | **nenhum** elemento do conjunto é `X` |
| `EM [..]` | o valor está na lista | a interseção com a lista não é vazia |
| `NAO_EM [..]` | o valor não está na lista | a interseção com a lista **é vazia** |

A negação só se aplica quando o fato está **resolvido**. Fato ausente ou nulo avalia como
*indeterminado*, e **indeterminado nunca inverte para verdadeiro** — a ausência tem precedência sobre a
negação. Sem essa precedência, um predicado negativo passaria a ser satisfeito justamente pelos
candidatos sobre os quais nada se sabe, que é o oposto do comportamento seguro.

A incoerência de tipo entre o valor resolvido e o configurado tem tratamento **diferente** conforme a
cardinalidade, e a diferença é deliberada:

- No fato **escalar**, tipo incoerente é *indeterminado* — o predicado não sabe comparar aquilo, e
  responder "falso" confundiria "resolvido e diferente" com "resolvido em forma que não sei comparar".
- No fato **multivalorado**, a comparação é de pertinência elemento a elemento: um elemento de tipo
  diferente simplesmente **não é igual**, e não contamina o conjunto. Um `DIFERENTE` sobre conjunto
  cujos elementos são todos de outro tipo resolve *verdadeiro* — corretamente, porque de fato nenhum
  elemento é igual ao valor configurado.

### 1.3 — Lista vazia em `EM`/`NAO_EM` é aceita na forma e decidível na avaliação

Esta ADR afirmava que "a lista de `EM` nunca é vazia". **Não é o caso**, e a emenda corrige: a factory
do átomo aceita `EM []` e `NAO_EM []` como forma válida. A validação de forma não é o lugar de recusar
a lista vazia, porque a lista pode chegar vazia legitimamente de uma projeção que não selecionou nada,
e a avaliação é decidível sem ambiguidade.

Sobre um fato **resolvido**, o resultado segue da definição de interseção:

- `EM []` → **falso**: a interseção com o conjunto vazio é sempre vazia.
- `NAO_EM []` → **verdadeiro**: nada pertence ao conjunto vazio. É verdade vácua, não caso especial.

Sobre um fato **não resolvido**, ambos continuam *indeterminado*: a regra de precedência da ausência,
fixada em 1.2, prevalece sobre a semântica de lista vazia.

### 1.4 — Ordem canônica dos operadores

A canonicalização do envelope de publicação ordena os átomos de uma cláusula, e essa ordem entra no
hash congelado por RN08 — portanto precisa de **fonte única e declarada**. A ordem canônica dos
operadores é a **ordinal do seu código textual**:

```text
DIFERENTE < EM < IGUAL < MAIOR_IGUAL < MENOR_IGUAL < NAO_EM
```

Ela não é uma regra própria de operador: **decorre** da regra geral da ADR-0109 de ordenar toda coleção
sem chave de negócio natural pela própria serialização canônica do item. O átomo canônico é
`{fato, operador, valor}` com chaves ordenadas, e a comparação é ordinal sobre o texto dessa
serialização — de modo que recai primeiro sobre o fato e, no desempate, sobre o código textual do
operador. Nenhuma tabela de precedência participa disso, e é assim que o canonicalizador se comporta
hoje.

Duas fontes rivais ficam explicitamente **descartadas** como critério de ordenação:

- **A numeração do enum `Operador`** é identidade de persistência e reflete a cronologia em que os
  operadores foram introduzidos, não precedência. Ordenar por valor numérico produziria outra ordem, e
  renumerar o enum para alinhá-lo reescreveria valores já persistidos. A numeração **nunca** é peso de
  ordenação.
- **Uma ordem semântica** que agrupasse cada operador com a sua negação
  (`IGUAL < DIFERENTE < EM < NAO_EM < MAIOR_IGUAL < MENOR_IGUAL`) seria mais legível para quem audita
  um envelope, mas exigiria uma tabela de precedência específica de operador, criando exceção à regra
  uniforme de canonicalização por conteúdo e um segundo lugar a manter em sincronia com o enum.

A ordem só é observável quando dois átomos sobre o **mesmo fato** coexistem numa cláusula. Ela é
critério de determinismo, não de semântica: o que o congelamento exige dela é que seja total e estável,
não que exprima parentesco entre operadores.

### 1.5 — Correção da contagem de fatos do vocabulário

Esta ADR descreve "o vocabulário (nove fatos)". O catálogo semeado tem hoje **dezessete** fatos: os
nove originais, os dois acrescentados ao refinar origem e binding (`NACIONALIDADE` e
`TIPO_DEFICIENCIA`) e os seis da coleta de fatos de cota. A tabela dos nove permanece válida como
registro do que esta decisão semeou; a contagem total vive no seed, que é a fonte única, e não deve ser
repetida como número fixo em texto de ADR.
