---
status: "accepted"
date: "2026-07-19"
decision-makers:
  - "Tech Lead"
consulted:
  - "CEPS (dono do processo)"
informed:
  - "Equipe Seleção"
  - "Equipe Configuração"
---

# ADR-0116: Fato multi-fonte — origem, ponto de resolução, binding e `FatoValorDominio`

## Contexto e enunciado do problema

A ADR-0111 fixou o vocabulário fechado de fatos do candidato com três eixos
ortogonais ao domínio: `Natureza` (origem do dado), `Cardinalidade` e o próprio
`Dominio`. A análise dos editais reais de 2027 (change
`documentos-exigidos-cobertura-editais`, Story #917) mostrou que esse modelo
precisa de mais três capacidades para expressar requisitos documentais sem
código por-tipo: (a) um **ponto de resolução** — a fase em que o valor do fato
fica conhecido, para recusar no cadastro uma condição que cita um fato ainda
não resolvido na fase da exigência; (b) um **binding** concreto — a referência
de onde/como o valor é produzido, para que "novo fato sem código" seja real e
não apenas metadado decorativo; (c) **descrição por valor** de um domínio
categórico, para orientar a escolha do candidato em fatos `DECLARADO` (ex.:
o que é "TEA"), modelada como entidade filha `FatoValorDominio`.

Esta ADR refina a ADR-0111 nesses três pontos e ajusta a nomenclatura do eixo
de origem — sem alterar sua governança (seed-governado, sem CRUD, ADR-0111
continua valendo integralmente nesse aspecto).

## Drivers da decisão

- Um gatilho não pode citar um fato cujo valor só é conhecido numa fase
  posterior à da exigência — sem isso, a exigência ficaria condicionada a um
  dado que não existe ainda no momento em que precisaria ser avaliado.
- "Novo fato usável sem código" exige que o fato declare **de onde** seu valor
  vem (o binding), não só metadado de classificação.
- Fatos `DECLARADO` (seleção do candidato) precisam de descrição por valor —
  requisito de acessibilidade/clareza (o público inclui comunidades rurais e
  tradicionais, editais de Campo/PSIQ).

## Opções consideradas

- **Origem:** manter `NaturezaFato` como está (`BrutoInformado`/`DeVontade`/
  `Derivado`) e tratar `DERIVADO`/`DECLARADO`/`INTEGRACAO` como vocabulário só
  de documentação, com tradução implícita — **rejeitada**: cria uma camada de
  tradução permanente entre o código e o contrato/wire (fechado em PR4), sem
  benefício, e o Uni+ prefere mudar o código a manter tradução.
- **Origem:** renomear tokens sem reclassificar nenhum fato — **rejeitada**
  (achado do Codex): perderia a distinção real entre fatos computados
  (`FAIXA_ETARIA`, `RENDA_PER_CAPITA`) e fatos respondidos diretamente
  (os demais sete), que hoje colapsam sob o mesmo token `BrutoInformado` só
  porque a distinção não importava para o resolvedor até agora.
- **Binding:** string livre sem gramática — **rejeitada** (achado do Codex):
  não sustenta "sem código" de forma verificável nem barra incoerência entre
  binding e origem.
- **Binding:** shape JSON completo de wire nesta ADR — **rejeitada**: o shape
  de wire é fechado na Story #919 (PR4); esta ADR só fixa a coerência mínima
  no domínio.

## Resultado da decisão

**Origem (renomeia e reclassifica `Natureza`→`Origem`):** o enum
`NaturezaFato` (`BrutoInformado`/`DeVontade`/`Derivado`) torna-se `OrigemFato`
(`Derivado`/`Declarado`/`Integracao`), refletindo exatamente o vocabulário do
design da Story #917. `DeVontade` nunca foi semeado — é removido sem custo.
Reclassificação real dos nove fatos (não uma renomeação mecânica):

| Fato | Origem nova | Justificativa |
|---|---|---|
| `FAIXA_ETARIA` | `Derivado` | Computada a partir da data de nascimento — o motor deriva o valor, o candidato não a responde diretamente |
| `RENDA_PER_CAPITA` | `Derivado` | Computada como razão renda familiar ÷ integrantes — o fato em si é uma saída, não uma resposta direta |
| `COR_RACA`, `QUILOMBOLA`, `PCD`, `EGRESSO_ESCOLA_PUBLICA`, `SEXO`, `MODALIDADE`, `CONDICAO_ATENDIMENTO` | `Declarado` | Resposta/seleção direta do candidato no cadastro de inscrição |
| `NACIONALIDADE`, `TIPO_DEFICIENCIA` (novos) | `Declarado` | Seleção direta do candidato sobre o domínio do fato |

`Integracao` fica reservada, sem fato semeado (fonte externa futura, ex.:
SIGAA, issue #874). Esta reclassificação **não** afeta o resolvedor — a
origem nunca alimenta a avaliação do predicado (invariante que a ADR-0111 já
estabelecia para o eixo `Natureza`); o efeito é só descritivo/de binding.

**Ponto de resolução:** `FatoCandidato.PontoResolucao: string`, validado
contra `FaseCanonicaCatalogo.EhCanonico(...)` (o conjunto fechado de catorze
códigos, sem depender de linha viva em `fase_canonica` — decopla do fato de a
tabela ser povoada operacionalmente pós-deploy). Todos os fatos desta colheita
(os nove existentes + `NACIONALIDADE`/`TIPO_DEFICIENCIA`) resolvem em
`INSCRICAO`. O *gate* que recusa uma condição citando fato de fase posterior
(comparando o ponto de resolução contra a fase da exigência, via a tabela de
precedência entre fases já existente — migration
`AddPrecedenciaFaseEAtributosFaseCanonica` — nunca por posição em lista, que
não é ordenada semanticamente) é implementado na Story #916 (PR2), que
consome este campo.

**Binding:** `FatoCandidato.Binding: string`, formato `"{PREFIXO}:{REFERENCIA}"`
com prefixo coerente com a origem: `Derivado`→`ATRIBUTO_CANDIDATO:`;
`Declarado`→`CAMPO_INSCRICAO:`; `Integracao`→`INTEGRACAO:`. A factory recusa
prefixo incoerente com a origem declarada. O shape de wire completo (JSON
estruturado por origem) é fechado na Story #919 (PR4); aqui fixa-se só a
coerência mínima que sustenta a promessa de "sem código".

**`FatoValorDominio`:** entidade filha de `FatoCandidato`
(`Id, FatoCandidatoId, Codigo, Descricao, Ordem, Ativo`), unicidade
`(FatoCandidatoId, Codigo)` (código normalizado trim, comparação ordinal),
`Descricao` obrigatória quando o fato pai é `Declarado`. Adicionada só pelo
agregado `FatoCandidato.AdicionarValorDominio(...)` — o único que conhece a
`Origem` do pai (para exigir descrição) e os irmãos já adicionados (para a
unicidade). Substitui o array `jsonb ValoresDominio` **somente** para os fatos
categóricos **estáticos** desta leva (`COR_RACA`, `SEXO`, `NACIONALIDADE`).
Fatos categóricos de **escopo-processo** (`MODALIDADE`, `CONDICAO_ATENDIMENTO`,
`TIPO_DEFICIENCIA`) continuam sem `ValoresDominio`/`FatoValorDominio` — seu
domínio vem de cadastro vivo + projeção do processo, nunca duplicado no
catálogo de fatos ("não dois catálogos": a fonte de `TIPO_DEFICIENCIA` é o
cadastro `TipoDeficiencia` já existente, projetado por `tipoDeficienciaIds`,
mesmo mecanismo de `OfertaAtendimentoEspecializado`/`OfertaTipoDeficiencia`).

**`TipoDeficiencia` (cadastro CRUD existente em Configuração) ganha:**

- `Permanente: bool?` (nullable — `null` = ainda não classificado pelo CEPS,
  distinto de `false` = classificado como não-permanente; a taxonomia
  concreta é refinamento residual, task 0.1 da change, e não bloqueia este
  modelo). `NOT NULL` fica para quando a classificação estiver completa.
- `Descricao` passa de opcional a **obrigatória** — agora serve também como a
  descrição por valor exigida pela spec para o fato `TIPO_DEFICIENCIA`
  (`Declarado`), exposta via `ITipoDeficienciaReader`/`TipoDeficienciaView`.

## Consequências

### Positivas

- O eixo de origem no código passa a usar exatamente o vocabulário do
  contrato (`DERIVADO`/`DECLARADO`/`INTEGRACAO`), eliminando uma tradução
  permanente entre modelo interno e wire.
- `PontoResolucao`/`Binding` tornam "novo fato sem código" verificável, não
  apenas descritivo.
- `FatoValorDominio` fecha a lacuna de descrição por valor sem duplicar
  catálogo para fatos de escopo-processo.

### Negativas

- Reclassificar `FAIXA_ETARIA`/`RENDA_PER_CAPITA` como `Derivado` é uma
  migration de dados (não só rename) — exige `UpdateData` explícito por linha.
- `TipoDeficiencia.Descricao` obrigatória é breaking para qualquer linha
  existente sem descrição — aceitável só por não haver dado real em produção
  (pré-produção).

### Neutras

- `Integracao` fica sem fato semeado até a integração SIGAA (#874) existir.

## Confirmação

- Teste de unidade cobre a reclassificação de origem dos nove fatos contra a
  tabela desta ADR.
- Teste de unidade cobre a coerência prefixo-de-binding × origem (recusa
  binding com prefixo incoerente).
- Teste de integração confere o seed do catálogo (11 fatos) contra esta ADR,
  incluindo `PontoResolucao`/`Binding`/`FatoValorDominio`.

## Mais informações

- ADR-0111 (vocabulário fechado de fatos do candidato — continua vigente para
  domínio, cardinalidade e governança seed-only; esta ADR refina só origem,
  ponto de resolução, binding e descrição por valor).
- ADR-0056 (leitor cross-módulo `IXxxReader`).
- ADR-0061 (snapshot-copy cross-módulo).
- Change OpenSpec `documentos-exigidos-cobertura-editais`, Story
  [#917](https://github.com/unifesspa-edu-br/uniplus-api/issues/917).

## Emenda 1 (2026-07-22) — `MODALIDADE` passa a derivado e o binding admite mais de um prefixo por origem

Esta emenda registra decisões **prospectivas**: o código ainda classifica `MODALIDADE` como `Declarado`
com binding `CAMPO_INSCRICAO:MODALIDADE`, e a factory de `FatoCandidato` ainda aceita um único prefixo
por origem. A emenda é pré-requisito das stories que implementam a derivação — decide antes, para que o
código não nasça contradizendo a decisão registrada.

**Trechos substituídos.** A tabela de origem dos fatos, na seção "Resultado da decisão", passa a
classificar `MODALIDADE` como `Derivado` (1.1). A gramática de binding da mesma seção é substituída por
1.2. Na ADR-0111, o parágrafo que atribui ao eixo de origem a sustentação da invariante "um fato
derivado não é insumo de si mesmo nem de um gatilho avaliado antes da etapa que o produz" é substituído
por 1.4. As demais decisões de ambas permanecem vigentes.

### 1.1 — `MODALIDADE` passa de `Declarado` a `Derivado`, mantendo o código

O desenho da coleta de fatos do candidato mostrou que `MODALIDADE` não é resposta do candidato: ele
declara fatos (cor/raça, egresso de escola pública, deficiência, renda) e manifesta opt-ins de
concorrência, e o **conjunto de modalidades resulta** da avaliação dessas declarações contra as regras
congeladas do processo. Mantê-la como `Declarado` obrigaria a tela a perguntar "em qual modalidade você
concorre?" — pergunta que a legislação de ações afirmativas não delega ao candidato, e que produziria
inscrição inválida sempre que a resposta divergisse dos fatos declarados.

`MODALIDADE` passa a `Derivado` mantendo o mesmo código — sem `MODALIDADE_V2` nem código novo. Isso
exige reconciliar a regra de imutabilidade da ADR-0111, que declara `Codigo`, `Dominio`, `Origem` e
`Cardinalidade` imutáveis **desde a semeadura**, exigindo código novo para mudança incompatível.

**A emenda revoga esse marco e o substitui por outro:** os quatro eixos tornam-se imutáveis **a partir
da primeira publicação de um processo seletivo que cite o fato**. Não é releitura da regra antiga — é
troca deliberada, e vale a pena declarar por que.

A regra existe para uma finalidade única e declarada na própria ADR-0111: proteger **predicado já
publicado**, que carrega o código por valor e seria reinterpretado com outra semântica se o significado
do código mudasse sob ele. Não existe predicado publicado — o sistema está em pré-produção e o
congelamento só ocorre no ato de publicar, o que nunca ocorreu. Não há passado a preservar, e criar um
código novo apenas para satisfazer a letra da regra deixaria `MODALIDADE` como lixo permanente no
vocabulário, com um sucessor idêntico ao lado. Depois da primeira publicação, a regra da ADR-0111 volta
a valer integralmente, incluindo a exigência de código novo para mudança incompatível.

O marco **não exige mecanismo em runtime**, e não é omissão. A entidade `FatoCandidato` já é imutável
nesses eixos por construção (propriedades sem mutador público), e ela continua assim: a reclassificação
de um fato não acontece por chamada de domínio, mas por alteração do seed e migration — operação de
desenvolvimento, sujeita a revisão de PR. O marco governa **o que um PR pode fazer**, e é aí que ele é
verificável: enquanto não houver publicação, a reclassificação é legítima; depois, o PR que a tentasse
estaria violando decisão registrada.

A reclassificação não altera o avaliador — ver 1.4.

### 1.2 — O mapa origem → prefixo de binding deixa de ser bijeção

A gramática fixada por esta ADR associa **exatamente um** prefixo a cada origem
(`Derivado`→`ATRIBUTO_CANDIDATO:`, `Declarado`→`CAMPO_INSCRICAO:`, `Integracao`→`INTEGRACAO:`), e a
factory recusa qualquer outro. É estreito demais para `Derivado`, que passa a ter **dois mecanismos
distintos** de produção de valor:

| Origem | Prefixos aceitos | Referência |
|---|---|---|
| `Derivado` | `ATRIBUTO_CANDIDATO:` | atributo do candidato do qual o valor é computado — o mecanismo de `FAIXA_ETARIA` e `RENDA_PER_CAPITA` |
| `Derivado` | `REGRA_DERIVACAO:` | código do próprio fato, cuja regra de derivação vive na configuração congelada do processo |
| `Declarado` | `CAMPO_INSCRICAO:` | campo do formulário de inscrição |
| `Integracao` | `INTEGRACAO:` | sistema externo de origem |

`MODALIDADE` passa a `REGRA_DERIVACAO:MODALIDADE`.

A divisão de responsabilidade que isso expressa: **o catálogo de fatos é global e diz o mecanismo; a
configuração do processo diz o conteúdo.** A matriz de regras que decide quais modalidades um perfil
alcança é parâmetro de edital, congelado por RN08 junto com o resto da configuração, não entrada de um
catálogo compartilhado por todos os processos. Um binding que apontasse para a matriz tornaria o
catálogo global dependente de configuração por edital; apontar para o **código do fato** mantém a
referência estável e resolve a matriz onde ela de fato vive.

A validação continua recusando prefixo incoerente com a origem — `REGRA_DERIVACAO:` em fato `Declarado`
ou `Integracao` é recusado como sempre foi. O que muda é que a coerência passa a ser verificada contra
um **conjunto** de prefixos aceitos por origem, e a mensagem de erro deixa de anunciar um prefixo único
como o esperado.

Não há restrição declarativa de banco a ajustar: os CHECKs de `rol_de_fatos_candidato` cobrem
cardinalidade, domínio, origem e coerência de valores de domínio — o binding é validado no agregado.

### 1.3 — `AC_PCD` é a identidade da modalidade de pessoa com deficiência fora da reserva federal

A modalidade de pessoa com deficiência que concorre fora da reserva da Lei 12.711/2012 tem **`AC_PCD`**
como código canônico. O termo `V`, usado nos editais, é rótulo de apresentação e vive na `Descricao` da
modalidade cadastrada ("Ampla Concorrência – Pessoa com Deficiência (V)").

**Não haverá alias.** O formato de `CodigoModalidade` aceita `V` como código sintaticamente válido, e
continuará aceitando — a recusa não é sintática. Ela vem do **domínio congelado da configuração**: um
código de modalidade que não pertença ao conjunto ofertado e congelado naquele processo é recusado com
erro nomeado, nunca traduzido para o código canônico. É a mesma disciplina que o decodificador de wire
aplica: recusar, jamais normalizar. Um tradutor de alias criaria duas grafias para a mesma identidade,
e a que entrasse no envelope congelado dependeria de qual caminho de código a produziu.

Isso vale para todo código de modalidade, e não introduz lista global fixa: o domínio de `MODALIDADE`
continua sendo de escopo-processo, resolvido contra a oferta congelada, como a ADR-0111 fixa.

### 1.4 — Quem faz cumprir a ordem de resolução é o ponto de resolução, não a origem

A ADR-0111 atribui ao eixo de origem a sustentação da invariante de que um fato derivado não é insumo
de si mesmo nem de um gatilho avaliado antes da etapa que o produz. Essa atribuição fica **superada**:
quem faz cumprir a ordem temporal é o `PontoResolucao` introduzido por esta ADR, comparado contra a
fase da exigência pela tabela de precedência entre fases — e é assim que o código a implementa.

A origem passa a ser **puramente descritiva**: diz de onde o valor vem e qual binding é coerente, e
**nunca** entra na avaliação de um predicado. É por isso que reclassificar `MODALIDADE` de `Declarado`
para `Derivado` não muda nenhum resultado de avaliação — só muda o mecanismo pelo qual o valor passa a
ser produzido.

A invariante de que um fato derivado não é insumo de si mesmo continua valendo, mas por outro
mecanismo: a lista **explícita** de dependências do fato derivado, validada como grafo acíclico no
cadastro e congelada na publicação.

### 1.5 — O que a regra de derivação precisa ser para que este binding signifique algo

`REGRA_DERIVACAO:{codigoDoFato}` só é uma promessa cumprida se a regra apontada for **determinística e
congelável**. Hoje a configuração de modalidade guarda critérios como lista de texto livre, que não
serve: texto livre não é avaliável, não tem vocabulário fechado e não pode ser congelado com garantia
de que a mesma entrada produz a mesma saída.

Esta ADR fixa, portanto, o que a story de implementação deve entregar junto com o prefixo, sob pena de
o binding ficar decorativo — exatamente o defeito que a ADR-0111 existe para impedir:

1. **Regra tipada** sobre o vocabulário fechado de fatos, na forma `{quando, contribui}`, em que
   `quando` é um predicado em forma normal disjuntiva de átomos completos, sem referência de uma regra
   a outra. Regra incondicional é a disjunção **vazia**, não um literal textual.
2. **Agregação declarada**: o valor do fato derivado é a **união** das contribuições de todas as regras
   cujo `quando` é verdadeiro. A união é idempotente e comutativa, o que torna o resultado independente
   da ordem de avaliação; nenhuma regra aplicável produz o conjunto vazio.
3. **Dependências explícitas**, não inferidas do texto da regra — é o que alimenta o grafo acíclico
   de 1.4.
4. **Recorte pela oferta congelada** do processo como último passo, e não como propriedade da regra.
5. **Congelamento**: a regra entra no snapshot da publicação junto com o vocabulário e a versão do
   interpretador que a avalia, de modo que uma reavaliação futura produza o mesmo resultado (RN08).

Sem esses cinco elementos, o binding não deve ser adotado — é preferível manter o fato como
`Declarado` a registrar uma origem que o código não sustenta.
